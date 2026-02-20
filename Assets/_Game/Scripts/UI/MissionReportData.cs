using System;

namespace FantasyGuildmaster.UI
{
    [Serializable]
    public sealed class MissionReportData
    {
        public string squadId;
        public string squadName;
        public string regionId;
        public string regionName;
        public string contractId;
        public string contractTitle;
        public int rewardGold;
        public int readinessBeforePercent;
        public int readinessAfterPercent;
        public string membersSummary;
        public string outcomeText;
    }
}
