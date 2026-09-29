using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class Theme {
    public static readonly Color Background=Color.FromArgb(14,18,25), Card=Color.FromArgb(23,29,39), Border=Color.FromArgb(43,52,66), Text=Color.FromArgb(235,239,246), Muted=Color.FromArgb(146,158,178), Accent=Color.FromArgb(255,209,112);
    public static GraphicsPath Round(RectangleF box,float radius) {
        var path=new GraphicsPath();float d=radius*2;
        path.AddArc(box.X,box.Y,d,d,180,90);path.AddArc(box.Right-d,box.Y,d,d,270,90);path.AddArc(box.Right-d,box.Bottom-d,d,d,0,90);path.AddArc(box.X,box.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
}
public class CardPanel : Panel {
    public CardPanel() { DoubleBuffered=true; BackColor=Theme.Background; }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),16))
        using(var fill=new SolidBrush(Theme.Card)) using(var border=new Pen(Theme.Border)) {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
    }
}
public class ModernButton : Button {
    public bool Primary; public Color? Swatch; bool hovered;
    public ModernButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=new Font("Segoe UI",10,FontStyle.Bold); }
    protected override void OnMouseEnter(EventArgs e) {hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e) {hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(BackColor);
        Color color=Swatch ?? (Primary?Theme.Accent:Color.FromArgb(37,46,61));
        if(!Enabled) color=Swatch.HasValue?Color.FromArgb((color.R+46)/2,(color.G+49)/2,(color.B+55)/2):Color.FromArgb(31,38,49);
        else if(hovered) color=ControlPaint.Light(color,0.13f);
        using(var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),10)) using(var brush=new SolidBrush(color)) using(var pen=new Pen(Focused?Theme.Accent:Theme.Border)) {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?Theme.Muted:Primary?Theme.Background:Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
}
public class ModernSlider : Control {
    int minimum=1,maximum=100,value=50; bool dragging;
    public int Minimum {get{return minimum;}set{minimum=value;Value=this.value;}}
    public int Maximum {get{return maximum;}set{maximum=value;Value=this.value;}}
    public int Value {get{return value;}set{int next=Math.Max(minimum,Math.Min(maximum,value));if(this.value!=next){this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}}}
    public Color StartColor=Theme.Accent,EndColor=Theme.Accent;
    public event EventHandler ValueChanged, Scroll;
    public ModernSlider(){SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);TabStop=true;Cursor=Cursors.Hand;BackColor=Theme.Card;AccessibleRole=AccessibleRole.Slider;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float x=12+(Width-24)*(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum),y=Height/2f;
        using(var rail=new Pen(Theme.Border,6)){rail.StartCap=rail.EndCap=LineCap.Round;e.Graphics.DrawLine(rail,12,y,Width-12,y);}
        using(var fill=new LinearGradientBrush(new Rectangle(10,0,Math.Max(1,Width-20),Math.Max(1,Height)),Enabled?StartColor:Theme.Border,Enabled?EndColor:Theme.Border,0f))
        using(var pen=new Pen(fill,6)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLine(pen,12,y,x,y);}
        using(var brush=new SolidBrush(Enabled?Theme.Text:Theme.Muted)){e.Graphics.FillEllipse(brush,x-8,y-8,16,16);}
        if(Focused)using(var pen=new Pen(Theme.Accent)){e.Graphics.DrawEllipse(pen,x-11,y-11,22,22);}
    }
    void Choose(int x){Value=Minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(x-12)/(double)Math.Max(1,Width-24)))*(Maximum-Minimum));if(Scroll!=null)Scroll(this,EventArgs.Empty);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Capture=true;dragging=true;Choose(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Choose(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
    protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);int step=Maximum>100?100:1;if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value-=step;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value+=step;else if(e.KeyCode==Keys.Home)Value=Minimum;else if(e.KeyCode==Keys.End)Value=Maximum;else return;e.Handled=true;if(Scroll!=null)Scroll(this,EventArgs.Empty);}
}
public class LampGlyph : Control {
    public bool Lit;public Color LightColor=Theme.Accent;
    public LampGlyph(){DoubleBuffered=true;BackColor=Theme.Card;}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=Math.Min(Width,Height)/80f;
        e.Graphics.ScaleTransform(s,s);
        using(var glow=new SolidBrush(Lit?Color.FromArgb(45,LightColor):Color.FromArgb(33,43,57)))e.Graphics.FillEllipse(glow,2,2,76,76);
        using(var brush=new SolidBrush(Lit?LightColor:Theme.Muted)){e.Graphics.FillEllipse(brush,24,15,32,34);e.Graphics.FillRectangle(brush,31,41,18,13);}
        using(var pen=new Pen(Theme.Background,2)){e.Graphics.DrawLine(pen,33,32,40,39);e.Graphics.DrawLine(pen,47,32,40,39);e.Graphics.DrawLine(pen,40,39,40,50);}
        using(var pen=new Pen(Theme.Text,3)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLine(pen,32,60,48,60);e.Graphics.DrawLine(pen,35,66,45,66);}
    }
}
public partial class LightWindow {
    Label deviceTitle, ctValue, ctHint, rgbHint, rgbValue;
    ModernSlider temperature=new ModernSlider();
    ModernButton ctApply, rgbApply, customColor;
    ModernButton[] whitePresets=new ModernButton[3],swatches=new ModernButton[6];
    LampGlyph lampVisual;
    int selectedRgb=0xffd170; bool temperatureDirty, colorDirty;
    CardPanel Card(int x,int y,int width,int height){var p=new CardPanel();p.SetBounds(x,y,width,height);Controls.Add(p);return p;}
    Label Txt(Control parent,string text,int x,int y,int width,int height,float size,bool bold=false,Color? color=null){var l=new Label{Text=text,BackColor=Theme.Card,ForeColor=color??Theme.Text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)};l.SetBounds(x,y,width,height);parent.Controls.Add(l);return l;}
    ModernButton Btn(Control parent,string text,int x,int y,int width,int height,EventHandler click,bool primary=false){var b=new ModernButton{Text=text,Primary=primary,BackColor=parent==this?Theme.Background:Theme.Card};b.SetBounds(x,y,width,height);b.Click+=click;parent.Controls.Add(b);return b;}
    void BuildDashboard(){
        Text="LightController";AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);
        ClientSize=new Size(960,708);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);
        var brand=Txt(this,"LightController",28,24,300,40,25,true);brand.BackColor=Theme.Background;
        var subtitle=Txt(this,"Işığını, kendi ritmine göre ayarla.",30,68,600,25,10,false,Theme.Muted);subtitle.BackColor=Theme.Background;
        settings=Btn(this,"Cihazları yönet",758,32,174,42,delegate{OpenSettings();});
        var main=Card(28,118,436,336);
        deviceTitle=Txt(main,String.IsNullOrEmpty(prefs.Model)?"ODA IŞIĞI":prefs.Model.ToUpperInvariant(),24,22,296,24,10,true,Theme.Muted);
        state=Txt(main,"Bağlanıyor…",24,57,310,42,23,true);
        lampVisual=new LampGlyph();lampVisual.SetBounds(334,22,76,76);main.Controls.Add(lampVisual);
        on=Btn(main,"Işığı aç",24,117,188,42,async delegate{await Power(true);},true);
        off=Btn(main,"Kapat",224,117,188,42,async delegate{await Power(false);});
        Txt(main,"PARLAKLIK",24,191,180,24,9,true,Theme.Muted);
        percent=Txt(main,"%50",296,177,116,42,26,true);percent.TextAlign=ContentAlignment.MiddleRight;
        brightness.SetBounds(16,224,404,36);brightness.AccessibleName="Parlaklık yüzdesi";
        brightness.ValueChanged+=delegate{percent.Text="%"+brightness.Value;};brightness.Scroll+=delegate{dirty=true;};main.Controls.Add(brightness);
        apply=Btn(main,"Parlaklığı uygula",24,278,388,36,async delegate{await SetBrightness(brightness.Value,false);});
        var saved=Card(28,470,436,188);
        Txt(saved,"Favori parlaklıklar",24,20,370,28,14,true);
        Txt(saved,"Yüzdeyi seç, bir yuvaya kaydet.",24,51,388,24,9,false,Theme.Muted);
        for(int i=0;i<4;i++){int index=i;presets[i]=Btn(saved,"%"+prefs.Presets[i],24+i*98,87,94,42,async delegate{await SetBrightness(prefs.Presets[index],true);});Btn(saved,"Kaydet",24+i*98,136,94,29,delegate{SavePreset(index);});}
        var white=Card(480,118,452,254);
        Txt(white,"Beyaz tonu",24,20,240,30,16,true);
        ctValue=Txt(white,"4000 K",288,22,140,28,15,true);ctValue.TextAlign=ContentAlignment.MiddleRight;
        string[] names={"Sıcak sarı","Doğal beyaz","Soğuk beyaz"};int[] kelvin={2700,4000,6500};
        for(int i=0;i<3;i++){int k=kelvin[i];whitePresets[i]=Btn(white,names[i],24+i*136,69,128,36,async delegate{temperature.Value=k;await ApplyTemperature();});}
        temperature.Minimum=1700;temperature.Maximum=6500;temperature.Value=4000;temperature.StartColor=Color.FromArgb(255,174,81);temperature.EndColor=Color.FromArgb(172,211,255);temperature.SetBounds(16,116,420,30);temperature.AccessibleName="Beyaz ışık sıcaklığı Kelvin";
        temperature.ValueChanged+=delegate{ctValue.Text=temperature.Value+" K";};temperature.Scroll+=delegate{temperatureDirty=true;};white.Controls.Add(temperature);
        ctHint=Txt(white,"Cihaz desteği kontrol ediliyor…",24,152,404,32,9,false,Theme.Muted);
        ctApply=Btn(white,"Beyaz tonunu uygula",24,199,404,34,async delegate{await ApplyTemperature();});
        var rgb=Card(480,388,452,270);
        Txt(rgb,"Renk paleti",24,20,240,30,16,true);
        rgbValue=Txt(rgb,"#FFD170",294,22,134,26,12,true,Theme.Muted);rgbValue.TextAlign=ContentAlignment.MiddleRight;
        int[] colors={0xff6464,0xffb64d,0xffe289,0x62d6aa,0x69aaff,0xb293ff};
        for(int i=0;i<6;i++){int value=colors[i];swatches[i]=Btn(rgb,"",24+i*68,72,64,43,delegate{ChooseColor(value);});swatches[i].Swatch=Color.FromArgb((value>>16)&255,(value>>8)&255,value&255);swatches[i].AccessibleName="Renk #"+value.ToString("X6");}
        customColor=Btn(rgb,"Özel renk seç",24,129,404,35,delegate{using(var dialog=new ColorDialog{FullOpen=true,Color=Color.FromArgb((selectedRgb>>16)&255,(selectedRgb>>8)&255,selectedRgb&255)})if(dialog.ShowDialog(this)==DialogResult.OK)ChooseColor(Math.Max(1,dialog.Color.ToArgb()&0xffffff));});
        rgbHint=Txt(rgb,"Cihaz desteği kontrol ediliyor…",24,174,404,32,9,false,Theme.Muted);
        rgbApply=Btn(rgb,"Rengi uygula",24,215,404,34,async delegate{await ApplyColor();});
        connection=Txt(this,"● Bağlanıyor…",30,677,352,24,9,false,Theme.Muted);connection.BackColor=Theme.Background;
        status=Txt(this,"Kontrol tamamen yerel ağında.",390,677,542,24,9,false,Theme.Muted);status.BackColor=Theme.Background;status.TextAlign=ContentAlignment.TopRight;
        UpdateCapabilities();
    }
    void UpdateCapabilities(){
        Device device=session.Device;
        bool ct=device.Supports("set_ct_abx"),rgb=device.Supports("set_rgb");
        temperature.Enabled=ctApply.Enabled=ct&&!commandBusy;
        foreach(var b in whitePresets)b.Enabled=ct&&!commandBusy;
        customColor.Enabled=rgbApply.Enabled=rgb&&!commandBusy;
        foreach(var b in swatches)b.Enabled=rgb&&!commandBusy;
        ctHint.Text=ct?"1700 K sıcak sarı → 6500 K soğuk beyaz":device.CapabilitiesKnown?"Bu ampul beyaz sıcaklığı ayarını desteklemiyor.":"Beyaz tonu desteği henüz doğrulanmadı.";
        rgbHint.Text=rgb?"Bir renk seç, ardından uygula.":device.CapabilitiesKnown?"Bu ampul RGB desteklemiyor. Renkli cihazlarda açılır.":"RGB desteği henüz doğrulanmadı.";
        deviceTitle.Text=!String.IsNullOrWhiteSpace(device.Name)?device.Name:device.Model=="Yeelight"?"ODA IŞIĞI":device.Model.ToUpperInvariant();
    }
    void ChooseColor(int color){selectedRgb=color;colorDirty=true;rgbValue.Text="#"+color.ToString("X6");rgbValue.ForeColor=Color.FromArgb((color>>16)&255,(color>>8)&255,color&255);}
    async Task ApplyTemperature(){
        if(!session.Device.Supports("set_ct_abx"))return;
        int value=temperature.Value;
        await Command(async delegate{await Task.Run(()=>session.SetTemperature(value));if(!closing){temperatureDirty=false;lampVisual.LightColor=WhiteColor(value);lampVisual.Invalidate();}});
    }
    async Task ApplyColor(){
        if(!session.Device.Supports("set_rgb"))return;
        int value=selectedRgb;
        await Command(async delegate{await Task.Run(()=>session.SetColor(value));if(!closing){colorDirty=false;lampVisual.LightColor=Color.FromArgb((value>>16)&255,(value>>8)&255,value&255);lampVisual.Invalidate();}});
    }
    void UpdateExtraState(object[] result){
        lampVisual.Lit=Convert.ToString(result[0])=="on";lampVisual.Invalidate();
        int value;
        if(result.Length>2&&!temperatureDirty&&int.TryParse(Convert.ToString(result[2]),out value)&&value>=1700&&value<=6500)temperature.Value=value;
        if(result.Length>3&&!colorDirty&&session.Device.Supports("set_rgb")&&int.TryParse(Convert.ToString(result[3]),out value)&&value>=1&&value<=0xffffff){selectedRgb=value;rgbValue.Text="#"+value.ToString("X6");}
        if(result.Length>4&&Convert.ToString(result[4])=="1"&&int.TryParse(Convert.ToString(result[3]),out value)&&value>=1&&value<=0xffffff)lampVisual.LightColor=Color.FromArgb((value>>16)&255,(value>>8)&255,value&255);
        else if(result.Length>2&&int.TryParse(Convert.ToString(result[2]),out value)&&value>=1700&&value<=6500)lampVisual.LightColor=WhiteColor(value);
        lampVisual.Invalidate();
        UpdateCapabilities();
    }
    static Color WhiteColor(int kelvin){double t=Math.Max(0,Math.Min(1,(kelvin-1700)/4800.0));return Color.FromArgb((int)(255-40*t),(int)(170+60*t),(int)(80+175*t));}
}
