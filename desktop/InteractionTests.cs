using System;
using System.Drawing;
using System.Threading;

namespace CellularGarden {
    internal static class InteractionTests {
        internal static void Run(Action<bool,string> check){
            var shield=new InputShield{Viewport=new Rectangle(0,0,1000,800),Panel=new Rectangle(100,100,200,300)};
            check(!shield.Allows(new Point(150,150))&&shield.Allows(new Point(50,150)),"panel blocks canvas input while uncovered canvas remains drawable");
            check(!shield.Allows(new Point(-1,40))&&!shield.Allows(new Point(1000,40)),"captured gestures outside the canvas are rejected");
            check(!shield.AllowsStroke(new Point(50,200),new Point(350,200)),"fast captured brush strokes cannot jump through the panel");
            check(!shield.AllowsStroke(new Point(200,50),new Point(200,500)),"vertical strokes cannot bridge the panel");
            check(shield.AllowsStroke(new Point(80,110),new Point(110,80)),"diagonal strokes outside the panel corner remain allowed");
            check(shield.AllowsStroke(new Point(500,120),new Point(500,120)),"single-point drawing outside the panel is allowed");
            shield.Panel=new Rectangle(500,100,200,300);
            check(shield.Allows(new Point(150,150))&&!shield.Allows(new Point(550,150)),"moving the panel immediately swaps protected and drawable regions");
            shield.Panel=Rectangle.Empty;
            check(shield.AllowsStroke(new Point(50,200),new Point(750,200)),"hiding the panel restores the whole canvas");
            check(InputShield.ClampPanel(new Point(900,700),new Size(348,500),new Size(1000,800))==new Point(652,300),"panel drag is clamped inside the visible window");
            check(InputShield.ClampPanel(new Point(-20,-20),new Size(348,500),new Size(1000,800))==Point.Empty,"dragging beyond the top left keeps the header reachable");
            var world=new Universe(160,90);world.PaintLine(0,0,159,0,false);world.PaintLine(0,0,159,0,false);
            check(world.Population==160,"repeated brush passes maintain population without rescanning the world");
            world.PaintLine(0,0,159,0,true);check(world.Population==0,"erase updates population incrementally");
            using(var started=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
                int writes=0,last=0,failures=0;
                var writer=new SettingsWriter(delegate(Settings value){if(Interlocked.Increment(ref writes)==1){started.Set();release.Wait(3000);}last=value.Speed;},delegate{Interlocked.Increment(ref failures);});
                try{
                    writer.Enqueue(new Settings{Speed=1});check(started.Wait(2000),"settings writer runs off the caller thread");
                    writer.Enqueue(new Settings{Speed=2});writer.Enqueue(new Settings{Speed=3});
                    check(writes==1,"editing remains nonblocking while a previous disk write is pending");
                }finally{release.Set();}
                check(writer.Flush(3000)&&last==3&&writes==2&&failures==0,"queued saves coalesce and flush the latest snapshot in order");
            }
            int errors=0,attempts=0;
            var retry=new SettingsWriter(delegate{if(Interlocked.Increment(ref attempts)==1)throw new InvalidOperationException("test write failure");},delegate{Interlocked.Increment(ref errors);});
            retry.Enqueue(new Settings());check(retry.Flush(3000)&&errors==1,"background save errors are reported without blocking the queue");
            retry.Enqueue(new Settings());check(retry.Flush(3000)&&attempts==2,"saving recovers after an earlier write failure");
            var original=new Settings{StepSeconds=2};Settings snapshot=original.Copy();original.StepSeconds=8;
            check(snapshot.StepSeconds==2,"queued preferences are isolated from later UI edits");
        }
    }
}
