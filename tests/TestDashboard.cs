using System;
using System.IO;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Windows.Forms;
class TestDashboard {
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object obj,string name){return obj.GetType().GetField(name,flags).GetValue(obj);}
 static void Call(object obj,string name,params object[] args){obj.GetType().GetMethod(name,flags).Invoke(obj,args);}
 [STAThread] static int Main(){try{Tests();Console.WriteLine("PASS: RGB/CT wire parameters, unsupported-command blocking, capability UI gating, saved presets, mono/RGB rendering");return 0;}catch(Exception ex){Console.WriteLine("FAIL: "+ex);return 1;}}
 static void Tests(){
  using(var mono=new LampSession(new Device{IP="127.0.0.1",CapabilitiesKnown=true,Commands=new[]{"set_power","set_bright"}})){
   bool blocked=false;try{mono.SetColor(0xff0000);}catch(NotSupportedException){blocked=true;}Check(blocked,"mono RGB not blocked");
   blocked=false;try{mono.SetTemperature(2700);}catch(NotSupportedException){blocked=true;}Check(blocked,"mono CT not blocked");
  }
  var server=new TcpListener(IPAddress.Loopback,0);server.Start();int port=((IPEndPoint)server.LocalEndpoint).Port;
  var responder=Task.Run(()=>{using(var client=server.AcceptTcpClient()){client.ReceiveTimeout=3000;var stream=client.GetStream();using(var reader=new StreamReader(stream)){
   for(int i=0;i<2;i++){var json=new JavaScriptSerializer();var request=json.Deserialize<Dictionary<string,object>>(reader.ReadLine());var args=(System.Collections.ArrayList)request["params"];
    Check((string)request["method"]==(i==0?"set_ct_abx":"set_rgb"),"wrong command");Check(Convert.ToInt32(args[0])==(i==0?2700:0x12abef),"wrong value");Check((string)args[1]=="smooth"&&Convert.ToInt32(args[2])==200,"wrong transition");
    var bytes=System.Text.Encoding.UTF8.GetBytes(json.Serialize(new{id=request["id"],result=new[]{"ok"}})+"\r\n");stream.Write(bytes,0,bytes.Length);
   }
  }}});
  using(var rgb=new LampSession(new Device{IP="127.0.0.1",Port=port,CapabilitiesKnown=true,Commands=new[]{"set_ct_abx","set_rgb"}})){rgb.SetTemperature(2700);rgb.SetColor(0x12abef);}
  Check(responder.Wait(5000),"network test hung");server.Stop();
  Application.EnableVisualStyles();
  string config=Path.GetFullPath("work/dashboard-settings.json");new Preferences{Presets=new[]{16,30,60,100}}.Save(config);
  using(var form=new LightWindow(config,false)){
   var device=new Device{IP="127.0.0.1",Model="test-mono",CapabilitiesKnown=true,Commands=new[]{"set_power","set_bright","get_prop"}};
   ((ILightController)Field(form,"session")).Dispose();typeof(LightWindow).GetField("session",flags).SetValue(form,new LampSession(device));
   Call(form,"UpdateExtraState",new object[]{new object[]{"on","16","","",""}});
   Check(!((Control)Field(form,"ctApply")).Enabled&&!((Control)Field(form,"rgbApply")).Enabled,"unsupported controls enabled");
   var slider=(ModernSlider)Field(form,"brightness");slider.Value=42;Call(form,"SavePreset",1);Check(Preferences.Load(config).Presets[1]==42,"preset save failed");
   form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
   Capture(form,"work/dashboard-mono.png");
   device.Model="color";device.Commands=new[]{"set_power","set_bright","get_prop","set_rgb","set_ct_abx"};
   Call(form,"UpdateExtraState",new object[]{new object[]{"on","42","4000","12226559","1"}});
   Check(((Control)Field(form,"ctApply")).Enabled&&((Control)Field(form,"rgbApply")).Enabled,"RGB controls not enabled");
   ((Label)Field(form,"state")).Text="Işık açık"; ((Label)Field(form,"connection")).Text="● Örnek cihaz görünümü"; ((Label)Field(form,"status")).Text="RGB destekli ampul · Arayüz önizlemesi"; Capture(form,"work/dashboard-rgb.png");
   device.Commands=new[]{"set_ct_abx"};Call(form,"UpdateCapabilities");Check(((Control)Field(form,"ctApply")).Enabled&&!((Control)Field(form,"rgbApply")).Enabled,"CT-only gating failed");
   form.Close();
  }
 }
 static void Capture(Form form,string path){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(path);}}
}
