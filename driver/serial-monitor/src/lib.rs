/// Serial Monitor - Windows Kernel-Mode Serial Port Filter Driver
///
/// This driver attaches to serial port device stacks to transparently
/// monitor TX/RX data without affecting original communication.
///
/// # Architecture
/// - `shared`: Shared data structures between kernel and user-mode (#[repr(C)])
/// - `ioctl`: IOCTL code definitions and helpers
/// - `ring_buffer`: Lock-free ring buffer for captured data storage
/// - `device`: Device object creation and management
/// - `filter`: Filter driver IRP interception logic
pub mod shared;
pub mod ioctl;
pub mod ring_buffer;
pub mod device;
pub mod filter;
