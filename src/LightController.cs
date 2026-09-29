using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("LightController")]
[assembly: System.Reflection.AssemblyProduct("LightController")]
[assembly: System.Reflection.AssemblyDescription("Yerel lamba kontrolü")]
[assembly: System.Reflection.AssemblyVersion("4.1.0.0")]

public class Device {
    public string IP; public int Port = 55443; public string Model = "Yeelight";
    public string Id = "", Name = ""; public bool Compatible = true;
    public string[] Commands = new string[0]; public bool CapabilitiesKnown;
    public bool Supports(string command) { return CapabilitiesKnown && Array.IndexOf(Commands ?? new string[0], command) >= 0; }
    public override string ToString() { return (String.IsNullOrEmpty(Name) ? Model : Name) + " · " + IP + (Compatible ? "" : " · desteklenmiyor"); }
}

public sealed class LampSession : ILightController {
    TcpClient client; NetworkStream stream; string pending = ""; int sequence;
    public Device Device { get; private set; }
    public object[] ReadState() { return Send("get_prop", new object[] { "power", "bright", "ct", "rgb", "color_mode" }); }
    public void SetPower(bool enabled) { Send("set_power", new object[] { enabled ? "on" : "off", "sudden", 0 }); }
    public void SetBrightness(int value) { Send("set_bright", new object[] { value, "sudden", 0 }); }
    public void SetColor(int rgb) {
        if (!Device.Supports("set_rgb")) throw new NotSupportedException("Bu cihaz RGB renk kontrolünü desteklemiyor.");
        if (rgb < 1 || rgb > 0xffffff) throw new ArgumentOutOfRangeException("rgb");
        Send("set_rgb", new object[] { rgb, "smooth", 200 });
    }
    public void SetTemperature(int kelvin) {
        if (!Device.Supports("set_ct_abx")) throw new NotSupportedException("Bu cihaz beyaz sıcaklığı kontrolünü desteklemiyor.");
        if (kelvin < 1700 || kelvin > 6500) throw new ArgumentOutOfRangeException("kelvin");
        Send("set_ct_abx", new object[] { kelvin, "smooth", 200 });
    }
    public LampSession(Device device) { Device = device; }
    public void Dispose() { if (client != null) client.Close(); client = null; stream = null; pending = ""; }
    public object[] Send(string method, object[] args) {
        // All operations are serialized by the UI gate. Set commands are idempotent.
        for (int attempt = 0; ; attempt++) {
            try { return Exchange(method, args); }
            catch (IOException) { Dispose(); if (attempt != 0) throw; }
            catch (SocketException) { Dispose(); if (attempt != 0) throw; }
            catch (TimeoutException) { Dispose(); if (attempt != 0) throw; }
        }
    }
    object[] Exchange(string method, object[] args) {
        if (client == null) {
            IPAddress address;
            if (!IPAddress.TryParse(Device.IP, out address)) throw new Exception("IP adresi geçersiz.");
            client = new TcpClient(); client.NoDelay = true;
            var connection = client.BeginConnect(address, Device.Port, null, null);
            using (connection.AsyncWaitHandle) {
                if (!connection.AsyncWaitHandle.WaitOne(1200)) throw new TimeoutException("Lambaya ulaşılamadı.");
                client.EndConnect(connection);
            }
            stream = client.GetStream(); stream.WriteTimeout = 1200;
        }
        var json = new JavaScriptSerializer(); int id = ++sequence;
        byte[] data = Encoding.UTF8.GetBytes(json.Serialize(new { id = id, method = method, @params = args }) + "\r\n");
        stream.Write(data, 0, data.Length);
        var end = DateTime.UtcNow.AddMilliseconds(1500);
        byte[] buffer = new byte[4096];
        while (DateTime.UtcNow < end) {
            int newline;
            while ((newline = pending.IndexOf('\n')) >= 0) {
                string line = pending.Substring(0, newline); pending = pending.Substring(newline + 1);
                var reply = json.Deserialize<Dictionary<string, object>>(line);
                if (!reply.ContainsKey("id") || Convert.ToInt32(reply["id"]) != id) continue;
                if (reply.ContainsKey("error")) throw new InvalidOperationException("Lamba komutu reddetti: " + json.Serialize(reply["error"]));
                if (!reply.ContainsKey("result")) throw new InvalidOperationException("Geçersiz cihaz yanıtı.");
                var items = reply["result"] as System.Collections.IEnumerable;
                if (items == null || reply["result"] is string) throw new InvalidOperationException("Geçersiz cihaz yanıtı.");
                var result = new List<object>(); foreach (object item in items) result.Add(item);
                return result.ToArray();
            }
            stream.ReadTimeout = Math.Max(1, (int)(end - DateTime.UtcNow).TotalMilliseconds);
            int count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0) throw new IOException("Bağlantı kapandı.");
            pending += Encoding.UTF8.GetString(buffer, 0, count);
            if (pending.Length > 65536) { Dispose(); throw new IOException("Yanıt çok uzun."); }
        }
        throw new TimeoutException("Lamba yanıt vermedi.");
    }
}

public class Preferences {
    public string IP = "";
    public string DeviceId = ""; public int Port = 55443;
    public string Model = "", DeviceName = "";
    public int[] Presets = new int[] { 10, 30, 60, 100 };
    public static Preferences Load(string path) {
        Preferences p;
        try { p = new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path)); }
        catch { p = new Preferences(); }
        if (p == null) p = new Preferences();
        IPAddress ip;
        if (!IPAddress.TryParse(p.IP, out ip)) p.IP = "";
        if (p.Port < 1 || p.Port > 65535) p.Port = 55443;
        if (p.Presets == null || p.Presets.Length != 4) p.Presets = new int[] { 10, 30, 60, 100 };
        for (int i = 0; i < 4; i++) p.Presets[i] = Math.Max(1, Math.Min(100, p.Presets[i]));
        return p;
    }
    public void Save(string path) {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this));
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
    }
}

public partial class LightWindow : Form {
    readonly System.Threading.SemaphoreSlim gate = new System.Threading.SemaphoreSlim(1, 1);
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly NotifyIcon tray = new NotifyIcon();
    readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
    readonly string path;
    Preferences prefs; ILightController session;
    Label state, status, percent, connection;
    ModernSlider brightness = new ModernSlider();
    Button on, off, apply, settings; Button[] presets = new Button[4];
    bool closing, commandBusy, dirty;
    public static string DefaultConfigPath() {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        string directory = Path.Combine(root, "Ayarlar");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "ayarlar.json");
        string previous = Path.Combine(root, "ayarlar.json");
        if (!File.Exists(destination) && File.Exists(previous)) File.Copy(previous, destination);
        string legacy = Path.Combine(root, "son-ip.txt");
        string legacyDestination = Path.Combine(directory, "son-ip.txt");
        if (!File.Exists(destination) && !File.Exists(legacyDestination) && File.Exists(legacy)) File.Copy(legacy, legacyDestination);
        return destination;
    }
    public LightWindow() : this(DefaultConfigPath(), true) {}
    public LightWindow(string configPath, bool automatic) {
        path = configPath; prefs = Preferences.Load(path);
        string legacy = Path.Combine(Path.GetDirectoryName(path), "son-ip.txt");
        IPAddress oldIP;
        if (!File.Exists(path) && File.Exists(legacy) && IPAddress.TryParse(File.ReadAllText(legacy).Trim(), out oldIP)) prefs.IP = oldIP.ToString();
        session = NewSession();
        BuildDashboard();
        timer.Interval = 10000;
        timer.Tick += async delegate { if (!commandBusy && gate.CurrentCount == 1) await RefreshState(); };
        if (automatic) Shown += async delegate { await StartupScan(); };
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        tray.Icon = Icon;
        tray.Text = "LightController · açmak için çift tıkla";
        trayMenu.Items.Add("LightController'ı aç", null, delegate { RestoreWindow(); });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Çıkış", null, delegate { Close(); });
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += delegate { RestoreWindow(); };
        Resize += delegate {
            if (!closing && WindowState == FormWindowState.Minimized) {
                tray.Visible = true;
                ShowInTaskbar = false;
                Hide();
            }
        };
        FormClosed += delegate {
            closing = true; timer.Stop(); timer.Dispose();
            tray.Visible = false; tray.Dispose(); trayMenu.Dispose();
            // Dispose after any in-flight operation has completed.
            Task.Run(async delegate { await gate.WaitAsync(); try { session.Dispose(); } finally { gate.Release(); } });
        };
    }
    void RestoreWindow() {
        if (closing || IsDisposed) return;
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Show();
        Activate();
        tray.Visible = false;
    }
    Label LabelAt(string text, int x, int y, int width, int height, int size, bool bold) {
        var label = new Label { Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
        label.SetBounds(x, y, width, height); Controls.Add(label); return label;
    }
    Button ButtonAt(string text, int x, int y, int width, EventHandler click) {
        var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(42, 51, 63), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.SetBounds(x, y, width, 40); button.Click += click; Controls.Add(button); return button;
    }
    void Busy(bool value) {
        commandBusy = value;
        on.Enabled = off.Enabled = apply.Enabled = settings.Enabled = !value;
        foreach (var button in presets) button.Enabled = !value;
        UpdateCapabilities();
    }
    public static int? ReadBrightness(object[] result) {
        if (result.Length < 1 || (Convert.ToString(result[0]) != "on" && Convert.ToString(result[0]) != "off")) throw new InvalidOperationException("Lambanın durumu okunamadı.");
        int value;
        return result.Length > 1 && int.TryParse(Convert.ToString(result[1]), out value) && value >= 1 && value <= 100 ? (int?)value : null;
    }
    async Task RefreshState() {
        await gate.WaitAsync();
        try {
            if (closing) return;
            if (String.IsNullOrWhiteSpace(session.Device.IP)) {
                state.Text = "Ampul seç";
                connection.Text = "● Bağlı cihaz yok";
                status.Text = "Cihazları yönet bölümünden bir ampul seçebilirsin.";
                return;
            }
            object[] result = await Task.Run(() => session.ReadState());
            int? value = ReadBrightness(result);
            if (closing) return;
            state.Text = Convert.ToString(result[0]) == "on" ? "Işık açık" : "Işık kapalı";
            if (value.HasValue && !dirty) brightness.Value = value.Value;
            connection.Text = "● Bağlı";
            SaveResolvedAddress();
            UpdateExtraState(result);
        } catch (Exception) {
            if (!closing) {
                connection.Text = "● Bağlantı yok · Yeniden tara ile güncelle";
                state.Text = "Lambaya ulaşılamıyor";
            }
        } finally { gate.Release(); }
    }
    async Task Command(Func<Task> work) {
        if (commandBusy || closing) return;
        Busy(true); status.Text = "Uygulanıyor…";
        await gate.WaitAsync();
        try {
            if (closing) return;
            await work();
            if (!closing) { connection.Text = "● Bağlı"; status.Text = "Uygulandı."; SaveResolvedAddress(); }
        } catch (Exception) {
            if (!closing) {
                connection.Text = "● Bağlantı bekleniyor · otomatik deneniyor";
                status.Text = "Komut doğrulanamadı. Bağlantı otomatik yenilenecek; tekrar deneyebilirsin.";
            }
        } finally { gate.Release(); if (!closing) Busy(false); }
    }
    public Task Power(bool enabled) {
        return Command(async delegate {
            await Task.Run(() => session.SetPower(enabled));
            if (!closing) { state.Text = enabled ? "Işık açık" : "Işık kapalı"; lampVisual.Lit = enabled; lampVisual.Invalidate(); }
        });
    }
    public Task SetBrightness(int value, bool turnOn) {
        return Command(async delegate {
            await Task.Run(() => {
                session.SetBrightness(value);
                if (turnOn) session.SetPower(true);
            });
            if (!closing) { brightness.Value = value; dirty = false; if (turnOn) { state.Text = "Işık açık"; lampVisual.Lit = true; lampVisual.Invalidate(); } }
        });
    }
    void SavePreset(int index) {
        int previous = prefs.Presets[index]; prefs.Presets[index] = brightness.Value;
        try { prefs.Save(path); presets[index].Text = "%" + prefs.Presets[index]; status.Text = "Kayıt " + (index + 1) + " kaydedildi: %" + prefs.Presets[index]; }
        catch (Exception) { prefs.Presets[index] = previous; status.Text = "Ayar kaydedilemedi. Uygulama klasörünün yazılabilir olduğunu kontrol et."; }
    }
    Device CachedSelection() {
        foreach (var item in NetworkDiscovery.LastResult.Devices) {
            if (item.Light == null) continue;
            if (!String.IsNullOrEmpty(prefs.DeviceId) && String.Equals(item.Light.Id, prefs.DeviceId, StringComparison.OrdinalIgnoreCase)) return item.Light;
            if (String.IsNullOrEmpty(prefs.DeviceId) && item.IP == prefs.IP) return item.Light;
        }
        return null;
    }
    async Task StartupScan() {
        Busy(true); connection.Text = "● İlk ağ taraması…";
        try {
            await Task.Run(() => NetworkDiscovery.Scan());
            if (closing) return;
            var device = CachedSelection();
            if (device != null) { session.Dispose(); session = new LampSession(device); SaveResolvedAddress(); UpdateCapabilities(); }
            status.Text = "Tarama tamamlandı. Listeyi Yeniden tara ile güncelleyebilirsin.";
        } catch (Exception) { if (!closing) status.Text = "İlk tarama tamamlanamadı. Cihazları yönet → Yeniden tara."; }
        finally { if (!closing) Busy(false); }
        if (!closing) { timer.Start(); await RefreshState(); }
    }
    void OpenSettings() {
        if (commandBusy) return;
        timer.Stop();
        using (var dialog = new DevicePicker(prefs.IP)) {
            if (dialog.ShowDialog(this) == DialogResult.OK) SelectDevice(dialog.Selected);
            else { var current = CachedSelection(); if (current != null && (current.IP != session.Device.IP || current.Port != session.Device.Port)) SelectDevice(current); }
        }
        if (!closing) timer.Start();
    }
    ILightController NewSession() { return new LampSession(new Device { IP = prefs.IP, Port = prefs.Port, Id = prefs.DeviceId, Model = String.IsNullOrEmpty(prefs.Model) ? "Yeelight" : prefs.Model, Name = prefs.DeviceName }); }
    void SaveResolvedAddress() {
        if (prefs.IP == session.Device.IP && prefs.Port == session.Device.Port && prefs.DeviceId == session.Device.Id && prefs.Model == session.Device.Model && prefs.DeviceName == session.Device.Name) return;
        prefs.IP = session.Device.IP; prefs.Port = session.Device.Port; prefs.DeviceId = session.Device.Id; prefs.Model = session.Device.Model; prefs.DeviceName = session.Device.Name;
        try { prefs.Save(path); } catch { status.Text = "Bağlı; yeni adres diske kaydedilemedi."; }
    }
    async void SelectDevice(Device device) {
        if (device == null || closing) return;
        await gate.WaitAsync();
        try {
            if (closing) return;
            string oldIP = prefs.IP, oldId = prefs.DeviceId; int oldPort = prefs.Port;
            prefs.IP = device.IP; prefs.Port = device.Port; prefs.DeviceId = device.Id;
            try { prefs.Save(path); }
            catch { prefs.IP = oldIP; prefs.Port = oldPort; prefs.DeviceId = oldId; status.Text = "Cihaz seçimi kaydedilemedi."; return; }
            session.Dispose(); session = new LampSession(device); dirty = temperatureDirty = colorDirty = false; UpdateCapabilities();
        }
        finally { gate.Release(); }
        if (!closing) await RefreshState();
    }
    [STAThread] public static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LightWindow());
    }
}
