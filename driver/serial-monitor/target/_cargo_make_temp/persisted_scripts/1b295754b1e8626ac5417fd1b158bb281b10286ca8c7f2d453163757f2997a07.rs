//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

wdk_build::cargo_make::copy_to_driver_package_folder(
    wdk_build::cargo_make::get_wdk_build_output_directory().join("WDRLocalTestCert.cer"),
)?
