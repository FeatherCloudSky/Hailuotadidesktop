using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HailuotaInstaller {
 static class Setup {
  const string Key="Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\HailuotaPet";
  const string Exe="海洛塔帝桌宠.exe";
  const string Marker="HailuotaPet-install-v2";
  public static string DefaultFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","HailuotaPet"); } }
  [STAThread] static int Main(string[] args) {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   try {
#if UNINSTALLER
    return Uninstall(args);
#else
    if(args.Contains("--preview")) { using(var form=new SetupForm()) { form.Show(); form.Refresh(); using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height)); image.Save(Option(args,"--preview","setup-preview.png")); } form.Close(); } return 0; }
    if(args.Contains("--silent")) { string folder=Option(args,"--dir",DefaultFolder); Install(folder,args.Contains("--no-shortcuts"),args.Contains("--no-registry"),delegate{}); if(!args.Contains("--no-launch")) Launch(folder); return 0; }
    Application.Run(new SetupForm()); return 0;
#endif
   } catch(Exception error) { File.WriteAllText(Path.Combine(Path.GetTempPath(),"HailuotaPet-setup-error.log"),error.ToString()); if(!args.Contains("--silent")) MessageBox.Show(error.Message,"海洛塔帝桌宠",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
  }
  static string Option(string[] args,string name,string fallback) { int i=Array.IndexOf(args,name); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
  static string Quote(string value) { return "\""+value.Replace("\"","\\\"")+"\""; }
  static string Root(string folder) {
   string full=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
   if(full==Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar)||full.Length<8) throw new Exception("安装目录无效。"); return full;
  }
  static string Inside(string root,string relative) {
   string path=Path.GetFullPath(Path.Combine(root,relative)); if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("安装包包含无效路径。"); return path;
  }
  static string Hash(string file) { using(var hash=SHA256.Create()) using(var stream=File.OpenRead(file)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant(); }
  public static void Install(string folder,bool noShortcuts,bool noRegistry,Action<string> progress) {
   folder=Root(folder); Directory.CreateDirectory(folder); progress("正在展开并校验安装文件……");
   string staging=Path.Combine(folder,".install-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);
   var installed=new List<string>();
   try {
    using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
     if(resource==null) throw new Exception("安装包数据不完整。");
     using(var zip=new ZipArchive(resource,ZipArchiveMode.Read)) foreach(var entry in zip.Entries) {
      if(string.IsNullOrEmpty(entry.Name)) continue; string path=Inside(staging,entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path));
      using(var source=entry.Open()) using(var target=File.Create(path)) source.CopyTo(target); installed.Add(entry.FullName.Replace('/',Path.DirectorySeparatorChar));
     }
    }
    var hashes=File.ReadAllLines(Path.Combine(staging,"payload.sha256")); foreach(var line in hashes) {
     int split=line.IndexOf("  ",StringComparison.Ordinal); if(split<0) throw new Exception("校验清单无效。");
     if(Hash(Inside(staging,line.Substring(split+2)))!=line.Substring(0,split)) throw new Exception("安装文件校验失败："+line.Substring(split+2));
    }
    progress("正在更新桌宠……"); StopOurs(folder);
    if(!noShortcuts) StopOldDesktopCopy();
    foreach(string relative in installed) { string target=Inside(folder,relative); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(Inside(staging,relative),target,true); }
    File.WriteAllText(Path.Combine(folder,"install.marker"),Marker); installed.Add("install.marker");
    File.WriteAllLines(Path.Combine(folder,"installed-files.txt"),installed.ToArray(),new UTF8Encoding(false));
    if(!noShortcuts) {
     progress("正在创建桌面和开始菜单快捷方式……");
     Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"海洛塔帝桌宠.lnk"),Path.Combine(folder,Exe),folder);
     string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"海洛塔帝桌宠"); Directory.CreateDirectory(menu);
     Shortcut(Path.Combine(menu,"海洛塔帝桌宠.lnk"),Path.Combine(folder,Exe),folder);
     Shortcut(Path.Combine(menu,"卸载海洛塔帝桌宠.lnk"),Path.Combine(folder,"卸载海洛塔帝桌宠.exe"),folder);
    }
    if(!noRegistry) using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
     key.SetValue("DisplayName","海洛塔帝桌宠"); key.SetValue("DisplayVersion","0.4.1"); key.SetValue("Publisher","海洛塔帝桌宠 · 个人定制"); key.SetValue("InstallLocation",folder);
     key.SetValue("DisplayIcon",Path.Combine(folder,Exe)+",0"); key.SetValue("UninstallString",Quote(Path.Combine(folder,"卸载海洛塔帝桌宠.exe")));
     key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
     key.SetValue("EstimatedSize",(int)(installed.Sum(f=>new FileInfo(Inside(folder,f)).Length)/1024),RegistryValueKind.DWord);
    }
    progress("安装完成。首次使用可选择 Codex、API 或仅桌宠模式。");
   } finally { if(Directory.Exists(staging)&&Path.GetFullPath(staging).StartsWith(folder+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) Directory.Delete(staging,true); }
  }
  static void StopOurs(string folder) {
   foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Exe))) { try { if(string.Equals(process.MainModule.FileName,Path.Combine(folder,Exe),StringComparison.OrdinalIgnoreCase)) { process.Kill(); process.WaitForExit(5000); } } catch{} }
  }
  static void StopOldDesktopCopy() {
   string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"海洛塔帝桌宠.lnk"); if(!File.Exists(path)) return;
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); dynamic link=shell.CreateShortcut(path); string target=link.TargetPath as string;
   if(string.IsNullOrEmpty(target)||Path.GetFileName(target)!=Exe||!File.Exists(Path.Combine(Path.GetDirectoryName(target),"assets","idle.png"))) return;
   string placement=Path.Combine(Path.GetDirectoryName(target),"placement.txt"), data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"HailuotaPet");
   if(File.Exists(placement)&&!File.Exists(Path.Combine(data,"placement.txt"))) { Directory.CreateDirectory(data); File.Copy(placement,Path.Combine(data,"placement.txt")); }
   StopOurs(Path.GetDirectoryName(target));
  }
  static void Shortcut(string path,string target,string folder) {
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); dynamic link=shell.CreateShortcut(path);
   link.TargetPath=target; link.WorkingDirectory=folder; link.IconLocation=Path.Combine(folder,Exe)+",0"; link.Description="海洛塔帝桌宠 · 双击聊 AI · Codex / 自定义 API"; link.Save();
  }
  public static void Launch(string folder) { Process.Start(new ProcessStartInfo(Path.Combine(folder,Exe),"") { UseShellExecute=true,WorkingDirectory=folder }); }
  static int Uninstall(string[] args) {
   if(args.Contains("--remove")) {
    string folder=Root(Option(args,"--remove","")); if(File.ReadAllText(Path.Combine(folder,"install.marker"))!=Marker) throw new Exception("不是本程序的安装目录。");
    int wait; if(int.TryParse(Option(args,"--wait","0"),out wait)) try { Process.GetProcessById(wait).WaitForExit(10000); } catch{};
    StopOurs(folder);
    RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"海洛塔帝桌宠.lnk"),folder);
    string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"海洛塔帝桌宠");
    RemoveShortcut(Path.Combine(menu,"海洛塔帝桌宠.lnk"),folder); RemoveShortcut(Path.Combine(menu,"卸载海洛塔帝桌宠.lnk"),folder);
    if(Directory.Exists(menu)&&Directory.GetFileSystemEntries(menu).Length==0) Directory.Delete(menu);
    using(var registry=Registry.CurrentUser.OpenSubKey(Key)) if(registry!=null&&string.Equals(registry.GetValue("InstallLocation") as string,folder,StringComparison.OrdinalIgnoreCase)) Registry.CurrentUser.DeleteSubKeyTree(Key,false);
    var files=File.ReadAllLines(Path.Combine(folder,"installed-files.txt")); foreach(var file in files) { string target=Inside(folder,file); if(File.Exists(target)) File.Delete(target); }
    File.Delete(Path.Combine(folder,"installed-files.txt"));
    foreach(var dir in Directory.GetDirectories(folder,"*",SearchOption.AllDirectories).OrderByDescending(s=>s.Length)) if(Directory.GetFileSystemEntries(dir).Length==0) Directory.Delete(dir);
    if(Directory.GetFileSystemEntries(folder).Length==0) Directory.Delete(folder);
    if(!args.Contains("--silent")) MessageBox.Show("桌宠已卸载。聊天记录和连接设置保留在用户目录中。","海洛塔帝桌宠"); return 0;
   }
   if(!args.Contains("--silent")&&MessageBox.Show("卸载海洛塔帝桌宠？\n聊天记录和连接设置会保留。","海洛塔帝桌宠",MessageBoxButtons.YesNo)!=DialogResult.Yes) return 0;
   string root=Root(AppDomain.CurrentDomain.BaseDirectory); string temp=Path.Combine(Path.GetTempPath(),"HailuotaPet-uninstall-"+Guid.NewGuid().ToString("N")+".exe"); File.Copy(Assembly.GetExecutingAssembly().Location,temp);
   Process.Start(new ProcessStartInfo(temp,"--remove "+Quote(root)+" --wait "+Process.GetCurrentProcess().Id+(args.Contains("--silent")?" --silent":"")) { UseShellExecute=false,CreateNoWindow=true }); return 0;
  }
  static void RemoveShortcut(string path,string root) { if(!File.Exists(path)) return; dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); dynamic link=shell.CreateShortcut(path); string target=link.TargetPath as string; if(target!=null&&Path.GetFullPath(target).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) File.Delete(path); }
 }
 class SetupForm : Form {
  readonly Label status=new Label(); readonly Button install=new Button(); readonly CheckBox launch=new CheckBox();
  bool finished,installing;
  public SetupForm() {
   Text="海洛塔帝桌宠 · 一键安装"; ClientSize=new Size(530,365); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Microsoft YaHei UI",10); BackColor=Color.FromArgb(33,30,48); ForeColor=Color.White; Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
   Controls.Add(new Label {Text="海洛塔帝桌宠",Font=new Font(Font.FontFamily,22,FontStyle.Bold),AutoSize=true,Location=new Point(28,28)});
   Controls.Add(new Label {Text="双击聊 AI  ·  Codex 账号登录  ·  自定义 API",AutoSize=true,Location=new Point(30,85),ForeColor=Color.FromArgb(204,189,225)});
   Controls.Add(new Label {Text="Windows 10/11 x64 · 无需 Python 或 Node.js\n有 Codex 桌面端 / CLI 就连接，没有就填写自己的 API。\n也可以跳过 AI，仅使用桌宠互动功能。",AutoSize=false,Size=new Size(465,75),Location=new Point(30,125)});
   Controls.Add(new Label {Text="安装位置："+Setup.DefaultFolder,AutoSize=false,Size=new Size(470,42),Location=new Point(30,205),ForeColor=Color.FromArgb(204,189,225)});
   launch.Text="安装完成后打开桌宠"; launch.Checked=true; launch.AutoSize=true; launch.Location=new Point(30,250); Controls.Add(launch);
   status.AutoSize=false; status.Size=new Size(470,30); status.Location=new Point(30,287); Controls.Add(status);
   install.Text="一键安装"; install.Size=new Size(140,38); install.Location=new Point(360,315); install.FlatStyle=FlatStyle.Flat; install.BackColor=Color.FromArgb(94,70,135); Controls.Add(install);
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(installing) e.Cancel=true; };
   install.Click+=async delegate { if(finished) { Close(); return; } installing=true; install.Enabled=false; launch.Enabled=false; try { await Task.Run(delegate { Setup.Install(Setup.DefaultFolder,false,false,delegate(string text) { BeginInvoke(new Action(delegate { status.Text=text; })); }); }); status.Text="安装完成。桌面快捷方式已创建。"; if(launch.Checked) Setup.Launch(Setup.DefaultFolder); finished=true; install.Text="完成"; install.Enabled=true; } catch(Exception error) { status.Text="安装失败："+error.Message; install.Enabled=true; launch.Enabled=true; } finally { installing=false; } };
  }
 }
}
