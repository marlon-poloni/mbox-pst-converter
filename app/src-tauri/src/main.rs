#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod sidecar;

fn main() {
    sidecar::run();
}
