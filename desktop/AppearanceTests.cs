using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CellularGarden {
    internal static class AppearanceTests {
        private static Bitmap Render(WallpaperAppearance appearance,Settings settings,Universe world,Viewport view,Size viewport,Point offset){
            var output=new Bitmap(viewport.Width,viewport.Height);
            using(var g=Graphics.FromImage(output))appearance.Render(g,viewport,new Size(640,360),offset,view,world,new Palette(false));
            return output;
        }
        internal static void Run(Action<bool,string> check){
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"appearance-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            string input=Path.Combine(dir,"input.png"),imported=Path.Combine(dir,"wallpaper.png");
            try{
                using(var fixture=new Bitmap(640,360)){
                    for(int y=0;y<fixture.Height;y++)for(int x=0;x<fixture.Width;x++)fixture.SetPixel(x,y,Color.FromArgb(x*255/639,y*255/359,80));
                    fixture.Save(input,ImageFormat.Png);
                    check(WallpaperAppearance.Luminance(WallpaperAppearance.RecommendColor(fixture,true))<.4,"image background recommendation prefers dark colours in dark mode");
                    check(WallpaperAppearance.Luminance(WallpaperAppearance.RecommendColor(fixture,false))>.65,"image background recommendation prefers light colours in light mode");
                }
                using(var white=new Bitmap(4,4)){using(var g=Graphics.FromImage(white))g.Clear(Color.White);check(WallpaperAppearance.Luminance(WallpaperAppearance.RecommendColor(white,true))<.3,"bright-only images still get a dark theme recommendation");}
                using(var black=new Bitmap(4,4)){check(WallpaperAppearance.Luminance(WallpaperAppearance.RecommendColor(black,false))>.8,"transparent images still get a light theme recommendation");}
                var settings=new Settings{SolidCellLight="#FF0000",SolidBackgroundLight="#0000FF",Trails=true};var world=new Universe(160,90);for(int i=0;i<world.Cells.Length;i++)world.Cells[i]=1;
                var view=new Viewport(new Size(640,360),new Size(640,360),Point.Empty,1,0,0);
                using(var appearance=new WallpaperAppearance(settings,imported)){
                    using(var solid=Render(appearance,settings,world,view,new Size(640,360),Point.Empty))check(solid.GetPixel(202,102).ToArgb()==Color.Red.ToArgb(),"solid mode renders chosen cell colour");
                    world.Clear();using(var empty=Render(appearance,settings,world,view,new Size(640,360),Point.Empty))check(empty.GetPixel(202,102).ToArgb()==Color.Blue.ToArgb(),"empty cells render chosen solid background");
                    appearance.Import(input);check(appearance.HasImage&&settings.AppearanceMode==1&&File.Exists(imported),"import stores an independent local wallpaper copy");
                    using(var edit=File.Open(input,FileMode.Open,FileAccess.ReadWrite,FileShare.None))check(edit.Length>0,"import releases the original image file lock");
                    appearance.SetBackground(false,Color.FromArgb(20,30,40));appearance.SetBackground(true,Color.FromArgb(5,6,7));
                    check(appearance.Background(new Palette(false)).R==20&&appearance.Background(new Palette(true)).R==5,"light and dark manual backgrounds remain independent");
                    appearance.ResetColors(true);check(appearance.AutomaticBackground(true)&&!appearance.AutomaticBackground(false),"reset recommendation preserves other theme manual colour");
                    for(int i=0;i<world.Cells.Length;i++)world.Cells[i]=1;
                    Color original;
                    using(var live=Render(appearance,settings,world,view,new Size(640,360),Point.Empty)){original=live.GetPixel(202,102);check(original.ToArgb()==appearance.ScreenImage(new Size(640,360),appearance.Background(new Palette(false))).GetPixel(202,102).ToArgb(),"living cells reveal matching screen-space image pixels");}
                    var moved=new Viewport(new Size(640,360),new Size(640,360),Point.Empty,2,24,8);
                    using(var shifted=Render(appearance,settings,world,moved,new Size(640,360),Point.Empty))check(shifted.GetPixel(202,102).ToArgb()==original.ToArgb(),"image stays fixed during camera pan and zoom");
                    var offsetView=new Viewport(new Size(640,360),new Size(600,360),new Point(40,0),1,0,0);
                    using(var offset=Render(appearance,settings,world,offsetView,new Size(600,360),new Point(40,0)))check(offset.GetPixel(162,102).ToArgb()==original.ToArgb(),"control and desktop sample the same image at the same screen position");
                    world.Clear();for(int i=0;i<world.Ghosts.Length;i++)world.Ghosts[i]=8;
                    Color bg=appearance.Background(new Palette(false));
                    Color frost=appearance.FrostedImage(new Size(640,360),bg).GetPixel(202,102),expected=WallpaperAppearance.Blend(frost,original,.45f);
                    using(var ghost=Render(appearance,settings,world,view,new Size(640,360),Point.Empty)){Color c=ghost.GetPixel(202,102);check(Math.Abs(c.R-expected.R)<3&&Math.Abs(c.G-expected.G)<3&&Math.Abs(c.B-expected.B)<3,"dead cells reveal image at reduced opacity over the base colour");}
                    settings.Trails=false;using(var empty=Render(appearance,settings,world,view,new Size(640,360),Point.Empty))check(empty.GetPixel(202,102).ToArgb()==frost.ToArgb()&&frost.ToArgb()!=bg.ToArgb(),"empty cells and disabled trails retain faint frosted image");
                    using(var shifted=Render(appearance,settings,world,moved,new Size(640,360),Point.Empty))check(shifted.GetPixel(202,102).ToArgb()==frost.ToArgb(),"frosted empty background stays fixed during pan and zoom");
                    using(var offset=Render(appearance,settings,world,offsetView,new Size(600,360),new Point(40,0)))check(offset.GetPixel(162,102).ToArgb()==frost.ToArgb(),"frosted background aligns between controller and desktop");
                    check(object.ReferenceEquals(appearance.FrostedImage(new Size(640,360),bg),appearance.FrostedImage(new Size(640,360),bg)),"blur is cached across simulation frames");
                    bool failed=false;try{appearance.Import(Path.Combine(dir,"missing.png"));}catch{failed=true;}check(failed&&appearance.HasImage&&settings.ImageBackgroundLight=="#141E28","failed import preserves existing picture and manual colour");
                }
                using(var restored=new WallpaperAppearance(settings,imported))check(restored.HasImage&&restored.Background(new Palette(false)).ToArgb()==Color.FromArgb(20,30,40).ToArgb(),"stored wallpaper reloads with manual base colour");
                using(var checker=new Bitmap(640,360)){
                    using(var g=Graphics.FromImage(checker)){g.Clear(Color.Black);g.FillRectangle(Brushes.White,320,0,320,360);}checker.Save(input,ImageFormat.Png);
                }
                using(var appearance=new WallpaperAppearance(settings,imported)){
                    appearance.Import(input);appearance.SetBackground(false,Color.Black);
                    var blurred=appearance.FrostedImage(new Size(640,360),Color.Black);
                    check(blurred.GetPixel(315,180).R>0&&blurred.GetPixel(325,180).R<30&&blurred.GetPixel(500,180).R<=32,"frosted image softens sharp edges and uses low opacity");
                    settings.AppearanceMode=0;
                    foreach(Color colour in new[]{Color.Black,Color.White,Color.Red,Color.Blue,Color.FromArgb(128,128,128)}){
                        appearance.SetBackground(false,colour);appearance.SetCellColor(false,colour);Palette controls=appearance.ControlPalette(new Palette(false));
                        check(controls.Background.ToArgb()==colour.ToArgb()&&WallpaperAppearance.Contrast(controls.Panel,controls.Text)>=4.5&&WallpaperAppearance.Contrast(controls.Surface,controls.Accent)>=4.5,"controller follows custom colour with readable text: "+WallpaperAppearance.Hex(colour));
                    }
                }
            }finally{foreach(string file in Directory.GetFiles(dir))File.Delete(file);Directory.Delete(dir);}
        }
    }
}
