using System.Collections.Generic;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class RegionDetailsPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text regionNameText;
        [SerializeField] private TMP_Text dangerText;
        [SerializeField] private TMP_Text factionText;
        [SerializeField] private TMP_Text travelDaysText;
        [SerializeField] private TMP_Text threatsText;
        [SerializeField] private RectTransform contractsRoot;
        [SerializeField] private ContractRow contractRowPrefab;

        private readonly List<ContractRow> _rows = new();
        private List<ContractData> _contracts;

        public void Show(RegionData region, List<ContractData> contracts)
        {
            regionNameText.text = region.name;
            dangerText.text = $"Danger: {region.danger}";
            factionText.text = $"Faction: {region.faction}";
            travelDaysText.text = $"Travel: {region.travelDays} days";
            var threats = region.threats != null ? string.Join(", ", region.threats) : "-";
            threatsText.text = $"Threats: {threats}";

            _contracts = contracts;
            RebuildContracts();
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
                var row = Instantiate(contractRowPrefab, contractsRoot);
                row.Bind(_contracts[i]);
                _rows.Add(row);
            }
        }
    }
}
