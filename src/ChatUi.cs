using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HailuotaPet {
 public static class Ui {
  public static Brush Color(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
  public static TextBlock Text(string text,double size=13,string color="#eee8ff") { return new TextBlock { Text=text,FontSize=size,Foreground=Color(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8) }; }
  public static Button Button(string title,Action click) {
   var b=new Button { Content=title,Padding=new Thickness(13,7,13,7),Margin=new Thickness(4,0,0,0),Background=Color("#514570"),Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Hand };
   var border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5)); border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   var content=new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center); content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);
   content.SetBinding(ContentPresenter.ContentProperty,new System.Windows.Data.Binding("Content") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   content.SetBinding(ContentPresenter.MarginProperty,new System.Windows.Data.Binding("Padding") {RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)}); border.AppendChild(content);
   var template=new ControlTemplate(typeof(Button)) {VisualTree=border}; var disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false}; disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.38)); template.Triggers.Add(disabled);
   var pressed=new Trigger {Property=System.Windows.Controls.Button.IsPressedProperty,Value=true}; pressed.Setters.Add(new Setter(UIElement.OpacityProperty,.7)); template.Triggers.Add(pressed); b.Template=template;
   b.Click+=delegate { click(); }; return b;
  }
  public static TextBox Input(string value,bool multiline=false) { return new TextBox { Text=value??"",Background=Color("#302b42"),Foreground=Brushes.White,BorderBrush=Color("#64547e"),Padding=new Thickness(9,7,9,7),FontSize=13,Margin=new Thickness(0,0,0,13),AcceptsReturn=multiline,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=multiline?85:34 }; }
  public static void Theme(Window w) { w.FontFamily=new FontFamily("Microsoft YaHei UI"); w.Background=Color("#211e30"); w.Foreground=Brushes.White; w.Icon=BitmapFrame.Create(new Uri(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","pet.ico"))); }
  public static void Render(Window window,string file) { window.UpdateLayout(); var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(file)) png.Save(stream); }
 }
 public class WelcomeWindow : Window {
  public AiSettings Value;
  public string DetectedPath;
  bool connecting;
  readonly TextBlock info=Ui.Text("",12,"#c8b9da");
  public WelcomeWindow(AiSettings source) {
   Value=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(source)); Title="海洛塔帝 · 初次见面"; Width=530; Height=480; ResizeMode=ResizeMode.NoResize; WindowStartupLocation=WindowStartupLocation.CenterOwner; Ui.Theme(this);
   var panel=new StackPanel { Margin=new Thickness(28) }; Content=panel;
   panel.Children.Add(Ui.Text("先选一下陪伴方式",23)); panel.Children.Add(Ui.Text("这台电脑已经安装 Codex 桌面端或 Codex CLI 了吗？",15));
   try { DetectedPath=AiBackend.FindCodex(source); info.Text="已检测到本机 Codex，可直接复用现有登录。"; } catch { info.Text="暂未检测到本机 Codex。你可以手动选择程序，或直接填写 API。"; }
   panel.Children.Add(info);
   var yes=Ui.Button("有，连接已安装的 Codex",UseCodex); yes.Margin=new Thickness(0,14,0,10); yes.Height=46; panel.Children.Add(yes);
   var api=Ui.Button("没有，填写自己的 API",delegate { Value.Provider="chat"; Value.SetupComplete=false; DialogResult=true; }); api.Margin=new Thickness(0,0,0,10); api.Height=46; panel.Children.Add(api);
   var skip=Ui.Button("跳过，暂不启用 AI",Skip); skip.Margin=new Thickness(0,0,0,16); skip.Height=46; panel.Children.Add(skip);
   panel.Children.Add(Ui.Text("跳过后保留桌宠的点击反馈、拖动和缩放。\n以后可右键桌宠 → AI 设置，随时启用聊天。",12,"#b8a9cc"));
  }
  async void UseCodex() {
   if(connecting) return;
   if(string.IsNullOrEmpty(DetectedPath)) {
    var pick=new Microsoft.Win32.OpenFileDialog {Title="选择已安装的 Codex 程序",Filter="Codex 程序 (codex.exe)|codex.exe|可执行文件 (*.exe)|*.exe"};
    if(pick.ShowDialog(this)!=true) return; Value.CodexPath=pick.FileName;
    try { DetectedPath=AiBackend.FindCodex(Value); } catch(Exception e) { info.Text=e.Message; return; }
   }
   connecting=true; info.Text="正在检查 Codex 登录……"; Value.Provider="codex";
   try { await AiBackend.LoginStatus(Value); Value.SetupComplete=true; }
   catch { Value.SetupComplete=false; }
   finally { connecting=false; }
   if(!IsVisible) return;
   if(Value.SetupComplete) AiStore.Write("settings.json",Value); DialogResult=true;
  }
  public void Skip() { Value.Provider="none"; Value.SetupComplete=true; AiStore.Write("settings.json",Value); DialogResult=true; }
 }
 public class SettingsWindow : Window {
  public AiSettings Value;
  readonly ComboBox provider=new ComboBox(),effort=new ComboBox();
  readonly TextBox url,model,codex,system,timeout,tokens,temp;
  readonly PasswordBox key=new PasswordBox(); readonly CheckBox remember=new CheckBox(); readonly TextBlock info=Ui.Text("",12,"#d0bcdf");
  CancellationTokenSource testing;
  readonly StackPanel codexPanel=new StackPanel(),apiPanel=new StackPanel(),commonPanel=new StackPanel();
  public SettingsWindow(AiSettings source) {
   Value=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(source)); Title="海洛塔帝 · AI 设置"; Width=560; Height=760; MinHeight=530; WindowStartupLocation=WindowStartupLocation.CenterOwner; Ui.Theme(this);
   var dock=new DockPanel { Margin=new Thickness(22) }; Content=dock;
   var footer=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); dock.Children.Add(footer);
   footer.Children.Add(Ui.Button("跳过 AI",SkipAi)); footer.Children.Add(Ui.Button("取消",Close)); footer.Children.Add(Ui.Button("保存",Save));
   var content=new StackPanel(); var scroll=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; dock.Children.Add(scroll);
   content.Children.Add(Ui.Text("AI 连接",23)); content.Children.Add(Ui.Text("连接已有 Codex、填写自己的 API，或仅使用桌宠。",12,"#b8a9cc"));
   provider.Margin=new Thickness(0,0,0,14); provider.Padding=new Thickness(7);
   foreach(var pair in new[]{new[]{"codex","已安装的 Codex 桌面端 / CLI"},new[]{"chat","自定义 API · Chat Completions"},new[]{"responses","自定义 API · Responses"},new[]{"none","暂不启用 AI（仅桌宠）"}}) provider.Items.Add(new ComboBoxItem { Content=pair[1],Tag=pair[0],IsSelected=source.Provider==pair[0] });
   content.Children.Add(Ui.Text("连接方式")); content.Children.Add(provider);
   var loginRow=new WrapPanel { Margin=new Thickness(0,0,0,13) };
   loginRow.Children.Add(Ui.Button("连接 / 登录 Codex",delegate { TryLogin(false); })); loginRow.Children.Add(Ui.Button("设备码登录",delegate { TryLogin(true); })); loginRow.Children.Add(Ui.Button("检查登录",CheckLogin)); codexPanel.Children.Add(loginRow);
   codex=Field(codexPanel,"Codex 程序路径（留空自动检测本机安装）",source.CodexPath);
   codexPanel.Children.Add(Ui.Button("手动选择 codex.exe",delegate { var pick=new Microsoft.Win32.OpenFileDialog {Filter="Codex 程序 (codex.exe)|codex.exe|可执行文件 (*.exe)|*.exe"}; if(pick.ShowDialog(this)==true) codex.Text=pick.FileName; })); content.Children.Add(codexPanel);
   url=Field(apiPanel,"API 地址（例如 https://example.com/v1）",source.ApiUrl);
   apiPanel.Children.Add(Ui.Text("API Key（本机加密保存；留空可连接免密本地接口）")); key.Padding=new Thickness(9); key.Margin=new Thickness(0,0,0,13); key.Background=Ui.Color("#302b42"); key.Foreground=Brushes.White;
   try { key.Password=source.GetKey(); } catch { info.Text="原密钥无法在当前 Windows 用户下解密，请重新填写。"; }
   apiPanel.Children.Add(key); content.Children.Add(apiPanel);
   model=Field(commonPanel,"模型名称（Codex 可留空；API 需填写供应商的模型 ID）",source.Model);
   commonPanel.Children.Add(Ui.Text("思考等级（默认不发送此参数）")); effort.IsEditable=true; effort.Padding=new Thickness(7); effort.Margin=new Thickness(0,0,0,13);
   foreach(var choice in new[]{"default","none","minimal","low","medium","high","xhigh","max","ultra"}) effort.Items.Add(choice); effort.Text=source.Reasoning; commonPanel.Children.Add(effort);
   commonPanel.Children.Add(Ui.Text("具体可用等级由所选模型决定；不兼容时请选择 default。",11,"#b8a9cc"));
   timeout=Field(commonPanel,"回复超时（秒，15–900）",source.TimeoutSeconds.ToString()); tokens=Field(commonPanel,"最大输出 Token（0 表示跟随模型默认，仅 API）",source.MaxTokens.ToString()); temp=Field(commonPanel,"Temperature（可留空，仅 API）",source.Temperature);
   system=Field(commonPanel,"角色 / 系统提示词",source.SystemPrompt,true); system.Height=120; content.Children.Add(commonPanel);
   remember.Content="在这台电脑保存聊天记录"; remember.Foreground=Brushes.White; remember.IsChecked=source.RememberHistory; remember.Margin=new Thickness(0,5,0,15); content.Children.Add(remember);
   commonPanel.Children.Add(Ui.Button("测试当前连接（发送一条简短测试消息）",Test)); content.Children.Add(info);
   provider.SelectionChanged+=delegate { ShowProvider(); }; ShowProvider();
   Closing+=delegate { if(testing!=null) testing.Cancel(); };
  }
  TextBox Field(StackPanel panel,string label,string value,bool multiline=false) { panel.Children.Add(Ui.Text(label)); var box=Ui.Input(value,multiline); panel.Children.Add(box); return box; }
  string CurrentProvider() { return ((ComboBoxItem)provider.SelectedItem).Tag as string; }
  void ShowProvider() { string choice=CurrentProvider(); codexPanel.Visibility=choice=="codex"?Visibility.Visible:Visibility.Collapsed; apiPanel.Visibility=choice=="chat"||choice=="responses"?Visibility.Visible:Visibility.Collapsed; commonPanel.Visibility=choice=="none"?Visibility.Collapsed:Visibility.Visible; tokens.IsEnabled=temp.IsEnabled=choice!="codex"; if(choice=="none") info.Text="AI 已关闭。保存后双击桌宠不会打开聊天。"; else info.Text=""; }
  AiSettings Read() {
   if(CurrentProvider()=="none") { var offline=AiStore.Json.Deserialize<AiSettings>(AiStore.Json.Serialize(Value)); offline.Provider="none"; offline.SetupComplete=true; return offline; }
   int seconds,max; double temperature;
   if(!int.TryParse(timeout.Text,out seconds)||seconds<15||seconds>900) throw new Exception("超时请输入 15 到 900 秒。");
   if(!int.TryParse(tokens.Text,out max)||max<0) throw new Exception("最大 Token 请输入非负整数。");
   if(!string.IsNullOrWhiteSpace(temp.Text)&&(!double.TryParse(temp.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out temperature)||temperature<0||temperature>2)) throw new Exception("Temperature 请输入 0 到 2，或留空。");
   string reason=effort.Text.Trim(); if(reason.Length==0) reason="default";
   foreach(char ch in reason) if(!char.IsLetterOrDigit(ch)&&ch!='_'&&ch!='-') throw new Exception("思考等级只能包含字母、数字、下划线或短横线。");
   var result=new AiSettings { Provider=CurrentProvider(),ApiUrl=url.Text.Trim(),Model=model.Text.Trim(),CodexPath=codex.Text.Trim(),Reasoning=reason,TimeoutSeconds=seconds,MaxTokens=max,Temperature=temp.Text.Trim(),SystemPrompt=system.Text,RememberHistory=remember.IsChecked==true,SetupComplete=true };
   result.SetKey(key.Password);
   if(result.Provider!="codex") { AiBackend.Endpoint(result); if(result.Model.Length==0) throw new Exception("请填写 API 模型名称。"); } else AiBackend.FindCodex(result);
   return result;
  }
  void Save() { try { Value=Read(); AiStore.Write("settings.json",Value); DialogResult=true; } catch(Exception e) { info.Text=e.Message; } }
  public void SkipAi() { Value.Provider="none"; Value.SetupComplete=true; AiStore.Write("settings.json",Value); DialogResult=true; }
  void TryLogin(bool device) { try { AiBackend.Login(new AiSettings { CodexPath=codex.Text.Trim() },device); info.Text="请在打开的浏览器 / 登录窗口中完成登录，再点“检查登录”。"; } catch(Exception e) { info.Text=e.Message; } }
  async void CheckLogin() { info.Text="正在检查……"; try { info.Text=await AiBackend.LoginStatus(new AiSettings { CodexPath=codex.Text.Trim() }); } catch(Exception e) { info.Text=e.Message; } }
  async void Test() { if(testing!=null) return; try { var settings=Read(); testing=new CancellationTokenSource(); info.Text="正在测试连接……"; string answer=await AiBackend.Ask(settings,new List<ChatMessage> {new ChatMessage("user","这是连接测试，请只回复：连接成功")},delegate{},testing.Token); info.Text="连接成功："+answer; } catch(OperationCanceledException) { info.Text="测试已停止。"; } catch(Exception e) { info.Text=e.Message; } finally { if(testing!=null) testing.Dispose(); testing=null; } }
 }
}
