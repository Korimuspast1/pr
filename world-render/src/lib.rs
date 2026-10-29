//! `world-render` as a library.
//!
//! The binary (`src/main.rs`) uses the same modules directly, so a single
//! `rustc src/main.rs` still builds the desktop renderer without cargo. This
//! library entry point exists for the WebAssembly build used by the browser
//! walk mode: `rustc --target wasm32-unknown-unknown --crate-type cdylib`.

pub mod camera;
pub mod png;
pub mod presets;
pub mod render;
pub mod vec3;
pub mod walk;
pub mod world;

#[cfg(target_arch = "wasm32")]
pub mod wasm;
