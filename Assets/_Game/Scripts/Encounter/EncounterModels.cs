using System;
using System.Collections.Generic;

namespace FantasyGuildmaster.Encounter
{
    [Serializable]
    public sealed class EncounterOption
    {
        public string text;
        public float successChance;
        public string successText;
        public string failText;
        public int goldReward;
        public int hpLoss;
    }

    [Serializable]
    public sealed class EncounterData
    {
        public string id;
        public string title;
        public string description;
        public List<EncounterOption> options = new();
    }
}
