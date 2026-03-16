#[cfg(any(target_os = "windows", target_os = "linux"))]
#[tauri::command]
fn supported_platform() -> &'static str {
    if cfg!(target_os = "windows") {
        "windows"
    } else {
        "linux"
    }
}

#[cfg(any(target_os = "windows", target_os = "linux"))]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![supported_platform])
        .run(tauri::generate_context!())
        .expect("failed to run Orion desktop");
}

#[cfg(not(any(target_os = "windows", target_os = "linux")))]
pub fn run() {
    panic!("Orion Desktop currently supports only Windows and Linux.");
}
