using System;
using System.Threading;

namespace CellularGarden {
    // One serial writer, retaining only the latest pending snapshot during rapid edits.
    internal sealed class SettingsWriter {
        private readonly object gate=new object();
        private readonly Action<Settings> write;
        private readonly Action<Exception> failed;
        private Settings pending;
        private bool active;
        internal SettingsWriter(Action<Settings> persist,Action<Exception> error){write=persist;failed=error;}
        internal void Enqueue(Settings snapshot){lock(gate){pending=snapshot;if(active)return;active=true;ThreadPool.QueueUserWorkItem(delegate{Drain();});}}
        private void Drain(){
            while(true){
                Settings next;
                lock(gate){next=pending;pending=null;if(next==null){active=false;Monitor.PulseAll(gate);return;}}
                try{write(next);}catch(Exception e){failed(e);}
            }
        }
        internal bool Flush(int milliseconds){
            var timer=System.Diagnostics.Stopwatch.StartNew();
            lock(gate){while(active){int left=milliseconds-(int)timer.ElapsedMilliseconds;if(left<=0)return false;Monitor.Wait(gate,left);}return true;}
        }
    }
}
