using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using FantasyGuildmaster.UI;
using UnityEngine;

namespace FantasyGuildmaster.Encounter
{
    public sealed class EncounterManager : MonoBehaviour
    {
        private struct EncounterRequest
        {
            public string regionId;
            public string squadId;
            public Action onEncounterClosed;
        }

        [SerializeField] private EncounterPanel encounterPanel;

        private readonly List<EncounterData> _encounters = new();
        private readonly Queue<EncounterRequest> _queue = new();
        private readonly HashSet<string> _cancelledSquadIds = new();

        private Func<string, SquadData> _resolveSquad;
        private Action<int> _addGold;
        private Action<string> _onSquadDestroyed;
        private bool _isPresentingEncounter;

        public bool IsEncounterActive => _isPresentingEncounter;

        private void Awake()
        {
            SeedEncounters();
        }

        public void SetEncounterPanel(EncounterPanel panel)
        {
            encounterPanel = panel;
        }

        public void Configure(Func<string, SquadData> resolveSquad, Action<int> addGold, Action<string> onSquadDestroyed)
        {
            _resolveSquad = resolveSquad;
            _addGold = addGold;
            _onSquadDestroyed = onSquadDestroyed;
        }

        public void EnqueueEncounter(string regionId, string squadId, Action onEncounterClosed)
        {
            StartEncounter(regionId, squadId, onEncounterClosed);
        }

        public void StartEncounter(string regionId, string squadId, Action onEncounterClosed)
        {
            if (string.IsNullOrEmpty(squadId))
            {
                onEncounterClosed?.Invoke();
                return;
            }

            var request = new EncounterRequest
            {
                regionId = regionId,
                squadId = squadId,
                onEncounterClosed = onEncounterClosed
            };

            _cancelledSquadIds.Remove(squadId);
            _queue.Enqueue(request);
            TryPresentNextEncounter();
        }

        public void CancelPendingForSquad(string squadId)
        {
            if (string.IsNullOrEmpty(squadId))
            {
                return;
            }

            _cancelledSquadIds.Add(squadId);

            if (_queue.Count == 0)
            {
                return;
            }

            var tmp = new Queue<EncounterRequest>(_queue.Count);
            while (_queue.Count > 0)
            {
                var req = _queue.Dequeue();
                if (req.squadId != squadId)
                {
                    tmp.Enqueue(req);
                }
            }

            while (tmp.Count > 0)
            {
                _queue.Enqueue(tmp.Dequeue());
            }
        }

        private void TryPresentNextEncounter()
        {
            if (_isPresentingEncounter)
            {
                return;
            }

            while (_queue.Count > 0)
            {
                var request = _queue.Dequeue();

                if (_cancelledSquadIds.Contains(request.squadId))
                {
                    request.onEncounterClosed?.Invoke();
                    continue;
                }

                if (encounterPanel == null || _encounters.Count == 0)
                {
                    request.onEncounterClosed?.Invoke();
                    continue;
                }

                var squad = _resolveSquad?.Invoke(request.squadId);
                if (squad == null || squad.hp <= 0)
                {
                    request.onEncounterClosed?.Invoke();
                    continue;
                }

                var index = Mathf.Abs((request.regionId + request.squadId).GetHashCode()) % _encounters.Count;
                var encounter = _encounters[index];
                _isPresentingEncounter = true;
                Debug.Log($"[TravelDebug] Encounter UI show: squad={request.squadId}, region={request.regionId}, encounter={encounter.id}");
                encounterPanel.ShowEncounter(encounter, option => ResolveOption(request, option));
                return;
            }
        }

        private void ResolveOption(EncounterRequest request, EncounterOption option)
        {
            if (_cancelledSquadIds.Contains(request.squadId))
            {
                encounterPanel.ShowResult("Encounter cancelled.", () =>
                {
                    _isPresentingEncounter = false;
                    Debug.Log($"[TravelDebug] Encounter continue: squad={request.squadId} (cancelled)");
                    request.onEncounterClosed?.Invoke();
                    TryPresentNextEncounter();
                });
                return;
            }

            var success = UnityEngine.Random.value <= option.successChance;
            var squad = _resolveSquad?.Invoke(request.squadId);
            var result = success ? option.successText : option.failText;

            if (success && option.goldReward > 0)
            {
                _addGold?.Invoke(option.goldReward);
            }

            if (option.hpLoss > 0 && squad != null)
            {
                squad.hp = Mathf.Max(0, squad.hp - option.hpLoss);
                if (squad.hp <= 0)
                {
                    _onSquadDestroyed?.Invoke(squad.id);
                    result += "\nSquad destroyed";
                }
            }

            encounterPanel.ShowResult(result, () =>
            {
                _isPresentingEncounter = false;
                _cancelledSquadIds.Remove(request.squadId);
                Debug.Log($"[TravelDebug] Encounter continue: squad={request.squadId}");
                request.onEncounterClosed?.Invoke();
                TryPresentNextEncounter();
            });
        }

        private void SeedEncounters()
        {
            _encounters.Clear();

            _encounters.Add(new EncounterData
            {
                id = "undead_ambush",
                title = "Undead Ambush",
                description = "A pack of revenants rushes from ruined crypts.",
                options = new List<EncounterOption>
                {
                    new EncounterOption { text = "Hold the line", successChance = 0.65f, successText = "The undead are scattered.", failText = "The line breaks under pressure.", goldReward = 40, hpLoss = 12 },
                    new EncounterOption { text = "Retreat and regroup", successChance = 0.8f, successText = "You withdraw with minor losses.", failText = "The retreat turns chaotic.", goldReward = 10, hpLoss = 8 }
                }
            });

            _encounters.Add(new EncounterData
            {
                id = "cult_ritual",
                title = "Cult Ritual",
                description = "Cultists channel power in a blood-lit circle.",
                options = new List<EncounterOption>
                {
                    new EncounterOption { text = "Disrupt the altar", successChance = 0.55f, successText = "The ritual collapses and relics are seized.", failText = "Dark backlash wounds the squad.", goldReward = 60, hpLoss = 15 },
                    new EncounterOption { text = "Shadow the acolytes", successChance = 0.7f, successText = "You gather intel and coin from hidden caches.", failText = "You are discovered by fanatics.", goldReward = 35, hpLoss = 10 }
                }
            });

            _encounters.Add(new EncounterData
            {
                id = "smuggler_negotiation",
                title = "Smuggler Negotiation",
                description = "A smuggler crew offers a risky bargain.",
                options = new List<EncounterOption>
                {
                    new EncounterOption { text = "Take the deal", successChance = 0.6f, successText = "The exchange succeeds with profit.", failText = "It was a trap.", goldReward = 75, hpLoss = 10 },
                    new EncounterOption { text = "Force confiscation", successChance = 0.5f, successText = "Goods seized by force.", failText = "A firefight erupts on the docks.", goldReward = 55, hpLoss = 16 }
                }
            });

            _encounters.Add(new EncounterData
            {
                id = "grave_road",
                title = "Grave Road",
                description = "The road is cursed and fog swallows the trail.",
                options = new List<EncounterOption>
                {
                    new EncounterOption { text = "Push through", successChance = 0.62f, successText = "The squad reaches safety with salvage.", failText = "The curse takes its toll.", goldReward = 30, hpLoss = 9 },
                    new EncounterOption { text = "Camp until dawn", successChance = 0.77f, successText = "Rested and ready by sunrise.", failText = "Night horrors attack the camp.", goldReward = 15, hpLoss = 7 }
                }
            });
        }
    }
}
