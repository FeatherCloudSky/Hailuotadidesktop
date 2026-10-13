using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HailuotaPet {
 public static class ReplySegments {
  static bool Boundary(string text,int i) {
   char ch=text[i];
   if(ch=='.') {
    if(i>0&&i+1<text.Length&&char.IsDigit(text[i-1])&&char.IsDigit(text[i+1])) return false;
    int tokenStart=i; while(tokenStart>0&&!char.IsWhiteSpace(text[tokenStart-1])) tokenStart--;
    if(i+1<text.Length&&char.IsLetterOrDigit(text[i+1])&&text.Substring(tokenStart,i-tokenStart).Contains("://")) return false;
    // A trailing ASCII period can still become a decimal or an ellipsis in the next delta.
    return true;
   }
   if(ch==':'&&i+2<text.Length&&text.Substring(i+1,2)=="//") return false;
   return "，。！？；：、,!?;:\n…".IndexOf(ch)>=0;
  }
  static bool Closing(char ch) { return "，。！？；：、,!?;:….”’\"」』）》】)]".IndexOf(ch)>=0; }
  public static List<string> Split(string text,bool final) {
   var result=new List<string>(); if(string.IsNullOrEmpty(text)) return result;
   int start=0;
   for(int i=0;i<text.Length;i++) {
    bool split=Boundary(text,i),longPart=i-start>=119&&!char.IsHighSurrogate(text[i]);
    if(!split&&!longPart) continue;
    if(text[i]=='.'&&i==text.Length-1&&!final) break;
    if(text[i]==':'&&!final&&(i==text.Length-1||(i==text.Length-2&&text[i+1]=='/'))) break;
    int end=i+1;
    if(split) while(end<text.Length&&Closing(text[end])) end++;
    string part=text.Substring(start,end-start).Trim(); if(part.Length>0) result.Add(part);
    start=end; i=end-1;
   }
   if(final&&start<text.Length) { string tail=text.Substring(start).Trim(); if(tail.Length>0) result.Add(tail); }
   return result;
  }
  public static double ReadingSeconds(string text) { return Math.Max(2,Math.Min(10,1.2+text.Length*.09)); }
 }
 public class BubbleChatWindow : Window {
  public const string ThinkingText="海洛塔帝正在深度思考中......";
  readonly TextBlock reply=Ui.Text("我在，想聊些什么？",15),caption=Ui.Text("海洛塔帝",11,"#c6b2dc"),counter=Ui.Text("",11,"#b6a6c9");
  readonly TextBox input=Ui.Input("",true);
  readonly StackPanel inputPanel=new StackPanel();
  readonly StackPanel attachmentPanel=new StackPanel {Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed};
  readonly Image thumbnail=new Image {Width=72,Height=44,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,8,6)};
  readonly TextBlock attachmentLabel=Ui.Text("",10);
  CapturedImage attachment; bool captureActive,dialogActive; double openedAt;
  readonly Button capture;
  readonly Button send,stop,previous,next,play;
  readonly DispatcherTimer playback=new DispatcherTimer(); readonly Stopwatch clock=Stopwatch.StartNew();
  readonly Func<Point> anchor; readonly Func<Rect> area; readonly Action<bool,string> feedback;
  readonly bool interactive; bool welcomed,shutdown,busy,autoplay=true,finished;
  CancellationTokenSource pending;
  List<string> parts=new List<string>(); int current=-1; double nextAt;
  public AiSettings Settings;
  public List<ChatMessage> History;
  public string ReplyText { get { return reply.Text; } }
  public int SegmentCount { get { return parts.Count; } }
  public bool InputVisible { get { return inputPanel.Visibility==Visibility.Visible; } }
  public bool AiEnabled { get { return Settings.SetupComplete&&Settings.Provider!="none"; } }
  public bool IsBusy { get { return busy; } }

  public BubbleChatWindow(Window owner,Func<Point> head,Func<Rect> workArea,Action<bool,string> petFeedback,bool askSetup=true) {
   Owner=owner; anchor=head; area=workArea; feedback=petFeedback; interactive=askSetup;
   Settings=AiStore.LoadSettings(); History=Settings.RememberHistory?AiStore.LoadHistory():new List<ChatMessage>();
   Title="海洛塔帝 · 气泡对话"; Width=380; SizeToContent=SizeToContent.Height; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
   AllowsTransparency=true; ShowInTaskbar=false; Topmost=owner==null||owner.Topmost; Ui.Theme(this); Background=Brushes.Transparent;
   var layout=new StackPanel(); Content=new ScrollViewer {Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
   var content=new StackPanel();
   var titleRow=new DockPanel {Margin=new Thickness(0,0,0,8)};
   var tools=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   tools.Children.Add(Mini("记录",OpenHistory)); tools.Children.Add(Mini("新对话",NewChat)); tools.Children.Add(Mini("设置",OpenSettings)); tools.Children.Add(Mini("×",delegate { Hide(); }));
   DockPanel.SetDock(tools,Dock.Right); titleRow.Children.Add(tools); caption.VerticalAlignment=VerticalAlignment.Center; caption.Margin=new Thickness(0); titleRow.Children.Add(caption); content.Children.Add(titleRow);
   reply.Margin=new Thickness(0); reply.LineHeight=24;
   var textScroll=new ScrollViewer {Content=reply,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=168,MinHeight=36}; content.Children.Add(textScroll);
   var navigation=new DockPanel {Margin=new Thickness(0,8,0,0)};
   var controls=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   previous=Mini("‹",delegate { Navigate(-1); }); play=Mini("暂停",TogglePlay); next=Mini("›",delegate { Navigate(1); }); stop=Mini("停止",Stop);
   controls.Children.Add(previous); controls.Children.Add(play); controls.Children.Add(next); controls.Children.Add(stop); DockPanel.SetDock(controls,Dock.Right); navigation.Children.Add(controls); counter.Margin=new Thickness(0); counter.VerticalAlignment=VerticalAlignment.Center; navigation.Children.Add(counter); content.Children.Add(navigation);
   layout.Children.Add(new Border {Background=Ui.Color("#f326223a"),BorderBrush=Ui.Color("#b49ace"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(17),Padding=new Thickness(15,11,15,10),Child=content});
   input.MaxLength=32000; input.MinHeight=0; input.Height=55; input.Margin=new Thickness(0,0,0,7); input.ToolTip="输入问题 · Enter 发送 · Shift+Enter 换行";
   input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0) { e.Handled=true; Send(); } else if(e.Key==Key.Escape) { inputPanel.Visibility=Visibility.Collapsed; FollowAnchor(); e.Handled=true; } };
   inputPanel.Children.Add(input);
   attachmentPanel.Children.Add(thumbnail); attachmentPanel.Children.Add(attachmentLabel); attachmentPanel.Children.Add(Mini("移除",delegate { SetAttachment(null); })); inputPanel.Children.Add(attachmentPanel);
   var inputFooter=new DockPanel(); var hint=Ui.Text("Enter 发送 · Shift+Enter 换行",10,"#b8a9cc"); hint.Margin=new Thickness(0); hint.VerticalAlignment=VerticalAlignment.Center;
   send=Mini("发送",Send); DockPanel.SetDock(send,Dock.Right); inputFooter.Children.Add(send); capture=Mini("截图",Capture); DockPanel.SetDock(capture,Dock.Right); inputFooter.Children.Add(capture); inputFooter.Children.Add(hint); inputPanel.Children.Add(inputFooter);
   var inputBox=new Border {CornerRadius=new CornerRadius(13),Background=Ui.Color("#f326223a"),BorderBrush=Ui.Color("#78648f"),BorderThickness=new Thickness(1),Padding=new Thickness(10),Margin=new Thickness(0,5,0,0),Child=inputPanel};
   // Collapsing the input must also remove its border and margin.
   inputBox.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("Visibility") { Source=inputPanel }); layout.Children.Add(inputBox);
   layout.Children.Add(new System.Windows.Shapes.Path { Data=Geometry.Parse("M 0 0 L 12 10 L 24 0 Z"),Fill=Ui.Color("#f326223a"),HorizontalAlignment=HorizontalAlignment.Center,Width=24,Height=10,Stretch=Stretch.Fill });
   SizeChanged+=delegate { FollowAnchor(); };
   Deactivated+=delegate { if(!dialogActive&&!captureActive) Hide(); };
   Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(!shutdown) { e.Cancel=true; Hide(); } else { playback.Stop(); if(pending!=null) pending.Cancel(); } };
   playback.Interval=TimeSpan.FromMilliseconds(80); playback.Tick+=Tick; playback.Start();
   inputPanel.Visibility=Visibility.Collapsed; Refresh();
  }
  Button Mini(string title,Action action) { var button=Ui.Button(title,action); button.FontSize=10; button.Padding=new Thickness(7,4,7,4); button.MinHeight=24; return button; }
  void Refresh() { input.IsEnabled=send.IsEnabled=capture.IsEnabled=AiEnabled&&!busy; previous.IsEnabled=current>0; next.IsEnabled=current>=0&&current+1<parts.Count; play.IsEnabled=parts.Count>1; stop.IsEnabled=busy||current+1<parts.Count; play.Content=autoplay?"暂停":"继续"; counter.Text=current<0?(busy?"等待回复":""):((current+1)+" / "+parts.Count+(finished?"":"…")); }
  public void BringUp(bool showInput=true) {
   openedAt=clock.Elapsed.TotalSeconds;
   if(!IsVisible) Show(); FollowAnchor();
   if(interactive&&!welcomed&&!Settings.SetupComplete) {
    welcomed=true; var welcome=new WelcomeWindow(Settings); welcome.Owner=this;
    dialogActive=true; bool? accepted; try { accepted=welcome.ShowDialog(); } finally { dialogActive=false; }
    if(accepted==true) { Settings=welcome.Value; if(Settings.Provider=="none") { Hide(); Refresh(); return; } if(!Settings.SetupComplete) OpenSettings(); }
   }
   if(!AiEnabled) { Hide(); return; }
   inputPanel.Visibility=showInput?Visibility.Visible:Visibility.Collapsed; Refresh(); UpdateLayout(); FollowAnchor(); if(showInput) { Activate(); input.Focus(); }
  }
  public void FollowAnchor() {
   if(!IsVisible) return; if(Owner!=null&&!Owner.IsVisible) { Hide(); return; }
   if(Owner!=null) Topmost=Owner.Topmost;
   Rect screen=area(); Point head=anchor(); double targetWidth=Math.Min(380,screen.Width-16); if(Math.Abs(Width-targetWidth)>.5) Width=targetWidth;
   MaxHeight=Math.Max(100,screen.Height-8);
   double height=ActualHeight>0?ActualHeight:250, left=head.X-Width/2, top=head.Y-height-8;
   if(top<screen.Top+4) { top=screen.Top+4; if(head.X-Width-35>=screen.Left) left=head.X-Width-35; else if(head.X+Width+35<=screen.Right) left=head.X+35; }
   Left=Math.Max(screen.Left+4,Math.Min(left,screen.Right-Width-4)); Top=Math.Max(screen.Top+4,Math.Min(top,screen.Bottom-height-4));
  }
  public void Shutdown() { shutdown=true; playback.Stop(); if(pending!=null) pending.Cancel(); Close(); }
  public void OpenSettings() {
   if(busy) { reply.Text="先停止当前回复，再修改 AI 设置。"; return; }
   if(!IsVisible) Show(); var settings=new SettingsWindow(Settings); settings.Owner=this;
   dialogActive=true; bool? accepted; try { accepted=settings.ShowDialog(); } finally { dialogActive=false; }
   if(accepted==true) { Settings=settings.Value; AiStore.SaveHistory(Settings,History); Refresh(); if(!AiEnabled) Hide(); else { reply.Text="设置已保存，想聊些什么？"; inputPanel.Visibility=Visibility.Visible; input.Focus(); } }
   FollowAnchor();
  }
  public void OpenHistory() {
   var window=new Window {Title="海洛塔帝 · 完整聊天记录",Width=520,Height=610,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner}; Ui.Theme(window);
   var text=new StringBuilder(); foreach(var message in History) text.AppendLine(message.Role=="user"?"你：":"海洛塔帝：").AppendLine(message.Content).AppendLine();
   var view=Ui.Input(text.ToString(),true); view.IsReadOnly=true; view.Margin=new Thickness(16); window.Content=view; dialogActive=true; try { window.ShowDialog(); } finally { dialogActive=false; }
  }
  void NewChat() { if(busy) { reply.Text="先停止当前回复，再开始新对话。"; return; } SetAttachment(null); History.Clear(); AiStore.SaveHistory(Settings,History); parts.Clear(); current=-1; reply.Text="我在，想聊些什么？"; inputPanel.Visibility=Visibility.Visible; Refresh(); FollowAnchor(); input.Focus(); }
  void Navigate(int change) { if(parts.Count==0) return; autoplay=false; Display(Math.Max(0,Math.Min(parts.Count-1,current+change))); Refresh(); }
  void TogglePlay() { autoplay=!autoplay; nextAt=clock.Elapsed.TotalSeconds+ReplySegments.ReadingSeconds(reply.Text); Refresh(); }
  void Stop() { if(pending!=null) pending.Cancel(); autoplay=false; Refresh(); }
  void Tick(object sender,EventArgs e) { FollowAnchor(); if(!IsVisible||!autoplay||captureActive||dialogActive) return; if(current+1<parts.Count&&clock.Elapsed.TotalSeconds>=nextAt) Display(current+1); else if(!busy&&!IsMouseOver&&!InputVisible&&finished&&current==parts.Count-1&&clock.Elapsed.TotalSeconds>=nextAt+3) Hide(); else if(!busy&&InputVisible&&!IsKeyboardFocusWithin&&!IsMouseOver&&clock.Elapsed.TotalSeconds>openedAt+12) Hide(); }
  void Display(int index) { if(index<0||index>=parts.Count) return; current=index; reply.Text=parts[index]; nextAt=clock.Elapsed.TotalSeconds+ReplySegments.ReadingSeconds(reply.Text); Refresh(); FollowAnchor(); }
  public void BeginThinking() { parts.Clear(); current=-1; finished=false; autoplay=true; reply.Text=ThinkingText; caption.Text="海洛塔帝"; inputPanel.Visibility=Visibility.Collapsed; Refresh(); UpdateLayout(); FollowAnchor(); }
  public void Receive(string full,bool complete) { if(finished&&!complete) return; parts=ReplySegments.Split(full,complete); finished=complete; if(current<0&&parts.Count>0) Display(0); else if(current>=0&&current<parts.Count) reply.Text=parts[current]; Refresh(); FollowAnchor(); }
  public void TestAdvance() { if(current+1<parts.Count) Display(current+1); }
  async void Send() { await Submit(input.Text); }
  public void SetAttachment(CapturedImage image) {
   attachment=image; attachmentPanel.Visibility=image==null?Visibility.Collapsed:Visibility.Visible; thumbnail.Source=null;
   if(image!=null) { var bitmap=new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.StreamSource=new MemoryStream(image.Bytes); bitmap.EndInit(); bitmap.Freeze(); thumbnail.Source=bitmap; attachmentLabel.Text=image.Width+" × "+image.Height+"\n发送后交给 AI"; }
   FollowAnchor();
  }
  async void Capture() {
   if(busy||captureActive||!AiEnabled) return; captureActive=true; bool ownerVisible=Owner!=null&&Owner.IsVisible;
   try { Hide(); if(ownerVisible) Owner.Hide(); await System.Threading.Tasks.Task.Delay(180); var picked=RegionCapture.Pick(); if(picked!=null) SetAttachment(picked); }
   catch(Exception error) { reply.Text="截图未完成："+error.Message; }
   finally { if(ownerVisible) Owner.Show(); captureActive=false; BringUp(true); }
  }
  public async System.Threading.Tasks.Task Submit(string question) {
   if(busy||!AiEnabled||(string.IsNullOrWhiteSpace(question)&&attachment==null)) return;
   question=string.IsNullOrWhiteSpace(question)?"请帮我看看这张截图。":question.Trim(); var sentImage=attachment;
   foreach(var old in History) old.ImageData=null;
   var userMessage=new ChatMessage("user",question) {ImageData=sentImage==null?null:sentImage.DataUrl,HadImage=sentImage!=null};
   input.Clear(); SetAttachment(null); History.Add(userMessage); busy=true; pending=new CancellationTokenSource(); BeginThinking(); feedback(true,ThinkingText);
   try {
    string answer=await AiBackend.Ask(Settings,new List<ChatMessage>(History),delegate(string full) { Dispatcher.BeginInvoke(new Action(delegate { if(!shutdown) Receive(full,false); })); },pending.Token);
    Receive(answer,true); History.Add(new ChatMessage("assistant",answer)); AiStore.SaveHistory(Settings,History); feedback(false,"");
   } catch(OperationCanceledException) { History.Remove(userMessage); reply.Text="已停止。"; parts.Clear(); current=-1; inputPanel.Visibility=Visibility.Visible; feedback(false,""); }
   catch(Exception error) { History.Remove(userMessage); input.Text=question; SetAttachment(sentImage); string message=error.Message; try { string key=Settings.GetKey(); if(key.Length>0) message=message.Replace(key,"[已隐藏密钥]"); } catch{} reply.Text="连接未成功："+message; parts.Clear(); current=-1; inputPanel.Visibility=Visibility.Visible; feedback(false,""); }
   finally { busy=false; pending.Dispose(); pending=null; Refresh(); FollowAnchor(); }
  }
 }
}
