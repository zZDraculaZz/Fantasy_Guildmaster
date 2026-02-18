using System;

namespace FantasyGuildmaster.Map
{
    public enum SquadStatus
    {
        Idle = 0,
        Traveling = 1
    }

    [Serializable]
    public sealed class SquadData
    {
        public string id;
        public string name;
        public int membersCount;
        public int hp;
        public SquadStatus status;
        public string currentRegionId;
    }

    [Serializable]
    public sealed class TravelTask
    {
        public string squadId;
        public string fromRegionId;
        public string toRegionId;
        public string contractId;
        public long startUnix;
        public long endUnix;

        public float GetProgress(long nowUnix)
        {
            var duration = endUnix - startUnix;
            if (duration <= 0)
            {
                return 1f;
            }

            return UnityEngine.Mathf.Clamp01((nowUnix - startUnix) / (float)duration);
        }
    }
}
