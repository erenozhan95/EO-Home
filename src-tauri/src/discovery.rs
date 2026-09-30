//! Discovery is invoked only by startup and an explicit user rescan.
//! Yeelight UDP search follows EmreOzhan/smart-gadget (MIT); see attribution.
use crate::model::{Device, DeviceKind, Service};
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
fn advertised_kind(value: &str) -> DeviceKind {
    let value = value.to_ascii_lowercase();
    if [
        "airconditioner",
        "air_conditioner",
        "air-conditioner",
        "aircon",
        "thermostat",
        "hvac",
        "_climate._",
    ]
    .iter()
    .any(|word| value.contains(word))
    {
        DeviceKind::Climate
    } else if ["lightbulb", "dimmablelight", "binarylight", "_light._"]
        .iter()
        .any(|word| value.contains(word))
    {
        DeviceKind::Light
    } else {
        DeviceKind::Other
    }
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
        kind: h
            .get("st")
            .or(h.get("nt"))
            .map(|value| advertised_kind(value))
            .unwrap_or_default(),
        climate: None,
    })
}
pub async fn scan() -> Scan {
    let mdns = tokio::task::spawn_blocking(scan_mdns);
    let mut tasks = tokio::task::JoinSet::new();
    let mut seen = std::collections::HashSet::new();
    let mut networks = Vec::new();
    if let Ok(interfaces) = if_addrs::get_if_addrs() {
        for i in interfaces {
            if let if_addrs::IfAddr::V4(v) = i.addr {
                if !v.ip.is_loopback() && v.ip.is_private() && seen.insert(v.ip) && seen.len() <= 8
                {
                    networks.push((v.ip, v.netmask));
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
    let mut hosts: BTreeMap<String, Service> = scan_neighbors(&networks)
        .into_iter()
        .map(|ip| {
            let address = ip.to_string();
            (
                address.clone(),
                Service {
                    id: format!("host:{address}"),
                    ip: address.clone(),
                    name: format!("Ağ cihazı {address}"),
                    protocol: "Yerel ağ".into(),
                    kind: DeviceKind::Other,
                    climate: None,
                },
            )
        })
        .collect();
    for service in services.into_values() {
        if let Some(host) = hosts.get_mut(&service.ip) {
            if host.protocol == "Yerel ağ" {
                *host = service;
                continue;
            }
            if !host.protocol.contains(&service.protocol) {
                host.protocol.push_str(" + ");
                host.protocol.push_str(&service.protocol);
            }
            if service.kind == DeviceKind::Climate {
                host.kind = DeviceKind::Climate;
                host.name = service.name;
            } else if service.kind == DeviceKind::Light && host.kind == DeviceKind::Other {
                host.kind = DeviceKind::Light;
                host.name = service.name;
            } else if service.protocol == "mDNS" && host.kind == DeviceKind::Other {
                host.name = service.name;
            }
        } else {
            hosts.insert(service.ip.clone(), service);
        }
    }
    hosts.retain(|ip, _| !out.lamps.iter().any(|lamp| &lamp.ip == ip));
    out.services = hosts.into_values().collect();
    out
}
fn scan_targets(ip: Ipv4Addr, mask: Ipv4Addr) -> Vec<Ipv4Addr> {
    let host = u32::from(ip);
    let mask = u32::from(mask);
    let network = host & mask;
    let broadcast = network | !mask;
    let (start, end) = if broadcast.saturating_sub(network) <= 1024 {
        (network.saturating_add(1), broadcast.saturating_sub(1))
    } else {
        ((host & 0xffffff00) + 1, (host & 0xffffff00) + 254)
    };
    if start > end {
        return Vec::new();
    }
    (start..=end)
        .filter(|candidate| *candidate != host && (*candidate & mask) == network)
        .map(Ipv4Addr::from)
        .collect()
}
fn parse_arp_neighbors(text: &str, networks: &[(Ipv4Addr, Ipv4Addr)]) -> Vec<Ipv4Addr> {
    let mut found = std::collections::BTreeSet::new();
    for line in text.lines() {
        let mut columns = line.split_whitespace();
        let Some(address) = columns.next() else { continue };
        let Some(mac) = columns.next() else { continue };
        let octets: Vec<_> = mac.split('-').collect();
        if octets.len() != 6
            || !octets
                .iter()
                .all(|part| part.len() == 2 && part.chars().all(|c| c.is_ascii_hexdigit()))
            || octets.iter().all(|part| part.eq_ignore_ascii_case("ff"))
        {
            continue;
        }
        let Ok(ip) = address.parse::<Ipv4Addr>() else {
            continue;
        };
        if !ip.is_private() || ip.is_multicast() || ip.is_broadcast() {
            continue;
        }
        if networks.iter().any(|(local, mask)| {
            let network = u32::from(*local) & u32::from(*mask);
            let candidate = u32::from(ip);
            candidate != u32::from(*local)
                && candidate != network
                && candidate != (network | !u32::from(*mask))
                && (candidate & u32::from(*mask)) == network
        }) {
            found.insert(ip);
        }
    }
    found.into_iter().collect()
}
#[cfg(windows)]
fn scan_neighbors(networks: &[(Ipv4Addr, Ipv4Addr)]) -> Vec<Ipv4Addr> {
    use std::os::windows::process::CommandExt;
    let mut command = std::process::Command::new("arp");
    command.arg("-a").creation_flags(0x0800_0000);
    command
        .output()
        .ok()
        .filter(|output| output.status.success())
        .map(|output| parse_arp_neighbors(&String::from_utf8_lossy(&output.stdout), networks))
        .unwrap_or_default()
}
#[cfg(not(windows))]
fn scan_neighbors(_networks: &[(Ipv4Addr, Ipv4Addr)]) -> Vec<Ipv4Addr> {
    Vec::new()
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
    let _ = socket.set_broadcast(true);
    let _ = socket.set_multicast_ttl_v4(2);
    let query=b"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1982\r\nMAN: \"ssdp:discover\"\r\nST: wifi_bulb\r\n\r\n";
    let general=b"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n";
    let until = Instant::now() + Duration::from_millis(4200);
    let targets = scan_targets(ip, mask);
    let broadcast = Ipv4Addr::from((u32::from(ip) & u32::from(mask)) | !u32::from(mask));
    let mut round = 0;
    let mut resend = Instant::now();
    let mut buffer = [0u8; 8192];
    let mut lamps = BTreeMap::new();
    let mut services = BTreeMap::new();
    while Instant::now() < until {
        if Instant::now() >= resend && round < 2 {
            let _ = socket.send_to(query, "239.255.255.250:1982").await;
            let _ = socket.send_to(general, "239.255.255.250:1900").await;
            let _ = socket.send_to(query, (broadcast, 1982)).await;
            for addr in &targets {
                let _ = socket.send_to(query, (*addr, 1982)).await;
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
        "_aircon._tcp.local.",
        "_thermostat._tcp.local.",
        "_climate._tcp.local.",
        "_light._tcp.local.",
    ];
    let receivers: Vec<_> = types
        .iter()
        .filter_map(|t| daemon.browse(t).ok().map(|rx| (*t, rx)))
        .collect();
    let until = std::time::Instant::now() + Duration::from_secs(4);
    let mut found = BTreeMap::new();
    while std::time::Instant::now() < until {
        for (service_type, rx) in &receivers {
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
                                kind: advertised_kind(service_type),
                                climate: None,
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
    #[test]
    fn classifies_only_explicit_climate_advertisements() {
        let source = "203.0.113.7".parse().unwrap();
        let climate = "HTTP/1.1 200 OK\r\nUSN: uuid:test::urn:example:device:Thermostat:1\r\nST: urn:example:device:Thermostat:1\r\nSERVER: Test device\r\n";
        assert_eq!(parse_service(climate, source).unwrap().kind, DeviceKind::Climate);
        let generic = "HTTP/1.1 200 OK\r\nUSN: uuid:test::upnp:rootdevice\r\nST: upnp:rootdevice\r\nSERVER: Smart climate company\r\n";
        assert_eq!(parse_service(generic, source).unwrap().kind, DeviceKind::Other);
        assert_eq!(advertised_kind("_thermostat._tcp.local."), DeviceKind::Climate);
        assert_eq!(advertised_kind("urn:schemas-upnp-org:device:DimmableLight:1"), DeviceKind::Light);
        assert_eq!(advertised_kind("_matter._tcp.local."), DeviceKind::Other);
    }
    #[test]
    fn scans_entire_small_subnet_and_filters_arp_to_local_hosts() {
        let local: Ipv4Addr = "192.0.2.10".parse().unwrap();
        let mask: Ipv4Addr = "255.255.254.0".parse().unwrap();
        let targets = scan_targets(local, mask);
        assert!(targets.contains(&"192.0.3.42".parse().unwrap()));
        assert!(!targets.contains(&local));
        let networks = [(Ipv4Addr::new(10, 20, 2, 10), mask)];
        let table = format!(
            "  {}  aa-bb-cc-dd-ee-01  dynamic\n  {}  aa-bb-cc-dd-ee-02  dynamic\n  {}  aa-bb-cc-dd-ee-03  dynamic\n  224.0.0.22  01-00-5e-00-00-16  static\n",
            Ipv4Addr::new(10, 20, 2, 1),
            Ipv4Addr::new(10, 20, 3, 42),
            Ipv4Addr::new(10, 20, 5, 2)
        );
        assert_eq!(
            parse_arp_neighbors(&table, &networks),
            vec![Ipv4Addr::new(10, 20, 2, 1), Ipv4Addr::new(10, 20, 3, 42)]
        );
    }
}
