using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class NetworkDevice {
    public string Id, IP, Name, Protocol, WebUrl = "";
    public Device Light;
    public override string ToString() { return Name + " · " + IP; }
}
public interface IDiscoveryProvider {
    string Name { get; }
    List<NetworkDevice> Scan();
}
public interface ILightController : IDisposable {
    Device Device { get; }
    object[] ReadState();
    void SetPower(bool enabled);
    void SetBrightness(int value);
    void SetColor(int rgb);
    void SetTemperature(int kelvin);
}
public class LightDiscoveryProvider : IDiscoveryProvider {
    public string Name { get { return "Yeelight LAN"; } }
    public List<NetworkDevice> Scan() {
        return Discovery.Scan(true).Select(d => new NetworkDevice { Id = d.Id, IP = d.IP, Name = String.IsNullOrWhiteSpace(d.Name) ? d.Model : d.Name, Protocol = Name, Light = d.Compatible ? d : null }).ToList();
    }
}
public class NetworkScanResult {
    public List<NetworkDevice> Devices = new List<NetworkDevice>();
    public List<string> Errors = new List<string>();
}
public static class NetworkDiscovery {
    public static NetworkScanResult LastResult = new NetworkScanResult();
    public static List<IPAddress> Interfaces() {
        return NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses).Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)).Select(a => a.Address).Distinct().ToList();
    }
    public static NetworkScanResult Scan() {
        IDiscoveryProvider[] providers = { new LightDiscoveryProvider(), new SsdpProvider(), new MdnsProvider() };
        var result = new NetworkScanResult();
        Parallel.ForEach(providers, provider => {
            try { var found = provider.Scan(); lock (result) result.Devices.AddRange(found); }
            catch (Exception) { lock (result) result.Errors.Add(provider.Name); }
        });
        result.Devices = Merge(result.Devices);
        LastResult = result;
        return result;
    }
    public static List<NetworkDevice> Merge(IEnumerable<NetworkDevice> devices) {
        // Keep all discovered protocols on the host and prefer its available light controller.
        return devices.GroupBy(d => d.IP).Select(group => {
            var best = group.FirstOrDefault(d => d.Light != null) ?? group.FirstOrDefault(d => d.Protocol == "mDNS / DNS-SD") ?? group.First();
            best.Protocol = String.Join(" + ", group.Select(d => d.Protocol).Distinct());
            best.WebUrl = group.Select(d => d.WebUrl).FirstOrDefault(url => !String.IsNullOrEmpty(url)) ?? "";
            return best;
        }).OrderBy(d => d.Name).ToList();
    }
}
public class SsdpProvider : IDiscoveryProvider {
    public string Name { get { return "SSDP / UPnP"; } }
    public static NetworkDevice Parse(string text, IPAddress sender) {
        var headers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.Split('\n')) { int i = line.IndexOf(':'); if (i > 0) headers[line.Substring(0,i).Trim()] = line.Substring(i+1).Trim(); }
        string usn, type, server;
        if (!headers.TryGetValue("usn", out usn) || !headers.TryGetValue("st", out type)) return null;
        headers.TryGetValue("server", out server);
        string title = type.StartsWith("urn:") ? type.Split(':').Reverse().Skip(1).FirstOrDefault() : "UPnP cihazı";
        return new NetworkDevice { Id = usn.Split(new[] { "::" },StringSplitOptions.None)[0], IP = sender.ToString(), Name = String.IsNullOrEmpty(title) ? "UPnP cihazı" : title, Protocol = "SSDP / UPnP" };
    }
    public List<NetworkDevice> Scan() {
        var found = new List<NetworkDevice>();
        Parallel.ForEach(NetworkDiscovery.Interfaces(), local => {
            using (var udp = new UdpClient(new IPEndPoint(local, 0))) {
                udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                udp.Ttl = 2;
                byte[] query = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: ssdp:all\r\n\r\n");
                var destination = new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900);
                udp.Send(query,query.Length,destination);
                DateTime end = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < end) {
                    udp.Client.ReceiveTimeout = Math.Max(1,(int)(end-DateTime.UtcNow).TotalMilliseconds);
                    var sender = new IPEndPoint(IPAddress.Any,0); byte[] bytes;
                    try { bytes = udp.Receive(ref sender); } catch (SocketException ex) { if (ex.SocketErrorCode == SocketError.TimedOut) break; throw; }
                    var device = Parse(Encoding.UTF8.GetString(bytes),sender.Address);
                    if (device != null) lock(found) found.Add(device);
                }
            }
        });
        return found;
    }
}
public class DnsRecord {
    public string Name, Target, Address; public int Type, Port;
}
public class MdnsProvider : IDiscoveryProvider {
    public string Name { get { return "mDNS / DNS-SD"; } }
    static int U16(byte[] data, int offset) { if (offset+2 > data.Length) throw new FormatException(); return (data[offset]<<8)|data[offset+1]; }
    public static string ReadName(byte[] data, ref int position) {
        int cursor = position, hops = 0; bool jumped = false; var labels = new List<string>();
        while (true) {
            if (++hops > 128 || cursor >= data.Length) throw new FormatException("Invalid DNS name");
            int length = data[cursor++];
            if ((length & 192) == 192) {
                if (cursor >= data.Length) throw new FormatException();
                if (!jumped) position = cursor+1;
                cursor = ((length & 63)<<8)|data[cursor]; jumped = true; continue;
            }
            if (length == 0) { if (!jumped) position = cursor; break; }
            if (length > 63 || cursor+length > data.Length) throw new FormatException();
            labels.Add(Encoding.UTF8.GetString(data,cursor,length)); cursor += length;
            if (!jumped) position = cursor;
        }
        return String.Join(".",labels);
    }
    public static List<DnsRecord> Parse(byte[] data) {
        if (data.Length < 12 || (data[2] & 128) == 0) return new List<DnsRecord>();
        int pos = 12, questions = U16(data,4), count = U16(data,6)+U16(data,8)+U16(data,10);
        if (questions+count > 512) throw new FormatException();
        for(int i=0;i<questions;i++) { ReadName(data,ref pos); pos += 4; if(pos>data.Length) throw new FormatException(); }
        var records = new List<DnsRecord>();
        for(int i=0;i<count;i++) {
            string name = ReadName(data,ref pos); int type = U16(data,pos), length = U16(data,pos+8); pos += 10;
            int end = pos+length; if(end>data.Length) throw new FormatException();
            var record = new DnsRecord { Name=name,Type=type };
            int p=pos;
            if(type==12) record.Target=ReadName(data,ref p);
            if(type==33 && length>=7) { record.Port=U16(data,pos+4); p=pos+6; record.Target=ReadName(data,ref p); }
            if(type==1 && length==4) record.Address=new IPAddress(data.Skip(pos).Take(4).ToArray()).ToString();
            records.Add(record); pos=end;
        }
        return records;
    }
    public static byte[] Query(string name, int type=12) {
        var bytes = new List<byte>(new byte[] {0,0,0,0,0,1,0,0,0,0,0,0});
        foreach(string label in name.TrimEnd('.').Split('.')) { byte[] text=Encoding.UTF8.GetBytes(label); if(text.Length>63) throw new FormatException(); bytes.Add((byte)text.Length); bytes.AddRange(text); }
        bytes.AddRange(new byte[] {0,(byte)(type>>8),(byte)type,128,1}); return bytes.ToArray();
    }
    public static List<NetworkDevice> Build(List<DnsRecord> records, Dictionary<string,string> origins) {
        var result = new List<NetworkDevice>();
        foreach(var service in records.Where(r=>r.Type==33 && r.Port>0)) {
            var a = records.FirstOrDefault(r=>r.Type==1 && String.Equals(r.Name,service.Target,StringComparison.OrdinalIgnoreCase));
            string ip = a==null ? null : a.Address;
            if(ip==null) origins.TryGetValue(service.Name,out ip);
            IPAddress parsed;
            if(!IPAddress.TryParse(ip,out parsed) || parsed.AddressFamily!=AddressFamily.InterNetwork) continue;
            int separator=service.Name.IndexOf("._",StringComparison.Ordinal);
            string title=separator>0?service.Name.Substring(0,separator):service.Name;
            string url="";
            if(service.Name.IndexOf("._http._tcp.",StringComparison.OrdinalIgnoreCase)>=0) url="http://"+ip+":"+service.Port+"/";
            if(service.Name.IndexOf("._https._tcp.",StringComparison.OrdinalIgnoreCase)>=0) url="https://"+ip+":"+service.Port+"/";
            result.Add(new NetworkDevice { Id=service.Name,IP=ip,Name=title,Protocol="mDNS / DNS-SD",WebUrl=url });
        }
        return result;
    }
    public List<NetworkDevice> Scan() {
        var found = new List<NetworkDevice>();
        Parallel.ForEach(NetworkDiscovery.Interfaces(), local => {
            using(var udp = new UdpClient(new IPEndPoint(local,0))) {
                udp.Client.SetSocketOption(SocketOptionLevel.IP,SocketOptionName.MulticastInterface,local.GetAddressBytes());
                udp.Ttl=255;
                var multicast=new IPEndPoint(IPAddress.Parse("224.0.0.251"),5353);
                var sent=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Action<string,int> ask=(name,type)=>{if(sent.Count<96 && sent.Add(name+":"+type)){var query=Query(name,type);udp.Send(query,query.Length,multicast);}};
                foreach(string name in new[]{"_services._dns-sd._udp.local","_http._tcp.local","_https._tcp.local","_hap._tcp.local","_matter._tcp.local","_matterc._udp.local","_googlecast._tcp.local","_shelly._tcp.local"}) ask(name,12);
                var records=new List<DnsRecord>(); var origins=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                DateTime end=DateTime.UtcNow.AddSeconds(4);
                while(DateTime.UtcNow<end) {
                    udp.Client.ReceiveTimeout=Math.Max(1,(int)(end-DateTime.UtcNow).TotalMilliseconds);
                    var sender=new IPEndPoint(IPAddress.Any,0); byte[] data;
                    try { data=udp.Receive(ref sender); } catch(SocketException ex){if(ex.SocketErrorCode==SocketError.TimedOut)break;throw;}
                    try {
                        var batch=Parse(data);
                        if(records.Count>4096)break;
                        records.AddRange(batch);
                        foreach(var record in batch) {
                            if(record.Type==33) {origins[record.Name]=sender.Address.ToString();ask(record.Target,1);}
                            if(record.Type==12 && record.Target!=null) ask(record.Target,record.Name.Equals("_services._dns-sd._udp.local",StringComparison.OrdinalIgnoreCase)?12:33);
                        }
                    } catch(FormatException) { }
                }
                lock(found) found.AddRange(Build(records,origins));
            }
        });
        return found;
    }
}
