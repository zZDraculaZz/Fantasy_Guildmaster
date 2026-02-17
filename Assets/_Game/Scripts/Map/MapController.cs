using System;
using System.Collections.Generic;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.UI;
using UnityEngine;

namespace FantasyGuildmaster.Map
{
    public sealed class MapController : MonoBehaviour
    {
        [SerializeField] private RectTransform mapRect;
        [SerializeField] private RectTransform markersRoot;
        [SerializeField] private RectTransform contractIconsRoot;
        [SerializeField] private RectTransform travelTokensRoot;
        [SerializeField] private RegionMarker regionMarkerPrefab;
        [SerializeField] private ContractIcon contractIconPrefab;
        [SerializeField] private TravelToken travelTokenPrefab;
        [SerializeField] private RegionDetailsPanel detailsPanel;
        [SerializeField] private SquadSelectPanel squadSelectPanel;
        [SerializeField] private GameClock gameClock;

        private readonly Dictionary<string, List<ContractData>> _contractsByRegion = new();
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
            SeedContracts();
            SeedSquads();
            SpawnMarkers();
            InitializePools();
            SyncAllContractIcons();
            if (detailsPanel != null)
            {
                detailsPanel.AssignSquadRequested += OnAssignSquadRequested;
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }

            if (squadSelectPanel != null)
            {
                squadSelectPanel.Hide();
            }

            if (_gameData.regions.Count > 0)
            {
                SelectRegion(_gameData.regions[0]);
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
            foreach (var contracts in _contractsByRegion.Values)
            {
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

                CompleteTravelTask(task);
                _travelTasks.RemoveAt(i);
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
            foreach (var marker in _markersByRegion.Values)
            {
                if (marker != null)
                {
                    Destroy(marker.gameObject);
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
            if (squadSelectPanel == null || region == null || contract == null)
            {
                return;
            }

            var idle = GetIdleSquads();
            if (idle.Count == 0)
            {
                return;
            }

            squadSelectPanel.Show(idle, squad => StartTravelTask(squad, region, contract));
        }

        private void StartTravelTask(SquadData squad, RegionData destination, ContractData contract)
        {
            if (squad == null || destination == null || contract == null)
            {
                return;
            }

            if (squad.status != SquadStatus.Idle)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var duration = Mathf.Max(1, destination.travelDays * 10);

            var task = new TravelTask
            {
                squadId = squad.id,
                fromRegionId = squad.currentRegionId,
                toRegionId = destination.id,
                contractId = contract.id,
                startUnix = now,
                endUnix = now + duration
            };

            squad.status = SquadStatus.Traveling;
            _travelTasks.Add(task);
            AcquireTravelToken(squad, task);
            if (detailsPanel != null)
            {
                detailsPanel.SetIdleSquadsCount(GetIdleSquads().Count);
            }
        }

        private void AcquireTravelToken(SquadData squad, TravelTask task)
        {
            if (_travelTokenPool == null)
            {
                return;
            }

            if (_travelTokenBySquadId.TryGetValue(task.squadId, out var existing) && existing != null)
            {
                return;
            }

            var token = _travelTokenPool.Get();
            token.transform.SetParent(travelTokensRoot, false);
            token.Bind(squad.id, squad.name);
            _travelTokenBySquadId[squad.id] = token;
        }

        private void CompleteTravelTask(TravelTask task)
        {
            var squad = FindSquad(task.squadId);
            if (squad != null)
            {
                squad.status = SquadStatus.Idle;
                squad.currentRegionId = task.toRegionId;
                Debug.Log($"Squad arrived: {squad.name}");
            }

            if (_travelTokenBySquadId.TryGetValue(task.squadId, out var token))
            {
                _travelTokenPool?.Return(token);
                _travelTokenBySquadId.Remove(task.squadId);
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
            _squads.Add(new SquadData { id = "squad_iron_hawks", name = "Iron Hawks", membersCount = 6, status = SquadStatus.Idle, currentRegionId = _gameData.regions.Count > 0 ? _gameData.regions[0].id : string.Empty });
            _squads.Add(new SquadData { id = "squad_ash_blades", name = "Ash Blades", membersCount = 5, status = SquadStatus.Idle, currentRegionId = _gameData.regions.Count > 1 ? _gameData.regions[1].id : (_gameData.regions.Count > 0 ? _gameData.regions[0].id : string.Empty) });
            _squads.Add(new SquadData { id = "squad_grim_lantern", name = "Grim Lantern", membersCount = 4, status = SquadStatus.Idle, currentRegionId = _gameData.regions.Count > 2 ? _gameData.regions[2].id : (_gameData.regions.Count > 0 ? _gameData.regions[0].id : string.Empty) });
        }

        private List<SquadData> GetIdleSquads()
        {
            var list = new List<SquadData>();
            for (var i = 0; i < _squads.Count; i++)
            {
                if (_squads[i].status == SquadStatus.Idle)
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

        private static string ToTimerText(int remainingSeconds)
        {
            var clamped = Mathf.Max(0, remainingSeconds);
            var minutes = clamped / 60;
            var seconds = clamped % 60;
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
