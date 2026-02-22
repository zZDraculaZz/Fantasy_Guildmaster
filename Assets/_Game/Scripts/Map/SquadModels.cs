using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;

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
    public sealed class SquadMemberData
    {
        public string id;
        public string name;
        public int hp = 100;
        public int maxHp = 100;
        public string status = "Ready";
        public int joinedDay;
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
        public List<SquadMemberData> members = new();
        public List<string> hunterIds = new();
        public bool exhausted;
        public string exhaustedReason;
        public int cohesion = 35;
        public int lastRosterChangeDay;
        public int contractsDoneToday;

        public bool IsDestroyed => hp <= 0 || state == SquadState.Destroyed;
    }

    [Serializable]
    public sealed class TravelTask
    {
        public string squadId;
        public string soloHunterId;
        public string fromRegionId;
        public string toRegionId;
        public string contractId;
        public int contractReward;
        public TravelPhase phase;
        public long startSimSeconds;
        public long endSimSeconds;

        public float GetProgress(long nowSimSeconds)
        {
            var duration = endSimSeconds - startSimSeconds;
            if (duration <= 0)
            {
                return 1f;
            }

            return UnityEngine.Mathf.Clamp01((nowSimSeconds - startSimSeconds) / (float)duration);
        }
    }
}
