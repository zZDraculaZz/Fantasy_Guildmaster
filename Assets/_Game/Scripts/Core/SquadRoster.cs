using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public sealed class SquadRoster : MonoBehaviour
    {
        [SerializeField] private List<SquadData> squads = new();

        public List<SquadData> Squads => squads;

        public event Action OnRosterChanged;

        public void SeedDefaultSquadsIfEmpty()
        {
            if (squads.Count > 0)
            {
                return;
            }

            squads.Add(CreateDefaultSquad("squad_iron_hawks", "Iron Hawks"));
            squads.Add(CreateDefaultSquad("squad_ash_blades", "Ash Blades"));
            squads.Add(CreateDefaultSquad("squad_grim_lantern", "Grim Lantern"));

            Debug.Log($"[RosterDebug] Seeded squads: count={squads.Count}");
            NotifyChanged();
        }


        private static SquadData CreateDefaultSquad(string squadId, string squadName)
        {
            var members = new List<SquadMemberData>
            {
                new SquadMemberData { id = $"{squadId}_m1", name = "Vanguard", hp = 100, maxHp = 100, status = "Ready" },
                new SquadMemberData { id = $"{squadId}_m2", name = "Scout", hp = 92, maxHp = 100, status = "Ready" },
                new SquadMemberData { id = $"{squadId}_m3", name = "Support", hp = 85, maxHp = 100, status = "Ready" }
            };

            return new SquadData
            {
                id = squadId,
                name = squadName,
                membersCount = members.Count,
                hp = 100,
                maxHp = 100,
                state = SquadState.IdleAtHQ,
                currentRegionId = "guild_hq",
                members = members
            };
        }

        public List<SquadData> GetSquads()
        {
            return squads;
        }

        public void NotifyChanged()
        {
            OnRosterChanged?.Invoke();
        }
    }
}
