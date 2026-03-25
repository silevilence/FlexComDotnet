//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

let driver_binary_extension = std::env::var("WDK_BUILD_DRIVER_EXTENSION").expect("WDK_BUILD_DRIVER_EXTENSION should be set by cargo-make");
wdk_build::cargo_make::copy_to_driver_package_folder(
    wdk_build::cargo_make::get_wdk_build_output_directory().join(format!(
        "{}.{driver_binary_extension}",
        wdk_build::cargo_make::get_current_package_name()
    )),
)?
