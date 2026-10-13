using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Forms=System.Windows.Forms;

namespace HailuotaPet {
 public class CapturedImage {
  public byte[] Bytes; public string Mime; public int Width,Height;
  public string DataUrl { get { return "data:"+Mime+";base64,"+Convert.ToBase64String(Bytes); } }
 }
 public static class RegionCapture {
  public static Rectangle Selection(Point first,Point second,Size bounds) {
   int x1=Math.Max(0,Math.Min(bounds.Width,first.X)), y1=Math.Max(0,Math.Min(bounds.Height,first.Y));
   int x2=Math.Max(0,Math.Min(bounds.Width,second.X)), y2=Math.Max(0,Math.Min(bounds.Height,second.Y));
   return Rectangle.FromLTRB(Math.Min(x1,x2),Math.Min(y1,y2),Math.Max(x1,x2),Math.Max(y1,y2));
  }
  public static CapturedImage Encode(Bitmap image) {
   double ratio=Math.Min(1,2560.0/Math.Max(image.Width,image.Height));
   using(var scaled=new Bitmap(Math.Max(1,(int)(image.Width*ratio)),Math.Max(1,(int)(image.Height*ratio)))) {
    using(var draw=Graphics.FromImage(scaled)) { draw.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; draw.DrawImage(image,new Rectangle(0,0,scaled.Width,scaled.Height)); }
    using(var stream=new MemoryStream()) {
     scaled.Save(stream,ImageFormat.Png); string mime="image/png";
     if(stream.Length>6*1024*1024) { stream.SetLength(0); ImageCodecInfo jpeg=null; foreach(var codec in ImageCodecInfo.GetImageEncoders()) if(codec.MimeType=="image/jpeg") jpeg=codec;
      using(var parameters=new EncoderParameters(1)) { parameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,92L); scaled.Save(stream,jpeg,parameters); } mime="image/jpeg";
     }
     return new CapturedImage {Bytes=stream.ToArray(),Mime=mime,Width=scaled.Width,Height=scaled.Height};
    }
   }
  }
  public static CapturedImage Crop(Bitmap frozen,Rectangle selection) {
   selection.Intersect(new Rectangle(Point.Empty,frozen.Size)); if(selection.Width<1||selection.Height<1) throw new ArgumentException("截图区域为空。");
   using(var crop=frozen.Clone(selection,PixelFormat.Format32bppArgb)) return Encode(crop);
  }
  // Invoked only by the user's screenshot button. No image is saved to disk here.
  public static CapturedImage Pick() {
   Rectangle bounds=Forms.SystemInformation.VirtualScreen;
   using(var frozen=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb)) {
    using(var draw=Graphics.FromImage(frozen)) draw.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
    using(var picker=new RegionPicker(frozen,bounds)) return picker.ShowDialog()==Forms.DialogResult.OK?Crop(frozen,picker.Selected):null;
   }
  }
 }
 public class RegionPicker : Forms.Form {
  readonly Bitmap frozen; Point start; bool selecting; public Rectangle Selected;
  public RegionPicker(Bitmap snapshot,Rectangle bounds) {
   frozen=snapshot; FormBorderStyle=Forms.FormBorderStyle.None; StartPosition=Forms.FormStartPosition.Manual; Bounds=bounds; AutoScaleMode=Forms.AutoScaleMode.None; TopMost=true; ShowInTaskbar=false; Cursor=Forms.Cursors.Cross; DoubleBuffered=true; KeyPreview=true;
  }
  protected override void OnPaint(Forms.PaintEventArgs e) {
   base.OnPaint(e); e.Graphics.DrawImageUnscaled(frozen,0,0);
   using(var shade=new SolidBrush(Color.FromArgb(105,0,0,0))) {
    if(Selected.Width<=0||Selected.Height<=0) e.Graphics.FillRectangle(shade,ClientRectangle);
    else { e.Graphics.FillRectangle(shade,0,0,Width,Selected.Top); e.Graphics.FillRectangle(shade,0,Selected.Bottom,Width,Height-Selected.Bottom); e.Graphics.FillRectangle(shade,0,Selected.Top,Selected.Left,Selected.Height); e.Graphics.FillRectangle(shade,Selected.Right,Selected.Top,Width-Selected.Right,Selected.Height); }
   }
   if(Selected.Width>0&&Selected.Height>0) using(var pen=new Pen(Color.FromArgb(216,191,255),2)) e.Graphics.DrawRectangle(pen,Selected);
   string text=selecting?(Selected.Width+" × "+Selected.Height+"  ·  松开完成") : "拖动框选屏幕区域  ·  Esc / 右键取消";
   Point pointer=PointToClient(Forms.Cursor.Position); int x=Math.Max(8,Math.Min(pointer.X+18,Width-350)),y=Math.Max(8,Math.Min(pointer.Y+22,Height-44));
   e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   using(var capsule=new System.Drawing.Drawing2D.GraphicsPath()) {
    capsule.AddArc(x,y,32,32,90,180);capsule.AddArc(x+308,y,32,32,270,180);capsule.CloseFigure();
    using(var fill=new SolidBrush(Color.FromArgb(248,255,255,255))) e.Graphics.FillPath(fill,capsule);
   }
   using(var font=new Font("Microsoft YaHei UI",10)) using(var foreground=new SolidBrush(Color.FromArgb(115,102,137))) e.Graphics.DrawString(text,font,foreground,x+14,y+6);
  }
  protected override void OnMouseDown(Forms.MouseEventArgs e) {
   if(e.Button==Forms.MouseButtons.Right) { Cancel(); return; }
   if(e.Button==Forms.MouseButtons.Left) { start=e.Location; selecting=true; Selected=Rectangle.Empty; Capture=true; Invalidate(); }
  }
  protected override void OnMouseMove(Forms.MouseEventArgs e) { if(selecting) Selected=RegionCapture.Selection(start,e.Location,frozen.Size); Invalidate(); }
  protected override void OnMouseUp(Forms.MouseEventArgs e) {
   if(!selecting||e.Button!=Forms.MouseButtons.Left) return; Selected=RegionCapture.Selection(start,e.Location,frozen.Size); selecting=false; Capture=false;
   if(Selected.Width>=8&&Selected.Height>=8) { DialogResult=Forms.DialogResult.OK; Close(); } else Invalidate();
  }
  protected override bool ProcessCmdKey(ref Forms.Message msg,Forms.Keys key) { if(key==Forms.Keys.Escape) { Cancel(); return true; } return base.ProcessCmdKey(ref msg,key); }
  void Cancel() { selecting=false; Capture=false; DialogResult=Forms.DialogResult.Cancel; Close(); }
 }
}
