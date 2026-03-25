//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

// Create build output directory if it doesn't exist
let output_folder_path = wdk_build::cargo_make::get_wdk_build_output_directory();
if !output_folder_path.exists() {
    std::fs::create_dir_all(&output_folder_path).expect(&format!("creation of '{}' folder should succeed", output_folder_path.display()));
}

let cargo_make_working_directory = std::env::var("CARGO_MAKE_WORKING_DIRECTORY").expect(
    "CARGO_MAKE_WORKING_DIRECTORY should be set by cargo-make via the env section of \
        rust-driver-makefile.toml",
);

let source_file = [
    cargo_make_working_directory,
    format!("{}.inx", wdk_build::cargo_make::get_current_package_name()),
]
.iter()
.collect::<std::path::PathBuf>();

let destination_file = wdk_build::cargo_make::get_wdk_build_output_directory().join(format!(
    "{}.inf",
    wdk_build::cargo_make::get_current_package_name()
));

std::fs::copy(&source_file, &destination_file).expect(&format!(
    "copy of '{}' file to '{}' file should succeed",
    source_file.display(),
    destination_file.display()
));
