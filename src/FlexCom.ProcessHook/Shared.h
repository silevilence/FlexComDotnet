#pragma once
#include <windows.h>
#include <cstdint>
#include <string>

// Versioned little-endian ABI shared with NativeProcessMonitorTransport. No pointers cross it.
constexpr uint32_t HookVersion = 2, QueueCapacity = 256, PayloadCapacity = 1024;
#pragma pack(push, 8)
struct HookRecord {
    GUID session;
    uint64_t sequence, handle, generation, request, time;
    uint32_t api, direction, requested, completed, succeeded, error, integrity, dataLength, attemptLength;
    wchar_t port[32];
    wchar_t detail[128];
    BYTE data[PayloadCapacity], attempt[PayloadCapacity];
    uint32_t dropped;
};
struct HookShared {
    uint32_t version, recordSize, capacity, enabled;
    GUID session;
    uint32_t readIndex, writeIndex, count, initialized;
    volatile LONG dropped;
    uint32_t startup;
    uint64_t sequence;
    HookRecord records[QueueCapacity];
};
#pragma pack(pop)
static_assert(sizeof(HookRecord) == 2464);
static_assert(offsetof(HookRecord, time) == 48 && offsetof(HookRecord, api) == 56);
static_assert(offsetof(HookRecord, port) == 92 && offsetof(HookRecord, detail) == 156);
static_assert(offsetof(HookRecord, data) == 412 && offsetof(HookRecord, attempt) == 1436);
static_assert(offsetof(HookRecord, dropped) == 2460);
static_assert(offsetof(HookShared, records) == 64);
enum Api : uint32_t { ApiOpen = 1, ApiClose, ApiRead, ApiWrite, ApiReadEx, ApiWriteEx,
    ApiSetState, ApiGetState, ApiSetTimeouts, ApiGetTimeouts, ApiEscape, ApiPurge, ApiCancel, ApiGap };
enum Integrity : uint32_t { Complete, Gap, Truncated, Failed, Disconnected, Unavailable };
inline uint64_t ProcessCreationTime(HANDLE process) {
    FILETIME created{}, exited{}, kernel{}, user{};
    if (!GetProcessTimes(process, &created, &exited, &kernel, &user)) return 0;
    return (static_cast<uint64_t>(created.dwHighDateTime) << 32) | created.dwLowDateTime;
}
inline std::wstring MappingName(DWORD pid, uint64_t created) {
    return L"Local\\FlexCom.ProcessHook.v1." + std::to_wstring(pid) + L"." + std::to_wstring(created);
}
