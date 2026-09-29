use eo_light::{discovery, hub::Hub};
use std::sync::Arc;
#[tokio::main]
async fn main() {
    let scan = discovery::scan().await;
    let hub = Hub::new(
        std::env::temp_dir().join("eo-light-read-only-inspection.json"),
        Arc::new(|_| {}),
    );
    for d in scan.lamps {
        hub.attach(d);
    }
    tokio::time::sleep(std::time::Duration::from_secs(2)).await;
    println!("{}",serde_json::to_string_pretty(&serde_json::json!({"lamps":hub.snapshot().devices,"services":scan.services,"errors":scan.errors})).unwrap());
}
