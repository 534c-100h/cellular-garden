using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CellularGarden {
    internal sealed class WallpaperAppearance : IDisposable {
        private readonly Settings settings;
        internal readonly string ImagePath;
        private Bitmap source, screenImage, frostedImage;
        private Color recommendedDark, recommendedLight, cachedBackground;
        internal string Error="";
        internal bool HasImage {get{return source!=null;}}
        internal WallpaperAppearance(Settings s,string path){settings=s;ImagePath=path;Reload();}
        internal void Reload(){
            if(source!=null){source.Dispose();source=null;}InvalidateImage();Error="";
            if(!File.Exists(ImagePath))return;
            try{source=ReadImage(ImagePath);Recommend();}catch(Exception e){Error="图片无法读取："+e.Message;}
        }
        private static Bitmap ReadImage(string path){
            using(var image=Image.FromFile(path)){
                if((long)image.Width*image.Height>80000000)throw new ArgumentException("图片过大，请选择不超过 8000 万像素的图片。");
                if(image.PropertyIdList.Contains(0x112)){
                    int orientation=image.GetPropertyItem(0x112).Value[0];
                    RotateFlipType[] rotations={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};
                    if(orientation>=1&&orientation<=8)image.RotateFlip(rotations[orientation]);
                }
                float scale=Math.Min(1,4096f/Math.Max(image.Width,image.Height));
                var copy=new Bitmap(Math.Max(1,(int)(image.Width*scale)),Math.Max(1,(int)(image.Height*scale)),PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(copy)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,new Rectangle(0,0,copy.Width,copy.Height));}
                return copy;
            }
        }
        internal void Import(string path){
            // Fully decode first; failed or cancelled imports leave the old wallpaper intact.
            using(var candidate=ReadImage(path)){
                Color nextDark=RecommendColor(candidate,true),nextLight=RecommendColor(candidate,false);
                Bitmap nextSource=(Bitmap)candidate.Clone();
                string temp=ImagePath+".tmp";
                try{candidate.Save(temp,ImageFormat.Png);if(File.Exists(ImagePath))File.Replace(temp,ImagePath,null);else File.Move(temp,ImagePath);}
                catch{nextSource.Dispose();throw;}
                finally{if(File.Exists(temp))File.Delete(temp);}
                if(source!=null)source.Dispose();source=nextSource;recommendedDark=nextDark;recommendedLight=nextLight;InvalidateImage();Error="";
            }
            settings.ImageName=Path.GetFileName(path);settings.ImageBackgroundDark=null;settings.ImageBackgroundLight=null;
            settings.AppearanceMode=1;
        }
        internal static Color Blend(Color background,Color foreground,float alpha){return Color.FromArgb(
            (int)Math.Round(background.R+(foreground.R-background.R)*alpha),
            (int)Math.Round(background.G+(foreground.G-background.G)*alpha),
            (int)Math.Round(background.B+(foreground.B-background.B)*alpha));}
        internal static double Luminance(Color c){return (.2126*c.R+.7152*c.G+.0722*c.B)/255;}
        internal static string Hex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
        internal static Color Parse(string value,Color fallback){if(value==null||value.Length!=7||value[0]!='#')return fallback;int rgb;return int.TryParse(value.Substring(1),System.Globalization.NumberStyles.HexNumber,null,out rgb)?Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255):fallback;}
        internal static Color RecommendColor(Bitmap image,bool dark){
            // Find a frequent image colour in the desired luminance range, retaining its hue.
            var counts=new int[512];var red=new long[512];var green=new long[512];var blue=new long[512];
            Color average=Color.Gray;long sr=0,sg=0,sb=0,total=0;
            for(int y=0;y<image.Height;y+=Math.Max(1,image.Height/80))for(int x=0;x<image.Width;x+=Math.Max(1,image.Width/80)){
                Color c=image.GetPixel(x,y);if(c.A<128)continue;sr+=c.R;sg+=c.G;sb+=c.B;total++;
                double l=Luminance(c);if(dark?l>.38:l<.66)continue;
                int bin=(c.R/32)*64+(c.G/32)*8+c.B/32;counts[bin]++;red[bin]+=c.R;green[bin]+=c.G;blue[bin]+=c.B;
            }
            if(total>0)average=Color.FromArgb((int)(sr/total),(int)(sg/total),(int)(sb/total));
            int best=Array.IndexOf(counts,counts.Max());
            if(counts[best]>0)return Color.FromArgb((int)(red[best]/counts[best]),(int)(green[best]/counts[best]),(int)(blue[best]/counts[best]));
            return dark?Blend(Color.Black,average,.22f):Blend(Color.White,average,.15f);
        }
        private void Recommend(){recommendedDark=RecommendColor(source,true);recommendedLight=RecommendColor(source,false);}
        internal Color Background(Palette p){return settings.AppearanceMode==1?
            Parse(p.Dark?settings.ImageBackgroundDark:settings.ImageBackgroundLight,HasImage?(p.Dark?recommendedDark:recommendedLight):p.Background):
            Parse(p.Dark?settings.SolidBackgroundDark:settings.SolidBackgroundLight,p.Background);}
        internal Color CellColor(Palette p){return Parse(p.Dark?settings.SolidCellDark:settings.SolidCellLight,p.Alive);}
        internal Palette ControlPalette(Palette theme){
            Color bg=Background(theme);var p=new Palette(Luminance(bg)<.45);
            p.Background=bg;p.Panel=Blend(bg,p.Dark?Color.Black:Color.White,.12f);
            p.Text=Readable(p.Panel,p.Dark?Color.FromArgb(240,243,245):Color.FromArgb(24,27,30));
            p.Surface=Blend(p.Panel,p.Text,.09f);p.Line=Blend(p.Panel,p.Text,.22f);p.Muted=Readable(p.Panel,Blend(p.Panel,p.Text,.65f));
            Color accent=settings.AppearanceMode==1&&HasImage?(p.Dark?recommendedLight:recommendedDark):CellColor(theme);
            p.Accent=Readable(p.Surface,accent);p.OnAccent=Readable(p.Accent,p.Background);p.Alive=accent;return p;
        }
        internal static double Contrast(Color a,Color b){double x=RelativeLight(a),y=RelativeLight(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        private static double RelativeLight(Color c){return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
        private static double Linear(byte value){double n=value/255.0;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}
        private static Color Readable(Color background,Color proposed){Color end=Contrast(background,Color.White)>Contrast(background,Color.Black)?Color.White:Color.Black;for(int i=0;i<=10;i++){Color c=Blend(proposed,end,i/10f);if(Contrast(background,c)>=4.5)return c;}return end;}
        internal bool AutomaticBackground(bool dark){return string.IsNullOrEmpty(dark?settings.ImageBackgroundDark:settings.ImageBackgroundLight);}
        internal void SetBackground(bool dark,Color color){string hex=Hex(color);if(settings.AppearanceMode==1){if(dark)settings.ImageBackgroundDark=hex;else settings.ImageBackgroundLight=hex;}else{if(dark)settings.SolidBackgroundDark=hex;else settings.SolidBackgroundLight=hex;}}
        internal void SetCellColor(bool dark,Color color){if(dark)settings.SolidCellDark=Hex(color);else settings.SolidCellLight=Hex(color);}
        internal void ResetColors(bool dark){if(settings.AppearanceMode==1){if(dark)settings.ImageBackgroundDark=null;else settings.ImageBackgroundLight=null;}else if(dark){settings.SolidBackgroundDark=null;settings.SolidCellDark=null;}else{settings.SolidBackgroundLight=null;settings.SolidCellLight=null;}}
        private void InvalidateImage(){if(screenImage!=null){screenImage.Dispose();screenImage=null;}if(frostedImage!=null){frostedImage.Dispose();frostedImage=null;}}
        internal Bitmap ScreenImage(Size size,Color background){
            if(!HasImage)return null;
            if(screenImage!=null&&screenImage.Size==size&&cachedBackground==background)return screenImage;
            InvalidateImage();screenImage=new Bitmap(Math.Max(1,size.Width),Math.Max(1,size.Height),PixelFormat.Format32bppPArgb);cachedBackground=background;
            using(var g=Graphics.FromImage(screenImage)){
                g.Clear(background);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                float scale=Math.Max(size.Width/(float)source.Width,size.Height/(float)source.Height);
                float width=source.Width*scale,height=source.Height*scale;
                using(var attrs=new ImageAttributes()){attrs.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(source,new Rectangle((int)Math.Floor((size.Width-width)/2),(int)Math.Floor((size.Height-height)/2),(int)Math.Ceiling(width),(int)Math.Ceiling(height)),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attrs);}
            }
            return screenImage;
        }
        internal Bitmap FrostedImage(Size size,Color background){
            Bitmap sharp=ScreenImage(size,background);if(sharp==null)return null;if(frostedImage!=null)return frostedImage;
            // Blur a reduced copy in three separable passes, only when image/size/colour changes.
            using(var small=new Bitmap(Math.Max(1,size.Width/4),Math.Max(1,size.Height/4),PixelFormat.Format32bppArgb)){
                using(var g=Graphics.FromImage(small)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(sharp,new Rectangle(Point.Empty,small.Size));}
                var data=small.LockBits(new Rectangle(Point.Empty,small.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
                try{int[] pixels=new int[small.Width*small.Height],buffer=new int[pixels.Length];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                    for(int pass=0;pass<3;pass++){BlurPass(pixels,buffer,small.Width,small.Height,true);BlurPass(buffer,pixels,small.Width,small.Height,false);}
                    Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
                }finally{small.UnlockBits(data);}
                frostedImage=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(frostedImage)){g.Clear(background);g.InterpolationMode=InterpolationMode.HighQualityBicubic;using(var attrs=Opacity(.12f))g.DrawImage(small,new Rectangle(Point.Empty,size),0,0,small.Width,small.Height,GraphicsUnit.Pixel,attrs);}
            }
            return frostedImage;
        }
        private static void BlurPass(int[] input,int[] output,int width,int height,bool horizontal){
            const int radius=4;int length=horizontal?width:height,lines=horizontal?height:width;
            for(int line=0;line<lines;line++){
                int r=0,g=0,b=0;
                for(int k=-radius;k<=radius;k++){int pos=Math.Max(0,Math.Min(length-1,k)),c=input[horizontal?line*width+pos:pos*width+line];r+=(c>>16)&255;g+=(c>>8)&255;b+=c&255;}
                for(int pos=0;pos<length;pos++){
                    output[horizontal?line*width+pos:pos*width+line]=unchecked((int)0xff000000)|(r/9<<16)|(g/9<<8)|b/9;
                    int remove=Math.Max(0,pos-radius),add=Math.Min(length-1,pos+radius+1);
                    int a=input[horizontal?line*width+remove:remove*width+line],z=input[horizontal?line*width+add:add*width+line];
                    r+=((z>>16)&255)-((a>>16)&255);g+=((z>>8)&255)-((a>>8)&255);b+=(z&255)-(a&255);
                }
            }
        }
        private static ImageAttributes Opacity(float alpha){var attrs=new ImageAttributes();var matrix=new ColorMatrix();matrix.Matrix33=alpha;attrs.SetColorMatrix(matrix);return attrs;}
        internal void Render(Graphics g,Size viewport,Size screen,Point screenOffset,Viewport view,Universe world,Palette palette){
            Color background=Background(palette),cellColor=CellColor(palette);g.Clear(background);
            float s=view.CellSize;PointF o=new PointF(view.Origin.X+world.Left*s,view.Origin.Y+world.Top*s);
            int x0=Math.Max(0,(int)Math.Floor(-o.X/s)),x1=Math.Min(world.Width,(int)Math.Ceiling((viewport.Width-o.X)/s));
            int y0=Math.Max(0,(int)Math.Floor(-o.Y/s)),y1=Math.Min(world.Height,(int)Math.Ceiling((viewport.Height-o.Y)/s));
            Bitmap image=settings.AppearanceMode==1?ScreenImage(screen,background):null;
            if(image!=null)g.DrawImageUnscaled(FrostedImage(screen,background),-screenOffset.X,-screenOffset.Y);
            using(var alive=new SolidBrush(cellColor)){
                TextureBrush texture=image==null?null:new TextureBrush(image,WrapMode.Clamp);
                if(texture!=null)texture.TranslateTransform(-screenOffset.X,-screenOffset.Y);
                var trails=new SolidBrush[9];
                var ghostTextures=new TextureBrush[9];
                for(int n=1;n<=8;n++){
                    trails[n]=new SolidBrush(Blend(background,cellColor,n/8f*(palette.Dark?.28f:.24f)));
                    if(image!=null)using(var attrs=Opacity(n/8f*.45f)){ghostTextures[n]=new TextureBrush(image,new Rectangle(Point.Empty,image.Size),attrs);ghostTextures[n].TranslateTransform(-screenOffset.X,-screenOffset.Y);}
                }
                try{
                    for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){
                        int i=y*world.Width+x;bool living=world.Cells[i]!=0;int ghost=settings.Trails?world.Ghosts[i]:0;
                        if(!living&&ghost==0)continue;
                        float gap=s>=5?Math.Max(1,s*.14f):.5f;var rect=new RectangleF(o.X+x*s+gap/2,o.Y+y*s+gap/2,s-gap,s-gap);
                        if(texture!=null)g.FillRectangle(living?texture:ghostTextures[ghost],rect);
                        else g.FillRectangle(living?alive:trails[ghost],rect);
                    }
                }finally{if(texture!=null)texture.Dispose();for(int n=1;n<=8;n++){trails[n].Dispose();if(ghostTextures[n]!=null)ghostTextures[n].Dispose();}}
            }
            if(settings.Grid&&s>=7)using(var pen=new Pen(Color.FromArgb(35,palette.Muted))){for(int x=x0;x<=x1;x++)g.DrawLine(pen,o.X+x*s,Math.Max(0,o.Y),o.X+x*s,Math.Min(viewport.Height,o.Y+world.Height*s));for(int y=y0;y<=y1;y++)g.DrawLine(pen,Math.Max(0,o.X),o.Y+y*s,Math.Min(viewport.Width,o.X+world.Width*s),o.Y+y*s);}
        }
        public void Dispose(){if(source!=null)source.Dispose();InvalidateImage();}
    }
}
