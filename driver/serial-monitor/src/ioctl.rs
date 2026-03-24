//! IOCTL code definitions for serial monitor driver communication.
//!
//! Windows IOCTL codes encode device type, function number, access mode, and
//! data transfer method into a single 32-bit value using the `CTL_CODE` macro.
//!
//! # C# Equivalent
//! These constants must match the values defined in the C# P/Invoke layer.
//! When modifying IOCTL codes, **MUST** synchronize both sides.

/// Device type for the serial monitor driver.
/// Uses a value in the user-defined range (0x8000-0xFFFF).
pub const FILE_DEVICE_SERIAL_MONITOR: u32 = 0x8000;

/// Transfer method: Input/output buffers are copied into system space (safest).
pub const METHOD_BUFFERED: u32 = 0;

/// Access flags.
pub const FILE_ANY_ACCESS: u32 = 0;
pub const FILE_READ_ACCESS: u32 = 1;
pub const FILE_WRITE_ACCESS: u32 = 2;
pub const FILE_READ_WRITE_ACCESS: u32 = FILE_READ_ACCESS | FILE_WRITE_ACCESS;

/// Constructs a Windows IOCTL control code following the `CTL_CODE` macro layout.
///
/// # Layout (32-bit)
/// ```text
/// [31..16] Device Type (16 bits)
/// [15..14] Required Access (2 bits)
/// [13..2]  Function Code (12 bits)
/// [1..0]   Transfer Method (2 bits)
/// ```
///
/// # Parameters
/// - `device_type`: Device type identifier (e.g., `FILE_DEVICE_SERIAL_MONITOR`)
/// - `function`: Function code (0x800-0xFFF for user-defined)
/// - `method`: Transfer method (METHOD_BUFFERED, etc.)
/// - `access`: Required access flags
pub const fn ctl_code(device_type: u32, function: u32, method: u32, access: u32) -> u32 {
    (device_type << 16) | (access << 14) | (function << 2) | method
}

// Function codes for our IOCTL operations.
// Uses 0x800+ range for user-defined functions.

/// Function code for starting serial port monitoring.
const FUNCTION_START_MONITOR: u32 = 0x800;

/// Function code for stopping serial port monitoring.
const FUNCTION_STOP_MONITOR: u32 = 0x801;

/// Function code for retrieving captured data from the driver buffer.
const FUNCTION_GET_DATA: u32 = 0x802;

/// Function code for querying driver status.
const FUNCTION_GET_STATUS: u32 = 0x803;

/// IOCTL to start monitoring a specific serial port.
///
/// - **Input**: `StartMonitorRequest` containing the target port name.
/// - **Output**: None.
/// - **Access**: Read/Write (opens device for monitoring).
pub const IOCTL_START_MONITOR: u32 = ctl_code(
    FILE_DEVICE_SERIAL_MONITOR,
    FUNCTION_START_MONITOR,
    METHOD_BUFFERED,
    FILE_READ_WRITE_ACCESS,
);

/// IOCTL to stop monitoring the currently monitored serial port.
///
/// - **Input**: None.
/// - **Output**: None.
/// - **Access**: Read/Write.
pub const IOCTL_STOP_MONITOR: u32 = ctl_code(
    FILE_DEVICE_SERIAL_MONITOR,
    FUNCTION_STOP_MONITOR,
    METHOD_BUFFERED,
    FILE_READ_WRITE_ACCESS,
);

/// IOCTL to read captured data from the driver's ring buffer.
///
/// - **Input**: None.
/// - **Output**: `GetDataResponse` containing one captured data entry.
/// - **Access**: Read.
///
/// Returns one entry at a time. Caller should loop until no more data is available.
pub const IOCTL_GET_DATA: u32 = ctl_code(
    FILE_DEVICE_SERIAL_MONITOR,
    FUNCTION_GET_DATA,
    METHOD_BUFFERED,
    FILE_READ_ACCESS,
);

/// IOCTL to query the current driver status.
///
/// - **Input**: None.
/// - **Output**: `DriverStatus` containing state and statistics.
/// - **Access**: Read.
pub const IOCTL_GET_STATUS: u32 = ctl_code(
    FILE_DEVICE_SERIAL_MONITOR,
    FUNCTION_GET_STATUS,
    METHOD_BUFFERED,
    FILE_READ_ACCESS,
);

/// Device name for the control device object in kernel space.
pub const DEVICE_NAME: &str = "\\Device\\SerialMonitor";

/// Symbolic link name exposed to user-mode applications.
/// User-mode code accesses the driver via `\\.\SerialMonitor`.
pub const SYMLINK_NAME: &str = "\\DosDevices\\SerialMonitor";

/// User-mode path to open the driver.
/// Used with `CreateFile` in Win32 API.
pub const USER_MODE_PATH: &str = "\\\\.\\SerialMonitor";

/// Validates that an IOCTL code belongs to our driver.
pub const fn is_serial_monitor_ioctl(code: u32) -> bool {
    let device_type = (code >> 16) & 0xFFFF;
    device_type == FILE_DEVICE_SERIAL_MONITOR
}

/// Extracts the function code from an IOCTL.
pub const fn ioctl_function(code: u32) -> u32 {
    (code >> 2) & 0xFFF
}

/// Extracts the transfer method from an IOCTL.
pub const fn ioctl_method(code: u32) -> u32 {
    code & 0x3
}

/// Extracts the access flags from an IOCTL.
pub const fn ioctl_access(code: u32) -> u32 {
    (code >> 14) & 0x3
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_ctl_code_layout() {
        // Verify the CTL_CODE macro produces expected bit layout
        let code = ctl_code(0x8000, 0x800, METHOD_BUFFERED, FILE_READ_WRITE_ACCESS);

        // Device type should be in bits [31..16]
        assert_eq!((code >> 16) & 0xFFFF, 0x8000);
        // Access should be in bits [15..14]
        assert_eq!((code >> 14) & 0x3, FILE_READ_WRITE_ACCESS);
        // Function should be in bits [13..2]
        assert_eq!((code >> 2) & 0xFFF, 0x800);
        // Method should be in bits [1..0]
        assert_eq!(code & 0x3, METHOD_BUFFERED);
    }

    #[test]
    fn test_ioctl_start_monitor_code() {
        assert!(is_serial_monitor_ioctl(IOCTL_START_MONITOR));
        assert_eq!(ioctl_function(IOCTL_START_MONITOR), FUNCTION_START_MONITOR);
        assert_eq!(ioctl_method(IOCTL_START_MONITOR), METHOD_BUFFERED);
        assert_eq!(ioctl_access(IOCTL_START_MONITOR), FILE_READ_WRITE_ACCESS);
    }

    #[test]
    fn test_ioctl_stop_monitor_code() {
        assert!(is_serial_monitor_ioctl(IOCTL_STOP_MONITOR));
        assert_eq!(ioctl_function(IOCTL_STOP_MONITOR), FUNCTION_STOP_MONITOR);
        assert_eq!(ioctl_method(IOCTL_STOP_MONITOR), METHOD_BUFFERED);
        assert_eq!(ioctl_access(IOCTL_STOP_MONITOR), FILE_READ_WRITE_ACCESS);
    }

    #[test]
    fn test_ioctl_get_data_code() {
        assert!(is_serial_monitor_ioctl(IOCTL_GET_DATA));
        assert_eq!(ioctl_function(IOCTL_GET_DATA), FUNCTION_GET_DATA);
        assert_eq!(ioctl_method(IOCTL_GET_DATA), METHOD_BUFFERED);
        assert_eq!(ioctl_access(IOCTL_GET_DATA), FILE_READ_ACCESS);
    }

    #[test]
    fn test_ioctl_get_status_code() {
        assert!(is_serial_monitor_ioctl(IOCTL_GET_STATUS));
        assert_eq!(ioctl_function(IOCTL_GET_STATUS), FUNCTION_GET_STATUS);
        assert_eq!(ioctl_method(IOCTL_GET_STATUS), METHOD_BUFFERED);
        assert_eq!(ioctl_access(IOCTL_GET_STATUS), FILE_READ_ACCESS);
    }

    #[test]
    fn test_ioctl_codes_unique() {
        // All IOCTL codes must be distinct
        let codes = [
            IOCTL_START_MONITOR,
            IOCTL_STOP_MONITOR,
            IOCTL_GET_DATA,
            IOCTL_GET_STATUS,
        ];
        for i in 0..codes.len() {
            for j in (i + 1)..codes.len() {
                assert_ne!(codes[i], codes[j], "IOCTL codes at index {} and {} are equal", i, j);
            }
        }
    }

    #[test]
    fn test_is_serial_monitor_ioctl_false() {
        // A code from a different device type
        let other_code = ctl_code(0x0001, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS);
        assert!(!is_serial_monitor_ioctl(other_code));
    }

    #[test]
    fn test_function_codes_in_user_range() {
        // User-defined function codes should be >= 0x800
        // Using const assertions via ioctl_function extraction
        assert_eq!(ioctl_function(IOCTL_START_MONITOR) & 0x800, 0x800);
        assert_eq!(ioctl_function(IOCTL_STOP_MONITOR) & 0x800, 0x800);
        assert_eq!(ioctl_function(IOCTL_GET_DATA) & 0x800, 0x800);
        assert_eq!(ioctl_function(IOCTL_GET_STATUS) & 0x800, 0x800);
    }

    #[test]
    fn test_ioctl_concrete_values_for_csharp_sync() {
        // These concrete values must match C# P/Invoke definitions.
        // If these tests break, both Rust and C# sides need updating.
        assert_eq!(IOCTL_START_MONITOR, 0x8000E000);
        assert_eq!(IOCTL_STOP_MONITOR, 0x8000E004);
        assert_eq!(IOCTL_GET_DATA, 0x80006008);
        assert_eq!(IOCTL_GET_STATUS, 0x8000600C);
    }

    #[test]
    fn test_device_names() {
        assert_eq!(DEVICE_NAME, "\\Device\\SerialMonitor");
        assert_eq!(SYMLINK_NAME, "\\DosDevices\\SerialMonitor");
        assert_eq!(USER_MODE_PATH, "\\\\.\\SerialMonitor");
    }

    #[test]
    fn test_ioctl_extract_helpers_roundtrip() {
        // For any valid CTL_CODE, the extraction helpers should reverse it
        let device_type: u32 = 0x8000;
        let function: u32 = 0xABC;
        let method: u32 = METHOD_BUFFERED;
        let access: u32 = FILE_READ_WRITE_ACCESS;

        let code = ctl_code(device_type, function, method, access);

        assert_eq!((code >> 16) & 0xFFFF, device_type);
        assert_eq!(ioctl_function(code), function);
        assert_eq!(ioctl_method(code), method);
        assert_eq!(ioctl_access(code), access);
    }
}
