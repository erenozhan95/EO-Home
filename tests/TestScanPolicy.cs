using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
class TestScanPolicy {
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread] static int Main() {
        try { Test(); Console.WriteLine("PASS: saved-ID commands and reconnect without discovery; picker reuses cache; power, presets, UI regression"); return 0; }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex); return 1; }
    }
    static void Test() {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; int accepted = 0;
        var server = Task.Run(() => {
            var json = new JavaScriptSerializer();
            for (int batch = 0; batch < 2; batch++) {
                using (var client = listener.AcceptTcpClient()) {
                    accepted++; client.ReceiveTimeout = 5000;
                    using (var reader = new StreamReader(client.GetStream())) {
                        for (int i = 0; i < (batch == 0 ? 2 : 1); i++) {
                            var request = json.Deserialize<Dictionary<string, object>>(reader.ReadLine());
                            string method = (string)request["method"];
                            if (method == "set_power") {
                                var args = (System.Collections.ArrayList)request["params"];
                                Assert((string)args[1] == "sudden" && Convert.ToInt32(args[2]) == 0, "Power uses transition delay");
                            }
                            object[] result = method == "get_prop" ? new object[] { "off", "0" } : new object[] { "ok" };
                            byte[] reply = Encoding.UTF8.GetBytes("{\"method\":\"props\",\"params\":{\"power\":\"off\"}}\r\n" + json.Serialize(new { id = request["id"], result = result }) + "\r\n");
                            client.GetStream().Write(reply, 0, reply.Length / 2);
                            client.GetStream().Write(reply, reply.Length / 2, reply.Length - reply.Length / 2);
                        }
                    }
                }
            }
        });
        using (var session = new LampSession(new Device { IP = "127.0.0.1", Port = port, Id = "saved-lamp-id", CapabilitiesKnown = true })) {
            var state = session.Send("get_prop", new object[] { "power", "bright" });
            Assert(!LightWindow.ReadBrightness(state).HasValue, "Off with zero brightness rejected");
            session.Send("set_power", new object[] { "off", "sudden", 0 });
            // The fake device closes its socket after off; on must reconnect itself.
            session.Send("set_power", new object[] { "on", "sudden", 0 });
        }
        Assert(server.Wait(7000), "Server did not finish"); listener.Stop();
        Assert(accepted == 2, "Connection not reused or not recovered");
        Assert(!LightWindow.ReadBrightness(new object[] { "off", "" }).HasValue, "Off with missing brightness rejected");
        string config = Path.GetFullPath("work/test-v2-settings.json");
        if (File.Exists(config)) File.Delete(config);
        var prefs = new Preferences { Presets = new int[] { 7, 26, 64, 99 } }; prefs.Save(config);
        Assert(Preferences.Load(config).Presets[2] == 64, "Settings did not persist");
        NetworkDiscovery.LastResult = new NetworkScanResult();
        NetworkDiscovery.LastResult.Devices.Add(new NetworkDevice { IP = "203.0.113.50", Name = "Cached lamp", Protocol = "Test" });
        Application.EnableVisualStyles();
        using(var picker = new DevicePicker("203.0.113.50")) {
            var list = (ListBox)typeof(DevicePicker).GetField("list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(picker);
            if(list.Items.Count != 1) throw new Exception("Picker did not reuse cached result");
            picker.StartPosition = FormStartPosition.Manual; picker.Location = new System.Drawing.Point(-10000,-10000);picker.ShowInTaskbar=false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            picker.Show(); Application.DoEvents(); picker.Close();
            if(watch.ElapsedMilliseconds > 1500) throw new Exception("Opening cached picker blocked");
            if(NetworkDiscovery.LastResult.Devices[0].Name != "Cached lamp") throw new Exception("Unexpected scan replaced cache");
        }
        using (var form = new LightWindow(config, false)) {
            var slider = (ModernSlider)typeof(LightWindow).GetField("brightness", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            slider.Value = 43;
            typeof(LightWindow).GetMethod("SavePreset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { 1 });
            Assert(Preferences.Load(config).Presets[1] == 43, "Preset save button failed");
            var buttons = (Button[])typeof(LightWindow).GetField("presets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            Assert(buttons.Length == 4 && buttons[1].Text == "%43", "Preset labels wrong");
            foreach (Control control in form.Controls) Assert(!(control is TextBox), "IP exposed in main screen");
            form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-10000, -10000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents();
            using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height)) {
                form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height)); bitmap.Save("work/v2-preview.png");
            }
            form.Close();
        }
    }
}
