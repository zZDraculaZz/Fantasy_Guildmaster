using FantasyGuildmaster.Map;

namespace FantasyGuildmaster.Core
{
    public static class RankUtil
    {
        public static HunterRank GetMinRank(SquadData squad)
        {
            if (squad == null || squad.members == null || squad.members.Count == 0)
            {
                return HunterRank.E;
            }

            // fallback if hunter mapping not available
            return HunterRank.D;
        }

        public static string FormatRank(HunterRank rank)
        {
            return $"[{rank}]";
        }
    }
}
