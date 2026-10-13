using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace HailuotaPet {
 public class AiSettings {
  public string Provider="codex", Model="", ApiUrl="https://api.openai.com/v1", ApiKeyCipher="", Reasoning="default", CodexPath="";
  public string SystemPrompt="你是海洛塔帝，是桌面陪伴助手，温和、聪明、带一点幽默。默认用中文自然回答，优先准确地帮助用户。可以保持角色语气，但不要冒充真实人物或声称是游戏官方角色。你只能聊天，不能执行命令、调用工具或访问用户文件。";
  public int TimeoutSeconds=180, MaxTokens=0;
  public string Temperature="";
  public bool RememberHistory=true;
  public bool SetupComplete=false;
  public string GetKey() { if(string.IsNullOrEmpty(ApiKeyCipher)) return ""; return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyCipher),null,DataProtectionScope.CurrentUser)); }
  public void SetKey(string value) { ApiKeyCipher=string.IsNullOrEmpty(value)?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser)); }
 }
 public class ChatMessage { public string Role,Content,ImageData; public bool HadImage; public ChatMessage(){} public ChatMessage(string role,string text) { Role=role; Content=text; } }
 public static class AiStore {
  public static string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"HailuotaPet");
  public static JavaScriptSerializer Json { get { return new JavaScriptSerializer { MaxJsonLength=64*1024*1024 }; } }
  public static void Write(string name,object value) {
   Directory.CreateDirectory(Root); string target=Path.Combine(Root,name), temp=target+".tmp";
   File.WriteAllText(temp,Json.Serialize(value),new UTF8Encoding(false));
   if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target);
  }
  public static AiSettings LoadSettings() { try { var path=Path.Combine(Root,"settings.json"); return File.Exists(path)?Json.Deserialize<AiSettings>(File.ReadAllText(path)):new AiSettings(); } catch { return new AiSettings(); } }
  public static List<ChatMessage> LoadHistory() { try { var path=Path.Combine(Root,"history.json"); return File.Exists(path)?Json.Deserialize<List<ChatMessage>>(File.ReadAllText(path)):new List<ChatMessage>(); } catch { return new List<ChatMessage>(); } }
  public static void SaveHistory(AiSettings settings,List<ChatMessage> history) { if(settings.RememberHistory) Write("history.json",history.Skip(Math.Max(0,history.Count-100)).Select(m=>new ChatMessage(m.Role,m.Content) {HadImage=m.HadImage}).ToArray()); else { var path=Path.Combine(Root,"history.json"); if(File.Exists(path)) File.Delete(path); } }
 }
 public static class AiBackend {
  public static string FindCodex(AiSettings settings) {
   if(!string.IsNullOrWhiteSpace(settings.CodexPath)) { if(File.Exists(settings.CodexPath)) return settings.CodexPath; throw new Exception("设置中的 Codex 路径不存在。"); }
   string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
   if(Directory.Exists(local)) foreach(var dir in Directory.GetDirectories(local).OrderByDescending(Directory.GetLastWriteTimeUtc)) { var exe=Path.Combine(dir,"codex.exe"); if(File.Exists(exe)) return exe; }
   var paths=new List<string>((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)); paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"npm"));
   foreach(var dir in paths) { try {
    string clean=dir.Trim('"'); var exe=Path.Combine(clean,"codex.exe"); if(File.Exists(exe)) return exe;
    foreach(var relative in new[]{"node_modules\\@openai\\codex\\vendor\\x86_64-pc-windows-msvc\\codex\\codex.exe","node_modules\\@openai\\codex\\vendor\\x86_64-pc-windows-msvc\\bin\\codex.exe","node_modules\\@openai\\codex\\node_modules\\@openai\\codex-win32-x64\\vendor\\x86_64-pc-windows-msvc\\bin\\codex.exe","node_modules\\@openai\\codex-win32-x64\\vendor\\x86_64-pc-windows-msvc\\bin\\codex.exe"}) { var native=Path.Combine(clean,relative); if(File.Exists(native)) return native; }
   } catch{} }
   throw new Exception("未检测到已安装的 Codex 桌面端或 CLI。可以选择 codex.exe、填写自己的 API，或跳过 AI 功能。");
  }
  public static string Quote(string value) {
   var result=new StringBuilder("\""); int slashes=0;
   foreach(char ch in value) { if(ch=='\\') { slashes++; continue; } if(ch=='"') result.Append('\\',slashes*2+1); else result.Append('\\',slashes); result.Append(ch); slashes=0; }
   result.Append('\\',slashes*2); return result.Append('"').ToString();
  }
  public static async Task<string> LoginStatus(AiSettings settings) {
   return await RunProcess(FindCodex(settings),"login status",null,20,CancellationToken.None);
  }
  public static void Login(AiSettings settings,bool device) {
   Process.Start(new ProcessStartInfo(FindCodex(settings),device?"login --device-auth":"login") { UseShellExecute=true, WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), WindowStyle=ProcessWindowStyle.Normal });
  }
  static async Task<string> RunProcess(string exe,string args,string input,int timeout,CancellationToken token) {
   using(var process=new Process()) {
    process.StartInfo=new ProcessStartInfo(exe,args) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Directory.CreateDirectory(Path.Combine(AiStore.Root,"workspace")).FullName };
    process.Start(); var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
    if(input!=null) { var bytes=new UTF8Encoding(false).GetBytes(input); await process.StandardInput.BaseStream.WriteAsync(bytes,0,bytes.Length,token); }
    process.StandardInput.Close();
    using(var limit=CancellationTokenSource.CreateLinkedTokenSource(token)) {
     limit.CancelAfter(TimeSpan.FromSeconds(timeout));
     using(limit.Token.Register(delegate { try { if(!process.HasExited) process.Kill(); } catch{} })) {
      await Task.Run(delegate { process.WaitForExit(); });
      if(token.IsCancellationRequested) throw new OperationCanceledException(token);
      if(limit.IsCancellationRequested) throw new TimeoutException("等待回复超时，可在设置中增加超时时间。");
      string result=await output, errors=await error;
      if(process.ExitCode!=0) throw new Exception(string.IsNullOrWhiteSpace(errors)?result:errors);
      return string.IsNullOrWhiteSpace(result)?errors:result;
     }
    }
   }
  }
  public static Task<string> Ask(AiSettings settings,List<ChatMessage> history,Action<string> update,CancellationToken token) {
   if(settings.Provider=="none") throw new InvalidOperationException("尚未启用 AI，请先在设置中选择 Codex 或自定义 API。");
   return settings.Provider=="codex"?AskCodex(settings,history,update,token):AskApi(settings,history,update,token);
  }
  static async Task<string> AskCodex(AiSettings settings,List<ChatMessage> history,Action<string> update,CancellationToken token) {
   var text=new StringBuilder(settings.SystemPrompt); text.AppendLine("\n以下为聊天记录。只回答最后一条用户消息，不使用任何工具。\n");
   var images=new List<string>();
   string workspace=Directory.CreateDirectory(Path.Combine(AiStore.Root,"workspace")).FullName;
   string answer=Path.Combine(workspace,Guid.NewGuid().ToString("N")+".txt");
   string args="exec --ignore-user-config --skip-git-repo-check --ephemeral --sandbox read-only --color never --json -c approval_policy=\"never\" -c features.shell_tool=false -c features.unified_exec=false -c features.apps=false -c features.code_mode_host=false -c web_search=\"disabled\" -C "+Quote(workspace)+" -o "+Quote(answer);
   if(!string.IsNullOrWhiteSpace(settings.Model)) args+=" --model "+Quote(settings.Model.Trim());
   if(settings.Reasoning!="default" && !string.IsNullOrWhiteSpace(settings.Reasoning)) args+=" -c "+Quote("model_reasoning_effort=\""+settings.Reasoning+"\"");
   try {
    foreach(var message in history.Skip(Math.Max(0,history.Count-30))) {
     text.AppendLine((message.Role=="user"?"用户":"助手")+": "+message.Content);
     if(!string.IsNullOrEmpty(message.ImageData)) {
      string image=Path.Combine(workspace,Guid.NewGuid().ToString("N")+(message.ImageData.StartsWith("data:image/png;")?".png":".jpg")); images.Add(image);
      File.WriteAllBytes(image,Convert.FromBase64String(message.ImageData.Substring(message.ImageData.IndexOf(',')+1))); args+=" --image "+Quote(image); text.AppendLine("[此消息附有截图 "+images.Count+"]");
     } else if(message.HadImage) text.AppendLine("[此消息的历史截图已释放，不可查看]");
    }
    args+=" -";
    await RunProcess(FindCodex(settings),args,text.ToString(),settings.TimeoutSeconds,token);
    if(!File.Exists(answer)) throw new Exception("Codex 未返回回复。请在设置中检查登录状态及模型名称。");
    string result=File.ReadAllText(answer,Encoding.UTF8).Trim(); if(result.Length==0) throw new Exception("Codex 返回了空回复。");
    update(result); return result;
   } finally { if(File.Exists(answer)) File.Delete(answer); foreach(string image in images) if(File.Exists(image)) File.Delete(image); }
  }
  public static Uri Endpoint(AiSettings settings) {
   string url=(settings.ApiUrl??"").Trim().TrimEnd('/');
   if(!url.EndsWith("/responses",StringComparison.OrdinalIgnoreCase)&&!url.EndsWith("/chat/completions",StringComparison.OrdinalIgnoreCase)) url+=settings.Provider=="responses"?"/responses":"/chat/completions";
   Uri uri; if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)) throw new Exception("API 地址需为有效的 http/https 地址。");
   return uri;
  }
  public static Dictionary<string,object> Payload(AiSettings settings,List<ChatMessage> history) {
   if(string.IsNullOrWhiteSpace(settings.Model)) throw new Exception("请先在设置里填写 API 模型名称。");
   var payload=new Dictionary<string,object> { {"model",settings.Model.Trim()},{"stream",true} };
   var messages=new List<object>();
   if(settings.Provider=="responses") {
    payload["instructions"]=settings.SystemPrompt; payload["store"]=false;
    foreach(var m in history.Skip(Math.Max(0,history.Count-30))) messages.Add(new { role=m.Role, content=MessageContent(m,true) }); payload["input"]=messages;
    if(settings.Reasoning!="default") payload["reasoning"]=new { effort=settings.Reasoning };
    if(settings.MaxTokens>0) payload["max_output_tokens"]=settings.MaxTokens;
   } else {
    messages.Add(new {role="system",content=settings.SystemPrompt}); foreach(var m in history.Skip(Math.Max(0,history.Count-30))) messages.Add(new {role=m.Role,content=MessageContent(m,false)}); payload["messages"]=messages;
    if(settings.Reasoning!="default") payload["reasoning_effort"]=settings.Reasoning;
    if(settings.MaxTokens>0) payload["max_completion_tokens"]=settings.MaxTokens;
   }
   double temp; if(!string.IsNullOrWhiteSpace(settings.Temperature)&&double.TryParse(settings.Temperature,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out temp)) payload["temperature"]=temp;
   return payload;
  }
  static object MessageContent(ChatMessage message,bool responses) {
   string text=message.Content+(message.HadImage&&string.IsNullOrEmpty(message.ImageData)?"\n[历史截图已释放]":"");
   if(string.IsNullOrEmpty(message.ImageData)) return text;
   if(responses) return new object[]{new {type="input_text",text=text},new {type="input_image",image_url=message.ImageData,detail="auto"}};
   return new object[]{new {type="text",text=text},new {type="image_url",image_url=new {url=message.ImageData,detail="auto"}}};
  }
  public static string TextFromJson(string json,bool streaming) {
   var obj=AiStore.Json.DeserializeObject(json) as Dictionary<string,object>; if(obj==null) return "";
   object value;
   if(obj.TryGetValue("error",out value)&&value!=null) throw new Exception(AiStore.Json.Serialize(value));
   if(obj.TryGetValue("type",out value)) {
    string type=value as string; if(type=="response.output_text.delta") return obj.ContainsKey("delta")?obj["delta"] as string:"";
    if(type=="response.failed") throw new Exception("Responses 请求失败："+json);
    if(streaming) return "";
   }
   if(obj.TryGetValue("choices",out value)) {
    var choices=value as object[]; if(choices!=null&&choices.Length>0) {
     var choice=choices[0] as Dictionary<string,object>; object message;
     if(choice!=null&&choice.TryGetValue(streaming?"delta":"message",out message)) { var part=message as Dictionary<string,object>; if(part!=null&&part.TryGetValue("content",out value)) return value as string??""; }
    }
   }
   if(obj.TryGetValue("output_text",out value)&&value is string) return (string)value;
   if(obj.TryGetValue("output",out value)&&value is object[]) {
    var result=new StringBuilder(); foreach(var output in (object[])value) { var item=output as Dictionary<string,object>; object content; if(item==null||!item.TryGetValue("content",out content)||!(content is object[])) continue;
     foreach(var c in (object[])content) { var part=c as Dictionary<string,object>; object t; if(part!=null&&part.TryGetValue("text",out t)&&t is string) result.Append((string)t); }
    } return result.ToString();
   }
   return "";
  }
  static async Task<string> AskApi(AiSettings settings,List<ChatMessage> history,Action<string> update,CancellationToken token) {
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(Endpoint(settings)); request.Method="POST"; request.ContentType="application/json; charset=utf-8"; request.Accept="text/event-stream, application/json"; request.AllowAutoRedirect=false; request.ServicePoint.Expect100Continue=false;
   string key=settings.GetKey(); if(!string.IsNullOrEmpty(key)) request.Headers["Authorization"]="Bearer "+key;
   var data=Encoding.UTF8.GetBytes(AiStore.Json.Serialize(Payload(settings,history))); request.ContentLength=data.Length;
   using(var limit=CancellationTokenSource.CreateLinkedTokenSource(token)) {
    limit.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
    using(limit.Token.Register(request.Abort)) {
     try {
      using(var stream=await request.GetRequestStreamAsync()) await stream.WriteAsync(data,0,data.Length,limit.Token);
      using(var response=(HttpWebResponse)await request.GetResponseAsync()) using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
       if((int)response.StatusCode>=300) throw new Exception("API 返回重定向，请直接填写最终 API 地址。");
       if((response.ContentType??"").IndexOf("text/event-stream",StringComparison.OrdinalIgnoreCase)<0) { string full=TextFromJson(await reader.ReadToEndAsync(),false); if(full.Length==0) throw new Exception("API 没有返回可显示的文本。"); update(full); return full; }
       var result=new StringBuilder(); var eventData=new StringBuilder();
       while(true) {
        string line=await reader.ReadLineAsync();
        if(line==null||line.Length==0) {
         if(eventData.Length>0) { string part=eventData.ToString().TrimEnd('\n'); eventData.Clear(); if(part=="[DONE]") break; string delta=TextFromJson(part,true); if(!string.IsNullOrEmpty(delta)) { result.Append(delta); update(result.ToString()); } }
         if(line==null) break;
        } else if(line.StartsWith("data:")) eventData.Append(line.Substring(5).TrimStart()).Append('\n');
       }
       if(result.Length==0) throw new Exception("API 返回了空回复，请检查接口类型、模型和参数。"); return result.ToString();
      }
     } catch(WebException error) {
      if(token.IsCancellationRequested) throw new OperationCanceledException(token);
      if(limit.IsCancellationRequested) throw new TimeoutException("等待 API 回复超时。");
      string detail=error.Message; if(error.Response!=null) using(var reader=new StreamReader(error.Response.GetResponseStream())) detail=reader.ReadToEnd();
      if(!string.IsNullOrEmpty(key)) detail=detail.Replace(key,"[已隐藏密钥]"); throw new Exception("API 请求失败："+detail);
     }
    }
   }
  }
 }
}
