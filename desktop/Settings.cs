using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CellularGarden {
    [DataContract] public sealed class Settings {
        [DataMember] public int Version=1;
        [DataMember] public int Speed=8;
        [DataMember] public double? StepSeconds;
        [DataMember] public bool? RenewRepeating;
        [DataMember] public bool? RenewTimed;
        [DataMember] public int? RepeatSeconds;
        [DataMember] public int? RenewMinutes;
        public double StepInterval {get {return StepSeconds.HasValue&&!double.IsNaN(StepSeconds.Value)?Math.Max(1.0/30,Math.Min(60,StepSeconds.Value)):1.0/Math.Max(1,Speed);}}
        public int RepeatDelay {get{return Math.Max(1,Math.Min(3600,RepeatSeconds??30));}}
        public int TimedDelay {get{return Math.Max(1,Math.Min(1440,RenewMinutes??5));}}
        [DataMember] public int Theme=0;
        [DataMember] public int RuleIndex=0;
        [DataMember] public bool Wrap=true;
        [DataMember] public bool Trails=true;
        [DataMember] public bool Renew=true;
        [DataMember] public bool Grid=false;
        [DataMember] public bool Running=true;
        [DataMember] public bool PauseFullscreen=true;
        [DataMember] public string Monitor="";
        [DataMember] public float Zoom=1;
        [DataMember] public float PanX=0;
        [DataMember] public float PanY=0;
        [DataMember] public byte[] Cells;
        [DataMember] public byte[] Initial;
        [DataMember] public long Generation;
        [DataMember] public int WorldWidth;
        [DataMember] public int WorldHeight;
        [DataMember] public int WorldLeft;
        [DataMember] public int WorldTop;
        [DataMember] public int AppearanceMode;
        [DataMember] public string SolidCellDark;
        [DataMember] public string SolidCellLight;
        [DataMember] public string SolidBackgroundDark;
        [DataMember] public string SolidBackgroundLight;
        [DataMember] public string ImageBackgroundDark;
        [DataMember] public string ImageBackgroundLight;
        [DataMember] public string ImageName;
        public System.Drawing.Rectangle WorldBounds {
            get {return WorldWidth==0&&WorldHeight==0?new System.Drawing.Rectangle(0,0,160,90):
                new System.Drawing.Rectangle(WorldLeft,WorldTop,WorldWidth,WorldHeight);}
        }
        public static string FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");
        public static Settings Load(){
            try {using(var stream=File.OpenRead(FilePath)){var s=(Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);s.Speed=Math.Max(1,Math.Min(30,s.Speed));s.Theme=Math.Max(0,Math.Min(2,s.Theme));s.RuleIndex=Math.Max(0,Math.Min(2,s.RuleIndex));if(float.IsNaN(s.Zoom)||float.IsInfinity(s.Zoom))s.Zoom=1;s.Zoom=Math.Max(.5f,Math.Min(3,s.Zoom));if(float.IsNaN(s.PanX)||float.IsInfinity(s.PanX))s.PanX=0;if(float.IsNaN(s.PanY)||float.IsInfinity(s.PanY))s.PanY=0;s.PanX=Math.Max(-4000,Math.Min(4000,s.PanX));s.PanY=Math.Max(-4000,Math.Min(4000,s.PanY));return s;}}
            catch{return new Settings();}
        }
        public void Save(){
            string temp=FilePath+".tmp";
            using(var stream=File.Create(temp))new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream,this);
            if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
        }
    }
}
