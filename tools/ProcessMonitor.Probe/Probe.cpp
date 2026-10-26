#include "ProbeShared.h"
#include "detours.h"
#include <tlhelp32.h>
#include <string>
#include <vector>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <shellapi.h>

static std::wstring ReportPath() {
    wchar_t executable[MAX_PATH]{}; GetModuleFileNameW(nullptr, executable, MAX_PATH);
    const std::wstring path(executable);
    return path.substr(0, path.find_last_of(L"\\") + 1) + L"probe-result.txt";
}

static void Require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(std::string(message) + "; Win32=" + std::to_string(GetLastError()));
}
struct Handle {
    HANDLE value = nullptr;
    explicit Handle(HANDLE h = nullptr) : value(h) {}
    ~Handle() { if (value && value != INVALID_HANDLE_VALUE) CloseHandle(value); }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
};
static std::wstring EventName(const std::wstring& map, const wchar_t* suffix) { return map + suffix; }

static HANDLE OpenSerial(const std::wstring& name) {
    HANDLE h = CreateFileW((L"\\\\.\\" + name).c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    Require(h != INVALID_HANDLE_VALUE, "open test serial port");
    DCB dcb{}; dcb.DCBlength = sizeof(dcb);
    Require(GetCommState(h, &dcb), "get DCB");
    dcb.BaudRate = 115200; dcb.ByteSize = 8; dcb.Parity = NOPARITY; dcb.StopBits = ONESTOPBIT;
    dcb.fBinary = TRUE; dcb.fOutxCtsFlow = FALSE; dcb.fOutxDsrFlow = FALSE;
    dcb.fOutX = FALSE; dcb.fInX = FALSE; dcb.fDtrControl = DTR_CONTROL_ENABLE; dcb.fRtsControl = RTS_CONTROL_ENABLE;
    Require(SetCommState(h, &dcb), "set DCB");
    COMMTIMEOUTS timeouts{}; timeouts.ReadTotalTimeoutConstant = 2000; timeouts.WriteTotalTimeoutConstant = 2000;
    Require(SetCommTimeouts(h, &timeouts), "set timeouts");
    Require(PurgeComm(h, PURGE_RXCLEAR | PURGE_TXCLEAR), "clear dedicated test ports");
    return h;
}

static int Worker(const std::wstring& target, const std::wstring& peer) {
    wchar_t mapName[128]{};
    Require(GetEnvironmentVariableW(L"FLEXCOM_PROBE_MAP", mapName, 128) != 0, "worker environment");
    Handle mapping(OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, mapName));
    auto* shared = static_cast<ProbeShared*>(MapViewOfFile(mapping.value, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(ProbeShared)));
    Require(shared != nullptr, "worker mapping");
    Handle ready(OpenEventW(EVENT_MODIFY_STATE, FALSE, EventName(mapName, L"-ready").c_str()));
    Handle go(OpenEventW(SYNCHRONIZE, FALSE, EventName(mapName, L"-go").c_str()));
    Handle targetPort(target == L"none" ? nullptr : OpenSerial(target));
    Handle peerPort(peer == L"none" ? nullptr : OpenSerial(peer));
    shared->targetHandle = reinterpret_cast<ULONG_PTR>(targetPort.value);
    SetEvent(ready.value);
    Require(WaitForSingleObject(go.value, 15000) == WAIT_OBJECT_0, "worker go timeout");
    DWORD count = 0;
    // The non-serial exclusion check uses NUL (a non-serial character device).
    Handle nul(CreateFileW(L"NUL", GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr));
    SetLastError(12345);
    Require(WriteFile(nul.value, "not serial", 10, &count, nullptr) && count == 10, "nonserial I/O changed");
    if (targetPort.value) {
        const BYTE tx[] = {0, 1, 2, 0x7F, 0x80, 0xFE, 0xFF};
        BYTE received[sizeof(tx)]{};
        Require(WriteFile(targetPort.value, tx, sizeof(tx), &count, nullptr) && count == sizeof(tx), "target write");
        Require(ReadFile(peerPort.value, received, sizeof(received), &count, nullptr) && count == sizeof(tx)
            && memcmp(tx, received, sizeof(tx)) == 0, "peer receive");
        Require(WriteFile(peerPort.value, tx, sizeof(tx), &count, nullptr) && count == sizeof(tx), "peer write");
        Require(ReadFile(targetPort.value, received, sizeof(received), &count, nullptr) && count == sizeof(tx)
            && memcmp(tx, received, sizeof(tx)) == 0, "target receive");
        Require(WriteFile(targetPort.value, tx, 0, &count, nullptr) && count == 0, "zero write");
    }
    InterlockedExchange(&shared->workerResult, 1);
    UnmapViewOfFile(shared);
    return 0;
}

static void InjectControlledWorker(const PROCESS_INFORMATION& process, const std::wstring& dll) {
    // Resolve the LoadLibraryW owner module and relative offset, rather than assuming equal ASLR bases.
    const auto loadLibrary = GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW");
    HMODULE owner{};
    Require(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(loadLibrary), &owner), "LoadLibrary owner");
    wchar_t ownerPath[MAX_PATH]{}; GetModuleFileNameW(owner, ownerPath, MAX_PATH);
    const std::wstring ownerName = std::wstring(ownerPath).substr(std::wstring(ownerPath).find_last_of(L"\\") + 1);
    Handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, process.dwProcessId));
    MODULEENTRY32W entry{}; entry.dwSize = sizeof(entry);
    BYTE* remoteBase = nullptr;
    if (Module32FirstW(snapshot.value, &entry)) do {
        if (_wcsicmp(entry.szModule, ownerName.c_str()) == 0) { remoteBase = entry.modBaseAddr; break; }
    } while (Module32NextW(snapshot.value, &entry));
    Require(remoteBase != nullptr, "find remote loader module");
    const SIZE_T size = (dll.size() + 1) * sizeof(wchar_t);
    void* remote = VirtualAllocEx(process.hProcess, nullptr, size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    Require(remote != nullptr, "allocate DLL path");
    SIZE_T written = 0;
    if (!WriteProcessMemory(process.hProcess, remote, dll.c_str(), size, &written) || written != size) {
        VirtualFreeEx(process.hProcess, remote, 0, MEM_RELEASE);
        Require(false, "write DLL path");
    }
    auto remoteLoader = reinterpret_cast<LPTHREAD_START_ROUTINE>(remoteBase +
        (reinterpret_cast<BYTE*>(loadLibrary) - reinterpret_cast<BYTE*>(owner)));
    Handle thread(CreateRemoteThread(process.hProcess, nullptr, 0, remoteLoader, remote, 0, nullptr));
    Require(thread.value != nullptr, "inject controlled worker");
    // On timeout retain the argument allocation; the worker owns its process memory until exit.
    Require(WaitForSingleObject(thread.value, 10000) == WAIT_OBJECT_0, "loader timeout");
    VirtualFreeEx(process.hProcess, remote, 0, MEM_RELEASE);
}

static void Run(const std::wstring& target, const std::wstring& peer, bool attach, bool instrument) {
    wchar_t executable[MAX_PATH]{}; GetModuleFileNameW(nullptr, executable, MAX_PATH);
    const std::wstring path(executable);
    const std::wstring dll = path.substr(0, path.find_last_of(L"\\") + 1) + L"FlexComProbeHook.dll";
    const std::wstring mapName = L"Local\\FlexComProbe-" + std::to_wstring(GetCurrentProcessId()) + L"-" + std::to_wstring(GetTickCount64());
    Handle mapping(CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(ProbeShared), mapName.c_str()));
    auto* shared = static_cast<ProbeShared*>(MapViewOfFile(mapping.value, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(ProbeShared)));
    Require(shared != nullptr, "parent mapping");
    shared->version = ProbeVersion;
    Handle ready(CreateEventW(nullptr, TRUE, FALSE, EventName(mapName, L"-ready").c_str()));
    Handle go(CreateEventW(nullptr, TRUE, FALSE, EventName(mapName, L"-go").c_str()));
    SetEnvironmentVariableW(L"FLEXCOM_PROBE_MAP", mapName.c_str());
    std::wstring command = L"\"" + path + L"\" --worker " + target + L" " + peer;
    STARTUPINFOW startup{}; startup.cb = sizeof(startup); PROCESS_INFORMATION process{};
    const BOOL created = CreateProcessW(path.c_str(), command.data(), nullptr, nullptr, FALSE,
        CREATE_SUSPENDED | CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process);
    SetEnvironmentVariableW(L"FLEXCOM_PROBE_MAP", nullptr);
    Require(created, "create controlled worker");
    Handle processHandle(process.hProcess), threadHandle(process.hThread);
    if (instrument && !attach) {
        char dllUtf8[MAX_PATH]{};
        Require(WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, dll.c_str(), -1, dllUtf8, MAX_PATH, nullptr, nullptr) != 0, "DLL path conversion");
        LPCSTR dlls[] = {dllUtf8};
        if (!DetourUpdateProcessWithDll(process.hProcess, dlls, 1)) {
            // Only terminate our own suspended laboratory worker on failed initialization.
            TerminateProcess(process.hProcess, 2);
            Require(false, "startup instrumentation");
        }
    }
    ResumeThread(process.hThread);
    Require(WaitForSingleObject(ready.value, 10000) == WAIT_OBJECT_0, "worker ready timeout");
    if (instrument && attach) InjectControlledWorker(process, dll);
    SetEvent(go.value);
    Require(WaitForSingleObject(process.hProcess, 10000) == WAIT_OBJECT_0, "worker exit timeout");
    DWORD exitCode = 1; GetExitCodeProcess(process.hProcess, &exitCode);
    Require(exitCode == 0 && shared->workerResult == 1, "worker result");
    if (instrument) Require(shared->attached == 1, "Hook initialization");
    Require(shared->dropped == 0, "probe record overflow");
    if (target == L"none" || !instrument) Require(shared->count == 0, "nonserial exclusion");
    else {
        Require(shared->count == 5, "expected five synchronous serial completions");
        int reads = 0, writes = 0;
        const BYTE expected[] = {0, 1, 2, 0x7F, 0x80, 0xFE, 0xFF};
        for (LONG i = 0; i < shared->count; ++i) {
            const auto& record = shared->records[i];
            Require(record.succeeded && record.completed == record.requested, "completion lengths");
            Require(record.completed == 0 || (record.completed == sizeof(expected)
                && memcmp(record.data, expected, sizeof(expected)) == 0), "captured bytes");
            if (record.handle == shared->targetHandle) { if (record.direction) ++reads; else ++writes; }
        }
        Require(reads == 1 && writes == 2, "target preopened handle capture");
    }
    std::ofstream report(ReportPath(), std::ios::app);
    report << "mode=" << (instrument ? (attach ? "attach" : "startup") : "baseline")
        << " pointerBits=" << sizeof(void*) * 8 << " records=" << shared->count << " dropped=" << shared->dropped << '\n';
    for (LONG i = 0; i < shared->count; ++i) {
        const auto& record = shared->records[i];
        report << "handle=" << record.handle << " direction=" << record.direction << " requested=" << record.requested
            << " completed=" << record.completed << " succeeded=" << record.succeeded << " lastError=" << record.error
            << " fileTime=" << record.completionTime << " bytes=";
        for (DWORD n = 0; n < record.completed && n < PayloadCapacity; ++n) report << std::hex << static_cast<int>(record.data[n]) << ' ';
        report << std::dec << '\n';
    }
    UnmapViewOfFile(shared);
}

int wmain(int argc, wchar_t** argv) {
    try {
        if (argc == 4 && std::wstring(argv[1]) == L"--worker") return Worker(argv[2], argv[3]);
        if (argc == 2 && std::wstring(argv[1]) == L"--self-test") {
            Run(L"none", L"none", false, true); Run(L"none", L"none", true, true);
            std::cout << "PASS: startup/attach Hook initialization and nonserial exclusion; no serial hardware tested.\n";
            return 0;
        }
        if (argc == 4 && std::wstring(argv[1]) == L"--serial") {
            Require(_wcsicmp(argv[2], argv[3]) != 0, "use distinct dedicated paired ports");
            Run(argv[2], argv[3], false, false);
            Run(argv[2], argv[3], false, true);
            Run(argv[2], argv[3], true, true);
            std::cout << "PASS: baseline/startup/attach, preopened handles, bidirectional payload, zero length, nonserial exclusion.\n";
            return 0;
        }
        std::cout << "Usage: --self-test | --serial COM10 COM20 (dedicated connected ports only).\n";
        return 2;
    } catch (const std::exception& error) {
        std::cerr << "FAIL: " << error.what() << '\n';
        std::ofstream(ReportPath(), std::ios::app) << "FAIL: " << error.what() << '\n';
        return 1;
    }
}

// GUI entry is useful on an isolated machine reached only through Remote Desktop.
// It invokes the same controlled tests, never a shell or a command interpreter.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    int argc = 0;
    auto argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argc > 1) { const int result = wmain(argc, argv); LocalFree(argv); return result; }
    LocalFree(argv);
    if (MessageBoxW(nullptr, L"Test only the dedicated COM10 / COM20 pair. Close other users of those ports.\nRun baseline, startup Hook and attach Hook tests?",
        L"FlexCom API Hook feasibility probe", MB_OKCANCEL | MB_ICONINFORMATION) != IDOK) return 2;
    wchar_t program[] = L"probe"; wchar_t mode[] = L"--serial"; wchar_t target[] = L"COM10"; wchar_t peer[] = L"COM20";
    wchar_t* arguments[] = {program, mode, target, peer};
    const int result = wmain(4, arguments);
    MessageBoxW(nullptr, result == 0 ? L"PASS: baseline, startup and attach; synchronous bidirectional data and preopened handles.\nDetails: probe-result.txt. Async paths and clean-environment acceptance remain unverified."
        : L"FAIL: see probe-result.txt beside the probe executable.", L"FlexCom API Hook feasibility result", MB_OK | (result == 0 ? MB_ICONINFORMATION : MB_ICONERROR));
    return result;
}
