#!@rust

//! ```cargo
//! [dependencies]
//! wdk-build = { path = ".", version = "0.5.1" }
//! anyhow = "1"
//! ```
#![allow(unused_doc_comments)]

fn main() -> anyhow::Result<()> {
    wdk_build::cargo_make::package_driver_flow_condition_script()
}
