import { invoke, isTauri } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { getCurrentWindow } from "@tauri-apps/api/window";
import {
  createIcons,
  Lightbulb,
  Power,
  RefreshCw,
  Sun,
  Moon,
  Minus,
  X,
  Settings2,
  Check,
  ChevronRight,
  Radio,
  Network,
  Save,
  Monitor,
  Palette,
  Thermometer,
  Pencil,
  WifiOff,
  AirVent,
  Fan,
} from "lucide";
import "./style.css";

type Device = {
  id: string;
  ip: string;
  port: number;
  model: string;
  name: string;
  alias: string;
  support: string[];
  presets: number[];
  connected: boolean;
  power: boolean;
  bright: number;
  ct: number;
  rgb: number;
  color_mode: number;
  latency_ms: number | null;
  error: string;
};
type ClimateState = {
  power: boolean;
  target_c: number;
  ambient_c: number | null;
  mode: string;
  fan: string;
  min_c: number;
  max_c: number;
  modes: string[];
  fan_modes: string[];
  support: string[];
};
type Service = {
  id: string;
  ip: string;
  name: string;
  protocol: string;
  kind: "climate" | "other";
  climate: ClimateState | null;
};
type Snapshot = {
  devices: Device[];
  services: Service[];
  selected: string;
  scanning: boolean;
  last_scan: number | null;
  scan_error: string;
  theme: string;
};
let state: Snapshot = {
  devices: [],
  services: [],
  selected: "",
  scanning: false,
  last_scan: null,
  scan_error: "",
  theme: "dark",
};
let current = "";
const pending = new Set<string>();
const drafts = new Map<
  string,
  Partial<Pick<Device, "bright" | "ct" | "rgb">>
>();
const demo =
  import.meta.env?.DEV && new URLSearchParams(location.search).has("demo");
const native = isTauri();
const iconSet = {
  Lightbulb,
  Power,
  RefreshCw,
  Sun,
  Moon,
  Minus,
  X,
  Settings2,
  Check,
  ChevronRight,
  Radio,
  Network,
  Save,
  Monitor,
  Palette,
  Thermometer,
  Pencil,
  WifiOff,
  AirVent,
  Fan,
};
const icons = () =>
  createIcons({ icons: iconSet, attrs: { "stroke-width": 1.7 } });
const icon = (name: string) => `<i data-lucide="${name}"></i>`;
const esc = (s: unknown) =>
  String(s ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ]!,
  );
const label = (d: Device) =>
  d.alias || d.name || `Ampul ${state.devices.indexOf(d) + 1}`;
const selected = () => state.devices.find((d) => d.id === current);
const selectedClimate = () =>
  state.services.find((s) => s.kind === "climate" && current === `service:${s.id}`);
const climateServices = () => state.services.filter((s) => s.kind === "climate");
const supports = (d: Device, method: string) => d.support.includes(method);
const disabled = (value: boolean) => (value ? "disabled" : "");
const hex = (n: number) => "#" + n.toString(16).padStart(6, "0");
const $ = <T extends HTMLElement = HTMLElement>(query: string) =>
  document.querySelector<T>(query)!;

$("#app").innerHTML = `
<header class="titlebar"><div class="brand">${icon("lightbulb")}<b>EO<span>·</span>Light</b><span class="version">5</span></div><div class="window-actions"><span class="local-label">${icon("radio")} Yerel bağlantı</span><button data-action="theme" title="Temayı değiştir" aria-label="Temayı değiştir">${icon("sun")}</button><span class="divider"></span><button data-action="hide" title="Sistem tepsisine küçült" aria-label="Sistem tepsisine küçült">${icon("minus")}</button><button data-action="close" class="close" title="Çıkış" aria-label="Çıkış">${icon("x")}</button></div></header>
<div class="shell"><aside><div class="sidebar-heading"><span>CİHAZLARIM</span><span id="count">0</span></div><div id="devices" role="navigation" aria-label="Cihaz seçimi"></div><div class="sidebar-bottom"><div class="connection-summary"><span class="dot"></span><span id="connection-count">Bağlantı bekleniyor</span></div><button class="scan" data-action="scan">${icon("refresh-cw")}<span>Yeniden tara</span></button><p id="scan-time">Açılışta bir kez taranır</p><button class="network-link" data-action="network">${icon("network")} Ağdaki diğer cihazlar <span id="service-count">0</span></button></div></aside>
<main><div class="page-heading"><div><div class="eyebrow">AKILLI EVİN, TEK BİR YERDE</div><h1>Ev kontrolü</h1></div><span class="connection-mode">${icon("radio")} Aynı anda bağlı</span></div><div id="scan-warning" role="status"></div><section id="panel"></section><footer><span>${icon("monitor")} Yalnızca yerel ağında çalışır</span><span id="footer-note">Cihaz seçmek diğer bağlantıları etkilemez</span></footer></main></div>
<div id="toast" role="status" aria-live="polite"></div><dialog id="dialog"><div id="dialog-body"></div></dialog>`;

function bulb(d: Device) {
  return `<div class="bulb-art ${d.power ? "lit" : ""}" style="--lamp-color:${d.color_mode === 1 ? hex(d.rgb) : d.ct < 3500 ? "#ffd793" : d.ct < 5000 ? "#fff0d0" : "#d8eaff"};--intensity:${d.bright / 100}"><div class="halo"></div><svg viewBox="0 0 180 205" aria-hidden="true"><defs><linearGradient id="glass" x1="0" y1="0" x2="0.8" y2="1"><stop stop-color="currentColor" stop-opacity=".25"/><stop offset="1" stop-color="currentColor" stop-opacity=".04"/></linearGradient></defs><path class="bulb-glass" d="M90 19c-33 0-57 24-57 55 0 25 15 36 26 52 5 7 7 14 7 24h48c0-10 2-17 7-24 11-16 26-27 26-52 0-31-24-55-57-55Z"/><path class="filament" d="m72 86 18 17 18-17m-18 17v47"/><path class="shine" d="M51 73c0-22 17-39 39-39"/><path class="base" d="M67 153h46v13H67zm3 15h40v12H70z"/><path class="tip" d="M77 184h26c-1 9-25 9-26 0Z"/></svg><div class="platform"></div></div>`;
}

const modeLabel = (mode: string) =>
  ({ cool: "Soğutma", heat: "Isıtma", auto: "Otomatik", fan: "Fan", dry: "Nem alma" })[
    mode as "cool" | "heat" | "auto" | "fan" | "dry"
  ] || mode;
const fanLabel = (fan: string) =>
  ({ auto: "Otomatik", low: "Düşük", medium: "Orta", high: "Yüksek" })[
    fan as "auto" | "low" | "medium" | "high"
  ] || fan;
function renderClimatePanel(service: Service) {
  const climate = service.climate;
  const has = (feature: string) => !!climate?.support.includes(feature);
  const modes = climate?.modes || ["cool", "heat", "auto", "fan"];
  const fans = climate?.fan_modes || ["auto", "low", "medium", "high"];
  const target = climate?.target_c ?? 24;
  $("#panel").innerHTML = `
    <div class="selected-heading"><div><h2>${esc(service.name)}</h2><span class="model">Klima <span>·</span> ${esc(service.protocol)}</span></div><span class="climate-badge">${climate ? "Kontrol hazır" : "Sürücü gerekli"}</span></div>
    ${climate ? "" : `<div class="offline-banner">${icon("wifi-off")}<span>Cihaz klima/termostat türü duyurdu. Sıcaklık, mod ve fan komutları için bu modelin yerel kontrol sürücüsü gerekir; şu anda cihazına komut gönderilmiyor.</span></div>`}
    <div class="climate-grid">
      <section class="card climate-hero"><div class="climate-symbol">${icon("air-vent")}</div><div class="card-label">KLİMA DURUMU</div><h3>${climate ? (climate.power ? "Açık" : "Kapalı") : "Durum bilinmiyor"}</h3><p>${climate?.ambient_c == null ? "Oda sıcaklığı okunamadı" : `Oda sıcaklığı ${climate.ambient_c.toFixed(1)} °C`}</p><button class="power-button ${climate?.power ? "active" : ""}" data-action="climate-power" aria-label="Klimayı aç veya kapat" ${disabled(!has("power") || pending.has(`service:${service.id}`))}>${icon("power")}</button></section>
      <section class="card climate-temperature"><div class="card-label">${icon("thermometer")} HEDEF SICAKLIK</div><div class="climate-target"><strong id="climate-target-number">${climate ? target.toFixed(1) : "—"}</strong><span>°C</span></div><input id="climate-temperature" type="range" aria-label="Klima hedef sıcaklığı" min="${climate?.min_c ?? 16}" max="${climate?.max_c ?? 30}" step="0.5" value="${target}" ${disabled(!has("target_temperature"))}><div class="range-labels"><span>${climate?.min_c ?? 16} °C</span><span>${climate?.max_c ?? 30} °C</span></div></section>
      <section class="card climate-options"><div class="card-label">${icon("air-vent")} ÇALIŞMA MODU</div><div class="climate-buttons">${modes.map((mode) => `<button data-action="climate-mode" data-value="${esc(mode)}" class="${climate?.mode === mode ? "chosen" : ""}" ${disabled(!has("mode") || pending.has(`service:${service.id}`))}>${esc(modeLabel(mode))}</button>`).join("")}</div></section>
      <section class="card climate-options"><div class="card-label">${icon("fan")} FAN HIZI</div><div class="climate-buttons">${fans.map((fan) => `<button data-action="climate-fan" data-value="${esc(fan)}" class="${climate?.fan === fan ? "chosen" : ""}" ${disabled(!has("fan_speed") || pending.has(`service:${service.id}`))}>${esc(fanLabel(fan))}</button>`).join("")}</div></section>
    </div>
    <p class="climate-note">${climate ? "Yalnızca cihazın bildirdiği desteklenen ayarlar kullanılabilir." : "Ağda bulunması, doğrudan kontrol edilebileceği anlamına gelmez."}</p>`;
}
function renderSidebar() {
  $("#count").textContent = String(state.devices.length + climateServices().length);
  $("#devices").innerHTML =
    (state.devices
      .map(
        (d) =>
          `<button class="device ${d.id === current ? "selected" : ""}" data-action="select" data-id="${esc(d.id)}" aria-pressed="${d.id === current}"><span class="device-icon ${d.power && d.connected ? "on" : ""}">${icon("lightbulb")}</span><span class="device-copy"><b>${esc(label(d))}</b><small><span class="dot ${d.connected ? "online" : "offline"}"></span>${d.connected ? (d.power ? "Açık · %" + d.bright : "Kapalı · Bağlı") : "Çevrimdışı"}</small></span>${icon("chevron-right")}</button>`,
      )
      .join("") +
      climateServices()
        .map(
          (s) =>
            `<button class="device ${current === `service:${s.id}` ? "selected" : ""}" data-action="select" data-id="${esc(`service:${s.id}`)}" aria-pressed="${current === `service:${s.id}`}"><span class="device-icon climate-icon">${icon("air-vent")}</span><span class="device-copy"><b>${esc(s.name)}</b><small><span class="dot ${s.climate ? "online" : "offline"}"></span>${s.climate ? "Klima · Kontrol hazır" : "Klima · Sürücü gerekli"}</small></span>${icon("chevron-right")}</button>`,
        )
        .join("")) ||
    `<div class="empty-sidebar">${icon("network")}<p>${state.scanning ? "Cihazlar aranıyor…" : "Henüz cihaz bulunamadı"}</p></div>`;
  const online = state.devices.filter((d) => d.connected).length;
  $("#connection-count").textContent =
    `${online} / ${state.devices.length} ampul bağlı`;
  const scan = $<HTMLButtonElement>('[data-action="scan"]');
  scan.disabled = state.scanning;
  scan.classList.toggle("scanning", state.scanning);
  scan.querySelector("span")!.textContent = state.scanning
    ? "Ağ taranıyor…"
    : "Yeniden tara";
  $("#scan-time").textContent = state.scanning
    ? "Kontrolleri kullanmaya devam edebilirsin"
    : state.last_scan
      ? `Son tarama ${new Date(state.last_scan * 1000).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}`
      : "Açılışta bir kez taranır";
  $("#service-count").textContent = String(state.services.length);
  $("#scan-warning").textContent = state.scan_error;
}

function renderPanel(force = false) {
  if (
    !force &&
    document.activeElement instanceof HTMLInputElement &&
    $("#panel").contains(document.activeElement)
  )
    return;
  const focus = document.activeElement as HTMLElement | null;
  const sameDevice = $("#panel").dataset.device === current;
  const focusId =
    sameDevice && focus && $("#panel").contains(focus) ? focus.id : "";
  const d = selected();
  const climate = selectedClimate();
  if (climate) {
    renderClimatePanel(climate);
    $("#panel").dataset.device = current;
    return;
  }
  if (!d) {
    $("#panel").innerHTML =
      `<div class="empty-state">${icon("lightbulb")}<h2>${state.scanning ? "Cihazlar aranıyor" : "Cihazlarını bulalım"}</h2><p>${state.scanning ? "Ağdaki uyumlu cihazlar burada görünecek." : "Bilgisayar ve ampul aynı ağda, ampulün LAN kontrolü açık olmalı. Ardından Yeniden tara’ya bas."}</p></div>`;
    return;
  }
  const draft = drafts.get(d.id) || {};
  const bright = draft.bright ?? d.bright,
    ct = draft.ct ?? d.ct;
  const rgb = supports(d, "set_rgb"),
    temperature = supports(d, "set_ct_abx");
  const busy = pending.has(d.id);
  const off = !d.connected;
  $("#panel").innerHTML = `
  <div class="selected-heading"><div><h2>${esc(label(d))}</h2><span class="model">${esc(d.model)} <span>·</span> ${d.connected ? "Bağlantı hazır" : "Bağlantı bekleniyor"}</span></div><button class="icon-button" data-action="details" title="Ampul adı ve ayrıntıları" aria-label="Ampul adı ve ayrıntıları">${icon("settings-2")}</button></div>
  ${off ? `<div class="offline-banner">${icon("wifi-off")}<span>Bu ampule ulaşılamıyor. IP değiştiyse yeniden tara; diğer ampulleri kullanabilirsin.</span></div>` : ""}
  <div class="primary-grid"><div class="hero card">${bulb(d)}<div class="hero-bottom"><span class="power-caption"><span class="dot ${d.power ? "online" : ""}"></span>${d.power ? "Işık açık" : "Işık kapalı"}</span><button class="power-button ${d.power ? "active" : ""}" data-action="power" ${disabled(off || busy || !supports(d, "set_power"))} title="${d.power ? "Işığı kapat" : "Işığı aç"}" aria-label="${d.power ? "Işığı kapat" : "Işığı aç"}" aria-pressed="${d.power}">${icon("power")}</button></div></div>
  <div class="brightness card"><div class="card-label">${icon("sun")} PARLAKLIK <span class="live-tag">${busy ? "Gönderiliyor" : d.connected ? "Hazır" : "Çevrimdışı"}</span></div><div class="brightness-value"><strong id="bright-number">${bright}</strong><span>%</span></div><p>Odanın ışığını sana göre ayarla.</p><input aria-label="Parlaklık" id="brightness" type="range" min="1" max="100" value="${bright}" style="--fill:${bright}%" ${disabled(off || !supports(d, "set_bright"))}><div class="range-labels"><span>Loş</span><span>Aydınlık</span></div><div class="response"><span class="dot ${d.connected ? "online" : "offline"}"></span>${d.latency_ms === null ? "Doğrudan ampul bağlantısı" : `Son komut yanıtı <b>${d.latency_ms} ms</b>`}</div></div></div>
  <div class="presets-heading"><h3>Hızlı ayarlar</h3><span>Mevcut parlaklığı bir yuvaya kaydet</span></div><div class="presets">${d.presets.map((value, i) => `<div class="preset"><button class="preset-apply" data-action="preset" data-slot="${i}" ${disabled(off || busy || !supports(d, "set_power") || !supports(d, "set_bright"))}><small>AYAR 0${i + 1}</small><b>%${value}</b></button><button class="preset-save" data-action="save" data-slot="${i}" title="%${bright} değerini Ayar ${i + 1} olarak kaydet" aria-label="Ayar ${i + 1} için mevcut parlaklığı kaydet">${icon("save")}</button></div>`).join("")}</div>
  <div class="color-grid"><section class="card temperature ${temperature ? "" : "unsupported"}"><div class="card-heading"><h3>${icon("thermometer")} Beyaz tonu</h3><span>${temperature ? `<b id="ct-number">${ct}</b> K` : "Desteklenmiyor"}</span></div><div class="white-options">${[
    [2700, "Sıcak"],
    [4000, "Doğal"],
    [6500, "Soğuk"],
  ]
    .map(
      ([v, t]) =>
        `<button data-action="white" data-value="${v}" class="${d.ct === v && temperature ? "chosen" : ""}" ${disabled(!temperature || off || busy)}><span style="--swatch:${v === 2700 ? "#ffd28d" : v === 4000 ? "#fff1c9" : "#d6eaff"}"></span>${t}</button>`,
    )
    .join(
      "",
    )}</div><input type="range" id="temperature" aria-label="Beyaz ışık sıcaklığı" min="1700" max="6500" step="50" value="${ct}" ${disabled(!temperature || off)}><p>${temperature ? "Sarıdan soğuk beyaza, istediğin ton." : "Bu ampulün beyaz tonu donanımda sabit."}</p></section>
  <section class="card colors ${rgb ? "" : "unsupported"}"><div class="card-heading"><h3>${icon("palette")} Renkler</h3><span>${rgb ? "RGB" : "Desteklenmiyor"}</span></div><div class="swatches">${["#fa746e", "#ffbd69", "#f2e789", "#7cdeb2", "#7fb4ff", "#bd96f6"].map((c) => `<button data-action="color" data-value="${parseInt(c.slice(1), 16)}" style="--swatch:${c}" aria-label="Renk ${c}" title="${c}" ${disabled(!rgb || off || busy)}>${d.rgb === parseInt(c.slice(1), 16) ? icon("check") : ""}</button>`).join("")}</div><label class="custom-color"><span>Özel renk</span><input type="color" id="color" value="${hex(d.rgb)}" aria-label="Özel renk seç" ${disabled(!rgb || off)}></label><p>${rgb ? "Bir renk seç, odanın havası değişsin." : "RGB destekli bir ampul seçtiğinde kullanılabilir."}</p></section></div>`;
  $("#panel").dataset.device = current;
  if (focusId) document.getElementById(focusId)?.focus({ preventScroll: true });
}
function render(force = false) {
  document.documentElement.dataset.theme = state.theme;
  renderSidebar();
  renderPanel(force);
  icons();
}
function accept(next: Snapshot) {
  state = next;
  if (
    !current ||
    (!state.devices.some((d) => d.id === current) &&
      !state.services.some((s) => current === `service:${s.id}`))
  )
    current = state.selected || state.devices[0]?.id || "";
  render();
}
let toastTimer: ReturnType<typeof setTimeout>;
function toast(message: string, error = false) {
  const e = $("#toast");
  e.textContent = message;
  e.className = `visible ${error ? "error" : ""}`;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (e.className = ""), 4000);
}

async function call<T>(
  method: string,
  args: Record<string, unknown> = {},
): Promise<T> {
  if (demo) return demoCall(method, args) as T;
  if (!native)
    throw new Error(
      "Ampulleri kontrol etmek için EO-Light.exe uygulamasını aç.",
    );
  return invoke<T>(method, args);
}
async function change(action: string, value: number) {
  const d = selected();
  if (!d) return;
  const id = d.id;
  pending.add(id);
  render();
  try {
    await call("control", { id, action, value });
    drafts.delete(id);
  } catch (e) {
    drafts.delete(id);
    toast(String(e), true);
  } finally {
    pending.delete(id);
    render(true);
  }
}
async function changeClimate(action: string, value: number | string | boolean) {
  const service = selectedClimate();
  if (!service?.climate) return;
  const key = `service:${service.id}`;
  pending.add(key);
  render();
  try {
    await call("control_climate", { id: service.id, action, value });
  } catch (error) {
    toast(String(error), true);
  } finally {
    pending.delete(key);
    render(true);
  }
}
async function action(button: HTMLButtonElement) {
  const a = button.dataset.action,
    d = selected();
  if (a === "select") {
    current = button.dataset.id!;
    render(true);
    if (!current.startsWith("service:"))
      await call("select_device", { id: current });
    return;
  }
  if (a === "scan") {
    await call("rescan");
    return;
  }
  if (a === "theme") {
    await call("set_theme", {
      theme: state.theme === "dark" ? "light" : "dark",
    });
    return;
  }
  if (a === "hide") {
    if (native) await getCurrentWindow().hide();
    else toast("Sistem tepsisi masaüstü uygulamasında kullanılabilir.");
    return;
  }
  if (a === "close") {
    if (native) await getCurrentWindow().close();
    return;
  }
  if (a === "dismiss") {
    $<HTMLDialogElement>("#dialog").close();
    return;
  }
  if (a === "network") {
    showNetwork();
    return;
  }
  const climate = selectedClimate();
  if (climate?.climate) {
    if (a === "climate-power")
      await changeClimate("power", !climate.climate.power);
    if (a === "climate-mode")
      await changeClimate("mode", String(button.dataset.value));
    if (a === "climate-fan")
      await changeClimate("fan_speed", String(button.dataset.value));
    return;
  }
  if (!d) return;
  if (a === "details") {
    showDetails(d);
    return;
  }
  if (a === "power") {
    await change("power", d.power ? 0 : 1);
    return;
  }
  if (a === "white") {
    await change("temperature", Number(button.dataset.value));
    return;
  }
  if (a === "color") {
    await change("color", Number(button.dataset.value));
    return;
  }
  const slot = Number(button.dataset.slot);
  if (a === "save") {
    const value = drafts.get(d.id)?.bright ?? d.bright;
    await call("save_preset", { id: d.id, slot, value });
    toast(`Ayar ${slot + 1} · %${value} kaydedildi`);
    return;
  }
  if (a === "preset") {
    const id = d.id;
    pending.add(id);
    render();
    try {
      await call("apply_preset", { id, slot });
    } finally {
      pending.delete(id);
      render();
    }
    return;
  }
}
document.addEventListener("click", (e) => {
  const b = (e.target as Element).closest<HTMLButtonElement>(
    "button[data-action]",
  );
  if (b && !b.disabled) void action(b).catch((e) => toast(String(e), true));
});
document.addEventListener("input", (e) => {
  const el = e.target as HTMLInputElement,
    d = selected();
  if (el.id === "climate-temperature") {
    $("#climate-target-number").textContent = Number(el.value).toFixed(1);
    return;
  }
  if (!d) return;
  const draft = drafts.get(d.id) || {};
  if (el.id === "brightness") {
    draft.bright = Number(el.value);
    $("#bright-number").textContent = el.value;
    el.style.setProperty("--fill", el.value + "%");
  }
  if (el.id === "temperature") {
    draft.ct = Number(el.value);
    $("#ct-number").textContent = el.value;
  }
  drafts.set(d.id, draft);
});
document.addEventListener("change", (e) => {
  const el = e.target as HTMLInputElement;
  if (el.id === "climate-temperature")
    void changeClimate("target_temperature", Number(el.value));
  if (el.id === "brightness") void change("brightness", Number(el.value));
  if (el.id === "temperature") void change("temperature", Number(el.value));
  if (el.id === "color") void change("color", parseInt(el.value.slice(1), 16));
});
$(".titlebar").addEventListener("mousedown", (e) => {
  if (native && !(e.target as Element).closest("button"))
    void getCurrentWindow().startDragging();
});

function showDialog(body: string) {
  $("#dialog-body").innerHTML = body;
  icons();
  $<HTMLDialogElement>("#dialog").showModal();
}
const dialogClose = () =>
  `<button class="icon-button" data-action="dismiss" aria-label="Kapat">${icon("x")}</button>`;
function showDetails(d: Device) {
  showDialog(
    `<div class="dialog-heading"><h2>Ampul ayarları</h2>${dialogClose()}</div><form id="rename-form"><label>Ampul adı<input id="device-name" value="${esc(label(d))}" maxlength="40" placeholder="Örn. Çalışma lambası" autocomplete="off"></label><button class="primary" type="submit">İsmi kaydet</button></form><dl><dt>Model</dt><dd>${esc(d.model)}</dd><dt>Yerel adres</dt><dd>${esc(d.ip)}:${d.port}</dd><dt>Cihaz kimliği</dt><dd>${esc(d.id)}</dd><dt>Bağlantı</dt><dd>${d.connected ? "Bağlı" : esc(d.error || "Çevrimdışı")}</dd></dl><p class="dialog-note">İsim yalnızca EO-Light içinde kaydedilir. IP değişirse Yeniden tara’yı kullan; ampul kimliğiyle tanınır.</p>`,
  );
  $("#rename-form").addEventListener("submit", (e) => {
    e.preventDefault();
    void call("rename_device", {
      id: d.id,
      name: $<HTMLInputElement>("#device-name").value,
    })
      .then(() => {
        $<HTMLDialogElement>("#dialog").close();
        toast("Ampul adı kaydedildi");
      })
      .catch((e) => toast(String(e), true));
  });
}
function showNetwork() {
  showDialog(
    `<div class="dialog-heading"><h2>Ağdaki diğer cihazlar</h2>${dialogClose()}</div><p class="dialog-note">Son taramanın sonuçları. Cihaz türü yalnızca açıkça duyuruluyorsa tanınır; kontrol için markaya uygun bağlantı desteği gerekir.</p><div class="service-list">${state.services.map((d) => `<div class="service">${icon(d.kind === "climate" ? "air-vent" : "network")}<div><b>${esc(d.name)}</b><small>${d.kind === "climate" ? "Klima/termostat · " : ""}${esc(d.protocol)} · ${esc(d.ip)}</small></div></div>`).join("") || "<p>Son taramada ek cihaz bulunamadı.</p>"}</div>`,
  );
}

// Explicit development-only preview. Never enabled in the packaged application.
function demoCall(method: string, args: Record<string, unknown>) {
  const d = state.devices.find((d) => d.id === args.id);
  const climate = state.services.find((s) => s.id === args.id)?.climate;
  if (method === "snapshot") return state;
  if (method === "select_device") state.selected = String(args.id);
  if (method === "set_theme") state.theme = String(args.theme);
  if (method === "rename_device" && d) d.alias = String(args.name);
  if (method === "save_preset" && d)
    d.presets[Number(args.slot)] = Number(args.value);
  if (method === "control" && d) {
    if (args.action === "power") d.power = Boolean(args.value);
    if (args.action === "brightness") d.bright = Number(args.value);
    if (args.action === "temperature") {
      d.ct = Number(args.value);
      d.color_mode = 2;
    }
    if (args.action === "color") {
      d.rgb = Number(args.value);
      d.color_mode = 1;
    }
    d.latency_ms = 28;
  }
  if (method === "control_climate" && climate) {
    if (args.action === "power") climate.power = Boolean(args.value);
    if (args.action === "target_temperature") climate.target_c = Number(args.value);
    if (args.action === "mode") climate.mode = String(args.value);
    if (args.action === "fan_speed") climate.fan = String(args.value);
  }
  if (method === "apply_preset" && d) {
    d.power = true;
    d.bright = d.presets[Number(args.slot)];
  }
  if (method === "rescan") {
    state.scanning = true;
    setTimeout(() => {
      state.scanning = false;
      state.last_scan = Date.now() / 1000;
      render();
    }, 800);
  }
  render();
  return 28;
}
async function init() {
  if (demo) {
    const base = {
      ip: "203.0.113.5",
      port: 55443,
      name: "",
      connected: true,
      power: true,
      bright: 65,
      ct: 2700,
      rgb: 0xbd96f6,
      color_mode: 2,
      latency_ms: 28,
      error: "",
      presets: [15, 30, 60, 100],
    };
    state.devices = [
      {
        ...base,
        presets: [...base.presets],
        id: "one",
        alias: "Çalışma lambası",
        model: "test-mono",
        support: ["set_power", "set_bright"],
      },
      {
        ...base,
        presets: [...base.presets],
        id: "two",
        alias: "Renkli lamba",
        model: "test-rgb-a",
        support: ["set_power", "set_bright", "set_rgb", "set_ct_abx"],
      },
      {
        ...base,
        presets: [...base.presets],
        id: "three",
        alias: "Masa lambası",
        model: "test-rgb-b",
        power: false,
        bright: 30,
        support: ["set_power", "set_bright", "set_rgb", "set_ct_abx"],
      },
    ];
    state.selected = "one";
    state.last_scan = Date.now() / 1000;
    state.services = [
      { id: "tv", ip: "198.51.100.12", name: "Salon TV", protocol: "mDNS", kind: "other", climate: null },
      {
        id: "sample-climate",
        ip: "203.0.113.8",
        name: "Örnek klima",
        protocol: "Örnek sürücü",
        kind: "climate",
        climate: {
          power: true,
          target_c: 23.5,
          ambient_c: 25.1,
          mode: "cool",
          fan: "auto",
          min_c: 16,
          max_c: 30,
          modes: ["cool", "heat", "auto", "fan", "dry"],
          fan_modes: ["auto", "low", "medium", "high"],
          support: ["power", "target_temperature", "mode", "fan_speed"],
        },
      },
    ];
    current = "one";
    render(true);
    return;
  }
  if (native) {
    await listen<Snapshot>("state", (e) => accept(e.payload));
    accept(await call<Snapshot>("snapshot"));
    await call("ui_ready");
  } else {
    state.scan_error =
      "Bu bir arayüz önizlemesidir. Cihaz bağlantısı için EO-Light.exe’yi çalıştır.";
    render();
  }
}
render();
void init().catch((e) => toast(String(e), true));
