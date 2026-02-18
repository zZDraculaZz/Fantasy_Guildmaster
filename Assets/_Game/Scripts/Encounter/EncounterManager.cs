using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using FantasyGuildmaster.UI;
using UnityEngine;

namespace FantasyGuildmaster.Encounter
{
    public sealed class EncounterManager : MonoBehaviour
    {
        [SerializeField] private EncounterPanel encounterPanel;

        private readonly List<EncounterData> _encounters = new();

        private Func<string, SquadData> _resolveSquad;
        private Action<int> _addGold;
        private Action<string> _onSquadDestroyed;

        private void Awake()
        {
            SeedEncounters();
        }

        public void Configure(Func<string, SquadData> resolveSquad, Action<int> addGold, Action<string> onSquadDestroyed)
        {
            _resolveSquad = resolveSquad;
            _addGold = addGold;
            _onSquadDestroyed = onSquadDestroyed;
        }

        public void StartEncounter(string regionId, string squadId, Action onEncounterClosed)
        {
            if (encounterPanel == null || _encounters.Count == 0)
            {
                onEncounterClosed?.Invoke();
                return;
            }

            var index = Mathf.Abs((regionId + squadId).GetHashCode()) % _encounters.Count;
            var encounter = _encounters[index];
            encounterPanel.ShowEncounter(encounter, option => ResolveOption(squadId, option, onEncounterClosed));
        }

        private void ResolveOption(string squadId, EncounterOption option, Action onEncounterClosed)
        {
            var success = UnityEngine.Random.value <= option.successChance;
            var squad = _resolveSquad?.Invoke(squadId);
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

            encounterPanel.ShowResult(result, onEncounterClosed);
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
