using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

public static class Discovery {
    public static Device Parse(string text, IPAddress sender) {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n')) {
            int colon = line.IndexOf(':');
            if (colon > 0) headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
        }
        string location; Uri url; IPAddress address;
        if (!headers.TryGetValue("location", out location) || !Uri.TryCreate(location, UriKind.Absolute, out url) || url.Scheme != "yeelight" || !IPAddress.TryParse(url.Host, out address) || address.AddressFamily != AddressFamily.InterNetwork || !address.Equals(sender)) return null;
        int port = url.Port;
        if (port < 1 || port > 65535) return null;
        string id, model, name, support;
        headers.TryGetValue("id", out id); headers.TryGetValue("model", out model);
        headers.TryGetValue("name", out name); headers.TryGetValue("support", out support);
        if (String.IsNullOrWhiteSpace(id)) return null;
        var commands = new HashSet<string>((support ?? "").Split(' '));
        return new Device { IP = address.ToString(), Port = port, Id = id, Model = model ?? "Yeelight", Name = name ?? "", Commands = commands.Where(c => c.Length > 0).ToArray(), CapabilitiesKnown = support != null, Compatible = commands.Contains("get_prop") && commands.Contains("set_power") && commands.Contains("set_bright") };
    }
    public static List<Device> Scan(bool thorough = false) {
        var addresses = new HashSet<IPAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            foreach (var address in nic.GetIPProperties().UnicastAddresses)
                if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address)) addresses.Add(address.Address);
        }
        if (addresses.Count == 0) addresses.Add(IPAddress.Any);
        var tasks = addresses.Select(address => Task.Run(() => ScanInterface(address))).ToArray();
        Task.WaitAll(tasks);
        var found = tasks.SelectMany(task => task.Result).GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
        if (found.Count == 0 || thorough) {
            var unicast = addresses.Select(address => Task.Run(() => ScanInterface(address, true))).ToArray();
            Task.WaitAll(unicast);
            foreach (var device in unicast.SelectMany(task => task.Result)) if (!found.Any(item => item.Id == device.Id)) found.Add(device);
        }
        if (found.Count == 0) {
            var direct = ScanPorts();
            foreach (var device in direct) if (!found.Any(item => item.IP == device.IP)) found.Add(device);
        }
        return found.OrderBy(device => device.IP).ToList();
    }
    static List<Device> ScanInterface(IPAddress local, bool sweep = false) {
        var found = new List<Device>();
        try {
            using (var udp = new UdpClient(new IPEndPoint(local, 0))) {
                udp.Ttl = 2;
                if (!local.Equals(IPAddress.Any)) udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                byte[] query = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1982\r\nMAN: \"ssdp:discover\"\r\nST: wifi_bulb\r\n\r\n");
                var destination = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1982);
                udp.Send(query, query.Length, destination); udp.Send(query, query.Length, destination);
                // Inspired by smart-gadget's unicast discovery fallback (MIT).
                // Query the local /24 with UDP instead of consuming TCP slots on each lamp.
                if (sweep && !local.Equals(IPAddress.Any)) foreach (string ip in Targets(local, IPAddress.Parse("255.255.255.0"))) udp.Send(query, query.Length, new IPEndPoint(IPAddress.Parse(ip),1982));
                DateTime end = DateTime.UtcNow.AddSeconds(2.5);
                while (DateTime.UtcNow < end) {
                    udp.Client.ReceiveTimeout = Math.Max(1, (int)(end - DateTime.UtcNow).TotalMilliseconds);
                    var sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packet;
                    try { packet = udp.Receive(ref sender); } catch (SocketException ex) { if (ex.SocketErrorCode == SocketError.TimedOut) break; throw; }
                    Device device = Parse(Encoding.UTF8.GetString(packet), sender.Address);
                    if (device != null) found.Add(device);
                }
            }
        } catch (SocketException) { }
        return found;
    }
    public static Device Match(IEnumerable<Device> devices, string id) {
        return devices.FirstOrDefault(device => device.Compatible && String.Equals(device.Id, id, StringComparison.OrdinalIgnoreCase));
    }
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    static extern int SendARP(int destination, int source, byte[] mac, ref int length);
    public static string Mac(string ip) {
        try {
            var bytes = IPAddress.Parse(ip).GetAddressBytes();
            byte[] mac = new byte[6]; int length = mac.Length;
            if (SendARP(BitConverter.ToInt32(bytes, 0), 0, mac, ref length) != 0 || length != 6) return "";
            return "mac:" + BitConverter.ToString(mac).ToLowerInvariant();
        } catch { return ""; }
    }
    public static List<string> Targets(IPAddress address, IPAddress mask) {
        byte[] a = address.GetAddressBytes(), m = mask.GetAddressBytes();
        uint ip = ((uint)a[0]<<24)|((uint)a[1]<<16)|((uint)a[2]<<8)|a[3];
        uint subnet = ((uint)m[0]<<24)|((uint)m[1]<<16)|((uint)m[2]<<8)|m[3];
        // Bound large networks to the computer's /24; never enumerate the Internet.
        if (~subnet > 511) subnet = 0xffffff00;
        uint first = ip & subnet, last = first | ~subnet;
        var result = new List<string>();
        for (uint value = first + 1; value < last; value++) {
            if (value == ip) continue;
            result.Add(new IPAddress(new byte[] { (byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value }).ToString());
        }
        return result;
    }
    public static List<Device> ScanPorts() {
        var targets = new HashSet<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()) {
            if (nic.OperationalStatus != OperationalStatus.Up || (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)) continue;
            var properties = nic.GetIPProperties();
            if (!properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) continue;
            foreach (var address in properties.UnicastAddresses) {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.IPv4Mask == null) continue;
                foreach (string ip in Targets(address.Address, address.IPv4Mask)) targets.Add(ip);
            }
        }
        var found = new List<Device>();
        Parallel.ForEach(targets, new ParallelOptions { MaxDegreeOfParallelism = 24 }, ip => {
            try {
                using (var client = new TcpClient()) {
                    var connect = client.BeginConnect(ip, 55443, null, null);
                    using (connect.AsyncWaitHandle) { if (!connect.AsyncWaitHandle.WaitOne(250)) return; client.EndConnect(connect); }
                    client.ReceiveTimeout = client.SendTimeout = 600;
                    var stream = client.GetStream();
                    byte[] query = Encoding.ASCII.GetBytes("{\"id\":987,\"method\":\"get_prop\",\"params\":[\"power\",\"bright\"]}\r\n");
                    stream.Write(query, 0, query.Length);
                    byte[] buffer = new byte[4096]; string pending = ""; DateTime end = DateTime.UtcNow.AddMilliseconds(1000);
                    while (DateTime.UtcNow < end) {
                        int count = stream.Read(buffer, 0, buffer.Length); if (count == 0) return;
                        pending += Encoding.UTF8.GetString(buffer, 0, count); if (pending.Length > 8192) return;
                        int newline;
                        while ((newline = pending.IndexOf('\n')) >= 0) {
                            string line = pending.Substring(0, newline); pending = pending.Substring(newline + 1);
                            var reply = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(line);
                            if (!reply.ContainsKey("id") || Convert.ToInt32(reply["id"]) != 987 || !reply.ContainsKey("result")) continue;
                            var result = reply["result"] as System.Collections.ArrayList;
                            if (result == null) return;
                            LightWindow.ReadBrightness(result.ToArray());
                            var device = new Device { IP = ip, Id = Mac(ip), Model = "Yeelight (yerel bağlantı)" };
                            lock (found) found.Add(device);
                            return;
                        }
                    }
                }
            } catch { }
        });
        return found;
    }
}
