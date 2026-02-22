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
        private Func<ContractData, bool> _hasAnyEligibleParty;
        private Func<ContractData, string> _formatContractReq;
        private Func<ContractData, string> _formatNoEligibleReason;
        private readonly HashSet<string> _blockedContractIds = new();

        public event Action<RegionData, ContractData> AssignSquadRequested;

        public void ConfigureEligibilityResolvers(Func<ContractData, bool> hasAnyEligibleParty, Func<ContractData, string> formatContractReq, Func<ContractData, string> formatNoEligibleReason)
        {
            _hasAnyEligibleParty = hasAnyEligibleParty;
            _formatContractReq = formatContractReq;
            _formatNoEligibleReason = formatNoEligibleReason;
            RefreshSelectionVisuals();
            UpdateAssignSquadButtonState();
        }


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


        public void UnblockContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId))
            {
                return;
            }

            _blockedContractIds.Remove(contractId);

            if (_selectedContract == null)
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
                var hasEligibleParty = _hasAnyEligibleParty == null || _hasAnyEligibleParty(contract);
                row.SetAssigned(isBlocked);
                row.SetSelected(_selectedContract != null && _selectedContract.id == contract.id);
                row.SetRequirements(_formatContractReq != null ? _formatContractReq(contract) : ContractUiText.FormatContractReq(contract));
                row.SetUnavailableReason(!isBlocked && !hasEligibleParty ? (_formatNoEligibleReason != null ? _formatNoEligibleReason(contract) : "No eligible party") : null);

                var button = row.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isBlocked && hasEligibleParty;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() =>
                    {
                        if (isBlocked || !hasEligibleParty)
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
            if (_region == null || _selectedContract == null || _blockedContractIds.Contains(_selectedContract.id) || (_hasAnyEligibleParty != null && !_hasAnyEligibleParty(_selectedContract)))
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

            var selectedEligible = _selectedContract != null && (_hasAnyEligibleParty == null || _hasAnyEligibleParty(_selectedContract));
            var hasContract = GetFirstAvailableContract() != null;
            assignSquadButton.interactable = hasContract && _idleSquadsCount > 0 && selectedEligible;
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

                var isBlocked = _blockedContractIds.Contains(row.Contract.id);
                var hasEligibleParty = _hasAnyEligibleParty == null || _hasAnyEligibleParty(row.Contract);
                row.SetAssigned(isBlocked);
                row.SetSelected(_selectedContract != null && row.Contract.id == _selectedContract.id);
                row.SetRequirements(_formatContractReq != null ? _formatContractReq(row.Contract) : ContractUiText.FormatContractReq(row.Contract));
                row.SetUnavailableReason(!isBlocked && !hasEligibleParty ? (_formatNoEligibleReason != null ? _formatNoEligibleReason(row.Contract) : "No eligible party") : null);
                var button = row.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = !isBlocked && hasEligibleParty;
                }
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
