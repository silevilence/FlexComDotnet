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

// In kernel (no_std) mode, String/format! come from alloc instead of std.
#[cfg(feature = "kernel")]
use alloc::string::{String, ToString};

use crate::ioctl;
use crate::ring_buffer::RingBuffer;
use crate::shared::{DriverDiagnostics, MonitorStartStage, MonitorState};

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

const FILTER_DEVICE_NAME: &str = "\\Device\\SerialMonitorFilter";

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

    /// Configures the filter device with the I/O characteristics inherited
    /// from the device it is attached to.
    fn configure_filter_device(
        &mut self,
        filter_device: DeviceHandle,
        lower_device: DeviceHandle,
    ) -> Result<(), NtStatus>;

    /// Clears the initialization gate only after a device has been fully
    /// configured. Until this succeeds, the I/O manager must not dispatch
    /// requests to the device.
    fn mark_device_ready(&mut self, device: DeviceHandle) -> Result<(), NtStatus>;

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
    /// Most recent failure and filter-open diagnostics exposed to user mode.
    diagnostics: DriverDiagnostics,
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
            diagnostics: DriverDiagnostics::default(),
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

    /// Makes the user-mode control device available after the driver has
    /// installed its dispatch table and published the device manager.
    pub fn activate_control_device(&mut self) -> Result<(), NtStatus> {
        let Some(device) = self.control_device else {
            return Err(STATUS_INVALID_PARAMETER);
        };

        self.kernel.mark_device_ready(device)
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
            self.set_start_diagnostic(MonitorStartStage::Idle, STATUS_ALREADY_MONITORING);
            return Err(STATUS_ALREADY_MONITORING);
        }

        if port_name.is_empty() {
            self.set_start_diagnostic(MonitorStartStage::Idle, STATUS_INVALID_PARAMETER);
            return Err(STATUS_INVALID_PARAMETER);
        }

        // Create filter device
        self.diagnostics = DriverDiagnostics::default();
        self.diagnostics.last_start_stage = MonitorStartStage::CreatingFilter as u32;
        let filter_dev = match self.kernel.create_device(FILTER_DEVICE_NAME) {
            Ok(device) => device,
            Err(error) => {
                self.set_start_diagnostic(MonitorStartStage::CreatingFilter, error);
                return Err(error);
            }
        };
        self.filter_device = Some(filter_dev);

        // Attach to target serial port device stack
        self.diagnostics.last_start_stage = MonitorStartStage::AttachingFilter as u32;
        match self.kernel.attach_device(filter_dev, port_name) {
            Ok(lower_dev) => {
                self.diagnostics.last_start_stage = MonitorStartStage::ConfiguringFilter as u32;
                if let Err(e) = self.kernel.configure_filter_device(filter_dev, lower_dev) {
                    self.set_start_diagnostic(MonitorStartStage::ConfiguringFilter, e);
                    let _ = self.kernel.detach_device(filter_dev, lower_dev);
                    let _ = self.kernel.delete_device(filter_dev);
                    self.filter_device = None;
                    return Err(e);
                }
                self.diagnostics.last_start_stage = MonitorStartStage::EnablingFilter as u32;
                if let Err(e) = self.kernel.mark_device_ready(filter_dev) {
                    self.set_start_diagnostic(MonitorStartStage::EnablingFilter, e);
                    let _ = self.kernel.detach_device(filter_dev, lower_dev);
                    let _ = self.kernel.delete_device(filter_dev);
                    self.filter_device = None;
                    return Err(e);
                }
                self.lower_device = Some(lower_dev);
            }
            Err(e) => {
                self.set_start_diagnostic(MonitorStartStage::AttachingFilter, e);
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
        self.set_start_diagnostic(MonitorStartStage::Running, STATUS_SUCCESS);

        Ok(())
    }

    /// Starts capture after a PnP filter device has already been inserted into
    /// the serial device stack by the Plug and Play manager. Unlike
    /// `start_monitor`, this does not attach to a named device at runtime.
    pub fn start_pnp_monitor(&mut self, port_name: &str) -> Result<(), NtStatus> {
        if self.state == MonitorState::Running {
            self.set_start_diagnostic(MonitorStartStage::Idle, STATUS_ALREADY_MONITORING);
            return Err(STATUS_ALREADY_MONITORING);
        }
        if port_name.is_empty() {
            self.set_start_diagnostic(MonitorStartStage::Idle, STATUS_INVALID_PARAMETER);
            return Err(STATUS_INVALID_PARAMETER);
        }

        self.diagnostics = DriverDiagnostics::default();
        self.monitored_port = Some(port_name.to_string());
        self.state = MonitorState::Running;
        self.buffer.reset();
        self.total_bytes_rx = 0;
        self.total_bytes_tx = 0;
        self.set_start_diagnostic(MonitorStartStage::Running, STATUS_SUCCESS);
        Ok(())
    }

    /// Records that no PnP filter device was attached to the selected serial
    /// stack. This is reported to user mode instead of pretending monitoring
    /// started successfully.
    pub fn report_pnp_filter_unavailable(&mut self) {
        self.diagnostics = DriverDiagnostics::default();
        self.set_start_diagnostic(MonitorStartStage::AttachingFilter, STATUS_UNSUCCESSFUL);
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

    /// Returns whether a device object is the user-mode control device.
    pub fn is_control_device(&self, device: DeviceHandle) -> bool {
        self.control_device == Some(device)
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

    /// Returns the latest startup and external-open diagnostic state.
    pub fn diagnostics(&self) -> DriverDiagnostics {
        self.diagnostics
    }

    /// Records the completion result of an external application's CREATE IRP
    /// that passed through the filter device.
    pub fn record_filter_create_completion(&mut self, status: NtStatus) {
        self.diagnostics.filter_create_count =
            self.diagnostics.filter_create_count.saturating_add(1);
        self.diagnostics.last_filter_create_status = status;
    }

    fn set_start_diagnostic(&mut self, stage: MonitorStartStage, status: NtStatus) {
        self.diagnostics.last_start_stage = stage as u32;
        self.diagnostics.last_start_status = status;
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
        configured_filter: Option<(DeviceHandle, DeviceHandle)>,
        ready_devices: Vec<DeviceHandle>,
        operation_log: Vec<String>,
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
                configured_filter: None,
                ready_devices: Vec::new(),
                operation_log: Vec::new(),
            }
        }

        fn device_count(&self) -> usize {
            self.devices.len()
        }

        fn device_names(&self) -> impl Iterator<Item = &str> {
            self.devices.values().map(String::as_str)
        }

        fn symlink_count(&self) -> usize {
            self.symlinks.len()
        }

        fn configured_filter(&self) -> Option<(DeviceHandle, DeviceHandle)> {
            self.configured_filter
        }

        fn operation_log(&self) -> &[String] {
            &self.operation_log
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
            self.operation_log.push(format!("create:{handle}"));
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
            self.operation_log.push(format!("attach:{filter_device}"));
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

        fn configure_filter_device(
            &mut self,
            filter_device: DeviceHandle,
            lower_device: DeviceHandle,
        ) -> Result<(), NtStatus> {
            self.configured_filter = Some((filter_device, lower_device));
            self.operation_log
                .push(format!("configure:{filter_device}"));
            Ok(())
        }

        fn mark_device_ready(&mut self, device: DeviceHandle) -> Result<(), NtStatus> {
            self.ready_devices.push(device);
            self.operation_log.push(format!("ready:{device}"));
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
    fn test_start_monitor_uses_a_valid_filter_device_name() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        mgr.start_monitor("\\Device\\USBSER000").unwrap();

        assert!(mgr
            .kernel()
            .device_names()
            .any(|name| name == "\\Device\\SerialMonitorFilter"));
    }

    #[test]
    fn test_start_monitor_configures_filter_for_the_attached_serial_device() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        mgr.start_monitor("\\Device\\USBSER000").unwrap();

        assert!(mgr.kernel().configured_filter().is_some());
    }

    #[test]
    fn test_start_monitor_marks_filter_ready_only_after_attach_and_configuration() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        mgr.start_monitor("\\Device\\USBSER000").unwrap();

        assert_eq!(
            mgr.kernel().operation_log(),
            ["create:1", "create:2", "attach:2", "configure:2", "ready:2"]
        );
    }

    #[test]
    fn test_start_monitor_attach_failure_exposes_stage_and_ntstatus() {
        let mut kernel = MockKernel::new();
        kernel.fail_attach = Some(STATUS_UNSUCCESSFUL);
        let mut mgr = DeviceManager::new(kernel, 1024);
        mgr.initialize().unwrap();

        mgr.start_monitor("\\Device\\USBSER000").unwrap_err();

        let diagnostic = mgr.diagnostics();
        assert_eq!(
            diagnostic.last_start_stage,
            MonitorStartStage::AttachingFilter as u32
        );
        assert_eq!(diagnostic.last_start_status, STATUS_UNSUCCESSFUL);
    }

    #[test]
    fn test_start_pnp_monitor_does_not_create_or_attach_a_runtime_filter() {
        let mut mgr = create_manager();
        mgr.initialize().unwrap();

        mgr.start_pnp_monitor("COM10").unwrap();

        assert_eq!(mgr.state(), MonitorState::Running);
        assert!(mgr.filter_device.is_none());
        assert!(mgr.lower_device.is_none());
        assert_eq!(
            mgr.diagnostics().last_start_stage,
            MonitorStartStage::Running as u32
        );
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
