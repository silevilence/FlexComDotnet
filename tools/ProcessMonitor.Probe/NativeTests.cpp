#include "../../src/FlexCom.ProcessHook/Shared.h"
#include <stdexcept>
#include <iostream>
#include <fstream>

static std::wstring OutputPath() {
    wchar_t path[MAX_PATH]{}; GetModuleFileNameW(nullptr, path, MAX_PATH);
    auto result = std::wstring(path);
    return result.substr(0, result.find_last_of(L"\\") + 1) + L"native-results.txt";
}
static HANDLE OpenPort(const wchar_t* name, DWORD flags = 0) {
    HANDLE port = CreateFileW(name, GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, flags, nullptr);
    if (port == INVALID_HANDLE_VALUE) throw std::runtime_error("open dedicated serial port; Win32=" + std::to_string(GetLastError())
        + "; flags=" + std::to_string(flags));
    DCB state{}; state.DCBlength = sizeof(state); GetCommState(port, &state);
    state.BaudRate = 115200; state.ByteSize = 8; state.Parity = NOPARITY; state.StopBits = ONESTOPBIT;
    state.fBinary = TRUE; state.fOutxCtsFlow = FALSE; state.fOutxDsrFlow = FALSE;
    state.fOutX = FALSE; state.fInX = FALSE; state.fDtrControl = DTR_CONTROL_ENABLE; state.fRtsControl = RTS_CONTROL_ENABLE;
    SetCommState(port, &state);
    COMMTIMEOUTS timeouts{}; timeouts.ReadTotalTimeoutConstant = 200; timeouts.WriteTotalTimeoutConstant = 2000;
    SetCommTimeouts(port, &timeouts); PurgeComm(port, PURGE_RXCLEAR | PURGE_TXCLEAR);
    return port;
}

static void Check(bool ok, const char* message) {
    if (!ok) throw std::runtime_error(std::string(message) + "; Win32=" + std::to_string(GetLastError()));
}
static void Stage(const char* name) { std::ofstream(OutputPath(), std::ios::app) << "stage=" << name << '\n'; }
static DWORD callbackCount = 0, callbackError = 0, callbackBytes = 0;
static OVERLAPPED* callbackRequest = nullptr;
static void WINAPI Callback(DWORD error, DWORD bytes, OVERLAPPED* request) {
    callbackCount++; callbackError = error; callbackBytes = bytes; callbackRequest = request;
}
static void AwaitCallback(DWORD expected) {
    for (int i = 0; i < 100 && callbackCount < expected; i++) SleepEx(20, TRUE);
    Check(callbackCount == expected, "original callback count");
}
// New control/open/close observations do not change the byte-completion assertions below.
static uint32_t DataCount(const HookShared* queue) {
    uint32_t count = 0;
    for (uint32_t i = 0; i < queue->count; ++i) if (queue->records[i].direction != 2) ++count;
    return count;
}
static const HookRecord& DataAt(const HookShared* queue, uint32_t index) {
    for (uint32_t i = 0; i < queue->count; ++i) if (queue->records[i].direction != 2) {
        if (index == 0) return queue->records[i];
        --index;
    }
    throw std::runtime_error("missing data completion");
}
static int Run(bool serial) {
    try {
        Stage("initial ports");
        HANDLE target = serial ? OpenPort(L"\\\\.\\COM10") : nullptr;
        HANDLE peer = serial ? OpenPort(L"\\\\.\\COM20") : nullptr;
        auto name = MappingName(GetCurrentProcessId(), ProcessCreationTime(GetCurrentProcess()));
        HANDLE mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(HookShared), name.c_str());
        auto shared = static_cast<HookShared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(HookShared)));
        Check(shared != nullptr, "create shared queue");
        shared->version = HookVersion; shared->recordSize = sizeof(HookRecord); shared->capacity = QueueCapacity;
        shared->enabled = 1; shared->session.Data1 = 123;
        HANDLE mutex = CreateMutexW(nullptr, FALSE, (name + L".lock").c_str());
        HANDLE owner = CreateSemaphoreW(nullptr, 0, 1, (name + L".owner").c_str());
        HMODULE module = LoadLibraryW(L"FlexCom.ProcessHook.dll");
        Check(module != nullptr, "load production Hook");
        auto initialize = reinterpret_cast<DWORD(WINAPI*)(LPVOID)>(GetProcAddress(module, "InitializeProcessMonitor"));
        Check(initialize && initialize(nullptr) == ERROR_SUCCESS, "initialize production Hook");
        Check(shared->initialized == 1, "host readiness");
        HANDLE nul = CreateFileW(L"NUL", GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
        DWORD count = 0; SetLastError(12345);
        Check(WriteFile(nul, "test", 4, &count, nullptr) && count == 4, "business result");
        Check(GetLastError() == 12345, "last error preservation");
        CloseHandle(nul);
        Check(DataCount(shared) == 0 && shared->dropped == 0, "nonserial exclusion");
        if (serial) {
            const BYTE expected[] = {0, 1, 2, 0x7F, 0x80, 0xFE, 0xFF};
            BYTE received[16]{};
            Check(WriteFile(target, expected, 7, &count, nullptr) && count == 7, "sync target write");
            Check(ReadFile(peer, received, 16, &count, nullptr) && count == 7 && !memcmp(received, expected, 7), "partial peer read");
            Check(WriteFile(peer, expected, 7, &count, nullptr) && count == 7, "sync peer write");
            Check(ReadFile(target, received, 16, &count, nullptr) && count == 7 && !memcmp(received, expected, 7), "partial target read");
            Check(WriteFile(target, expected, 0, &count, nullptr) && count == 0, "zero write");
            Check(DataCount(shared) == 5, "five production sync records");
            for (uint32_t i = 0; i < 5; i++) {
                const auto& record = DataAt(shared, i);
                Check(record.succeeded && record.completed == (i == 4 ? 0u : 7u), "actual completion count");
                Check(record.dataLength == record.completed && !memcmp(record.data, expected, record.dataLength), "production payload");
                Check(record.direction == (i % 2), "direction");
                Check(record.generation > 0 && record.request > 0 && record.time > 0, "record identity");
                if (record.direction == 0) Check(record.attemptLength == record.requested && !memcmp(record.attempt, expected, record.attemptLength), "write attempt");
            }
            shared->enabled = 0;
            Check(WriteFile(target, expected, 7, &count, nullptr) && count == 7, "write after stop");
            Check(ReadFile(peer, received, 16, &count, nullptr) && count == 7, "read after stop");
            Check(DataCount(shared) == 5, "stopped capture");
            CloseHandle(target); CloseHandle(peer);
            Stage("event ports");
            target = OpenPort(L"\\\\.\\COM10", FILE_FLAG_OVERLAPPED); peer = OpenPort(L"\\\\.\\COM20");
            shared->count = shared->readIndex = shared->writeIndex = 0; shared->enabled = 1;
            OVERLAPPED request{}; request.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            memset(received, 0xCC, sizeof(received));
            BOOL started = ReadFile(target, received, 7, &count, &request);
            Check(started || GetLastError() == ERROR_IO_PENDING, "event read accepted");
            if (!started) Check(DataCount(shared) == 0, "pending read buffer not captured");
            Check(WriteFile(peer, expected, 7, &count, nullptr), "event peer write");
            Check(GetOverlappedResult(target, &request, &count, TRUE) && count == 7 && !memcmp(received, expected, 7), "event completion");
            Check(GetOverlappedResult(target, &request, &count, TRUE), "repeat query business result");
            Check(DataCount(shared) == 2, "event completion emitted exactly once");
            const auto& eventRecord = DataAt(shared, 1);
            Check(eventRecord.api == ApiRead && eventRecord.completed == 7 && eventRecord.dataLength == 7
                && !memcmp(eventRecord.data, expected, 7), "event completion bytes");
            CloseHandle(request.hEvent);
            HANDLE completionPort = CreateIoCompletionPort(target, nullptr, 99, 0);
            Check(completionPort != nullptr, "create completion port");
            request = {}; memset(received, 0xCC, sizeof(received));
            started = ReadFile(target, received, 7, &count, &request);
            Check(started || GetLastError() == ERROR_IO_PENDING, "IOCP read accepted");
            Check(WriteFile(peer, expected, 7, &count, nullptr), "IOCP peer write");
            ULONG_PTR key = 0; OVERLAPPED* completedRequest = nullptr;
            Check(GetQueuedCompletionStatus(completionPort, &count, &key, &completedRequest, 3000)
                && count == 7 && key == 99 && completedRequest == &request, "IOCP completion");
            Check(GetOverlappedResult(target, &request, &count, FALSE), "query after IOCP");
            Check(DataCount(shared) == 4, "IOCP completion emitted exactly once");
            const auto& portRecord = DataAt(shared, 3);
            Check(portRecord.api == ApiRead && portRecord.completed == 7 && portRecord.dataLength == 7
                && !memcmp(portRecord.data, expected, 7), "IOCP completion bytes");
            shared->enabled = 0;
            CloseHandle(target); CloseHandle(peer); CloseHandle(completionPort);
            Stage("callback ports");
            target = OpenPort(L"\\\\.\\COM10", FILE_FLAG_OVERLAPPED); peer = OpenPort(L"\\\\.\\COM20");
            shared->count = shared->readIndex = shared->writeIndex = 0; shared->enabled = 1;
            request = {}; callbackCount = 0;
            Check(ReadFileEx(target, received, 7, &request, Callback), "callback read accepted");
            Check(WriteFile(peer, expected, 7, &count, nullptr), "callback peer write");
            AwaitCallback(1);
            Check(callbackError == 0 && callbackBytes == 7 && callbackRequest == &request, "original callback arguments");
            Check(DataCount(shared) == 2 && DataAt(shared, 1).api == ApiReadEx
                && DataAt(shared, 1).completed == 7 && !memcmp(DataAt(shared, 1).data, expected, 7), "callback read capture");
            request = {};
            Check(WriteFileEx(target, expected, 7, &request, Callback), "callback write accepted");
            AwaitCallback(2);
            Check(callbackError == 0 && callbackBytes == 7, "original write callback");
            Check(ReadFile(peer, received, 7, &count, nullptr) && count == 7, "callback peer receive");
            Check(DataCount(shared) == 4 && DataAt(shared, 2).api == ApiWriteEx && DataAt(shared, 2).completed == 7, "callback write capture");
            request = {};
            Check(ReadFileEx(target, received, 7, &request, Callback), "cancelled read accepted");
            Check(CancelIoEx(target, &request), "cancel callback request");
            AwaitCallback(3);
            Check(callbackError == ERROR_OPERATION_ABORTED && callbackBytes == 0, "cancelled callback result");
            Check(DataCount(shared) == 5 && DataAt(shared, 4).integrity == Failed
                && DataAt(shared, 4).error == ERROR_OPERATION_ABORTED && DataAt(shared, 4).dataLength == 0, "cancelled capture result");
            request = {};
            Check(ReadFileEx(target, received, 7, &request, Callback), "old session pending read");
            shared->enabled = 0; shared->session.Data1++; shared->enabled = 1;
            Check(WriteFile(peer, expected, 7, &count, nullptr), "new session peer write");
            AwaitCallback(4);
            Check(callbackError == 0 && callbackBytes == 7 && !memcmp(received, expected, 7), "late callback preserved");
            Check(DataCount(shared) == 6 && DataAt(shared, 5).api == ApiWrite, "old callback excluded from new session");
            request = {};
            Check(ReadFileEx(target, received, 7, &request, Callback), "close pending read");
            Check(CloseHandle(target), "close pending handle"); target = nullptr;
            AwaitCallback(5);
            Check(callbackError == ERROR_OPERATION_ABORTED && DataCount(shared) == 7
                && DataAt(shared, 6).error == ERROR_OPERATION_ABORTED, "close cancellation callback");
            shared->enabled = 0; CloseHandle(target); CloseHandle(peer);
            Stage("extended ports after cancelled close");
            target = OpenPort(L"\\\\.\\COM10", FILE_FLAG_OVERLAPPED); peer = OpenPort(L"\\\\.\\COM20");
            shared->count = shared->readIndex = shared->writeIndex = 0; shared->enabled = 1;
            request = {}; request.hEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            started = ReadFile(target, received, 7, &count, &request);
            Check(started || GetLastError() == ERROR_IO_PENDING, "extended result read accepted");
            Check(!GetOverlappedResultEx(target, &request, &count, 0, FALSE) && GetLastError() == ERROR_IO_INCOMPLETE, "incomplete query");
            Check(!GetOverlappedResultEx(target, &request, &count, 20, FALSE) && GetLastError() == WAIT_TIMEOUT, "query timeout is not I/O completion");
            Check(DataCount(shared) == 0, "incomplete queries not recorded as completion");
            Check(WriteFile(peer, expected, 7, &count, nullptr), "extended result peer write");
            Check(GetOverlappedResultEx(target, &request, &count, 2000, FALSE) && count == 7, "extended completion");
            Check(GetOverlappedResultEx(target, &request, &count, 0, FALSE), "extended repeat query");
            Check(DataCount(shared) == 2 && DataAt(shared, 1).completed == 7 && !memcmp(DataAt(shared, 1).data, expected, 7), "extended completion capture");
            CloseHandle(request.hEvent);
            completionPort = CreateIoCompletionPort(target, nullptr, 99, 0);
            request = {}; started = ReadFile(target, received, 7, &count, &request);
            Check(started || GetLastError() == ERROR_IO_PENDING, "batch IOCP read accepted");
            Check(WriteFile(peer, expected, 7, &count, nullptr), "batch IOCP peer write");
            OVERLAPPED_ENTRY entries[4]{}; ULONG removed = 0;
            Check(GetQueuedCompletionStatusEx(completionPort, entries, 4, &removed, 3000, FALSE)
                && removed == 1 && entries[0].lpOverlapped == &request && entries[0].dwNumberOfBytesTransferred == 7, "batch IOCP business completion");
            Check(DataCount(shared) == 4 && DataAt(shared, 3).completed == 7 && !memcmp(DataAt(shared, 3).data, expected, 7), "batch IOCP capture");
            shared->enabled = 0; CloseHandle(target); CloseHandle(peer); CloseHandle(completionPort);
        }
        shared->enabled = 0;
        CloseHandle(owner); CloseHandle(mutex); UnmapViewOfFile(shared); CloseHandle(mapping);
        std::cout << "PASS: production initialization, NUL exclusion, business result/last-error preservation\n";
        std::ofstream(OutputPath(), std::ios::app) << "PASS architecture=" << sizeof(void*) * 8 << " serial=" << serial
            << " initialization/exclusion" << (serial ? ", sync, event/IOCP/deduplication, callback/cancel/close/session, extended result/batch IOCP" : "") << '\n';
        return 0;
    } catch (const std::exception& error) {
        std::cerr << "FAIL: " << error.what() << '\n';
        std::ofstream(OutputPath(), std::ios::app) << "FAIL: " << error.what() << "; win32=" << GetLastError() << '\n';
        return 1;
    }
}
static void EmitWireFixture() {
    // Synthetic producer at the IPC boundary: verifies the C++ ABI against the real managed reader, not serial I/O.
    auto name = MappingName(GetCurrentProcessId(), ProcessCreationTime(GetCurrentProcess()));
    HANDLE mapping = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, name.c_str());
    auto queue = mapping ? static_cast<HookShared*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(HookShared))) : nullptr;
    Check(queue != nullptr, "wire fixture mapping");
    HANDLE mutex = OpenMutexW(SYNCHRONIZE | MUTEX_MODIFY_STATE, FALSE, (name + L".lock").c_str());
    DWORD acquired = WaitForSingleObject(mutex, 1000);
    Check(acquired == WAIT_OBJECT_0 || acquired == WAIT_ABANDONED, "wire fixture lock");
    HookRecord record{}; FILETIME now{}; GetSystemTimePreciseAsFileTime(&now);
    record.session = queue->session; record.sequence = 1; record.handle = 0x12345678; record.generation = 4; record.request = 1;
    record.time = (static_cast<uint64_t>(now.dwHighDateTime) << 32) | now.dwLowDateTime;
    record.api = ApiRead; record.direction = 1; record.requested = 16; record.completed = record.dataLength = 3; record.succeeded = 1;
    wcscpy_s(record.port, L"ABI fixture"); wcscpy_s(record.detail, L"synthetic IPC record, no serial device");
    record.data[0] = 0; record.data[1] = 0x7f; record.data[2] = 0xff;
    queue->records[0] = record;
    record.sequence = record.request = 2; record.dropped = 2; record.completed = record.dataLength = 1; record.data[0] = 0x11;
    queue->records[1] = record;
    queue->readIndex = 0; queue->writeIndex = queue->count = 2; queue->sequence = 2; queue->dropped = 2;
    ReleaseMutex(mutex); CloseHandle(mutex); UnmapViewOfFile(queue); CloseHandle(mapping);
}
int wmain(int argc, wchar_t** argv) {
    if (argc > 1 && std::wstring(argv[1]) == L"--launch-worker") { Sleep(3000); return 0; }
    if (argc > 1 && std::wstring(argv[1]) == L"--host-worker") {
        std::cout << "READY" << std::endl;
        std::string line;
        while (std::getline(std::cin, line) && line != "EXIT") {
            if (line != "EMIT") continue;
            try { EmitWireFixture(); std::cout << "EMITTED" << std::endl; }
            catch (const std::exception& error) { std::cerr << error.what() << std::endl; return 1; }
        }
        return 0;
    }
    return Run(argc > 1);
}
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR commandLine, int) {
    // The host integration suite uses the GUI subsystem worker without creating any window.
    if (std::wstring(commandLine) == L"--launch-worker") { Sleep(3000); return 0; }
    if (MessageBoxW(nullptr, L"Run production Hook tests on dedicated COM10 / COM20?", L"FlexCom Hook tests", MB_OKCANCEL) != IDOK) return 2;
    int result = Run(true);
    MessageBoxW(nullptr, result ? L"FAIL: native-results.txt" : L"PASS: native-results.txt", L"FlexCom Hook tests", MB_OK);
    return result;
}
