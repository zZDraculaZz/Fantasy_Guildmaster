using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private GameState gameState;

        private MapController _mapController;
        private SquadRoster _squadRoster;
        private Func<string, string> _resolveRegionName;
        private Coroutine _refreshCoroutine;

        private void Awake()
        {
            EnsureBodyText();
            ApplyTextStyles();
            HardBind();
            UpdateGoldText(gameState != null ? gameState.Gold : 0);
        }

        private void Start()
        {
            StartCoroutine(RefreshNextFrame());
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshNow();
        }

        private void OnEnable()
        {
            HardBind();

            if (gameState != null)
            {
                gameState.OnGoldChanged += UpdateGoldText;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged += OnRosterChanged;
            }

            if (_refreshCoroutine == null)
            {
                _refreshCoroutine = StartCoroutine(RefreshEachSecond());
            }

            StartCoroutine(RefreshNextFrame());
        }

        private void OnDisable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged -= OnRosterChanged;
            }

            if (_refreshCoroutine != null)
            {
                StopCoroutine(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        public void BindGameState(GameState state)
        {
            if (gameState == state)
            {
                return;
            }

            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }

            gameState = state;
            if (gameState != null && isActiveAndEnabled)
            {
                gameState.OnGoldChanged += UpdateGoldText;
                UpdateGoldText(gameState.Gold);
            }
        }

        public void Sync(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            _resolveRegionName = resolveRegionName;
            Render(squads, tasks, nowUnix);
        }

        public void RefreshNow()
        {
            HardBind();

            if (_mapController == null)
            {
                return;
            }

            var squads = _squadRoster != null ? _squadRoster.GetSquads() : _mapController.GetSquads();
            var tasks = _mapController.GetTravelTasks();
            Render(squads, tasks, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        private IEnumerator RefreshEachSecond()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(1f);
                RefreshNow();
            }
        }

        private void HardBind()
        {
            if (_mapController == null)
            {
                _mapController = FindFirstObjectByType<MapController>();
            }

            var foundRoster = FindFirstObjectByType<SquadRoster>();
            if (_squadRoster != foundRoster)
            {
                if (_squadRoster != null)
                {
                    _squadRoster.OnRosterChanged -= OnRosterChanged;
                }

                _squadRoster = foundRoster;

                if (_squadRoster != null && isActiveAndEnabled)
                {
                    _squadRoster.OnRosterChanged += OnRosterChanged;
                }
            }

            if (gameState == null)
            {
                gameState = FindFirstObjectByType<GameState>();
            }
        }

        private void OnRosterChanged()
        {
            RefreshNow();
        }

        private void UpdateGoldText(int gold)
        {
            _mapController = FindFirstObjectByType<MapController>();
            _squadRoster = FindFirstObjectByType<SquadRoster>();
            _gameClock = FindFirstObjectByType<GameClock>();

            if (gameState == null)
            {
                gameState = FindFirstObjectByType<GameState>();
            }
        }

        private void EnsureBodyText()
        {
            if (bodyText != null)
            {
                return;
            }

            var existing = transform.Find("BodyText");
            if (existing != null)
            {
                bodyText = existing.GetComponent<TMP_Text>() ?? existing.gameObject.AddComponent<TextMeshProUGUI>();
                return;
            }

            var go = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            bodyText = go.GetComponent<TextMeshProUGUI>();
        }

        private void ApplyTextStyles()
        {
            if (bodyText == null)
            {
                return;
            }

            if (goldText != null)
            {
                bodyText.font = goldText.font;
                bodyText.fontSharedMaterial = goldText.fontSharedMaterial;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                bodyText.font = TMP_Settings.defaultFontAsset;
                bodyText.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
            }

            bodyText.color = Color.white;
            bodyText.enableAutoSizing = false;
            bodyText.fontSize = 18f;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.raycastTarget = false;
            bodyText.textWrappingMode = TextWrappingModes.Normal;

            var rect = bodyText.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -56f);
        }

        private void Render(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, long nowUnix)
        {
            EnsureBodyText();
            ApplyTextStyles();

            if (bodyText == null)
            {
                return;
            }

            if (squads == null || squads.Count == 0)
            {
                bodyText.text = "No squads";
                return;
            }

            var lines = new List<string>(squads.Count);
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name;
                var status = BuildStatus(squad, tasks, nowUnix);
                var hp = squad.maxHp > 0 ? $"HP {Mathf.Max(0, squad.hp)}/{Mathf.Max(1, squad.maxHp)}" : string.Empty;
                lines.Add(string.IsNullOrEmpty(hp) ? $"{name} | {status}" : $"{name} | {status} | {hp}");
            }

            bodyText.text = lines.Count > 0 ? string.Join("\n", lines) : "No squads";
        }

        private string BuildStatus(SquadData squad, IReadOnlyList<TravelTask> tasks, long nowUnix)
        {
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    if (task == null || task.squadId != squad.id)
                    {
                        continue;
                    }

                    var left = Mathf.Max(0, (int)(task.endUnix - nowUnix));
                    var mm = left / 60;
                    var ss = left % 60;

                    if (task.phase == TravelPhase.Outbound)
                    {
                        var region = ResolveRegion(task.toRegionId);
                        return $"Traveling → {region} (ETA {mm:00}:{ss:00})";
                    }

                    return $"Returning (ETA {mm:00}:{ss:00})";
                }
            }

            return "Idle";
        }

        private string ResolveRegion(string regionId)
        {
            if (string.IsNullOrWhiteSpace(regionId))
            {
                return "Unknown";
            }

            if (_resolveRegionName == null)
            {
                return regionId;
            }

            var resolved = _resolveRegionName(regionId);
            return string.IsNullOrWhiteSpace(resolved) ? regionId : resolved;
        }
    }
}
