//! Shared data structures between kernel driver (Rust) and user-mode application (C#).
//!
//! All structures must be `#[repr(C)]` and use fixed-width integer types
//! to ensure memory layout consistency with C# `[StructLayout(LayoutKind.Sequential)]`.
//!
//! # Cross-Language Sync
//! When adding/removing fields, **MUST** update the corresponding C# P/Invoke definitions.

/// Maximum length of a serial port device name (e.g., `\Device\Serial0`).
pub const MAX_PORT_NAME_LEN: usize = 64;

/// Maximum data payload size for a single captured data entry.
pub const MAX_DATA_SIZE: usize = 4096;

/// Data direction indicator for captured serial data.
#[repr(u8)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DataDirection {
    /// Data transmitted from host to device (TX).
    Tx = 0,
    /// Data received from device to host (RX).
    Rx = 1,
}

impl DataDirection {
    /// Creates a `DataDirection` from a raw `u8` value.
    ///
    /// Returns `None` if the value is not a valid direction.
    pub fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(DataDirection::Tx),
            1 => Some(DataDirection::Rx),
            _ => None,
        }
    }
}

/// Monitoring state of the driver.
#[repr(u8)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MonitorState {
    /// Monitoring is stopped.
    Stopped = 0,
    /// Monitoring is active.
    Running = 1,
}

impl MonitorState {
    /// Creates a `MonitorState` from a raw `u8` value.
    pub fn from_u8(value: u8) -> Option<Self> {
        match value {
            0 => Some(MonitorState::Stopped),
            1 => Some(MonitorState::Running),
            _ => None,
        }
    }
}

/// Request structure for starting monitoring on a specific serial port.
///
/// Sent from user-mode to driver via `IOCTL_START_MONITOR`.
///
/// # C# Equivalent
/// ```csharp
/// [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
/// struct StartMonitorRequest {
///     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
///     public char[] PortName;
/// }
/// ```
#[repr(C)]
#[derive(Clone)]
pub struct StartMonitorRequest {
    /// Null-terminated UTF-16 port name (e.g., "COM3\0" in UTF-16).
    /// Padded with zeros to fill `MAX_PORT_NAME_LEN` u16 slots.
    pub port_name: [u16; MAX_PORT_NAME_LEN],
}

impl StartMonitorRequest {
    /// Creates a new `StartMonitorRequest` with all zeros.
    pub fn new() -> Self {
        Self {
            port_name: [0u16; MAX_PORT_NAME_LEN],
        }
    }

    /// Creates a `StartMonitorRequest` from a port name string.
    ///
    /// Returns `None` if the name is too long (exceeds `MAX_PORT_NAME_LEN - 1`
    /// characters, reserving one slot for the null terminator).
    pub fn from_port_name(name: &str) -> Option<Self> {
        let utf16_chars: Vec<u16> = name.encode_utf16().collect();
        // Reserve one slot for null terminator
        if utf16_chars.len() >= MAX_PORT_NAME_LEN {
            return None;
        }
        let mut request = Self::new();
        request.port_name[..utf16_chars.len()].copy_from_slice(&utf16_chars);
        // Remaining slots are already zero (null terminator)
        Some(request)
    }

    /// Extracts the port name as a Rust `String`.
    ///
    /// Reads until the first null terminator (0x0000) in the UTF-16 array.
    pub fn port_name_string(&self) -> String {
        let len = self.port_name.iter().position(|&c| c == 0).unwrap_or(MAX_PORT_NAME_LEN);
        String::from_utf16_lossy(&self.port_name[..len])
    }
}

impl Default for StartMonitorRequest {
    fn default() -> Self {
        Self::new()
    }
}

impl core::fmt::Debug for StartMonitorRequest {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        f.debug_struct("StartMonitorRequest")
            .field("port_name", &self.port_name_string())
            .finish()
    }
}

/// Header for a captured data entry in the ring buffer.
///
/// Each captured data block consists of a `CapturedDataHeader` followed
/// by `data_length` bytes of actual data payload.
///
/// # C# Equivalent
/// ```csharp
/// [StructLayout(LayoutKind.Sequential)]
/// struct CapturedDataHeader {
///     public ulong Timestamp;
///     public byte Direction;
///     public byte Reserved1;
///     public ushort Reserved2;
///     public uint DataLength;
/// }
/// ```
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct CapturedDataHeader {
    /// Timestamp in 100-nanosecond intervals since system boot
    /// (matching Windows `KeQuerySystemTime` format).
    pub timestamp: u64,
    /// Data direction: 0 = TX, 1 = RX.
    pub direction: u8,
    /// Reserved for alignment, must be 0.
    pub reserved1: u8,
    /// Reserved for alignment, must be 0.
    pub reserved2: u16,
    /// Length of the data payload following this header, in bytes.
    pub data_length: u32,
}

impl CapturedDataHeader {
    /// Size of the header in bytes.
    pub const SIZE: usize = core::mem::size_of::<Self>();

    /// Creates a new `CapturedDataHeader`.
    pub fn new(timestamp: u64, direction: DataDirection, data_length: u32) -> Self {
        Self {
            timestamp,
            direction: direction as u8,
            reserved1: 0,
            reserved2: 0,
            data_length,
        }
    }

    /// Returns the `DataDirection` parsed from the raw direction byte.
    pub fn data_direction(&self) -> Option<DataDirection> {
        DataDirection::from_u8(self.direction)
    }

    /// Returns the total size of this entry (header + payload) in the buffer.
    pub fn total_entry_size(&self) -> usize {
        Self::SIZE + self.data_length as usize
    }

    /// Serializes the header to a byte array.
    ///
    /// # Safety
    /// The struct is `#[repr(C)]` with fixed-width fields, so this is safe.
    pub fn to_bytes(&self) -> [u8; CapturedDataHeader::SIZE] {
        let mut bytes = [0u8; Self::SIZE];
        bytes[0..8].copy_from_slice(&self.timestamp.to_le_bytes());
        bytes[8] = self.direction;
        bytes[9] = self.reserved1;
        bytes[10..12].copy_from_slice(&self.reserved2.to_le_bytes());
        bytes[12..16].copy_from_slice(&self.data_length.to_le_bytes());
        bytes
    }

    /// Deserializes a header from a byte slice.
    ///
    /// Returns `None` if the slice is too short.
    pub fn from_bytes(bytes: &[u8]) -> Option<Self> {
        if bytes.len() < Self::SIZE {
            return None;
        }
        Some(Self {
            timestamp: u64::from_le_bytes(bytes[0..8].try_into().ok()?),
            direction: bytes[8],
            reserved1: bytes[9],
            reserved2: u16::from_le_bytes(bytes[10..12].try_into().ok()?),
            data_length: u32::from_le_bytes(bytes[12..16].try_into().ok()?),
        })
    }
}

/// Response structure for `IOCTL_GET_DATA`.
///
/// Contains a header describing the data followed by the raw payload.
///
/// # C# Equivalent
/// ```csharp
/// [StructLayout(LayoutKind.Sequential)]
/// struct GetDataResponse {
///     public CapturedDataHeader Header;
///     [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
///     public byte[] Data;
/// }
/// ```
#[repr(C)]
pub struct GetDataResponse {
    /// Header describing the captured data.
    pub header: CapturedDataHeader,
    /// Raw data payload. Only the first `header.data_length` bytes are valid.
    pub data: [u8; MAX_DATA_SIZE],
}

impl GetDataResponse {
    /// Creates a new empty `GetDataResponse`.
    pub fn new() -> Self {
        Self {
            header: CapturedDataHeader::new(0, DataDirection::Tx, 0),
            data: [0u8; MAX_DATA_SIZE],
        }
    }

    /// Creates a `GetDataResponse` from header and data slice.
    ///
    /// Returns `None` if data exceeds `MAX_DATA_SIZE`.
    pub fn from_data(header: CapturedDataHeader, data: &[u8]) -> Option<Self> {
        if data.len() > MAX_DATA_SIZE {
            return None;
        }
        let mut response = Self::new();
        response.header = header;
        response.data[..data.len()].copy_from_slice(data);
        Some(response)
    }

    /// Returns only the valid data portion as a slice.
    pub fn valid_data(&self) -> &[u8] {
        let len = (self.header.data_length as usize).min(MAX_DATA_SIZE);
        &self.data[..len]
    }
}

impl Default for GetDataResponse {
    fn default() -> Self {
        Self::new()
    }
}

impl core::fmt::Debug for GetDataResponse {
    fn fmt(&self, f: &mut core::fmt::Formatter<'_>) -> core::fmt::Result {
        f.debug_struct("GetDataResponse")
            .field("header", &self.header)
            .field("data_len", &self.valid_data().len())
            .finish()
    }
}

/// Driver status information returned to user-mode.
///
/// # C# Equivalent
/// ```csharp
/// [StructLayout(LayoutKind.Sequential)]
/// struct DriverStatus {
///     public byte State;
///     public byte Reserved1;
///     public ushort Reserved2;
///     public uint CapturedEntryCount;
///     public ulong TotalBytesRx;
///     public ulong TotalBytesTx;
/// }
/// ```
#[repr(C)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct DriverStatus {
    /// Current monitoring state (0 = Stopped, 1 = Running).
    pub state: u8,
    /// Reserved for alignment.
    pub reserved1: u8,
    /// Reserved for alignment.
    pub reserved2: u16,
    /// Number of captured data entries currently in the buffer.
    pub captured_entry_count: u32,
    /// Total bytes received (RX) since monitoring started.
    pub total_bytes_rx: u64,
    /// Total bytes transmitted (TX) since monitoring started.
    pub total_bytes_tx: u64,
}

impl DriverStatus {
    /// Creates a new `DriverStatus` with all fields zeroed.
    pub fn new() -> Self {
        Self {
            state: MonitorState::Stopped as u8,
            reserved1: 0,
            reserved2: 0,
            captured_entry_count: 0,
            total_bytes_rx: 0,
            total_bytes_tx: 0,
        }
    }

    /// Returns the parsed `MonitorState`.
    pub fn monitor_state(&self) -> Option<MonitorState> {
        MonitorState::from_u8(self.state)
    }
}

impl Default for DriverStatus {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_data_direction_from_u8() {
        assert_eq!(DataDirection::from_u8(0), Some(DataDirection::Tx));
        assert_eq!(DataDirection::from_u8(1), Some(DataDirection::Rx));
        assert_eq!(DataDirection::from_u8(2), None);
        assert_eq!(DataDirection::from_u8(255), None);
    }

    #[test]
    fn test_monitor_state_from_u8() {
        assert_eq!(MonitorState::from_u8(0), Some(MonitorState::Stopped));
        assert_eq!(MonitorState::from_u8(1), Some(MonitorState::Running));
        assert_eq!(MonitorState::from_u8(2), None);
    }

    #[test]
    fn test_start_monitor_request_from_port_name() {
        let req = StartMonitorRequest::from_port_name("COM3").unwrap();
        assert_eq!(req.port_name_string(), "COM3");
    }

    #[test]
    fn test_start_monitor_request_empty_name() {
        let req = StartMonitorRequest::from_port_name("").unwrap();
        assert_eq!(req.port_name_string(), "");
    }

    #[test]
    fn test_start_monitor_request_max_length() {
        // MAX_PORT_NAME_LEN - 1 chars should succeed (last slot for null)
        let name: String = "A".repeat(MAX_PORT_NAME_LEN - 1);
        let req = StartMonitorRequest::from_port_name(&name).unwrap();
        assert_eq!(req.port_name_string(), name);
    }

    #[test]
    fn test_start_monitor_request_too_long() {
        // Exactly MAX_PORT_NAME_LEN chars should fail (no room for null)
        let name: String = "A".repeat(MAX_PORT_NAME_LEN);
        assert!(StartMonitorRequest::from_port_name(&name).is_none());
    }

    #[test]
    fn test_start_monitor_request_unicode() {
        let req = StartMonitorRequest::from_port_name("COM端口1").unwrap();
        assert_eq!(req.port_name_string(), "COM端口1");
    }

    #[test]
    fn test_start_monitor_request_default() {
        let req = StartMonitorRequest::default();
        assert_eq!(req.port_name_string(), "");
    }

    #[test]
    fn test_captured_data_header_new() {
        let header = CapturedDataHeader::new(12345, DataDirection::Rx, 100);
        assert_eq!(header.timestamp, 12345);
        assert_eq!(header.direction, 1);
        assert_eq!(header.data_length, 100);
        assert_eq!(header.data_direction(), Some(DataDirection::Rx));
    }

    #[test]
    fn test_captured_data_header_total_entry_size() {
        let header = CapturedDataHeader::new(0, DataDirection::Tx, 256);
        assert_eq!(header.total_entry_size(), CapturedDataHeader::SIZE + 256);
    }

    #[test]
    fn test_captured_data_header_serialization_roundtrip() {
        let original = CapturedDataHeader::new(0xDEAD_BEEF_CAFE_BABE, DataDirection::Rx, 42);
        let bytes = original.to_bytes();
        let restored = CapturedDataHeader::from_bytes(&bytes).unwrap();
        assert_eq!(original, restored);
    }

    #[test]
    fn test_captured_data_header_from_bytes_too_short() {
        let bytes = [0u8; 5];
        assert!(CapturedDataHeader::from_bytes(&bytes).is_none());
    }

    #[test]
    fn test_captured_data_header_from_bytes_empty() {
        assert!(CapturedDataHeader::from_bytes(&[]).is_none());
    }

    #[test]
    fn test_captured_data_header_size() {
        // timestamp(8) + direction(1) + reserved1(1) + reserved2(2) + data_length(4) = 16
        assert_eq!(CapturedDataHeader::SIZE, 16);
    }

    #[test]
    fn test_get_data_response_from_data() {
        let header = CapturedDataHeader::new(100, DataDirection::Tx, 5);
        let data = b"hello";
        let response = GetDataResponse::from_data(header, data).unwrap();
        assert_eq!(response.valid_data(), b"hello");
        assert_eq!(response.header.data_length, 5);
    }

    #[test]
    fn test_get_data_response_from_data_too_large() {
        let header = CapturedDataHeader::new(100, DataDirection::Tx, (MAX_DATA_SIZE + 1) as u32);
        let data = vec![0u8; MAX_DATA_SIZE + 1];
        assert!(GetDataResponse::from_data(header, &data).is_none());
    }

    #[test]
    fn test_get_data_response_valid_data_capped() {
        // If header says data_length > MAX_DATA_SIZE, valid_data caps it
        let mut response = GetDataResponse::new();
        response.header.data_length = (MAX_DATA_SIZE + 100) as u32;
        assert_eq!(response.valid_data().len(), MAX_DATA_SIZE);
    }

    #[test]
    fn test_get_data_response_default() {
        let response = GetDataResponse::default();
        assert_eq!(response.valid_data().len(), 0);
    }

    #[test]
    fn test_driver_status_default() {
        let status = DriverStatus::default();
        assert_eq!(status.monitor_state(), Some(MonitorState::Stopped));
        assert_eq!(status.captured_entry_count, 0);
        assert_eq!(status.total_bytes_rx, 0);
        assert_eq!(status.total_bytes_tx, 0);
    }

    #[test]
    fn test_driver_status_running() {
        let mut status = DriverStatus::new();
        status.state = MonitorState::Running as u8;
        status.captured_entry_count = 42;
        status.total_bytes_rx = 1024;
        status.total_bytes_tx = 512;
        assert_eq!(status.monitor_state(), Some(MonitorState::Running));
        assert_eq!(status.captured_entry_count, 42);
    }

    #[test]
    fn test_struct_alignment_captured_data_header() {
        // Verify the struct has expected size and alignment for C interop
        assert_eq!(core::mem::size_of::<CapturedDataHeader>(), 16);
        assert_eq!(core::mem::align_of::<CapturedDataHeader>(), 8);
    }

    #[test]
    fn test_struct_alignment_driver_status() {
        // state(1) + reserved1(1) + reserved2(2) + captured_entry_count(4)
        // + total_bytes_rx(8) + total_bytes_tx(8) = 24
        assert_eq!(core::mem::size_of::<DriverStatus>(), 24);
        assert_eq!(core::mem::align_of::<DriverStatus>(), 8);
    }

    #[test]
    fn test_struct_alignment_start_monitor_request() {
        // 64 * u16 = 128 bytes
        assert_eq!(
            core::mem::size_of::<StartMonitorRequest>(),
            MAX_PORT_NAME_LEN * 2
        );
    }
}
