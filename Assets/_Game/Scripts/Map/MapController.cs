using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.Encounter;
using FantasyGuildmaster.UI;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed class MapController : MonoBehaviour
    {
        private const string GuildHqId = "guild_hq";
        private const string GuildHqName = "Guild HQ";

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

            for (var i = _travelTasks.Count - 1; i >= 0; i--)
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

                if (now < task.endUnix)
                {
                    continue;
                }

                _travelTasks.RemoveAt(i);
                HandleTravelTaskCompleted(task);
            }
        }

        private void HandleTravelTaskCompleted(TravelTask task)
        {
            var squad = FindSquad(task.squadId);
            if (squad == null)
            {
                RemoveTravelToken(task.squadId);
                return;
            }

            if (task.phase == TravelPhase.Outbound)
            {
                squad.currentRegionId = task.toRegionId;
                squad.state = SquadState.ResolvingEncounter;

                if (encounterManager != null)
                {
                    encounterManager.StartEncounter(task.toRegionId, squad.id, () => OnEncounterResolved(squad.id, task.toRegionId, task.contractId, task.contractReward));
                }
                else
                {
                    OnEncounterResolved(squad.id, task.toRegionId, task.contractId, task.contractReward);
                }

                return;
            }

            squad.currentRegionId = GuildHqId;
            squad.state = SquadState.IdleAtHQ;
            AddGold(task.contractReward);
            RemoveTravelToken(task.squadId);
        }

        private void OnEncounterResolved(string squadId, string regionId, string contractId, int contractReward)
        {
            var squad = FindSquad(squadId);
            if (squad == null || squad.hp <= 0)
            {
                RemoveTravelToken(squadId);
                return;
            }

            StartTravelTask(squad, regionId, GuildHqId, contractId, contractReward, TravelPhase.Return);
            squad.state = SquadState.ReturningToHQ;

            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }
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

            squadSelectPanel.Show(idle, squad =>
            {
                StartTravelTask(squad, GuildHqId, region.id, contract.id, contract.reward, TravelPhase.Outbound);
                squad.state = SquadState.TravelingToRegion;

                if (detailsPanel != null)
                {
                    detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
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

            RemoveExistingTaskForSquad(squadId);
            RemoveTravelToken(squadId);
            squad.state = SquadState.IdleAtHQ;
            squad.currentRegionId = GuildHqId;
            squad.hp = 0;
            Debug.Log("Squad destroyed");
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

        private static string ToTimerText(int remainingSeconds)
        {
            var clamped = Mathf.Max(0, remainingSeconds);
            var minutes = clamped / 60;
            var seconds = clamped % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
