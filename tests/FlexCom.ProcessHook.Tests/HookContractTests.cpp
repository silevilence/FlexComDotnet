// Contract tests use the production hooks, with only the external Win32 I/O boundary replaced.
// Detours instruments these test functions in this executable; no other process or port is touched.
#include "../../src/FlexCom.ProcessHook/Shared.h"
#include "detours.h"
#include <iostream>
#include <stdexcept>
#include <cstring>
#include <thread>
#include <deque>
#include <atomic>
#include <set>

namespace TestOs {
static HANDLE port = reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(0x1234));
static bool portOpen = true;
static HANDLE closeEntered = nullptr, closeReleased = nullptr;
static bool pauseSerialClose = false;
static bool hasCompletionPort = true;
struct Request { OVERLAPPED* overlapped; void* buffer; DWORD length; bool ready; DWORD count; DWORD error; bool queued; LPOVERLAPPED_COMPLETION_ROUTINE callback; bool write; };
static Request requests[2048]{};
static COMMTIMEOUTS configuredTimeouts{};
static DWORD writeLimit = MAXDWORD;
static bool failWrite = false;
static bool immediateRead = false;
static std::deque<OVERLAPPED_ENTRY> packets;
static Request* Find(OVERLAPPED* overlapped) {
    for (auto& r : requests) if (r.overlapped == overlapped) return &r;
    return nullptr;
}
__declspec(noinline) DWORD WINAPI FileType(HANDLE handle) { return handle == port ? FILE_TYPE_CHAR : GetFileType(handle); }
__declspec(noinline) BOOL WINAPI State(HANDLE handle, DCB* state) {
    if (handle != port) return GetCommState(handle, state);
    if (!portOpen) { SetLastError(ERROR_INVALID_HANDLE); return FALSE; }
    state->BaudRate = 115200; return TRUE;
}
__declspec(noinline) HANDLE WINAPI OpenW(LPCWSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templ) {
    if (!wcscmp(name, L"\\\\.\\COM404")) { SetLastError(ERROR_FILE_NOT_FOUND); return INVALID_HANDLE_VALUE; }
    if (!wcscmp(name, L"\\\\.\\COM10")) { portOpen = true; hasCompletionPort = false; return port; }
    return CreateFileW(name, access, share, security, disposition, flags, templ);
}
__declspec(noinline) HANDLE WINAPI OpenA(LPCSTR name, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templ) {
    if (!strcmp(name, "\\\\.\\COM10")) return OpenW(L"\\\\.\\COM10", access, share, security, disposition, flags, templ);
    return CreateFileA(name, access, share, security, disposition, flags, templ);
}
__declspec(noinline) BOOL WINAPI Close(HANDLE handle) {
    if (handle == reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(0x9999))) {
        SetEvent(closeEntered); WaitForSingleObject(closeReleased, INFINITE); return TRUE;
    }
    if (handle != port) return CloseHandle(handle);
    if (!portOpen) { SetLastError(ERROR_INVALID_HANDLE); return FALSE; }
    portOpen = false;
    if (pauseSerialClose) { SetEvent(closeEntered); WaitForSingleObject(closeReleased, INFINITE); }
    return TRUE;
}
__declspec(noinline) BOOL WINAPI SetState(HANDLE handle, DCB* state) {
    if (handle != port) return SetCommState(handle, state);
    DCB current{}; current.DCBlength = sizeof(current); State(handle, &current); // Nested API must not double-count.
    if (!state->BaudRate) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    return TRUE;
}
__declspec(noinline) BOOL WINAPI Read(HANDLE handle, void* data, DWORD length, DWORD* count, OVERLAPPED* overlapped) {
    if (handle != port) return ReadFile(handle, data, length, count, overlapped);
    if (!overlapped) { if (count) *count = 0; return TRUE; }
    auto r = Find(overlapped);
    if (!r) for (auto& slot : requests) if (!slot.overlapped) { r = &slot; break; }
    if (!r) { SetLastError(ERROR_NOT_ENOUGH_MEMORY); return FALSE; }
    *r = {overlapped, data, length, false, 0, 0, false, nullptr, false};
    if (immediateRead) {
        const BYTE completed[] = {0, 0x7f, 0xff}; memcpy(data, completed, 3);
        r->ready = true; r->count = 3; overlapped->Internal = 0;
        if (count) *count = 3;
        if (hasCompletionPort) packets.push_back({0, overlapped, 0, 3});
        return TRUE;
    }
    SetLastError(ERROR_IO_PENDING); return FALSE;
}
static void Complete(OVERLAPPED* overlapped, DWORD error = 0) {
    auto r = Find(overlapped);
    if (!r) throw std::runtime_error("unknown test request");
    const BYTE data[] = {0x00, 0x7f, 0xff};
    r->count = error ? 0 : 3; r->error = error; r->ready = true; r->queued = true;
    overlapped->Internal = error ? 0xc0000120u : 0; // STATUS_CANCELLED
    if (!error && !r->write) memcpy(r->buffer, data, r->count);
    if (hasCompletionPort && !r->callback) packets.push_back({0, overlapped, 0, r->count});
}
__declspec(noinline) BOOL WINAPI Result(HANDLE handle, OVERLAPPED* overlapped, DWORD* count, BOOL wait) {
    if (handle != port) return GetOverlappedResult(handle, overlapped, count, wait);
    auto r = Find(overlapped);
    if (!r || !r->ready) { SetLastError(ERROR_IO_INCOMPLETE); return FALSE; }
    *count = r->count; SetLastError(r->error); return r->error == 0;
}
__declspec(noinline) BOOL WINAPI ReadEx(HANDLE handle, void* data, DWORD length, OVERLAPPED* overlapped, LPOVERLAPPED_COMPLETION_ROUTINE callback) {
    if (handle != port) return ReadFileEx(handle, data, length, overlapped, callback);
    DWORD count = 0;
    Read(handle, data, length, &count, overlapped);
    auto r = Find(overlapped); r->callback = callback; return TRUE;
}
__declspec(noinline) BOOL WINAPI Write(HANDLE handle, const void* data, DWORD length, DWORD* count, OVERLAPPED* overlapped) {
    if (handle != port) return WriteFile(handle, data, length, count, overlapped);
    if (failWrite) { if (count) *count = 0; SetLastError(ERROR_ACCESS_DENIED); return FALSE; }
    if (overlapped) {
        Read(handle, const_cast<void*>(data), length, count, overlapped);
        Find(overlapped)->write = true; return FALSE;
    }
    *count = length < writeLimit ? length : writeLimit; return TRUE;
}
__declspec(noinline) BOOL WINAPI WriteEx(HANDLE handle, const void* data, DWORD length, OVERLAPPED* overlapped, LPOVERLAPPED_COMPLETION_ROUTINE callback) {
    if (handle != port) return WriteFileEx(handle, data, length, overlapped, callback);
    DWORD count = 0; Write(handle, data, length, &count, overlapped);
    Find(overlapped)->callback = callback; return TRUE;
}
static void Deliver(OVERLAPPED* overlapped) {
    auto r = Find(overlapped);
    if (!r || !r->ready || !r->callback) throw std::runtime_error("callback not ready");
    auto callback = r->callback; r->callback = nullptr;
    callback(r->error, r->count, overlapped);
}
__declspec(noinline) BOOL WINAPI ResultEx(HANDLE handle, OVERLAPPED* overlapped, DWORD* count, DWORD timeout, BOOL alertable) {
    if (handle != port) return GetOverlappedResultEx(handle, overlapped, count, timeout, alertable);
    auto r = Find(overlapped);
    if (!r || !r->ready) { SetLastError(timeout == 0 ? ERROR_IO_INCOMPLETE : WAIT_TIMEOUT); return FALSE; }
    *count = r->count; SetLastError(r->error); return r->error == 0;
}
__declspec(noinline) BOOL WINAPI CompletionEx(HANDLE, OVERLAPPED_ENTRY* entries, ULONG capacity, ULONG* removed, DWORD, BOOL) {
    *removed = 0;
    while (!packets.empty() && *removed < capacity) { entries[(*removed)++] = packets.front(); packets.pop_front(); }
    if (*removed == 0) { SetLastError(WAIT_TIMEOUT); return FALSE; }
    return TRUE;
}
__declspec(noinline) HANDLE WINAPI Associate(HANDLE file, HANDLE existing, ULONG_PTR key, DWORD concurrency) {
    if (file != port) return CreateIoCompletionPort(file, existing, key, concurrency);
    hasCompletionPort = true; return reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(0x5678));
}
__declspec(noinline) BOOL WINAPI SetTimeouts(HANDLE handle, COMMTIMEOUTS* value) {
    if (handle != port) return SetCommTimeouts(handle, value);
    configuredTimeouts = *value; return TRUE;
}
__declspec(noinline) BOOL WINAPI GetTimeouts(HANDLE handle, COMMTIMEOUTS* value) {
    if (handle != port) return GetCommTimeouts(handle, value);
    *value = configuredTimeouts; return TRUE;
}
__declspec(noinline) BOOL WINAPI Escape(HANDLE handle, DWORD function) {
    if (handle != port) return EscapeCommFunction(handle, function);
    if (function == 0) { SetLastError(ERROR_INVALID_PARAMETER); return FALSE; }
    return TRUE;
}
__declspec(noinline) BOOL WINAPI Purge(HANDLE handle, DWORD flags) {
    if (handle != port) return PurgeComm(handle, flags);
    return TRUE;
}
__declspec(noinline) BOOL WINAPI CancelEx(HANDLE handle, OVERLAPPED* overlapped) {
    if (handle != port) return CancelIoEx(handle, overlapped);
    Complete(overlapped, ERROR_OPERATION_ABORTED); return TRUE;
}
}
#define GetFileType TestOs::FileType
#define GetCommState TestOs::State
#define SetCommState TestOs::SetState
#define ReadFile TestOs::Read
#define ReadFileEx TestOs::ReadEx
#define WriteFile TestOs::Write
#define WriteFileEx TestOs::WriteEx
#define GetOverlappedResult TestOs::Result
#define GetOverlappedResultEx TestOs::ResultEx
#define GetQueuedCompletionStatusEx TestOs::CompletionEx
#define CreateIoCompletionPort TestOs::Associate
#define SetCommTimeouts TestOs::SetTimeouts
#define GetCommTimeouts TestOs::GetTimeouts
#define EscapeCommFunction TestOs::Escape
#define PurgeComm TestOs::Purge
#define CancelIoEx TestOs::CancelEx
#define CreateFileW TestOs::OpenW
#define CreateFileA TestOs::OpenA
#define CloseHandle TestOs::Close
#include "../../src/FlexCom.ProcessHook/Hook.cpp"
#undef GetFileType
#undef GetCommState
#undef SetCommState
#undef ReadFile
#undef ReadFileEx
#undef WriteFile
#undef WriteFileEx
#undef GetOverlappedResult
#undef GetOverlappedResultEx
#undef GetQueuedCompletionStatusEx
#undef CreateIoCompletionPort
#undef SetCommTimeouts
#undef GetCommTimeouts
#undef EscapeCommFunction
#undef PurgeComm
#undef CancelIoEx
#undef CreateFileW
#undef CreateFileA
#undef CloseHandle

static void Require(bool ok, const char* description) { if (!ok) throw std::runtime_error(description); }
static DWORD callbackCount = 0, callbackError = 0, callbackBytes = 0;
static OVERLAPPED* callbackRequest = nullptr;
static void WINAPI BusinessCallback(DWORD error, DWORD count, OVERLAPPED* request) {
    callbackCount++; callbackError = error; callbackBytes = count; callbackRequest = request; SetLastError(777);
}
int main() {
    try {
        auto name = MappingName(GetCurrentProcessId(), ProcessCreationTime(GetCurrentProcess()));
        HANDLE mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(HookShared), name.c_str());
        auto queue = static_cast<HookShared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(HookShared)));
        Require(queue != nullptr, "create queue");
        queue->version = HookVersion; queue->recordSize = sizeof(HookRecord); queue->capacity = QueueCapacity;
        queue->enabled = 1; queue->session.Data1 = 42;
        HANDLE mutex = CreateMutexW(nullptr, FALSE, (name + L".lock").c_str());
        HANDLE owner = CreateSemaphoreW(nullptr, 0, 1, (name + L".owner").c_str());
        Require(InitializeProcessMonitor(nullptr) == 0, "initialize production hooks over test OS boundary");
        OVERLAPPED request{}; BYTE data[8]; memset(data, 0xcc, sizeof(data)); DWORD count = 0;
        Require(!TestOs::Read(TestOs::port, data, 8, &count, &request) && GetLastError() == ERROR_IO_PENDING, "pending business result");
        Require(queue->count == 0, "pending buffer is never captured");
        Require(!TestOs::ResultEx(TestOs::port, &request, &count, 20, FALSE) && GetLastError() == WAIT_TIMEOUT, "timeout result");
        Require(queue->count == 0, "timeout is not terminal completion");
        TestOs::Complete(&request);
        Require(TestOs::ResultEx(TestOs::port, &request, &count, 100, FALSE) && count == 3, "extended business completion");
        Require(queue->count == 1, "extended completion must be captured exactly once");
        Require(queue->records[0].completed == 3 && queue->records[0].dataLength == 3
            && queue->records[0].data[0] == 0 && queue->records[0].data[1] == 0x7f && queue->records[0].data[2] == 0xff, "only actual completed bytes are captured");
        Require(TestOs::Result(TestOs::port, &request, &count, FALSE), "repeat completion query");
        Require(queue->count == 1, "cross-API query deduplicated");
        OVERLAPPED second{}, third{}; BYTE secondData[8]{}, thirdData[8]{};
        Require(!TestOs::Read(TestOs::port, secondData, 8, &count, &second), "second request pending");
        Require(!TestOs::Read(TestOs::port, thirdData, 8, &count, &third), "third request pending");
        TestOs::Complete(&third, ERROR_OPERATION_ABORTED); TestOs::Complete(&second);
        OVERLAPPED_ENTRY entries[4]{}; ULONG removed = 0;
        SetLastError(12345);
        Require(TestOs::CompletionEx(nullptr, entries, 4, &removed, 20, FALSE) && removed == 3, "batch business result");
        Require(GetLastError() == 12345, "batch last-error preservation");
        Require(queue->count == 3, "batch captures both new completions and ignores old query duplicate");
        Require(queue->records[1].succeeded == 0 && queue->records[1].error == ERROR_OPERATION_ABORTED
            && queue->records[1].dataLength == 0 && queue->records[1].integrity == Failed, "batch TRUE must not turn cancelled entry into success");
        Require(queue->records[2].succeeded && queue->records[2].dataLength == 3, "out-of-order successful entry");
        DCB configuration{}; configuration.DCBlength = sizeof(configuration); configuration.BaudRate = 115200;
        configuration.ByteSize = 8; configuration.fDtrControl = DTR_CONTROL_ENABLE;
        SetLastError(12345);
        Require(TestOs::SetState(TestOs::port, &configuration) && GetLastError() == 12345, "configuration preserves business result and error");
        Require(queue->count == 4, "configuration event emitted once despite nested getter");
        Require(queue->records[3].api == ApiSetState && queue->records[3].direction == 2
            && queue->records[3].dataLength == 0 && wcsstr(queue->records[3].detail, L"Baud=115200"), "configuration parameters separated from byte stream");
        configuration.BaudRate = 0;
        Require(!TestOs::SetState(TestOs::port, &configuration) && GetLastError() == ERROR_INVALID_PARAMETER, "failed control business result");
        Require(queue->count == 5 && queue->records[4].error == ERROR_INVALID_PARAMETER
            && queue->records[4].integrity == Failed, "failed configuration retained");
        COMMTIMEOUTS timeouts{}; timeouts.ReadTotalTimeoutConstant = 200; timeouts.WriteTotalTimeoutConstant = 1000;
        Require(TestOs::SetTimeouts(TestOs::port, &timeouts), "set timeouts business result");
        Require(queue->count == 6 && queue->records[5].api == ApiSetTimeouts
            && wcsstr(queue->records[5].detail, L"RConst=200"), "timeout values captured");
        COMMTIMEOUTS restored{};
        Require(TestOs::GetTimeouts(TestOs::port, &restored) && restored.ReadTotalTimeoutConstant == 200, "get timeouts result");
        Require(TestOs::Escape(TestOs::port, SETDTR), "DTR business result");
        Require(TestOs::Purge(TestOs::port, PURGE_RXCLEAR), "purge business result");
        Require(queue->count == 9 && queue->records[7].api == ApiEscape && queue->records[8].api == ApiPurge, "line and purge events");
        OVERLAPPED cancelled{}; BYTE cancelledData[8]{};
        Require(!TestOs::Read(TestOs::port, cancelledData, 8, &count, &cancelled), "cancel request pending");
        Require(TestOs::CancelEx(TestOs::port, &cancelled), "cancel accepted");
        Require(queue->count == 10 && queue->records[9].api == ApiCancel, "cancel submission must not invent I/O completion");
        Require(!TestOs::ResultEx(TestOs::port, &cancelled, &count, 20, FALSE) && GetLastError() == ERROR_OPERATION_ABORTED, "cancel completion business result");
        Require(queue->count == 11 && queue->records[10].integrity == Failed && queue->records[10].dataLength == 0, "cancel completion captured separately");
        Require(TestOs::Close(TestOs::port), "close preopened port");
        Require(queue->count == 12 && queue->records[11].api == ApiClose, "serial close recorded");
        auto oldGeneration = queue->records[11].generation;
        Require(TestOs::OpenW(L"\\\\.\\COM10", GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr) == TestOs::port, "open serial port");
        Require(queue->count == 13 && queue->records[12].api == ApiOpen && !wcscmp(queue->records[12].port, L"COM10")
            && queue->records[12].generation > oldGeneration, "open captures normalized port and new handle generation");
        Require(TestOs::OpenW(L"\\\\.\\COM404", GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr) == INVALID_HANDLE_VALUE
            && GetLastError() == ERROR_FILE_NOT_FOUND, "failed open retains business error");
        Require(queue->count == 14 && !queue->records[13].succeeded && !wcscmp(queue->records[13].port, L"COM404"), "failed serial open recorded");
        Require(TestOs::Close(TestOs::port), "close before ANSI reopen");
        Require(TestOs::OpenA("\\\\.\\COM10", GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr) == TestOs::port, "ANSI open business result");
        Require(queue->count == 16 && queue->records[15].api == ApiOpen, "ANSI to Unicode nested open counted once");
        OVERLAPPED callbackIo{}; BYTE callbackBuffer[8]{};
        Require(TestOs::ReadEx(TestOs::port, callbackBuffer, 8, &callbackIo, BusinessCallback), "callback read accepted");
        TestOs::Complete(&callbackIo); TestOs::Deliver(&callbackIo);
        Require(callbackCount == 1 && callbackError == 0 && callbackBytes == 3 && callbackRequest == &callbackIo
            && GetLastError() == 777, "original callback arguments and last-error changes preserved");
        Require(queue->count == 17 && queue->records[16].api == ApiReadEx && queue->dropped == 0,
            "nested ReadFileEx to ReadFile emits one callback record without false loss");
        Require(TestOs::ReadEx(TestOs::port, callbackBuffer, 8, &callbackIo, BusinessCallback), "completed OVERLAPPED can be reused");
        Require(TestOs::CancelEx(TestOs::port, &callbackIo), "callback cancellation accepted");
        TestOs::Deliver(&callbackIo);
        Require(callbackCount == 2 && callbackError == ERROR_OPERATION_ABORTED && callbackBytes == 0
            && queue->records[18].integrity == Failed, "cancelled callback retains original result");
        Require(TestOs::ReadEx(TestOs::port, callbackBuffer, 8, &callbackIo, BusinessCallback), "old-session callback accepted");
        auto beforeOldCompletion = queue->count;
        queue->session.Data1 = 43;
        TestOs::Complete(&callbackIo); TestOs::Deliver(&callbackIo);
        Require(callbackCount == 3 && queue->count == beforeOldCompletion, "old-session callback still runs but cannot contaminate new capture");
        HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr), release = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        auto droppedBefore = queue->dropped;
        std::thread holder([&] {
            WaitForSingleObject(mutex, INFINITE); SetEvent(ready);
            WaitForSingleObject(release, INFINITE); ReleaseMutex(mutex);
        });
        WaitForSingleObject(ready, INFINITE);
        HANDLE nul = TestOs::OpenW(L"NUL", GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
        TestOs::Close(nul);
        SetEvent(release); holder.join(); CloseHandle(ready); CloseHandle(release);
        Require(queue->dropped == droppedBefore, "nonserial operations must not create false capture loss when queue is busy");
        const BYTE outgoing[] = {9, 8, 7, 6, 5};
        auto baseline = queue->count;
        TestOs::writeLimit = 3;
        Require(TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr) && count == 3, "partial write business result");
        Require(queue->records[baseline].completed == 3 && queue->records[baseline].dataLength == 3
            && queue->records[baseline].attemptLength == 5 && !memcmp(queue->records[baseline].data, outgoing, 3), "write attempt stays separate from completed bytes");
        Require(TestOs::Write(TestOs::port, outgoing, 0, &count, nullptr) && count == 0, "zero write business result");
        Require(queue->records[baseline + 1].dataLength == 0 && queue->records[baseline + 1].completed == 0, "zero completion never fabricates payload");
        TestOs::failWrite = true;
        Require(!TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr) && GetLastError() == ERROR_ACCESS_DENIED, "failed write business result");
        Require(queue->records[baseline + 2].integrity == Failed && queue->records[baseline + 2].dataLength == 0
            && queue->records[baseline + 2].attemptLength == 5, "failed write preserves attempt without success data");
        TestOs::failWrite = false; TestOs::writeLimit = 1100;
        BYTE large[1500]{}; memset(large, 0x5a, sizeof(large));
        Require(TestOs::Write(TestOs::port, large, 1500, &count, nullptr) && count == 1100, "large write business result");
        Require(queue->records[baseline + 3].integrity == Truncated && queue->records[baseline + 3].dataLength == 1024
            && queue->records[baseline + 3].completed == 1100, "bounded payload keeps original completed length");
        OVERLAPPED callbackWrite{};
        Require(TestOs::WriteEx(TestOs::port, outgoing, 5, &callbackWrite, BusinessCallback), "callback write accepted");
        TestOs::Complete(&callbackWrite); TestOs::Deliver(&callbackWrite);
        Require(callbackCount == 4 && callbackBytes == 3 && queue->records[baseline + 4].api == ApiWriteEx
            && queue->records[baseline + 4].attemptLength == 5 && !memcmp(queue->records[baseline + 4].data, outgoing, 3), "callback write actual bytes and original callback");
        for (int i = 0; i < 300; ++i) Require(TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr), "slow consumer cannot fail business writes");
        Require(queue->count == QueueCapacity && queue->dropped > 0, "slow consumer has bounded queue and visible loss");
        queue->count = queue->readIndex = queue->writeIndex = 0;
        Require(TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr), "write after queue drain");
        Require(queue->records[0].dropped == static_cast<uint32_t>(queue->dropped), "next surviving record carries exact loss boundary");
        queue->count = QueueCapacity + 1;
        Require(TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr), "corrupt consumer state cannot fail business I/O");
        Require(queue->count == QueueCapacity + 1, "producer must reject corrupt queue indices before writing");
        queue->count = queue->readIndex = queue->writeIndex = 0;
        Require(TestOs::ReadEx(TestOs::port, callbackBuffer, 8, &callbackIo, BusinessCallback), "callback pending before close");
        Require(TestOs::Close(TestOs::port), "close with pending callback");
        TestOs::Complete(&callbackIo, ERROR_OPERATION_ABORTED); TestOs::Deliver(&callbackIo);
        Require(callbackCount == 5 && callbackError == ERROR_OPERATION_ABORTED && queue->count == 2
            && queue->records[1].integrity == Failed, "close does not discard pending business callback");
        Require(TestOs::OpenW(L"\\\\.\\COM10", GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr) == TestOs::port, "reopen for bounded pending test");
        struct CallbackWork { OVERLAPPED request{}; BYTE data[8]{}; };
        std::vector<CallbackWork> work(1025);
        for (auto& item : work) Require(TestOs::ReadEx(TestOs::port, item.data, 8, &item.request, BusinessCallback), "pending capacity does not reject business request");
        for (auto& item : work) { TestOs::Complete(&item.request); TestOs::Deliver(&item.request); }
        Require(callbackCount == 1030 && queue->count <= QueueCapacity && queue->dropped > 0,
            "pending and queue overflow preserve every original business callback with visible capture loss");
        queue->count = queue->readIndex = queue->writeIndex = 0;
        OVERLAPPED reused{}; BYTE reusedData[8]{};
        Require(TestOs::Associate(TestOs::port, nullptr, 0, 0) != nullptr, "associate for mixed completion test");
        Require(!TestOs::Read(TestOs::port, reusedData, 8, &count, &reused), "query and IOCP mixed request");
        TestOs::Complete(&reused);
        Require(TestOs::ResultEx(TestOs::port, &reused, &count, 20, FALSE), "query completes before IOCP packet is consumed");
        Require(!TestOs::Read(TestOs::port, reusedData, 8, &count, &reused), "reuse before old completion packet drained");
        TestOs::Complete(&reused);
        OVERLAPPED_ENTRY mixedEntries[32]{};
        Require(TestOs::CompletionEx(nullptr, mixedEntries, 32, &removed, 20, FALSE), "old and new packets still reach business code");
        Require(queue->count == 2 && queue->records[1].integrity == Unavailable && queue->records[1].dataLength == 0,
            "ambiguous reused OVERLAPPED packet must be a capability gap, never attributed to the new buffer");
        Require(TestOs::Close(TestOs::port), "close before event-only phase");
        Require(TestOs::OpenW(L"\\\\.\\COM10", GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, nullptr) == TestOs::port, "new event-only handle");
        OVERLAPPED switching{}; BYTE switchingData[8]{};
        Require(!TestOs::Read(TestOs::port, switchingData, 8, &count, &switching), "event-only request");
        TestOs::Complete(&switching);
        Require(TestOs::ResultEx(TestOs::port, &switching, &count, 20, FALSE), "event-only completion");
        Require(TestOs::Associate(TestOs::port, nullptr, 0, 0) != nullptr, "later associate the same handle with IOCP");
        Require(!TestOs::Read(TestOs::port, switchingData, 8, &count, &switching), "reuse after event-only completion");
        TestOs::Complete(&switching);
        auto beforeSwitching = queue->count;
        Require(TestOs::CompletionEx(nullptr, mixedEntries, 32, &removed, 20, FALSE), "new IOCP completion");
        Require(queue->count == beforeSwitching + 1 && queue->records[beforeSwitching].integrity == Complete
            && queue->records[beforeSwitching].dataLength == 3, "event-only queries must not create phantom old IOCP packets");
        TestOs::immediateRead = true;
        OVERLAPPED immediate{}; BYTE immediateData[8]{}; auto beforeImmediate = queue->count;
        Require(TestOs::Read(TestOs::port, immediateData, 8, nullptr, &immediate), "immediate overlapped success with null byte-count pointer");
        Require(queue->count == beforeImmediate + 1 && queue->records[beforeImmediate].completed == 3
            && queue->records[beforeImmediate].dataLength == 3, "immediate completion uses actual result length");
        Require(TestOs::ResultEx(TestOs::port, &immediate, &count, 20, FALSE), "immediate repeat query");
        Require(TestOs::CompletionEx(nullptr, mixedEntries, 32, &removed, 20, FALSE), "immediate completion packet");
        Require(queue->count == beforeImmediate + 1, "immediate/query/packet paths emit one record");
        TestOs::immediateRead = false;
        queue->count = queue->readIndex = queue->writeIndex = 0; queue->dropped = 0;
        std::vector<CallbackWork> concurrent(16);
        for (auto& item : concurrent) { Require(!TestOs::Read(TestOs::port, item.data, 8, &count, &item.request), "concurrent submission"); TestOs::Complete(&item.request); }
        std::atomic<int> completedThreads{0}; std::vector<std::thread> observers;
        for (auto& item : concurrent) observers.emplace_back([&item, &completedThreads] {
            DWORD actual = 0;
            if (TestOs::ResultEx(TestOs::port, &item.request, &actual, 20, FALSE) && actual == 3) ++completedThreads;
        });
        for (auto& observer : observers) observer.join();
        Require(completedThreads == 16 && queue->count + queue->dropped == 16, "concurrent completions retain business results and account for any lock contention loss");
        std::set<uint64_t> identities;
        for (uint32_t i = 0; i < queue->count; ++i) {
            Require(queue->records[i].completed == 3 && queue->records[i].dataLength == 3, "concurrent completion payload bounds");
            identities.insert(queue->records[i].request);
        }
        Require(identities.size() == queue->count, "concurrent requests are never completed twice");
        TestOs::closeEntered = CreateEventW(nullptr, TRUE, FALSE, nullptr); TestOs::closeReleased = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        HANDLE writeCompleted = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        std::thread slowClose([] { TestOs::Close(reinterpret_cast<HANDLE>(static_cast<ULONG_PTR>(0x9999))); });
        WaitForSingleObject(TestOs::closeEntered, INFINITE);
        std::thread independentWrite([&] { DWORD actual = 0; TestOs::Write(TestOs::port, outgoing, 5, &actual, nullptr); SetEvent(writeCompleted); });
        bool independent = WaitForSingleObject(writeCompleted, 1000) == WAIT_OBJECT_0;
        SetEvent(TestOs::closeReleased); slowClose.join(); independentWrite.join();
        Require(independent, "a slow unrelated CloseHandle must not hold the serial identity lock over business I/O");
        ResetEvent(TestOs::closeEntered); ResetEvent(TestOs::closeReleased);
        queue->count = queue->readIndex = queue->writeIndex = 0;
        TestOs::pauseSerialClose = true;
        std::thread reusedClose([] { TestOs::Close(TestOs::port); });
        WaitForSingleObject(TestOs::closeEntered, INFINITE);
        auto reopened = TestOs::OpenW(L"\\\\.\\COM10", GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
        auto reopenedGeneration = queue->records[0].generation;
        SetEvent(TestOs::closeReleased); reusedClose.join(); TestOs::pauseSerialClose = false;
        Require(TestOs::Write(reopened, outgoing, 5, &count, nullptr), "reused handle business write");
        Require(queue->count == 3 && queue->records[0].api == ApiOpen
            && queue->records[1].api == ApiClose && queue->records[1].generation != reopenedGeneration
            && queue->records[2].generation == reopenedGeneration && !wcscmp(queue->records[2].port, L"COM10"),
            "a delayed close must not erase the identity of a concurrently reopened numeric handle");
        CloseHandle(TestOs::closeEntered); CloseHandle(TestOs::closeReleased); CloseHandle(writeCompleted);
        CloseHandle(owner); Sleep(1100);
        auto beforeDisconnect = queue->count;
        Require(TestOs::Write(TestOs::port, outgoing, 5, &count, nullptr), "collector exit cannot stop business writes");
        Require(queue->enabled == 0 && queue->count == beforeDisconnect, "missing collector disables observation without unloading callbacks");
        queue->enabled = 0;
        UnmapViewOfFile(queue); CloseHandle(mapping); CloseHandle(mutex);
        std::cout << "PASS: production Hook contract with simulated Win32 I/O\n";
        return 0;
    } catch (const std::exception& error) { std::cerr << "FAIL: " << error.what() << '\n'; return 1; }
}
