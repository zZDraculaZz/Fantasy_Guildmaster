using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField] private LayoutElement viewportLayoutElement;
        [SerializeField] private SquadStatusRow rowPrefab; // kept for scene serialization compatibility
        [SerializeField] private GameState gameState;

        private readonly List<TMP_Text> _rows = new();

        private MapController _mapController;
        private SquadRoster _squadRoster;
        private GameClock _gameClock;

        private void Awake()
        {
            BuildSimpleHud();
            HardBind();
            UpdateGoldText(gameState != null ? gameState.Gold : 0);
            RefreshNow();
        }

        private void OnEnable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged += UpdateGoldText;
            }

            if (_gameClock != null)
            {
                _gameClock.TickSecond += OnTick;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged += OnRosterChanged;
            }

            RefreshNow();
        }

        private void OnDisable()
        {
            if (gameState != null)
            {
                gameState.OnGoldChanged -= UpdateGoldText;
            }

            if (_gameClock != null)
            {
                _gameClock.TickSecond -= OnTick;
            }

            if (_squadRoster != null)
            {
                _squadRoster.OnRosterChanged -= OnRosterChanged;
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
            BuildSimpleHud();
            RenderRows(squads, tasks, nowUnix);
        }

        public void RefreshNow()
        {
            if (_mapController == null)
            {
                _mapController = FindFirstObjectByType<MapController>();
                if (_mapController == null)
                {
                    return;
                }
            }

            var squads = _squadRoster != null ? _squadRoster.GetSquads() : _mapController.GetSquads();
            var tasks = _mapController.GetTravelTasks();
            RenderRows(squads, tasks, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        private void OnRosterChanged()
        {
            RefreshNow();
        }

        private void OnTick()
        {
            RefreshNow();
        }

        private void UpdateGoldText(int gold)
        {
            if (goldText != null)
            {
                goldText.text = $"Gold: {gold}";
            }
        }

        private void HardBind()
        {
            _mapController = FindFirstObjectByType<MapController>();
            _squadRoster = FindFirstObjectByType<SquadRoster>();
            _gameClock = FindFirstObjectByType<GameClock>();

            if (gameState == null)
            {
                gameState = FindFirstObjectByType<GameState>();
            }
        }

        private void BuildSimpleHud()
        {
            rootRect ??= transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(16f, -16f);
            rootRect.sizeDelta = new Vector2(300f, 260f);

            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.16f, 0.84f);

            var rootLayout = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(8, 8, 8, 8);
            rootLayout.spacing = 6f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            titleText = GetOrCreateHeaderText("Title", "Squads", 20f);
            goldText = GetOrCreateHeaderText("GoldText", "Gold: 100", 16f);

            var rowsGo = transform.Find("RowsSimple")?.gameObject;
            if (rowsGo == null)
            {
                rowsGo = new GameObject("RowsSimple", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
                rowsGo.transform.SetParent(transform, false);
            }

            rowsRoot = rowsGo.GetComponent<RectTransform>();
            var rowsLayout = rowsGo.GetComponent<VerticalLayoutGroup>();
            rowsLayout.padding = new RectOffset(4, 4, 4, 4);
            rowsLayout.spacing = 4f;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            var rowsFitter = rowsGo.GetComponent<ContentSizeFitter>();
            rowsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rowsElement = rowsGo.GetComponent<LayoutElement>();
            rowsElement.minHeight = 120f;
            rowsElement.preferredHeight = 120f;
            rowsElement.flexibleHeight = 1f;
        }

        private TMP_Text GetOrCreateHeaderText(string name, string value, float size)
        {
            var child = transform.Find(name);
            TMP_Text text;
            if (child == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                go.transform.SetParent(transform, false);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                text = child.GetComponent<TMP_Text>() ?? child.gameObject.AddComponent<TextMeshProUGUI>();
            }

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
                text.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
            }

            text.text = value;
            text.color = Color.white;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = size + 8f;
            layout.preferredHeight = size + 8f;
            layout.flexibleHeight = 0f;
            return text;
        }

        private void RenderRows(IReadOnlyList<SquadData> squads, IReadOnlyList<TravelTask> tasks, long nowUnix)
        {
            if (rowsRoot == null)
            {
                return;
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                {
                    Destroy(_rows[i].gameObject);
                }
            }

            _rows.Clear();

            if (squads == null)
            {
                return;
            }

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    continue;
                }

                var rowGo = new GameObject($"Row_{squad.id}", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                rowGo.transform.SetParent(rowsRoot, false);
                var text = rowGo.GetComponent<TextMeshProUGUI>();

                if (goldText != null)
                {
                    text.font = goldText.font;
                    text.fontSharedMaterial = goldText.fontSharedMaterial;
                }
                else if (TMP_Settings.defaultFontAsset != null)
                {
                    text.font = TMP_Settings.defaultFontAsset;
                    text.fontSharedMaterial = TMP_Settings.defaultFontAsset.material;
                }

                var status = BuildStatus(squad, tasks, nowUnix);
                text.text = status;
                text.fontSize = 14f;
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Left;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.raycastTarget = false;

                var layout = rowGo.GetComponent<LayoutElement>();
                layout.minHeight = 24f;
                layout.preferredHeight = 24f;
                layout.flexibleHeight = 0f;

                _rows.Add(text);
            }
        }

        private static string BuildStatus(SquadData squad, IReadOnlyList<TravelTask> tasks, long nowUnix)
        {
            var name = string.IsNullOrWhiteSpace(squad.name) ? squad.id : squad.name;

            TravelTask task = null;
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    if (tasks[i] != null && tasks[i].squadId == squad.id)
                    {
                        task = tasks[i];
                        break;
                    }
                }
            }

            var hp = $"{Mathf.Max(0, squad.hp)}/{Mathf.Max(1, squad.maxHp)}";
            if (task != null)
            {
                var left = Mathf.Max(0, (int)(task.endUnix - nowUnix));
                var mm = left / 60;
                var ss = left % 60;
                var phase = task.phase == TravelPhase.Outbound ? "OUT" : "RET";
                return $"{name}  |  {phase} {mm:00}:{ss:00}  |  {hp}";
            }

            return $"{name}  |  {squad.state}  |  {hp}";
        }
    }
}
