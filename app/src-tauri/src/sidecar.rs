use std::sync::Mutex;
use std::time::Duration;

use tauri::{Emitter, Manager, RunEvent, State};
use tauri_plugin_shell::process::{CommandChild, CommandEvent};
use tauri_plugin_shell::ShellExt;

struct Core(Mutex<Option<CommandChild>>);

const STOPPED: &str = "O conversor parou de responder. Feche e abra o app de novo.";

#[tauri::command]
fn core_send(line: String, core: State<Core>) -> Result<(), String> {
    let mut guard = core.0.lock().map_err(|_| STOPPED.to_string())?;
    let child = guard.as_mut().ok_or(STOPPED)?;
    child
        .write(format!("{line}\n").as_bytes())
        .map_err(|_| STOPPED.to_string())
}

fn split_lines(pending: &mut Vec<u8>, chunk: &[u8]) -> Vec<String> {
    pending.extend_from_slice(chunk);
    let mut lines = Vec::new();
    while let Some(end) = pending.iter().position(|&b| b == b'\n') {
        let raw: Vec<u8> = pending.drain(..=end).collect();
        let line = String::from_utf8_lossy(&raw);
        let line = line.trim_end_matches(['\r', '\n']);
        if !line.is_empty() {
            lines.push(line.to_string());
        }
    }
    lines
}

pub fn run() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_opener::init())
        .manage(Core(Mutex::new(None)))
        .setup(|app| {
            let (mut rx, child) = app
                .shell()
                .sidecar("mboxpst-core")?
                .set_raw_out(true)
                .spawn()?;
            *app.state::<Core>().0.lock().unwrap() = Some(child);
            let handle = app.handle().clone();
            tauri::async_runtime::spawn(async move {
                let mut pending = Vec::new();
                while let Some(event) = rx.recv().await {
                    match event {
                        CommandEvent::Stdout(chunk) => {
                            for line in split_lines(&mut pending, &chunk) {
                                let _ = handle.emit("core-event", line);
                            }
                        }
                        CommandEvent::Terminated(_) => {
                            handle.state::<Core>().0.lock().unwrap().take();
                            let _ = handle.emit("core-exit", ());
                        }
                        _ => {}
                    }
                }
            });
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![core_send])
        .build(tauri::generate_context!())
        .expect("não foi possível iniciar o app");

    app.run(|handle, event| {
        if let RunEvent::Exit = event {
            if let Some(mut child) = handle.state::<Core>().0.lock().unwrap().take() {
                let _ = child.write(b"{\"cmd\":\"cancel\"}\n");
                std::thread::sleep(Duration::from_millis(1500));
                let _ = child.kill();
            }
        }
    });
}

#[cfg(test)]
mod tests {
    use super::split_lines;

    #[test]
    fn junta_pedacos_ate_fechar_a_linha() {
        let mut pending = Vec::new();
        assert!(split_lines(&mut pending, b"{\"event\":\"sc").is_empty());
        assert_eq!(split_lines(&mut pending, b"an\"}\n{\"a\":1}\n{\"b\""), vec!["{\"event\":\"scan\"}", "{\"a\":1}"]);
        assert_eq!(pending, b"{\"b\"");
    }

    #[test]
    fn acento_partido_entre_pedacos_continua_inteiro() {
        let mut pending = Vec::new();
        assert!(split_lines(&mut pending, b"{\"x\":\"Eletr\xc3").is_empty());
        assert_eq!(split_lines(&mut pending, b"\xb4nico\"}\n"), vec!["{\"x\":\"Eletrônico\"}"]);
    }

    #[test]
    fn ignora_cr_e_linhas_vazias() {
        let mut pending = Vec::new();
        assert_eq!(split_lines(&mut pending, b"\r\n{\"x\":\"\xc3\xa7\"}\r\n"), vec!["{\"x\":\"ç\"}"]);
    }
}
