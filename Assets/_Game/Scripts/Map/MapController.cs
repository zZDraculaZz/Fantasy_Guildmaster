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
        [SerializeField] private RegionMarker regionMarkerPrefab;
        [SerializeField] private RegionDetailsPanel detailsPanel;
        [SerializeField] private GameClock gameClock;

        private readonly Dictionary<string, List<ContractData>> _contractsByRegion = new();
        private readonly List<RegionMarker> _spawnedMarkers = new();
        private GameData _gameData;

        private void Awake()
        {
            _gameData = GameDataLoader.Load();
            SeedContracts();
            SpawnMarkers();

            if (_gameData.regions.Count > 0)
            {
                SelectRegion(_gameData.regions[0]);
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

            detailsPanel.TickContracts();
        }

        private void SpawnMarkers()
        {
            foreach (var marker in _spawnedMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker.gameObject);
                }
            }

            _spawnedMarkers.Clear();

            foreach (var region in _gameData.regions)
            {
                var marker = Instantiate(regionMarkerPrefab, markersRoot);
                marker.name = $"RegionMarker_{region.id}";
                marker.Setup(region, mapRect, SelectRegion);
                _spawnedMarkers.Add(marker);
            }
        }

        private void SelectRegion(RegionData region)
        {
            if (!_contractsByRegion.TryGetValue(region.id, out var contracts))
            {
                contracts = new List<ContractData>();
            }

            detailsPanel.Show(region, contracts);
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
    }
}
