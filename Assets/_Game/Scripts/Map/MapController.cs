using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.Encounter;
using FantasyGuildmaster.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FantasyGuildmaster.Map
{
    public sealed class MapController : MonoBehaviour
    {
        private const string GuildHqId = "guild_hq";
        private const string GuildHqName = "Guild HQ";
        private const bool DEBUG_TRAVEL = true;

        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform markersRoot;
        [SerializeField] private RectTransform contractIconsRoot;
        [SerializeField] private RectTransform travelTokensRoot;
        [SerializeField] private RegionMarker regionMarkerPrefab;
        [SerializeField] private ContractIcon contractIconPrefab;
        [SerializeField] private TravelToken travelTokenPrefab;
        [SerializeField] private RegionDetailsPanel detailsPanel;
        [SerializeField] private SquadSelectPanel squadSelectPanel;
        [SerializeField] private EncounterManager encounterManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameClock gameClock;
        [SerializeField] private SquadStatusHUD squadStatusHud;

        private readonly Dictionary<string, List<ContractData>> _contractsByRegion = new();
        private readonly Dictionary<string, RegionData> _regionById = new();
        private readonly Dictionary<string, RegionMarker> _markersByRegion = new();
        private readonly Dictionary<string, List<ContractIcon>> _iconsByRegion = new();
        private readonly Dictionary<string, ContractIcon> _iconByContractId = new();
        private readonly List<SquadData> _squads = new();
        private readonly List<TravelTask> _travelTasks = new();
        private readonly Dictionary<string, TravelToken> _travelTokenBySquadId = new();

        private GameData _gameData;
        private ContractIconPool _iconPool;
        private TravelTokenPool _travelTokenPool;

        private void Awake()
        {
            _gameData = GameDataLoader.Load();
            EnsureGuildHqRegion();
            BuildRegionIndex();
            SeedContracts();
            SeedSquads();
            SpawnMarkers();
            InitializePools();
            SyncAllContractIcons();
            EnsureEncounterDependencies();

            if (encounterManager != null)
            {
                encounterManager.Configure(FindSquad, AddGold, HandleSquadDestroyed);
            }

            if (detailsPanel != null)
            {
                detailsPanel.AssignSquadRequested += OnAssignSquadRequested;
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            if (squadSelectPanel != null)
            {
                squadSelectPanel.Hide();
            }

            RefreshSquadStatusHud();

            var firstPlayableRegion = GetFirstPlayableRegion();
            if (firstPlayableRegion != null)
            {
                SelectRegion(firstPlayableRegion);
            }
        }

        private void OnDestroy()
        {
            if (detailsPanel != null)
            {
                detailsPanel.AssignSquadRequested -= OnAssignSquadRequested;
            }
        }

        private void OnEnable()
        {
            if (gameClock != null)
            {
                gameClock.TickSecond += OnTick;
            }
        }

        private void OnDisable()
        {
            if (gameClock != null)
            {
                gameClock.TickSecond -= OnTick;
            }
        }

        private void OnTick()
        {
            TickContracts();
            SyncAllContractIcons();
            TickTravelTasks();

            if (detailsPanel != null)
            {
                detailsPanel.TickContracts();
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            RefreshSquadStatusHud();
        }

        private void TickContracts()
        {
            foreach (var pair in _contractsByRegion)
            {
                if (pair.Key == GuildHqId)
                {
                    continue;
                }

                var contracts = pair.Value;
                for (var i = contracts.Count - 1; i >= 0; i--)
                {
                    contracts[i].remainingSeconds--;
                    if (contracts[i].IsExpired)
                    {
                        contracts.RemoveAt(i);
                    }
                }
            }
        }

        private void TickTravelTasks()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var completedTasks = new List<TravelTask>();

            for (var i = 0; i < _travelTasks.Count; i++)
            {
                var task = _travelTasks[i];
                if (!_markersByRegion.TryGetValue(task.fromRegionId, out var fromMarker)
                    || !_markersByRegion.TryGetValue(task.toRegionId, out var toMarker)
                    || fromMarker == null
                    || toMarker == null)
                {
                    continue;
                }

                if (_travelTokenBySquadId.TryGetValue(task.squadId, out var token) && token != null)
                {
                    var progress = task.GetProgress(now);
                    var pos = Vector2.Lerp(fromMarker.AnchoredPosition, toMarker.AnchoredPosition, progress);
                    var remaining = Mathf.Max(0, (int)(task.endUnix - now));
                    token.UpdateView(pos, ToTimerText(remaining));
                }

                if (now >= task.endUnix)
                {
                    completedTasks.Add(task);
                }
            }

            for (var i = 0; i < completedTasks.Count; i++)
            {
                var task = completedTasks[i];
                HandleTravelTaskCompleted(task, now);
            }

            for (var i = 0; i < completedTasks.Count; i++)
            {
                _travelTasks.Remove(completedTasks[i]);
            }
        }

        private void HandleTravelTaskCompleted(TravelTask task, long nowUnix)
        {
            var squad = FindSquad(task.squadId);
            if (squad == null)
            {
                RemoveTravelToken(task.squadId);
                return;
            }

            if (task.phase == TravelPhase.Outbound)
            {
                if (DEBUG_TRAVEL)
                {
                    Debug.Log($"[TravelDebug] Outbound complete: squad={squad.name}, now={nowUnix}, region={task.toRegionId}");
                }

                squad.currentRegionId = task.toRegionId;
                squad.state = SquadState.ResolvingEncounter;

                if (encounterManager != null)
                {
                    if (DEBUG_TRAVEL)
                    {
                        Debug.Log($"[TravelDebug] Encounter queued: squad={squad.name}, region={task.toRegionId}");
                    }

                    encounterManager.EnqueueEncounter(task.toRegionId, squad.id, () => OnEncounterResolved(squad.id, task.toRegionId, task.contractId, task.contractReward));
                }
                else
                {
                    Debug.LogWarning($"[TravelDebug] EncounterManager missing, fallback to immediate return: squad={squad.name}");
                    OnEncounterResolved(squad.id, task.toRegionId, task.contractId, task.contractReward);
                }

                return;
            }

            if (DEBUG_TRAVEL)
            {
                Debug.Log($"[TravelDebug] Return complete: squad={squad.name}, now={nowUnix}, reward={task.contractReward}");
            }

            squad.currentRegionId = GuildHqId;
            squad.state = SquadState.IdleAtHQ;
            AddGold(task.contractReward);
            RemoveTravelToken(task.squadId);
            CompleteContract(task.fromRegionId, task.contractId);
        }

        private void OnEncounterResolved(string squadId, string regionId, string contractId, int contractReward)
        {
            var squad = FindSquad(squadId);
            if (squad == null || squad.hp <= 0)
            {
                RemoveTravelToken(squadId);
                RestoreContractAvailability(regionId, contractId);
                return;
            }

            StartTravelTask(squad, regionId, GuildHqId, contractId, contractReward, TravelPhase.Return);
            squad.state = SquadState.ReturningToHQ;

            if (DEBUG_TRAVEL)
            {
                Debug.Log($"[TravelDebug] Return task created: squad={squad.name}, {regionId}->{GuildHqId}");
            }

            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }
        }

        private void EnsureEncounterDependencies()
        {
            if (encounterManager == null)
            {
                encounterManager = FindFirstObjectByType<EncounterManager>();
            }

            var encounterPanel = FindFirstObjectByType<EncounterPanel>();
            if (encounterPanel == null)
            {
                var allPanels = Resources.FindObjectsOfTypeAll<EncounterPanel>();
                if (allPanels != null && allPanels.Length > 0)
                {
                    encounterPanel = allPanels[0];
                }
            }

            if (encounterPanel == null)
            {
                encounterPanel = CreateRuntimeEncounterPanel();
            }

            if (encounterManager == null)
            {
                var managerGo = new GameObject("EncounterManager");
                encounterManager = managerGo.AddComponent<EncounterManager>();
            }

            if (encounterPanel != null)
            {
                encounterManager.SetEncounterPanel(encounterPanel);
            }
        }

        private EncounterPanel CreateRuntimeEncounterPanel()
        {
            var canvas = EnsureCanvas();
            if (canvas == null)
            {
                Debug.LogError("[TravelDebug] Failed to create runtime EncounterPanel: Canvas missing.");
                return null;
            }

            var prefab = Resources.Load<GameObject>("Prefabs/EncounterPanel");
            if (prefab != null)
            {
                var instance = Instantiate(prefab, canvas.transform, false);
                var panelFromPrefab = instance.GetComponent<EncounterPanel>();
                if (panelFromPrefab != null)
                {
                    instance.transform.SetAsLastSibling();
                    return panelFromPrefab;
                }
            }

            var panelGo = new GameObject("EncounterPanel", typeof(RectTransform), typeof(Image), typeof(EncounterPanel));
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            contentGo.transform.SetParent(panelGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(680f, 420f);
            var contentImage = contentGo.GetComponent<Image>();
            contentImage.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateText(contentGo.transform, "Title", 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            var description = CreateText(contentGo.transform, "Description", 26f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            var optionsRootGo = new GameObject("OptionsRoot", typeof(RectTransform), typeof(VerticalLayoutGroup));
            optionsRootGo.transform.SetParent(contentGo.transform, false);
            var optionsLayout = optionsRootGo.GetComponent<VerticalLayoutGroup>();
            optionsLayout.spacing = 8f;
            optionsLayout.childForceExpandHeight = false;
            optionsLayout.childForceExpandWidth = true;

            var optionButton = CreateButton(contentGo.transform, "OptionButtonTemplate", "Option");
            optionButton.gameObject.SetActive(false);

            var continueButton = CreateButton(contentGo.transform, "ContinueButton", "Continue");

            var panel = panelGo.GetComponent<EncounterPanel>();
            panel.ConfigureRuntimeBindings(title, description, optionsRootGo.GetComponent<RectTransform>(), optionButton, continueButton);
            panelGo.SetActive(false);
            panelGo.transform.SetAsLastSibling();
            return panel;
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
                }

                EnsureEventSystem();
                return canvas;
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            EnsureEventSystem();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            _ = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static TextMeshProUGUI CreateText(Transform parent, string objectName, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = objectName;
            return text;
        }

        private static Button CreateButton(Transform parent, string objectName, string labelText)
        {
            var buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonGo.transform.SetParent(parent, false);
            var image = buttonGo.GetComponent<Image>();
            image.color = new Color(0.24f, 0.24f, 0.24f, 1f);
            var layoutElement = buttonGo.GetComponent<LayoutElement>();
            layoutElement.minHeight = 56f;

            var label = CreateText(buttonGo.transform, "Label", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.text = labelText;
            return buttonGo.GetComponent<Button>();
        }

        private void InitializePools()
        {
            if (contractIconPrefab != null && contractIconsRoot != null)
            {
                var preload = Math.Max(_gameData.regions.Count * 3, 8);
                _iconPool = new ContractIconPool(contractIconPrefab, contractIconsRoot, preload);
            }

            if (travelTokenPrefab != null && travelTokensRoot != null)
            {
                _travelTokenPool = new TravelTokenPool(travelTokenPrefab, travelTokensRoot, 6);
            }
        }

        private void SpawnMarkers()
        {
            for (var i = markersRoot.childCount - 1; i >= 0; i--)
            {
                var child = markersRoot.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            _markersByRegion.Clear();

            foreach (var region in _gameData.regions)
            {
                var marker = Instantiate(regionMarkerPrefab, markersRoot);
                marker.name = $"RegionMarker_{region.id}";
                marker.Setup(region, mapRect, SelectRegion);
                _markersByRegion[region.id] = marker;
            }
        }

        private void SelectRegion(RegionData region)
        {
            if (!_contractsByRegion.TryGetValue(region.id, out var contracts))
            {
                contracts = new List<ContractData>();
            }

            if (detailsPanel != null)
            {
                detailsPanel.Show(region, contracts);
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }
        }

        private void OnAssignSquadRequested(RegionData region, ContractData contract)
        {
            if (squadSelectPanel == null || region == null || contract == null || region.id == GuildHqId)
            {
                return;
            }

            var idle = GetIdleSquads();
            if (idle.Count == 0)
            {
                return;
            }

            if (_travelTasks.Exists(t => t.contractId == contract.id && t.phase == TravelPhase.Outbound))
            {
                return;
            }

            squadSelectPanel.Show(idle, squad =>
            {
                StartTravelTask(squad, GuildHqId, region.id, contract.id, contract.reward, TravelPhase.Outbound);
                squad.state = SquadState.TravelingToRegion;

                detailsPanel?.BlockContract(contract.id);

                if (detailsPanel != null)
                {
                    detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
                    SelectRegion(region);
                }
            });
        }

        private void StartTravelTask(SquadData squad, string fromRegionId, string toRegionId, string contractId, int contractReward, TravelPhase phase)
        {
            if (squad == null)
            {
                return;
            }

            var duration = ResolveTravelDuration(toRegionId);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var task = new TravelTask
            {
                squadId = squad.id,
                fromRegionId = fromRegionId,
                toRegionId = toRegionId,
                contractId = contractId,
                contractReward = contractReward,
                phase = phase,
                startUnix = now,
                endUnix = now + duration
            };

            RemoveExistingTaskForSquad(squad.id);
            _travelTasks.Add(task);
            AcquireOrUpdateTravelToken(squad, task);

            if (DEBUG_TRAVEL)
            {
                var phaseLabel = phase == TravelPhase.Outbound ? "OUT" : "RET";
                Debug.Log($"[TravelDebug] Travel task created: phase={phaseLabel}, squad={squad.name}, route={fromRegionId}->{toRegionId}, endUnix={task.endUnix}");
            }
        }

        private int ResolveTravelDuration(string regionId)
        {
            if (_regionById.TryGetValue(regionId, out var region))
            {
                return Mathf.Max(1, region.travelDays * 10);
            }

            return 10;
        }

        private void RemoveExistingTaskForSquad(string squadId)
        {
            for (var i = _travelTasks.Count - 1; i >= 0; i--)
            {
                if (_travelTasks[i].squadId == squadId)
                {
                    _travelTasks.RemoveAt(i);
                }
            }
        }

        private void AcquireOrUpdateTravelToken(SquadData squad, TravelTask task)
        {
            if (_travelTokenPool == null)
            {
                return;
            }

            if (!_travelTokenBySquadId.TryGetValue(task.squadId, out var token) || token == null)
            {
                token = _travelTokenPool.Get();
                token.transform.SetParent(travelTokensRoot, false);
                _travelTokenBySquadId[task.squadId] = token;
            }

            token.Bind(squad.id, squad.name);

            if (_markersByRegion.TryGetValue(task.fromRegionId, out var fromMarker) && fromMarker != null)
            {
                var remaining = Mathf.Max(0, (int)(task.endUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
                token.UpdateView(fromMarker.AnchoredPosition, ToTimerText(remaining));
            }
        }

        private void RemoveTravelToken(string squadId)
        {
            if (_travelTokenBySquadId.TryGetValue(squadId, out var token))
            {
                _travelTokenPool?.Return(token);
                _travelTokenBySquadId.Remove(squadId);
            }
        }

        private void SyncAllContractIcons()
        {
            if (_iconPool == null)
            {
                return;
            }

            foreach (var pair in _contractsByRegion)
            {
                if (pair.Key == GuildHqId)
                {
                    continue;
                }

                if (!_markersByRegion.TryGetValue(pair.Key, out var marker) || marker == null)
                {
                    continue;
                }

                SyncRegionContractIcons(pair.Key, marker, pair.Value);
            }
        }

        private void SyncRegionContractIcons(string regionId, RegionMarker marker, List<ContractData> contracts)
        {
            if (!_iconsByRegion.TryGetValue(regionId, out var icons))
            {
                icons = new List<ContractIcon>();
                _iconsByRegion[regionId] = icons;
            }

            for (var i = icons.Count - 1; i >= 0; i--)
            {
                var icon = icons[i];
                if (icon == null || icon.Contract == null || icon.Contract.IsExpired)
                {
                    if (icon != null && icon.Contract != null)
                    {
                        _iconByContractId.Remove(icon.Contract.id);
                    }

                    _iconPool.Return(icon);
                    icons.RemoveAt(i);
                }
            }

            for (var i = 0; i < contracts.Count; i++)
            {
                var contract = contracts[i];
                if (_iconByContractId.ContainsKey(contract.id))
                {
                    continue;
                }

                var icon = _iconPool.Get();
                icon.transform.SetParent(contractIconsRoot, false);
                icon.Bind(contract);
                icons.Add(icon);
                _iconByContractId[contract.id] = icon;
            }

            LayoutRegionIcons(marker, icons);
        }

        private static void LayoutRegionIcons(RegionMarker marker, List<ContractIcon> icons)
        {
            var count = icons.Count;
            if (count == 0)
            {
                return;
            }

            var basePosition = marker.AnchoredPosition;
            for (var i = 0; i < count; i++)
            {
                var icon = icons[i];
                if (icon == null)
                {
                    continue;
                }

                var angle = (Mathf.PI * 2f * i) / count;
                var radius = 35f + (i % 3) * 10f;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                icon.SetAnchoredPosition(basePosition + offset);
                icon.Refresh();
            }
        }

        private static string PickContractIconKey(int index)
        {
            return index % 2 == 0 ? "contract_hunt" : "contract_escort";
        }

        private void SeedContracts()
        {
            _contractsByRegion.Clear();

            foreach (var region in _gameData.regions)
            {
                if (region.id == GuildHqId)
                {
                    _contractsByRegion[region.id] = new List<ContractData>();
                    continue;
                }

                var random = new System.Random(region.id.GetHashCode());
                var count = random.Next(2, 5);
                var contracts = new List<ContractData>(count);

                for (var i = 0; i < count; i++)
                {
                    contracts.Add(new ContractData
                    {
                        id = $"{region.id}_contract_{i}",
                        title = $"Contract #{i + 1}: {region.name}",
                        remainingSeconds = random.Next(45, 300),
                        reward = random.Next(50, 250),
                        iconKey = PickContractIconKey(i)
                    });
                }

                _contractsByRegion[region.id] = contracts;
            }
        }

        private void SeedSquads()
        {
            _squads.Clear();
            _squads.Add(new SquadData { id = "squad_iron_hawks", name = "Iron Hawks", membersCount = 6, hp = 100, state = SquadState.IdleAtHQ, currentRegionId = GuildHqId });
            _squads.Add(new SquadData { id = "squad_ash_blades", name = "Ash Blades", membersCount = 5, hp = 100, state = SquadState.IdleAtHQ, currentRegionId = GuildHqId });
            _squads.Add(new SquadData { id = "squad_grim_lantern", name = "Grim Lantern", membersCount = 4, hp = 100, state = SquadState.IdleAtHQ, currentRegionId = GuildHqId });
        }

        private List<SquadData> GetIdleSquads()
        {
            var list = new List<SquadData>();
            for (var i = 0; i < _squads.Count; i++)
            {
                if (_squads[i].state == SquadState.IdleAtHQ && _squads[i].hp > 0)
                {
                    list.Add(_squads[i]);
                }
            }

            return list;
        }

        private SquadData FindSquad(string squadId)
        {
            for (var i = 0; i < _squads.Count; i++)
            {
                if (_squads[i].id == squadId)
                {
                    return _squads[i];
                }
            }

            return null;
        }

        private void AddGold(int amount)
        {
            if (gameManager != null)
            {
                gameManager.AddGold(amount);
            }
        }

        private void HandleSquadDestroyed(string squadId)
        {
            var squad = FindSquad(squadId);
            if (squad == null)
            {
                return;
            }

            var failedTask = FindTaskForSquad(squadId);
            RemoveExistingTaskForSquad(squadId);
            RemoveTravelToken(squadId);
            encounterManager?.CancelPendingForSquad(squadId);

            if (failedTask != null)
            {
                var failedRegionId = failedTask.phase == TravelPhase.Outbound ? failedTask.toRegionId : failedTask.fromRegionId;
                RestoreContractAvailability(failedRegionId, failedTask.contractId);
            }

            squad.state = SquadState.IdleAtHQ;
            squad.currentRegionId = GuildHqId;
            squad.hp = 0;
            Debug.Log("Squad destroyed");
        }

        private void CompleteContract(string regionId, string contractId)
        {
            if (string.IsNullOrEmpty(regionId) || string.IsNullOrEmpty(contractId))
            {
                return;
            }

            if (_contractsByRegion.TryGetValue(regionId, out var contracts))
            {
                for (var i = contracts.Count - 1; i >= 0; i--)
                {
                    if (contracts[i].id == contractId)
                    {
                        contracts.RemoveAt(i);
                    }
                }
            }

            detailsPanel?.UnblockContract(contractId);
            SyncAllContractIcons();

            if (_regionById.TryGetValue(regionId, out var region))
            {
                SelectRegion(region);
            }
        }

        private void RestoreContractAvailability(string regionId, string contractId)
        {
            if (string.IsNullOrEmpty(contractId))
            {
                return;
            }

            detailsPanel?.UnblockContract(contractId);

            if (!string.IsNullOrEmpty(regionId) && _regionById.TryGetValue(regionId, out var region))
            {
                SelectRegion(region);
            }
        }

        private TravelTask FindTaskForSquad(string squadId)
        {
            for (var i = 0; i < _travelTasks.Count; i++)
            {
                if (_travelTasks[i].squadId == squadId)
                {
                    return _travelTasks[i];
                }
            }

            return null;
        }

        private void EnsureGuildHqRegion()
        {
            var exists = false;
            for (var i = 0; i < _gameData.regions.Count; i++)
            {
                if (_gameData.regions[i].id == GuildHqId)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                return;
            }

            _gameData.regions.Insert(0, new RegionData
            {
                id = GuildHqId,
                name = GuildHqName,
                pos = new NormalizedPosition { x = 0.5f, y = 0.52f },
                danger = 0,
                threats = new List<string>(),
                faction = "Guild",
                travelDays = 1,
                iconKey = "guild_hq"
            });
        }

        private void BuildRegionIndex()
        {
            _regionById.Clear();
            for (var i = 0; i < _gameData.regions.Count; i++)
            {
                _regionById[_gameData.regions[i].id] = _gameData.regions[i];
            }
        }

        private RegionData GetFirstPlayableRegion()
        {
            for (var i = 0; i < _gameData.regions.Count; i++)
            {
                if (_gameData.regions[i].id != GuildHqId)
                {
                    return _gameData.regions[i];
                }
            }

            return _gameData.regions.Count > 0 ? _gameData.regions[0] : null;
        }

        private void RefreshSquadStatusHud()
        {
            if (squadStatusHud == null)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            squadStatusHud.Sync(_squads, _travelTasks, ResolveRegionName, now);
        }

        private string ResolveRegionName(string regionId)
        {
            if (string.IsNullOrEmpty(regionId))
            {
                return null;
            }

            if (_regionById.TryGetValue(regionId, out var region) && region != null)
            {
                return region.name;
            }

            return regionId;
        }

        private static string ToTimerText(int remainingSeconds)
        {
            var clamped = Mathf.Max(0, remainingSeconds);
            var minutes = clamped / 60;
            var seconds = clamped % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
