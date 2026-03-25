//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! ```
#![allow(unused_doc_comments)]

let cli_env_vars = wdk_build::cargo_make::validate_command_line_args();
let path_env_vars = wdk_build::cargo_make::setup_path()?;
let wdk_version_env_vars = wdk_build::cargo_make::setup_wdk_version()?;

wdk_build::cargo_make::forward_printed_env_vars(
    cli_env_vars.into_iter().chain(path_env_vars).chain(wdk_version_env_vars),
);
