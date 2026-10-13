using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HailuotaPet {
 public static class AiChecks {
  static void Assert(bool condition,string message) { if(!condition) throw new Exception(message); }
  public static int Run(string[] args) {
   string checks=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"checks"); Directory.CreateDirectory(checks); AiStore.Root=Path.Combine(checks,"ai-test-user");
   try {
    CheckBackend().GetAwaiter().GetResult();
    var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
    var welcome=new WelcomeWindow(new AiSettings()); welcome.Loaded+=delegate { welcome.Dispatcher.BeginInvoke(new Action(delegate { Ui.Render(welcome,Path.Combine(checks,"welcome-window.png")); welcome.Skip(); })); }; welcome.ShowDialog();
    Assert(AiStore.LoadSettings().Provider=="none"&&AiStore.LoadSettings().SetupComplete,"Onboarding skip did not persist");
    Point head=new Point(650,500); Rect screen=new Rect(0,0,1280,720);
    var offline=new BubbleChatWindow(null,delegate{return head;},delegate{return screen;},delegate{},false); Assert(!offline.AiEnabled,"Offline mode allows sending"); offline.Shutdown();
    AiStore.Write("settings.json",new AiSettings { SetupComplete=true });
    var chat=new BubbleChatWindow(null,delegate{return head;},delegate{return screen;},delegate{},false); Assert(!chat.IsVisible,"Bubble visible before click"); chat.BringUp(true); Assert(chat.InputVisible,"Input does not open under bubble"); Ui.Render(chat,Path.Combine(checks,"bubble-input.png"));
    using(var fixture=new System.Drawing.Bitmap(180,90)) { chat.SetAttachment(RegionCapture.Encode(fixture)); Ui.Render(chat,Path.Combine(checks,"bubble-screenshot-preview.png")); chat.SetAttachment(null); }
    chat.BeginThinking(); Assert(chat.ReplyText==BubbleChatWindow.ThinkingText&&!chat.InputVisible,"Thinking bubble state incorrect"); Ui.Render(chat,Path.Combine(checks,"bubble-thinking.png"));
    chat.Receive("你好，今天想一起做点什么？我们慢慢来。尾句",false); Assert(chat.ReplyText=="你好，"&&chat.SegmentCount==3,"Streaming punctuation segmentation failed"); chat.TestAdvance(); Assert(chat.ReplyText=="今天想一起做点什么？","Segment order incorrect"); Ui.Render(chat,Path.Combine(checks,"bubble-reply.png"));
    chat.Receive("你好，今天想一起做点什么？我们慢慢来。尾句",true); chat.Receive("你好，",false); Assert(chat.SegmentCount==4,"Late delta removed final tail");
    chat.TestAdvance(); chat.TestAdvance(); Assert(chat.ReplyText=="尾句","Unpunctuated final tail was lost");
    var privateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    typeof(BubbleChatWindow).GetField("nextAt",privateFlags).SetValue(chat,-100.0);
    typeof(BubbleChatWindow).GetMethod("Tick",privateFlags).Invoke(chat,new object[]{null,EventArgs.Empty}); Assert(!chat.IsVisible,"Completed bubble did not auto-hide");
    chat.BringUp(true); typeof(Window).GetMethod("OnDeactivated",privateFlags).Invoke(chat,new object[]{EventArgs.Empty}); Assert(!chat.IsVisible,"Outside click did not hide bubble");
    chat.BringUp(true); head=new Point(20,20); chat.FollowAnchor(); Assert(chat.Left>=screen.Left&&chat.Top>=screen.Top&&chat.Left+chat.ActualWidth<=screen.Right+1,"Bubble overflowed screen edge");
    screen=new Rect(-1280,0,1280,720); head=new Point(-100,400); chat.FollowAnchor(); Assert(chat.Left>=screen.Left&&chat.Left+chat.ActualWidth<=screen.Right+1,"Bubble failed on negative-coordinate monitor");
    var settings=new SettingsWindow(new AiSettings {Provider="chat"}); settings.Show(); Ui.Render(settings,Path.Combine(checks,"settings-window.png")); settings.Close();
    var incomplete=new SettingsWindow(new AiSettings {Provider="chat",ApiUrl="",Model=""}); incomplete.Loaded+=delegate { incomplete.Dispatcher.BeginInvoke(new Action(incomplete.SkipAi)); }; incomplete.ShowDialog(); Assert(AiStore.LoadSettings().Provider=="none","API skip requires filling API fields");
    chat.Shutdown(); app.Shutdown();
    File.WriteAllText(Path.Combine(checks,"ai-self-test.txt"),"PASS: default hidden; auto-dismiss completed bubble; outside-click dismissal; reverse region selection; crop boundaries; image encoding; screenshot preview; Chat/Responses image payloads; image omitted from saved history; DPAPI key roundtrip; SSE Unicode streaming; HTTP error redaction; cancellation; onboarding/API skip; offline guard; bubble input/thinking/reply rendering; punctuation segmentation/order/decimals/ellipsis/quotes; final tail; late-delta guard; screen-edge and negative-monitor placement.\r\n"); var previous=Path.Combine(checks,"ai-test-error.txt"); if(File.Exists(previous)) File.Delete(previous); return 0;
   } catch(Exception error) { File.WriteAllText(Path.Combine(checks,"ai-test-error.txt"),error.ToString()); return 1; }
  }
  static async Task CheckBackend() {
   var selection=RegionCapture.Selection(new System.Drawing.Point(90,60),new System.Drawing.Point(-10,10),new System.Drawing.Size(100,80)); Assert(selection==new System.Drawing.Rectangle(0,10,90,50),"Reverse selection or bounds clipping failed");
   CapturedImage fixture;
   using(var image=new System.Drawing.Bitmap(240,120)) {
    using(var draw=System.Drawing.Graphics.FromImage(image)) { draw.Clear(System.Drawing.Color.Red); draw.FillRectangle(System.Drawing.Brushes.Blue,20,10,80,50); }
    fixture=RegionCapture.Crop(image,new System.Drawing.Rectangle(20,10,80,50));
    using(var stream=new MemoryStream(fixture.Bytes)) using(var decoded=new System.Drawing.Bitmap(stream)) Assert(decoded.Width==80&&decoded.Height==50&&decoded.GetPixel(40,25).B==255,"Screenshot crop leaked outside selected area");
    using(var picker=new RegionPicker(image,new System.Drawing.Rectangle(0,0,240,120))) {
     var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
     typeof(RegionPicker).GetMethod("OnMouseDown",flags).Invoke(picker,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,100,80,0)});
     typeof(RegionPicker).GetMethod("OnMouseMove",flags).Invoke(picker,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,0,20,10,0)});
     typeof(RegionPicker).GetMethod("OnMouseUp",flags).Invoke(picker,new object[]{new System.Windows.Forms.MouseEventArgs(System.Windows.Forms.MouseButtons.Left,1,20,10,0)});
     Assert(picker.Selected==new System.Drawing.Rectangle(20,10,80,70)&&picker.DialogResult==System.Windows.Forms.DialogResult.OK,"Region picker mouse selection failed");
    }
   }
   SynchronizationContext.SetSynchronizationContext(null);
   var settings=new AiSettings { Provider="chat",Model="mock-model",ApiUrl="http://127.0.0.1/v1" }; settings.SetKey("test-secret-for-dpapi");
   AiStore.Write("settings.json",settings); string raw=File.ReadAllText(Path.Combine(AiStore.Root,"settings.json")); Assert(!raw.Contains("test-secret-for-dpapi"),"Key saved in plaintext"); Assert(AiStore.LoadSettings().GetKey()=="test-secret-for-dpapi","Key roundtrip failed");
   var history=new List<ChatMessage> {new ChatMessage("user","你好")};
   var imageHistory=new List<ChatMessage>{new ChatMessage("user","看看图片"){ImageData=fixture.DataUrl,HadImage=true}};
   string imagePayload=AiStore.Json.Serialize(AiBackend.Payload(settings,imageHistory)); Assert(imagePayload.Contains("image_url")&&imagePayload.Contains(fixture.DataUrl),"Chat image missing");
   settings.Provider="responses"; imagePayload=AiStore.Json.Serialize(AiBackend.Payload(settings,imageHistory)); Assert(imagePayload.Contains("input_image")&&imagePayload.Contains("input_text")&&imagePayload.Contains(fixture.DataUrl),"Responses image missing"); settings.Provider="chat";
   AiStore.SaveHistory(settings,imageHistory); string saved=File.ReadAllText(Path.Combine(AiStore.Root,"history.json")); Assert(!saved.Contains(fixture.DataUrl)&&AiStore.LoadHistory()[0].HadImage,"Screenshot persisted in history");
   Assert(ReplySegments.Split("圆周率是3.14。",true).Count==1,"Decimal was split");
   Assert(ReplySegments.Split("地址 https://example.com/v1。",true).Count==1,"URL punctuation was split");
   var ellipsis=ReplySegments.Split("等一下......好了！",true); Assert(ellipsis.Count==2&&ellipsis[0]=="等一下......","Ellipsis was split");
   var quoted=ReplySegments.Split("他说：“我在。”",true); Assert(quoted.Count==2&&quoted[1]=="“我在。”","Closing quotation was separated");
   Assert(ReplySegments.Split("没说完",false).Count==0&&ReplySegments.Split("没说完",true).Count==1,"Streaming tail was flushed too early");
   bool offlineGuard=false; try { await AiBackend.Ask(new AiSettings {Provider="none"},history,delegate{},CancellationToken.None); } catch(InvalidOperationException) { offlineGuard=true; } Assert(offlineGuard,"Offline backend is not blocked");
   var chat=AiBackend.Payload(settings,history); Assert(!chat.ContainsKey("reasoning_effort"),"Default reasoning was sent"); settings.Reasoning="high"; Assert((string)AiBackend.Payload(settings,history)["reasoning_effort"]=="high","Chat reasoning missing");
   settings.Provider="responses"; var responses=AiBackend.Payload(settings,history); Assert(responses.ContainsKey("reasoning")&&responses.ContainsKey("input")&&!responses.ContainsKey("messages"),"Responses schema incorrect"); Assert(AiBackend.Endpoint(settings).AbsolutePath=="/v1/responses","Responses endpoint incorrect"); settings.ApiUrl="http://127.0.0.1/v1/responses"; Assert(AiBackend.Endpoint(settings).AbsolutePath=="/v1/responses","Endpoint duplicated");
   Assert(AiBackend.TextFromJson("{\"choices\":[{\"delta\":{\"content\":\"你好\"}}]}",true)=="你好","Chat delta incorrect");
   Assert(AiBackend.TextFromJson("{\"type\":\"response.output_text.delta\",\"delta\":\"你好\"}",true)=="你好","Responses delta incorrect");
   Assert(AiBackend.TextFromJson("{\"output\":[{\"content\":[{\"type\":\"output_text\",\"text\":\"你好\"}]}]}",false)=="你好","Responses text incorrect");
   string streamed="";
   string result=await MockRequest("chat",200,"text/event-stream","data: {\"choices\":[{\"delta\":{\"content\":\"你好，\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"海洛塔帝\"}}]}\n\ndata: [DONE]\n\n",delegate(string update){streamed=update;},CancellationToken.None,0);
   Assert(result=="你好，海洛塔帝"&&streamed==result,"Chat SSE transport failed");
   result=await MockRequest("responses",200,"text/event-stream","data: {\"type\":\"response.output_text.delta\",\"delta\":\"连接成功\"}\n\ndata: {\"type\":\"response.completed\"}\n\n",delegate{},CancellationToken.None,0); Assert(result=="连接成功","Responses SSE transport failed");
   result=await MockRequest("chat",200,"application/json","{\"choices\":[{\"message\":{\"content\":\"非流式成功\"}}]}",delegate{},CancellationToken.None,0); Assert(result=="非流式成功","JSON transport failed");
   bool redacted=false; try { await MockRequest("chat",400,"application/json","{\"error\":\"bad test-secret-for-dpapi\"}",delegate{},CancellationToken.None,0); } catch(Exception e) { redacted=e.Message.Contains("已隐藏密钥")&&!e.Message.Contains("test-secret-for-dpapi"); } Assert(redacted,"Error exposed key");
   bool cancelled=false; using(var token=new CancellationTokenSource()) { token.CancelAfter(100); try { await MockRequest("chat",200,"text/event-stream","",delegate{},token.Token,800); } catch(OperationCanceledException) { cancelled=true; } } Assert(cancelled,"Cancellation failed");
  }
  static async Task<string> MockRequest(string provider,int code,string type,string body,Action<string> update,CancellationToken token,int delay) {
   var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); int port=((IPEndPoint)listener.LocalEndpoint).Port;
   var server=Task.Run(async delegate {
    try { using(var client=await listener.AcceptTcpClientAsync()) using(var stream=client.GetStream()) {
     var header=new StringBuilder(); int b; while((b=stream.ReadByte())>=0) { header.Append((char)b); if(header.ToString().EndsWith("\r\n\r\n")) break; if(header.Length>16000) throw new Exception("Headers too long"); }
     int length=0; foreach(var line in header.ToString().Split('\n')) if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)) length=int.Parse(line.Substring(15).Trim());
     Assert(header.ToString().Contains("Bearer test-secret-for-dpapi"),"Authorization not sent");
     byte[] data=new byte[length]; int offset=0; while(offset<length) { int read=await stream.ReadAsync(data,offset,length-offset); if(read==0) break; offset+=read; }
     var parsed=AiStore.Json.DeserializeObject(Encoding.UTF8.GetString(data)) as Dictionary<string,object>; Assert(parsed!=null&&parsed.ContainsKey(provider=="chat"?"messages":"input"),"Transport payload incorrect");
     if(delay>0) await Task.Delay(delay);
     byte[] bytes=Encoding.UTF8.GetBytes(body), headers=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Bad Request")+"\r\nContent-Type: "+type+"; charset=utf-8\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");
     await stream.WriteAsync(headers,0,headers.Length); for(int i=0;i<bytes.Length;i+=7) await stream.WriteAsync(bytes,i,Math.Min(7,bytes.Length-i));
    }} catch(IOException) { if(!token.IsCancellationRequested) throw; }
   });
   var settings=new AiSettings {Provider=provider,Model="mock-model",ApiUrl="http://127.0.0.1:"+port+"/v1",TimeoutSeconds=15}; settings.SetKey("test-secret-for-dpapi");
   try { return await AiBackend.Ask(settings,new List<ChatMessage>{new ChatMessage("user","测试")},update,token); }
   finally { listener.Stop(); server.GetAwaiter().GetResult(); }
  }
  public static int Smoke(string[] args) {
   string checks=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"checks"); Directory.CreateDirectory(checks); AiStore.Root=Path.Combine(checks,"codex-smoke-user");
   try {
    CapturedImage fixture; using(var image=new System.Drawing.Bitmap(640,200)) { using(var draw=System.Drawing.Graphics.FromImage(image)) using(var font=new System.Drawing.Font("Arial",52)) { draw.Clear(System.Drawing.Color.White); draw.DrawString("VISION 7319",font,System.Drawing.Brushes.Black,25,60); } fixture=RegionCapture.Encode(image); }
    var settings=new AiSettings {TimeoutSeconds=180}; string result=AiBackend.Ask(settings,new List<ChatMessage>{new ChatMessage("user","只回复图片中的四位数字，不使用任何工具。"){ImageData=fixture.DataUrl,HadImage=true}},delegate{},CancellationToken.None).GetAwaiter().GetResult();
    Assert(result.Contains("7319"),"Codex did not read synthetic screenshot"); Assert(Directory.GetFiles(Path.Combine(AiStore.Root,"workspace")).Length==0,"Temporary screenshot was not cleaned");
    File.WriteAllText(Path.Combine(checks,"codex-smoke.txt"),"PASS: synthetic image vision; temporary image cleanup.\r\n"+result,new UTF8Encoding(false)); return 0;
   }
   catch(Exception error) { File.WriteAllText(Path.Combine(checks,"codex-smoke-error.txt"),error.Message,new UTF8Encoding(false)); return 1; }
  }
  public static int BubbleSmoke(string[] args) {
   string checks=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"checks"); Directory.CreateDirectory(checks); AiStore.Root=Path.Combine(checks,"bubble-smoke-user"); AiStore.Write("settings.json",new AiSettings {SetupComplete=true,TimeoutSeconds=90});
   var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown}; int code=1;
   var bubble=new BubbleChatWindow(null,delegate{return new Point(650,500);},delegate{return new Rect(0,0,1280,720);},delegate{},false); bubble.ShowActivated=false;
   bubble.Loaded+=delegate { bubble.Dispatcher.BeginInvoke(new Action(async delegate {
    try {
     var request=bubble.Submit("这是气泡连接测试。请只回复这句话，不要加其他内容：你好，我是海洛塔帝。我们一起想办法吧！");
     Assert(bubble.IsBusy&&bubble.ReplyText==BubbleChatWindow.ThinkingText,"Real send did not show thinking text"); Ui.Render(bubble,Path.Combine(checks,"bubble-live-thinking.png"));
     await request; Assert(!bubble.IsBusy&&bubble.History.Count>=2,"Real reply was not saved");
     string answer=bubble.History[bubble.History.Count-1].Content; Assert(bubble.SegmentCount>=2&&answer.Contains("海洛塔帝"),"Real response was not segmented"); Ui.Render(bubble,Path.Combine(checks,"bubble-live-reply.png"));
     File.WriteAllText(Path.Combine(checks,"bubble-live-test.txt"),"PASS: real Codex request through bubble Submit; exact thinking text; complete reply saved; punctuation segments.\r\n"+answer,new UTF8Encoding(false)); code=0;
    } catch(Exception e) { File.WriteAllText(Path.Combine(checks,"bubble-live-error.txt"),e.ToString()); }
    finally { bubble.Shutdown(); app.Shutdown(); }
   })); };
   bubble.Show(); app.Run(); return code;
  }
 }
}
