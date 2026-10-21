//! WDM adapter. Capture failures never fail or modify a serial request.
//! Session state is shared with host tests; WDK lifetimes remain here.
use crate::monitor::{self, CaptureToken, EventInfo, MonitorCore};
use core::{
    cell::UnsafeCell,
    ffi::c_void,
    mem::{size_of, size_of_val},
    ptr,
    sync::atomic::{AtomicPtr, AtomicU64, Ordering},
};
use wdk_sys::{ntddk::*, *};

const TAG: u32 = u32::from_le_bytes(*b"SmC2");
const POOL_NON_PAGED: u64 = 0x40;
const REMOVE: u8 = 2;
const REMOVE_LOCK_SIZE: u32 = size_of::<IO_REMOVE_LOCK>() as u32;
const PNP: u32 = 0x1b;
const CONFIG_MAX: usize = 32;
struct State {
    lock: UnsafeCell<KSPIN_LOCK>,
    core: UnsafeCell<MonitorCore>,
    diagnostics: UnsafeCell<crate::shared::DriverDiagnostics>,
}
static STATE: AtomicPtr<State> = AtomicPtr::new(ptr::null_mut());
static CONTROL: AtomicPtr<DEVICE_OBJECT> = AtomicPtr::new(ptr::null_mut());
static NEXT_PORT_ID: AtomicU64 = AtomicU64::new(1);
#[repr(C)]
struct Extension {
    lower: PDEVICE_OBJECT,
    remove_lock: IO_REMOVE_LOCK,
    io_flags: u32,
    port_id: u64,
}
struct CompletionContext {
    token: Option<CaptureToken>,
    extension: *mut Extension,
    major: u8,
    requested: u32,
    ioctl: u32,
    config_len: usize,
    config: [u8; CONFIG_MAX],
}
// Implementation behind the WDK IoCreateDeviceSecure macro.
#[link(name = "Wdmsec", kind = "static")]
unsafe extern "system" {
    fn WdmlibIoCreateDeviceSecure(
        driver: PDRIVER_OBJECT,
        extension_size: u32,
        name: *const UNICODE_STRING,
        device_type: u32,
        characteristics: u32,
        exclusive: u8,
        sddl: *const UNICODE_STRING,
        class_guid: *const GUID,
        device: *mut PDEVICE_OBJECT,
    ) -> NTSTATUS;
}
fn unicode(bytes: &mut [u16]) -> UNICODE_STRING {
    UNICODE_STRING {
        Length: (bytes.len() * 2) as u16,
        MaximumLength: (bytes.len() * 2) as u16,
        Buffer: bytes.as_mut_ptr(),
    }
}
fn literal_unicode(text: &str, storage: &mut [u16; 64]) -> UNICODE_STRING {
    // All callers use fixed driver names/SDDL shorter than 64 characters.
    let mut length = 0;
    for (slot, character) in storage.iter_mut().zip(text.encode_utf16()) {
        *slot = character;
        length += 1;
    }
    unicode(&mut storage[..length])
}
/// Runs at <= DISPATCH_LEVEL while a device/IRP keeps the driver alive.
/// Closures access nonpaged memory only, never allocate or call down.
fn with_state<R>(
    f: impl FnOnce(&mut MonitorCore, &mut crate::shared::DriverDiagnostics) -> R,
) -> Option<R> {
    let state = STATE.load(Ordering::Acquire);
    if state.is_null() {
        return None;
    }
    // SAFETY: state lives until unload; mutable references are created only under the lock.
    unsafe {
        let lock = (*state).lock.get();
        let irql = KeAcquireSpinLockRaiseToDpc(lock);
        struct Guard(*mut KSPIN_LOCK, KIRQL);
        impl Drop for Guard {
            fn drop(&mut self) {
                unsafe { KeReleaseSpinLock(self.0, self.1) };
            }
        }
        let _guard = Guard(lock, irql);
        Some(f(
            &mut *(*state).core.get(),
            &mut *(*state).diagnostics.get(),
        ))
    }
}
/// # Safety
/// Invoked only by the Windows loader with a valid DRIVER_OBJECT.
#[unsafe(export_name = "DriverEntry")]
pub unsafe extern "system" fn driver_entry(
    driver: &mut DRIVER_OBJECT,
    _registry: PCUNICODE_STRING,
) -> NTSTATUS {
    let core = match MonitorCore::new(32) {
        Ok(core) => core,
        Err(status) => return status,
    };
    // SAFETY: checked nonpaged allocation at PASSIVE_LEVEL, initialized before publication.
    let state =
        unsafe { ExAllocatePool2(POOL_NON_PAGED, size_of::<State>() as u64, TAG) as *mut State };
    if state.is_null() {
        return monitor::STATUS_NO_MEMORY;
    }
    unsafe {
        state.write(State {
            lock: UnsafeCell::new(0),
            core: UnsafeCell::new(core),
            diagnostics: UnsafeCell::new(Default::default()),
        });
    }
    let mut name_buffer = [0; 64];
    let mut name = literal_unicode(crate::ioctl::DEVICE_NAME, &mut name_buffer);
    let mut sddl_buffer = [0; 64];
    let sddl = literal_unicode("D:P(A;;GA;;;SY)(A;;GA;;;BA)", &mut sddl_buffer);
    let class = GUID {
        Data1: 0x5c68d998,
        Data2: 0x124e,
        Data3: 0x4fab,
        Data4: [0xa8, 0xc3, 0x19, 0x96, 0x11, 0x76, 0x51, 0xa2],
    };
    let mut control = ptr::null_mut();
    let status = unsafe {
        WdmlibIoCreateDeviceSecure(
            driver,
            0,
            &name,
            crate::ioctl::FILE_DEVICE_SERIAL_MONITOR,
            FILE_DEVICE_SECURE_OPEN,
            0,
            &sddl,
            &class,
            &mut control,
        )
    };
    if status < 0 {
        unsafe {
            ptr::drop_in_place(state);
            ExFreePoolWithTag(state.cast(), TAG);
        }
        return status;
    }
    let mut link_buffer = [0; 64];
    let mut link = literal_unicode(crate::ioctl::SYMLINK_NAME, &mut link_buffer);
    let status = unsafe { IoCreateSymbolicLink(&mut link, &mut name) };
    if status < 0 {
        unsafe {
            IoDeleteDevice(control);
            ptr::drop_in_place(state);
            ExFreePoolWithTag(state.cast(), TAG);
        }
        return status;
    }
    driver.MajorFunction.fill(Some(dispatch_irp));
    driver.DriverUnload = Some(unload);
    unsafe {
        (*driver.DriverExtension).AddDevice = Some(add_device);
    }
    STATE.store(state, Ordering::Release);
    CONTROL.store(control, Ordering::Release);
    unsafe {
        (*control).Flags &= !DO_DEVICE_INITIALIZING;
    }
    0
}
unsafe extern "C" fn unload(_driver: PDRIVER_OBJECT) {
    // SAFETY: PnP removed filters and the I/O manager released open handles.
    let mut link_buffer = [0; 64];
    let mut link = literal_unicode(crate::ioctl::SYMLINK_NAME, &mut link_buffer);
    unsafe {
        let _ = IoDeleteSymbolicLink(&mut link);
    }
    let device = CONTROL.swap(ptr::null_mut(), Ordering::AcqRel);
    if !device.is_null() {
        unsafe {
            IoDeleteDevice(device);
        }
    }
    let state = STATE.swap(ptr::null_mut(), Ordering::AcqRel);
    if !state.is_null() {
        unsafe {
            ptr::drop_in_place(state);
            ExFreePoolWithTag(state.cast(), TAG);
        }
    }
}
unsafe extern "C" fn add_device(driver: PDRIVER_OBJECT, pdo: PDEVICE_OBJECT) -> NTSTATUS {
    let mut name = [0u16; 64];
    let mut returned = 0;
    // SAFETY: PnP supplies a live PDO at PASSIVE_LEVEL. No COM open is performed.
    let named = unsafe {
        IoGetDeviceProperty(
            pdo,
            DEVICE_REGISTRY_PROPERTY::DevicePropertyPhysicalDeviceObjectName,
            size_of_val(&name) as u32,
            name.as_mut_ptr().cast(),
            &mut returned,
        )
    } >= 0;
    let mut device = ptr::null_mut();
    let status = unsafe {
        IoCreateDevice(
            driver,
            size_of::<Extension>() as u32,
            ptr::null_mut(),
            FILE_DEVICE_UNKNOWN,
            0,
            0,
            &mut device,
        )
    };
    if status < 0 {
        return status;
    }
    let ext = unsafe { (*device).DeviceExtension as *mut Extension };
    unsafe {
        (*ext).port_id = NEXT_PORT_ID.fetch_add(1, Ordering::Relaxed);
    }
    unsafe {
        IoInitializeRemoveLockEx(
            ptr::addr_of_mut!((*ext).remove_lock),
            TAG,
            0,
            0,
            REMOVE_LOCK_SIZE,
        );
    }
    // Safe attachment publishes the lower pointer atomically with insertion.
    let status =
        unsafe { IoAttachDeviceToDeviceStackSafe(device, pdo, ptr::addr_of_mut!((*ext).lower)) };
    if status < 0 {
        unsafe {
            IoDeleteDevice(device);
        }
        return status;
    }
    unsafe {
        (*ext).io_flags = (*(*ext).lower).Flags & (DO_BUFFERED_IO | DO_DIRECT_IO);
        (*device).Flags |=
            (*(*ext).lower).Flags & (DO_BUFFERED_IO | DO_DIRECT_IO | DO_POWER_PAGABLE);
        (*device).DeviceType = (*(*ext).lower).DeviceType;
        (*device).Characteristics = (*(*ext).lower).Characteristics;
        (*device).AlignmentRequirement = (*(*ext).lower).AlignmentRequirement;
    }
    if named {
        with_state(|core, _| {
            let _ = core.register_port(unsafe { (*ext).port_id }, name);
        });
    }
    unsafe {
        (*device).Flags &= !DO_DEVICE_INITIALIZING;
    }
    0
}
unsafe fn stack(irp: *mut IRP) -> *mut IO_STACK_LOCATION {
    unsafe {
        (*irp)
            .Tail
            .Overlay
            .__bindgen_anon_2
            .__bindgen_anon_1
            .CurrentStackLocation
    }
}
unsafe fn skip(irp: *mut IRP) {
    unsafe {
        (*irp).CurrentLocation += 1;
        (*irp)
            .Tail
            .Overlay
            .__bindgen_anon_2
            .__bindgen_anon_1
            .CurrentStackLocation = stack(irp).add(1);
    }
}
unsafe fn copy_to_next(irp: *mut IRP) {
    // SAFETY: the attached device owns a stack slot. Equivalent to the WDK macro.
    unsafe {
        let current = stack(irp);
        let next = current.sub(1);
        ptr::copy_nonoverlapping(
            current.cast::<u8>(),
            next.cast::<u8>(),
            core::mem::offset_of!(IO_STACK_LOCATION, CompletionRoutine),
        );
        (*next).Control = 0;
    }
}
unsafe fn finish(irp: *mut IRP, status: i32, bytes: usize) -> i32 {
    unsafe {
        (*irp).IoStatus.__bindgen_anon_1.Status = status;
        (*irp).IoStatus.Information = bytes as u64;
        IofCompleteRequest(irp, IO_NO_INCREMENT as i8);
    }
    status
}
unsafe extern "C" fn dispatch_irp(device: PDEVICE_OBJECT, irp: *mut IRP) -> NTSTATUS {
    // SAFETY: I/O manager supplies live device and IRP pointers throughout dispatch.
    let current = unsafe { stack(irp) };
    let major = unsafe { (*current).MajorFunction };
    if device == CONTROL.load(Ordering::Acquire) {
        return unsafe { control_irp(irp, major) };
    }
    let ext = unsafe { (*device).DeviceExtension as *mut Extension };
    let lock = unsafe { ptr::addr_of_mut!((*ext).remove_lock) };
    let status = unsafe {
        IoAcquireRemoveLockEx(
            lock,
            irp.cast(),
            c"serial-monitor".as_ptr().cast(),
            line!(),
            REMOVE_LOCK_SIZE,
        )
    };
    if status < 0 {
        return unsafe { finish(irp, status, 0) };
    }
    let lower = unsafe { (*ext).lower };
    if u32::from(major) == PNP && unsafe { (*current).MinorFunction } == REMOVE {
        with_state(|core, _| core.unregister_port(unsafe { (*ext).port_id }));
        unsafe {
            skip(irp);
        }
        let result = unsafe { IofCallDriver(lower, irp) };
        // REMOVE is PASSIVE_LEVEL. Completion contexts retain their remove locks.
        unsafe {
            IoReleaseRemoveLockAndWaitEx(lock, irp.cast(), REMOVE_LOCK_SIZE);
            IoDetachDevice(lower);
            IoDeleteDevice(device);
        }
        return result;
    }
    let capture = matches!(
        u32::from(major),
        IRP_MJ_READ | IRP_MJ_WRITE | IRP_MJ_DEVICE_CONTROL
    );
    let token = if capture {
        with_state(|core, _| core.begin(unsafe { (*ext).port_id })).flatten()
    } else {
        None
    };
    let observe_create = u32::from(major) == IRP_MJ_CREATE;
    // Observe completions even while idle so a read already pending when the
    // user starts monitoring can be captured without reopening the COM handle.
    if capture || observe_create {
        let context = unsafe {
            ExAllocatePool2(POOL_NON_PAGED, size_of::<CompletionContext>() as u64, TAG)
                as *mut CompletionContext
        };
        if !context.is_null() {
            let mut requested = 0;
            let mut ioctl = 0;
            if u32::from(major) == IRP_MJ_READ {
                requested = unsafe { (*current).Parameters.Read.Length };
            }
            if u32::from(major) == IRP_MJ_WRITE {
                requested = unsafe { (*current).Parameters.Write.Length };
            }
            if u32::from(major) == IRP_MJ_DEVICE_CONTROL {
                requested = unsafe { (*current).Parameters.DeviceIoControl.InputBufferLength };
                ioctl = unsafe { (*current).Parameters.DeviceIoControl.IoControlCode };
            }
            unsafe {
                context.write(CompletionContext {
                    token,
                    extension: ext,
                    major,
                    requested,
                    ioctl,
                    config_len: 0,
                    config: [0; CONFIG_MAX],
                });
            }
            // Snapshot supported serial SET_* input before buffered IOCTL completion.
            if crate::filter::is_monitored_serial_ioctl(ioctl) && requested as usize <= CONFIG_MAX {
                let buffer = unsafe { (*irp).AssociatedIrp.SystemBuffer as *const u8 };
                if !buffer.is_null() {
                    unsafe {
                        ptr::copy_nonoverlapping(
                            buffer,
                            (*context).config.as_mut_ptr(),
                            requested as usize,
                        );
                        (*context).config_len = requested as usize;
                    }
                }
            }
            unsafe {
                copy_to_next(irp);
            }
            let installed = unsafe {
                IoSetCompletionRoutineEx(device, irp, Some(completion), context.cast(), 1, 1, 1)
            };
            if installed >= 0 {
                return unsafe { IofCallDriver(lower, irp) };
            }
            unsafe {
                ExFreePoolWithTag(context.cast(), TAG);
            }
        }
        // Capture allocation/registration failures do not change third-party I/O.
        if let Some(token) = token {
            with_state(|core, _| core.lost(token, 0));
        }
    }
    unsafe {
        skip(irp);
    }
    let result = unsafe { IofCallDriver(lower, irp) };
    unsafe {
        IoReleaseRemoveLockEx(lock, irp.cast(), REMOVE_LOCK_SIZE);
    }
    result
}
unsafe fn data_buffer<'a>(irp: *mut IRP, ext: *mut Extension, length: usize) -> Option<&'a [u8]> {
    // SAFETY: returned slice is used only while completion owns the live IRP.
    let flags = unsafe { (*ext).io_flags };
    if flags & DO_BUFFERED_IO != 0 {
        let buffer = unsafe { (*irp).AssociatedIrp.SystemBuffer as *const u8 };
        if !buffer.is_null() {
            return Some(unsafe { core::slice::from_raw_parts(buffer, length) });
        }
    } else if flags & DO_DIRECT_IO != 0 {
        let mdl = unsafe { (*irp).MdlAddress };
        if !mdl.is_null() && unsafe { (*mdl).ByteCount as usize } >= length {
            let mapped = if unsafe { (*mdl).MdlFlags as u32 }
                & (MDL_MAPPED_TO_SYSTEM_VA | MDL_SOURCE_IS_NONPAGED_POOL)
                != 0
            {
                unsafe { (*mdl).MappedSystemVa }
            } else {
                unsafe {
                    MmMapLockedPagesSpecifyCache(
                        mdl,
                        0,
                        _MEMORY_CACHING_TYPE::MmCached,
                        ptr::null_mut(),
                        0,
                        _MM_PAGE_PRIORITY::NormalPagePriority as u32 | MdlMappingNoExecute,
                    )
                }
            };
            if !mapped.is_null() {
                return Some(unsafe { core::slice::from_raw_parts(mapped.cast::<u8>(), length) });
            }
        }
    }
    // Never dereference neither-I/O user pointers at arbitrary completion IRQL.
    None
}
unsafe extern "C" fn completion(
    _device: PDEVICE_OBJECT,
    irp: *mut IRP,
    opaque: *mut c_void,
) -> NTSTATUS {
    // SAFETY: context is nonpaged, and its remove lock pins the device extension.
    let context = unsafe { &*(opaque as *const CompletionContext) };
    let status = unsafe { (*irp).IoStatus.__bindgen_anon_1.Status };
    if u32::from(context.major) == IRP_MJ_CREATE {
        with_state(|_, diag| {
            diag.filter_create_count = diag.filter_create_count.saturating_add(1);
            diag.last_filter_create_status = status;
        });
    }
    if u32::from(context.major) != IRP_MJ_CREATE {
        let port_id = unsafe { (*context.extension).port_id };
        let mut time: LARGE_INTEGER = unsafe { core::mem::zeroed() };
        unsafe {
            KeQuerySystemTimePrecise(&mut time);
        }
        let timestamp = unsafe { time.QuadPart as u64 };
        if matches!(u32::from(context.major), IRP_MJ_READ | IRP_MJ_WRITE) {
            let actual = if status >= 0 {
                unsafe { (*irp).IoStatus.Information }.min(u64::from(context.requested)) as u32
            } else {
                0
            };
            let data = unsafe { data_buffer(irp, context.extension, actual as usize) };
            let info = EventInfo::data(
                if u32::from(context.major) == IRP_MJ_WRITE {
                    0
                } else {
                    1
                },
                context.requested,
                actual,
                status,
                timestamp,
            );
            with_state(|core, _| core.observe_completion(context.token, port_id, info, data));
        } else {
            let info = EventInfo {
                flags: 0,
                direction: 2,
                kind: 1,
                requested: context.requested,
                transferred: 0,
                status,
                timestamp,
                ioctl: context.ioctl,
            };
            with_state(|core, _| {
                core.observe_completion(
                    context.token,
                    port_id,
                    info,
                    Some(&context.config[..context.config_len]),
                )
            });
        }
    }
    // IoMarkIrpPending equivalent: preserve asynchronous completion semantics.
    if unsafe { (*irp).PendingReturned } != 0 {
        unsafe {
            (*stack(irp)).Control |= SL_PENDING_RETURNED as u8;
        }
    }
    let lock = unsafe { ptr::addr_of_mut!((*context.extension).remove_lock) };
    unsafe {
        ExFreePoolWithTag(opaque, TAG);
        IoReleaseRemoveLockEx(lock, irp.cast(), REMOVE_LOCK_SIZE);
    }
    0
}
unsafe fn control_irp(irp: *mut IRP, major: u8) -> NTSTATUS {
    // SAFETY: METHOD_BUFFERED memory is touched only after validating both lengths.
    let current = unsafe { stack(irp) };
    let owner = unsafe { (*current).FileObject as u64 };
    if matches!(
        u32::from(major),
        IRP_MJ_CREATE | IRP_MJ_CLOSE | IRP_MJ_CLEANUP
    ) {
        if u32::from(major) != IRP_MJ_CREATE {
            with_state(|core, _| core.cleanup(owner));
        }
        return unsafe { finish(irp, 0, 0) };
    }
    if u32::from(major) != IRP_MJ_DEVICE_CONTROL {
        return unsafe { finish(irp, monitor::STATUS_INVALID_PARAMETER, 0) };
    }
    let params = unsafe { (*current).Parameters.DeviceIoControl };
    if let Err(status) = monitor::validate_ioctl(
        params.IoControlCode,
        params.InputBufferLength as usize,
        params.OutputBufferLength as usize,
    ) {
        return unsafe { finish(irp, status, 0) };
    }
    let buffer = unsafe { (*irp).AssociatedIrp.SystemBuffer as *mut u8 };
    if buffer.is_null() && params.IoControlCode != monitor::IOCTL_STOP {
        return unsafe { finish(irp, monitor::STATUS_INVALID_PARAMETER, 0) };
    }
    let result = with_state(|core, diag| -> Result<usize, i32> {
        match params.IoControlCode {
            monitor::IOCTL_START => {
                let mut name = [0u16; 64];
                for (i, ch) in name.iter_mut().enumerate() {
                    *ch = unsafe { ptr::read_unaligned(buffer.add(i * 2).cast::<u16>()) };
                }
                let result = core.start(owner, &name);
                diag.last_start_status = result.as_ref().err().copied().unwrap_or(0);
                diag.last_start_stage = if result.is_ok() { 5 } else { 2 };
                result.map(|_| 0)
            }
            monitor::IOCTL_STOP => core.stop(owner).map(|_| 0),
            monitor::IOCTL_DATA => core.read(owner, unsafe {
                core::slice::from_raw_parts_mut(buffer, monitor::RECORD_SIZE)
            }),
            monitor::IOCTL_STATUS => {
                core.status().encode(unsafe {
                    core::slice::from_raw_parts_mut(buffer, monitor::STATUS_SIZE)
                });
                Ok(monitor::STATUS_SIZE)
            }
            monitor::IOCTL_DIAGNOSTICS => {
                diag.pnp_filter_count = core.status().attached_ports;
                for (i, value) in [
                    diag.last_start_stage,
                    diag.last_start_status as u32,
                    diag.pnp_filter_count,
                    diag.filter_create_count,
                    diag.last_filter_create_status as u32,
                ]
                .iter()
                .enumerate()
                {
                    unsafe {
                        ptr::write_unaligned(buffer.add(i * 4).cast::<u32>(), *value);
                    }
                }
                Ok(20)
            }
            _ => Err(monitor::STATUS_INVALID_PARAMETER),
        }
    })
    .unwrap_or(Err(monitor::STATUS_NOT_FOUND));
    match result {
        Ok(bytes) => unsafe { finish(irp, 0, bytes) },
        Err(status) => unsafe { finish(irp, status, 0) },
    }
}
