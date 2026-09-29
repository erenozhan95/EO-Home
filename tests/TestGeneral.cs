using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
class TestGeneral {
 static void Check(bool v,string message) {if(!v)throw new Exception(message);}
 static byte[] Name(string s){var q=MdnsProvider.Query(s);return q.Skip(12).Take(q.Length-16).ToArray();}
 static void Record(List<byte> bytes,string name,int type,byte[] value) {
  bytes.AddRange(Name(name));bytes.AddRange(new byte[]{0,(byte)type,0,1,0,0,0,120,(byte)(value.Length>>8),(byte)value.Length});bytes.AddRange(value);
 }
 [STAThread] static int Main() {
  try {
   var ssdp=SsdpProvider.Parse("HTTP/1.1 200 OK\r\nUSN: uuid:abc::upnp:rootdevice\r\nST: urn:schemas-upnp-org:device:MediaRenderer:1\r\n",IPAddress.Parse("203.0.113.20"));
   Check(ssdp.Name=="MediaRenderer"&&ssdp.Light==null,"SSDP classified as lamp");
   Check(SsdpProvider.Parse("bad",IPAddress.Loopback)==null,"Invalid SSDP accepted");
   var packet=new List<byte>(new byte[]{0,0,132,0,0,0,0,2,0,0,0,0});
   Record(packet,"Desk._http._tcp.local",33,new byte[]{0,0,0,0,31,144}.Concat(Name("desk.local")).ToArray());
   Record(packet,"desk.local",1,new byte[]{203,0,113,21});
   var records=MdnsProvider.Parse(packet.ToArray());
   var devices=MdnsProvider.Build(records,new Dictionary<string,string>());
   Check(devices.Count==1&&devices[0].WebUrl=="http://203.0.113.21:8080/"&&devices[0].Light==null,"mDNS service not parsed");
   int position=0; bool rejected=false;
   try {MdnsProvider.ReadName(new byte[]{192,0},ref position);}catch(FormatException){rejected=true;}
   Check(rejected,"DNS pointer cycle not rejected");
   rejected=false;try{MdnsProvider.Parse(packet.Take(packet.Count-1).ToArray());}catch(FormatException){rejected=true;}
   Check(rejected,"Truncated DNS not rejected");
   var lamp=new Device{IP="203.0.113.21"};
   devices.Add(new NetworkDevice{IP=lamp.IP,Name="Lamp",Protocol="Yeelight LAN",Light=lamp});
   var merged=NetworkDiscovery.Merge(devices);
   Check(merged.Count==1&&merged[0].Light==lamp&&merged[0].WebUrl.Length>0&&merged[0].Protocol.Contains("mDNS"),"Merge lost controller/web/protocol");
   System.Windows.Forms.Application.EnableVisualStyles();
   using(var picker=new DevicePicker("203.0.113.5",false)) {
    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var list=(System.Windows.Forms.ListBox)typeof(DevicePicker).GetField("list",flags).GetValue(picker);
    var choose=(System.Windows.Forms.Button)typeof(DevicePicker).GetField("choose",flags).GetValue(picker);
    var web=(System.Windows.Forms.Button)typeof(DevicePicker).GetField("openWeb",flags).GetValue(picker);
    list.Items.Add(ssdp);list.Items.Add(new NetworkDevice{IP="203.0.113.9",Name="Yerel web hizmeti",Protocol="mDNS / DNS-SD",WebUrl="http://203.0.113.9/"});list.Items.Add(merged[0]);
    list.SelectedIndex=0;Check(!choose.Enabled&&!web.Enabled,"Unsupported device has controls");
    list.SelectedIndex=1;Check(!choose.Enabled&&web.Enabled,"Web action missing");
    list.SelectedIndex=2;Check(choose.Enabled&&web.Enabled,"Light capability missing");
    picker.StartPosition=System.Windows.Forms.FormStartPosition.Manual;picker.Location=new System.Drawing.Point(-10000,-10000);picker.ShowInTaskbar=false;picker.Show();System.Windows.Forms.Application.DoEvents();
    using(var bitmap=new System.Drawing.Bitmap(picker.Width,picker.Height)){picker.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save("work/general-preview.png");}
    picker.Close();
   }
   Console.WriteLine("PASS: generic SSDP, mDNS SRV/A, URL, no false light support, DNS cycle/truncation protection, protocol merge");return 0;
  }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}
 }
}
