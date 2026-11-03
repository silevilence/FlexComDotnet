#include "Shared.h"
#include "detours.h"
#include <tlhelp32.h>
#include <vector>
#include <algorithm>

static HookShared* shared = nullptr;
static HANDLE queueMutex = nullptr;
static HANDLE mappingHandle = nullptr;
static SRWLOCK initializationLock = SRWLOCK_INIT;
static bool installed = false;
// Brief consumer/producer hand-offs may wait; a stopped collector cannot block business I/O indefinitely.
constexpr DWORD QueueWaitMilliseconds = 10;
struct CaptureRecord : HookRecord { uint32_t epoch = 0; bool suppressesPacket = false; bool skipsSuccessfulPacket = false; };
static void Lose(SessionCounter& reason, uint32_t epoch) {
    if (reason.Add(epoch)) shared->dropped.Add(epoch);
}
static std::wstring ownerName;
alignas(8) static volatile LONG64 nextOwnerCheck = 0;
static decltype(&ReadFile) realRead = ReadFile;
static decltype(&WriteFile) realWrite = WriteFile;
static decltype(&CloseHandle) realClose = CloseHandle;
static decltype(&CreateFileW) realOpenW = CreateFileW;
static decltype(&CreateFileA) realOpenA = CreateFileA;
static decltype(&GetOverlappedResult) realGetResult = GetOverlappedResult;
static decltype(&GetOverlappedResultEx) realGetResultEx = GetOverlappedResultEx;
static decltype(&GetQueuedCompletionStatus) realGetCompletion = GetQueuedCompletionStatus;
static decltype(&GetQueuedCompletionStatusEx) realGetCompletionEx = GetQueuedCompletionStatusEx;
static decltype(&CreateIoCompletionPort) realAssociate = CreateIoCompletionPort;
static ULONG (WINAPI* statusToError)(LONG) = nullptr;
static decltype(&ReadFileEx) realReadEx = ReadFileEx;
static decltype(&WriteFileEx) realWriteEx = WriteFileEx;
static decltype(&GetCommState) realGetState = GetCommState;
static decltype(&SetCommState) realSetState = SetCommState;
static decltype(&SetCommTimeouts) realSetTimeouts = SetCommTimeouts;
static decltype(&GetCommTimeouts) realGetTimeouts = GetCommTimeouts;
static decltype(&EscapeCommFunction) realEscape = EscapeCommFunction;
static decltype(&PurgeComm) realPurge = PurgeComm;
static decltype(&CancelIo) realCancel = CancelIo;
static decltype(&CancelIoEx) realCancelEx = CancelIoEx;
static decltype(&WaitCommEvent) realWaitComm = WaitCommEvent;
static decltype(&SetFileCompletionNotificationModes) realNotificationModes = SetFileCompletionNotificationModes;
static thread_local unsigned observationDepth = 0;
struct SuppressNested {
    SuppressNested() { ++observationDepth; }
    ~SuppressNested() { --observationDepth; }
};
template<typename F> static auto CallOriginal(F operation) { SuppressNested scope; return operation(); }
static SRWLOCK handleLock = SRWLOCK_INIT;
struct SerialHandle { HANDLE value; uint64_t generation; wchar_t port[32]; bool portKnown; HANDLE completionPort; bool skipSuccessfulPacket; };
static SerialHandle handles[1024]{};
static uint64_t nextGeneration = 0;
alignas(8) static volatile LONG64 nextRequest = 0;
struct PendingRequest { OVERLAPPED* overlapped; HANDLE handle; const void* buffer; CaptureRecord record; LPOVERLAPPED_COMPLETION_ROUTINE callback; bool ambiguousPacket; };
static PendingRequest pending[1024]{};
static SRWLOCK pendingLock = SRWLOCK_INIT;
// A result query does not dequeue an IOCP packet. Keep bounded tombstones until that packet arrives.
struct RetiredRequest { OVERLAPPED* overlapped; bool ambiguous; HANDLE handle; uint64_t generation; };
static RetiredRequest retired[1024]{};
static bool retirementOverflow = false;
static void RememberRetired(OVERLAPPED* pointer, bool ambiguous, HANDLE handle, uint64_t generation) {
    RetiredRequest* empty = nullptr;
    for (auto& item : retired) {
        if (item.overlapped == pointer) { item.ambiguous = item.ambiguous || ambiguous; return; }
        if (!item.overlapped && !empty) empty = &item;
    }
    if (empty) *empty = {pointer, ambiguous, handle, generation};
    // Tracking saturation is a capability diagnostic, not another lost business completion.
    else if (!retirementOverflow) { retirementOverflow = true; shared->diagnostics.retirementCapacity.Add(shared->dropped.Epoch()); }
}
static bool KnownWithoutCompletionPort(HANDLE handle, uint64_t generation) {
    bool absent = false;
    AcquireSRWLockShared(&handleLock);
    for (const auto& entry : handles) if (entry.value == handle && entry.generation == generation) {
        absent = entry.portKnown && !entry.completionPort; break;
    }
    ReleaseSRWLockShared(&handleLock); return absent;
}
static bool KnownSkipSuccessfulPacket(HANDLE handle, uint64_t generation) {
    bool skip = false;
    AcquireSRWLockShared(&handleLock);
    for (const auto& entry : handles) if (entry.value == handle && entry.generation == generation) {
        skip = entry.skipSuccessfulPacket; break;
    }
    ReleaseSRWLockShared(&handleLock); return skip;
}
static bool HasRetired(OVERLAPPED* pointer) {
    if (retirementOverflow) return true;
    for (auto& item : retired) if (item.overlapped == pointer) return true;
    return false;
}
static void ForgetRetired(OVERLAPPED* pointer) {
    for (auto& item : retired) if (item.overlapped == pointer && !item.ambiguous) { item = {}; return; }
}
static uint64_t PendingId(OVERLAPPED* pointer, HANDLE handle) {
    uint64_t id = 0;
    AcquireSRWLockShared(&pendingLock);
    for (const auto& item : pending) if (item.overlapped == pointer && item.handle == handle) { id = item.record.request; break; }
    ReleaseSRWLockShared(&pendingLock); return id;
}

// Invalid caller buffers must retain the original API's error behavior, not crash the observer.
static bool SafeCopy(void* destination, const void* source, size_t length) {
    __try { if (length) memcpy(destination, source, length); return true; }
    __except (EXCEPTION_EXECUTE_HANDLER) { return false; }
}
static bool Serial(HANDLE value, SerialHandle& result, bool newlyOpened = false) {
    DCB state{}; state.DCBlength = sizeof(state);
    if (GetFileType(value) != FILE_TYPE_CHAR || !realGetState(value, &state)) return false;
    AcquireSRWLockExclusive(&handleLock);
    SerialHandle* empty = nullptr;
    for (auto& entry : handles) {
        if (entry.value == value) {
            if (newlyOpened) {
                entry = {}; entry.value = value; entry.generation = ++nextGeneration;
                wcscpy_s(entry.port, L"未知（未取得 COM 名称）");
            }
            result = entry; ReleaseSRWLockExclusive(&handleLock); return true;
        }
        if (!entry.value && !empty) empty = &entry;
    }
    if (empty) { empty->value = value; empty->generation = ++nextGeneration; wcscpy_s(empty->port, L"未知（附加前已打开）"); result = *empty; }
    ReleaseSRWLockExclusive(&handleLock);
    if (!empty && shared) Lose(shared->diagnostics.handleCapacity, shared->dropped.Epoch());
    return empty != nullptr;
}
static bool ObservationEnabled() {
    if (!shared || !shared->enabled || observationDepth) return false;
    auto now = static_cast<LONG64>(GetTickCount64());
    auto next = InterlockedCompareExchange64(&nextOwnerCheck, 0, 0);
    if (now >= next && InterlockedCompareExchange64(&nextOwnerCheck, now + 1000, next) == next) {
        HANDLE owner = OpenSemaphoreW(SYNCHRONIZE, FALSE, ownerName.c_str());
        if (owner) realClose(owner);
        else if (GetLastError() == ERROR_FILE_NOT_FOUND) {
            DWORD lock = WaitForSingleObject(queueMutex, 0);
            if (lock == WAIT_OBJECT_0 || lock == WAIT_ABANDONED) {
                // Recheck under the same lock used by a new host to enable its session.
                owner = OpenSemaphoreW(SYNCHRONIZE, FALSE, ownerName.c_str());
                if (owner) realClose(owner);
                else if (GetLastError() == ERROR_FILE_NOT_FOUND) shared->enabled = 0;
                ReleaseMutex(queueMutex);
            }
        }
    }
    return shared->enabled != 0;
}
static bool SnapshotSession(CaptureRecord& record) {
    if (!ObservationEnabled()) return false;
    record.epoch = shared->dropped.Epoch();
    DWORD lock = WaitForSingleObject(queueMutex, QueueWaitMilliseconds);
    if (lock != WAIT_OBJECT_0 && lock != WAIT_ABANDONED) { Lose(shared->diagnostics.snapshotContention, record.epoch); return false; }
    bool enabled = shared->enabled != 0;
    if (enabled) { record.session = shared->session; record.epoch = shared->dropped.Epoch(); }
    ReleaseMutex(queueMutex);
    return enabled;
}
static bool BeginRecord(HANDLE handle, Api api, DWORD requested, CaptureRecord& record) {
    SerialHandle serial{};
    if (!ObservationEnabled() || !Serial(handle, serial) || !SnapshotSession(record)) return false;
    record.handle = reinterpret_cast<ULONG_PTR>(handle); record.generation = serial.generation;
    wcscpy_s(record.port, serial.port);
    record.api = api; record.direction = (api == ApiRead || api == ApiReadEx) ? 1 : (api == ApiWrite || api == ApiWriteEx) ? 0 : 2;
    record.requested = requested; record.request = static_cast<uint64_t>(InterlockedIncrement64(&nextRequest));
    return true;
}
static void Publish(CaptureRecord& record) {
    LARGE_INTEGER started{}, ended{}; QueryPerformanceCounter(&started);
    auto loseRecord = [&](SessionCounter& reason) {
        if (record.api == ApiGap) shared->diagnostics.notificationLoss.Add(record.epoch);
        else Lose(reason, record.epoch);
    };
    DWORD lock = WaitForSingleObject(queueMutex, QueueWaitMilliseconds);
    if (lock != WAIT_OBJECT_0 && lock != WAIT_ABANDONED) { loseRecord(shared->diagnostics.publishContention); return; }
    if (shared->enabled && !memcmp(&record.session, &shared->session, sizeof(GUID))) {
        if (shared->count >= QueueCapacity || shared->writeIndex >= QueueCapacity || shared->readIndex >= QueueCapacity)
            loseRecord(shared->diagnostics.queueFull);
        else {
            record.sequence = ++shared->sequence;
            record.dropped = static_cast<uint32_t>(shared->dropped);
            shared->records[shared->writeIndex] = record;
            shared->writeIndex = (shared->writeIndex + 1) % QueueCapacity;
            shared->count++;
            shared->diagnostics.highWater = (std::max)(shared->diagnostics.highWater, shared->count);
        }
        QueryPerformanceCounter(&ended);
        auto elapsed = static_cast<uint64_t>(ended.QuadPart - started.QuadPart);
        ++shared->diagnostics.publishCalls; shared->diagnostics.publishTicks += elapsed;
        shared->diagnostics.maxPublishTicks = (std::max)(shared->diagnostics.maxPublishTicks, elapsed);
    }
    ReleaseMutex(queueMutex);
}
static void SnapshotCompletion(CaptureRecord& record, const void* data, DWORD completed, BOOL success, DWORD error) {
    FILETIME now{}; GetSystemTimePreciseAsFileTime(&now);
    record.time = (static_cast<uint64_t>(now.dwHighDateTime) << 32) | now.dwLowDateTime;
    record.succeeded = success; record.error = error; record.completed = completed;
    if (!success) record.integrity = Failed;
    if (success) {
        record.dataLength = (std::min<DWORD>)(completed, PayloadCapacity);
        if (completed > record.requested || !SafeCopy(record.data, data, record.dataLength)) {
            record.dataLength = 0; record.integrity = Unavailable;
        } else if (completed > PayloadCapacity) record.integrity = Truncated;
    }
}
static void Finish(CaptureRecord& record, const void* data, DWORD completed, BOOL success, DWORD error) {
    SnapshotCompletion(record, data, completed, success, error);
    Publish(record);
}
static bool StorePending(OVERLAPPED* overlapped, HANDLE handle, const void* buffer, const CaptureRecord& record, LPOVERLAPPED_COMPLETION_ROUTINE callback = nullptr) {
    AcquireSRWLockExclusive(&pendingLock);
    PendingRequest* slot = nullptr;
    for (auto& item : pending) {
        if (item.overlapped == overlapped) {
            Lose(shared->diagnostics.ambiguousCompletion, item.callback ? record.epoch : item.record.epoch);
            // Never lose a business callback, even for an invalid early OVERLAPPED reuse.
            if (item.callback) { ReleaseSRWLockExclusive(&pendingLock); return false; }
            RememberRetired(overlapped, true, item.handle, item.record.generation);
            slot = &item; break;
        }
        if (!item.overlapped && !slot) slot = &item;
    }
    if (slot) {
        *slot = {overlapped, handle, buffer, record, callback, HasRetired(overlapped)};
        slot->record.skipsSuccessfulPacket = KnownSkipSuccessfulPacket(handle, record.generation);
        HANDLE event = nullptr;
        if (SafeCopy(&event, &overlapped->hEvent, sizeof(event)))
            slot->record.suppressesPacket = (reinterpret_cast<ULONG_PTR>(event) & 1) != 0;
    }
    else Lose(shared->diagnostics.pendingCapacity, record.epoch);
    ReleaseSRWLockExclusive(&pendingLock);
    return slot != nullptr;
}
static LPOVERLAPPED_COMPLETION_ROUTINE CompletePending(OVERLAPPED* overlapped, HANDLE handle, DWORD count, BOOL success, DWORD error, uint64_t requestId = 0, bool callbackCompletion = false, bool packetCompletion = false, bool noPacketExpected = false) {
    if (!overlapped) return nullptr;
    PendingRequest completed{};
    bool ambiguous = false;
    AcquireSRWLockExclusive(&pendingLock);
    for (auto& item : pending) {
        if (item.overlapped == overlapped && (!handle || item.handle == handle) && (!requestId || item.record.request == requestId) && (callbackCompletion || !item.callback)) {
            completed = item;
            ambiguous = packetCompletion && (item.ambiguousPacket || error == ERROR_IO_INCOMPLETE);
            if (ambiguous) RememberRetired(overlapped, true, item.handle, item.record.generation);
            else if (packetCompletion) ForgetRetired(overlapped);
            else if (!item.callback && !noPacketExpected && !item.record.suppressesPacket && !KnownWithoutCompletionPort(item.handle, item.record.generation))
                RememberRetired(overlapped, false, item.handle, item.record.generation);
            // Snapshot before removing ownership: another completion consumer may immediately
            // return to business code and release/reuse the read buffer after this lock is released.
            if (!ambiguous && shared->enabled && completed.record.epoch == shared->dropped.Epoch())
                SnapshotCompletion(completed.record, completed.record.direction == 0 ? completed.record.attempt : completed.buffer,
                    count, success, error);
            item = {}; break;
        }
    }
    if (!completed.overlapped && packetCompletion) ForgetRetired(overlapped);
    ReleaseSRWLockExclusive(&pendingLock);
    if (!completed.overlapped) return nullptr;
    // Old-session completions still dispatch callbacks, but their buffers are not inspected.
    DWORD lock = WaitForSingleObject(queueMutex, QueueWaitMilliseconds);
    bool active = false;
    if (lock == WAIT_OBJECT_0 || lock == WAIT_ABANDONED) {
        active = shared->enabled && !memcmp(&completed.record.session, &shared->session, sizeof(GUID));
        ReleaseMutex(queueMutex);
    } else Lose(shared->diagnostics.completionContention, completed.record.epoch);
    if (active && ambiguous) {
        FILETIME now{}; GetSystemTimePreciseAsFileTime(&now);
        completed.record.time = (static_cast<uint64_t>(now.dwHighDateTime) << 32) | now.dwLowDateTime;
        completed.record.api = ApiGap; completed.record.direction = 2; completed.record.integrity = Unavailable;
        wcscpy_s(completed.record.detail, L"完成来源不明确：OVERLAPPED 在旧完成包取出前被复用；此地址的 IOCP 记录需重启目标后恢复。");
        Lose(shared->diagnostics.ambiguousCompletion, completed.record.epoch); Publish(completed.record);
    } else if (active) Publish(completed.record);
    return completed.callback;
}
static void ImmediateResult(HANDLE handle, OVERLAPPED* overlapped, const CaptureRecord& record, BOOL success, DWORD error) {
    if (!success && error == ERROR_IO_PENDING) return;
    DWORD actual = 0;
    if (success) {
        // IOCP consumers may already have dispatched/freed/reused this address before the API returns.
        // Only an observed event-only handle permits an observer-owned result query.
        // For associated or pre-attachment handles, use the caller's completion query or actual IOCP packet.
        bool query = false, observed = false;
        AcquireSRWLockExclusive(&pendingLock);
        for (const auto& item : pending) if (item.overlapped == overlapped && item.handle == handle && item.record.request == record.request) {
            query = item.record.suppressesPacket || KnownWithoutCompletionPort(handle, record.generation)
                || item.record.skipsSuccessfulPacket;
            break;
        }
        // Other covered consumers cannot return and recycle the storage during this non-waiting query.
        if (query) observed = CallOriginal([&] { return realGetResult(handle, overlapped, &actual, FALSE); }) != FALSE;
        ReleaseSRWLockExclusive(&pendingLock);
        if (!observed) return;
    }
    CompletePending(overlapped, handle, actual, success, error, record.request, false, false, true);
}

static BOOL WINAPI HookRead(HANDLE handle, LPVOID data, DWORD length, LPDWORD completed, LPOVERLAPPED overlapped) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiRead, length, record);
    if (capture && overlapped) capture = StorePending(overlapped, handle, data, record);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realRead(handle, data, length, completed, overlapped); }); DWORD error = GetLastError();
    DWORD actual = 0;
    if (capture && overlapped) ImmediateResult(handle, overlapped, record, result, error);
    else if (capture) { if (result && completed) SafeCopy(&actual, completed, sizeof(actual)); Finish(record, data, actual, result, error); }
    SetLastError(error); return result;
}
static BOOL WINAPI HookWrite(HANDLE handle, LPCVOID data, DWORD length, LPDWORD completed, LPOVERLAPPED overlapped) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiWrite, length, record);
    if (capture) {
        record.attemptLength = (std::min<DWORD>)(length, PayloadCapacity);
        if (!SafeCopy(record.attempt, data, record.attemptLength)) { record.attemptLength = 0; record.integrity = Unavailable; }
        else if (length > PayloadCapacity) record.integrity = Truncated;
    }
    if (capture && overlapped) capture = StorePending(overlapped, handle, data, record);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realWrite(handle, data, length, completed, overlapped); }); DWORD error = GetLastError();
    DWORD actual = 0;
    if (capture && overlapped) ImmediateResult(handle, overlapped, record, result, error);
    else if (capture) { if (result && completed) SafeCopy(&actual, completed, sizeof(actual)); Finish(record, record.attempt, actual, result, error); }
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetResult(HANDLE handle, LPOVERLAPPED overlapped, LPDWORD count, BOOL wait) {
    if (observationDepth) return realGetResult(handle, overlapped, count, wait);
    DWORD incoming = GetLastError(); auto requestId = PendingId(overlapped, handle); SetLastError(incoming);
    BOOL result = realGetResult(handle, overlapped, count, wait); DWORD error = GetLastError();
    DWORD actual = 0;
    if (result && count) SafeCopy(&actual, count, sizeof(actual));
    if (requestId && (result || error != ERROR_IO_INCOMPLETE)) CompletePending(overlapped, handle, actual, result, error, requestId);
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetCompletion(HANDLE port, LPDWORD count, PULONG_PTR key, LPOVERLAPPED* overlapped, DWORD timeout) {
    BOOL result = realGetCompletion(port, count, key, overlapped, timeout); DWORD error = GetLastError();
    OVERLAPPED* completed = nullptr; DWORD actual = 0;
    if (overlapped) SafeCopy(&completed, overlapped, sizeof(completed));
    if (completed && count) SafeCopy(&actual, count, sizeof(actual));
    if (completed) CompletePending(completed, nullptr, actual, result, error, 0, false, true);
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetResultEx(HANDLE handle, LPOVERLAPPED overlapped, LPDWORD count, DWORD timeout, BOOL alertable) {
    if (observationDepth) return realGetResultEx(handle, overlapped, count, timeout, alertable);
    DWORD incoming = GetLastError(); auto requestId = PendingId(overlapped, handle); SetLastError(incoming);
    BOOL result = realGetResultEx(handle, overlapped, count, timeout, alertable); DWORD error = GetLastError();
    DWORD actual = 0;
    if (result && count) SafeCopy(&actual, count, sizeof(actual));
    // A timed-out/alertable wait does not complete or cancel the underlying I/O.
    if (requestId && (result || (error != ERROR_IO_INCOMPLETE && error != WAIT_TIMEOUT && error != WAIT_IO_COMPLETION)))
        CompletePending(overlapped, handle, actual, result, error, requestId);
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetCompletionEx(HANDLE port, LPOVERLAPPED_ENTRY entries, ULONG capacity, PULONG removed, DWORD timeout, BOOL alertable) {
    BOOL result = realGetCompletionEx(port, entries, capacity, removed, timeout, alertable); DWORD error = GetLastError();
    ULONG count = 0;
    if (result && removed && SafeCopy(&count, removed, sizeof(count)) && count <= capacity) {
        for (ULONG i = 0; i < count; ++i) {
            OVERLAPPED_ENTRY entry{}; ULONG_PTR status = 0;
            if (!SafeCopy(&entry, entries + i, sizeof(entry))) break;
            if (!entry.lpOverlapped) continue;
            // OVERLAPPED_ENTRY.Internal is reserved. The documented status is OVERLAPPED.Internal.
            bool known = SafeCopy(&status, &entry.lpOverlapped->Internal, sizeof(status));
            LONG ntstatus = static_cast<LONG>(status);
            BOOL success = known && ntstatus >= 0 && ntstatus != 0x103; // STATUS_PENDING is not a completion.
            DWORD failure = !known ? ERROR_INVALID_DATA : ntstatus == 0x103 ? ERROR_IO_INCOMPLETE
                : success ? ERROR_SUCCESS : statusToError ? statusToError(ntstatus) : ERROR_MR_MID_NOT_FOUND;
            CompletePending(entry.lpOverlapped, nullptr, entry.dwNumberOfBytesTransferred, success, failure, 0, false, true);
        }
    }
    SetLastError(error); return result;
}
static HANDLE WINAPI HookAssociate(HANDLE file, HANDLE existing, ULONG_PTR key, DWORD concurrency) {
    HANDLE result = realAssociate(file, existing, key, concurrency); DWORD error = GetLastError();
    SerialHandle serial{};
    if (result && file != INVALID_HANDLE_VALUE && Serial(file, serial)) {
        AcquireSRWLockExclusive(&handleLock);
        for (auto& entry : handles) if (entry.value == file && entry.generation == serial.generation) {
            entry.portKnown = true; entry.completionPort = result; break;
        }
        ReleaseSRWLockExclusive(&handleLock);
        if (!existing) {
            // Successful association with a newly created port proves earlier event-only queries had no queued packets.
            AcquireSRWLockExclusive(&pendingLock);
            for (auto& entry : retired) if (!entry.ambiguous && entry.handle == file && entry.generation == serial.generation) entry = {};
            ReleaseSRWLockExclusive(&pendingLock);
        }
    }
    SetLastError(error); return result;
}
static void WINAPI CompletionCallback(DWORD error, DWORD count, OVERLAPPED* overlapped) {
    DWORD incoming = GetLastError();
    auto callback = CompletePending(overlapped, nullptr, count, error == ERROR_SUCCESS, error, 0, true);
    SetLastError(incoming);
    if (callback) callback(error, count, overlapped);
}
static BOOL WINAPI HookReadEx(HANDLE handle, LPVOID data, DWORD length, LPOVERLAPPED overlapped, LPOVERLAPPED_COMPLETION_ROUTINE callback) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = callback && overlapped && BeginRecord(handle, ApiReadEx, length, record);
    if (capture) capture = StorePending(overlapped, handle, data, record, callback);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realReadEx(handle, data, length, overlapped, capture ? CompletionCallback : callback); });
    DWORD error = GetLastError();
    if (capture && !result) CompletePending(overlapped, handle, 0, FALSE, error, record.request, true);
    SetLastError(error); return result;
}
static BOOL WINAPI HookWriteEx(HANDLE handle, LPCVOID data, DWORD length, LPOVERLAPPED overlapped, LPOVERLAPPED_COMPLETION_ROUTINE callback) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = callback && overlapped && BeginRecord(handle, ApiWriteEx, length, record);
    if (capture) {
        record.attemptLength = (std::min<DWORD>)(length, PayloadCapacity);
        if (!SafeCopy(record.attempt, data, record.attemptLength)) { record.attemptLength = 0; record.integrity = Unavailable; }
        else if (length > PayloadCapacity) record.integrity = Truncated;
        capture = StorePending(overlapped, handle, data, record, callback);
    }
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realWriteEx(handle, data, length, overlapped, capture ? CompletionCallback : callback); });
    DWORD error = GetLastError();
    if (capture && !result) CompletePending(overlapped, handle, 0, FALSE, error, record.request, true);
    SetLastError(error); return result;
}
static BOOL WINAPI HookClose(HANDLE handle) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiClose, 0, record);
    uint64_t generation = 0;
    AcquireSRWLockShared(&handleLock);
    for (const auto& entry : handles) if (entry.value == handle) { generation = entry.generation; break; }
    ReleaseSRWLockShared(&handleLock);
    // Business I/O may block; never keep the observation lock across the real API.
    SetLastError(incoming); BOOL result = realClose(handle); DWORD error = GetLastError();
    if (result && generation) {
        AcquireSRWLockExclusive(&handleLock);
        for (auto& entry : handles) if (entry.value == handle && entry.generation == generation) { entry = {}; break; }
        ReleaseSRWLockExclusive(&handleLock);
    }
    if (capture) Finish(record, nullptr, 0, result, error);
    SetLastError(error); return result;
}
template<typename Character> static bool PortName(const Character* pointer, wchar_t (&name)[32]) {
    wchar_t path[64]{};
    for (size_t i = 0; i < 63; ++i) {
        Character value{};
        if (!SafeCopy(&value, pointer + i, sizeof(value))) return false;
        path[i] = static_cast<wchar_t>(value);
        if (!value) break;
    }
    const wchar_t* port = path;
    if (wcsncmp(port, L"\\\\.\\", 4) == 0 || wcsncmp(port, L"\\\\?\\", 4) == 0) port += 4;
    if (_wcsnicmp(port, L"COM", 3) != 0) return false;
    size_t length = 3;
    while (port[length] >= L'0' && port[length] <= L'9') ++length;
    if (length == 3 || length >= 32 || (port[length] && !(port[length] == L':' && port[length + 1] == 0))) return false;
    wcsncpy_s(name, port, length); name[0] = L'C'; name[1] = L'O'; name[2] = L'M'; return true;
}
template<typename Character, typename F> static HANDLE ObserveOpen(const Character* path, const wchar_t* api, DWORD access, DWORD flags, F operation) {
    DWORD incoming = GetLastError(); CaptureRecord record{}; wchar_t port[32]{};
    bool eligible = ObservationEnabled();
    bool namedPort = eligible && PortName(path, port);
    bool capture = namedPort && SnapshotSession(record);
    SetLastError(incoming);
    HANDLE handle = CallOriginal(operation); DWORD error = GetLastError();
    SerialHandle serial{};
    bool serialOpened = eligible && handle != INVALID_HANDLE_VALUE && Serial(handle, serial, true);
    if (serialOpened && !namedPort) capture = SnapshotSession(record);
    if (capture && (serialOpened || (handle == INVALID_HANDLE_VALUE && namedPort))) {
        record.api = ApiOpen; record.direction = 2; record.handle = reinterpret_cast<ULONG_PTR>(handle);
        record.generation = serial.generation; record.request = static_cast<uint64_t>(InterlockedIncrement64(&nextRequest));
        if (serialOpened) {
            AcquireSRWLockExclusive(&handleLock);
            for (auto& entry : handles) if (entry.value == handle && entry.generation == serial.generation) {
                entry.portKnown = true; entry.completionPort = nullptr; break;
            }
            ReleaseSRWLockExclusive(&handleLock);
        }
        if (namedPort) {
            wcscpy_s(record.port, port);
            if (handle != INVALID_HANDLE_VALUE) {
                AcquireSRWLockExclusive(&handleLock);
                for (auto& entry : handles) if (entry.value == handle && entry.generation == serial.generation) { wcscpy_s(entry.port, port); break; }
                ReleaseSRWLockExclusive(&handleLock);
            }
        } else wcscpy_s(record.port, L"未知（未取得 COM 名称）");
        _snwprintf_s(record.detail, _TRUNCATE, L"%s Access=0x%lX Flags=0x%lX", api, access, flags);
        Finish(record, nullptr, 0, handle != INVALID_HANDLE_VALUE, error);
    }
    SetLastError(error); return handle;
}
static HANDLE WINAPI HookOpenW(LPCWSTR path, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templ) {
    return ObserveOpen(path, L"CreateFileW", access, flags, [&] { return realOpenW(path, access, share, security, disposition, flags, templ); });
}
static HANDLE WINAPI HookOpenA(LPCSTR path, DWORD access, DWORD share, LPSECURITY_ATTRIBUTES security, DWORD disposition, DWORD flags, HANDLE templ) {
    return ObserveOpen(path, L"CreateFileA", access, flags, [&] { return realOpenA(path, access, share, security, disposition, flags, templ); });
}
static void DescribeState(CaptureRecord& record, const DCB* pointer) {
    DCB state{};
    if (!SafeCopy(&state, pointer, sizeof(state))) { wcscpy_s(record.detail, L"参数未知"); return; }
    _snwprintf_s(record.detail, _TRUNCATE, L"Baud=%lu Byte=%u Parity=%u Stop=%u CTS=%lu DSR=%lu OutX=%lu InX=%lu DTR=%lu RTS=%lu",
        state.BaudRate, state.ByteSize, state.Parity, state.StopBits, state.fOutxCtsFlow, state.fOutxDsrFlow,
        state.fOutX, state.fInX, state.fDtrControl, state.fRtsControl);
}
static BOOL WINAPI HookSetState(HANDLE handle, LPDCB state) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiSetState, 0, record);
    if (capture) DescribeState(record, state);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realSetState(handle, state); }); DWORD error = GetLastError();
    if (capture) Finish(record, nullptr, 0, result, error);
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetState(HANDLE handle, LPDCB state) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiGetState, 0, record);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realGetState(handle, state); }); DWORD error = GetLastError();
    if (capture) { if (result) DescribeState(record, state); else wcscpy_s(record.detail, L"参数未知"); Finish(record, nullptr, 0, result, error); }
    SetLastError(error); return result;
}
static void DescribeTimeouts(CaptureRecord& record, const COMMTIMEOUTS* pointer) {
    COMMTIMEOUTS value{};
    if (!SafeCopy(&value, pointer, sizeof(value))) { wcscpy_s(record.detail, L"参数未知"); return; }
    _snwprintf_s(record.detail, _TRUNCATE, L"Interval=%lu RMult=%lu RConst=%lu WMult=%lu WConst=%lu",
        value.ReadIntervalTimeout, value.ReadTotalTimeoutMultiplier, value.ReadTotalTimeoutConstant,
        value.WriteTotalTimeoutMultiplier, value.WriteTotalTimeoutConstant);
}
static BOOL WINAPI HookSetTimeouts(HANDLE handle, LPCOMMTIMEOUTS value) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiSetTimeouts, 0, record);
    if (capture) DescribeTimeouts(record, value);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realSetTimeouts(handle, value); }); DWORD error = GetLastError();
    if (capture) Finish(record, nullptr, 0, result, error);
    SetLastError(error); return result;
}
static BOOL WINAPI HookGetTimeouts(HANDLE handle, LPCOMMTIMEOUTS value) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, ApiGetTimeouts, 0, record);
    SetLastError(incoming);
    BOOL result = CallOriginal([&] { return realGetTimeouts(handle, value); }); DWORD error = GetLastError();
    if (capture) { if (result) DescribeTimeouts(record, value); else wcscpy_s(record.detail, L"参数未知"); Finish(record, nullptr, 0, result, error); }
    SetLastError(error); return result;
}
template<typename F> static BOOL ObserveControl(HANDLE handle, Api api, const wchar_t* detail, F operation) {
    DWORD incoming = GetLastError(); CaptureRecord record{};
    bool capture = BeginRecord(handle, api, 0, record);
    if (capture) wcsncpy_s(record.detail, detail, _TRUNCATE);
    SetLastError(incoming);
    BOOL result = CallOriginal(operation); DWORD error = GetLastError();
    if (capture) Finish(record, nullptr, 0, result, error);
    SetLastError(error); return result;
}
static BOOL WINAPI HookEscape(HANDLE handle, DWORD function) {
    DWORD incoming = GetLastError(); wchar_t detail[128]{};
    const wchar_t* name = function == SETDTR ? L"SETDTR" : function == CLRDTR ? L"CLRDTR"
        : function == SETRTS ? L"SETRTS" : function == CLRRTS ? L"CLRRTS" : L"EscapeCommFunction";
    _snwprintf_s(detail, _TRUNCATE, L"%s Function=%lu", name, function); SetLastError(incoming);
    return ObserveControl(handle, ApiEscape, detail, [&] { return realEscape(handle, function); });
}
static BOOL WINAPI HookPurge(HANDLE handle, DWORD flags) {
    DWORD incoming = GetLastError(); wchar_t detail[128]{};
    _snwprintf_s(detail, _TRUNCATE, L"PurgeComm Flags=0x%lX (TXABORT=1 RXABORT=2 TXCLEAR=4 RXCLEAR=8)", flags); SetLastError(incoming);
    return ObserveControl(handle, ApiPurge, detail, [&] { return realPurge(handle, flags); });
}
static BOOL WINAPI HookCancel(HANDLE handle) {
    return ObserveControl(handle, ApiCancel, L"CancelIo（当前线程挂起请求）", [&] { return realCancel(handle); });
}
static BOOL WINAPI HookCancelEx(HANDLE handle, LPOVERLAPPED request) {
    DWORD incoming = GetLastError(); wchar_t detail[128]{};
    _snwprintf_s(detail, _TRUNCATE, L"CancelIoEx OVERLAPPED=%p（接受取消不等于请求完成）", request); SetLastError(incoming);
    return ObserveControl(handle, ApiCancel, detail, [&] { return realCancelEx(handle, request); });
}
static BOOL WINAPI HookWaitComm(HANDLE handle, LPDWORD events, LPOVERLAPPED overlapped) {
    DWORD incoming = GetLastError();
    CaptureRecord abandoned{};
    if (overlapped && !observationDepth) {
        AcquireSRWLockExclusive(&pendingLock);
        for (auto& item : pending) if (item.overlapped == overlapped) {
            abandoned = item.record;
            RememberRetired(overlapped, true, item.handle, item.record.generation);
            // Even invalid reuse must not discard an already-wrapped business callback.
            if (item.callback) item.record.session = {};
            else item = {};
            break;
        }
        ReleaseSRWLockExclusive(&pendingLock);
        if (abandoned.request) {
            DWORD lock = WaitForSingleObject(queueMutex, QueueWaitMilliseconds);
            bool active = false;
            if (lock == WAIT_OBJECT_0 || lock == WAIT_ABANDONED) {
                active = shared->enabled && !memcmp(&shared->session, &abandoned.session, sizeof(GUID));
                ReleaseMutex(queueMutex);
            } else Lose(shared->diagnostics.completionContention, abandoned.epoch);
            if (active) {
                abandoned.api = ApiGap; abandoned.direction = 2; abandoned.integrity = Unavailable;
                wcscpy_s(abandoned.detail, L"完成未被观察：OVERLAPPED 已被 WaitCommEvent 复用；不将控制结果记作串口字节。请重新启动目标并从启动时监控。");
                Lose(shared->diagnostics.ambiguousCompletion, abandoned.epoch);
                Finish(abandoned, nullptr, 0, TRUE, ERROR_SUCCESS);
            }
        }
    }
    SetLastError(incoming);
    return CallOriginal([&] { return realWaitComm(handle, events, overlapped); });
}
static BOOL WINAPI HookNotificationModes(HANDLE handle, UCHAR flags) {
    BOOL result = CallOriginal([&] { return realNotificationModes(handle, flags); }); DWORD error = GetLastError();
    SerialHandle serial{};
    if (result && ObservationEnabled() && Serial(handle, serial)) {
        AcquireSRWLockExclusive(&handleLock);
        for (auto& entry : handles) if (entry.value == handle && entry.generation == serial.generation) {
            entry.skipSuccessfulPacket = entry.skipSuccessfulPacket || (flags & FILE_SKIP_COMPLETION_PORT_ON_SUCCESS) != 0;
            break;
        }
        ReleaseSRWLockExclusive(&handleLock);
    }
    SetLastError(error); return result;
}

extern "C" DWORD WINAPI InitializeProcessMonitor(LPVOID) {
    AcquireSRWLockExclusive(&initializationLock);
    if (installed) { ReleaseSRWLockExclusive(&initializationLock); return ERROR_SUCCESS; }
    auto name = MappingName(GetCurrentProcessId(), ProcessCreationTime(GetCurrentProcess()));
    ownerName = name + L".owner";
    mappingHandle = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, name.c_str());
    shared = mappingHandle ? static_cast<HookShared*>(MapViewOfFile(mappingHandle, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(HookShared))) : nullptr;
    if (!shared || shared->version != HookVersion || shared->recordSize != sizeof(HookRecord) || shared->capacity != QueueCapacity) {
        if (shared) UnmapViewOfFile(shared);
        if (mappingHandle) CloseHandle(mappingHandle);
        mappingHandle = nullptr;
        shared = nullptr; ReleaseSRWLockExclusive(&initializationLock); return ERROR_REVISION_MISMATCH;
    }
    queueMutex = OpenMutexW(SYNCHRONIZE | MUTEX_MODIFY_STATE, FALSE, (name + L".lock").c_str());
    statusToError = reinterpret_cast<decltype(statusToError)>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "RtlNtStatusToDosError"));
    LONG result = queueMutex ? DetourTransactionBegin() : ERROR_INVALID_HANDLE;
    std::vector<HANDLE> threads;
    if (result == NO_ERROR) {
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        THREADENTRY32 entry{}; entry.dwSize = sizeof(entry);
        if (snapshot == INVALID_HANDLE_VALUE) result = GetLastError();
        else {
            if (!Thread32First(snapshot, &entry)) result = GetLastError();
            else do {
                if (entry.th32OwnerProcessID != GetCurrentProcessId() || entry.th32ThreadID == GetCurrentThreadId()) continue;
                HANDLE thread = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_SET_CONTEXT | THREAD_QUERY_INFORMATION, FALSE, entry.th32ThreadID);
                if (!thread) { result = GetLastError(); break; }
                threads.push_back(thread);
            } while (Thread32Next(snapshot, &entry));
            CloseHandle(snapshot);
        }
        // Allocate/enumerate before suspending any thread; a suspended thread may own the CRT heap lock.
        if (result == NO_ERROR) for (HANDLE thread : threads) { result = DetourUpdateThread(thread); if (result != NO_ERROR) break; }
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realRead), HookRead);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realWrite), HookWrite);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realClose), HookClose);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realOpenW), HookOpenW);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realOpenA), HookOpenA);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetResult), HookGetResult);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetResultEx), HookGetResultEx);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetCompletion), HookGetCompletion);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetCompletionEx), HookGetCompletionEx);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realAssociate), HookAssociate);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realReadEx), HookReadEx);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realWriteEx), HookWriteEx);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realSetState), HookSetState);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetState), HookGetState);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realSetTimeouts), HookSetTimeouts);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realGetTimeouts), HookGetTimeouts);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realEscape), HookEscape);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realPurge), HookPurge);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realCancel), HookCancel);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realCancelEx), HookCancelEx);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realWaitComm), HookWaitComm);
        if (result == NO_ERROR) result = DetourAttach(reinterpret_cast<PVOID*>(&realNotificationModes), HookNotificationModes);
        if (result == NO_ERROR) result = DetourTransactionCommit();
        else DetourTransactionAbort();
    }
    for (HANDLE thread : threads) CloseHandle(thread);
    installed = result == NO_ERROR;
    shared->initialized = installed ? 1 : static_cast<uint32_t>(result);
    if (!installed) {
        if (queueMutex) CloseHandle(queueMutex);
        queueMutex = nullptr; UnmapViewOfFile(shared); shared = nullptr;
        CloseHandle(mappingHandle); mappingHandle = nullptr;
    }
    ReleaseSRWLockExclusive(&initializationLock);
    return static_cast<DWORD>(result);
}

BOOL WINAPI DllMain(HINSTANCE, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH && !DetourIsHelperProcess()) {
        DetourRestoreAfterWith();
        auto name = MappingName(GetCurrentProcessId(), ProcessCreationTime(GetCurrentProcess()));
        HANDLE mapping = OpenFileMappingW(FILE_MAP_READ, FALSE, name.c_str());
        auto config = mapping ? static_cast<const HookShared*>(MapViewOfFile(mapping, FILE_MAP_READ, 0, 0, sizeof(HookShared))) : nullptr;
        bool startup = config && config->version == HookVersion && config->startup == 1;
        if (config) UnmapViewOfFile(config);
        if (mapping) CloseHandle(mapping);
        if (startup && InitializeProcessMonitor(nullptr) != ERROR_SUCCESS) return FALSE;
    }
    // No forced unloading: callbacks and in-flight I/O may still execute this module.
    return TRUE;
}
