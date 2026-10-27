#include "Shared.h"
#include "detours.h"
#include <tlhelp32.h>
#include <iostream>
#include <stdexcept>

struct Handle {
    HANDLE value;
    explicit Handle(HANDLE h) : value(h) {}
    ~Handle() { if (value && value != INVALID_HANDLE_VALUE) CloseHandle(value); }
    Handle(const Handle&) = delete;
};
static void Check(bool ok, const char* message) {
    if (!ok) throw std::runtime_error(std::string(message) + "; Win32=" + std::to_string(GetLastError()));
}
static BYTE* ModuleBase(DWORD pid, const wchar_t* name, DWORD* size = nullptr) {
    Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid));
    MODULEENTRY32W entry{}; entry.dwSize = sizeof(entry);
    if (Module32FirstW(snapshot.value, &entry)) do {
        if (_wcsicmp(entry.szModule, name) == 0) { if (size) *size = entry.modBaseSize; return entry.modBaseAddr; }
    } while (Module32NextW(snapshot.value, &entry));
    return nullptr;
}
template<typename T> static T ReadImage(HANDLE process, BYTE* base, DWORD size, uint64_t offset) {
    Check(offset <= size && sizeof(T) <= size - offset, "remote image range");
    T result{}; SIZE_T read = 0;
    Check(ReadProcessMemory(process, base + offset, &result, sizeof(T), &read) && read == sizeof(T), "read remote image");
    return result;
}
static DWORD InitializationRva(HANDLE process, BYTE* base, DWORD size, const IMAGE_NT_HEADERS& headers) {
    const auto& directory = headers.OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
    auto exports = ReadImage<IMAGE_EXPORT_DIRECTORY>(process, base, size, directory.VirtualAddress);
    Check(exports.NumberOfNames <= 65536 && exports.NumberOfFunctions <= 65536, "remote export count");
    for (DWORD i = 0; i < exports.NumberOfNames; ++i) {
        DWORD nameRva = ReadImage<DWORD>(process, base, size, static_cast<uint64_t>(exports.AddressOfNames) + i * sizeof(DWORD));
        char name[64]{};
        for (size_t j = 0; j < sizeof(name) - 1; ++j) {
            name[j] = ReadImage<char>(process, base, size, static_cast<uint64_t>(nameRva) + j);
            if (!name[j]) break;
        }
        if (strcmp(name, "InitializeProcessMonitor") != 0) continue;
        WORD ordinal = ReadImage<WORD>(process, base, size, static_cast<uint64_t>(exports.AddressOfNameOrdinals) + i * sizeof(WORD));
        Check(ordinal < exports.NumberOfFunctions, "remote export ordinal");
        DWORD rva = ReadImage<DWORD>(process, base, size, static_cast<uint64_t>(exports.AddressOfFunctions) + ordinal * sizeof(DWORD));
        Check(rva > 0 && rva < size && !(rva >= directory.VirtualAddress
            && static_cast<uint64_t>(rva) < static_cast<uint64_t>(directory.VirtualAddress) + directory.Size), "invalid or forwarded initialization export");
        return rva;
    }
    SetLastError(ERROR_PROC_NOT_FOUND); Check(false, "remote initialization export missing"); return 0;
}
static DWORD RemoteCall(HANDLE process, LPTHREAD_START_ROUTINE function, void* argument) {
    Handle thread(CreateRemoteThread(process, nullptr, 0, function, argument, 0, nullptr));
    Check(thread.value != nullptr, "create initialization thread");
    Check(WaitForSingleObject(thread.value, 10000) == WAIT_OBJECT_0, "initialization timeout; module remains resident");
    DWORD result = 0; Check(GetExitCodeThread(thread.value, &result), "initialization result"); return result;
}
static void Attach(DWORD pid, uint64_t created, const std::wstring& dll) {
    Handle process(OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, FALSE, pid));
    Check(process.value != nullptr, "open target; check permissions");
    Check(ProcessCreationTime(process.value) == created, "target identity changed");
    USHORT machine{}, native{};
    Check(IsWow64Process2(process.value, &machine, &native), "target architecture");
    const USHORT effective = machine ? machine : native;
    Check(effective == (sizeof(void*) == 8 ? IMAGE_FILE_MACHINE_AMD64 : IMAGE_FILE_MACHINE_I386), "architecture mismatch");
    const auto filename = dll.substr(dll.find_last_of(L"\\/") + 1);
    DWORD remoteSize = 0;
    BYTE* remoteModule = ModuleBase(pid, filename.c_str(), &remoteSize);
    if (!remoteModule) {
        auto loader = GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW"); HMODULE owner{};
        Check(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(loader), &owner), "loader owner");
        wchar_t ownerPath[MAX_PATH]{}; GetModuleFileNameW(owner, ownerPath, MAX_PATH);
        std::wstring ownerName(ownerPath); ownerName = ownerName.substr(ownerName.find_last_of(L"\\") + 1);
        BYTE* remoteOwner = ModuleBase(pid, ownerName.c_str()); Check(remoteOwner != nullptr, "remote loader module");
        SIZE_T size = (dll.size() + 1) * sizeof(wchar_t), written = 0;
        void* remotePath = VirtualAllocEx(process.value, nullptr, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        Check(remotePath != nullptr, "allocate module path");
        if (!WriteProcessMemory(process.value, remotePath, dll.c_str(), size, &written) || written != size) {
            VirtualFreeEx(process.value, remotePath, 0, MEM_RELEASE); Check(false, "write module path");
        }
        auto remoteLoader = reinterpret_cast<LPTHREAD_START_ROUTINE>(remoteOwner + (reinterpret_cast<BYTE*>(loader) - reinterpret_cast<BYTE*>(owner)));
        // On timeout retain the path: the target thread may still be using it.
        RemoteCall(process.value, remoteLoader, remotePath);
        VirtualFreeEx(process.value, remotePath, 0, MEM_RELEASE);
        remoteModule = ModuleBase(pid, filename.c_str(), &remoteSize); Check(remoteModule != nullptr, "load capture module");
    }
    HMODULE local = LoadLibraryExW(dll.c_str(), nullptr, DONT_RESOLVE_DLL_REFERENCES);
    Check(local != nullptr, "read capture exports");
    auto initialize = GetProcAddress(local, "InitializeProcessMonitor");
    if (!initialize) { FreeLibrary(local); Check(false, "missing initialization export"); }
    const auto* localDos = reinterpret_cast<const IMAGE_DOS_HEADER*>(local);
    const auto localHeaders = *reinterpret_cast<const IMAGE_NT_HEADERS*>(reinterpret_cast<const BYTE*>(local) + localDos->e_lfanew);
    const auto localRva = static_cast<DWORD>(reinterpret_cast<BYTE*>(initialize) - reinterpret_cast<BYTE*>(local));
    FreeLibrary(local);
    const auto remoteDos = ReadImage<IMAGE_DOS_HEADER>(process.value, remoteModule, remoteSize, 0);
    Check(remoteDos.e_magic == IMAGE_DOS_SIGNATURE && remoteDos.e_lfanew >= 0, "remote PE header");
    const auto remoteHeaders = ReadImage<IMAGE_NT_HEADERS>(process.value, remoteModule, remoteSize, remoteDos.e_lfanew);
    Check(remoteHeaders.Signature == IMAGE_NT_SIGNATURE, "remote PE signature");
    DWORD remoteRva = InitializationRva(process.value, remoteModule, remoteSize, remoteHeaders);
    if (localHeaders.FileHeader.TimeDateStamp != remoteHeaders.FileHeader.TimeDateStamp
        || localHeaders.OptionalHeader.SizeOfImage != remoteHeaders.OptionalHeader.SizeOfImage || localRva != remoteRva) {
        SetLastError(ERROR_REVISION_MISMATCH); Check(false, "capture module build mismatch; restart target after updating components");
    }
    auto remoteInitialize = reinterpret_cast<LPTHREAD_START_ROUTINE>(remoteModule + remoteRva);
    DWORD result = RemoteCall(process.value, remoteInitialize, nullptr);
    if (result != ERROR_SUCCESS) { SetLastError(result); Check(false, "install capture hooks"); }
}
int wmain(int argc, wchar_t** argv) {
    try {
        if (argc == 5 && std::wstring(argv[1]) == L"--launch") {
            STARTUPINFOW startup{}; startup.cb = sizeof(startup); PROCESS_INFORMATION child{};
            std::wstring command = L"\"" + std::wstring(argv[2]) + L"\" " + argv[3];
            Check(CreateProcessW(argv[2], command.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED, nullptr, nullptr, &startup, &child), "create suspended target");
            Handle process(child.hProcess), thread(child.hThread);
            bool resumed = false;
            try {
                std::cout << child.dwProcessId << '|' << ProcessCreationTime(child.hProcess) << std::endl;
                std::string line;
                Check(static_cast<bool>(std::getline(std::cin, line)) && line == "START", "startup cancelled");
                char dll[MAX_PATH]{}; BOOL substituted = FALSE;
                Check(WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, argv[4], -1, dll, MAX_PATH, nullptr, &substituted) != 0 && !substituted, "capture module path cannot be represented");
                LPCSTR modules[] = {dll};
                Check(DetourUpdateProcessWithDll(child.hProcess, modules, 1), "prepare startup instrumentation");
                Check(ResumeThread(child.hThread) != static_cast<DWORD>(-1), "resume target");
                resumed = true; return 0;
            } catch (...) {
                if (!resumed) TerminateProcess(child.hProcess, ERROR_CANCELLED);
                throw;
            }
        }
        if (argc != 5 || std::wstring(argv[1]) != L"--attach") return 2;
        Attach(static_cast<DWORD>(std::stoul(argv[2])), std::stoull(argv[3]), argv[4]);
        std::cout << "ATTACHED" << std::endl; return 0;
    } catch (const std::exception& error) { std::cerr << error.what() << std::endl; return 1; }
}
