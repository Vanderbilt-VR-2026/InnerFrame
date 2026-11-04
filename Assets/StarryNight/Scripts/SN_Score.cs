using System;

namespace InnerFrame.StarryNight
{
    /// <summary>Attention, never speed: what the player noticed. No time, no failure.</summary>
    [Serializable]
    public class SN_Score
    {
        public int starsWoken, starsTotal = 12;
        public int namedInOrder, namedTotal = 3;
        public bool windTraced, letterFound;

        public override string ToString() =>
            $"stars={starsWoken}/{starsTotal} named={namedInOrder}/{namedTotal} wind={(windTraced ? "yes" : "no")} letter={(letterFound ? "yes" : "no")}";
    }
}
