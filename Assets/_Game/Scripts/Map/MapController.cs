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
        private enum DayState
        {
            MapActive,
            ResolvingEvents,
            ShowingReports,
            EveningGuildHall
        }
        private const string GuildHqId = "guild_hq";
        private const string GuildHqName = "Guild HQ";
        private const bool DEBUG_TRAVEL = true;

        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform markersRoot;
        [SerializeField] private RectTransform markersRootParent;
        [SerializeField] private ScrollRect mapScrollRect;
        [SerializeField] private RectTransform contractIconsRoot;
        [SerializeField] private RectTransform travelTokensRoot;
        [SerializeField] private RegionMarker regionMarkerPrefab;
        [SerializeField] private ContractIcon contractIconPrefab;
        [SerializeField] private TravelToken travelTokenPrefab;
        [SerializeField] private RegionDetailsPanel detailsPanel;
        [SerializeField] private SquadSelectPanel squadSelectPanel;
        [SerializeField] private EncounterManager encounterManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GameState gameState;
        [SerializeField] private SquadRoster squadRoster;
        [SerializeField] private HunterRoster hunterRoster;
        [SerializeField] private GameClock gameClock;
        [SerializeField] private SquadStatusHUD squadStatusHud;
        [SerializeField] private SquadDetailsPanel squadDetailsPanel;
        [SerializeField] private MissionReportPanel missionReportPanel;
        [SerializeField] private GuildHallPanel guildHallPanel;
        [SerializeField] private Button endDayButton;
        [SerializeField] private TMP_Text endDayHintText;
        [SerializeField] private GameObject endDayConfirmPanel;
        [SerializeField] private TMP_Text endDayConfirmBodyText;
        [SerializeField] private Button endDayConfirmYesButton;
        [SerializeField] private Button endDayConfirmNoButton;

        private readonly Dictionary<string, List<ContractData>> _contractsByRegion = new();
        private readonly Dictionary<string, RegionData> _regionById = new();
        private readonly Dictionary<string, RegionMarker> _markersByRegion = new();
        private readonly Dictionary<string, List<ContractIcon>> _iconsByRegion = new();
        private readonly Dictionary<string, ContractIcon> _iconByContractId = new();
        private readonly List<TravelTask> _travelTasks = new();
        private readonly Dictionary<string, TravelToken> _travelTokenBySquadId = new();
        private readonly Dictionary<string, TravelToken> _travelTokenBySoloHunterId = new();

        private GameData _gameData;
        private ContractIconPool _iconPool;
        private TravelTokenPool _travelTokenPool;
        private string _selectedSquadId;
        private readonly Queue<MissionReportData> _pendingReports = new();
        private bool _reportOpen;
        private int _dayIndex = 0;
        private GuildHallEveningData _guildHallEveningData;
        private float _stuckPauseSince = -1f;
        private DayState _dayState = DayState.MapActive;
        private bool _endDayRequested;
        private bool _endDayConfirmPauseHeld;

        private void Awake()
        {
            _gameData = GameDataLoader.Load();
            ResolveRuntimeReferences();
            EnsureGuildHqRegion();
            BuildRegionIndex();
            SeedContracts();
            EnsureHunterRoster();
            EnsureSquadRoster();
            EnsureSelectedSquad();
            SpawnMarkers();
            InitializePools();
            SyncAllContractIcons();
            EnsureEncounterDependencies();
            EnsureMissionReportPanel();
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();

            if (gameState == null)
            {
                gameState = FindFirstObjectByType<GameState>();
                if (gameState == null)
                {
                    gameState = gameObject.AddComponent<GameState>();
                }
            }

            squadStatusHud?.BindGameState(gameState);
            squadDetailsPanel?.BindMap(this);

            if (encounterManager != null)
            {
                encounterManager.Configure(FindSquad, FindHunter, AddGold, HandleSquadDestroyed, NotifyRosterChanged, GetSquadCohesionModifier, GetCurrentDayIndex);
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
            UpdateEndDayUiState();

            var firstPlayableRegion = GetFirstPlayableRegion();
            if (firstPlayableRegion != null)
            {
                SelectRegion(firstPlayableRegion);
            }
        }

        private void Start()
        {
            if (_markersByRegion.Count == 0)
            {
                SpawnMarkers();
                SyncAllContractIcons();
            }

            var count = squadRoster != null ? squadRoster.GetSquads().Count : 0;
            Debug.Log($"[RosterDebug] MapController roster squadsCount={count}");
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

            HideEndDayConfirm();
        }

        private void Update()
        {
            if (GamePauseService.Count <= 0)
            {
                _stuckPauseSince = -1f;
                return;
            }

            if (IsAnyContextModalActive())
            {
                _stuckPauseSince = -1f;
                return;
            }

            if (_stuckPauseSince < 0f)
            {
                _stuckPauseSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _stuckPauseSince > 0.25f)
            {
                Debug.LogError("[Pause] Stuck pause detected -> ResetAll [TODO REMOVE]");
                GamePauseService.ResetAll("StuckPause");
                _stuckPauseSince = -1f;
            }
        }

        private bool IsAnyContextModalActive()
        {
            var encounterPanel = FindFirstObjectByType<EncounterPanel>();
            var encounterActive = encounterPanel != null && encounterPanel.gameObject.activeInHierarchy;
            var reportActive = missionReportPanel != null && missionReportPanel.gameObject.activeInHierarchy;
            var squadSelectActive = squadSelectPanel != null && squadSelectPanel.gameObject.activeInHierarchy;
            var guildHallActive = guildHallPanel != null && guildHallPanel.gameObject.activeInHierarchy;
            var endDayConfirmActive = endDayConfirmPanel != null && endDayConfirmPanel.activeInHierarchy;
            return encounterActive || reportActive || squadSelectActive || guildHallActive || endDayConfirmActive;
        }

        private void OnTick(long simNow)
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
            UpdateEndDayUiState();
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
            var now = SimulationTime.NowSeconds;
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

                if (string.IsNullOrEmpty(task.squadId) && string.IsNullOrEmpty(task.soloHunterId))
                {
                    Debug.LogError("[Travel] Invalid task (no squadId and no hunterId), removing. [TODO REMOVE]");
                    completedTasks.Add(task);
                    continue;
                }

                TravelToken token = null;
                if (!string.IsNullOrEmpty(task.squadId))
                {
                    _travelTokenBySquadId.TryGetValue(task.squadId, out token);
                }
                else if (!string.IsNullOrEmpty(task.soloHunterId))
                {
                    _travelTokenBySoloHunterId.TryGetValue(task.soloHunterId, out token);
                }

                if (token != null)
                {
                    var progress = task.GetProgress(now);
                    var pos = Vector2.Lerp(fromMarker.AnchoredPosition, toMarker.AnchoredPosition, progress);
                    var remaining = Mathf.Max(0, (int)(task.endSimSeconds - now));
                    token.UpdateView(pos, ToTimerText(remaining));
                }

                if (now >= task.endSimSeconds)
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

            if (completedTasks.Count > 0)
            {
                TryAdvanceDayFlow("TravelTaskCompleted");
            }
        }

        private void HandleTravelTaskCompleted(TravelTask task, long nowUnix)
        {
            var squad = FindSquad(task.squadId);
            if (squad == null && string.IsNullOrEmpty(task.soloHunterId))
            {
                squad.currentRegionId = GuildHqId;
                squad.state = SquadState.IdleAtHQ;
                NotifyRosterChanged();
                RemoveTravelToken(task.squadId);
            }

            var report = BuildMissionReport(task, squad, soloHunter);
            _pendingReports.Enqueue(report);
            Debug.Log($"[Report] Enqueued: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");
            TryAdvanceDayFlow("ReportEnqueued");
            TryShowNextMissionReport();
        }

        private void OnEncounterResolved(string squadId, string regionId, string contractId, int contractReward, string soloHunterId = null)
        {
            if (!string.IsNullOrEmpty(soloHunterId))
            {
                var soloHunter = hunterRoster != null ? hunterRoster.GetById(soloHunterId) : null;
                if (soloHunter == null || soloHunter.hp <= 0)
                {
                    RestoreContractAvailability(regionId, contractId);
                    return;
                }

                StartSoloTravelTask(soloHunter, GuildHqId, contractId, contractReward, TravelPhase.Return);
                TryAdvanceDayFlow("EncounterResolved");
                return;
            }

            var squad = FindSquad(squadId);
            if (squad == null || squad.hp <= 0)
            {
                RemoveTravelToken(squadId);
                RestoreContractAvailability(regionId, contractId);
                return;
            }

            StartTravelTask(squad, regionId, GuildHqId, contractId, contractReward, TravelPhase.Return);
            squad.state = SquadState.ReturningToHQ;
            NotifyRosterChanged();

            if (DEBUG_TRAVEL)
            {
                Debug.Log($"[TravelDebug] Return task created: squad={squad.name}, {regionId}->{GuildHqId}");
            }

            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            TryAdvanceDayFlow("EncounterResolved");
        }

        private MissionReportData BuildMissionReport(TravelTask task, SquadData squad, HunterData soloHunter = null)
        {
            var readiness = soloHunter != null ? Mathf.RoundToInt(Mathf.Clamp01(soloHunter.maxHp > 0 ? soloHunter.hp / (float)soloHunter.maxHp : 1f) * 100f) : ComputeReadinessPercent(squad);
            var membersSummary = soloHunter != null ? $"Solo: {soloHunter.name}" : BuildMembersSummary(squad);
            var regionName = ResolveRegionName(task.fromRegionId);
            var contractTitle = ResolveContractTitle(task.fromRegionId, task.contractId);
            var outcome = readiness < 70
                ? "Contract completed. Loot secured. Injuries reported."
                : "Contract completed. Loot secured. Minor injuries.";

            return new MissionReportData
            {
                squadId = squad?.id,
                soloHunterId = soloHunter?.id,
                squadName = soloHunter != null ? soloHunter.name : squad?.name,
                regionId = task.fromRegionId,
                regionName = regionName,
                contractId = task.contractId,
                contractTitle = contractTitle,
                rewardGold = task.contractReward,
                readinessBeforePercent = readiness,
                readinessAfterPercent = readiness,
                membersSummary = membersSummary,
                outcomeText = outcome
            };
        }

        private void TryShowNextMissionReport()
        {
            EnsureMissionReportPanel();
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();
            if (missionReportPanel == null || _reportOpen || missionReportPanel.IsOpen || _pendingReports.Count == 0)
            {
                return;
            }

            var report = _pendingReports.Peek();
            Debug.Log($"[Report] Showing: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");
            _reportOpen = true;
            var shown = missionReportPanel.Show(report, () => OnMissionReportContinue(report));
            if (!shown)
            {
                _reportOpen = false;
                Debug.LogError("[Report] MissionReport failed to show; applying fallback continuation. [TODO REMOVE]");
                OnMissionReportContinue(report);
            }
        }
        private void OnMissionReportContinue(MissionReportData report)
        {
            Debug.Log($"[Report] onContinue invoked: squad={report?.squadId} contract={report?.contractId} reward={report?.rewardGold} [TODO REMOVE]");

            if (_pendingReports.Count > 0)
            {
                _pendingReports.Dequeue();
            }

            AddGold(report.rewardGold);
            CompleteContract(report.regionId, report.contractId);

            var reportSquad = FindSquad(report.squadId);
            if (reportSquad != null)
            {
                reportSquad.exhausted = true;
                reportSquad.exhaustedReason = "Needs rest";
                reportSquad.contractsDoneToday++;
                Debug.Log($"[Squad] Exhausted after contract: squad={reportSquad.id} contract={report.contractId} [TODO REMOVE]");

                var cohesionDelta = reportSquad.lastRosterChangeDay == _dayIndex ? 1 : 3;
                reportSquad.cohesion = Mathf.Clamp(reportSquad.cohesion + cohesionDelta, 0, 100);
                Debug.Log($"[Cohesion] After mission squad={reportSquad.id} cohesion={reportSquad.cohesion} delta={cohesionDelta} [TODO REMOVE]");
            }

            if (!string.IsNullOrEmpty(report.soloHunterId) && hunterRoster != null)
            {
                var hunter = hunterRoster.GetById(report.soloHunterId);
                if (hunter != null)
                {
                    hunter.exhaustedToday = true;
                }
            }

            missionReportPanel?.Hide();
            _reportOpen = false;
            Debug.Log($"[Report] Applied+Closed: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");

            RefreshSquadStatusHud();
            UpdateEndDayUiState();
            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            TryAdvanceDayFlow("MissionReportClosed");
            if (_pendingReports.Count > 0)
            {
                TryShowNextMissionReport();
            }
        }


        private void OnEndDayButtonClicked()
        {
            if (IsEndDayBlocked(out var reason))
            {
                Debug.Log($"[EndDay] blocked reason={reason} [TODO REMOVE]");
                UpdateEndDayUiState();
                return;
            }

            if (IsEndDayWarning(out var warning))
            {
                Debug.Log($"[EndDay] warning shown {warning} [TODO REMOVE]");
                ShowEndDayConfirm(warning);
                return;
            }

            _endDayRequested = true;
            Debug.Log("[EndDay] confirmed -> request end day [TODO REMOVE]");
            TryAdvanceDayFlow("EndDay");
            UpdateEndDayUiState();
        }

        private void EnsureEndDayButton()
        {
            EnsureEndDayHeaderLayout();
            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/RegionDetailsPanel/HeaderContainer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                var canvas = EnsureCanvas();
                if (canvas == null)
                {
                    return;
                }

                var button = CreateButton(canvas.transform, "EndDayButton", "End Day");
                endDayButton = button;
            }

            EnsureEndDayHeaderLayout();

            if (endDayButton != null)
            {
                endDayButton.onClick.RemoveListener(OnEndDayButtonClicked);
                endDayButton.onClick.AddListener(OnEndDayButtonClicked);
            }

            UpdateEndDayUiState();
        }

        private void EnsureEndDayHeaderLayout()
        {
            if (detailsPanel == null)
            {
                return;
            }

            var soloHunter = !string.IsNullOrEmpty(task.soloHunterId) && hunterRoster != null ? hunterRoster.GetById(task.soloHunterId) : null;

            if (task.phase == TravelPhase.Outbound)
            {
                if (soloHunter != null)
                {
                    if (encounterManager != null)
                    {
                        encounterManager.EnqueueEncounter(task.toRegionId, null, () => OnEncounterResolved(null, task.toRegionId, task.contractId, task.contractReward, soloHunter.id), soloHunter.id);
                        TryAdvanceDayFlow("EncounterQueued");
                    }
                    else
                    {
                        OnEncounterResolved(null, task.toRegionId, task.contractId, task.contractReward, soloHunter.id);
                    }

                    return;
                }
                if (DEBUG_TRAVEL)
                {
                    continue;
                }

                var seed = (region.id != null ? region.id.GetHashCode() : 0) ^ (_dayIndex * 397);
                var random = new System.Random(seed);
                if (!_contractsByRegion.TryGetValue(region.id, out var contracts) || contracts == null)
                {
                    contracts = new List<ContractData>();
                    _contractsByRegion[region.id] = contracts;
                }

                if (contracts.Count == 0)
                {
                    var count = random.Next(2, 5);
                    for (var i = 0; i < count; i++)
                    {
                        contracts.Add(new ContractData
                        {
                            id = $"{region.id}_day{_dayIndex}_contract_{i}",
                            title = $"Contract #{i + 1}: {region.name}",
                            remainingSeconds = random.Next(45, 300),
                            reward = random.Next(50, 250),
                            iconKey = PickContractIconKey(i),
                            minRank = i % 3 == 0 ? HunterRank.C : HunterRank.D,
                            allowSquad = i % 5 != 0,
                            allowSolo = true
                        });
                    }

                    encounterManager.EnqueueEncounter(task.toRegionId, squad.id, () => OnEncounterResolved(squad.id, task.toRegionId, task.contractId, task.contractReward));
                    TryAdvanceDayFlow("EncounterQueued");
                }
                else
                {
                    for (var i = 0; i < contracts.Count; i++)
                    {
                        contracts[i].remainingSeconds = random.Next(45, 300);
                        contracts[i].reward = random.Next(50, 250);
                    }
                }
            }

            if (soloHunter != null)
            {
                soloHunter.squadId = null;
                RemoveSoloTravelToken(soloHunter.id);
            }
            else
            {
                squad.currentRegionId = GuildHqId;
                squad.state = SquadState.IdleAtHQ;
                NotifyRosterChanged();
                RemoveTravelToken(task.squadId);
            }

            var report = BuildMissionReport(task, squad, soloHunter);
            _pendingReports.Enqueue(report);
            Debug.Log($"[Report] Enqueued: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");
            TryAdvanceDayFlow("ReportEnqueued");
            TryShowNextMissionReport();
        }

        private void OnEncounterResolved(string squadId, string regionId, string contractId, int contractReward, string soloHunterId = null)
        {
            if (!string.IsNullOrEmpty(soloHunterId))
            {
                var soloHunter = hunterRoster != null ? hunterRoster.GetById(soloHunterId) : null;
                if (soloHunter == null || soloHunter.hp <= 0)
                {
                    RestoreContractAvailability(regionId, contractId);
                    return;
                }

                StartSoloTravelTask(soloHunter, GuildHqId, contractId, contractReward, TravelPhase.Return);
                TryAdvanceDayFlow("EncounterResolved");
                return;
            }

            var squad = FindSquad(squadId);
            if (squad == null || squad.hp <= 0)
            {
                _reportOpen = missionReportPanel.IsOpen;
                EnsureMissionReportInteractionInfra();
                return;
            }

            missionReportPanel = FindFirstObjectByType<MissionReportPanel>();
            if (missionReportPanel != null)
            {
                _reportOpen = missionReportPanel.IsOpen;
                EnsureMissionReportInteractionInfra();
                return;
            }

            var prefab = Resources.Load<GameObject>("Prefabs/MissionReportPanel");
            var canvas = EnsureCanvas();
            if (canvas == null)
            {
                return;
            }

            TryAdvanceDayFlow("EncounterResolved");
        }

        private MissionReportData BuildMissionReport(TravelTask task, SquadData squad, HunterData soloHunter = null)
        {
            var readiness = soloHunter != null ? Mathf.RoundToInt(Mathf.Clamp01(soloHunter.maxHp > 0 ? soloHunter.hp / (float)soloHunter.maxHp : 1f) * 100f) : ComputeReadinessPercent(squad);
            var membersSummary = soloHunter != null ? $"Solo: {soloHunter.name}" : BuildMembersSummary(squad);
            var regionName = ResolveRegionName(task.fromRegionId);
            var contractTitle = ResolveContractTitle(task.fromRegionId, task.contractId);
            var outcome = readiness < 70
                ? "Contract completed. Loot secured. Injuries reported."
                : "Contract completed. Loot secured. Minor injuries.";

            return new MissionReportData
            {
                squadId = squad?.id,
                soloHunterId = soloHunter?.id,
                squadName = soloHunter != null ? soloHunter.name : squad?.name,
                regionId = task.fromRegionId,
                regionName = regionName,
                contractId = task.contractId,
                contractTitle = contractTitle,
                rewardGold = task.contractReward,
                readinessBeforePercent = readiness,
                readinessAfterPercent = readiness,
                membersSummary = membersSummary,
                outcomeText = outcome
            };
        }

        private void TryShowNextMissionReport()
        {
            EnsureMissionReportPanel();
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();
            if (missionReportPanel == null || _reportOpen || missionReportPanel.IsOpen || _pendingReports.Count == 0)
            {
                var instance = Instantiate(prefab, canvas.transform, false);
                missionReportPanel = instance.GetComponent<MissionReportPanel>();
                if (missionReportPanel != null)
                {
                    instance.transform.SetAsLastSibling();
                    missionReportPanel.Hide();
                    _reportOpen = false;
                    EnsureMissionReportInteractionInfra();
                    return;
                }
            }

            var report = _pendingReports.Peek();
            Debug.Log($"[Report] Showing: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");
            _reportOpen = true;
            var shown = missionReportPanel.Show(report, () => OnMissionReportContinue(report));
            if (!shown)
            {
                _reportOpen = false;
                Debug.LogError("[Report] MissionReport failed to show; applying fallback continuation. [TODO REMOVE]");
                OnMissionReportContinue(report);
            }
        }
        private void OnMissionReportContinue(MissionReportData report)
        {
            Debug.Log($"[Report] onContinue invoked: squad={report?.squadId} contract={report?.contractId} reward={report?.rewardGold} [TODO REMOVE]");

            if (_pendingReports.Count > 0)
            {
                _pendingReports.Dequeue();
            }

            AddGold(report.rewardGold);
            CompleteContract(report.regionId, report.contractId);

            var reportSquad = FindSquad(report.squadId);
            if (reportSquad != null)
            {
                reportSquad.exhausted = true;
                reportSquad.exhaustedReason = "Needs rest";
                reportSquad.contractsDoneToday++;
                Debug.Log($"[Squad] Exhausted after contract: squad={reportSquad.id} contract={report.contractId} [TODO REMOVE]");

                var cohesionDelta = reportSquad.lastRosterChangeDay == _dayIndex ? 1 : 3;
                reportSquad.cohesion = Mathf.Clamp(reportSquad.cohesion + cohesionDelta, 0, 100);
                Debug.Log($"[Cohesion] After mission squad={reportSquad.id} cohesion={reportSquad.cohesion} delta={cohesionDelta} [TODO REMOVE]");
            }

            if (!string.IsNullOrEmpty(report.soloHunterId) && hunterRoster != null)
            {
                var hunter = hunterRoster.GetById(report.soloHunterId);
                if (hunter != null)
                {
                    hunter.exhaustedToday = true;
                }
            }

            missionReportPanel?.Hide();
            _reportOpen = false;
            Debug.Log($"[Report] Applied+Closed: squad={report.squadId} contract={report.contractId} reward={report.rewardGold} [TODO REMOVE]");

            RefreshSquadStatusHud();
            UpdateEndDayUiState();
            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            TryAdvanceDayFlow("MissionReportClosed");
            if (_pendingReports.Count > 0)
            {
                TryShowNextMissionReport();
            }
        }


        private void OnEndDayButtonClicked()
        {
            if (IsEndDayBlocked(out var reason))
            {
                Debug.Log($"[EndDay] blocked reason={reason} [TODO REMOVE]");
                UpdateEndDayUiState();
                return;
            }

            if (IsEndDayWarning(out var warning))
            {
                Debug.Log($"[EndDay] warning shown {warning} [TODO REMOVE]");
                ShowEndDayConfirm(warning);
                return;
            }

            _endDayRequested = true;
            Debug.Log("[EndDay] confirmed -> request end day [TODO REMOVE]");
            TryAdvanceDayFlow("EndDay");
            UpdateEndDayUiState();
        }

        private void EnsureEndDayButton()
        {
            EnsureEndDayHeaderLayout();
            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/RegionDetailsPanel/HeaderContainer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                endDayButton = transform.Find("MapCanvas/OverlayLayer/EndDayButton")?.GetComponent<Button>();
            }

            if (endDayButton == null)
            {
                var canvas = EnsureCanvas();
                if (canvas == null)
                {
                    return;
                }

                var button = CreateButton(canvas.transform, "EndDayButton", "End Day");
                endDayButton = button;
            }

            EnsureEndDayHeaderLayout();

            if (endDayButton != null)
            {
                endDayButton.onClick.RemoveListener(OnEndDayButtonClicked);
                endDayButton.onClick.AddListener(OnEndDayButtonClicked);
            }

            UpdateEndDayUiState();
        }

        private void EnsureEndDayHeaderLayout()
        {
            if (detailsPanel == null)
            {
                return;
            }

            var panel = detailsPanel.transform as RectTransform;
            if (panel == null)
            {
                return;
            }

            var header = panel.Find("HeaderContainer") as RectTransform;
            if (header == null)
            {
                var headerGo = new GameObject("HeaderContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                headerGo.transform.SetParent(panel, false);
                header = headerGo.GetComponent<RectTransform>();
                header.SetSiblingIndex(0);

                var headerLayout = headerGo.GetComponent<HorizontalLayoutGroup>();
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = false;
                headerLayout.childForceExpandHeight = false;
                headerLayout.spacing = 10f;
                headerLayout.padding = new RectOffset(0, 0, 0, 0);

                var headerElement = headerGo.GetComponent<LayoutElement>();
                headerElement.minHeight = 40f;
                headerElement.preferredHeight = 44f;
            }

            var regionName = panel.Find("RegionName")?.GetComponent<TMP_Text>();
            if (regionName != null && regionName.transform.parent != header)
            {
                regionName.transform.SetParent(header, false);
            }

            if (regionName != null)
            {
                regionName.textWrappingMode = TextWrappingModes.NoWrap;
                regionName.overflowMode = TextOverflowModes.Ellipsis;
                var nameLayout = regionName.GetComponent<LayoutElement>() ?? regionName.gameObject.AddComponent<LayoutElement>();
                nameLayout.flexibleWidth = 1f;
                nameLayout.minWidth = 120f;
            }

            if (endDayButton != null && endDayButton.transform.parent != header)
            {
                endDayButton.transform.SetParent(header, false);
            }

            if (endDayButton != null)
            {
                var buttonLayout = endDayButton.GetComponent<LayoutElement>() ?? endDayButton.gameObject.AddComponent<LayoutElement>();
                buttonLayout.preferredWidth = 160f;
                buttonLayout.minWidth = 140f;
                buttonLayout.flexibleWidth = 0f;
                var buttonRect = endDayButton.GetComponent<RectTransform>();
                if (buttonRect != null)
                {
                    buttonRect.sizeDelta = new Vector2(160f, 36f);
                }
            }

            if (endDayHintText == null)
            {
                var hint = panel.Find("EndDayHint")?.GetComponent<TMP_Text>();
                if (hint == null)
                {
                    var hintGo = new GameObject("EndDayHint", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                    hintGo.transform.SetParent(panel, false);
                    hintGo.transform.SetSiblingIndex(Mathf.Min(1, panel.childCount - 1));
                    hint = hintGo.GetComponent<TextMeshProUGUI>();
                    hint.fontSize = 16f;
                    hint.alignment = TextAlignmentOptions.Left;
                    hint.color = new Color(1f, 0.85f, 0.55f, 1f);
                    hint.textWrappingMode = TextWrappingModes.Normal;
                    var hintLayout = hintGo.GetComponent<LayoutElement>();
                    hintLayout.minHeight = 20f;
                    hintLayout.preferredHeight = 24f;
                }

                endDayHintText = hint;
            }
        }

        private bool IsEndDayBlocked(out string reason)
        {
            if (_travelTasks.Count > 0)
            {
                reason = "Squads are still on missions.";
                return true;
            }

            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (encounterQueued || encounterActive)
            {
                reason = "Resolve encounters/reports first.";
                return true;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            if (_pendingReports.Count > 0 || reportActive)
            {
                reason = "Resolve encounters/reports first.";
                return true;
            }

            if (IsAnyContextModalActive())
            {
                reason = "Close active windows first.";
                return true;
            }

            reason = null;
            return false;
        }

        private bool IsEndDayWarning(out string warning)
        {
            warning = null;
            if (IsEndDayBlocked(out _))
            {
                return false;
            }

            var availableContracts = 0;
            foreach (var pair in _contractsByRegion)
            {
                if (pair.Key == GuildHqId || pair.Value == null)
                {
                    continue;
                }

                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var contract = pair.Value[i];
                    if (contract == null || contract.IsExpired || IsContractAssigned(contract.id))
                    {
                        continue;
                    }

                    availableContracts++;
                }
            }

            var readyIdleSquads = 0;
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.IsDestroyed)
                {
                    continue;
                }

                if (squad.state == SquadState.IdleAtHQ && !squad.exhausted)
                {
                    readyIdleSquads++;
                }
            }

            if (availableContracts > 0 && readyIdleSquads > 0)
            {
                warning = $"You still have {availableContracts} available contracts and {readyIdleSquads} ready squads. End the day anyway?";
                return true;
            }

            return false;
        }

        private bool IsContractAssigned(string contractId)
        {
            if (string.IsNullOrEmpty(contractId))
            {
                return false;
            }

            for (var i = 0; i < _travelTasks.Count; i++)
            {
                if (_travelTasks[i] != null && _travelTasks[i].contractId == contractId)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateEndDayUiState()
        {
            if (endDayButton == null)
            {
                return;
            }

            if (IsEndDayBlocked(out var reason))
            {
                endDayButton.interactable = false;
                var label = endDayButton.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = "End Day";
                }

                if (endDayHintText != null)
                {
                    endDayHintText.text = $"Cannot end day: {reason}";
                }

                return;
            }

            endDayButton.interactable = true;
            if (endDayHintText != null)
            {
                endDayHintText.text = string.Empty;
            }

            var buttonLabel = endDayButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
            {
                buttonLabel.text = IsEndDayWarning(out _) ? "End Day (Confirm)" : "End Day";
            }
        }

        private void EnsureEndDayConfirmPanel()
        {
            if (endDayConfirmPanel == null)
            {
                var canvas = EnsureCanvas();
                if (canvas == null)
                {
                    return;
                }

                var root = new GameObject("EndDayConfirmPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                root.transform.SetParent(canvas.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var bg = root.GetComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.55f);

                var content = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
                content.transform.SetParent(root.transform, false);
                var contentRect = content.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.sizeDelta = new Vector2(720f, 280f);
                var contentImage = content.GetComponent<Image>();
                contentImage.color = new Color(0.08f, 0.1f, 0.14f, 0.98f);
                var contentLayoutElement = content.GetComponent<LayoutElement>() ?? content.AddComponent<LayoutElement>();
                contentLayoutElement.minWidth = 420f;
                contentLayoutElement.preferredWidth = 560f;
                contentLayoutElement.minHeight = 260f;
                contentLayoutElement.preferredHeight = 280f;
                var v = content.GetComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(18, 18, 18, 18);
                v.spacing = 12f;
                v.childControlHeight = true;
                v.childControlWidth = true;
                v.childForceExpandHeight = false;

                var title = CreateText(content.transform, "Title", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
                title.text = "End Day?";
                var titleLayout = title.gameObject.AddComponent<LayoutElement>();
                titleLayout.preferredHeight = 40f;

                var body = CreateText(content.transform, "Body", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                body.textWrappingMode = TextWrappingModes.Normal;
                body.overflowMode = TextOverflowModes.Overflow;
                body.text = "";
                var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
                bodyLayout.preferredHeight = 120f;

                var row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(content.transform, false);
                var h = row.GetComponent<HorizontalLayoutGroup>();
                h.spacing = 12f;
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = true;
                h.childControlHeight = true;
                h.childForceExpandHeight = false;
                h.childForceExpandWidth = false;

                var rowLayout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
                rowLayout.minHeight = 56f;
                rowLayout.preferredHeight = 60f;

                var yes = CreateButton(row.transform, "ConfirmButton", "Confirm");
                var no = CreateButton(row.transform, "CancelButton", "Cancel");
                ConfigureEndDayConfirmButtonVisuals(yes);
                ConfigureEndDayConfirmButtonVisuals(no);

                endDayConfirmPanel = root;
                endDayConfirmBodyText = body;
                endDayConfirmYesButton = yes;
                endDayConfirmNoButton = no;
            }

            if (endDayConfirmYesButton != null)
            {
                endDayConfirmYesButton.onClick.RemoveAllListeners();
                endDayConfirmYesButton.onClick.AddListener(OnEndDayConfirmYes);
            }

            if (endDayConfirmNoButton != null)
            {
                endDayConfirmNoButton.onClick.RemoveAllListeners();
                endDayConfirmNoButton.onClick.AddListener(HideEndDayConfirm);
            }

            if (endDayConfirmPanel != null)
            {
                endDayConfirmPanel.SetActive(false);
            }
        }

        private void ConfigureEndDayConfirmButtonVisuals(Button button)
        {
            if (button == null)
            {
                return;
            }

            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 160f;
            layout.preferredWidth = 180f;
            layout.minHeight = 44f;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null)
            {
                return;
            }

            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void ShowEndDayConfirm(string warning)
        {
            EnsureEndDayConfirmPanel();
            if (endDayConfirmPanel == null)
            {
                return;
            }

            if (endDayConfirmBodyText != null)
            {
                endDayConfirmBodyText.text = warning;
            }

            endDayConfirmPanel.SetActive(true);
            if (!_endDayConfirmPauseHeld)
            {
                GamePauseService.Push("EndDayConfirm");
                _endDayConfirmPauseHeld = true;
            }
        }

        private void HideEndDayConfirm()
        {
            if (endDayConfirmPanel != null)
            {
                endDayConfirmPanel.SetActive(false);
            }

            if (_endDayConfirmPauseHeld)
            {
                GamePauseService.Pop("EndDayConfirm");
                _endDayConfirmPauseHeld = false;
            }
        }

        private void OnEndDayConfirmYes()
        {
            HideEndDayConfirm();
            _endDayRequested = true;
            Debug.Log("[EndDay] confirmed -> request end day [TODO REMOVE]");
            TryAdvanceDayFlow("EndDayConfirm");
            UpdateEndDayUiState();
        }

        private void TryAdvanceDayFlow(string trigger)
        {
            if (_dayState == DayState.EveningGuildHall)
            {
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (_pendingReports.Count > 0 || reportActive)
            {
                _dayState = DayState.ShowingReports;
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            if (encounterQueued || encounterActive)
            {
                _dayState = DayState.ResolvingEvents;
                LogDayFlow(trigger, IsAnyContextModalActive());
                return;
            }

            var anyModal = IsAnyContextModalActive();
            if (_endDayRequested && CanEnterEveningNow(anyModal))
            {
                _dayState = DayState.EveningGuildHall;
                LogDayFlow(trigger, anyModal);
                EnterGuildHallEvening();
                return;
            }

            _dayState = DayState.MapActive;
            LogDayFlow(trigger, anyModal);
        }

        private bool CanEnterEveningNow(bool anyModal)
        {
            if (!_endDayRequested)
            {
                return false;
            }

            if (_travelTasks.Count > 0)
            {
                return false;
            }

            var encounterQueued = encounterManager != null && encounterManager.PendingEncounterCount > 0;
            var encounterActive = encounterManager != null && encounterManager.IsEncounterActive;
            if (encounterQueued || encounterActive)
            {
                return false;
            }

            var reportActive = missionReportPanel != null && missionReportPanel.IsOpen;
            if (_pendingReports.Count > 0 || reportActive)
            {
                return false;
            }

            if (anyModal)
            {
                return false;
            }

            return GamePauseService.Count == 0;
        }

        private void LogDayFlow(string trigger, bool anyModal)
        {
            Debug.Log($"[DayFlow] reason={trigger} endDayRequested={_endDayRequested} travelActive={_travelTasks.Count} encounters={(encounterManager != null ? encounterManager.PendingEncounterCount : 0)}/{(encounterManager != null && encounterManager.IsEncounterActive)} reports={_pendingReports.Count}/{(missionReportPanel != null && missionReportPanel.IsOpen)} modals={anyModal} [TODO REMOVE]");
        }

        private void EnterGuildHallEvening()
        {
            Debug.Log("[GuildHall] Enter evening [TODO REMOVE]");
            EnsureGuildHallPanel();
            EnsureEndDayButton();
            EnsureEndDayConfirmPanel();
            if (guildHallPanel == null)
            {
                Debug.LogWarning("[GuildHall] Panel missing, skipping evening. [TODO REMOVE]");
                OnGuildHallNextDay();
                return;
            }

            if (_guildHallEveningData == null)
            {
                _guildHallEveningData = GuildHallEveningLoader.Load();
            }

            _endDayRequested = false;
            EnsureEventSystem();
            guildHallPanel.ShowEvening(_guildHallEveningData, _dayIndex, OnGuildHallNextDay, ApplyRestEveningEffect);
        }

        private void ApplyRestEveningEffect()
        {
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || squad.IsDestroyed)
                {
                    continue;
                }

                if (squad.members == null)
                {
                    continue;
                }

                for (var m = 0; m < squad.members.Count; m++)
                {
                    var member = squad.members[m];
                    if (member == null || member.maxHp <= 0)
                    {
                        continue;
                    }

                    member.hp = Mathf.Min(member.maxHp, member.hp + 5);
                }
            }

            RefreshSquadStatusHud();
            UpdateEndDayUiState();
            squadDetailsPanel?.Refresh();
        }

        private void OnGuildHallNextDay()
        {
            guildHallPanel?.Hide();
            _endDayRequested = false;
            _dayIndex++;
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null)
                {
                    continue;
                }

                squad.exhausted = false;
                squad.exhaustedReason = null;
                squad.contractsDoneToday = 0;
                if (squad.lastRosterChangeDay != _dayIndex - 1)
                {
                    squad.cohesion = Mathf.Clamp(squad.cohesion + 1, 0, 100);
                }
            }

            if (hunterRoster != null)
            {
                for (var i = 0; i < hunterRoster.Hunters.Count; i++)
                {
                    if (hunterRoster.Hunters[i] != null)
                    {
                        hunterRoster.Hunters[i].exhaustedToday = false;
                    }
                }
            }

            RefreshContractsForNextDay();
            SyncAllContractIcons();

            if (detailsPanel != null)
            {
                return;
            }

            Debug.Log($"[Day] Reset squad exhaustion, day={_dayIndex} [TODO REMOVE]");
            UpdateEndDayUiState();
        }

        private void RefreshContractsForNextDay()
        {
            foreach (var region in _gameData.regions)
            {
                if (region == null || region.id == GuildHqId)
                {
                    continue;
                }

                var seed = (region.id != null ? region.id.GetHashCode() : 0) ^ (_dayIndex * 397);
                var random = new System.Random(seed);
                if (!_contractsByRegion.TryGetValue(region.id, out var contracts) || contracts == null)
                {
                    contracts = new List<ContractData>();
                    _contractsByRegion[region.id] = contracts;
                }

                if (contracts.Count == 0)
                {
                    var count = random.Next(2, 5);
                    for (var i = 0; i < count; i++)
                    {
                        contracts.Add(new ContractData
                        {
                            id = $"{region.id}_day{_dayIndex}_contract_{i}",
                            title = $"Contract #{i + 1}: {region.name}",
                            remainingSeconds = random.Next(45, 300),
                            reward = random.Next(50, 250),
                            iconKey = PickContractIconKey(i),
                            minRank = i % 3 == 0 ? HunterRank.C : HunterRank.D,
                            allowSquad = i % 5 != 0,
                            allowSolo = true
                        });
                    }
                }
                else
                {
                    for (var i = 0; i < contracts.Count; i++)
                    {
                        contracts[i].remainingSeconds = random.Next(45, 300);
                        contracts[i].reward = random.Next(50, 250);
                    }
                }
            }
        }

        private void EnsureGuildHallPanel()
        {
            if (guildHallPanel != null)
            {
                _reportOpen = missionReportPanel.IsOpen;
                EnsureMissionReportInteractionInfra();
                return;
            }

            guildHallPanel = FindFirstObjectByType<GuildHallPanel>();
            if (guildHallPanel != null)
            {
                _reportOpen = missionReportPanel.IsOpen;
                EnsureMissionReportInteractionInfra();
                return;
            }

            var prefab = Resources.Load<GameObject>("Prefabs/GuildHallPanel");
            var canvas = EnsureCanvas();
            if (canvas == null)
            {
                return;
            }

            if (prefab != null)
            {
                var instance = Instantiate(prefab, canvas.transform, false);
                guildHallPanel = instance.GetComponent<GuildHallPanel>();
                if (guildHallPanel != null)
                {
                    instance.transform.SetAsLastSibling();
                    missionReportPanel.Hide();
                    _reportOpen = false;
                    EnsureMissionReportInteractionInfra();
                    return;
                }
            }

            missionReportPanel = CreateRuntimeMissionReportPanel(canvas);
            _reportOpen = false;
            EnsureMissionReportInteractionInfra();
        }


        private void EnsureMissionReportInteractionInfra()
        {
            EnsureEventSystem();
            if (missionReportPanel == null)
            {
                return;
            }

            var canvas = missionReportPanel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void EnsureGuildHallPanel()
        {
            if (guildHallPanel != null)
            {
                return;
            }

            guildHallPanel = FindFirstObjectByType<GuildHallPanel>();
            if (guildHallPanel != null)
            {
                guildHallPanel.Hide();
                return;
            }

            var prefab = Resources.Load<GameObject>("Prefabs/GuildHallPanel");
            var canvas = EnsureCanvas();
            if (canvas == null)
            {
                return;
            }

            if (prefab != null)
            {
                var instance = Instantiate(prefab, canvas.transform, false);
                guildHallPanel = instance.GetComponent<GuildHallPanel>();
                if (guildHallPanel != null)
                {
                    instance.transform.SetAsLastSibling();
                    guildHallPanel.Hide();
                    return;
                }
            }

            guildHallPanel = CreateRuntimeGuildHallPanel(canvas);
        }

        private GuildHallPanel CreateRuntimeGuildHallPanel(Canvas canvas)
        {
            var root = new GameObject("GuildHallPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(GuildHallPanel));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();

            var dimmer = root.GetComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.65f);
            dimmer.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            content.transform.SetAsLastSibling();

            var backgroundLayer = new GameObject("BackgroundLayer", typeof(RectTransform), typeof(Image));
            backgroundLayer.transform.SetParent(content.transform, false);
            var backgroundRect = backgroundLayer.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = backgroundLayer.GetComponent<Image>();
            backgroundImage.color = new Color(0.15f, 0.13f, 0.1f, 1f);
            backgroundImage.raycastTarget = false;

            var stageLayer = new GameObject("StageLayer", typeof(RectTransform), typeof(Image));
            stageLayer.transform.SetParent(content.transform, false);
            var stageRect = stageLayer.GetComponent<RectTransform>();
            stageRect.anchorMin = new Vector2(0.05f, 0.20f);
            stageRect.anchorMax = new Vector2(0.95f, 0.88f);
            stageRect.offsetMin = Vector2.zero;
            stageRect.offsetMax = Vector2.zero;
            var stageImage = stageLayer.GetComponent<Image>();
            stageImage.color = new Color(0.25f, 0.2f, 0.16f, 0.38f);
            stageImage.raycastTarget = false;

            var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(content.transform, false);
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0.02f, 0.90f);
            topRect.anchorMax = new Vector2(0.98f, 0.98f);
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;
            var topImage = topBar.GetComponent<Image>();
            topImage.color = new Color(0.08f, 0.08f, 0.09f, 0.72f);
            topImage.raycastTarget = false;

            var title = CreateText(topBar.transform, "Title", 34f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.text = "GUILD HALL";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(0.7f, 1f);
            titleRect.offsetMin = new Vector2(20f, 0f);
            titleRect.offsetMax = Vector2.zero;

            var day = CreateText(topBar.transform, "Day", 26f, FontStyles.Bold, TextAlignmentOptions.Right);
            day.text = "Day 1";
            var dayRect = day.rectTransform;
            dayRect.anchorMin = new Vector2(0.58f, 0f);
            dayRect.anchorMax = new Vector2(0.8f, 1f);
            dayRect.offsetMin = Vector2.zero;
            dayRect.offsetMax = Vector2.zero;

            var hint = CreateText(topBar.transform, "Hint", 18f, FontStyles.Italic, TextAlignmentOptions.Center);
            hint.text = "Click a character to talk";
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.22f, 0f);
            hintRect.anchorMax = new Vector2(0.58f, 1f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;

            var nextDayButton = CreateButton(topBar.transform, "NextDayButton", "Next Day");
            var nextDayRect = (RectTransform)nextDayButton.transform;
            nextDayRect.anchorMin = new Vector2(0.82f, 0.14f);
            nextDayRect.anchorMax = new Vector2(0.99f, 0.86f);
            nextDayRect.offsetMin = Vector2.zero;
            nextDayRect.offsetMax = Vector2.zero;

            var dialogueBar = new GameObject("DialogueBar", typeof(RectTransform), typeof(Image));
            dialogueBar.transform.SetParent(content.transform, false);
            var dialogueRect = dialogueBar.GetComponent<RectTransform>();
            dialogueRect.anchorMin = new Vector2(0.02f, 0.02f);
            dialogueRect.anchorMax = new Vector2(0.98f, 0.30f);
            dialogueRect.offsetMin = Vector2.zero;
            dialogueRect.offsetMax = Vector2.zero;
            var dialogueImage = dialogueBar.GetComponent<Image>();
            dialogueImage.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
            dialogueImage.raycastTarget = true;

            var speaker = CreateText(dialogueBar.transform, "Speaker", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            var speakerRect = speaker.rectTransform;
            speakerRect.anchorMin = new Vector2(0.02f, 0.72f);
            speakerRect.anchorMax = new Vector2(0.52f, 0.98f);
            speakerRect.offsetMin = Vector2.zero;
            speakerRect.offsetMax = Vector2.zero;

            var body = CreateText(dialogueBar.transform, "Body", 24f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            var bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.02f, 0.12f);
            bodyRect.anchorMax = new Vector2(0.74f, 0.70f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            buttons.transform.SetParent(dialogueBar.transform, false);
            var buttonsRect = buttons.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.76f, 0.12f);
            buttonsRect.anchorMax = new Vector2(0.98f, 0.88f);
            buttonsRect.offsetMin = Vector2.zero;
            buttonsRect.offsetMax = Vector2.zero;
            var buttonsLayout = buttons.GetComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 10f;
            buttonsLayout.childControlWidth = true;
            buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;

            var nextButton = CreateButton(buttons.transform, "NextButton", "Next");
            var skipButton = CreateButton(buttons.transform, "SkipButton", "Skip");

            dialogueBar.SetActive(false);
            stageLayer.transform.SetAsLastSibling();
            dialogueBar.transform.SetAsLastSibling();
            topBar.transform.SetAsLastSibling();

            var panel = root.GetComponent<GuildHallPanel>();
            panel.ConfigureRuntimeBindings(
                root,
                dimmer,
                contentRect,
                backgroundImage,
                stageRect,
                title,
                day,
                hint,
                nextDayButton,
                dialogueBar,
                speaker,
                body,
                nextButton,
                skipButton);
            panel.Hide();
            return panel;
        }

        private MissionReportPanel CreateRuntimeMissionReportPanel(Canvas canvas)
        {
            var root = new GameObject("MissionReportPanel", typeof(RectTransform), typeof(Image), typeof(MissionReportPanel));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();

            var blocker = root.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.68f);
            blocker.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            content.transform.SetParent(root.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(760f, 460f);

            var contentImage = content.GetComponent<Image>();
            contentImage.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateText(content.transform, "Title", 34f, FontStyles.Bold, TextAlignmentOptions.Center);
            var body = CreateText(content.transform, "Body", 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            var bodyLayout = body.gameObject.GetComponent<LayoutElement>() ?? body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 260f;
            bodyLayout.flexibleHeight = 1f;

            var continueButton = CreateButton(content.transform, "ContinueButton", "Continue");
            var panel = root.GetComponent<MissionReportPanel>();
            panel.ConfigureRuntimeBindings(root, blocker, title, body, continueButton);
            panel.Hide();
            return panel;
        }

        private string ResolveContractTitle(string regionId, string contractId)
        {
            if (!string.IsNullOrEmpty(regionId) && _contractsByRegion.TryGetValue(regionId, out var contracts))
            {
                for (var i = 0; i < contracts.Count; i++)
                {
                    if (contracts[i] != null && contracts[i].id == contractId)
                    {
                        return contracts[i].title;
                    }
                }
            }

            return contractId;
        }

        private static string BuildMembersSummary(SquadData squad)
        {
            if (squad?.members == null || squad.members.Count == 0)
            {
                return "Members not implemented yet";
            }

            return $"{squad.members.Count} members";
        }

        private static int ComputeReadinessPercent(SquadData squad)
        {
            if (squad?.members == null || squad.members.Count == 0)
            {
                return 100;
            }

            float sum = 0f;
            var valid = 0;
            for (var i = 0; i < squad.members.Count; i++)
            {
                var member = squad.members[i];
                if (member == null || member.maxHp <= 0)
                {
                    continue;
                }

                sum += Mathf.Clamp01(member.hp / (float)member.maxHp);
                valid++;
            }

            if (valid == 0)
            {
                return 100;
            }

            return Mathf.RoundToInt((sum / valid) * 100f);
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
            label.raycastTarget = false;
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
            if (!EnsureMarkersRoot())
            {
                return;
            }

            markersRoot.SetAsLastSibling();
            Debug.Log($"[MapController] markersRoot path={GetHierarchyPath(markersRoot)}");

            if (regionMarkerPrefab == null)
            {
                regionMarkerPrefab = CreateRuntimeRegionMarkerTemplate();
                if (regionMarkerPrefab == null)
                {
                    Debug.LogError("[MapController] regionMarkerPrefab is not assigned and runtime fallback creation failed. Marker spawn skipped.");
                    return;
                }
            }

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
                Debug.Log($"[MapController] Marker spawned: regionId={region.id}, markerPath={GetHierarchyPath(marker.transform)}, parent={GetHierarchyPath(marker.transform.parent)}");
            }

            Debug.Log($"[MapController] Markers spawned total={_markersByRegion.Count}");
        }

        private bool EnsureMarkersRoot()
        {
            if (markersRoot != null)
            {
                return true;
            }

            var parent = FindMarkersParent();
            if (parent == null)
            {
                Debug.LogWarning("[MapController] markersRoot is not assigned and UI parent was not found yet. Marker spawn deferred.");
                return false;
            }

            var fallback = new GameObject("MarkersRoot", typeof(RectTransform));
            markersRoot = fallback.GetComponent<RectTransform>();
            markersRoot.SetParent(parent, false);
            markersRoot.anchorMin = Vector2.zero;
            markersRoot.anchorMax = Vector2.one;
            markersRoot.offsetMin = Vector2.zero;
            markersRoot.offsetMax = Vector2.zero;
            markersRoot.localScale = Vector3.one;
            markersRoot.localPosition = Vector3.zero;
            markersRoot.SetAsLastSibling();
            Debug.Log($"[MapController] Created fallback markersRoot under {GetHierarchyPath(parent)}");
            Debug.Log($"[MapController] markersRoot parent path={GetHierarchyPath(markersRoot.parent)}");
            return true;
        }

        private RectTransform FindMarkersParent()
        {
            if (markersRootParent != null)
            {
                return markersRootParent;
            }

            if (mapScrollRect != null && mapScrollRect.content != null)
            {
                Debug.Log($"[MapController] Found map scroll rect: {GetHierarchyPath(mapScrollRect.transform)}");
                return mapScrollRect.content;
            }

            var scrollRects = GetComponentsInChildren<ScrollRect>(true);
            for (var i = 0; i < scrollRects.Length; i++)
            {
                var candidate = scrollRects[i];
                if (candidate == null)
                {
                    continue;
                }

                var candidatePath = GetHierarchyPath(candidate.transform);
                var isOverlay = candidatePath.IndexOf("OverlayLayer", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidatePath.IndexOf("RegionDetails", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidatePath.IndexOf("Contracts", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isOverlay)
                {
                    Debug.Log($"[MapController] Ignored overlay scroll rect: {candidatePath}");
                    continue;
                }

                var isMapScroll = candidate.name.IndexOf("MapScrollRect", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidatePath.IndexOf("MapScrollRect", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidatePath.IndexOf("MapLayer", StringComparison.OrdinalIgnoreCase) >= 0
                    || candidatePath.IndexOf("MapLayout", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isMapScroll)
                {
                    continue;
                }

                if (candidate.content != null)
                {
                    Debug.Log($"[MapController] Found map scroll rect: {candidatePath}");
                    mapScrollRect = candidate;
                    return candidate.content;
                }
            }

            var namedMapScrollRect = transform.Find("MapCanvas/MapLayer/MapScrollRect") as RectTransform
                ?? transform.Find("MapCanvas/MapLayout/MapScrollRect") as RectTransform;
            if (namedMapScrollRect != null)
            {
                mapScrollRect = namedMapScrollRect.GetComponent<ScrollRect>();
                if (mapScrollRect != null && mapScrollRect.content != null)
                {
                    Debug.Log($"[MapController] Found map scroll rect: {GetHierarchyPath(mapScrollRect.transform)}");
                    return mapScrollRect.content;
                }
            }

            return null;
        }

        private static string GetHierarchyPath(Transform node)
        {
            if (node == null)
            {
                return "<null>";
            }

            var path = node.name;
            var cursor = node.parent;
            while (cursor != null)
            {
                path = $"{cursor.name}/{path}";
                cursor = cursor.parent;
            }

            return path;
        }

        private void ResolveRuntimeReferences()
        {
            if (mapRect == null)
            {
                var mapImage = transform.Find("MapCanvas/MapLayer/MapScrollRect/Viewport/Content/MapImage") as RectTransform;
                if (mapImage != null)
                {
                    mapRect = mapImage;
                }
            }

            if (mapScrollRect == null)
            {
                var mapScrollRectTransform = transform.Find("MapCanvas/MapLayer/MapScrollRect")
                    ?? transform.Find("MapCanvas/MapLayout/MapScrollRect");
                if (mapScrollRectTransform != null)
                {
                    mapScrollRect = mapScrollRectTransform.GetComponent<ScrollRect>();
                }
            }

            if (markersRootParent == null && mapScrollRect != null)
            {
                markersRootParent = mapScrollRect.content;
            }

            if (markersRoot == null)
            {
                var root = transform.Find("MapCanvas/MapLayer/MapScrollRect/Viewport/Content/MapImage/MarkersRoot") as RectTransform;
                if (root != null)
                {
                    markersRoot = root;
                }
            }

            if (contractIconsRoot == null)
            {
                var root = transform.Find("MapCanvas/MapLayer/MapScrollRect/Viewport/Content/MapImage/ContractIconsRoot") as RectTransform;
                if (root != null)
                {
                    contractIconsRoot = root;
                }
            }

            if (travelTokensRoot == null)
            {
                var root = transform.Find("MapCanvas/MapLayer/MapScrollRect/Viewport/Content/MapImage/TravelTokensRoot") as RectTransform;
                if (root != null)
                {
                    travelTokensRoot = root;
                }
            }
        }

        private RegionMarker CreateRuntimeRegionMarkerTemplate()
        {
            var template = new GameObject("RuntimeRegionMarkerTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HorizontalLayoutGroup), typeof(RegionMarker));
            template.SetActive(false);
            template.transform.SetParent(transform, false);

            var layout = template.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rootImage = template.GetComponent<Image>();
            rootImage.color = new Color(0.65f, 0.15f, 0.15f, 0.85f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(template.transform, false);
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            var iconLayout = iconGo.GetComponent<LayoutElement>();
            iconLayout.preferredWidth = 36f;
            iconLayout.preferredHeight = 36f;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            labelGo.transform.SetParent(template.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Region";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white;
            var labelLayout = labelGo.GetComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;

            var rect = template.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 48f);

            return template.GetComponent<RegionMarker>();
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

            var soloHunters = hunterRoster != null ? hunterRoster.GetSoloHunters() : new List<HunterData>();
            squadSelectPanel.Show(idle, soloHunters, option =>
            {
                if (option.isSolo)
                {
                    var soloHunter = hunterRoster != null ? hunterRoster.GetById(option.hunterId) : null;
                    if (soloHunter == null)
                    {
                        return;
                    }

                    if (soloHunter.exhaustedToday)
                    {
                        Debug.LogWarning("[Assign] blocked exhausted solo hunter [TODO REMOVE]");
                        return;
                    }

                    if (!contract.allowSolo)
                    {
                        Debug.LogWarning("[Assign] blocked: contract forbids solo [TODO REMOVE]");
                        return;
                    }

                    if (soloHunter.rank < contract.minRank)
                    {
                        Debug.LogWarning("[Assign] blocked: solo rank too low [TODO REMOVE]");
                        return;
                    }

                    StartSoloTravelTask(soloHunter, region.id, contract.id, contract.reward, TravelPhase.Outbound);
                    detailsPanel?.BlockContract(contract.id);
                    return;
                }

                var squad = FindSquad(option.squadId);
                if (squad == null)
                {
                    return;
                }

                if (squad.exhausted)
                {
                    Debug.LogWarning($"[Assign] blocked exhausted squad={squad.id} day={_dayIndex} [TODO REMOVE]");
                    return;
                }

                if (!contract.allowSquad)
                {
                    Debug.LogWarning("[Assign] blocked: contract forbids squad [TODO REMOVE]");
                    return;
                }

                if (GetSquadEffectiveRank(squad) < contract.minRank)
                {
                    Debug.LogWarning("[Assign] blocked: squad rank too low [TODO REMOVE]");
                    return;
                }

                StartTravelTask(squad, GuildHqId, region.id, contract.id, contract.reward, TravelPhase.Outbound);
                squad.state = SquadState.TravelingToRegion;
                NotifyRosterChanged();

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
            var now = SimulationTime.NowSeconds;

            var task = new TravelTask
            {
                squadId = squad.id,
                fromRegionId = fromRegionId,
                toRegionId = toRegionId,
                contractId = contractId,
                contractReward = contractReward,
                phase = phase,
                startSimSeconds = now,
                endSimSeconds = now + duration
            };

            RemoveExistingTaskForSquad(squad.id);
            _travelTasks.Add(task);
            AcquireOrUpdateTravelToken(squad, task);

            if (DEBUG_TRAVEL)
            {
                var phaseLabel = phase == TravelPhase.Outbound ? "OUT" : "RET";
                Debug.Log($"[TravelDebug] Travel task created: phase={phaseLabel}, squad={squad.name}, route={fromRegionId}->{toRegionId}, endSimSeconds={task.endSimSeconds}");
            }
        }

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
                var remaining = Mathf.Max(0, (int)(task.endSimSeconds - SimulationTime.NowSeconds));
                token.UpdateView(fromMarker.AnchoredPosition, ToTimerText(remaining));
            }
        }

        private void RemoveTravelToken(string squadId)
        {
            if (string.IsNullOrEmpty(squadId))
            {
                return;
            }

            if (_travelTokenBySquadId.TryGetValue(squadId, out var token))
            {
                _travelTokenPool?.Return(token);
                _travelTokenBySquadId.Remove(squadId);
            }
        }
        private void AcquireOrUpdateSoloTravelToken(HunterData hunter, TravelTask task)
        {
            if (_travelTokenPool == null || hunter == null || string.IsNullOrEmpty(hunter.id))
            {
                return;
            }

            if (!_travelTokenBySoloHunterId.TryGetValue(hunter.id, out var token) || token == null)
            {
                token = _travelTokenPool.Get();
                token.transform.SetParent(travelTokensRoot, false);
                _travelTokenBySoloHunterId[hunter.id] = token;
            }

            token.Bind($"solo_{hunter.id}", $"Solo {hunter.name}");
            if (_markersByRegion.TryGetValue(task.fromRegionId, out var fromMarker) && fromMarker != null)
            {
                var remaining = Mathf.Max(0, (int)(task.endSimSeconds - SimulationTime.NowSeconds));
                token.UpdateView(fromMarker.AnchoredPosition, ToTimerText(remaining));
            }
        }

        private void RemoveSoloTravelToken(string hunterId)
        {
            if (string.IsNullOrEmpty(hunterId))
            {
                return;
            }

            if (_travelTokenBySoloHunterId.TryGetValue(hunterId, out var token))
            {
                _travelTokenPool?.Return(token);
                _travelTokenBySoloHunterId.Remove(hunterId);
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
                        iconKey = PickContractIconKey(i),
                        minRank = i % 3 == 0 ? HunterRank.C : HunterRank.D,
                        allowSquad = i % 5 != 0,
                        allowSolo = true
                    });
                }

                _contractsByRegion[region.id] = contracts;
            }
        }

        private void EnsureHunterRoster()
        {
            if (hunterRoster == null)
            {
                hunterRoster = FindFirstObjectByType<HunterRoster>();
                if (hunterRoster == null)
                {
                    hunterRoster = gameObject.AddComponent<HunterRoster>();
                }
            }

            hunterRoster.EnsureSeededDefaultHunters(_dayIndex);
        }

        private void EnsureSquadRoster()
        {
            if (squadRoster == null)
            {
                squadRoster = FindFirstObjectByType<SquadRoster>();
                if (squadRoster == null)
                {
                    squadRoster = gameObject.AddComponent<SquadRoster>();
                }
            }

            squadRoster.SeedDefaultSquadsIfEmpty();
        }

        private List<SquadData> GetRosterSquads()
        {
            return squadRoster != null ? squadRoster.GetSquads() : new List<SquadData>();
        }

        private void NotifyRosterChanged()
        {
            squadRoster?.NotifyChanged();
        }

        private void ApplyRosterChangedPenalty(SquadData squad)
        {
            if (squad == null)
            {
                return;
            }

            squad.lastRosterChangeDay = _dayIndex;
            squad.cohesion = Mathf.Max(0, squad.cohesion - 8);
            Debug.Log($"[Cohesion] Roster changed squad={squad.id} cohesion={squad.cohesion} day={_dayIndex} [TODO REMOVE]");
        }

        private List<SquadData> GetIdleSquads()
        {
            var squads = GetRosterSquads();
            var list = new List<SquadData>();
            for (var i = 0; i < squads.Count; i++)
            {
                if (squads[i].state == SquadState.IdleAtHQ && !squads[i].IsDestroyed)
                {
                    list.Add(squads[i]);
                }
            }

            return list;
        }

        private HunterData FindHunter(string hunterId)
        {
            return hunterRoster != null ? hunterRoster.GetById(hunterId) : null;
        }

        private SquadData FindSquad(string squadId)
        {
            var squads = GetRosterSquads();
            for (var i = 0; i < squads.Count; i++)
            {
                if (squads[i].id == squadId)
                {
                    return squads[i];
                }
            }

            return null;
        }

        private void AddGold(int amount)
        {
            gameState?.AddGold(amount);

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

            squad.state = SquadState.Destroyed;
            squad.currentRegionId = GuildHqId;
            squad.hp = 0;
            NotifyRosterChanged();
            Debug.Log($"[TravelDebug] Squad destroyed: {squad.name}");
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
            var now = SimulationTime.NowSeconds;
            if (squadStatusHud != null)
            {
                squadStatusHud.Sync(GetRosterSquads(), _travelTasks, ResolveRegionName, now);
            }

            squadDetailsPanel?.Refresh();
        }

        public IReadOnlyList<SquadData> GetSquads()
        {
            return GetRosterSquads();
        }

        public IReadOnlyList<TravelTask> GetTravelTasks()
        {
            return _travelTasks;
        }

        public int GetCurrentDayIndex()
        {
            return _dayIndex;
        }

        public int GetNewRecruitsCount(SquadData squad)
        {
            if (squad == null || squad.members == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < squad.members.Count; i++)
            {
                var member = squad.members[i];
                if (member != null && member.joinedDay == _dayIndex)
                {
                    count++;
                }
            }

            return count;
        }

        public int GetSquadCohesionModifier(SquadData squad, int dayIndex)
        {
            if (squad == null)
            {
                return 0;
            }

            var modFromCohesion = Mathf.RoundToInt((squad.cohesion - 50) / 5f);
            var newbieCount = 0;
            if (squad.members != null)
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

            var modFromNewbies = Mathf.Max(-15, -5 * newbieCount);
            return Mathf.Clamp(modFromCohesion + modFromNewbies, -25, 15);
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

        private void EnsureSelectedSquad()
        {
            if (!string.IsNullOrWhiteSpace(_selectedSquadId))
            {
                return;
            }

            var squads = GetRosterSquads();
            if (squads != null && squads.Count > 0)
            {
                _selectedSquadId = squads[0].id;
            }
        }

        public void SetSelectedSquad(string squadId)
        {
            if (string.IsNullOrWhiteSpace(squadId) || _selectedSquadId == squadId)
            {
                return;
            }

            _selectedSquadId = squadId;
            squadDetailsPanel?.Refresh();
            RefreshSquadStatusHud();
            UpdateEndDayUiState();
        }

        public string GetSelectedSquadId()
        {
            EnsureSelectedSquad();
            return _selectedSquadId;
        }

        public SquadData GetSelectedSquad()
        {
            EnsureSelectedSquad();
            return FindSquad(_selectedSquadId);
        }

        public TravelTask GetTravelTaskForSquad(string squadId)
        {
            return FindTaskForSquad(squadId);
        }

        public IReadOnlyList<HunterData> GetSoloHunters()
        {
            return hunterRoster != null ? hunterRoster.GetSoloHunters() : new List<HunterData>();
        }

        public TravelTask GetTravelTaskForSoloHunter(string hunterId)
        {
            if (string.IsNullOrEmpty(hunterId))
            {
                return null;
            }

            for (var i = 0; i < _travelTasks.Count; i++)
            {
                var task = _travelTasks[i];
                if (task != null && task.soloHunterId == hunterId)
                {
                    return task;
                }
            }

            return null;
        }

        public string GetRegionNameById(string regionId)
        {
            return ResolveRegionName(regionId);
        }

        private HunterRank GetSquadEffectiveRank(SquadData squad)
        {
            if (squad == null)
            {
                return HunterRank.E;
            }

            var minRank = HunterRank.S;
            var hasAny = false;
            if (hunterRoster != null && squad.hunterIds != null)
            {
                for (var i = 0; i < squad.hunterIds.Count; i++)
                {
                    var hunter = hunterRoster.GetById(squad.hunterIds[i]);
                    if (hunter == null)
                    {
                        continue;
                    }

                    hasAny = true;
                    if (hunter.rank < minRank)
                    {
                        minRank = hunter.rank;
                    }
                }
            }

            return hasAny ? minRank : HunterRank.E;
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
