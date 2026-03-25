#!@rust

//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

let source_file = wdk_build::cargo_make::get_wdk_build_output_directory().join(format!(
    "{}.dll",
    wdk_build::cargo_make::get_current_package_name()
));

let destination_file = wdk_build::cargo_make::get_wdk_build_output_directory().join(format!(
    "{}.sys",
    wdk_build::cargo_make::get_current_package_name()
));

std::fs::copy(&source_file, &destination_file).expect(&format!(
    "copy of '{}' file to '{}' file should succeed",
    source_file.display(),
    destination_file.display()
));
