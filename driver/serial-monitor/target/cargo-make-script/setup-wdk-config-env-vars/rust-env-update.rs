//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

let serialized_wdk_metadata_map = wdk_build::metadata::to_map_with_prefix::<std::collections::BTreeMap<_, _>>(
    "WDK_BUILD_METADATA",
    &wdk_build::metadata::Wdk::try_from(&wdk_build::cargo_make::get_cargo_metadata()?)?,
)?;

#[cfg(not(target_os = "windows"))]
compile_error!(
    "windows-drivers-rs is designed to be run on a Windows host machine in a WDK \
    environment. Please build using a Windows target."
);

for (key, value) in &serialized_wdk_metadata_map {
    // SAFETY: this function is only conditionally compiled for windows targets, and
    // env::set_var is always safe for windows targets
    unsafe {
        std::env::set_var(key, value);
    } 
}

wdk_build::cargo_make::forward_printed_env_vars(
    serialized_wdk_metadata_map
        .into_iter()
        .map(|(key, _)| key),
);
