using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using UnityEngine;

namespace FantasyGuildmaster.Core
{
    public sealed class SquadRoster : MonoBehaviour
    {
        [SerializeField] private List<SquadData> squads = new();
        [SerializeField] private HunterRoster hunterRoster;

        public List<SquadData> Squads => squads;

        public event Action OnRosterChanged;

        public void SeedDefaultSquadsIfEmpty()
        {
            if (hunterRoster == null)
            {
                hunterRoster = FindFirstObjectByType<HunterRoster>();
                if (hunterRoster == null)
                {
                    hunterRoster = gameObject.AddComponent<HunterRoster>();
                }
            }

            hunterRoster.EnsureSeededDefaultHunters(0);

            if (squads.Count > 0)
            {
                return;
            }

            var assigned = new List<HunterData>();
            for (var i = 0; i < hunterRoster.Hunters.Count; i++)
            {
                var h = hunterRoster.Hunters[i];
                if (h != null && !h.loneWolf && assigned.Count < 9)
                {
                    assigned.Add(h);
                }
            }

            squads.Add(CreateDefaultSquad("squad_iron_hawks", "Iron Hawks", assigned, 0));
            squads.Add(CreateDefaultSquad("squad_ash_blades", "Ash Blades", assigned, 3));
            squads.Add(CreateDefaultSquad("squad_grim_lantern", "Grim Lantern", assigned, 6));

            Debug.Log("[SquadRoster] Seeded squads with hunters. [TODO REMOVE]");
            NotifyChanged();
        }

        private static SquadData CreateDefaultSquad(string squadId, string squadName, List<HunterData> assigned, int startIndex)
        {
            var hunterIds = new List<string>();
            var members = new List<SquadMemberData>();
            for (var i = startIndex; i < startIndex + 3 && i < assigned.Count; i++)
            {
                var h = assigned[i];
                if (h == null)
                {
                    continue;
                }

                h.squadId = squadId;
                hunterIds.Add(h.id);
                members.Add(new SquadMemberData
                {
                    id = h.id,
                    name = h.name,
                    hp = h.hp,
                    maxHp = h.maxHp,
                    status = "Ready",
                    joinedDay = h.joinedDay
                });
            }

            return new SquadData
            {
                id = squadId,
                name = squadName,
                membersCount = hunterIds.Count,
                hp = 100,
                maxHp = 100,
                state = SquadState.IdleAtHQ,
                currentRegionId = "guild_hq",
                members = members,
                hunterIds = hunterIds,
                exhausted = false,
                cohesion = 45,
                contractsDoneToday = 0,
                lastRosterChangeDay = 0
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
