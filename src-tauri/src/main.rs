#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
use eo_light::{hub::Hub, model::Snapshot};
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};
use tauri::{
    menu::{Menu, MenuItem},
    tray::{MouseButton, TrayIconBuilder, TrayIconEvent},
    Emitter, Manager, State,
};
type Shared = Arc<Hub>;
struct Ready(Arc<AtomicBool>);
#[tauri::command]
fn ui_ready(ready: State<Ready>) {
    ready.0.store(true, Ordering::SeqCst);
}
#[tauri::command]
fn snapshot(hub: State<Shared>) -> Snapshot {
    hub.snapshot()
}
#[tauri::command]
async fn rescan(hub: State<'_, Shared>) -> Result<(), String> {
    hub.inner().clone().scan().await;
    Ok(())
}
#[tauri::command]
async fn control(
    hub: State<'_, Shared>,
    id: String,
    action: String,
    value: u32,
) -> Result<u64, String> {
    hub.control(&id, &action, value).await
}
#[tauri::command]
fn control_climate(
    hub: State<Shared>,
    id: String,
    action: String,
    value: serde_json::Value,
) -> Result<(), String> {
    hub.control_climate(&id, &action, value)
}
#[tauri::command]
async fn apply_preset(hub: State<'_, Shared>, id: String, slot: usize) -> Result<u64, String> {
    hub.preset(&id, slot).await
}
#[tauri::command]
fn select_device(hub: State<Shared>, id: String) -> Result<(), String> {
    hub.select(id)
}
#[tauri::command]
fn save_preset(hub: State<Shared>, id: String, slot: usize, value: u8) -> Result<(), String> {
    hub.save_preset(&id, slot, value)
}
#[tauri::command]
fn rename_device(hub: State<Shared>, id: String, name: String) -> Result<(), String> {
    hub.rename(&id, name)
}
#[tauri::command]
fn set_theme(hub: State<Shared>, theme: String) -> Result<(), String> {
    hub.theme(theme)
}
fn show(app: &tauri::AppHandle) {
    if let Some(w) = app.get_webview_window("main") {
        let _ = w.unminimize();
        let _ = w.show();
        let _ = w.set_focus();
    }
}
fn main() {
    let result = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _, _| show(app)))
        .setup(|app| {
            let handle = app.handle().clone();
            let path = std::env::current_exe()?
                .parent()
                .ok_or("Uygulama klasörü yok")?
                .join("Ayarlar/eo-light-v5.json");
            let hub = Hub::new(
                path,
                Arc::new(move |data| {
                    let _ = handle.emit("state", data);
                }),
            );
            app.manage(hub.clone());
            let ready = Arc::new(AtomicBool::new(false));
            app.manage(Ready(ready.clone()));
            // Optional read-only integration diagnostic. Never sends lamp control commands.
            if std::env::args().any(|a| a == "--diagnose") {
                if let Some(w) = app.get_webview_window("main") {
                    let _ = w.hide();
                }
                let handle = app.handle().clone();
                let check = hub.clone();
                tauri::async_runtime::spawn(async move {
                    tokio::time::sleep(std::time::Duration::from_secs(10)).await;
                    let ok = ready.load(Ordering::SeqCst);
                    if let Ok(exe) = std::env::current_exe() {
                        if let Some(parent) = exe.parent() {
                            let data =
                                serde_json::json!({"ui_ready":ok,"snapshot":check.snapshot()});
                            let _ = std::fs::write(
                                parent.join("diagnostics.json"),
                                serde_json::to_vec_pretty(&data).unwrap_or_default(),
                            );
                        }
                    }
                    handle.exit(if ok { 0 } else { 1 });
                });
            }
            tauri::async_runtime::spawn(async move {
                hub.start();
                hub.scan().await;
            });
            let open = MenuItem::with_id(app, "show", "EO-Light’ı aç", true, None::<&str>)?;
            let quit = MenuItem::with_id(app, "quit", "Çıkış", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&open, &quit])?;
            TrayIconBuilder::new()
                .icon(app.default_window_icon().ok_or("Simge bulunamadı")?.clone())
                .tooltip("EO-Light · Yerel ışık kontrolü")
                .menu(&menu)
                .show_menu_on_left_click(false)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "show" => show(app),
                    "quit" => app.exit(0),
                    _ => (),
                })
                .on_tray_icon_event(|tray, event| {
                    if matches!(
                        event,
                        TrayIconEvent::DoubleClick {
                            button: MouseButton::Left,
                            ..
                        }
                    ) {
                        show(tray.app_handle());
                    }
                })
                .build(app)?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            snapshot,
            rescan,
            control,
            control_climate,
            apply_preset,
            select_device,
            save_preset,
            rename_device,
            set_theme,
            ui_ready
        ])
        .run(tauri::generate_context!());
    if let Err(error) = result {
        eprintln!("EO-Light: {error}");
    }
}
