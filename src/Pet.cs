using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace HailuotaPet {
 public class Pet : Window {
  static bool testMode;
  const string PetTitle = "海洛塔帝 · 桌面陪伴";
  readonly string root = AppDomain.CurrentDomain.BaseDirectory;
  string DataDir { get { return testMode?Path.Combine(root,"checks","test-user"):AiStore.Root; } }
  BubbleChatWindow chat;
  bool openChatOnRelease,aiBusy;
  readonly Image actor = new Image();
  readonly Grid stage = new Grid();
  readonly Border bubble = new Border();
  readonly TextBlock speech = new TextBlock();
  readonly ScaleTransform squash = new ScaleTransform();
  readonly RotateTransform tilt = new RotateTransform();
  readonly TranslateTransform bob = new TranslateTransform();
  readonly BitmapSource[] frames = new BitmapSource[4];
  readonly DispatcherTimer timer = new DispatcherTimer();
  readonly Stopwatch time = Stopwatch.StartNew();
  readonly Random random = new Random();
  readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
  double dpiX = 1, dpiY = 1, zoom = 1, downLeft, downTop, reactionUntil, bubbleUntil;
  Point downScreen;
  bool held, dragging, paused, wander, closing;
  int frame, clicks;
  string state = "idle";
  readonly string[] lines = {
   "嗯？这次又有什么有趣的想法？", "我在。慢慢来，一起想。",
   "被你发现我在思考了。", "好吧，今天的议题就由你决定。",
   "休息一下，灵感也需要喘口气。"
  };
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [DllImport("user32.dll", EntryPoint="GetWindowLong")] static extern int GetWindowLong(IntPtr h, int n);
  [DllImport("user32.dll", EntryPoint="SetWindowLong")] static extern int SetWindowLong(IntPtr h, int n, int v);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
  [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }

  [STAThread] public static int Main(string[] args) {
   SetProcessDPIAware();
   if(Array.IndexOf(args,"--chat-self-test")>=0) return AiChecks.Run(args);
   if(Array.IndexOf(args,"--backend-smoke-test")>=0) return AiChecks.Smoke(args);
   if(Array.IndexOf(args,"--bubble-smoke-test")>=0) return AiChecks.BubbleSmoke(args);
   testMode=Array.IndexOf(args,"--self-test") >= 0;
   if(testMode) AiStore.Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"checks","test-user");
   bool first;
   using(var mutex = new Mutex(true, "Local\\HailuotaDesktopPet_v1"+(testMode?"_test_"+Process.GetCurrentProcess().Id:""), out first)) {
    if(!first) { Directory.CreateDirectory(AiStore.Root); File.WriteAllText(Path.Combine(AiStore.Root,"show.request"),Array.IndexOf(args,"--chat")>=0?"chat":"show"); return 0; }
    try {
     var app = new Application();
     var pet = new Pet();
     if(testMode) {
      pet.Show(); pet.SelfTest(); pet.Close(); return 0;
     }
     if(Array.IndexOf(args,"--chat")>=0) pet.Loaded+=delegate { pet.OpenChat(false); };
     app.Run(pet); return 0;
    } catch(Exception e) {
     File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.log"),e.ToString());
     MessageBox.Show("桌宠启动失败，详细信息已写入 error.log。\n"+e.Message,PetTitle); return 1;
    }
   }
  }

  public Pet() {
   Title=PetTitle; Width=340; Height=520; WindowStyle=WindowStyle.None;
   ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
   ShowInTaskbar=false; ShowActivated=false; Topmost=true; UseLayoutRounding=true;
   for(int i=0;i<4;i++) {
    var b=new BitmapImage(); b.BeginInit(); b.CacheOption=BitmapCacheOption.OnLoad;
    b.UriSource=new Uri(Path.Combine(root,"assets",new[]{"idle.png","happy.png","pressed.png","think.png"}[i]));
    b.EndInit(); b.Freeze(); frames[i]=new FormatConvertedBitmap(b,PixelFormats.Bgra32,null,0); frames[i].Freeze();
   }
   Content=stage;
   actor.Width=300; actor.Height=440; actor.VerticalAlignment=VerticalAlignment.Bottom;
   actor.HorizontalAlignment=HorizontalAlignment.Center; actor.Margin=new Thickness(0,0,0,10);
   actor.Stretch=Stretch.Uniform; actor.Source=frames[0]; actor.RenderTransformOrigin=new Point(.5,.9);
   var transforms=new TransformGroup(); transforms.Children.Add(squash); transforms.Children.Add(tilt); transforms.Children.Add(bob);
   actor.RenderTransform=transforms;
   RenderOptions.SetBitmapScalingMode(actor,BitmapScalingMode.HighQuality);
   stage.Children.Add(actor);
   speech.FontFamily=new FontFamily("Microsoft YaHei UI"); speech.FontSize=13;
   speech.Foreground=new SolidColorBrush(Color.FromRgb(60,52,72)); speech.TextWrapping=TextWrapping.Wrap;
   speech.TextAlignment=TextAlignment.Center;
   bubble.Background=new SolidColorBrush(Color.FromArgb(255,255,255,255));
   bubble.BorderBrush=new SolidColorBrush(Color.FromRgb(166,146,214)); bubble.BorderThickness=new Thickness(0);
   bubble.CornerRadius=new CornerRadius(24); bubble.Padding=new Thickness(16,10,16,10);
   bubble.Width=280; bubble.HorizontalAlignment=HorizontalAlignment.Center; bubble.VerticalAlignment=VerticalAlignment.Top;
   bubble.Margin=new Thickness(0,12,0,0); bubble.Child=speech; bubble.Visibility=Visibility.Collapsed;
   bubble.IsHitTestVisible=false; stage.Children.Add(bubble);
   actor.MouseLeftButtonDown+=OnDown; actor.MouseMove+=OnMove; actor.MouseLeftButtonUp+=OnUp;
   actor.LostMouseCapture+=delegate { if(held) { held=false; dragging=false; Reaction(0,.1,"idle"); } };
   actor.MouseWheel+=delegate(object s,MouseWheelEventArgs e) { SetZoom(zoom+(e.Delta>0?.1:-.1)); e.Handled=true; };
   actor.ContextMenu=BuildMenu();
   SourceInitialized+=delegate {
    var source=(HwndSource)PresentationSource.FromVisual(this);
    dpiX=source.CompositionTarget.TransformToDevice.M11; dpiY=source.CompositionTarget.TransformToDevice.M22;
    var h=new WindowInteropHelper(this).Handle; SetWindowLong(h,-20,GetWindowLong(h,-20)|0x80|0x08000000);
    source.AddHook(Hook); PlaceAtCorner(); if(!testMode) RestorePlacement();
   };
   Loaded+=delegate { WriteStatus(); };
   Closing+=delegate { if(chat!=null) chat.Shutdown(); SavePlacement(); closing=true; timer.Stop(); tray.Visible=false; tray.Dispose(); WriteStatus(); };
   tray.Text="海洛塔帝桌宠 · 单击找回";
   tray.Icon=System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
   var trayMenu=new Forms.ContextMenuStrip();
   trayMenu.Items.Add("显示 / 找回桌宠",null,delegate { Dispatcher.Invoke(new Action(Restore)); });
   trayMenu.Items.Add("和 AI 聊天",null,delegate { Dispatcher.Invoke(new Action(delegate { OpenChat(false); })); });
   trayMenu.Items.Add("AI 设置",null,delegate { Dispatcher.Invoke(new Action(delegate { OpenChat(true); })); });
   trayMenu.Items.Add("暂时隐藏",null,delegate { Dispatcher.Invoke(new Action(Hide)); });
   trayMenu.Items.Add("退出",null,delegate { Dispatcher.Invoke(new Action(Close)); });
   tray.ContextMenuStrip=trayMenu; tray.DoubleClick+=delegate { Dispatcher.Invoke(new Action(Restore)); }; tray.Visible=true;
   timer.Interval=TimeSpan.FromMilliseconds(33); timer.Tick+=Tick; timer.Start();
  }

  ContextMenu BuildMenu() {
   var menu=new ContextMenu();
   menu.Items.Add(Item("海洛塔帝 · 桌面陪伴",delegate { Say("一起想点有趣的事情吧。",3); }));
   menu.Items.Add(new Separator());
   menu.Items.Add(Item("和 AI 聊天（单击）",delegate { OpenChat(false); }));
   menu.Items.Add(Item("AI 设置 / 连接 Codex",delegate { OpenChat(true); }));
   menu.Items.Add(new Separator());
   menu.Items.Add(Item("和我打招呼",delegate { Reaction(1,2,"happy"); Say(lines[random.Next(lines.Length)],3); }));
   menu.Items.Add(Item("思考一会儿",delegate { Reaction(3,5,"think"); Say("让我想想……",4); }));
   var pause=Item("暂停待机动画",delegate { paused=!paused; WriteStatus(); }); pause.IsCheckable=true; menu.Items.Add(pause);
   var walk=Item("轻轻走动",delegate { wander=!wander; WriteStatus(); }); walk.IsCheckable=true; menu.Items.Add(walk);
   var top=Item("保持置顶",delegate { Topmost=!Topmost; WriteStatus(); }); top.IsCheckable=true; top.IsChecked=true; menu.Items.Add(top);
   var sizes=new MenuItem { Header="大小" };
   sizes.Items.Add(Item("小 · 75%",delegate { SetZoom(.75); }));
   sizes.Items.Add(Item("默认 · 100%",delegate { SetZoom(1); }));
   sizes.Items.Add(Item("大 · 125%",delegate { SetZoom(1.25); })); menu.Items.Add(sizes);
   menu.Items.Add(Item("回到屏幕右下角",delegate { PlaceAtCorner(); }));
   menu.Items.Add(Item("暂时隐藏（从托盘找回）",delegate { Hide(); }));
   menu.Items.Add(new Separator()); menu.Items.Add(Item("退出桌宠",delegate { Close(); })); return menu;
  }
  MenuItem Item(string text,Action action) { var m=new MenuItem { Header=text }; m.Click+=delegate { action(); }; return m; }
  Point? testCursor;
  Point CursorPoint() { if(testCursor.HasValue) return testCursor.Value; NativePoint p; GetCursorPos(out p); return new Point(p.X,p.Y); }
  void OnDown(object sender,MouseButtonEventArgs e) {
   if(e.ChangedButton!=MouseButton.Left) return;
   if(chat!=null&&chat.IsVisible) chat.Hide();
   downScreen=CursorPoint(); downLeft=Left; downTop=Top; held=true; dragging=false;
   actor.CaptureMouse(); Reaction(2,999,"pressed"); squash.ScaleX=1.035; squash.ScaleY=.94; bob.Y=6;
   openChatOnRelease=true;
   WriteStatus(); e.Handled=true;
  }
  void OnMove(object sender,MouseEventArgs e) {
   if(!held) return;
   var p=CursorPoint(); double dx=(p.X-downScreen.X)/dpiX,dy=(p.Y-downScreen.Y)/dpiY;
   if(!dragging && Math.Abs(dx)+Math.Abs(dy)>5) { dragging=true; openChatOnRelease=false; Reaction(3,999,"dragging"); bubble.Visibility=Visibility.Collapsed; if(chat!=null) chat.Hide(); }
   if(dragging) { Left=downLeft+dx; Top=downTop+dy; tilt.Angle=Math.Max(-6,Math.Min(6,dx*.035)); }
   WriteStatus();
  }
  void OnUp(object sender,MouseButtonEventArgs e) {
   if(!held) return; bool wasDragging=dragging; held=false; dragging=false; actor.ReleaseMouseCapture();
   tilt.Angle=0; Clamp(); SavePlacement(); clicks++;
   Reaction(1,1.7,"happy");
   if(openChatOnRelease&&!wasDragging) { openChatOnRelease=false; OpenChat(false); }
   WriteStatus(); e.Handled=true;
  }
  void Reaction(int f,double seconds,string name) { frame=f; actor.Source=frames[f]; reactionUntil=time.Elapsed.TotalSeconds+seconds; state=name; }
  void Say(string text,double seconds) { speech.Text=text; bubble.Visibility=Visibility.Visible; bubbleUntil=time.Elapsed.TotalSeconds+seconds; }
  void OpenChat(bool settings) {
   if(!settings&&AiStore.LoadSettings().Provider=="none") { Say("当前仅桌宠模式。\n右键 → AI 设置，可随时启用聊天。",4); return; }
   if(chat==null) chat=new BubbleChatWindow(this,delegate { return new Point(Left+Width/2,Top+Height-actor.Height-actor.Margin.Bottom+12*zoom); },WorkingArea,delegate(bool busy,string answer) {
    aiBusy=busy; bubble.Visibility=Visibility.Collapsed;
    if(busy) Reaction(3,999,"thinking"); else Reaction(1,2,"happy");
   },!testMode);
   bubble.Visibility=Visibility.Collapsed; chat.BringUp(!settings); if(settings) chat.OpenSettings();
  }
  void Tick(object sender,EventArgs e) {
   double t=time.Elapsed.TotalSeconds;
   string request=Path.Combine(DataDir,"show.request");
   if(File.Exists(request)) { try { string action=File.ReadAllText(request); File.Delete(request); Restore(); if(action=="chat") OpenChat(false); } catch(IOException){} }
   if(!held && t>reactionUntil && frame!=0) { Reaction(0,0,"idle"); WriteStatus(); }
   if(aiBusy&&!held&&frame==0) Reaction(3,999,"thinking");
   if(chat!=null&&chat.IsVisible) { bubble.Visibility=Visibility.Collapsed; chat.FollowAnchor(); }
   if(t>bubbleUntil) bubble.Visibility=Visibility.Collapsed;
   double targetX=held?1.035:1, targetY=held?.94:(paused?1:1+.006*Math.Sin(t*2.1));
   squash.ScaleX+=(targetX-squash.ScaleX)*.25; squash.ScaleY+=(targetY-squash.ScaleY)*.25;
   double targetBob=held?6:(paused?0:Math.Sin(t*2.1)*1.7); bob.Y+=(targetBob-bob.Y)*.3;
   if(wander && !held && !paused && IsVisible && frame==0) { Left+=Math.Sin(t*.13)*.24; Clamp(); }
  }
  Rect WorkingArea() {
   var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)((Left+Width/2)*dpiX),(int)((Top+Height/2)*dpiY)));
   var r=screen.WorkingArea; return new Rect(r.X/dpiX,r.Y/dpiY,r.Width/dpiX,r.Height/dpiY);
  }
  void PlaceAtCorner() {
   var r=Forms.Screen.PrimaryScreen.WorkingArea;
   Left=(r.Right/dpiX)-Width-24; Top=(r.Bottom/dpiY)-Height; Clamp(); WriteStatus();
  }
  void Clamp() { var r=WorkingArea(); Left=Math.Max(r.Left,Math.Min(Left,r.Right-Width)); Top=Math.Max(r.Top,Math.Min(Top,r.Bottom-Height)); }
  void SetZoom(double value) {
   double bottom=Top+Height,center=Left+Width/2; var area=WorkingArea();
   zoom=Math.Max(.6,Math.Min(Math.Min(1.5,(area.Height-12)/520),value));
   Width=340*zoom; Height=520*zoom; actor.Width=300*zoom; actor.Height=440*zoom;
   actor.Margin=new Thickness(0,0,0,10*zoom); bubble.Width=Math.Min(280,Width-12);
   Left=center-Width/2; Top=bottom-Height; Clamp(); SavePlacement(); WriteStatus();
  }
  void SavePlacement() {
   if(testMode) return;
   try { Directory.CreateDirectory(DataDir); File.WriteAllLines(Path.Combine(DataDir,"placement.txt"),new[]{Left.ToString(System.Globalization.CultureInfo.InvariantCulture),Top.ToString(System.Globalization.CultureInfo.InvariantCulture),zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)}); } catch(IOException){}
  }
  void RestorePlacement() {
   string file=Path.Combine(DataDir,"placement.txt"); if(!File.Exists(file)) return;
   try {
    string[] values=File.ReadAllLines(file); if(values.Length<3) return;
    double x,y,z;
    if(!double.TryParse(values[0],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out x) ||
       !double.TryParse(values[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out y) ||
       !double.TryParse(values[2],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out z)) return;
    if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)||double.IsNaN(z)||double.IsInfinity(z)) return;
    SetZoom(z); Left=x; Top=y; Clamp(); SavePlacement(); WriteStatus();
   } catch(IOException){}
  }
  void Restore() { Show(); PlaceAtCorner(); Say("我回来了。",2); }
  IntPtr Hook(IntPtr hwnd,int msg,IntPtr wp,IntPtr lp,ref bool handled) {
   if(msg==0x0084 && !held && actor.ContextMenu!=null && !actor.ContextMenu.IsOpen) {
    long raw=lp.ToInt64(); var screen=new Point((short)(raw&65535),(short)((raw>>16)&65535));
    Point p=actor.PointFromScreen(screen); var b=frames[frame];
    double scale=Math.Min(actor.ActualWidth/b.PixelWidth,actor.ActualHeight/b.PixelHeight);
    double x=(p.X-(actor.ActualWidth-b.PixelWidth*scale)/2)/scale;
    double y=(p.Y-(actor.ActualHeight-b.PixelHeight*scale)/2)/scale;
    bool transparent=x<0||y<0||x>=b.PixelWidth||y>=b.PixelHeight;
    if(!transparent) { byte[] px=new byte[4]; b.CopyPixels(new Int32Rect((int)x,(int)y,1,1),px,4,0); transparent=px[3]<20; }
    if(transparent) { handled=true; return new IntPtr(-1); }
   }
   return IntPtr.Zero;
  }
  void WriteStatus() {
   try { Directory.CreateDirectory(DataDir); File.WriteAllText(Path.Combine(DataDir,"runtime.json"),
    "{\"pid\":"+Process.GetCurrentProcess().Id+",\"state\":\""+(closing?"closed":state)+"\",\"held\":"+held.ToString().ToLowerInvariant()+
    ",\"dragging\":"+dragging.ToString().ToLowerInvariant()+",\"clicks\":"+clicks+",\"frame\":"+frame+
    ",\"left\":"+Left.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"top\":"+Top.ToString(System.Globalization.CultureInfo.InvariantCulture)+
    ",\"zoom\":"+zoom.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"chatVisible\":"+(chat!=null&&chat.IsVisible).ToString().ToLowerInvariant()+",\"visible\":"+IsVisible.ToString().ToLowerInvariant()+
    ",\"hwnd\":"+new WindowInteropHelper(this).Handle.ToInt64()+"}",new UTF8Encoding(false)); } catch(IOException){} }
  void SelfTest() {
   testCursor=new Point(400,300);
   var folder=Path.Combine(root,"checks"); Directory.CreateDirectory(folder);
   if(bubble.Visibility==Visibility.Visible||chat!=null&&chat.IsVisible) throw new Exception("启动气泡没有隐藏");
   AiStore.Write("settings.json",new AiSettings {SetupComplete=true});
   var press=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left); press.RoutedEvent=MouseLeftButtonDownEvent;
   actor.RaiseEvent(press);
   if(!held||frame!=2||squash.ScaleY>=1) throw new Exception("按下反馈异常");
   var release=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left); release.RoutedEvent=MouseLeftButtonUpEvent;
   actor.RaiseEvent(release);
   if(held||frame!=1||clicks!=1) throw new Exception("松开反馈异常");
   press=new MouseButtonEventArgs(Mouse.PrimaryDevice,1,MouseButton.Left); press.RoutedEvent=MouseLeftButtonDownEvent;
   actor.RaiseEvent(press); downScreen=new Point(downScreen.X-35*dpiX,downScreen.Y-15*dpiY);
   var move=new MouseEventArgs(Mouse.PrimaryDevice,2); move.RoutedEvent=MouseMoveEvent; actor.RaiseEvent(move);
   if(!dragging||state!="dragging"||Math.Abs(Left-downLeft-35)>2||chat!=null&&chat.IsVisible) throw new Exception("拖动异常或拖动未收起气泡: dragging="+dragging+", state="+state+", dx="+(Left-downLeft)+", chatVisible="+(chat!=null&&chat.IsVisible));
   release=new MouseButtonEventArgs(Mouse.PrimaryDevice,3,MouseButton.Left); release.RoutedEvent=MouseLeftButtonUpEvent;
   actor.RaiseEvent(release); if(dragging||held||frame!=1) throw new Exception("拖动松开异常");
   AiStore.Write("settings.json",new AiSettings {SetupComplete=true});
   press=new MouseButtonEventArgs(Mouse.PrimaryDevice,4,MouseButton.Left); press.RoutedEvent=MouseLeftButtonDownEvent; actor.RaiseEvent(press); openChatOnRelease=true;
   release=new MouseButtonEventArgs(Mouse.PrimaryDevice,5,MouseButton.Left); release.RoutedEvent=MouseLeftButtonUpEvent; actor.RaiseEvent(release);
   if(chat==null||!chat.IsVisible||held) throw new Exception("单击打开聊天异常"); chat.Shutdown(); chat=null;
   Hide(); Restore(); if(!IsVisible) throw new Exception("隐藏找回异常");
   actor.ContextMenu.IsOpen=true; if(!actor.ContextMenu.IsOpen) throw new Exception("菜单异常"); actor.ContextMenu.IsOpen=false;
   for(int i=0;i<4;i++) {
    if(frames[i].PixelWidth<100||frames[i].PixelHeight<100) throw new Exception("素材尺寸异常");
    byte[] pixel=new byte[4]; frames[i].CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
    if(pixel[3]!=0) throw new Exception("素材背景不是透明的");
    Reaction(i,2,new[]{"idle","happy","pressed","think"}[i]);
    squash.ScaleX=i==2?1.035:1; squash.ScaleY=i==2?.94:1;
    Say(new[]{"我在。一起想点有趣的事情吧。","嗯？这次又有什么有趣的想法？","按住反馈","让我想想……"}[i],3);
    stage.Measure(new Size(Width,Height)); stage.Arrange(new Rect(0,0,Width,Height)); stage.UpdateLayout();
    var render=new RenderTargetBitmap((int)Width,(int)Height,96,96,PixelFormats.Pbgra32); render.Render(stage);
    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
    using(var f=File.Create(Path.Combine(folder,"preview-"+i+".png"))) encoder.Save(f);
    foreach(var background in new[]{"dark","light"}) {
     stage.Background=new SolidColorBrush(background=="dark"?Color.FromRgb(29,31,40):Color.FromRgb(245,243,249));
     stage.UpdateLayout();
     var composed=new RenderTargetBitmap((int)Width,(int)Height,96,96,PixelFormats.Pbgra32); composed.Render(stage);
     var composedEncoder=new PngBitmapEncoder(); composedEncoder.Frames.Add(BitmapFrame.Create(composed));
     using(var f=File.Create(Path.Combine(folder,"preview-"+i+"-"+background+".png"))) composedEncoder.Save(f);
    }
    stage.Background=null;
   }
   SetZoom(.1); if(zoom<.59) throw new Exception("缩放下限异常");
   SetZoom(9); if(zoom>1.51) throw new Exception("缩放上限异常");
   File.WriteAllText(Path.Combine(folder,"self-test.txt"),"PASS: WPF routed mouse press -> pressed sprite + immediate squash; release -> happy sprite; drag threshold + movement + release; hide + restore; context menu; four transparent sprites loaded; WPF rendered four states; zoom bounds; startup and clean exit.\r\n",new UTF8Encoding(false));
  }
 }
}
