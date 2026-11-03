#pragma once
#include <windows.h>
#include <cstdint>
#include <string>

// Versioned little-endian ABI shared with NativeProcessMonitorTransport. No pointers cross it.
constexpr uint32_t HookVersion = 4, QueueCapacity = 256, PayloadCapacity = 1024;
#pragma pack(push, 8)
// Epoch and count change atomically, so a delayed old-session increment cannot alter new counters.
struct alignas(8) SessionCounter {
    mutable volatile LONG64 value;
    uint32_t Epoch() const { return static_cast<uint32_t>(static_cast<uint64_t>(InterlockedCompareExchange64(&value, 0, 0)) >> 32); }
    operator uint32_t() const { return static_cast<uint32_t>(InterlockedCompareExchange64(&value, 0, 0)); }
    SessionCounter& operator=(uint32_t count) {
        InterlockedExchange64(&value, static_cast<LONG64>((static_cast<uint64_t>(Epoch()) << 32) | count)); return *this;
    }
    bool Add(uint32_t epoch) {
        LONG64 previous = InterlockedCompareExchange64(&value, 0, 0);
        for (;;) {
            if (static_cast<uint32_t>(static_cast<uint64_t>(previous) >> 32) != epoch) return false;
            auto next = static_cast<LONG64>((static_cast<uint64_t>(epoch) << 32) | (static_cast<uint32_t>(previous) + 1u));
            auto observed = InterlockedCompareExchange64(&value, next, previous);
            if (observed == previous) return true;
            previous = observed;
        }
    }
};
struct HookRecord {
    GUID session;
    uint64_t sequence, handle, generation, request, time;
    uint32_t api, direction, requested, completed, succeeded, error, integrity, dataLength, attemptLength;
    wchar_t port[32];
    wchar_t detail[128];
    BYTE data[PayloadCapacity], attempt[PayloadCapacity];
    uint32_t dropped;
};
struct HookDiagnostics {
    SessionCounter snapshotContention, publishContention, completionContention, queueFull;
    SessionCounter handleCapacity, pendingCapacity, ambiguousCompletion, retirementCapacity;
    uint32_t highWater, reserved;
    SessionCounter notificationLoss;
    uint64_t publishCalls, publishTicks, maxPublishTicks;
};
struct HookShared {
    uint32_t version, recordSize, capacity, enabled;
    GUID session;
    uint32_t readIndex, writeIndex, count, initialized;
    SessionCounter dropped;
    uint32_t startup;
    uint64_t sequence;
    HookDiagnostics diagnostics;
    HookRecord records[QueueCapacity];
};
#pragma pack(pop)
static_assert(sizeof(HookRecord) == 2464);
static_assert(offsetof(HookRecord, time) == 48 && offsetof(HookRecord, api) == 56);
static_assert(offsetof(HookRecord, port) == 92 && offsetof(HookRecord, detail) == 156);
static_assert(offsetof(HookRecord, data) == 412 && offsetof(HookRecord, attempt) == 1436);
static_assert(offsetof(HookRecord, dropped) == 2460);
static_assert(sizeof(SessionCounter) == 8 && sizeof(HookDiagnostics) == 104);
static_assert(offsetof(HookShared, startup) == 56 && offsetof(HookShared, sequence) == 64);
static_assert(offsetof(HookShared, diagnostics) == 72 && offsetof(HookShared, records) == 176);
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
