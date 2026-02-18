using System;
using System.Collections.Generic;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class RegionDetailsPanel : MonoBehaviour
    {
        private const string UiTravelTokenPath = "Icons/UI/travel_token";
        private const string UiFallbackPath = "Icons/UI/squad_token";

        [SerializeField] private TMP_Text regionNameText;
        [SerializeField] private TMP_Text dangerText;
        [SerializeField] private TMP_Text factionText;
        [SerializeField] private TMP_Text travelDaysText;
        [SerializeField] private TMP_Text threatsText;
        [SerializeField] private Image travelIconImage;
        [SerializeField] private RectTransform contractsRoot;
        [SerializeField] private ContractRow contractRowPrefab;
        [SerializeField] private Button assignSquadButton;

        private readonly List<ContractRow> _rows = new();
        private List<ContractData> _contracts;
        private RegionData _region;
        private ContractData _selectedContract;
        private int _idleSquadsCount;
        private readonly HashSet<string> _blockedContractIds = new();

        public event Action<RegionData, ContractData> AssignSquadRequested;

        public void BlockContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId))
            {
                return;
            }

            _blockedContractIds.Add(contractId);

            if (_selectedContract != null && _selectedContract.id == contractId)
            {
                _selectedContract = GetFirstAvailableContract();
            }

            RebuildContracts();
            UpdateAssignSquadButtonState();
        }

        private void Awake()
        {
            if (assignSquadButton != null)
            {
                assignSquadButton.onClick.RemoveAllListeners();
                assignSquadButton.onClick.AddListener(OnAssignSquadClicked);
            }
        }

        public void Show(RegionData region, List<ContractData> contracts)
        {
            _region = region;
            regionNameText.text = region.name;
            dangerText.text = $"Danger: {region.danger}";
            factionText.text = $"Faction: {region.faction}";
            travelDaysText.text = $"Travel: {region.travelDays} days";
            var threats = region.threats != null ? string.Join(", ", region.threats) : "-";
            threatsText.text = $"Threats: {threats}";

            if (travelIconImage != null)
            {
                travelIconImage.sprite = SpriteLoader.TryLoadSprite(UiTravelTokenPath, UiFallbackPath);
            }

            _contracts = contracts;
            _selectedContract = GetFirstAvailableContract(contracts);
            RebuildContracts();
            UpdateAssignSquadButtonState();
        }

        public void SetIdleSquadsCount(int idleSquadsCount)
        {
            _idleSquadsCount = idleSquadsCount;
            UpdateAssignSquadButtonState();
        }

        public void TickContracts()
        {
            if (_contracts == null)
            {
                return;
            }

            for (var i = _rows.Count - 1; i >= 0; i--)
            {
                var row = _rows[i];
                if (row.Contract == null || row.Contract.IsExpired)
                {
                    Destroy(row.gameObject);
                    _rows.RemoveAt(i);
                    continue;
                }

                row.Refresh();
            }

            if (_selectedContract != null && (_selectedContract.IsExpired || _blockedContractIds.Contains(_selectedContract.id)))
            {
                _selectedContract = GetFirstAvailableContract();
            }

            UpdateAssignSquadButtonState();
        }

        private void RebuildContracts()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                {
                    Destroy(row.gameObject);
                }
            }

            _rows.Clear();

            if (_contracts == null)
            {
                return;
            }

            for (var i = 0; i < _contracts.Count; i++)
            {
                var contract = _contracts[i];
                var row = Instantiate(contractRowPrefab, contractsRoot);
                row.Bind(contract);

                var isBlocked = _blockedContractIds.Contains(contract.id);
                row.SetAssigned(isBlocked);
                row.SetSelected(_selectedContract != null && _selectedContract.id == contract.id);

                var button = row.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isBlocked;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        if (isBlocked)
                        {
                            return;
                        }

                        _selectedContract = contract;
                        RefreshSelectionVisuals();
                        UpdateAssignSquadButtonState();
                    });
                }

                _rows.Add(row);
            }
        }

        private void OnAssignSquadClicked()
        {
            if (_region == null || _selectedContract == null || _blockedContractIds.Contains(_selectedContract.id))
            {
                return;
            }

            AssignSquadRequested?.Invoke(_region, _selectedContract);
        }

        private void UpdateAssignSquadButtonState()
        {
            if (assignSquadButton == null)
            {
                return;
            }

            var hasContract = GetFirstAvailableContract() != null;
            assignSquadButton.interactable = hasContract && _idleSquadsCount > 0;
        }

        private void RefreshSelectionVisuals()
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row == null || row.Contract == null)
                {
                    continue;
                }

                row.SetAssigned(_blockedContractIds.Contains(row.Contract.id));
                row.SetSelected(_selectedContract != null && row.Contract.id == _selectedContract.id);
            }
        }

        private ContractData GetFirstAvailableContract()
        {
            return GetFirstAvailableContract(_contracts);
        }

        private ContractData GetFirstAvailableContract(List<ContractData> contracts)
        {
            if (contracts == null)
            {
                return null;
            }

            for (var i = 0; i < contracts.Count; i++)
            {
                var contract = contracts[i];
                if (contract == null || contract.IsExpired || _blockedContractIds.Contains(contract.id))
                {
                    continue;
                }

                return contract;
            }

            return null;
        }
    }
}
