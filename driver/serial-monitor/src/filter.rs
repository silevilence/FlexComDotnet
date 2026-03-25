//! Filter driver IRP interception logic for serial port monitoring.
//!
//! This module handles the core filtering logic:
//! - IRP_MJ_WRITE: Capture TX data before forwarding to lower driver
//! - IRP_MJ_READ: Set completion routine to capture RX data after lower driver processes
//! - IRP_MJ_DEVICE_CONTROL: Monitor serial port configuration changes
//!
//! # Design
//! The actual IRP handling uses trait abstractions (`IrpHandler`) so the
//! data capture and protocol logic can be thoroughly tested without kernel dependencies.

// In kernel (no_std) mode, Vec comes from alloc instead of std.
#[cfg(feature = "kernel")]
use alloc::vec::Vec;

use crate::ring_buffer::RingBuffer;
use crate::shared::DataDirection;

/// IRP Major Function codes (matching Windows kernel definitions).
#[repr(u8)]
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum IrpMajorFunction {
    Create = 0x00,
    Close = 0x02,
    Read = 0x03,
    Write = 0x04,
    DeviceControl = 0x0E,
    InternalDeviceControl = 0x0F,
    Cleanup = 0x12,
    /// Catch-all for unhandled function codes.
    Other = 0xFF,
}

impl IrpMajorFunction {
    /// Converts a raw major function code to the enum.
    pub fn from_u8(value: u8) -> Self {
        match value {
            0x00 => Self::Create,
            0x02 => Self::Close,
            0x03 => Self::Read,
            0x04 => Self::Write,
            0x0E => Self::DeviceControl,
            0x0F => Self::InternalDeviceControl,
            0x12 => Self::Cleanup,
            _ => Self::Other,
        }
    }
}

/// Serial port IOCTL codes from ntddser.h that we want to monitor.
pub mod serial_ioctl {
    /// IOCTL_SERIAL_SET_BAUD_RATE
    pub const SET_BAUD_RATE: u32 = 0x001B0004;
    /// IOCTL_SERIAL_SET_LINE_CONTROL
    pub const SET_LINE_CONTROL: u32 = 0x001B000C;
    /// IOCTL_SERIAL_SET_HANDFLOW
    pub const SET_HANDFLOW: u32 = 0x001B0060;
}

/// Represents a captured IRP event for processing.
#[derive(Debug, Clone)]
pub struct IrpEvent {
    /// The IRP major function type.
    pub major_function: IrpMajorFunction,
    /// Timestamp when the IRP was captured.
    pub timestamp: u64,
    /// Data payload (for read/write IRPs).
    pub data: Vec<u8>,
    /// IOCTL code (for DeviceControl IRPs).
    pub ioctl_code: Option<u32>,
}

/// Determines the action to take for a given IRP.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FilterAction {
    /// Pass IRP to lower driver without any interception.
    PassThrough,
    /// Capture data before passing to lower driver (for writes).
    CaptureAndForward,
    /// Set completion routine to capture after lower driver completes (for reads).
    ForwardWithCompletion,
    /// Handle internally (for our own device IOCTLs).
    HandleInternal,
}

/// Determines the appropriate filter action for an IRP based on its type.
///
/// This is the core dispatch decision that can be tested independently.
///
/// # Parameters
/// - `major_function`: The IRP major function code.
/// - `is_our_device`: Whether the IRP targets our control device (vs. filtered device).
pub fn determine_filter_action(
    major_function: IrpMajorFunction,
    is_our_device: bool,
) -> FilterAction {
    if is_our_device {
        // IRPs targeting our control device (\Device\SerialMonitor)
        return match major_function {
            IrpMajorFunction::Create | IrpMajorFunction::Close | IrpMajorFunction::Cleanup => {
                FilterAction::PassThrough
            }
            IrpMajorFunction::DeviceControl => FilterAction::HandleInternal,
            _ => FilterAction::PassThrough,
        };
    }

    // IRPs targeting the filtered serial port device
    match major_function {
        IrpMajorFunction::Write => FilterAction::CaptureAndForward,
        IrpMajorFunction::Read => FilterAction::ForwardWithCompletion,
        IrpMajorFunction::DeviceControl => FilterAction::CaptureAndForward,
        _ => FilterAction::PassThrough,
    }
}

/// Processes a write IRP by capturing the TX data into the ring buffer.
///
/// This function is called before the IRP is forwarded to the lower driver.
/// It copies the write buffer data without modifying or blocking the IRP.
///
/// # Parameters
/// - `buffer`: The ring buffer to store captured data.
/// - `timestamp`: Current system timestamp.
/// - `write_data`: The data being written (TX).
pub fn capture_write_data(buffer: &mut RingBuffer, timestamp: u64, write_data: &[u8]) {
    if write_data.is_empty() {
        return;
    }
    buffer.push(timestamp, DataDirection::Tx, write_data);
}

/// Processes a read completion by capturing the RX data into the ring buffer.
///
/// This function is called in the IRP completion routine after the lower
/// driver has filled the read buffer with received data.
///
/// # Safety Note
/// In kernel mode, this runs at DISPATCH_LEVEL in the completion routine.
/// No blocking operations allowed. The ring buffer push is designed to be
/// non-blocking.
///
/// # Parameters
/// - `buffer`: The ring buffer to store captured data.
/// - `timestamp`: Current system timestamp.
/// - `read_data`: The data that was read (RX).
/// - `bytes_read`: Actual number of bytes transferred by the lower driver.
pub fn capture_read_data(
    buffer: &mut RingBuffer,
    timestamp: u64,
    read_data: &[u8],
    bytes_read: usize,
) {
    if bytes_read == 0 || read_data.is_empty() {
        return;
    }
    // Only capture the actual bytes transferred, not the full buffer
    let actual_data = &read_data[..bytes_read.min(read_data.len())];
    buffer.push(timestamp, DataDirection::Rx, actual_data);
}

/// Checks if a serial IOCTL is one we want to monitor for configuration changes.
pub fn is_monitored_serial_ioctl(ioctl_code: u32) -> bool {
    matches!(
        ioctl_code,
        serial_ioctl::SET_BAUD_RATE | serial_ioctl::SET_LINE_CONTROL | serial_ioctl::SET_HANDFLOW
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::shared::CapturedDataHeader;

    // ---- IrpMajorFunction tests ----

    #[test]
    fn test_irp_major_from_known_values() {
        assert_eq!(IrpMajorFunction::from_u8(0x00), IrpMajorFunction::Create);
        assert_eq!(IrpMajorFunction::from_u8(0x02), IrpMajorFunction::Close);
        assert_eq!(IrpMajorFunction::from_u8(0x03), IrpMajorFunction::Read);
        assert_eq!(IrpMajorFunction::from_u8(0x04), IrpMajorFunction::Write);
        assert_eq!(
            IrpMajorFunction::from_u8(0x0E),
            IrpMajorFunction::DeviceControl
        );
        assert_eq!(
            IrpMajorFunction::from_u8(0x0F),
            IrpMajorFunction::InternalDeviceControl
        );
        assert_eq!(IrpMajorFunction::from_u8(0x12), IrpMajorFunction::Cleanup);
    }

    #[test]
    fn test_irp_major_unknown_values() {
        assert_eq!(IrpMajorFunction::from_u8(0x01), IrpMajorFunction::Other);
        assert_eq!(IrpMajorFunction::from_u8(0x50), IrpMajorFunction::Other);
        assert_eq!(IrpMajorFunction::from_u8(0xFF), IrpMajorFunction::Other);
    }

    // ---- FilterAction determination tests ----

    #[test]
    fn test_filter_action_our_device_create_close() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Create, true),
            FilterAction::PassThrough
        );
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Close, true),
            FilterAction::PassThrough
        );
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Cleanup, true),
            FilterAction::PassThrough
        );
    }

    #[test]
    fn test_filter_action_our_device_ioctl() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::DeviceControl, true),
            FilterAction::HandleInternal
        );
    }

    #[test]
    fn test_filter_action_our_device_other() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Read, true),
            FilterAction::PassThrough
        );
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Write, true),
            FilterAction::PassThrough
        );
    }

    #[test]
    fn test_filter_action_filtered_write() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Write, false),
            FilterAction::CaptureAndForward
        );
    }

    #[test]
    fn test_filter_action_filtered_read() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Read, false),
            FilterAction::ForwardWithCompletion
        );
    }

    #[test]
    fn test_filter_action_filtered_device_control() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::DeviceControl, false),
            FilterAction::CaptureAndForward
        );
    }

    #[test]
    fn test_filter_action_filtered_passthrough() {
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Create, false),
            FilterAction::PassThrough
        );
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Close, false),
            FilterAction::PassThrough
        );
        assert_eq!(
            determine_filter_action(IrpMajorFunction::Other, false),
            FilterAction::PassThrough
        );
    }

    // ---- Data capture tests ----

    #[test]
    fn test_capture_write_data() {
        let mut buffer = RingBuffer::new(1024);
        let data = b"AT+RESET\r\n";

        capture_write_data(&mut buffer, 1000, data);

        assert_eq!(buffer.entry_count(), 1);
        let (header, payload) = buffer.pop().unwrap();
        assert_eq!(header.timestamp, 1000);
        assert_eq!(header.data_direction(), Some(DataDirection::Tx));
        assert_eq!(payload, data.to_vec());
    }

    #[test]
    fn test_capture_write_empty_data() {
        let mut buffer = RingBuffer::new(1024);
        capture_write_data(&mut buffer, 1000, &[]);
        assert!(buffer.is_empty());
    }

    #[test]
    fn test_capture_read_data() {
        let mut buffer = RingBuffer::new(1024);
        let data = b"OK\r\n";

        capture_read_data(&mut buffer, 2000, data, data.len());

        assert_eq!(buffer.entry_count(), 1);
        let (header, payload) = buffer.pop().unwrap();
        assert_eq!(header.timestamp, 2000);
        assert_eq!(header.data_direction(), Some(DataDirection::Rx));
        assert_eq!(payload, data.to_vec());
    }

    #[test]
    fn test_capture_read_partial_data() {
        let mut buffer = RingBuffer::new(1024);
        // Buffer is larger than actual bytes read
        let full_buffer = b"OK\r\nGARBAGE_DATA";
        let bytes_read = 4; // Only "OK\r\n" is valid

        capture_read_data(&mut buffer, 2000, full_buffer, bytes_read);

        let (_, payload) = buffer.pop().unwrap();
        assert_eq!(payload, b"OK\r\n");
    }

    #[test]
    fn test_capture_read_zero_bytes() {
        let mut buffer = RingBuffer::new(1024);
        capture_read_data(&mut buffer, 2000, b"data", 0);
        assert!(buffer.is_empty());
    }

    #[test]
    fn test_capture_read_empty_buffer() {
        let mut buffer = RingBuffer::new(1024);
        capture_read_data(&mut buffer, 2000, &[], 5);
        assert!(buffer.is_empty());
    }

    #[test]
    fn test_capture_read_bytes_read_exceeds_buffer() {
        let mut buffer = RingBuffer::new(1024);
        let data = b"short";
        // bytes_read is larger than actual data length — should be clamped
        capture_read_data(&mut buffer, 2000, data, 100);

        let (_, payload) = buffer.pop().unwrap();
        assert_eq!(payload, b"short");
    }

    #[test]
    fn test_mixed_tx_rx_capture() {
        let mut buffer = RingBuffer::new(2048);

        capture_write_data(&mut buffer, 100, b"AT\r\n");
        capture_read_data(&mut buffer, 200, b"OK\r\n", 4);
        capture_write_data(&mut buffer, 300, b"AT+VER\r\n");
        capture_read_data(&mut buffer, 400, b"V1.0\r\n", 6);

        assert_eq!(buffer.entry_count(), 4);

        // Verify FIFO order and directions
        let entries: Vec<_> = (0..4).map(|_| buffer.pop().unwrap()).collect();

        assert_eq!(entries[0].0.timestamp, 100);
        assert_eq!(entries[0].0.data_direction(), Some(DataDirection::Tx));
        assert_eq!(entries[0].1, b"AT\r\n");

        assert_eq!(entries[1].0.timestamp, 200);
        assert_eq!(entries[1].0.data_direction(), Some(DataDirection::Rx));
        assert_eq!(entries[1].1, b"OK\r\n");

        assert_eq!(entries[2].0.timestamp, 300);
        assert_eq!(entries[2].0.data_direction(), Some(DataDirection::Tx));
        assert_eq!(entries[2].1, b"AT+VER\r\n");

        assert_eq!(entries[3].0.timestamp, 400);
        assert_eq!(entries[3].0.data_direction(), Some(DataDirection::Rx));
        assert_eq!(entries[3].1, b"V1.0\r\n");
    }

    #[test]
    fn test_capture_large_write_data() {
        let mut buffer = RingBuffer::new(8192);
        let large_data = vec![0xAB; 4000];

        capture_write_data(&mut buffer, 999, &large_data);

        let (header, payload) = buffer.pop().unwrap();
        assert_eq!(header.data_length, 4000);
        assert_eq!(payload, large_data);
    }

    #[test]
    fn test_capture_preserves_binary_data() {
        let mut buffer = RingBuffer::new(1024);
        // Binary data including null bytes
        let binary_data: Vec<u8> = (0..=255).collect();

        capture_write_data(&mut buffer, 1, &binary_data);

        let (_, payload) = buffer.pop().unwrap();
        assert_eq!(payload, binary_data);
    }

    // ---- Serial IOCTL monitoring tests ----

    #[test]
    fn test_monitored_serial_ioctl() {
        assert!(is_monitored_serial_ioctl(serial_ioctl::SET_BAUD_RATE));
        assert!(is_monitored_serial_ioctl(serial_ioctl::SET_LINE_CONTROL));
        assert!(is_monitored_serial_ioctl(serial_ioctl::SET_HANDFLOW));
    }

    #[test]
    fn test_unmonitored_serial_ioctl() {
        assert!(!is_monitored_serial_ioctl(0x00000000));
        assert!(!is_monitored_serial_ioctl(0xFFFFFFFF));
        assert!(!is_monitored_serial_ioctl(0x001B0008)); // Some other serial IOCTL
    }

    // ---- Header serialization in context of filter operations ----

    #[test]
    fn test_captured_entry_roundtrip_through_buffer() {
        let mut buffer = RingBuffer::new(1024);
        let original_data = b"Test frame data 12345";
        let timestamp = 0x0001_2345_6789_ABCD_u64;

        capture_write_data(&mut buffer, timestamp, original_data);

        let (header, payload) = buffer.pop().unwrap();

        // Verify the header can be serialized and deserialized
        let header_bytes = header.to_bytes();
        let restored_header = CapturedDataHeader::from_bytes(&header_bytes).unwrap();

        assert_eq!(restored_header.timestamp, timestamp);
        assert_eq!(restored_header.data_direction(), Some(DataDirection::Tx));
        assert_eq!(restored_header.data_length, original_data.len() as u32);
        assert_eq!(payload, original_data.to_vec());
    }

    // ---- IRP event structure tests ----

    #[test]
    fn test_irp_event_construction() {
        let event = IrpEvent {
            major_function: IrpMajorFunction::Write,
            timestamp: 42,
            data: b"hello".to_vec(),
            ioctl_code: None,
        };

        assert_eq!(event.major_function, IrpMajorFunction::Write);
        assert_eq!(event.timestamp, 42);
        assert_eq!(event.data, b"hello");
        assert!(event.ioctl_code.is_none());
    }

    #[test]
    fn test_irp_event_with_ioctl() {
        let event = IrpEvent {
            major_function: IrpMajorFunction::DeviceControl,
            timestamp: 100,
            data: vec![],
            ioctl_code: Some(serial_ioctl::SET_BAUD_RATE),
        };

        assert_eq!(event.ioctl_code, Some(serial_ioctl::SET_BAUD_RATE));
    }
}
