using System;
using System.Collections.Generic;
using FantasyGuildmaster.Map;
using FantasyGuildmaster.UI;
using UnityEngine;
using UnityEngine.UI;

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
        private Action _onSquadChanged;
        private Func<SquadData, int, int> _getCohesionModifier;
        private Func<int> _getDayIndex;
        private bool _isPresentingEncounter;
        private Canvas _fallbackCanvas;

        public bool IsEncounterActive => _isPresentingEncounter;
        public int PendingEncounterCount => _queue.Count;

        private void Awake()
        {
            SeedEncounters();
        }

        public void SetEncounterPanel(EncounterPanel panel)
        {
            encounterPanel = panel;
        }

        public void Configure(Func<string, SquadData> resolveSquad, Action<int> addGold, Action<string> onSquadDestroyed, Action onSquadChanged = null, Func<SquadData, int, int> getCohesionModifier = null, Func<int> getDayIndex = null)
        {
            _resolveSquad = resolveSquad;
            _addGold = addGold;
            _onSquadDestroyed = onSquadDestroyed;
            _onSquadChanged = onSquadChanged;
            _getCohesionModifier = getCohesionModifier;
            _getDayIndex = getDayIndex;
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

                if (!EnsureEncounterPanelReadyForDisplay())
                {
                    request.onEncounterClosed?.Invoke();
                    continue;
                }

                if (_encounters.Count == 0)
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
                encounterPanel.gameObject.SetActive(true);
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

            var squad = _resolveSquad?.Invoke(request.squadId);
            var dayIndex = _getDayIndex != null ? _getDayIndex() : 0;
            var modifier = _getCohesionModifier != null ? _getCohesionModifier(squad, dayIndex) : 0;
            var newbieCount = 0;
            if (squad != null && squad.members != null)
            {
                for (var i = 0; i < squad.members.Count; i++)
                {
                    var member = squad.members[i];
                    if (member != null && member.joinedDay == dayIndex)
                    {
                        newbieCount++;
                    }
                }
            }

            var baseChance = Mathf.RoundToInt(option.successChance * 100f);
            var finalChance = Mathf.Clamp(baseChance + modifier, 5, 95);
            var roll = UnityEngine.Random.Range(1, 101);
            var success = roll <= finalChance;
            Debug.Log($"[Cohesion] squad={request.squadId} cohesion={(squad != null ? squad.cohesion : 0)} newbie={newbieCount} mod={modifier} base={baseChance} final={finalChance} roll={roll} [TODO REMOVE]");
            var result = success ? option.successText : option.failText;

            if (success && option.goldReward > 0)
            {
                _addGold?.Invoke(option.goldReward);
            }

            if (!success && squad != null)
            {
                squad.cohesion = Mathf.Clamp(squad.cohesion - 4, 0, 100);
                Debug.Log($"[Cohesion] After mission squad={squad.id} cohesion={squad.cohesion} delta=-4 [TODO REMOVE]");

                if (option.hpLoss > 0)
                {
                    squad.hp = Mathf.Max(0, squad.hp - option.hpLoss);
                    if (squad.hp <= 0)
                    {
                        squad.state = SquadState.Destroyed;
                        _onSquadDestroyed?.Invoke(squad.id);
                        result += "\nSquad destroyed";
                    }
                }

                _onSquadChanged?.Invoke();
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

        private bool EnsureEncounterPanelReadyForDisplay()
        {
            if (encounterPanel == null)
            {
                encounterPanel = FindFirstObjectByType<EncounterPanel>();
            }

            if (encounterPanel == null)
            {
                var prefab = Resources.Load<GameObject>("Prefabs/EncounterPanel");
                if (prefab != null)
                {
                    var canvas = EnsureOverlayCanvas();
                    var instance = Instantiate(prefab, canvas.transform, false);
                    encounterPanel = instance.GetComponent<EncounterPanel>();
                }
            }

            if (encounterPanel == null)
            {
                return false;
            }

            var canvasParent = encounterPanel.GetComponentInParent<Canvas>();
            if (canvasParent == null)
            {
                canvasParent = EnsureOverlayCanvas();
            }

            if (canvasParent.GetComponent<GraphicRaycaster>() == null)
            {
                canvasParent.gameObject.AddComponent<GraphicRaycaster>();
            }

            canvasParent.renderMode = RenderMode.ScreenSpaceOverlay;
            encounterPanel.transform.SetParent(canvasParent.transform, false);

            var canvasGroup = encounterPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = encounterPanel.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            encounterPanel.gameObject.SetActive(true);
            encounterPanel.transform.SetAsLastSibling();
            return true;
        }

        private Canvas EnsureOverlayCanvas()
        {
            if (_fallbackCanvas != null)
            {
                if (_fallbackCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    _fallbackCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return _fallbackCanvas;
            }

            _fallbackCanvas = FindFirstObjectByType<Canvas>();
            if (_fallbackCanvas != null)
            {
                if (_fallbackCanvas.GetComponent<GraphicRaycaster>() == null)
                {
                    _fallbackCanvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return _fallbackCanvas;
            }

            var canvasGo = new GameObject("EncounterOverlayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _fallbackCanvas = canvasGo.GetComponent<Canvas>();
            _fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fallbackCanvas.sortingOrder = 999;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return _fallbackCanvas;
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
