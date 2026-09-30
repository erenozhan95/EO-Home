use crate::{
    climate::{self, ClimateCommand},
    connection::{self, Handle, Update},
    discovery,
    model::{self, Config, Device, DeviceKind, Snapshot},
};
use std::{
    collections::HashMap,
    fs,
    net::SocketAddr,
    path::PathBuf,
    sync::{Arc, Mutex},
    time::{SystemTime, UNIX_EPOCH},
};
pub struct Hub {
    data: Mutex<Snapshot>,
    handles: Mutex<HashMap<String, Handle>>,
    saving: Mutex<()>,
    path: PathBuf,
    publish: Arc<dyn Fn(Snapshot) + Send + Sync>,
}
impl Hub {
    pub fn new(path: PathBuf, publish: Arc<dyn Fn(Snapshot) + Send + Sync>) -> Arc<Self> {
        let mut warning = String::new();
        let config = if path.exists() {
            match fs::read(&path)
                .ok()
                .and_then(|b| serde_json::from_slice::<Config>(&b).ok())
            {
                Some(c) => c,
                None => {
                    warning =
                        "Ayar dosyası okunamadı; orijinali bozuk-dosya yedeğinde korunur.".into();
                    Config::default()
                }
            }
        } else {
            Config::default()
        };
        if !warning.is_empty() {
            let _ = fs::copy(&path, path.with_extension(format!("{}.bak", now())));
        }
        let mut devices: Vec<_> = config.devices.into_values().collect();
        let mut selected = config.selected;
        // Migrate only preferences, without overwriting the previous application's file.
        if devices.is_empty() && !path.exists() {
            let candidates = [
                path.with_file_name("ayarlar.json"),
                path.parent()
                    .and_then(|p| p.parent())
                    .and_then(|p| p.parent())
                    .unwrap_or(&path)
                    .join("Ayarlar/ayarlar.json"),
            ];
            for legacy in candidates {
                if let Some(v) = fs::read(legacy)
                    .ok()
                    .and_then(|b| serde_json::from_slice::<serde_json::Value>(&b).ok())
                {
                    if let (Some(id), Some(ip)) = (v["DeviceId"].as_str(), v["IP"].as_str()) {
                        if !id.is_empty() && ip.parse::<std::net::Ipv4Addr>().is_ok() {
                            let mut d = Device {
                                id: id.into(),
                                ip: ip.into(),
                                model: v["Model"].as_str().unwrap_or("Ampul").into(),
                                alias: "Ampul".into(),
                                ..Device::default()
                            };
                            if let Some(p) = v["Presets"].as_array().filter(|p| p.len() == 4) {
                                for (i, v) in p.iter().enumerate() {
                                    d.presets[i] = v.as_u64().unwrap_or(100).clamp(1, 100) as u8;
                                }
                            }
                            selected = d.id.clone();
                            devices.push(d);
                            break;
                        }
                    }
                }
            }
        }
        Arc::new(Self {
            data: Mutex::new(Snapshot {
                devices,
                selected,
                theme: if config.theme == "light" {
                    "light"
                } else {
                    "dark"
                }
                .into(),
                scan_error: warning,
                ..Snapshot::default()
            }),
            handles: Mutex::new(HashMap::new()),
            saving: Mutex::new(()),
            path,
            publish,
        })
    }
    pub fn snapshot(&self) -> Snapshot {
        self.data.lock().unwrap().clone()
    }
    fn emit(&self) {
        (self.publish)(self.snapshot());
    }
    pub fn start(self: &Arc<Self>) {
        for d in self.snapshot().devices {
            self.attach(d);
        }
    }
    pub fn attach(self: &Arc<Self>, d: Device) {
        let addr = match format!("{}:{}", d.ip, d.port).parse::<SocketAddr>() {
            Ok(a) => a,
            Err(_) => return,
        };
        {
            let mut data = self.data.lock().unwrap();
            if let Some(old) = data.devices.iter_mut().find(|old| old.id == d.id) {
                old.ip = d.ip.clone();
                old.port = d.port;
                old.model = d.model.clone();
                old.name = d.name.clone();
                old.support = d.support.clone();
            } else {
                data.devices.push(d.clone());
            }
            if data.selected.is_empty() {
                data.selected = d.id.clone();
            }
        }
        let mut handles = self.handles.lock().unwrap();
        if let Some(h) = handles.get(&d.id) {
            let changed = *h.endpoint.borrow() != addr;
            if changed {
                let _ = h.endpoint.send(addr);
            }
        } else {
            let weak = Arc::downgrade(self);
            let id = d.id.clone();
            let callback = Arc::new(move |event| {
                if let Some(hub) = weak.upgrade() {
                    {
                        let mut data = hub.data.lock().unwrap();
                        if let Some(d) = data.devices.iter_mut().find(|d| d.id == id) {
                            match event {
                                Update::Connection(ok, error) => {
                                    d.connected = ok;
                                    d.error = error;
                                }
                                Update::Properties(props) => d.apply(&props),
                                Update::Latency(n) => d.latency_ms = Some(n),
                            }
                        }
                    }
                    hub.emit();
                }
            });
            handles.insert(d.id, connection::spawn(addr, callback));
        }
    }
    pub async fn scan(self: Arc<Self>) {
        {
            let mut data = self.data.lock().unwrap();
            if data.scanning {
                return;
            }
            data.scanning = true;
        }
        self.emit();
        let result = discovery::scan().await;
        for d in result.lamps {
            self.attach(d);
        }
        {
            let mut data = self.data.lock().unwrap();
            data.services = result.services;
            data.last_scan = Some(now());
            data.scanning = false;
            data.scan_error = result.errors.join(" · ");
        }
        if let Err(e) = self.persist() {
            self.data.lock().unwrap().scan_error = e;
        }
        self.emit();
    }
    pub async fn control(&self, id: &str, action: &str, value: u32) -> Result<u64, String> {
        let command = {
            let data = self.data.lock().unwrap();
            let d = data
                .devices
                .iter()
                .find(|d| d.id == id)
                .ok_or("Ampul bulunamadı.")?;
            if !d.connected {
                return Err("Ampul çevrimdışı. IP değiştiyse Yeniden tara’ya basın.".into());
            }
            model::command(d, action, value)?
        };
        let handle = self
            .handles
            .lock()
            .unwrap()
            .get(id)
            .cloned()
            .ok_or("Bağlantı bulunamadı.")?;
        connection::send(&handle, command).await
    }
    pub fn control_climate(
        &self,
        id: &str,
        action: &str,
        value: serde_json::Value,
    ) -> Result<(), String> {
        let data = self.data.lock().unwrap();
        let service = data
            .services
            .iter()
            .find(|service| service.id == id && service.kind == DeviceKind::Climate)
            .ok_or("Klima bulunamadı.")?;
        let state = service
            .climate
            .as_ref()
            .ok_or("Bu klima için yerel kontrol sürücüsü henüz yok.")?;
        let command = match action {
            "power" => ClimateCommand::Power(value.as_bool().ok_or("Geçersiz güç değeri.")?),
            "target_temperature" => ClimateCommand::TargetTemperature(
                value.as_f64().ok_or("Geçersiz sıcaklık değeri.")? as f32,
            ),
            "mode" => ClimateCommand::Mode(
                value.as_str().ok_or("Geçersiz çalışma modu.")?.into(),
            ),
            "fan_speed" => ClimateCommand::Fan(
                value.as_str().ok_or("Geçersiz fan hızı.")?.into(),
            ),
            _ => return Err("Bilinmeyen klima komutu.".into()),
        };
        climate::validate(state, command)?;
        Err("Bu klima için komut gönderebilen bir sürücü henüz bağlı değil.".into())
    }
    pub async fn preset(&self, id: &str, slot: usize) -> Result<u64, String> {
        let value = {
            let data = self.data.lock().unwrap();
            *data
                .devices
                .iter()
                .find(|d| d.id == id)
                .ok_or("Ampul bulunamadı.")?
                .presets
                .get(slot)
                .ok_or("Kayıt bulunamadı.")?
        };
        self.control(id, "brightness", value as u32).await?;
        self.control(id, "power", 1).await
    }
    pub fn select(&self, id: String) -> Result<(), String> {
        {
            let mut data = self.data.lock().unwrap();
            if !data.devices.iter().any(|d| d.id == id) {
                return Err("Ampul bulunamadı.".into());
            }
            data.selected = id;
        }
        self.persist()?;
        self.emit();
        Ok(())
    }
    pub fn save_preset(&self, id: &str, slot: usize, value: u8) -> Result<(), String> {
        if slot > 3 || !(1..=100).contains(&value) {
            return Err("Geçersiz parlaklık kaydı.".into());
        }
        {
            let mut data = self.data.lock().unwrap();
            let d = data
                .devices
                .iter_mut()
                .find(|d| d.id == id)
                .ok_or("Ampul bulunamadı.")?;
            d.presets[slot] = value;
        }
        self.persist()?;
        self.emit();
        Ok(())
    }
    pub fn rename(&self, id: &str, name: String) -> Result<(), String> {
        if name.chars().count() > 40 {
            return Err("İsim en fazla 40 karakter olabilir.".into());
        }
        {
            let mut data = self.data.lock().unwrap();
            data.devices
                .iter_mut()
                .find(|d| d.id == id)
                .ok_or("Ampul bulunamadı.")?
                .alias = name.trim().into();
        }
        self.persist()?;
        self.emit();
        Ok(())
    }
    pub fn theme(&self, theme: String) -> Result<(), String> {
        if theme != "dark" && theme != "light" {
            return Err("Geçersiz tema".into());
        }
        self.data.lock().unwrap().theme = theme;
        self.persist()?;
        self.emit();
        Ok(())
    }
    fn persist(&self) -> Result<(), String> {
        let _save = self.saving.lock().unwrap();
        let data = self.snapshot();
        let config = Config {
            selected: data.selected,
            theme: data.theme,
            devices: data
                .devices
                .into_iter()
                .map(|d| (d.id.clone(), d))
                .collect(),
        };
        let path = &self.path;
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent).map_err(|e| format!("Ayarlar kaydedilemedi: {e}"))?;
        }
        let temp = path.with_extension("tmp");
        fs::write(
            &temp,
            serde_json::to_vec_pretty(&config).map_err(|e| e.to_string())?,
        )
        .map_err(|e| format!("Ayarlar yazılamadı: {e}"))?;
        fs::rename(&temp, path).map_err(|e| format!("Ayarlar kaydedilemedi: {e}"))
    }
}
fn now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}
