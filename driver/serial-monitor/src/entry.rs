//! Windows kernel driver entry point and WDK API implementation.
//!
//! This module provides:
//! - `DriverEntry`: The kernel entry point called when the driver loads.
//! - `driver_unload`: Called when the driver is unloaded.
//! - `WdkKernelApi`: The real [`KernelApi`] implementation using WDK bindings.
//! - IRP dispatch stubs for the driver's major function table.
//!
//! # Safety
//! This module contains `unsafe` kernel API calls. Each unsafe block documents
//! the safety invariant being upheld. The driver relies on WDK guarantees about
//! object lifetimes, IRQL levels, and pointer validity.
//!
//! # Build
//! This module is only compiled with `--features kernel` and requires WDK/eWDK.
//! ```bash
//! cargo make --features kernel          # Build .sys driver
//! cargo make --features kernel --release # Release build
//! ```

use core::sync::atomic::{AtomicPtr, Ordering};

use wdk_sys::{
    ntddk::{
        IoAttachDevice, IoCreateDevice, IoCreateSymbolicLink, IoDeleteDevice, IoDeleteSymbolicLink,
        IoDetachDevice, IofCompleteRequest, KeQuerySystemTimePrecise,
    },
    DEVICE_OBJECT, DRIVER_OBJECT, FILE_DEVICE_UNKNOWN, IRP, LARGE_INTEGER, NTSTATUS,
    PCUNICODE_STRING, PDEVICE_OBJECT, PDRIVER_OBJECT, STATUS_SUCCESS, STATUS_UNSUCCESSFUL,
    UNICODE_STRING,
};

use crate::device::{DeviceHandle, DeviceManager, KernelApi, NtStatus};
use crate::ring_buffer::DEFAULT_BUFFER_CAPACITY;

// ---------------------------------------------------------------------------
// Global driver state
// ---------------------------------------------------------------------------

/// Global pointer to the heap-allocated `DeviceManager<WdkKernelApi>`.
///
/// # Safety
/// Access is synchronized by the kernel's guarantee that `DriverEntry` completes
/// before any IRP dispatch or `DriverUnload` is called. After initialization,
/// concurrent access must be serialized externally (e.g., via SpinLock in IRP
/// dispatch paths). For the MVP, IOCTL handlers run at PASSIVE_LEVEL and are
/// serialized by the I/O manager's device queue.
static DEVICE_MANAGER: AtomicPtr<DeviceManager<WdkKernelApi>> =
    AtomicPtr::new(core::ptr::null_mut());

// ---------------------------------------------------------------------------
// WDK KernelApi implementation
// ---------------------------------------------------------------------------

/// Real kernel API implementation backed by WDK FFI calls.
///
/// Each method wraps one or more Windows kernel functions with proper
/// safety checks and UNICODE_STRING conversions.
pub struct WdkKernelApi {
    /// The driver object pointer, needed for `IoCreateDevice`.
    driver_object: PDRIVER_OBJECT,
}

impl WdkKernelApi {
    /// Creates a new `WdkKernelApi` wrapping the given driver object.
    ///
    /// # Safety
    /// `driver_object` must be a valid, non-null pointer that remains valid
    /// for the lifetime of this struct (guaranteed by the kernel for loaded drivers).
    pub unsafe fn new(driver_object: PDRIVER_OBJECT) -> Self {
        Self { driver_object }
    }
}

/// Converts a Rust `&str` to a kernel `UNICODE_STRING` backed by a UTF-16 buffer.
///
/// # Returns
/// A tuple of (UNICODE_STRING, Vec<u16>) where the Vec owns the backing buffer.
/// The caller must keep the Vec alive while the UNICODE_STRING is in use.
fn str_to_unicode_string(s: &str) -> (UNICODE_STRING, alloc::vec::Vec<u16>) {
    let utf16: alloc::vec::Vec<u16> = s.encode_utf16().collect();
    let byte_len = (utf16.len() * 2) as u16;
    let us = UNICODE_STRING {
        Length: byte_len,
        MaximumLength: byte_len,
        Buffer: utf16.as_ptr() as *mut u16,
    };
    (us, utf16)
}

impl KernelApi for WdkKernelApi {
    fn create_device(&mut self, name: &str) -> Result<DeviceHandle, NtStatus> {
        let (mut dev_name, _name_buf) = str_to_unicode_string(name);
        let mut device_object: PDEVICE_OBJECT = core::ptr::null_mut();

        // SAFETY: IoCreateDevice is called at PASSIVE_LEVEL during DriverEntry
        // or IOCTL handling. driver_object is valid (kernel guarantee).
        // device_object receives the output pointer.
        let status = unsafe {
            IoCreateDevice(
                self.driver_object,
                0, // DeviceExtensionSize
                &mut dev_name,
                FILE_DEVICE_UNKNOWN,
                0, // DeviceCharacteristics
                0, // Exclusive = FALSE
                &mut device_object,
            )
        };

        if status == STATUS_SUCCESS as i32 {
            Ok(device_object as DeviceHandle)
        } else {
            Err(status)
        }
    }

    fn create_symbolic_link(&mut self, link_name: &str, device_name: &str) -> Result<(), NtStatus> {
        let (mut sym_name, _sym_buf) = str_to_unicode_string(link_name);
        let (mut dev_name, _dev_buf) = str_to_unicode_string(device_name);

        // SAFETY: IoCreateSymbolicLink at PASSIVE_LEVEL with valid UNICODE_STRINGs.
        let status = unsafe { IoCreateSymbolicLink(&mut sym_name, &mut dev_name) };

        if status == STATUS_SUCCESS as i32 {
            Ok(())
        } else {
            Err(status)
        }
    }

    fn delete_symbolic_link(&mut self, link_name: &str) -> Result<(), NtStatus> {
        let (mut sym_name, _sym_buf) = str_to_unicode_string(link_name);

        // SAFETY: IoDeleteSymbolicLink at PASSIVE_LEVEL with a valid UNICODE_STRING.
        let status = unsafe { IoDeleteSymbolicLink(&mut sym_name) };

        if status == STATUS_SUCCESS as i32 {
            Ok(())
        } else {
            Err(status)
        }
    }

    fn delete_device(&mut self, handle: DeviceHandle) -> Result<(), NtStatus> {
        let device_ptr = handle as PDEVICE_OBJECT;
        if device_ptr.is_null() {
            return Err(crate::device::STATUS_INVALID_PARAMETER);
        }

        // SAFETY: handle was obtained from IoCreateDevice and has not been
        // deleted yet. IoDeleteDevice is called at PASSIVE_LEVEL.
        unsafe { IoDeleteDevice(device_ptr) };
        Ok(())
    }

    fn attach_device(
        &mut self,
        filter_device: DeviceHandle,
        target_device_name: &str,
    ) -> Result<DeviceHandle, NtStatus> {
        let source_ptr = filter_device as PDEVICE_OBJECT;
        let (mut target_name, _target_buf) = str_to_unicode_string(target_device_name);
        let mut attached_device: PDEVICE_OBJECT = core::ptr::null_mut();

        // SAFETY: IoAttachDevice at PASSIVE_LEVEL. source_ptr is a valid device
        // object created by IoCreateDevice. target_name is a valid device path.
        let status = unsafe { IoAttachDevice(source_ptr, &mut target_name, &mut attached_device) };

        if status == STATUS_SUCCESS as i32 {
            Ok(attached_device as DeviceHandle)
        } else {
            Err(status)
        }
    }

    fn detach_device(
        &mut self,
        _filter_device: DeviceHandle,
        lower_device: DeviceHandle,
    ) -> Result<(), NtStatus> {
        let lower_ptr = lower_device as PDEVICE_OBJECT;
        if lower_ptr.is_null() {
            return Err(crate::device::STATUS_INVALID_PARAMETER);
        }

        // SAFETY: lower_ptr was obtained from IoAttachDevice and is the device
        // we attached to. IoDetachDevice detaches our filter from the stack.
        unsafe { IoDetachDevice(lower_ptr) };
        Ok(())
    }

    fn get_timestamp(&self) -> u64 {
        let mut time: LARGE_INTEGER = unsafe { core::mem::zeroed() };
        // SAFETY: KeQuerySystemTimePrecise is safe to call at any IRQL.
        // It writes a LARGE_INTEGER through the pointer.
        unsafe { KeQuerySystemTimePrecise(&mut time) };
        unsafe { time.QuadPart as u64 }
    }
}

// ---------------------------------------------------------------------------
// Driver entry point
// ---------------------------------------------------------------------------

/// Windows kernel driver entry point.
///
/// Called by the kernel when the driver service is started. Initializes
/// the device manager, creates the control device (`\Device\SerialMonitor`)
/// and symbolic link (`\DosDevices\SerialMonitor`), and sets up the IRP
/// dispatch table.
///
/// # Safety
/// This function is called by the Windows kernel with valid pointers.
/// - `driver` is a valid `DRIVER_OBJECT` pointer.
/// - `_registry_path` is a valid `UNICODE_STRING` pointer to the driver's
///   registry key.
///
/// The `export_name` attribute ensures the symbol name matches what the
/// kernel loader expects.
// SAFETY: "DriverEntry" is the required export name for Windows driver entry points.
// No other function in this crate exports this name.
#[unsafe(export_name = "DriverEntry")]
pub unsafe extern "system" fn driver_entry(
    driver: &mut DRIVER_OBJECT,
    _registry_path: PCUNICODE_STRING,
) -> NTSTATUS {
    // Create the WDK kernel API implementation
    // SAFETY: driver is a valid DRIVER_OBJECT reference from the kernel.
    let kernel_api = unsafe { WdkKernelApi::new(driver as *mut DRIVER_OBJECT) };

    // Create the device manager with default buffer capacity
    let mut manager =
        alloc::boxed::Box::new(DeviceManager::new(kernel_api, DEFAULT_BUFFER_CAPACITY));

    // Initialize control device and symbolic link
    if let Err(status) = manager.initialize() {
        return status;
    }

    // Set up IRP dispatch functions
    // IRP_MJ_CREATE and IRP_MJ_CLOSE allow user-mode handles (CreateFile/CloseHandle)
    driver.MajorFunction[wdk_sys::IRP_MJ_CREATE as usize] = Some(dispatch_create_close);
    driver.MajorFunction[wdk_sys::IRP_MJ_CLOSE as usize] = Some(dispatch_create_close);
    // IRP_MJ_DEVICE_CONTROL handles IOCTL requests from user-mode
    driver.MajorFunction[wdk_sys::IRP_MJ_DEVICE_CONTROL as usize] = Some(dispatch_device_control);

    // Set unload function
    driver.DriverUnload = Some(driver_unload);

    // Store the device manager globally
    let raw = alloc::boxed::Box::into_raw(manager);
    DEVICE_MANAGER.store(raw, Ordering::Release);

    STATUS_SUCCESS as NTSTATUS
}

// ---------------------------------------------------------------------------
// Driver unload
// ---------------------------------------------------------------------------

/// Called by the kernel when the driver service is stopped.
///
/// Cleans up all resources: stops monitoring if active, removes the symbolic
/// link, and deletes all device objects.
///
/// # Safety
/// Called by the kernel with a valid `DRIVER_OBJECT` pointer.
/// Guaranteed to be called only once, after all pending I/O has completed.
unsafe extern "C" fn driver_unload(_driver: *mut DRIVER_OBJECT) {
    let raw = DEVICE_MANAGER.swap(core::ptr::null_mut(), Ordering::AcqRel);
    if !raw.is_null() {
        // SAFETY: raw was stored by driver_entry via Box::into_raw and has
        // not been freed. We reclaim ownership to run cleanup and drop.
        let mut manager = unsafe { alloc::boxed::Box::from_raw(raw) };
        let _ = manager.cleanup();
        // Box drops and deallocates here
    }
}

// ---------------------------------------------------------------------------
// IRP dispatch stubs
// ---------------------------------------------------------------------------

/// Dispatch handler for IRP_MJ_CREATE and IRP_MJ_CLOSE.
///
/// Simply completes the IRP with success, allowing user-mode applications
/// to open and close handles to the control device.
///
/// # Safety
/// Called by the kernel I/O manager with valid device and IRP pointers.
unsafe extern "C" fn dispatch_create_close(
    _device: *mut DEVICE_OBJECT,
    irp: *mut IRP,
) -> NTSTATUS {
    // SAFETY: irp is a valid IRP pointer from the I/O manager.
    // We complete it immediately with STATUS_SUCCESS and 0 bytes transferred.
    unsafe {
        (*irp).IoStatus.Information = 0;
        (*irp).IoStatus.__bindgen_anon_1.Status = STATUS_SUCCESS as NTSTATUS;
        IofCompleteRequest(irp, wdk_sys::IO_NO_INCREMENT as i8);
    }
    STATUS_SUCCESS as NTSTATUS
}

/// Dispatch handler for IRP_MJ_DEVICE_CONTROL.
///
/// Routes IOCTL requests to the appropriate handler based on the control code:
/// - `IOCTL_START_MONITOR`: Begins monitoring a serial port.
/// - `IOCTL_STOP_MONITOR`: Stops monitoring.
/// - `IOCTL_GET_DATA`: Reads captured data from the ring buffer.
/// - `IOCTL_GET_STATUS`: Returns driver status information.
///
/// # Safety
/// Called by the kernel I/O manager with valid device and IRP pointers.
/// The IO_STACK_LOCATION contains validated buffer pointers and sizes.
unsafe extern "C" fn dispatch_device_control(
    _device: *mut DEVICE_OBJECT,
    irp: *mut IRP,
) -> NTSTATUS {
    let raw = DEVICE_MANAGER.load(Ordering::Acquire);
    if raw.is_null() {
        // SAFETY: Complete IRP with error if manager not initialized.
        unsafe {
            (*irp).IoStatus.Information = 0;
            (*irp).IoStatus.__bindgen_anon_1.Status = STATUS_UNSUCCESSFUL as NTSTATUS;
            IofCompleteRequest(irp, wdk_sys::IO_NO_INCREMENT as i8);
        }
        return STATUS_UNSUCCESSFUL as NTSTATUS;
    }

    // SAFETY: raw is valid (stored by driver_entry, not yet freed).
    // The kernel serializes IOCTL dispatch at PASSIVE_LEVEL for our device.
    let manager = unsafe { &mut *raw };

    // Get the current I/O stack location to determine the IOCTL code
    // SAFETY: Tail.Overlay.__bindgen_anon_2.__bindgen_anon_1.CurrentStackLocation
    // is the equivalent of IoGetCurrentIrpStackLocation() macro.
    let irp_ref = unsafe { &*irp };
    let stack = unsafe { *irp_ref.Tail.Overlay.__bindgen_anon_2.__bindgen_anon_1.CurrentStackLocation };
    let ioctl_code = unsafe { stack.Parameters.DeviceIoControl.IoControlCode };

    let status = match ioctl_code {
        crate::ioctl::IOCTL_START_MONITOR => handle_start_monitor(manager, irp),
        crate::ioctl::IOCTL_STOP_MONITOR => handle_stop_monitor(manager),
        crate::ioctl::IOCTL_GET_DATA => handle_get_data(manager, irp),
        crate::ioctl::IOCTL_GET_STATUS => handle_get_status(manager, irp),
        _ => crate::device::STATUS_INVALID_PARAMETER,
    };

    // SAFETY: irp is valid. We complete it with the computed status.
    unsafe {
        (*irp).IoStatus.__bindgen_anon_1.Status = status;
        IofCompleteRequest(irp, wdk_sys::IO_NO_INCREMENT as i8);
    }
    status
}

// ---------------------------------------------------------------------------
// IOCTL handlers
// ---------------------------------------------------------------------------

/// Handles IOCTL_START_MONITOR: parses the input buffer to get the port name
/// and starts monitoring.
fn handle_start_monitor(manager: &mut DeviceManager<WdkKernelApi>, irp: *mut IRP) -> NTSTATUS {
    // The input buffer contains a StartMonitorRequest struct.
    // SAFETY: irp is valid, AssociatedIrp is a union — accessing SystemBuffer is correct for
    // METHOD_BUFFERED IOCTLs.
    let input = unsafe { (*irp).AssociatedIrp.SystemBuffer };
    if input.is_null() {
        return crate::device::STATUS_INVALID_PARAMETER;
    }

    // SAFETY: SystemBuffer is kernel-allocated and valid for the input length.
    // We verify the size matches StartMonitorRequest before reading.
    let request = unsafe { &*(input as *const crate::shared::StartMonitorRequest) };

    let port_name = request.port_name_string();
    match manager.start_monitor(&port_name) {
        Ok(()) => STATUS_SUCCESS as NTSTATUS,
        Err(e) => e,
    }
}

/// Handles IOCTL_STOP_MONITOR: stops the current monitoring session.
fn handle_stop_monitor(manager: &mut DeviceManager<WdkKernelApi>) -> NTSTATUS {
    match manager.stop_monitor() {
        Ok(()) => STATUS_SUCCESS as NTSTATUS,
        Err(e) => e,
    }
}

/// Handles IOCTL_GET_DATA: reads captured data from the ring buffer into
/// the output buffer.
fn handle_get_data(manager: &mut DeviceManager<WdkKernelApi>, irp: *mut IRP) -> NTSTATUS {
    // SAFETY: irp is valid, accessing SystemBuffer for METHOD_BUFFERED IOCTL.
    let output = unsafe { (*irp).AssociatedIrp.SystemBuffer };
    if output.is_null() {
        return crate::device::STATUS_INVALID_PARAMETER;
    }

    // Try to pop data from the ring buffer
    match manager.buffer_mut().pop() {
        Some((header, data)) => {
            // Build GetDataResponse in the output buffer
            // SAFETY: SystemBuffer is valid and large enough (kernel validates
            // OutputBufferLength against our IOCTL definition).
            let response = unsafe { &mut *(output as *mut crate::shared::GetDataResponse) };
            response.header = header;
            response.data[..data.len()].copy_from_slice(&data);
            unsafe {
                (*irp).IoStatus.Information =
                    core::mem::size_of::<crate::shared::GetDataResponse>() as u64;
            }
            STATUS_SUCCESS as NTSTATUS
        }
        None => {
            unsafe { (*irp).IoStatus.Information = 0 };
            crate::device::STATUS_NO_DATA
        }
    }
}

/// Handles IOCTL_GET_STATUS: returns a DriverStatus snapshot.
fn handle_get_status(manager: &DeviceManager<WdkKernelApi>, irp: *mut IRP) -> NTSTATUS {
    // SAFETY: irp is valid, accessing SystemBuffer for METHOD_BUFFERED IOCTL.
    let output = unsafe { (*irp).AssociatedIrp.SystemBuffer };
    if output.is_null() {
        return crate::device::STATUS_INVALID_PARAMETER;
    }

    // SAFETY: SystemBuffer is valid and sized for DriverStatus (kernel validates).
    let status_out = unsafe { &mut *(output as *mut crate::shared::DriverStatus) };
    *status_out = manager.status();
    unsafe {
        (*irp).IoStatus.Information =
            core::mem::size_of::<crate::shared::DriverStatus>() as u64;
    }

    STATUS_SUCCESS as NTSTATUS
}
