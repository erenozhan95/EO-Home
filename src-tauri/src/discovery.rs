//! Discovery is invoked only by startup and an explicit user rescan.
//! Yeelight UDP search follows EmreOzhan/smart-gadget (MIT); see attribution.
use crate::model::{Device, Service};
use std::{
    collections::{BTreeMap, HashMap},
    net::{Ipv4Addr, SocketAddr},
    time::Duration,
};
use tokio::{
    net::UdpSocket,
    time::{timeout, Instant},
};

#[derive(Default)]
pub struct Scan {
    pub lamps: Vec<Device>,
    pub services: Vec<Service>,
    pub errors: Vec<String>,
}
fn headers(text: &str) -> HashMap<String, String> {
    text.lines()
        .filter_map(|l| l.split_once(':'))
        .map(|(k, v)| (k.trim().to_ascii_lowercase(), v.trim().to_string()))
        .collect()
}
pub fn parse_light(text: &str, source: Ipv4Addr) -> Option<Device> {
    let h = headers(text);
    let location = h.get("location")?.strip_prefix("yeelight://")?;
    let addr: SocketAddr = location.trim_end_matches('/').parse().ok()?;
    if addr.ip() != source || addr.port() == 0 {
        return None;
    }
    let id = h.get("id")?.clone();
    if id.len() > 128 || id.is_empty() {
        return None;
    }
    let mut d = Device {
        id,
        ip: source.to_string(),
        port: addr.port(),
        model: h.get("model").cloned().unwrap_or("Ampul".into()),
        name: h.get("name").cloned().unwrap_or_default(),
        support: h
            .get("support")
            .map(|s| s.split_whitespace().map(str::to_string).collect())
            .unwrap_or_default(),
        ..Device::default()
    };
    d.apply(&serde_json::to_value(h).ok()?);
    Some(d)
}
fn parse_service(text: &str, source: Ipv4Addr) -> Option<Service> {
    let h = headers(text);
    let usn = h.get("usn")?;
    let id = usn.split("::").next()?.to_owned();
    let name = h
        .get("server")
        .or(h.get("st"))
        .cloned()
        .unwrap_or("Ağ cihazı".into());
    Some(Service {
        id,
        ip: source.to_string(),
        name,
        protocol: "SSDP / UPnP".into(),
    })
}
pub async fn scan() -> Scan {
    let mdns = tokio::task::spawn_blocking(scan_mdns);
    let mut tasks = tokio::task::JoinSet::new();
    let mut seen = std::collections::HashSet::new();
    if let Ok(interfaces) = if_addrs::get_if_addrs() {
        for i in interfaces {
            if let if_addrs::IfAddr::V4(v) = i.addr {
                if !v.ip.is_loopback() && v.ip.is_private() && seen.insert(v.ip) && seen.len() <= 8
                {
                    tasks.spawn(scan_interface(v.ip, v.netmask));
                }
            }
        }
    }
    let mut out = Scan::default();
    if seen.is_empty() {
        out.errors.push("Etkin yerel IPv4 ağı bulunamadı.".into());
    }
    let mut lamps = BTreeMap::new();
    let mut services = BTreeMap::new();
    while let Some(result) = tasks.join_next().await {
        match result {
            Ok(s) => {
                for d in s.lamps {
                    lamps.insert(d.id.clone(), d);
                }
                for d in s.services {
                    services.insert(d.id.clone(), d);
                }
                out.errors.extend(s.errors);
            }
            Err(e) => out.errors.push(format!("Tarama: {e}")),
        }
    }
    if let Ok(s) = mdns.await {
        for d in s.services {
            services.insert(d.id.clone(), d);
        }
        out.errors.extend(s.errors);
    }
    out.lamps = lamps.into_values().collect();
    let mut hosts: BTreeMap<String, Service> = BTreeMap::new();
    for service in services.into_values() {
        if let Some(host) = hosts.get_mut(&service.ip) {
            if !host.protocol.contains(&service.protocol) {
                host.protocol.push_str(" + ");
                host.protocol.push_str(&service.protocol);
            }
            if service.protocol == "mDNS" {
                host.name = service.name;
            }
        } else {
            hosts.insert(service.ip.clone(), service);
        }
    }
    out.services = hosts.into_values().collect();
    out
}
async fn scan_interface(ip: Ipv4Addr, mask: Ipv4Addr) -> Scan {
    let mut result = Scan::default();
    let socket = match UdpSocket::bind((ip, 0)).await {
        Ok(s) => s,
        Err(e) => {
            result.errors.push(format!("{ip}: {e}"));
            return result;
        }
    };
    let _ = socket.set_multicast_ttl_v4(2);
    let query=b"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1982\r\nMAN: \"ssdp:discover\"\r\nST: wifi_bulb\r\n\r\n";
    let general=b"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n";
    let until = Instant::now() + Duration::from_millis(4200);
    let base = u32::from(ip) & 0xffffff00;
    let net = u32::from(ip) & u32::from(mask);
    let mut round = 0;
    let mut resend = Instant::now();
    let mut buffer = [0u8; 8192];
    let mut lamps = BTreeMap::new();
    let mut services = BTreeMap::new();
    while Instant::now() < until {
        if Instant::now() >= resend && round < 2 {
            let _ = socket.send_to(query, "239.255.255.250:1982").await;
            let _ = socket.send_to(general, "239.255.255.250:1900").await;
            for host in 1..255 {
                let addr = Ipv4Addr::from(base | host);
                if u32::from(addr) & u32::from(mask) == net && addr != ip {
                    let _ = socket.send_to(query, (addr, 1982)).await;
                }
            }
            round += 1;
            resend = Instant::now() + Duration::from_millis(1200);
        }
        if let Ok(Ok((n, SocketAddr::V4(source)))) =
            timeout(Duration::from_millis(100), socket.recv_from(&mut buffer)).await
        {
            if !source.ip().is_private() {
                continue;
            }
            let text = String::from_utf8_lossy(&buffer[..n]);
            if let Some(d) = parse_light(&text, *source.ip()) {
                lamps.insert(d.id.clone(), d);
            } else if let Some(d) = parse_service(&text, *source.ip()) {
                services.insert(d.id.clone(), d);
            }
        }
    }
    result.lamps = lamps.into_values().collect();
    result.services = services.into_values().collect();
    result
}
fn scan_mdns() -> Scan {
    use mdns_sd::{ServiceDaemon, ServiceEvent};
    let mut result = Scan::default();
    let daemon = match ServiceDaemon::new() {
        Ok(d) => d,
        Err(e) => {
            result.errors.push(format!("mDNS: {e}"));
            return result;
        }
    };
    let types = [
        "_http._tcp.local.",
        "_googlecast._tcp.local.",
        "_hap._tcp.local.",
        "_matter._tcp.local.",
        "_shelly._tcp.local.",
        "_ewelink._tcp.local.",
    ];
    let receivers: Vec<_> = types.iter().filter_map(|t| daemon.browse(t).ok()).collect();
    let until = std::time::Instant::now() + Duration::from_secs(4);
    let mut found = BTreeMap::new();
    while std::time::Instant::now() < until {
        for rx in &receivers {
            while let Ok(event) = rx.try_recv() {
                if let ServiceEvent::ServiceResolved(info) = event {
                    if let Some(ip) = info.get_addresses().iter().find(|ip| ip.is_ipv4()) {
                        let id = info.get_fullname().to_string();
                        found.insert(
                            id.clone(),
                            Service {
                                id,
                                ip: ip.to_string(),
                                name: info
                                    .get_fullname()
                                    .split("._")
                                    .next()
                                    .unwrap_or("Ağ cihazı")
                                    .to_string(),
                                protocol: "mDNS".into(),
                            },
                        );
                    }
                }
            }
        }
        std::thread::sleep(Duration::from_millis(40));
    }
    for t in types {
        let _ = daemon.stop_browse(t);
    }
    if let Ok(done) = daemon.shutdown() {
        let _ = done.recv_timeout(Duration::from_secs(1));
    }
    result.services = found.into_values().collect();
    result
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn respects_advertised_capabilities_and_source() {
        let t="HTTP/1.1 200 OK\r\nLocation: yeelight://203.0.113.5:55443\r\nID: 0x123\r\nmodel: test-mono\r\nsupport: set_power set_bright\r\nbright: 42\r\n";
        let d = parse_light(t, "203.0.113.5".parse().unwrap()).unwrap();
        assert_eq!(d.bright, 42);
        assert!(!d.supports("set_rgb"));
        assert!(parse_light(t, "203.0.113.6".parse().unwrap()).is_none());
    }
}
