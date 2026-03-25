// In kernel mode, no std library is available.
#![cfg_attr(feature = "kernel", no_std)]

//! Serial Monitor - Windows Kernel-Mode Serial Port Filter Driver
//!
//! This driver attaches to serial port device stacks to transparently
//! monitor TX/RX data without affecting original communication.
//!
//! # Architecture
//! - `shared`: Shared data structures between kernel and user-mode (#[repr(C)])
//! - `ioctl`: IOCTL code definitions and helpers
//! - `ring_buffer`: Lock-free ring buffer for captured data storage
//! - `device`: Device object creation and management
//! - `filter`: Filter driver IRP interception logic
//! - `entry` (kernel only): DriverEntry, unload, and WDK KernelApi implementation
//!
//! # Build Modes
//! - **User-mode tests** (`cargo test`): All logic tested via `KernelApi` trait mocks.
//! - **Kernel driver** (`cargo make --features kernel`): Compiles to `.sys` with WDK.

// alloc crate provides String, Vec, format!() for kernel (no_std) builds.
#[cfg(feature = "kernel")]
extern crate alloc;

// Panic handler for kernel mode (no_std).
#[cfg(all(feature = "kernel", not(test)))]
extern crate wdk_panic;

// Global allocator for kernel mode heap allocations.
#[cfg(all(feature = "kernel", not(test)))]
use wdk_alloc::WdkAllocator;

#[cfg(all(feature = "kernel", not(test)))]
#[global_allocator]
static GLOBAL_ALLOCATOR: WdkAllocator = WdkAllocator;

pub mod device;
pub mod filter;
pub mod ioctl;
pub mod ring_buffer;
pub mod shared;

/// Kernel-mode driver entry point and WDK API implementation.
/// Only compiled when targeting kernel mode.
#[cfg(feature = "kernel")]
pub mod entry;
