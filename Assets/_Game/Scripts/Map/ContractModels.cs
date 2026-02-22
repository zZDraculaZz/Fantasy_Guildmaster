using System;
using FantasyGuildmaster.Core;

namespace FantasyGuildmaster.Map
{
    [Serializable]
    public sealed class ContractData
    {
        public string id;
        public string title;
        public int remainingSeconds;
        public int reward;
        public string iconKey;
        public HunterRank minRank = HunterRank.E;
        public bool allowSquad = true;
        public bool allowSolo = true;

        public bool IsExpired => remainingSeconds <= 0;

        public string RemainingText
        {
            get
            {
                var clamped = Math.Max(remainingSeconds, 0);
                var minutes = clamped / 60;
                var seconds = clamped % 60;
                return $"{minutes:00}:{seconds:00}";
            }
        }
    }
}
