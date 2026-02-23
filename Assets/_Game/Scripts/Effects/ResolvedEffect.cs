using System;

namespace FantasyGuildmaster.Effects
{
    [Serializable]
    public sealed class ResolvedEffect
    {
        public string type;
        public int delta;
        public string id;
        public int tier;
    }
}
