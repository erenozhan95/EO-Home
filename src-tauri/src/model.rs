use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use std::collections::BTreeMap;

fn presets() -> [u8; 4] {
    [15, 30, 60, 100]
}
#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(default)]
pub struct Device {
    pub id: String,
    pub ip: String,
    pub port: u16,
    pub model: String,
    pub name: String,
    pub alias: String,
    pub support: Vec<String>,
    pub presets: [u8; 4],
    #[serde(skip_deserializing)]
    pub connected: bool,
    #[serde(skip_deserializing)]
    pub power: bool,
    pub bright: u8,
    pub ct: u32,
    pub rgb: u32,
    pub color_mode: u8,
    #[serde(skip_deserializing)]
    pub latency_ms: Option<u64>,
    #[serde(skip_deserializing)]
    pub error: String,
}
impl Default for Device {
    fn default() -> Self {
        Self {
            id: String::new(),
            ip: String::new(),
            port: 55443,
            model: String::new(),
            name: String::new(),
            alias: String::new(),
            support: vec![],
            presets: presets(),
            connected: false,
            power: false,
            bright: 100,
            ct: 2700,
            rgb: 0xffffff,
            color_mode: 2,
            latency_ms: None,
            error: String::new(),
        }
    }
}
impl Device {
    pub fn supports(&self, method: &str) -> bool {
        self.support.iter().any(|s| s == method)
    }
    pub fn apply(&mut self, props: &Value) {
        let n = |key: &str| {
            props
                .get(key)
                .and_then(|v| v.as_u64().or_else(|| v.as_str()?.parse().ok()))
        };
        if let Some(p) = props.get("power").and_then(Value::as_str) {
            self.power = p == "on";
        }
        if let Some(v) = n("bright").filter(|v| (1..=100).contains(v)) {
            self.bright = v as u8;
        }
        if let Some(v) = n("ct").filter(|v| (1700..=6500).contains(v)) {
            self.ct = v as u32;
        }
        if let Some(v) = n("rgb").filter(|v| *v <= 0xffffff) {
            self.rgb = v as u32;
        }
        if let Some(v) = n("color_mode") {
            self.color_mode = v as u8;
        }
    }
}
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Service {
    pub id: String,
    pub ip: String,
    pub name: String,
    pub protocol: String,
}
#[derive(Clone, Default, Serialize, Deserialize)]
#[serde(default)]
pub struct Config {
    pub selected: String,
    pub theme: String,
    pub devices: BTreeMap<String, Device>,
}
#[derive(Clone, Default, Serialize)]
pub struct Snapshot {
    pub devices: Vec<Device>,
    pub services: Vec<Service>,
    pub selected: String,
    pub scanning: bool,
    pub last_scan: Option<u64>,
    pub scan_error: String,
    pub theme: String,
}

#[derive(Clone, Debug)]
pub struct Command {
    pub method: &'static str,
    pub params: Value,
    pub optimistic: Value,
}
pub fn command(device: &Device, action: &str, value: u32) -> Result<Command, String> {
    let (method, params, optimistic) = match action {
        "power" if value <= 1 => (
            "set_power",
            json!([if value == 1 { "on" } else { "off" }, "sudden", 0]),
            json!({"power":if value==1 {"on"} else {"off"}}),
        ),
        "brightness" if (1..=100).contains(&value) => (
            "set_bright",
            json!([value, "sudden", 0]),
            json!({"bright":value}),
        ),
        "temperature" if (1700..=6500).contains(&value) => (
            "set_ct_abx",
            json!([value, "smooth", 200]),
            json!({"ct":value,"color_mode":2}),
        ),
        "color" if (1..=0xffffff).contains(&value) => (
            "set_rgb",
            json!([value, "smooth", 200]),
            json!({"rgb":value,"color_mode":1}),
        ),
        _ => return Err("Geçersiz komut veya değer.".into()),
    };
    if !device.supports(method) {
        return Err("Bu ampul seçilen özelliği desteklemiyor.".into());
    }
    Ok(Command {
        method,
        params,
        optimistic,
    })
}
pub fn property_query() -> Command {
    Command {
        method: "get_prop",
        params: json!(["power", "bright", "ct", "rgb", "color_mode"]),
        optimistic: Value::Null,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejects_unsupported_and_out_of_range() {
        let d = Device {
            support: vec!["set_power".into(), "set_bright".into()],
            ..Device::default()
        };
        assert!(command(&d, "color", 0x112233).is_err());
        assert!(command(&d, "brightness", 101).is_err());
        assert_eq!(
            command(&d, "power", 0).unwrap().params,
            json!(["off", "sudden", 0])
        );
    }
}
