//! Build script for the serial-monitor kernel driver.
//!
//! In a full WDK integration, this script would:
//! 1. Locate the Windows Driver Kit installation
//! 2. Set up include paths for kernel headers
//! 3. Configure the linker for kernel-mode output (.sys)
//! 4. Handle driver signing for test/release builds
//!
//! For now, this serves as a placeholder. The actual WDK integration
//! will use the `windows-drivers-rs` crate when available.

fn main() {
    // When building for kernel mode, additional WDK configuration is needed.
    // This is a placeholder for future WDK integration.
    //
    // TODO: Add WDK integration when ready:
    // - windows_kernel_rs or equivalent crate
    // - Kernel header include paths
    // - .sys output configuration
    // - Test signing setup

    println!("cargo:rerun-if-changed=build.rs");
}
