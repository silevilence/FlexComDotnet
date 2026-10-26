#pragma once
#include <windows.h>
#include <cstdint>

// Laboratory-only ABI. The production transport must be versioned separately.
constexpr DWORD ProbeVersion = 1;
constexpr DWORD RecordCapacity = 64;
constexpr DWORD PayloadCapacity = 64;
struct ProbeRecord {
    ULONGLONG handle;
    ULONGLONG completionTime;
    DWORD direction; // 0 = write, 1 = read
    DWORD requested;
    DWORD completed;
    DWORD error;
    BOOL succeeded;
    BYTE data[PayloadCapacity];
};
struct ProbeShared {
    DWORD version;
    volatile LONG count;
    volatile LONG dropped;
    volatile LONG attached;
    volatile LONG workerResult;
    ULONGLONG targetHandle;
    ProbeRecord records[RecordCapacity];
};
