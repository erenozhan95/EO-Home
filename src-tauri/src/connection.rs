//! One actor per lamp. No discovery, shared network lock or reconnect on selection.
use crate::model::{property_query, Command};
use serde_json::{json, Value};
use std::{collections::HashMap, net::SocketAddr, time::Duration};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpStream,
    sync::{mpsc, oneshot, watch},
    time::{timeout, Instant},
};

pub enum Update {
    Connection(bool, String),
    Properties(Value),
    Latency(u64),
}
pub type Notify = std::sync::Arc<dyn Fn(Update) + Send + Sync>;
pub struct Request {
    pub command: Command,
    pub reply: oneshot::Sender<Result<u64, String>>,
    pub queued: Instant,
}
#[derive(Clone)]
pub struct Handle {
    pub tx: mpsc::Sender<Request>,
    pub endpoint: watch::Sender<SocketAddr>,
}
struct Pending {
    command: Command,
    reply: Option<oneshot::Sender<Result<u64, String>>>,
    sent: Instant,
}
pub fn spawn(endpoint: SocketAddr, notify: Notify) -> Handle {
    let (tx, rx) = mpsc::channel(24);
    let (endpoint, watcher) = watch::channel(endpoint);
    tokio::spawn(run(watcher, rx, notify));
    Handle { tx, endpoint }
}
pub async fn send(handle: &Handle, command: Command) -> Result<u64, String> {
    let (reply, rx) = oneshot::channel();
    handle
        .tx
        .try_send(Request {
            command,
            reply,
            queued: Instant::now(),
        })
        .map_err(|_| "Ampul meşgul; tekrar deneyin.".to_string())?;
    timeout(Duration::from_secs(3), rx)
        .await
        .map_err(|_| "Ampul yanıt vermedi.".to_string())?
        .map_err(|_| "Bağlantı kapandı.".to_string())?
}
async fn run(
    mut endpoint: watch::Receiver<SocketAddr>,
    mut rx: mpsc::Receiver<Request>,
    notify: Notify,
) {
    let mut backoff = 1;
    loop {
        let addr = *endpoint.borrow_and_update();
        notify(Update::Connection(false, "Bağlanıyor…".into()));
        let connecting = timeout(Duration::from_millis(1200), TcpStream::connect(addr));
        tokio::pin!(connecting);
        let stream = loop {
            tokio::select! {
                result=&mut connecting => break result.ok().and_then(Result::ok),
                changed=endpoint.changed() => {if changed.is_err(){return;} break None;},
                request=rx.recv() => match request {Some(r)=>{let _=r.reply.send(Err("Ampul henüz bağlı değil.".into()));},None=>return},
            }
        };
        if let Some(stream) = stream {
            let _ = stream.set_nodelay(true);
            notify(Update::Connection(true, String::new()));
            let started = Instant::now();
            session(stream, &mut rx, &mut endpoint, &notify).await;
            if started.elapsed() > Duration::from_secs(5) {
                backoff = 1;
            }
        }
        notify(Update::Connection(
            false,
            "Bağlantı yok · kayıtlı adrese yeniden bağlanılıyor".into(),
        ));
        if endpoint.has_changed().unwrap_or(false) {
            continue;
        }
        let delay = tokio::time::sleep(Duration::from_secs(backoff));
        tokio::pin!(delay);
        loop {
            tokio::select! {
                _=&mut delay=>break,
                changed=endpoint.changed()=>{if changed.is_err(){return;}break;},
                request=rx.recv()=>match request {Some(r)=>{let _=r.reply.send(Err("Ampul çevrimdışı.".into()));},None=>return},
            }
        }
        backoff = (backoff * 2).min(15);
    }
}
async fn session(
    stream: TcpStream,
    rx: &mut mpsc::Receiver<Request>,
    endpoint: &mut watch::Receiver<SocketAddr>,
    notify: &Notify,
) {
    let (mut reader, mut writer) = stream.into_split();
    let mut chunk = [0u8; 4096];
    let mut buffer = Vec::new();
    let mut pending: HashMap<u64, Pending> = HashMap::new();
    let mut seq = 0u64;
    let mut poll = tokio::time::interval(Duration::from_secs(30));
    poll.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut expiry = tokio::time::interval(Duration::from_millis(200));
    loop {
        let outbound = tokio::select! {
            _=endpoint.changed()=>break,
            _=expiry.tick()=>{
                if pending.values().any(|p| p.sent.elapsed()>Duration::from_millis(1800)) {break;}
                continue;
            },
            _=poll.tick()=>{
                if !pending.is_empty(){continue;}
                Some(Pending {command:property_query(),reply:None,sent:Instant::now()})
            },
            request=rx.recv()=>match request {
                Some(r)=>{
                    if r.reply.is_closed() || r.queued.elapsed()>Duration::from_secs(2) {continue;}
                    Some(Pending {command:r.command,reply:Some(r.reply),sent:Instant::now()})
                },None=>break
            },
            read=reader.read(&mut chunk)=>{
                match read {Ok(0)|Err(_)=>break,Ok(n)=>buffer.extend_from_slice(&chunk[..n])}
                if buffer.len()>65536 {break;}
                while let Some(end)=buffer.iter().position(|b|*b==b'\n') {
                let line:Vec<_>=buffer.drain(..=end).collect();
                if let Ok(v)=serde_json::from_slice::<Value>(&line) {
                    if v["method"]=="props" {notify(Update::Properties(v["params"].clone()));}
                    if let Some(p)=v["id"].as_u64().and_then(|id|pending.remove(&id)) {
                        let elapsed=p.sent.elapsed().as_millis() as u64;
                        let result=if v.get("error").is_some() {Err(format!("Ampul komutu reddetti: {}",v["error"]))}
                        else if let Some(values)=v["result"].as_array() {
                            if p.command.method=="get_prop" {
                                let keys=["power","bright","ct","rgb","color_mode"];
                                let map=keys.iter().zip(values).map(|(k,v)|(k.to_string(),v.clone())).collect::<serde_json::Map<_,_>>();
                                notify(Update::Properties(Value::Object(map)));
                            } else {notify(Update::Properties(p.command.optimistic)); notify(Update::Latency(elapsed));}
                            Ok(elapsed)
                        } else {Err("Geçersiz ampul yanıtı.".into())};
                        if let Some(reply)=p.reply {let _=reply.send(result);}
                    }
                }
                }
                continue;
            }
        };
        if let Some(p) = outbound {
            seq += 1;
            let bytes = format!(
                "{}\r\n",
                json!({"id":seq,"method":p.command.method,"params":p.command.params})
            );
            let result = timeout(
                Duration::from_millis(700),
                writer.write_all(bytes.as_bytes()),
            )
            .await;
            if !matches!(result, Ok(Ok(()))) {
                if let Some(reply) = p.reply {
                    let _ = reply.send(Err("Komut gönderilemedi.".into()));
                }
                break;
            }
            pending.insert(seq, p);
        }
    }
    for (_, p) in pending {
        if let Some(reply) = p.reply {
            let _ = reply.send(Err("Bağlantı kesildi veya ampul yanıt vermedi.".into()));
        }
    }
}
