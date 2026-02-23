using System;

namespace FantasyGuildmaster.Effects
{
    [Serializable]
    public sealed class EffectDef
    {
        public string type;
        public int delta;
        public string id;
        public int chance = -1;
        public int tier;
    }
}
