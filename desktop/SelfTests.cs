using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

namespace CellularGarden {
    internal static class SelfTests {
        private static List<string> results=new List<string>();
        private static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);results.Add("PASS: "+name);File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"PASS: "+name+Environment.NewLine);}
        private static Universe Grid(params int[] points){var u=new Universe(9,9,42);u.Wrap=false;for(int i=0;i<points.Length;i+=2)u.Paint(points[i],points[i+1],false);u.Checkpoint();return u;}
        internal static void Run(){
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"");
            var block=Grid(3,3,3,4,4,3,4,4);var before=(byte[])block.Cells.Clone();for(int i=0;i<10;i++)block.Step();Check(block.Cells.SequenceEqual(before),"still life remains stable");
            var blinker=Grid(3,4,4,4,5,4);before=(byte[])blinker.Cells.Clone();blinker.Step();Check(blinker.Cells.SequenceEqual(Grid(4,3,4,4,4,5).Cells),"oscillator first step");blinker.Step();Check(blinker.Cells.SequenceEqual(before),"oscillator period 2");
            var glider=Grid(2,1,3,2,1,3,2,3,3,3);for(int i=0;i<4;i++)glider.Step();Check(glider.Cells.SequenceEqual(Grid(3,2,4,3,2,4,3,4,4,4).Cells),"glider translation after 4 steps");
            var edge=Grid(8,4,0,4,1,4);edge.Wrap=true;edge.Step();Check(edge.Cells[36]==1,"wrapped boundary");edge.Reset();edge.Wrap=false;edge.Step();Check(edge.Cells[36]==0,"fixed boundary");
            var high=Grid(3,3,4,3,5,3,3,4,5,4,4,5);high.RuleIndex=1;high.Step();Check(high.Cells[40]==1,"HighLife birth with six neighbours");high.Reset();high.RuleIndex=0;high.Step();Check(high.Cells[40]==0,"Conway does not birth with six neighbours");
            block.Reset();block.RuleIndex=2;block.Step();Check(block.Cells[30]==0,"Seeds cells never survive");
            var u=new Universe(160,90,42);u.Pattern(1);before=(byte[])u.Cells.Clone();for(int i=0;i<3;i++)u.Step();Check(u.Cells.SequenceEqual(before),"pulsar period 3");
            u.Pattern(2);Check(u.Population==36,"Gosper gun starts with 36 cells");for(int i=0;i<30;i++)u.Step();Check(u.Population>36,"Gosper gun emits cells");u.Reset();Check(u.Population==36&&u.Generation==0,"reset preserves initial seed");
            u.Clear();u.PaintLine(2,2,12,2,false);u.Count();Check(u.Population==11,"continuous paint stroke");u.PaintLine(2,2,12,2,true);u.Count();Check(u.Population==0,"continuous erase stroke");
            for(int i=0;i<150;i++)u.Step();Check(u.History.Count==100&&u.Recent.Count==32&&u.Repeating>30,"history bounded and repetitions detected");
            var full=new Viewport(new Size(2560,1600),new Size(2560,1600),Point.Empty,1,0,0);
            var moved=new Viewport(new Size(2560,1600),new Size(2560,1528),Point.Empty,.5f,4000,-4000);
            Check(moved.VisibleCells.Right<0&&moved.VisibleCells.Top>90,"panned viewport can lie entirely outside the old fixed grid");
            var seeded=new Universe(160,90,42);seeded.Randomize(moved.VisibleCells);
            Check(seeded.Bounds==moved.VisibleCells&&seeded.Width>160&&seeded.Height>90,"zoomed-out seeding covers the current viewport, not the old bounds");
            float leftPixel=moved.Origin.X+seeded.Left*moved.CellSize,topPixel=moved.Origin.Y+seeded.Top*moved.CellSize;
            Check(leftPixel<=0&&leftPixel>-moved.CellSize&&topPixel<=0&&topPixel>-moved.CellSize&&
                leftPixel+seeded.Width*moved.CellSize>=2560&&topPixel+seeded.Height*moved.CellSize>=1528,"seeded cell footprint covers all four viewport edges");
            int[] edgeCounts=new int[4];
            for(int y=0;y<seeded.Height;y++)for(int x=0;x<seeded.Width;x++)if(seeded.Cells[y*seeded.Width+x]!=0){if(x<4)edgeCounts[0]++;if(x>=seeded.Width-4)edgeCounts[1]++;if(y<4)edgeCounts[2]++;if(y>=seeded.Height-4)edgeCounts[3]++;}
            Check(edgeCounts.All(n=>n>0),"random cells reach all four visible edge bands");
            Point painted=moved.Cell(new Point(1280,764));seeded.Paint(painted.X-seeded.Left,painted.Y-seeded.Top,false);
            Check(seeded.Cells[(painted.Y-seeded.Top)*seeded.Width+painted.X-seeded.Left]==1,"painting uses the relocated grid coordinates");
            seeded.Checkpoint();before=(byte[])seeded.Cells.Clone();seeded.Step();seeded.Reset();Check(seeded.Bounds==moved.VisibleCells&&seeded.Cells.SequenceEqual(before),"reset retains relocated seed bounds and contents");
            var small=new Viewport(new Size(2560,1600),new Size(2560,1528),Point.Empty,3,-1275,390);
            seeded.Randomize(small.VisibleCells);Check(seeded.Width<160&&seeded.Height<90&&seeded.Bounds==small.VisibleCells,"zoomed-in seeding excludes offscreen regions");
            var controlView=new Viewport(new Size(2560,1600),new Size(2490,1600),new Point(70,0),1.3f,137,-93);
            var wallpaperView=new Viewport(new Size(2560,1600),new Size(2560,1600),Point.Empty,1.3f,137,-93);
            Check(controlView.Cell(new Point(400,300))==wallpaperView.Cell(new Point(470,300)),"controller and wallpaper agree on screen coordinates with taskbar offset");
            var portrait=new Viewport(new Size(1200,1920),new Size(1200,1860),Point.Empty,.5f,-4000,4000);
            seeded.Randomize(portrait.VisibleCells);Check(seeded.Bounds==portrait.VisibleCells,"portrait viewport seeds after opposite-direction panning");
            Check(new Settings().WorldBounds==new Rectangle(0,0,160,90),"legacy settings keep their original grid bounds");
            var expanded=new Universe(160,90);expanded.Paint(80,45,false);expanded.Checkpoint();expanded.Ghosts[0]=7;
            expanded.EnsureBounds(moved.VisibleCells);
            Check(expanded.Bounds.Contains(moved.VisibleCells)&&expanded.Cells[(45-expanded.Top)*expanded.Width+80-expanded.Left]==1&&expanded.Ghosts[-expanded.Top*expanded.Width-expanded.Left]==7,"expanding paint bounds preserves existing cells and trails");
            Point[] corners={new Point(0,0),new Point(2559,0),new Point(0,1527),new Point(2559,1527)};
            foreach(Point corner in corners){Point c=moved.Cell(corner);expanded.Paint(c.X-expanded.Left,c.Y-expanded.Top,false);Check(expanded.Cells[(c.Y-expanded.Top)*expanded.Width+c.X-expanded.Left]==1,"brush reaches panned zoomed-out viewport corner "+corner);}
            expanded.Reset();Check(expanded.Population==1&&expanded.Cells[(45-expanded.Top)*expanded.Width+80-expanded.Left]==1,"expanding bounds preserves reset checkpoint coordinates");
            expanded.EnsureBounds(portrait.VisibleCells);Check(expanded.Bounds.Contains(portrait.VisibleCells)&&expanded.Population==1,"opposite-side viewport expansion retains earlier drawing");
            var slow=new Settings{StepSeconds=60,RenewRepeating=true,RenewTimed=false,RepeatSeconds=30};var renewal=new RenewalClock();
            Check(slow.StepInterval==60&&EvolutionSpeed.DisplayValue(slow.StepInterval,true)==60,"speed supports one generation per minute");
            Check(EvolutionSpeed.Interval(2.5m,true)==2.5&&EvolutionSpeed.Interval(2.5m,false)==.4,"manual fractional speeds support seconds per generation and generations per second");
            Check(EvolutionSpeed.DisplayValue(.4,false)==2.5m&&EvolutionSpeed.DisplayValue(60,true)==60&&EvolutionSpeed.DisplayValue(1.0/30,false)==30,"unit conversion preserves fractional speeds and supported endpoints");
            using(var input=new SpeedInput{Minimum=1m/30,Maximum=60,DecimalPlaces=6,Value=1}){
                int edits=0;input.ValueChanged+=delegate{edits++;};input.Text="2.5";input.Commit();
                Check(input.Value==2.5m&&edits==1&&input.Text=="2.5","manual speed text commits once and keeps decimals without trailing zeros");
                input.Text="0";input.Commit();Check(input.Value==input.Minimum,"zero speed input is constrained to valid evolution range");
                input.Text="invalid";input.Commit();Check(input.Value==input.Minimum,"invalid speed text preserves last valid value");
            }
            Check(!renewal.Advance(29,true,true,slow)&&renewal.Advance(1,true,true,slow),"repetition renewal uses elapsed seconds independently of evolution speed");
            Check(!renewal.Advance(29,true,true,slow)&&!renewal.Advance(500,false,true,slow)&&renewal.Advance(1,true,true,slow),"paused and controller time do not advance renewal");
            renewal.Reset();Check(!renewal.Advance(29,true,true,slow)&&!renewal.Advance(1,true,false,slow)&&!renewal.Advance(1,true,true,slow),"changing patterns reset continuous repetition duration");
            slow.RenewRepeating=false;slow.RenewTimed=true;slow.RenewMinutes=2;renewal.Reset();
            Check(!renewal.Advance(119,true,true,slow)&&renewal.Advance(1,true,false,slow),"timed renewal can operate with repetition renewal disabled");
            slow.RenewTimed=false;renewal.Reset();Check(!renewal.Advance(10000,true,true,slow),"both renewal conditions can be disabled");
            slow.RenewTimed=true;Check(!renewal.Advance(119,true,true,slow)&&renewal.Advance(1,true,true,slow),"enabling timed renewal does not count time spent disabled");
            Check(new Settings{Speed=3}.StepInterval==1.0/3&&!(new Settings{Renew=false}.RenewTimed??false),"old speed and renewal settings retain backward compatibility");
            string oldPath=Settings.FilePath;try{Settings.FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-settings.json");var s=new Settings{Theme=2,Speed=14,Running=false,Cells=(byte[])u.Cells.Clone(),Initial=u.GetInitial(),Generation=u.Generation};s.Save();s.Speed=17;s.Save();var loaded=Settings.Load();Check(loaded.Speed==17&&loaded.Theme==2&&!loaded.Running&&loaded.Cells.SequenceEqual(u.Cells),"atomic preferences round trip");
                s.Cells=(byte[])seeded.Cells.Clone();s.Initial=seeded.GetInitial();s.WorldWidth=seeded.Width;s.WorldHeight=seeded.Height;s.WorldLeft=seeded.Left;s.WorldTop=seeded.Top;s.Save();loaded=Settings.Load();var restored=new Universe(160,90);restored.SetBounds(loaded.WorldBounds);restored.Restore(loaded.Cells,loaded.Initial,loaded.Generation);Check(restored.Bounds==seeded.Bounds&&restored.Cells.SequenceEqual(seeded.Cells),"relocated grid dimensions and origin survive restart");
                s.AppearanceMode=1;s.SolidCellLight="#102030";s.SolidCellDark="#8090A0";s.SolidBackgroundLight="#FFFEEE";s.SolidBackgroundDark="#111222";s.ImageBackgroundLight="#ABCDEF";s.ImageBackgroundDark="#012345";s.ImageName="example.png";s.Save();loaded=Settings.Load();Check(loaded.AppearanceMode==1&&loaded.SolidCellLight=="#102030"&&loaded.SolidCellDark=="#8090A0"&&loaded.SolidBackgroundLight=="#FFFEEE"&&loaded.SolidBackgroundDark=="#111222"&&loaded.ImageBackgroundLight=="#ABCDEF"&&loaded.ImageBackgroundDark=="#012345"&&loaded.ImageName=="example.png","appearance mode and per-theme colours survive settings round trip");
                s.StepSeconds=30;s.RenewRepeating=true;s.RenewTimed=false;s.RepeatSeconds=90;s.RenewMinutes=12;s.Save();loaded=Settings.Load();Check(loaded.StepInterval==30&&loaded.RenewRepeating==true&&loaded.RenewTimed==false&&loaded.RepeatDelay==90&&loaded.TimedDelay==12,"slow speed and independent renewal settings survive restart");
                File.WriteAllText(Settings.FilePath,"invalid");Check(Settings.Load().Speed==8,"corrupt preferences recover defaults");}finally{if(File.Exists(Settings.FilePath))File.Delete(Settings.FilePath);Settings.FilePath=oldPath;}
            AppearanceTests.Run(Check);
            InteractionTests.Run(Check);
            u.Randomize();var timer=Stopwatch.StartNew();for(int i=0;i<300;i++)u.Step();timer.Stop();results.Add("BENCH: 160 x 90, 300 generations in "+timer.ElapsedMilliseconds+" ms");
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),results);
        }
    }
}
