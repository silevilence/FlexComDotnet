//! Device object creation and management for the serial monitor driver.
//!
//! This module abstracts kernel device operations behind traits to enable
//! unit testing without actual kernel dependencies.
//!
//! # Architecture
//! - `KernelApi` trait: abstracts all kernel API calls (device creation, etc.)
//! - `DeviceManager`: manages the lifecycle of the control device and filter devices
//!
//! In kernel mode (`kernel` feature), `KernelApi` is implemented by the
//! actual WDK bindings. In test mode, a mock implementation is used.

use crate::ioctl;
use crate::ring_buffer::RingBuffer;
use crate::shared::MonitorState;

/// NTSTATUS-compatible result type.
/// In Windows kernel, NTSTATUS is a 32-bit signed integer.
pub type NtStatus = i32;

/// Common NTSTATUS codes.
pub const STATUS_SUCCESS: NtStatus = 0;
pub const STATUS_UNSUCCESSFUL: NtStatus = -1_073_741_823; // 0xC0000001
pub const STATUS_INVALID_PARAMETER: NtStatus = -1_073_741_811; // 0xC000000D
pub const STATUS_INSUFFICIENT_RESOURCES: NtStatus = -1_073_741_801; // 0xC000009A

// Simplified status codes for our logic
pub const STATUS_ALREADY_MONITORING: NtStatus = -1; // Custom: already monitoring a port
pub const STATUS_NOT_MONITORING: NtStatus = -2; // Custom: not currently monitoring
pub const STATUS_BUFFER_TOO_SMALL: NtStatus = -3; // Custom: output buffer too small
pub const STATUS_NO_DATA: NtStatus = -4; // Custom: no data available

/// Opaque handle representing a kernel device object.
/// In actual kernel code, this wraps a `PDEVICE_OBJECT`.
/// In tests, it can be any unique identifier.
pub type DeviceHandle = u64;

/// Trait abstracting kernel API calls for device operations.
///
/// This allows the device management logic to be tested without
/// actual kernel dependencies.
pub trait KernelApi {
    /// Creates a device object with the given name.
    ///
    /// Returns a handle to the created device, or an error status.
    fn create_device(&mut self, name: &str) -> Result<DeviceHandle, NtStatus>;

    /// Creates a symbolic link pointing to a device.
    fn create_symbolic_link(&mut self, link_name: &str, device_name: &str) -> Result<(), NtStatus>;

    /// Deletes a symbolic link.
    fn delete_symbolic_link(&mut self, link_name: &str) -> Result<(), NtStatus>;

    /// Deletes a device object.
    fn delete_device(&mut self, handle: DeviceHandle) -> Result<(), NtStatus>;

    /// Attaches a filter device to a target device in the device stack.
    ///
    /// Returns a handle to the lower device in the stack, or error.
    fn attach_device(
        &mut self,
        filter_device: DeviceHandle,
        target_device_name: &str,
    ) -> Result<DeviceHandle, NtStatus>;

    /// Detaches a filter device from the device stack.
    fn detach_device(
        &mut self,
        filter_device: DeviceHandle,
        lower_device: DeviceHandle,
    ) -> Result<(), NtStatus>;

    /// Gets the current system timestamp (100-nanosecond intervals).
    fn get_timestamp(&self) -> u64;
}

/// Manages the driver's device objects, monitoring state, and data buffer.
pub struct DeviceManager<K: KernelApi> {
    /// Kernel API abstraction.
    kernel: K,
    /// Handle to the control device object (`\Device\SerialMonitor`).
    control_device: Option<DeviceHandle>,
    /// Handle to the filter device attached to the target serial port.
    filter_device: Option<DeviceHandle>,
    /// Handle to the lower device in the serial port's device stack.
    lower_device: Option<DeviceHandle>,
    /// Name of the currently monitored port.
    monitored_port: Option<String>,
    /// Current monitoring state.
    state: MonitorState,
    /// Ring buffer for captured data.
    buffer: RingBuffer,
    /// Statistics: total RX bytes.
    total_bytes_rx: u64,
    /// Statistics: total TX bytes.
    total_bytes_tx: u64,
}

impl<K: KernelApi> DeviceManager<K> {
    /// Creates a new `DeviceManager` with the given kernel API.
    pub fn new(kernel: K, buffer_capacity: usize) -> Self {
        Self {
            kernel,
            control_device: None,
            filter_device: None,
            lower_device: None,
            monitored_port: None,
            state: MonitorState::Stopped,
            buffer: RingBuffer::new(buffer_capacity),
            total_bytes_rx: 0,
            total_bytes_tx: 0,
        }
    }

    /// Initializes the driver by creating the control device and symbolic link.
    ///
    /// This should be called from `DriverEntry`.
    pub fn initialize(&mut self) -> Result<(), NtStatus> {
        // Create the control device
        let device = self.kernel.create_device(ioctl::DEVICE_NAME)?;
        self.control_device = Some(device);

        // Create symbolic link for user-mode access
        if let Err(e) = self
            .kernel
            .create_symbolic_link(ioctl::SYMLINK_NAME, ioctl::DEVICE_NAME)
        {
            // Clean up device if symlink creation fails
            let _ = self.kernel.delete_device(device);
            self.control_device = None;
            return Err(e);
        }

        Ok(())
    }

    /// Cleans up all resources. Called during driver unload.
    pub fn cleanup(&mut self) -> Result<(), NtStatus> {
        // Stop monitoring if active
        if self.state == MonitorState::Running {
            let _ = self.stop_monitor();
        }

        // Delete symbolic link
        let _ = self.kernel.delete_symbolic_link(ioctl::SYMLINK_NAME);

        // Delete control device
        if let Some(device) = self.control_device.take() {
            let _ = self.kernel.delete_device(device);
        }

        Ok(())
    }

    /// Starts monitoring the specified serial port.
    ///
    /// Creates a filter device and attaches it to the target serial port's
    /// device stack.
    pub fn start_monitor(&mut self, port_name: &str) -> Result<(), NtStatus> {
        if self.state == MonitorState::Running {
            return Err(STATUS_ALREADY_MONITORING);
        }

        if port_name.is_empty() {
            return Err(STATUS_INVALID_PARAMETER);
        }

        // Create filter device
        let filter_device_name = format!("\\Device\\SerialMonitorFilter_{}", port_name);
        let filter_dev = self.kernel.create_device(&filter_device_name)?;
        self.filter_device = Some(filter_dev);

        // Attach to target serial port device stack
        match self.kernel.attach_device(filter_dev, port_name) {
            Ok(lower_dev) => {
                self.lower_device = Some(lower_dev);
            }
            Err(e) => {
                // Clean up filter device on attach failure
                let _ = self.kernel.delete_device(filter_dev);
                self.filter_device = None;
                return Err(e);
            }
        }

        self.monitored_port = Some(port_name.to_string());
        self.state = MonitorState::Running;
        self.buffer.reset();
        self.total_bytes_rx = 0;
        self.total_bytes_tx = 0;

        Ok(())
    }

    /// Stops monitoring the current serial port.
    ///
    /// Detaches the filter device from the device stack and destroys it.
    pub fn stop_monitor(&mut self) -> Result<(), NtStatus> {
        if self.state != MonitorState::Running {
            return Err(STATUS_NOT_MONITORING);
        }

        // Detach from device stack
        if let (Some(filter_dev), Some(lower_dev)) = (self.filter_device, self.lower_device) {
            let _ = self.kernel.detach_device(filter_dev, lower_dev);
        }

        // Delete filter device
        if let Some(filter_dev) = self.filter_device.take() {
            let _ = self.kernel.delete_device(filter_dev);
        }

        self.lower_device = None;
        self.monitored_port = None;
        self.state = MonitorState::Stopped;

        Ok(())
    }

    /// Returns the current monitoring state.
    pub fn state(&self) -> MonitorState {
        self.state
    }

    /// Returns the currently monitored port name, if any.
    pub fn monitored_port(&self) -> Option<&str> {
        self.monitored_port.as_deref()
    }

    /// Returns the handle to the lower device for IRP forwarding.
    pub fn lower_device(&self) -> Option<DeviceHandle> {
        self.lower_device
    }

    /// Returns a mutable reference to the ring buffer.
    pub fn buffer_mut(&mut self) -> &mut RingBuffer {
        &mut self.buffer
    }

    /// Returns an immutable reference to the ring buffer.
    pub fn buffer(&self) -> &RingBuffer {
        &self.buffer
    }

    /// Returns the kernel API (for IRP forwarding).
    pub fn kernel(&self) -> &K {
        &self.kernel
    }

    /// Returns a mutable reference to the kernel API.
    pub fn kernel_mut(&mut self) -> &mut K {
        &mut self.kernel
    }

    /// Returns the current timestamp from the kernel.
    pub fn timestamp(&self) -> u64 {
        self.kernel.get_timestamp()
    }

    /// Returns a `DriverStatus` snapshot.
    pub fn status(&self) -> crate::shared::DriverStatus {
        crate::shared::DriverStatus {
            state: self.state as u8,
            reserved1: 0,
            reserved2: 0,
            captured_entry_count: self.buffer.entry_count() as u32,
            total_bytes_rx: self.total_bytes_rx,
            total_bytes_tx: self.total_bytes_tx,
        }
    }

    /// Adds to the RX byte counter.
    pub fn add_rx_bytes(&mut self, count: u64) {
        self.total_bytes_rx += count;
    }

    /// Adds to the TX byte counter.
    pub fn add_tx_bytes(&mut self, count: u64) {
        self.total_bytes_tx += count;
    }

    /// Returns total RX bytes.
    pub fn total_bytes_rx(&self) -> u64 {
        self.total_bytes_rx
    }

    /// Returns total TX bytes.
    pub fn total_bytes_tx(&self) -> u64 {
        self.total_bytes_tx
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashMap;

    /// Mock kernel API for testing.
    struct MockKernel {
        devices: HashMap<DeviceHandle, String>,
        symlinks: HashMap<String, String>,
        attachments: HashMap<DeviceHandle, DeviceHandle>,
        next_handle: DeviceHandle,
        timestamp: u64,
        /// If set, create_device will fail with this status.
        fail_create_device: Option<NtStatus>,
        /// If set, attach_device will fail with this status.
        fail_attach: Option<NtStatus>,
        /// If set, create_symbolic_link will fail with this status.
        fail_symlink: Option<NtStatus>,
    }

    impl MockKernel {
        fn new() -> Self {
            Self {
                devices: HashMap::new(),
                symlinks: HashMap::new(),
                attachments: HashMap::new(),
                next_handle: 1,
                timestamp: 0,
                fail_create_device: None,
                fail_attach: None,
                fail_symlink: None,
            }
        }

        fn device_count(&self) -> usize {
            self.devices.len()
        }

        fn symlink_count(&self) -> usize {
            self.symlinks.len()
        }
    }

    impl KernelApi for MockKernel {
        fn create_device(&mut self, name: &str) -> Result<DeviceHandle, NtStatus> {
            if let Some(status) = self.fail_create_device {
                return Err(status);
            }
            let handle = self.next_handle;
            self.next_handle += 1;
            self.devices.insert(handle, name.to_string());
            Ok(handle)
        }

        fn create_symbolic_link(
            &mut self,
            link_name: &str,
            device_name: &str,
        ) -> Result<(), NtStatus> {
            if let Some(status) = self.fail_symlink {
                return Err(status);
            }
            self.symlinks
                .insert(link_name.to_string(), device_name.to_string());
            Ok(())
        }

        fn delete_symbolic_link(&mut self, link_name: &str) -> Result<(), NtStatus> {
            self.symlinks.remove(link_name);
            Ok(())
        }

        fn delete_device(&mut self, handle: DeviceHandle) -> Result<(), NtStatus> {
            self.devices.remove(&handle);
            Ok(())
        }

        fn attach_device(
            &mut self,
            filter_device: DeviceHandle,
            _target_device_name: &str,
        ) -> Result<DeviceHandle, NtStatus> {
            if let Some(status) = self.fail_attach {
                return Err(status);
            }
            let lower_handle = self.next_handle;
            self.next_handle += 1;
            self.attachments.insert(filter_device, lower_handle);
            Ok(lower_handle)
        }

        fn detach_device(
            &mut self,
            filter_device: DeviceHandle,
            _lower_device: DeviceHandle,
        ) -> Result<(), NtStatus> {
            self.attachments.remove(&filter_device);
            Ok(())
        }

        fn get_timestamp(&self) -> u64 {
            self.timestamp
        }
    }

    fn create_manager() -> DeviceManager<MockKernel> {
        DeviceManager::new(MockKernel::new(), 1024)
    }

    #[test]
    fn test_initialize_creates_device_and_symlink() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        assert!(mgr.control_device.is_some());
        assert_eq!(mgr.kernel().device_count(), 1);
        assert_eq!(mgr.kernel().symlink_count(), 1);
    }

    #[test]
    fn test_initialize_cleanup_on_symlink_failure() {
        let mut kernel = MockKernel::new();
        kernel.fail_symlink = Some(STATUS_UNSUCCESSFUL);
        let mut mgr = DeviceManager::new(kernel, 1024);

        let result = mgr.initialize();
        assert!(result.is_err());
        assert!(mgr.control_device.is_none());
        // Device should be cleaned up
        assert_eq!(mgr.kernel().device_count(), 0);
    }

    #[test]
    fn test_initialize_failure_on_device_creation() {
        let mut kernel = MockKernel::new();
        kernel.fail_create_device = Some(STATUS_INSUFFICIENT_RESOURCES);
        let mut mgr = DeviceManager::new(kernel, 1024);

        let result = mgr.initialize();
        assert!(result.is_err());
        assert!(mgr.control_device.is_none());
    }

    #[test]
    fn test_start_monitor() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.start_monitor("\\Device\\Serial0").unwrap();

        assert_eq!(mgr.state(), MonitorState::Running);
        assert_eq!(mgr.monitored_port(), Some("\\Device\\Serial0"));
        assert!(mgr.lower_device().is_some());
    }

    #[test]
    fn test_start_monitor_empty_port_name() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        let result = mgr.start_monitor("");
        assert_eq!(result.unwrap_err(), STATUS_INVALID_PARAMETER);
        assert_eq!(mgr.state(), MonitorState::Stopped);
    }

    #[test]
    fn test_start_monitor_already_running() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.start_monitor("\\Device\\Serial0").unwrap();

        let result = mgr.start_monitor("\\Device\\Serial1");
        assert_eq!(result.unwrap_err(), STATUS_ALREADY_MONITORING);
    }

    #[test]
    fn test_start_monitor_attach_failure_cleanup() {
        let mut kernel = MockKernel::new();
        kernel.fail_attach = Some(STATUS_UNSUCCESSFUL);
        let mut mgr = DeviceManager::new(kernel, 1024);
        mgr.initialize().unwrap();

        let devices_before = mgr.kernel().device_count();
        let result = mgr.start_monitor("\\Device\\Serial0");
        assert!(result.is_err());

        // Filter device should be cleaned up
        assert!(mgr.filter_device.is_none());
        assert_eq!(mgr.state(), MonitorState::Stopped);
        assert_eq!(mgr.kernel().device_count(), devices_before);
    }

    #[test]
    fn test_stop_monitor() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.start_monitor("\\Device\\Serial0").unwrap();
        mgr.stop_monitor().unwrap();

        assert_eq!(mgr.state(), MonitorState::Stopped);
        assert!(mgr.monitored_port().is_none());
        assert!(mgr.lower_device().is_none());
        assert!(mgr.filter_device.is_none());
    }

    #[test]
    fn test_stop_monitor_not_running() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        let result = mgr.stop_monitor();
        assert_eq!(result.unwrap_err(), STATUS_NOT_MONITORING);
    }

    #[test]
    fn test_cleanup_while_monitoring() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.start_monitor("\\Device\\Serial0").unwrap();

        mgr.cleanup().unwrap();

        assert_eq!(mgr.state(), MonitorState::Stopped);
        assert!(mgr.control_device.is_none());
    }

    #[test]
    fn test_cleanup_when_stopped() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.cleanup().unwrap();

        assert!(mgr.control_device.is_none());
    }

    #[test]
    fn test_status_stopped() {
        let mgr = create_manager();
        let status = mgr.status();

        assert_eq!(status.state, MonitorState::Stopped as u8);
        assert_eq!(status.captured_entry_count, 0);
        assert_eq!(status.total_bytes_rx, 0);
        assert_eq!(status.total_bytes_tx, 0);
    }

    #[test]
    fn test_status_running_with_data() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();
        mgr.start_monitor("\\Device\\Serial0").unwrap();

        // Simulate data capture
        mgr.add_rx_bytes(100);
        mgr.add_tx_bytes(50);
        mgr.buffer_mut()
            .push(1, crate::shared::DataDirection::Rx, b"data");

        let status = mgr.status();
        assert_eq!(status.state, MonitorState::Running as u8);
        assert_eq!(status.captured_entry_count, 1);
        assert_eq!(status.total_bytes_rx, 100);
        assert_eq!(status.total_bytes_tx, 50);
    }

    #[test]
    fn test_byte_counters() {
        let mut mgr = create_manager();

        mgr.add_rx_bytes(100);
        mgr.add_rx_bytes(200);
        assert_eq!(mgr.total_bytes_rx(), 300);

        mgr.add_tx_bytes(50);
        mgr.add_tx_bytes(75);
        assert_eq!(mgr.total_bytes_tx(), 125);
    }

    #[test]
    fn test_start_monitor_resets_buffer_and_counters() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        // Add some data
        mgr.add_rx_bytes(500);
        mgr.add_tx_bytes(300);
        mgr.buffer_mut()
            .push(1, crate::shared::DataDirection::Rx, b"old data");

        mgr.start_monitor("\\Device\\Serial0").unwrap();

        assert_eq!(mgr.total_bytes_rx(), 0);
        assert_eq!(mgr.total_bytes_tx(), 0);
        assert!(mgr.buffer().is_empty());
    }

    #[test]
    fn test_timestamp_from_kernel() {
        let mut kernel = MockKernel::new();
        kernel.timestamp = 123456789;
        let mgr = DeviceManager::new(kernel, 1024);
        assert_eq!(mgr.timestamp(), 123456789);
    }

    #[test]
    fn test_full_lifecycle() {
        let mut mgr = create_manager();

        // Initialize
        mgr.initialize().unwrap();
        assert_eq!(mgr.state(), MonitorState::Stopped);

        // Start monitoring
        mgr.start_monitor("\\Device\\Serial0").unwrap();
        assert_eq!(mgr.state(), MonitorState::Running);

        // Capture some data
        mgr.buffer_mut()
            .push(100, crate::shared::DataDirection::Tx, b"hello");
        mgr.add_tx_bytes(5);

        mgr.buffer_mut()
            .push(200, crate::shared::DataDirection::Rx, b"world");
        mgr.add_rx_bytes(5);

        // Verify data
        let status = mgr.status();
        assert_eq!(status.captured_entry_count, 2);
        assert_eq!(status.total_bytes_tx, 5);
        assert_eq!(status.total_bytes_rx, 5);

        // Read data
        let (h, d) = mgr.buffer_mut().pop().unwrap();
        assert_eq!(h.timestamp, 100);
        assert_eq!(d, b"hello");

        // Stop monitoring
        mgr.stop_monitor().unwrap();
        assert_eq!(mgr.state(), MonitorState::Stopped);

        // Cleanup
        mgr.cleanup().unwrap();
    }
}
