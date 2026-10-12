using System;

namespace CellularGarden {
    internal static class EvolutionSpeed {
        internal static decimal Minimum(bool seconds){return seconds?1m/30:1m/60;}
        internal static decimal Maximum(bool seconds){return seconds?60:30;}
        internal static decimal DisplayValue(double interval,bool seconds){decimal value=(decimal)(seconds?interval:1/interval);return Math.Max(Minimum(seconds),Math.Min(Maximum(seconds),value));}
        internal static double Interval(decimal value,bool seconds){return seconds?(double)value:1/(double)value;}
    }
    internal sealed class RenewalClock {
        private double elapsed,stable;
        internal void Reset(){elapsed=stable=0;}
        internal bool Advance(double seconds,bool eligible,bool repeating,Settings settings){
            if(!eligible)return false;
            elapsed=(settings.RenewTimed??settings.Renew)?elapsed+seconds:0;
            stable=(settings.RenewRepeating??settings.Renew)&&repeating?stable+seconds:0;
            bool due=((settings.RenewRepeating??settings.Renew)&&stable>=settings.RepeatDelay)||
                ((settings.RenewTimed??settings.Renew)&&elapsed>=settings.TimedDelay*60);
            if(due)Reset();return due;
        }
    }
}
