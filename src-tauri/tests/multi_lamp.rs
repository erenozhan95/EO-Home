use eo_home::{hub::Hub, model::Device};
use serde_json::{json, Value};
use std::sync::{
    atomic::{AtomicUsize, Ordering},
    Arc, Mutex,
};
use tokio::{
    io::{AsyncBufReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
    time::{Duration, Instant},
};

#[test]
fn migrates_legacy_presets_without_modifying_original() {
    let root = std::env::temp_dir().join(format!("eo-light-migration-{}", std::process::id()));
    let old = root.join("Ayarlar/ayarlar.json");
    std::fs::create_dir_all(old.parent().unwrap()).unwrap();
    let bytes =
        br#"{"IP":"203.0.113.5","DeviceId":"lamp-id","Model":"test-mono","Presets":[16,30,60,100]}"#;
    std::fs::write(&old, bytes).unwrap();
    let path = root.join("EO-Light-5/Ayarlar/eo-light-v5.json");
    let hub = Hub::new(path, Arc::new(|_| {}));
    let snapshot = hub.snapshot();
    assert_eq!(snapshot.selected, "lamp-id");
    assert_eq!(snapshot.devices[0].presets, [16, 30, 60, 100]);
    assert_eq!(std::fs::read(&old).unwrap(), bytes);
    std::fs::remove_file(&old).unwrap();
    std::fs::remove_dir(root.join("Ayarlar")).unwrap();
    std::fs::remove_dir(root).unwrap();
}
struct Fake {
    device: Device,
    accepts: Arc<AtomicUsize>,
    writes: Arc<Mutex<Vec<Value>>>,
    task: tokio::task::JoinHandle<()>,
}
async fn fake(id: &str, slow: bool) -> Fake {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let port = listener.local_addr().unwrap().port();
    let accepts = Arc::new(AtomicUsize::new(0));
    let writes = Arc::new(Mutex::new(vec![]));
    let a = accepts.clone();
    let w = writes.clone();
    let task = tokio::spawn(async move {
        while let Ok((stream, _)) = listener.accept().await {
            a.fetch_add(1, Ordering::SeqCst);
            let w = w.clone();
            tokio::spawn(async move {
                let (read, mut write) = stream.into_split();
                let mut lines = BufReader::new(read).lines();
                while let Ok(Some(line)) = lines.next_line().await {
                    let v: Value = serde_json::from_str(&line).unwrap();
                    w.lock().unwrap().push(v.clone());
                    if slow && v["method"] != "get_prop" {
                        tokio::time::sleep(Duration::from_millis(900)).await;
                    }
                    let result = if v["method"] == "get_prop" {
                        json!(["off", "42", "2700", "", "2"])
                    } else {
                        json!(["ok"])
                    };
                    // A property notification can precede the matching command response.
                    write
                        .write_all(b"{\"method\":\"props\",\"params\":{\"bright\":\"42\"}}\r\n")
                        .await
                        .unwrap();
                    if write
                        .write_all(
                            format!("{}\r\n", json!({"id":v["id"],"result":result})).as_bytes(),
                        )
                        .await
                        .is_err()
                    {
                        break;
                    }
                }
            });
        }
    });
    Fake {
        device: Device {
            id: id.into(),
            ip: "127.0.0.1".into(),
            port,
            support: vec!["set_power".into(), "set_bright".into()],
            ..Device::default()
        },
        accepts,
        writes,
        task,
    }
}
async fn ready(hub: &Hub, count: usize) {
    let until = Instant::now() + Duration::from_secs(3);
    while hub
        .snapshot()
        .devices
        .iter()
        .filter(|d| d.connected && d.bright == 42)
        .count()
        != count
    {
        assert!(Instant::now() < until, "not all connected");
        tokio::time::sleep(Duration::from_millis(10)).await;
    }
}
#[tokio::test]
async fn independent_connections_selection_presets_and_persistence() {
    let a = fake("a", true).await;
    let b = fake("b", false).await;
    let dir = std::env::temp_dir().join(format!("eo-light-test-{}", std::process::id()));
    let path = dir.join("prefs.json");
    let hub = Hub::new(path.clone(), Arc::new(|_| {}));
    hub.attach(a.device.clone());
    hub.attach(b.device.clone());
    ready(&hub, 2).await;
    let slow = tokio::spawn({
        let hub = hub.clone();
        async move { hub.control("a", "power", 1).await }
    });
    tokio::time::sleep(Duration::from_millis(30)).await;
    let start = Instant::now();
    hub.select("b".into()).unwrap();
    hub.control("b", "power", 1).await.unwrap();
    assert!(
        start.elapsed() < Duration::from_millis(500),
        "another lamp blocked this lamp"
    );
    slow.await.unwrap().unwrap();
    for i in 0..6 {
        hub.select(if i % 2 == 0 { "a" } else { "b" }.into())
            .unwrap();
    }
    assert_eq!(a.accepts.load(Ordering::SeqCst), 1);
    assert_eq!(b.accepts.load(Ordering::SeqCst), 1);
    assert_eq!(
        hub.snapshot().last_scan,
        None,
        "selection must not discover devices"
    );
    let before = b.writes.lock().unwrap().len();
    hub.save_preset("b", 2, 71).unwrap();
    assert_eq!(
        b.writes.lock().unwrap().len(),
        before,
        "saving preset sent a lamp command"
    );
    hub.rename("b", "Renkli lamba".into()).unwrap();
    hub.preset("b", 2).await.unwrap();
    assert!(hub.control("a", "color", 0xff0000).await.is_err());
    let loaded = Hub::new(path.clone(), Arc::new(|_| {})).snapshot();
    let d = loaded.devices.iter().find(|d| d.id == "b").unwrap();
    assert_eq!(d.presets[2], 71);
    assert_eq!(d.alias, "Renkli lamba");
    assert!(!d.connected);
    let writes = b.writes.lock().unwrap();
    assert!(writes
        .iter()
        .any(|v| v["method"] == "set_bright" && v["params"] == json!([71, "sudden", 0])));
    drop(writes);
    a.task.abort();
    b.task.abort();
    let _ = std::fs::remove_file(path);
    let _ = std::fs::remove_dir(dir);
}
#[tokio::test]
async fn stable_id_changes_endpoint_without_affecting_other_lamps() {
    let a = fake("same", false).await;
    let moved = fake("same", false).await;
    let b = fake("other", false).await;
    let hub = Hub::new(
        std::env::temp_dir().join("unused-eo-light-reconnect.json"),
        Arc::new(|_| {}),
    );
    hub.attach(a.device.clone());
    hub.attach(b.device.clone());
    ready(&hub, 2).await;
    hub.attach(moved.device.clone());
    let until = Instant::now() + Duration::from_secs(3);
    while moved.accepts.load(Ordering::SeqCst) == 0 {
        assert!(Instant::now() < until);
        tokio::time::sleep(Duration::from_millis(10)).await;
    }
    assert_eq!(hub.snapshot().devices.len(), 2);
    assert_eq!(b.accepts.load(Ordering::SeqCst), 1);
    hub.control("other", "power", 1).await.unwrap();
    a.task.abort();
    moved.task.abort();
    b.task.abort();
}
