using System;
using FantasyGuildmaster.Core;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed partial class MapController
    {
        private void StartSoloTravelTask(HunterData hunter, string toRegionId, string contractId, int contractReward, TravelPhase phase)
        {
            if (hunter == null)
            {
                return;
            }

            var duration = ResolveTravelDuration(toRegionId);
            var now = SimulationTime.NowSeconds;
            var task = new TravelTask
            {
                squadId = null,
                soloHunterId = hunter.id,
                fromRegionId = GuildHqId,
                toRegionId = toRegionId,
                contractId = contractId,
                contractReward = contractReward,
                phase = phase,
                startSimSeconds = now,
                endSimSeconds = now + duration
            };

            _travelTasks.Add(task);
            AcquireOrUpdateSoloTravelToken(hunter, task);
            Debug.Log($"[TravelDebug] Solo task created hunter={hunter.id} route={GuildHqId}->{toRegionId} [TODO REMOVE]");
        }
    }
}
