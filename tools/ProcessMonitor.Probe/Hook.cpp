#include "ProbeShared.h"
#include "detours.h"
#include <algorithm>

static decltype(&ReadFile) realRead = ReadFile;
static decltype(&WriteFile) realWrite = WriteFile;
static ProbeShared* shared = nullptr;
static SRWLOCK recordLock = SRWLOCK_INIT;

static bool IsSerial(HANDLE handle) {
    DCB state{};
    state.DCBlength = sizeof(state);
    return GetFileType(handle) == FILE_TYPE_CHAR && GetCommState(handle, &state);
}

static void Record(HANDLE handle, DWORD direction, const void* data, DWORD requested,
                   DWORD completed, BOOL succeeded, DWORD error) {
    if (!shared || !IsSerial(handle)) return;
    if (!TryAcquireSRWLockExclusive(&recordLock)) { InterlockedIncrement(&shared->dropped); return; }
    const LONG index = shared->count;
    if (index >= static_cast<LONG>(RecordCapacity)) {
        InterlockedIncrement(&shared->dropped);
    } else {
        auto& record = shared->records[index];
        record.handle = reinterpret_cast<ULONG_PTR>(handle);
        FILETIME time{};
        GetSystemTimePreciseAsFileTime(&time);
        record.completionTime = (static_cast<ULONGLONG>(time.dwHighDateTime) << 32) | time.dwLowDateTime;
        record.direction = direction;
        record.requested = requested;
        record.completed = completed;
        record.succeeded = succeeded;
        record.error = error;
        if (succeeded && completed && data) memcpy(record.data, data, (std::min)(completed, PayloadCapacity));
        InterlockedIncrement(&shared->count);
    }
    ReleaseSRWLockExclusive(&recordLock);
}

static BOOL WINAPI HookRead(HANDLE handle, LPVOID buffer, DWORD length, LPDWORD completed, LPOVERLAPPED overlapped) {
    const DWORD incomingError = GetLastError();
    SetLastError(incomingError);
    const BOOL result = realRead(handle, buffer, length, completed, overlapped);
    const DWORD error = GetLastError();
    // This feasibility probe promises synchronous paths only. Never inspect pending read buffers.
    if (!overlapped) Record(handle, 1, buffer, length, result && completed ? *completed : 0, result, error);
    SetLastError(error);
    return result;
}

static BOOL WINAPI HookWrite(HANDLE handle, LPCVOID buffer, DWORD length, LPDWORD completed, LPOVERLAPPED overlapped) {
    const BOOL result = realWrite(handle, buffer, length, completed, overlapped);
    const DWORD error = GetLastError();
    if (!overlapped) Record(handle, 0, buffer, length, result && completed ? *completed : 0, result, error);
    SetLastError(error);
    return result;
}

BOOL WINAPI DllMain(HINSTANCE, DWORD reason, LPVOID) {
    if (reason != DLL_PROCESS_ATTACH || DetourIsHelperProcess()) return TRUE;
    DetourRestoreAfterWith();
    wchar_t name[128]{};
    if (!GetEnvironmentVariableW(L"FLEXCOM_PROBE_MAP", name, 128)) return FALSE;
    const HANDLE mapping = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, name);
    if (!mapping) return FALSE;
    shared = static_cast<ProbeShared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(ProbeShared)));
    CloseHandle(mapping);
    if (!shared || shared->version != ProbeVersion) return FALSE;
    if (DetourTransactionBegin() != NO_ERROR) return FALSE;
    DetourUpdateThread(GetCurrentThread());
    DetourAttach(reinterpret_cast<PVOID*>(&realRead), HookRead);
    DetourAttach(reinterpret_cast<PVOID*>(&realWrite), HookWrite);
    const LONG result = DetourTransactionCommit();
    InterlockedExchange(&shared->attached, result == NO_ERROR ? 1 : -1);
    // Deliberately remain resident until this controlled worker exits. No unsafe asynchronous unload.
    return result == NO_ERROR;
}
