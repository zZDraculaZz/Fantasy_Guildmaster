using System;

namespace FantasyGuildmaster.Map
{
    public enum SquadState
    {
        IdleAtHQ = 0,
        TravelingToRegion = 1,
        ResolvingEncounter = 2,
        ReturningToHQ = 3,
        Destroyed = 4
    }

    public enum TravelPhase
    {
        Outbound = 0,
        Return = 1
    }

    [Serializable]
    public sealed class SquadData
    {
        public string id;
        public string name;
        public int membersCount;
        public int hp;
        public int maxHp;
        public SquadState state;
        public string currentRegionId;

        public bool IsDestroyed => hp <= 0 || state == SquadState.Destroyed;
    }

    [Serializable]
    public sealed class TravelTask
    {
        public string squadId;
        public string fromRegionId;
        public string toRegionId;
        public string contractId;
        public int contractReward;
        public TravelPhase phase;
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
