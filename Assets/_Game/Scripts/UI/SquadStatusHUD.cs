using System;
using System.Text;
using System.Collections;
using UnityEngine;
using TMPro;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;

namespace FantasyGuildmaster.UI
{
    /// <summary>
    /// Minimal deterministic HUD: renders all squad statuses into a single TMP text block.
    /// No ScrollRect, no row prefabs, no hierarchy tricks.
    /// </summary>
    public class SquadStatusHUD : MonoBehaviour
    {
        [Header("Optional UI")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("Required UI (auto-created if missing)")]
        [SerializeField] private TMP_Text bodyText;

        [Header("Behavior")]
        [SerializeField] private float refreshSeconds = 1f;

        private MapController _map;
        private GameState _gameState;
        private Coroutine _tick;

        private void Awake()
        {
            EnsureBodyText();
        }

        private void OnEnable()
        {
            // Delay one frame to let MapController seed roster.
            StartCoroutine(DelayedBindAndStart());
        }

        private void OnDisable()
        {
            if (_gameState != null)
            {
                _gameState.OnGoldChanged -= OnGoldChanged;
            }

            if (_tick != null) StopCoroutine(_tick);
            _tick = null;
        }

        private IEnumerator DelayedBindAndStart()
        {
            yield return null;
            BindIfNeeded();
            RefreshNow();

            if (_tick == null)
                _tick = StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            var w = new WaitForSeconds(Mathf.Max(0.2f, refreshSeconds));
            while (enabled && gameObject.activeInHierarchy)
            {
                RefreshNow();
                yield return w;
            }
        }

        /// <summary>Compatibility entry point if something calls it.</summary>
        public void BindGameState(MapController mc)
        {
            _map = mc;
            RefreshNow();
        }

        // Compatibility with current MapController usage.
        public void BindGameState(GameState gs)
        {
            if (_gameState != null)
            {
                _gameState.OnGoldChanged -= OnGoldChanged;
            }

            _gameState = gs;

            if (_gameState != null)
            {
                _gameState.OnGoldChanged += OnGoldChanged;
                OnGoldChanged(_gameState.Gold);
            }
        }

        // Compatibility entry point used by MapController.
        public void Sync(System.Collections.Generic.IReadOnlyList<SquadData> squads, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            Render(squads, tasks, resolveRegionName, nowUnix);
        }

        private void BindIfNeeded()
        {
            if (_map != null) return;
            _map = UnityEngine.Object.FindFirstObjectByType<MapController>();
        }

        public void RefreshNow()
        {
            EnsureBodyText();
            BindIfNeeded();

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Squads";

            if (_map == null)
            {
                bodyText.text = "Squads: (MapController not found)";
                return;
            }

            var squads = _map.GetSquads();
            var tasks = _map.GetTravelTasks();
            Render(squads, tasks, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        private void Render(System.Collections.Generic.IReadOnlyList<SquadData> squads, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            if (squads == null || squads.Count == 0)
            {
                bodyText.text = "No squads.";
                return;
            }

            var sb = new StringBuilder(256);
            for (int i = 0; i < squads.Count; i++)
            {
                var s = squads[i];
                if (s == null) continue;

                string name = string.IsNullOrWhiteSpace(s.name) ? s.id : s.name;
                string status = BuildStatusLine(s.id, tasks, resolveRegionName, nowUnix);

                // HP is optional — show if available.
                string hp = "";
                if (s.maxHp > 0)
                    hp = $" | HP {Mathf.Clamp(s.hp, 0, s.maxHp)}/{s.maxHp}";

                sb.Append(name).Append(" | ").Append(status).Append(hp);
                if (i < squads.Count - 1) sb.Append('\n');
            }

            bodyText.text = sb.ToString();
        }

        private string BuildStatusLine(string squadId, System.Collections.Generic.IReadOnlyList<TravelTask> tasks, Func<string, string> resolveRegionName, long nowUnix)
        {
            TravelTask task = null;
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i++)
                {
                    if (tasks[i] != null && tasks[i].squadId == squadId)
                    {
                        task = tasks[i];
                        break;
                    }
                }
            }

            if (task == null)
                return "Idle";

            int secondsLeft = Mathf.Max(0, (int)(task.endUnix - nowUnix));
            string eta = FormatMMSS(secondsLeft);

            if (task.phase == TravelPhase.Outbound)
            {
                var toRegionId = string.IsNullOrWhiteSpace(task.toRegionId) ? "unknown" : task.toRegionId;
                var resolved = resolveRegionName != null ? resolveRegionName(toRegionId) : toRegionId;
                return $"Traveling → {resolved} (ETA {eta})";
            }

            if (task.phase == TravelPhase.Return)
                return $"Returning (ETA {eta})";

            return $"Busy (ETA {eta})";
        }

        private static string FormatMMSS(int totalSeconds)
        {
            int m = totalSeconds / 60;
            int s = totalSeconds % 60;
            return $"{m:00}:{s:00}";
        }

        private void EnsureBodyText()
        {
            if (bodyText != null) return;

            // Try find existing child
            var t = transform.Find("BodyText");
            if (t != null) bodyText = t.GetComponent<TMP_Text>();

            if (bodyText == null)
            {
                var go = new GameObject("BodyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(12f, 12f);
                rt.offsetMax = new Vector2(-12f, -12f);

                bodyText = go.GetComponent<TextMeshProUGUI>();
            }

            // Style
            if (goldText != null && goldText.font != null)
                bodyText.font = goldText.font;
            else if (TMP_Settings.defaultFontAsset != null)
                bodyText.font = TMP_Settings.defaultFontAsset;

            bodyText.color = Color.white;
            bodyText.fontSize = 18;
            bodyText.raycastTarget = false;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private void OnGoldChanged(int value)
        {
            if (goldText != null)
            {
                goldText.text = $"Gold: {value}";
            }
        }
    }
}
