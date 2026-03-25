#!@rust

//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

wdk_build::cargo_make::condition_script(|| {
    let driver_type = std::env::var("WDK_BUILD_METADATA-DRIVER_MODEL-DRIVER_TYPE")
        .expect("WDK_BUILD_METADATA-DRIVER_MODEL-DRIVER_TYPE should be set by setup-wdk-config-env-vars cargo-make task");

    match driver_type.as_str()  {
        "WDM" | "KMDF" => Ok(()),
        _ => Err("Non-Kernel Mode Driver detected. Skipping generate-driver-binary-file task."),
    }
})?
