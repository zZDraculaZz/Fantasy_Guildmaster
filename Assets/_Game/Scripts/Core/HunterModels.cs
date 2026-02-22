using System;

namespace FantasyGuildmaster.Core
{
    public enum HunterRank
    {
        E = 0,
        D = 1,
        C = 2,
        B = 3,
        A = 4,
        S = 5
    }

    [Serializable]
    public sealed class HunterData
    {
        public string id;
        public string name;
        public HunterRank rank;
        public bool loneWolf;
        public int hp = 100;
        public int maxHp = 100;
        public int joinedDay;
        public string squadId;
        public bool exhaustedToday;
    }
}
