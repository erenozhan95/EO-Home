using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

public class DevicePicker : Form {
    public Device Selected;
    readonly ListBox list = new ListBox();
    readonly Label message = new Label();
    readonly Button scan = new ModernButton(), choose = new ModernButton();
    readonly Button openWeb = new ModernButton();
    readonly Label details = new Label();
    bool scanning;
    public DevicePicker(string currentIP, bool automatic = false) {
        Text = "Ağdaki cihazlar"; ClientSize = new Size(620, 570);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        BackColor = Theme.Background; ForeColor = Theme.Text;
        var title = new Label { Text = "Ağdaki cihazlar", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), Left = 22, Top = 20, Width = 560, Height = 38 };
        var hint = new Label { Text = "Marka bağımsız keşif: SSDP / UPnP ve mDNS.\nBir cihaz seçerek bağlantı ve kontrol seçeneklerini gör.", Left = 22, Top = 62, Width = 576, Height = 48 };
        list.SetBounds(22, 120, 576, 153); list.DisplayMember = "";
        list.BackColor=Theme.Card; list.ForeColor=Theme.Text; list.BorderStyle=BorderStyle.None;
        list.DrawMode=DrawMode.OwnerDrawFixed; list.ItemHeight=34;
        list.DrawItem += delegate(object sender, DrawItemEventArgs e) {
            if(e.Index<0)return;
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?Color.FromArgb(53,63,78):Theme.Card))e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,list.Items[e.Index].ToString(),Font,new Rectangle(e.Bounds.X+12,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height),selected?Theme.Accent:Theme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        };
        scan.Text = "Yeniden tara"; scan.SetBounds(22, 286, 170, 36);
        choose.Text = "Işığı kontrol et"; choose.SetBounds(418, 286, 180, 36); choose.Enabled = false;
        openWeb.Text = "Cihaz arayüzünü aç"; openWeb.SetBounds(202,286,206,36); openWeb.Enabled = false;
        details.SetBounds(22,337,576,84); details.Text = "Listeden bir cihaz seç.";
        message.SetBounds(22, 425, 576, 50); message.Text = "Aranıyor…";
        var ip = new TextBox { Text = currentIP, Left = 22, Top = 522, Width = 340 };
        var manual = new ModernButton { Text = "Işığa IP ile bağlan", Left = 418, Top = 517, Width = 180, Height = 36 };
        var manualHint = new Label { Text = "Elle ışık bağlantısı · Yeelight LAN · otomatik IP takibi yok", Left = 22, Top = 487, Width = 576, Height = 23 };
        foreach (var button in new Button[] { scan, choose, manual, openWeb }) { button.BackColor = Theme.Background; }
        Controls.AddRange(new Control[] { title, hint, list, scan, choose, openWeb, details, message, manualHint, ip, manual });
        list.SelectedIndexChanged += delegate { UpdateSelection(); };
        scan.Click += async delegate { await Search(); };
        choose.Click += delegate { var selected = list.SelectedItem as NetworkDevice; Selected = selected == null ? null : selected.Light; if (Selected != null && Selected.Compatible) DialogResult = DialogResult.OK; };
        openWeb.Click += delegate {
            var selected = list.SelectedItem as NetworkDevice;
            if (selected == null || String.IsNullOrEmpty(selected.WebUrl)) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(selected.WebUrl) { UseShellExecute = true }); }
            catch { message.Text = "Tarayıcı açılamadı: " + selected.WebUrl; }
        };
        manual.Click += delegate {
            IPAddress address;
            if (!IPAddress.TryParse(ip.Text.Trim(), out address) || address.AddressFamily != AddressFamily.InterNetwork) { message.Text = "Geçerli bir IPv4 adresi gir."; return; }
            Selected = new Device { IP = address.ToString() }; DialogResult = DialogResult.OK;
        };
        ShowCached();
    }
    void ShowCached() {
        var result = NetworkDiscovery.LastResult;
        list.Items.Clear();
        foreach (var device in result.Devices) list.Items.Add(device);
        message.Text = result.Devices.Count == 0 ? "Kayıtlı tarama sonucu yok. Yeniden tara düğmesine bas." : result.Devices.Count + " cihaz · Son tarama sonuçları. Güncellemek için Yeniden tara.";
        if (result.Errors.Count > 0) message.Text += "\nEksik tarama: " + String.Join(", ", result.Errors);
        UpdateSelection();
    }
    void UpdateSelection() {
        var device = list.SelectedItem as NetworkDevice;
        choose.Enabled = !scanning && device != null && device.Light != null;
        openWeb.Enabled = !scanning && device != null && !String.IsNullOrEmpty(device.WebUrl);
        details.Text = device == null ? "Listeden bir cihaz seç." : "Adres: " + device.IP + "\nKeşif: " + device.Protocol + "\n" + (device.Light != null ? "Işık kontrolü hazır · aç/kapat ve parlaklık" : String.IsNullOrEmpty(device.WebUrl) ? "Cihaz bulundu. Doğrudan kontrol için bu cihaza uygun bağlantı modülü gerekiyor." : "Cihazın yerel web arayüzü kullanılabilir.");
    }
    async Task Search() {
        if (scanning) return;
        scanning = true; scan.Enabled = choose.Enabled = openWeb.Enabled = false; list.Items.Clear(); message.Text = "Yerel ağda cihazlar ve hizmetler aranıyor…";
        try {
            var result = await Task.Run(() => NetworkDiscovery.Scan());
            if (IsDisposed) return;
            ShowCached();
        } catch (Exception) { if (!IsDisposed) message.Text = "Ağ taraması yapılamadı. Ağ bağlantını ve güvenlik duvarını kontrol et."; }
        finally { scanning = false; if (!IsDisposed) { scan.Enabled = true; UpdateSelection(); } }
    }
}
