using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HailuotaPet {
 public class CapsuleRadiusConverter : System.Windows.Data.IValueConverter {
  public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){return new CornerRadius(Math.Max(0,(double)value/2));}
  public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){throw new NotSupportedException();}
 }
 public static class Ui {
  public const string Ink="#25252b",Muted="#91899d",Purple="#7963be",Soft="#f3f2f6",Tint="#eee9fa";
  public static Brush Color(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
  public static TextBlock Text(string text,double size=13,string color=Ink) { return new TextBlock {Text=text,FontSize=size,Foreground=Color(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)}; }
  public static Button Button(string title,Action click) {
   var button=new Button {Content=title,MinHeight=44,MinWidth=64,Padding=new Thickness(20,11,20,11),Margin=new Thickness(6,0,0,0),Background=Color(Soft),Foreground=Color(Muted),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,FontSize=13};
   var border=new FrameworkElementFactory(typeof(Border),"Capsule"); border.SetBinding(Border.CornerRadiusProperty,new System.Windows.Data.Binding("ActualHeight") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self),Converter=new CapsuleRadiusConverter()}); border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center); content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);
   content.SetBinding(ContentPresenter.ContentProperty,new System.Windows.Data.Binding("Content") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   content.SetBinding(ContentPresenter.MarginProperty,new System.Windows.Data.Binding("Padding") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)}); border.AppendChild(content);
   var template=new ControlTemplate(typeof(Button)) {VisualTree=border};
   var disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false}; disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.35)); template.Triggers.Add(disabled);
   var hover=new Trigger {Property=UIElement.IsMouseOverProperty,Value=true}; hover.Setters.Add(new Setter(UIElement.OpacityProperty,.78)); template.Triggers.Add(hover);
   var pressed=new Trigger {Property=System.Windows.Controls.Button.IsPressedProperty,Value=true}; pressed.Setters.Add(new Setter(UIElement.OpacityProperty,.55)); template.Triggers.Add(pressed);
   button.Template=template; AutomationProperties.SetName(button,title); button.Click+=delegate { click(); }; return button;
  }
  public static Button Primary(string title,Action click) { var b=Button(title,click); b.Background=Color(Purple); b.Foreground=Brushes.White; return b; }
  public static System.Windows.Shapes.Path Glyph(string name,Brush ink=null) {
   string path;
   switch(name) {
    case "send":path="M12,20 L12,4 M6,10 L12,4 L18,10";break;
    case "crop":path="M8,3 L5,3 Q3,3 3,5 L3,8 M16,3 L19,3 Q21,3 21,5 L21,8 M3,16 L3,19 Q3,21 5,21 L8,21 M16,21 L19,21 Q21,21 21,19 L21,16 M8,8 L16,8 L16,16 L8,16 Z";break;
    case "settings":path="M3,7 L11,7 M17,7 L21,7 M3,17 L7,17 M13,17 L21,17 M17,7 A3,3 0 1 0 11,7 A3,3 0 1 0 17,7 M13,17 A3,3 0 1 0 7,17 A3,3 0 1 0 13,17";break;
    case "history":path="M4,10 A8,8 0 1 1 5,17 M4,4 L4,10 L10,10 M12,7 L12,12 L16,14";break;
    case "left":path="M15,5 L8,12 L15,19";break;
    case "right":path="M9,5 L16,12 L9,19";break;
    case "pause":path="M9,5 L9,19 M15,5 L15,19";break;
    case "play":path="M8,4 L19,12 L8,20 Z";break;
    case "stop":path="M6,6 L18,6 L18,18 L6,18 Z";break;
    case "plus":path="M12,4 L12,20 M4,12 L20,12";break;
    case "code":path="M8,6 L2,12 L8,18 M16,6 L22,12 L16,18 M14,3 L10,21";break;
    case "link":path="M9,15 L15,9 M8,12 L5,15 A4,4 0 0 0 11,21 L14,18 M10,6 L13,3 A4,4 0 0 1 19,9 L16,12";break;
    case "moon":path="M20,15 A8,8 0 0 1 9,4 A8,8 0 1 0 20,15 Z";break;
    case "folder":path="M3,7 L3,5 L10,5 L12,8 L21,8 L21,20 L3,20 Z";break;
    case "spark":path="M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z";break;
    default:path="M6,6 L18,18 M18,6 L6,18";break;
   }
   return new System.Windows.Shapes.Path {Data=Geometry.Parse(path),Width=18,Height=18,Stretch=Stretch.Uniform,Stroke=ink??Color(Muted),StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
  }
  public static void SetIcon(Button button,string glyph) { var icon=Glyph(glyph);icon.Width=icon.Height=Math.Min(18,button.Width-10); icon.SetBinding(Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") {Source=button}); button.Content=icon; }
  public static Button Icon(string name,string glyph,Action click,double size=30) { var b=Button(name,click); b.MinWidth=b.MinHeight=0;b.Width=b.Height=size; b.Padding=new Thickness(0); b.Background=Brushes.Transparent; b.ToolTip=name; ToolTipService.SetInitialShowDelay(b,350); SetIcon(b,glyph); return b; }
  public static CheckBox Toggle() {var box=new CheckBox {Width=34,Height=20,Cursor=Cursors.Hand};box.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'><Grid><Border x:Name='rail' Background='#b6b1c0' CornerRadius='10'/><Ellipse x:Name='thumb' Width='14' Height='14' Fill='White' Margin='3' HorizontalAlignment='Left'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='rail' Property='Background' Value='#9682c7'/><Setter TargetName='thumb' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");return box;}
  public static Expander Expand(string title,UIElement content){var expander=new Expander {Header=title,Content=content,Foreground=Color(Muted),FontSize=12,Margin=new Thickness(3,16,3,0)};expander.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Expander'><StackPanel><ToggleButton MinHeight='40' Cursor='Hand' IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='#f3f2f6' CornerRadius='20' Padding='14,10'><ContentPresenter/></Border></ControlTemplate></ToggleButton.Template><ContentPresenter Content='{TemplateBinding Header}'/></ToggleButton><ContentPresenter x:Name='body' Content='{TemplateBinding Content}' Margin='0,10,0,0' Visibility='Collapsed'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsExpanded' Value='True'><Setter TargetName='body' Property='Visibility' Value='Visible'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");return expander;}
  public static void ComboStyle(ComboBox box) {
   box.Background=Brushes.Transparent;box.Foreground=Color(Ink);box.BorderThickness=new Thickness(0);box.FontSize=12;box.MinWidth=125;box.HorizontalAlignment=HorizontalAlignment.Right;
   box.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'><Grid><ToggleButton MinHeight='40' Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='White' CornerRadius='20' Padding='12,10'><Path HorizontalAlignment='Right' Data='M0,0 L4,4 L8,0' Stroke='#91899d' StrokeThickness='1.4'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter x:Name='selection' Margin='14,8,32,8' HorizontalAlignment='Right' VerticalAlignment='Center' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}'/><TextBox x:Name='PART_EditableTextBox' Visibility='Hidden' Margin='12,8,31,8' VerticalAlignment='Center' Background='Transparent' BorderThickness='0' Foreground='#25252b' TextAlignment='Right'/><Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False' IsOpen='{TemplateBinding IsDropDownOpen}'><Border Background='White' BorderThickness='0' CornerRadius='22' Padding='8' MinWidth='190' Margin='8'><Border.Effect><DropShadowEffect BlurRadius='16' ShadowDepth='3' Opacity='.14' Color='#91899d'/></Border.Effect><ScrollViewer MaxHeight='240'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid><ControlTemplate.Triggers><Trigger Property='IsEditable' Value='True'><Setter TargetName='PART_EditableTextBox' Property='Visibility' Value='Visible'/><Setter TargetName='selection' Property='Visibility' Value='Hidden'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
   box.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBoxItem'><Setter Property='MinHeight' Value='40'/><Setter Property='Margin' Value='0,2'/><Setter Property='Padding' Value='12,8,32,8'/><Setter Property='Foreground' Value='#62596f'/><Setter Property='Background' Value='Transparent'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='pill' CornerRadius='20' Background='{TemplateBinding Background}' BorderThickness='1' BorderBrush='Transparent'><Grid><ContentPresenter Margin='{TemplateBinding Padding}' VerticalAlignment='Center'/><Path x:Name='check' Visibility='Hidden' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,13,0' Data='M0,4 L3,7 L9,0' Stroke='#7963be' StrokeThickness='1.6'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter Property='Background' Value='#f3f2f6'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='#eee9fa'/><Setter Property='Foreground' Value='#7963be'/><Setter TargetName='check' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='pill' Property='BorderBrush' Value='#b7a7d7'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
  }
  public static Image Logo(double size=24) { return new Image {Source=new BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","logo.png"))),Width=size,Height=size,Margin=new Thickness(0,0,9,0)}; }
  static void SlimScrollBars(Window w){w.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='6'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb MinHeight='24'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#d0cadb' CornerRadius='3' Margin='1'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>");}
  public static TextBox Input(string value,bool multiline=false) { return new TextBox {Text=value??"",Background=Brushes.Transparent,Foreground=Color(Ink),BorderThickness=new Thickness(0),CaretBrush=Color(Purple),SelectionBrush=Color("#c5b4ea"),Padding=new Thickness(5),FontSize=12,Margin=new Thickness(0),AcceptsReturn=multiline,TextWrapping=multiline?TextWrapping.Wrap:TextWrapping.NoWrap,VerticalScrollBarVisibility=multiline?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden,MinHeight=multiline?70:30}; }
  public static Border Surface(UIElement child,double radius=18,string color=Soft,Thickness? padding=null) { return new Border {Background=Color(color),CornerRadius=new CornerRadius(radius),Padding=padding??new Thickness(15),Child=child}; }
  public static void Theme(Window w) { w.FontFamily=new FontFamily("Microsoft YaHei UI"); w.Background=Brushes.White; w.Foreground=Color(Ink); w.UseLayoutRounding=true; SlimScrollBars(w); w.Icon=BitmapFrame.Create(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","pet.ico"))); }
  public static void Shell(Window w,string title,UIElement body) {
   Theme(w); w.WindowStyle=WindowStyle.None; w.AllowsTransparency=true; w.Background=Brushes.Transparent; w.ResizeMode=ResizeMode.NoResize; w.ShowInTaskbar=false;
   var panel=new DockPanel(); var header=new Grid {Height=50,Margin=new Thickness(18,0,18,0),Background=Brushes.Transparent};
   var traffic=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Left};
   foreach(string color in new[]{"#eb9b96","#eacb84","#a9c8a0"}) { if(traffic.Children.Count==0) { var close=Icon("关闭","x",w.Close,12); close.Margin=new Thickness(0,0,7,0); close.Background=Color(color); close.Content=null; traffic.Children.Add(close); } else traffic.Children.Add(new Ellipse {Fill=Color(color),Width=11,Height=11,Margin=new Thickness(0,0,7,0)}); }
   header.Children.Add(traffic); var label=Text(title,12,"#756d80"); label.HorizontalAlignment=HorizontalAlignment.Center; label.VerticalAlignment=VerticalAlignment.Center; label.Margin=new Thickness(0); label.IsHitTestVisible=false; header.Children.Add(label);
   header.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { if(e.OriginalSource==header) { try { w.DragMove(); } catch{} } };
   DockPanel.SetDock(header,Dock.Top); panel.Children.Add(header); panel.Children.Add(body);
   w.Content=new Border {Background=Brushes.White,CornerRadius=new CornerRadius(24),Margin=new Thickness(12),Child=panel,Effect=new DropShadowEffect {BlurRadius=22,ShadowDepth=3,Opacity=.12,Color=Colors.Gray}};
   w.MaxHeight=Math.Max(400,SystemParameters.WorkArea.Height-24);
   w.Loaded+=delegate {
    var source=PresentationSource.FromVisual(w);var scale=source==null?Matrix.Identity:source.CompositionTarget.TransformToDevice;
    double cx=w.Owner==null?w.Left+w.ActualWidth/2:w.Owner.Left+w.Owner.ActualWidth/2,cy=w.Owner==null?w.Top+w.ActualHeight/2:w.Owner.Top+w.Owner.ActualHeight/2;
    var area=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(cx*scale.M11),(int)(cy*scale.M22))).WorkingArea;
    double left=area.Left/scale.M11,top=area.Top/scale.M22,right=area.Right/scale.M11,bottom=area.Bottom/scale.M22;
    w.MaxHeight=Math.Max(250,bottom-top-8);w.MaxWidth=Math.Max(280,right-left-8);w.UpdateLayout();w.Left=Math.Max(left+4,Math.Min(w.Left,right-w.ActualWidth-4));w.Top=Math.Max(top+4,Math.Min(w.Top,bottom-w.ActualHeight-4));
   };
  }
  public static void Render(Window window,string file) { window.UpdateLayout(); var bitmap=new RenderTargetBitmap(Math.Max(1,(int)window.ActualWidth),Math.Max(1,(int)window.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(window); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(file)) png.Save(stream); }
 }
 public class WelcomeWindow : Window {
  public AiSettings Value; public string DetectedPath; bool connecting; string choice="codex";
  readonly TextBlock info=Ui.Text("",11,Ui.Muted); readonly Button[] choices=new Button[3];
  public WelcomeWindow(AiSettings source) {
   Value=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(source)); Title="海洛塔帝 · 初次见面"; Width=520; Height=610; WindowStartupLocation=WindowStartupLocation.CenterOwner;
   var panel=new StackPanel {Margin=new Thickness(30,10,30,26)}; Ui.Shell(this,"你好，初次见面",new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   var logo=Ui.Logo(54); logo.HorizontalAlignment=HorizontalAlignment.Center; logo.Margin=new Thickness(0,0,0,16); panel.Children.Add(logo);
   var heading=Ui.Text("让灵感，有个伴。",25); heading.TextAlignment=TextAlignment.Center; panel.Children.Add(heading);
   var subtitle=Ui.Text("我是海洛塔帝，很高兴见到你。\n这台电脑已经有 Codex 了吗？",12,Ui.Muted); subtitle.TextAlignment=TextAlignment.Center; subtitle.LineHeight=23; subtitle.Margin=new Thickness(0,0,0,20); panel.Children.Add(subtitle);
   string[] keys={"codex","chat","none"},titles={"有，连接 Codex","没有，使用自己的 API","先让你陪着我"},notes={"使用本机桌面端或 CLI，以及已有登录","填写接口地址、密钥与模型名称","跳过 AI，不填写任何信息也可以使用"},icons={"code","link","moon"};
   for(int i=0;i<3;i++) { int index=i; var row=new DockPanel(); var icon=Ui.Glyph(icons[i],Ui.Color(Ui.Purple)); icon.Margin=new Thickness(0,0,14,0); icon.VerticalAlignment=VerticalAlignment.Center; DockPanel.SetDock(icon,Dock.Left); row.Children.Add(icon); var words=new StackPanel(); words.Children.Add(Ui.Text(titles[i],13,Ui.Purple)); words.Children.Add(Ui.Text(notes[i],10,Ui.Muted)); row.Children.Add(words); var b=Ui.Button(titles[i],delegate {choice=keys[index]; UpdateChoice();}); b.Content=row; b.Margin=new Thickness(0,0,0,9); b.Padding=new Thickness(17,12,17,8); choices[i]=b; panel.Children.Add(b); }
   try { DetectedPath=AiBackend.FindCodex(source); info.Text="已检测到本机 Codex。"; } catch {info.Text="未检测到 Codex，也可以使用 API 或暂时跳过。";}
   info.Margin=new Thickness(7,9,0,12); panel.Children.Add(info);
   var footer=new DockPanel(); var next=Ui.Primary("继续",Continue); DockPanel.SetDock(next,Dock.Right); footer.Children.Add(next); var later=Ui.Text("之后可以随时更改",10,Ui.Muted); later.VerticalAlignment=VerticalAlignment.Center; later.Margin=new Thickness(0); footer.Children.Add(later); panel.Children.Add(footer); UpdateChoice();
  }
  void UpdateChoice() { for(int i=0;i<3;i++) choices[i].Background=Ui.Color(choice==new[]{"codex","chat","none"}[i]?Ui.Tint:Ui.Soft); }
  void Continue() { if(choice=="none") Skip(); else if(choice=="chat") {Value.Provider="chat";Value.SetupComplete=false;DialogResult=true;} else UseCodex(); }
  async void UseCodex() {
   if(connecting)return;
   if(string.IsNullOrEmpty(DetectedPath)) { var pick=new Microsoft.Win32.OpenFileDialog {Title="选择已安装的 Codex 程序",Filter="Codex 程序 (codex.exe)|codex.exe|可执行文件 (*.exe)|*.exe"}; if(pick.ShowDialog(this)!=true)return;Value.CodexPath=pick.FileName;try {DetectedPath=AiBackend.FindCodex(Value);}catch(Exception e){info.Text=e.Message;return;} }
   connecting=true;info.Text="正在检查 Codex 登录……";Value.Provider="codex";try {await AiBackend.LoginStatus(Value);Value.SetupComplete=true;}catch {Value.SetupComplete=false;}finally {connecting=false;}
   if(!IsVisible)return;if(Value.SetupComplete)AiStore.Write("settings.json",Value);DialogResult=true;
  }
  public void Skip() {Value.Provider="none";Value.SetupComplete=true;AiStore.Write("settings.json",Value);DialogResult=true;}
 }
 public class SettingsWindow : Window {
  public AiSettings Value;
  readonly ComboBox protocol=new ComboBox(),effort=new ComboBox(); readonly TextBox url,model,codex,system,timeout,tokens,temp;
  readonly PasswordBox key=new PasswordBox(); readonly CheckBox remember=Ui.Toggle(); readonly TextBlock info=Ui.Text("",11,Ui.Muted);
  readonly StackPanel codexPanel=new StackPanel(),apiPanel=new StackPanel(),commonPanel=new StackPanel(),offlinePanel=new StackPanel();
  readonly Button[] providers=new Button[3]; string selectedProvider; CancellationTokenSource testing;
  public SettingsWindow(AiSettings source) {
   Value=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(source));Title="海洛塔帝 · AI 设置";Width=620;Height=770;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   var dock=new DockPanel {Margin=new Thickness(29,9,29,25)};Ui.Shell(this,"设置",dock);
   var footer=new DockPanel {Margin=new Thickness(0,17,0,0)};DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);
   var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Ui.Button("跳过 AI",SkipAi));actions.Children.Add(Ui.Button("测试",Test));actions.Children.Add(Ui.Primary("完成",Save));footer.Children.Add(actions);
   var body=new StackPanel();dock.Children.Add(new ScrollViewer {Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   var heading=new DockPanel {Margin=new Thickness(0,0,0,20)};var star=Ui.Surface(Ui.Glyph("spark",Ui.Color(Ui.Purple)),14,Ui.Tint,new Thickness(12));star.Margin=new Thickness(0,0,12,0);DockPanel.SetDock(star,Dock.Left);heading.Children.Add(star);var words=new StackPanel();words.Children.Add(Ui.Text("AI 与连接",22));words.Children.Add(Ui.Text("选择一种方式，让灵感继续。",11,Ui.Muted));heading.Children.Add(words);body.Children.Add(heading);
   var segments=new System.Windows.Controls.Primitives.UniformGrid {Rows=1}; string[] titles={"Codex","自定义 API","仅桌宠"},ids={"codex","api","none"};
   for(int i=0;i<3;i++){int index=i;var b=Ui.Button(titles[i],delegate {selectedProvider=ids[index];ShowProvider();});b.Padding=new Thickness(12,9,12,9);b.Margin=new Thickness(2);providers[i]=b;segments.Children.Add(b);}body.Children.Add(Ui.Surface(segments,24,Ui.Soft,new Thickness(3)));
   Label(codexPanel,"本机连接");var codeGroup=new StackPanel();var loginActions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};loginActions.Children.Add(Ui.Button("登录",delegate {TryLogin(false);}));loginActions.Children.Add(Ui.Button("设备码",delegate {TryLogin(true);}));loginActions.Children.Add(Ui.Button("检查",CheckLogin));Row(codeGroup,"登录状态",loginActions);
   codex=Ui.Input(source.CodexPath);codex.ToolTip="留空自动检测本机安装";var pathRow=new DockPanel();var browse=Ui.Icon("选择 Codex 程序","folder",delegate {var pick=new Microsoft.Win32.OpenFileDialog {Filter="Codex 程序 (codex.exe)|codex.exe|可执行文件 (*.exe)|*.exe"};if(pick.ShowDialog(this)==true)codex.Text=pick.FileName;});DockPanel.SetDock(browse,Dock.Right);pathRow.Children.Add(browse);pathRow.Children.Add(codex);Row(codeGroup,"程序位置",pathRow);codexPanel.Children.Add(Ui.Surface(codeGroup));codexPanel.Children.Add(Help("使用本机 Codex 的登录状态，不需要再次填写密钥。"));body.Children.Add(codexPanel);
   Label(apiPanel,"接口配置");var apiGroup=new StackPanel();protocol.Items.Add("Chat Completions");protocol.Items.Add("Responses");protocol.SelectedIndex=source.Provider=="responses"?1:0;StyleCombo(protocol);Row(apiGroup,"接口类型",protocol);url=Ui.Input(source.ApiUrl);Row(apiGroup,"API 地址",url);
   key.Background=Brushes.Transparent;key.Foreground=Ui.Color(Ui.Ink);key.BorderThickness=new Thickness(0);key.Padding=new Thickness(5);key.FontSize=12;try {key.Password=source.GetKey();}catch {info.Text="原密钥无法解密，请重新填写。";}Row(apiGroup,"API 密钥",key);apiPanel.Children.Add(Ui.Surface(apiGroup));apiPanel.Children.Add(Help("密钥由 Windows 当前用户加密保存；支持免密本地接口。"));body.Children.Add(apiPanel);
   Label(commonPanel,"模型与回复");var modelGroup=new StackPanel();model=Ui.Input(source.Model);model.ToolTip="Codex 可留空；API 需填写实际模型 ID";Row(modelGroup,"模型名称",model);effort.IsEditable=true;foreach(string value in new[]{"default","none","minimal","low","medium","high","xhigh","max","ultra"})effort.Items.Add(value);effort.Text=source.Reasoning;StyleCombo(effort);Row(modelGroup,"思考等级",effort);remember.IsChecked=source.RememberHistory;remember.VerticalAlignment=VerticalAlignment.Center;remember.HorizontalAlignment=HorizontalAlignment.Right;remember.ToolTip="在这台电脑保存聊天记录";remember.Foreground=Ui.Color(Ui.Purple);Row(modelGroup,"记住聊天记录",remember);commonPanel.Children.Add(Ui.Surface(modelGroup));
   var advanced=new StackPanel();var advancedGroup=new StackPanel();timeout=Ui.Input(source.TimeoutSeconds.ToString());Row(advancedGroup,"回复超时 / 秒",timeout);tokens=Ui.Input(source.MaxTokens.ToString());Row(advancedGroup,"输出 Token 上限",tokens);temp=Ui.Input(source.Temperature);Row(advancedGroup,"Temperature",temp);advanced.Children.Add(Ui.Surface(advancedGroup));advanced.Children.Add(Help("Token 为 0 时跟随默认。思考等级及参数需所选模型支持。"));Label(advanced,"角色提示词");system=Ui.Input(source.SystemPrompt,true);system.Height=110;advanced.Children.Add(Ui.Surface(system,16));commonPanel.Children.Add(Ui.Expand("更多选项",advanced));body.Children.Add(commonPanel);
   var moon=Ui.Glyph("moon",Ui.Color(Ui.Purple));moon.Width=moon.Height=30;moon.HorizontalAlignment=HorizontalAlignment.Center;moon.Margin=new Thickness(0,20,0,14);offlinePanel.Children.Add(moon);var offline=Ui.Text("安静陪伴，也很好。\n\n保留点击、拖动与桌宠互动，暂不启用 AI。\n以后可以随时回来设置。",13,Ui.Muted);offline.TextAlignment=TextAlignment.Center;offline.LineHeight=25;offlinePanel.Children.Add(offline);body.Children.Add(offlinePanel);
   info.Margin=new Thickness(8,16,8,0);body.Children.Add(info);selectedProvider=source.Provider=="codex"?"codex":source.Provider=="none"?"none":"api";ShowProvider();Closing+=delegate {if(testing!=null)testing.Cancel();};
  }
  static void StyleCombo(ComboBox box){Ui.ComboStyle(box);}
  static TextBlock Help(string text){var t=Ui.Text(text,10,Ui.Muted);t.Margin=new Thickness(9,9,9,0);t.LineHeight=18;return t;}
  static void Label(StackPanel panel,string title){var t=Ui.Text(title,11,Ui.Muted);t.Margin=new Thickness(9,19,0,8);panel.Children.Add(t);}
  static void Row(StackPanel group,string title,FrameworkElement control){if(group.Children.Count>0)group.Children.Add(new Border {Height=1,Background=Ui.Color("#e9e7ef")});var grid=new Grid {MinHeight=44};grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(110)});grid.ColumnDefinitions.Add(new ColumnDefinition());var text=Ui.Text(title,12,"#766d83");text.VerticalAlignment=VerticalAlignment.Center;text.Margin=new Thickness(0);grid.Children.Add(text);Grid.SetColumn(control,1);control.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(control);group.Children.Add(grid);AutomationProperties.SetName(control,title);}
  string CurrentProvider(){return selectedProvider=="api"?(protocol.SelectedIndex==1?"responses":"chat"):selectedProvider;}
  void ShowProvider(){codexPanel.Visibility=selectedProvider=="codex"?Visibility.Visible:Visibility.Collapsed;apiPanel.Visibility=selectedProvider=="api"?Visibility.Visible:Visibility.Collapsed;commonPanel.Visibility=selectedProvider=="none"?Visibility.Collapsed:Visibility.Visible;offlinePanel.Visibility=selectedProvider=="none"?Visibility.Visible:Visibility.Collapsed;tokens.IsEnabled=temp.IsEnabled=selectedProvider=="api";for(int i=0;i<3;i++){bool active=selectedProvider==new[]{"codex","api","none"}[i];providers[i].Background=active?Brushes.White:Brushes.Transparent;providers[i].Foreground=Ui.Color(active?Ui.Purple:Ui.Muted);}info.Text=selectedProvider=="none"?"保存后仅使用桌宠，不发送 AI 请求。":"";}
  AiSettings Read(){
   if(CurrentProvider()=="none"){var offline=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(Value));offline.Provider="none";offline.SetupComplete=true;return offline;}
   int seconds,max;double temperature;if(!int.TryParse(timeout.Text,out seconds)||seconds<15||seconds>900)throw new Exception("超时请输入 15 到 900 秒。");if(!int.TryParse(tokens.Text,out max)||max<0)throw new Exception("最大 Token 请输入非负整数。");if(!string.IsNullOrWhiteSpace(temp.Text)&&(!double.TryParse(temp.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out temperature)||temperature<0||temperature>2))throw new Exception("Temperature 请输入 0 到 2，或留空。");
   string reason=effort.Text.Trim();if(reason.Length==0)reason="default";foreach(char ch in reason)if(!char.IsLetterOrDigit(ch)&&ch!='_'&&ch!='-')throw new Exception("思考等级只能包含字母、数字、下划线或短横线。");
   var result=new AiSettings {Provider=CurrentProvider(),ApiUrl=url.Text.Trim(),Model=model.Text.Trim(),CodexPath=codex.Text.Trim(),Reasoning=reason,TimeoutSeconds=seconds,MaxTokens=max,Temperature=temp.Text.Trim(),SystemPrompt=system.Text,RememberHistory=remember.IsChecked==true,SetupComplete=true};result.SetKey(key.Password);if(result.Provider!="codex"){AiBackend.Endpoint(result);if(result.Model.Length==0)throw new Exception("请填写 API 模型名称。");}else AiBackend.FindCodex(result);return result;
  }
  void Save(){try {Value=Read();AiStore.Write("settings.json",Value);DialogResult=true;}catch(Exception e){info.Text=e.Message;}}
  public void SkipAi(){Value.Provider="none";Value.SetupComplete=true;AiStore.Write("settings.json",Value);DialogResult=true;}
  void TryLogin(bool device){try {AiBackend.Login(new AiSettings {CodexPath=codex.Text.Trim()},device);info.Text="请完成浏览器登录，再点“检查”。";}catch(Exception e){info.Text=e.Message;}}
  async void CheckLogin(){info.Text="正在检查……";try {info.Text=await AiBackend.LoginStatus(new AiSettings {CodexPath=codex.Text.Trim()});}catch(Exception e){info.Text=e.Message;}}
  async void Test(){if(testing!=null)return;try {var settings=Read();testing=new CancellationTokenSource();info.Text="正在测试连接……";string answer=await AiBackend.Ask(settings,new System.Collections.Generic.List<ChatMessage>{new ChatMessage("user","这是连接测试，请只回复：连接成功")},delegate{},testing.Token);info.Text="连接成功："+answer;}catch(OperationCanceledException){info.Text="测试已停止。";}catch(Exception e){info.Text=e.Message;}finally {if(testing!=null)testing.Dispose();testing=null;}}
 }
}
