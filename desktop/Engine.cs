using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace CellularGarden {
    public sealed class Rule {
        public readonly string Name, Code, Description;
        public readonly int Birth, Survival;
        public Rule(string name, string code, string description, int birth, int survival) { Name=name; Code=code; Description=description; Birth=birth; Survival=survival; }
        public static readonly Rule[] All = {
            new Rule("康威生命游戏", "B3 / S23", "3 个邻居使生命诞生；2 或 3 个邻居让生命延续。", 1<<3, (1<<2)|(1<<3)),
            new Rule("HighLife · 高等生命", "B36 / S23", "3 或 6 个邻居使生命诞生；2 或 3 个邻居让生命延续。", (1<<3)|(1<<6), (1<<2)|(1<<3)),
            new Rule("Seeds · 种子", "B2 / S", "2 个邻居使生命诞生；所有活细胞在下一代消逝。", 1<<2, 0)
        };
    }
    public sealed class Universe {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Left { get; private set; }
        public int Top { get; private set; }
        public Rectangle Bounds { get { return new Rectangle(Left,Top,Width,Height); } }
        public byte[] Cells, Ghosts;
        private byte[] next, initial;
        private readonly Random random;
        public long Generation { get; private set; }
        public int Population { get; private set; }
        public int RuleIndex;
        public bool Wrap=true;
        public readonly Queue<int> History = new Queue<int>();
        public readonly Queue<ulong> Recent = new Queue<ulong>();
        public int Repeating { get; private set; }
        public Universe(int width, int height, int seed=0) {
            Width=width; Height=height; Cells=new byte[width*height]; Ghosts=new byte[Cells.Length]; next=new byte[Cells.Length]; initial=new byte[Cells.Length]; random=seed==0?new Random():new Random(seed);
        }
        public void Step() {
            Rule rule=Rule.All[RuleIndex]; int pop=0; ulong hash=14695981039346656037UL;
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) {
                int n=0, i=y*Width+x;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                    if(dx==0&&dy==0)continue;
                    int nx=x+dx,ny=y+dy;
                    if(Wrap){nx=(nx+Width)%Width;ny=(ny+Height)%Height;}
                    else if(nx<0||ny<0||nx>=Width||ny>=Height)continue;
                    n+=Cells[ny*Width+nx];
                }
                byte alive=(byte)((((Cells[i]!=0?rule.Survival:rule.Birth)>>n)&1)); next[i]=alive; pop+=alive;
                Ghosts[i]=(byte)(Cells[i]!=0&&alive==0?8:Math.Max(0,(int)Ghosts[i]-1));
                unchecked { hash=(hash^alive)*1099511628211UL; }
            }
            var old=Cells;Cells=next;next=old;Generation++;Population=pop;
            Repeating=Recent.Contains(hash)?Repeating+1:0;Recent.Enqueue(hash);if(Recent.Count>32)Recent.Dequeue();
            Record();
        }
        private void Record(){History.Enqueue(Population);if(History.Count>100)History.Dequeue();}
        public void Count(){Population=Cells.Sum(x=>(int)x);}
        public void Checkpoint(){initial=(byte[])Cells.Clone();Generation=0;Array.Clear(Ghosts,0,Ghosts.Length);History.Clear();Recent.Clear();Repeating=0;Count();Record();}
        public void Reset(){Cells=(byte[])initial.Clone();Checkpoint();}
        public void Clear(){Array.Clear(Cells,0,Cells.Length);Checkpoint();}
        public void Randomize(){for(int i=0;i<Cells.Length;i++)Cells[i]=(byte)(random.NextDouble()<0.22?1:0);Checkpoint();}
        public void Randomize(Rectangle bounds){
            SetBounds(bounds);
            Randomize();
        }
        public void SetBounds(Rectangle bounds){
            if(bounds.Width<1||bounds.Height<1||bounds.Width>8192||bounds.Height>8192||(long)bounds.Width*bounds.Height>16777216||
               bounds.X < -1000000||bounds.X>1000000||bounds.Y < -1000000||bounds.Y>1000000)
                throw new ArgumentException("Invalid simulation bounds");
            Width=bounds.Width;Height=bounds.Height;Left=bounds.X;Top=bounds.Y;
            Cells=new byte[Width*Height];Ghosts=new byte[Cells.Length];next=new byte[Cells.Length];
            Checkpoint();
        }
        public void Paint(int x,int y,bool erase){if(x<0||y<0||x>=Width||y>=Height)return;Cells[y*Width+x]=(byte)(erase?0:1);Ghosts[y*Width+x]=0;}
        public void EnsureBounds(Rectangle visible){
            if(Bounds.Contains(visible))return;
            Rectangle expanded=Rectangle.Union(Bounds,visible),old=Bounds;
            byte[] cells=Cells,ghosts=Ghosts,start=initial;long generation=Generation;
            SetBounds(expanded);
            for(int y=0;y<old.Height;y++){
                int target=(old.Top-expanded.Top+y)*Width+old.Left-expanded.Left;
                Array.Copy(cells,y*old.Width,Cells,target,old.Width);
                Array.Copy(ghosts,y*old.Width,Ghosts,target,old.Width);
                Array.Copy(start,y*old.Width,initial,target,old.Width);
            }
            Generation=generation;Count();History.Clear();Record();
        }
        public void PaintLine(int x0,int y0,int x1,int y1,bool erase){int steps=Math.Max(1,Math.Max(Math.Abs(x1-x0),Math.Abs(y1-y0)));for(int s=0;s<=steps;s++)Paint((int)Math.Round(x0+(x1-x0)*(double)s/steps),(int)Math.Round(y0+(y1-y0)*(double)s/steps),erase);Count();}
        public static readonly string[][] Patterns={
            new[]{".O.","..O","OOO"},
            new[]{"..OOO...OOO..",".............","O....O.O....O","O....O.O....O","O....O.O....O","..OOO...OOO..",".............","..OOO...OOO..","O....O.O....O","O....O.O....O","O....O.O....O",".............","..OOO...OOO.."},
            new[]{"........................O...........","......................O.O...........","............OO......OO............OO","...........O...O....OO............OO","OO........O.....O...OO..............","OO........O...O.OO....O.O...........","..........O.....O.......O...........","...........O...O....................","............OO......................"},
            new[]{".OO","OO.",".O."}
        };
        public void Pattern(int index){Array.Clear(Cells,0,Cells.Length);var rows=Patterns[index];int ox=(Width-rows.Max(x=>x.Length))/2,oy=(Height-rows.Length)/2;for(int y=0;y<rows.Length;y++)for(int x=0;x<rows[y].Length;x++)if(rows[y][x]=='O')Paint(x+ox,y+oy,false);Checkpoint();}
        public byte[] GetInitial(){return (byte[])initial.Clone();}
        public void Restore(byte[] data, byte[] start, long generation){if(data==null||data.Length!=Cells.Length||data.Any(x=>x>1))throw new ArgumentException("Invalid grid");Cells=(byte[])data.Clone();Checkpoint();if(start!=null&&start.Length==Cells.Length&&!start.Any(x=>x>1))initial=(byte[])start.Clone();Generation=Math.Max(0,generation);}
    }
}
