//! Build script for the serial-monitor kernel driver.
//!
//! When the `kernel` feature is enabled, this script delegates to
//! `wdk_build::configure_wdk_binary_build()` which:
//! 1. Locates the Windows Driver Kit (WDK/eWDK) installation
//! 2. Configures include paths for kernel headers (ntddk.h, wdm.h, etc.)
//! 3. Sets linker flags for kernel-mode output (.sys)
//! 4. Handles test-signing configuration
//!
//! Without the `kernel` feature, this is a no-op, allowing `cargo test`
//! to run user-mode tests without WDK installed.

fn main() {
    // Only configure WDK build when targeting kernel mode.
    // This requires:
    //   - WDK or eWDK installed
    //   - Running from an eWDK developer prompt (or equivalent env)
    //   - `cargo make` for post-build driver packaging
    //
    // Build command: cargo make --features kernel
    // Test command:  cargo test  (no WDK required)
    #[cfg(feature = "kernel")]
    wdk_build::configure_wdk_binary_build()
        .expect("Failed to configure WDK build. Ensure WDK/eWDK is installed and you are in a developer prompt.");

    println!("cargo:rerun-if-changed=build.rs");
}
