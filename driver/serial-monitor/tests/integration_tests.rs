//! Integration tests for the serial monitor driver.
//!
//! These tests verify the full data flow from capture to retrieval,
//! simulating the real-world usage pattern:
//! 1. Initialize driver → Create device + symlink
//! 2. Start monitor → Attach filter to serial port
//! 3. Capture TX/RX data → Store in ring buffer
//! 4. Retrieve data via IOCTL → Read from ring buffer
//! 5. Stop monitor → Detach and cleanup

use serial_monitor::device::{DeviceHandle, DeviceManager, KernelApi, NtStatus};
use serial_monitor::filter::{
    capture_read_data, capture_write_data, determine_filter_action, FilterAction, IrpMajorFunction,
};
use serial_monitor::ioctl;
use serial_monitor::ring_buffer::RingBuffer;
use serial_monitor::shared::*;
use std::collections::HashMap;

/// Mock kernel API for integration testing.
struct MockKernel {
    devices: HashMap<DeviceHandle, String>,
    symlinks: HashMap<String, String>,
    attachments: HashMap<DeviceHandle, DeviceHandle>,
    next_handle: DeviceHandle,
    timestamp_counter: u64,
}

impl MockKernel {
    fn new() -> Self {
        Self {
            devices: HashMap::new(),
            symlinks: HashMap::new(),
            attachments: HashMap::new(),
            next_handle: 1,
            timestamp_counter: 0,
        }
    }

    #[allow(dead_code)]
    fn advance_time(&mut self, delta: u64) {
        self.timestamp_counter += delta;
    }
}

impl KernelApi for MockKernel {
    fn create_device(&mut self, name: &str) -> Result<DeviceHandle, NtStatus> {
        let handle = self.next_handle;
        self.next_handle += 1;
        self.devices.insert(handle, name.to_string());
        Ok(handle)
    }

    fn create_symbolic_link(&mut self, link: &str, target: &str) -> Result<(), NtStatus> {
        self.symlinks.insert(link.to_string(), target.to_string());
        Ok(())
    }

    fn delete_symbolic_link(&mut self, link: &str) -> Result<(), NtStatus> {
        self.symlinks.remove(link);
        Ok(())
    }

    fn delete_device(&mut self, handle: DeviceHandle) -> Result<(), NtStatus> {
        self.devices.remove(&handle);
        Ok(())
    }

    fn attach_device(
        &mut self,
        filter: DeviceHandle,
        _target: &str,
    ) -> Result<DeviceHandle, NtStatus> {
        let lower = self.next_handle;
        self.next_handle += 1;
        self.attachments.insert(filter, lower);
        Ok(lower)
    }

    fn detach_device(
        &mut self,
        filter: DeviceHandle,
        _lower: DeviceHandle,
    ) -> Result<(), NtStatus> {
        self.attachments.remove(&filter);
        Ok(())
    }

    fn get_timestamp(&self) -> u64 {
        self.timestamp_counter
    }
}

fn create_test_manager() -> DeviceManager<MockKernel> {
    DeviceManager::new(MockKernel::new(), 4096)
}

// ============================================================
// Full lifecycle integration tests
// ============================================================

#[test]
fn test_full_driver_lifecycle() {
    let mut mgr = create_test_manager();

    // Phase 1: Initialize
    mgr.initialize().unwrap();
    assert_eq!(mgr.state(), MonitorState::Stopped);

    // Phase 2: Start monitoring COM3
    mgr.start_monitor("\\Device\\Serial0").unwrap();
    assert_eq!(mgr.state(), MonitorState::Running);
    assert_eq!(mgr.monitored_port(), Some("\\Device\\Serial0"));

    // Phase 3: Simulate data capture (TX → RX conversation)
    let tx1 = b"AT\r\n";
    capture_write_data(mgr.buffer_mut(), 100, tx1);
    mgr.add_tx_bytes(tx1.len() as u64);

    let rx1 = b"OK\r\n";
    capture_read_data(mgr.buffer_mut(), 200, rx1, rx1.len());
    mgr.add_rx_bytes(rx1.len() as u64);

    let tx2 = b"AT+VER\r\n";
    capture_write_data(mgr.buffer_mut(), 300, tx2);
    mgr.add_tx_bytes(tx2.len() as u64);

    let rx2 = b"FlexCom V1.0\r\n";
    capture_read_data(mgr.buffer_mut(), 400, rx2, rx2.len());
    mgr.add_rx_bytes(rx2.len() as u64);

    // Phase 4: Verify status
    let status = mgr.status();
    assert_eq!(status.state, MonitorState::Running as u8);
    assert_eq!(status.captured_entry_count, 4);
    assert_eq!(status.total_bytes_tx, (tx1.len() + tx2.len()) as u64);
    assert_eq!(status.total_bytes_rx, (rx1.len() + rx2.len()) as u64);

    // Phase 5: Read back data (simulating IOCTL_GET_DATA)
    let (h, d) = mgr.buffer_mut().pop().unwrap();
    assert_eq!(h.timestamp, 100);
    assert_eq!(h.data_direction(), Some(DataDirection::Tx));
    assert_eq!(d, tx1.to_vec());

    let (h, d) = mgr.buffer_mut().pop().unwrap();
    assert_eq!(h.timestamp, 200);
    assert_eq!(h.data_direction(), Some(DataDirection::Rx));
    assert_eq!(d, rx1.to_vec());

    let (h, d) = mgr.buffer_mut().pop().unwrap();
    assert_eq!(h.timestamp, 300);
    assert_eq!(h.data_direction(), Some(DataDirection::Tx));
    assert_eq!(d, tx2.to_vec());

    let (h, d) = mgr.buffer_mut().pop().unwrap();
    assert_eq!(h.timestamp, 400);
    assert_eq!(h.data_direction(), Some(DataDirection::Rx));
    assert_eq!(d, rx2.to_vec());

    assert!(mgr.buffer_mut().pop().is_none());

    // Phase 6: Stop and cleanup
    mgr.stop_monitor().unwrap();
    assert_eq!(mgr.state(), MonitorState::Stopped);

    mgr.cleanup().unwrap();
}

#[test]
fn test_start_stop_restart_cycle() {
    let mut mgr = create_test_manager();
    mgr.initialize().unwrap();

    // First session
    mgr.start_monitor("\\Device\\Serial0").unwrap();
    capture_write_data(mgr.buffer_mut(), 1, b"session1");
    mgr.stop_monitor().unwrap();

    // Second session — buffer should be reset
    mgr.start_monitor("\\Device\\Serial1").unwrap();
    assert_eq!(mgr.monitored_port(), Some("\\Device\\Serial1"));
    assert!(mgr.buffer().is_empty()); // Buffer was reset on start
    assert_eq!(mgr.total_bytes_rx(), 0); // Counters were reset

    capture_write_data(mgr.buffer_mut(), 2, b"session2");
    let (_, d) = mgr.buffer_mut().pop().unwrap();
    assert_eq!(d, b"session2");

    mgr.stop_monitor().unwrap();
    mgr.cleanup().unwrap();
}

// ============================================================
// Data flow end-to-end tests
// ============================================================

#[test]
fn test_binary_data_roundtrip() {
    let mut buffer = RingBuffer::new(2048);

    // Modbus RTU frame (binary with CRC)
    let modbus_frame: Vec<u8> = vec![0x01, 0x03, 0x00, 0x00, 0x00, 0x02, 0xC4, 0x0B];

    capture_write_data(&mut buffer, 1000, &modbus_frame);

    let (header, payload) = buffer.pop().unwrap();
    assert_eq!(header.data_length, modbus_frame.len() as u32);
    assert_eq!(payload, modbus_frame);
}

#[test]
fn test_dl_t645_frame_roundtrip() {
    let mut buffer = RingBuffer::new(2048);

    // DL/T 645-2007 frame
    let frame: Vec<u8> = vec![
        0xFE, 0xFE, 0xFE, 0xFE, // Preamble
        0x68, // Start
        0x99, 0x99, 0x99, 0x99, 0x99, 0x99, // Address (BCD)
        0x68, // Start2
        0x11, // Control: Read normal response
        0x04, // Length
        0x33, 0x33, 0x34, 0x33, // Data (with +0x33 offset)
        0xC5, // Checksum
        0x16, // End
    ];

    capture_write_data(&mut buffer, 500, &frame);

    let (_, payload) = buffer.pop().unwrap();
    assert_eq!(payload, frame);
}

#[test]
fn test_high_frequency_capture() {
    let mut buffer = RingBuffer::new(16 * 1024); // 16KB

    // Simulate high-frequency data capture (e.g., 1000 small frames)
    for i in 0..1000u64 {
        let data = format!("frame_{:04}", i);
        if i % 2 == 0 {
            capture_write_data(&mut buffer, i * 10, data.as_bytes());
        } else {
            capture_read_data(&mut buffer, i * 10, data.as_bytes(), data.len());
        }
    }

    // Should have data (some may have been dropped due to buffer overflow)
    assert!(!buffer.is_empty());

    // All remaining entries should be valid and in order
    let mut last_timestamp = 0u64;
    while let Some((header, payload)) = buffer.pop() {
        assert!(header.timestamp >= last_timestamp);
        last_timestamp = header.timestamp;
        assert!(!payload.is_empty());
        // Verify payload is a valid "frame_XXXX" string
        let s = String::from_utf8(payload).unwrap();
        assert!(s.starts_with("frame_"));
    }
}

// ============================================================
// IOCTL code verification tests
// ============================================================

#[test]
fn test_ioctl_codes_match_spec() {
    // These values form the contract with C# P/Invoke layer
    // If any of these fail, the C# side must be updated too

    assert!(ioctl::is_serial_monitor_ioctl(ioctl::IOCTL_START_MONITOR));
    assert!(ioctl::is_serial_monitor_ioctl(ioctl::IOCTL_STOP_MONITOR));
    assert!(ioctl::is_serial_monitor_ioctl(ioctl::IOCTL_GET_DATA));
    assert!(ioctl::is_serial_monitor_ioctl(ioctl::IOCTL_GET_STATUS));

    // All use METHOD_BUFFERED
    assert_eq!(ioctl::ioctl_method(ioctl::IOCTL_START_MONITOR), 0);
    assert_eq!(ioctl::ioctl_method(ioctl::IOCTL_STOP_MONITOR), 0);
    assert_eq!(ioctl::ioctl_method(ioctl::IOCTL_GET_DATA), 0);
    assert_eq!(ioctl::ioctl_method(ioctl::IOCTL_GET_STATUS), 0);
}

// ============================================================
// Shared struct memory layout tests (cross-language contract)
// ============================================================

#[test]
fn test_captured_data_header_layout() {
    assert_eq!(std::mem::size_of::<CapturedDataHeader>(), 16);
    assert_eq!(std::mem::align_of::<CapturedDataHeader>(), 8);
}

#[test]
fn test_driver_status_layout() {
    assert_eq!(std::mem::size_of::<DriverStatus>(), 24);
    assert_eq!(std::mem::align_of::<DriverStatus>(), 8);
}

#[test]
fn test_start_monitor_request_layout() {
    assert_eq!(
        std::mem::size_of::<StartMonitorRequest>(),
        MAX_PORT_NAME_LEN * 2
    );
}

#[test]
fn test_get_data_response_layout() {
    let expected = std::mem::size_of::<CapturedDataHeader>() + MAX_DATA_SIZE;
    assert_eq!(std::mem::size_of::<GetDataResponse>(), expected);
}

// ============================================================
// Filter action decision matrix test
// ============================================================

#[test]
fn test_complete_filter_action_matrix() {
    // Exhaustive test of all IRP types × device ownership combinations
    let all_irps = [
        IrpMajorFunction::Create,
        IrpMajorFunction::Close,
        IrpMajorFunction::Read,
        IrpMajorFunction::Write,
        IrpMajorFunction::DeviceControl,
        IrpMajorFunction::InternalDeviceControl,
        IrpMajorFunction::Cleanup,
        IrpMajorFunction::Other,
    ];

    // For our control device
    for irp in &all_irps {
        let action = determine_filter_action(*irp, true);
        match irp {
            IrpMajorFunction::DeviceControl => {
                assert_eq!(action, FilterAction::HandleInternal);
            }
            _ => {
                assert_eq!(action, FilterAction::PassThrough);
            }
        }
    }

    // For filtered serial device
    for irp in &all_irps {
        let action = determine_filter_action(*irp, false);
        match irp {
            IrpMajorFunction::Write => {
                assert_eq!(action, FilterAction::CaptureAndForward);
            }
            IrpMajorFunction::Read => {
                assert_eq!(action, FilterAction::ForwardWithCompletion);
            }
            IrpMajorFunction::DeviceControl => {
                assert_eq!(action, FilterAction::CaptureAndForward);
            }
            _ => {
                assert_eq!(action, FilterAction::PassThrough);
            }
        }
    }
}

// ============================================================
// Edge case tests
// ============================================================

#[test]
fn test_capture_with_null_bytes() {
    let mut buffer = RingBuffer::new(1024);
    let data_with_nulls = vec![0x00, 0x01, 0x00, 0x02, 0x00];

    capture_write_data(&mut buffer, 1, &data_with_nulls);
    let (_, payload) = buffer.pop().unwrap();
    assert_eq!(payload, data_with_nulls);
}

#[test]
fn test_capture_single_byte() {
    let mut buffer = RingBuffer::new(1024);

    capture_write_data(&mut buffer, 1, &[0xFF]);
    let (header, payload) = buffer.pop().unwrap();
    assert_eq!(header.data_length, 1);
    assert_eq!(payload, vec![0xFF]);
}

#[test]
fn test_start_monitor_request_port_name_encoding() {
    // Test that port names survive the UTF-16 encoding roundtrip
    let names = ["COM1", "COM256", "\\Device\\Serial0", "\\Device\\USBSER000"];

    for name in &names {
        let req = StartMonitorRequest::from_port_name(name).unwrap();
        assert_eq!(req.port_name_string(), *name);
    }
}

#[test]
fn test_header_serialization_endianness() {
    let header = CapturedDataHeader::new(0x0102030405060708, DataDirection::Rx, 0xAABBCCDD);
    let bytes = header.to_bytes();

    // Verify little-endian byte order
    assert_eq!(bytes[0], 0x08); // LSB of timestamp
    assert_eq!(bytes[7], 0x01); // MSB of timestamp
    assert_eq!(bytes[8], 0x01); // direction = Rx
    assert_eq!(bytes[12], 0xDD); // LSB of data_length
    assert_eq!(bytes[15], 0xAA); // MSB of data_length
}

#[test]
fn test_driver_status_snapshot_consistency() {
    let mut mgr = create_test_manager();
    mgr.initialize().unwrap();
    mgr.start_monitor("\\Device\\Serial0").unwrap();

    // Add data
    for i in 0..10u64 {
        capture_write_data(mgr.buffer_mut(), i, b"test");
        mgr.add_tx_bytes(4);
    }

    let status = mgr.status();
    assert_eq!(status.state, MonitorState::Running as u8);
    assert_eq!(status.captured_entry_count, 10);
    assert_eq!(status.total_bytes_tx, 40);
    assert_eq!(status.total_bytes_rx, 0);

    // Pop some data and verify status updates
    mgr.buffer_mut().pop();
    mgr.buffer_mut().pop();

    let status2 = mgr.status();
    assert_eq!(status2.captured_entry_count, 8);
    // Byte counters are cumulative, not affected by pop
    assert_eq!(status2.total_bytes_tx, 40);

    mgr.cleanup().unwrap();
}
