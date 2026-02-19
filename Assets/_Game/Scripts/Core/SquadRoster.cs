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

            squads.Add(new SquadData { id = "squad_iron_hawks", name = "Iron Hawks", membersCount = 6, hp = 100, maxHp = 100, state = SquadState.IdleAtHQ, currentRegionId = "guild_hq" });
            squads.Add(new SquadData { id = "squad_ash_blades", name = "Ash Blades", membersCount = 5, hp = 100, maxHp = 100, state = SquadState.IdleAtHQ, currentRegionId = "guild_hq" });
            squads.Add(new SquadData { id = "squad_grim_lantern", name = "Grim Lantern", membersCount = 4, hp = 100, maxHp = 100, state = SquadState.IdleAtHQ, currentRegionId = "guild_hq" });

            Debug.Log($"[RosterDebug] Seeded squads: count={squads.Count}");
            NotifyChanged();
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
