using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class ContractRow : MonoBehaviour
    {
        private const string ContractsIconBasePath = "Icons/Contracts/";
        private const string ContractsFallbackPath = "Icons/Contracts/contract_generic";

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text rewardText;

        private static readonly Color NormalBackground = new(0.18f, 0.18f, 0.18f, 0.82f);
        private static readonly Color SelectedBackground = new(0.35f, 0.32f, 0.12f, 0.92f);
        private static readonly Color AssignedBackground = new(0.12f, 0.24f, 0.14f, 0.92f);

        private Image _backgroundImage;
        private bool _isAssigned;

        private ContractData _contract;

        public ContractData Contract => _contract;

        public void Bind(ContractData contract)
        {
            _contract = contract;
            _backgroundImage = GetComponent<Image>();
            _isAssigned = false;
            Refresh();
            ApplyVisualState(false);
        }

        public void SetSelected(bool selected)
        {
            ApplyVisualState(selected);
        }

        public void SetAssigned(bool assigned)
        {
            _isAssigned = assigned;
            ApplyVisualState(false);
        }

        public void Refresh()
        {
            if (_contract == null)
            {
                return;
            }

            if (iconImage != null)
            {
                var path = string.IsNullOrWhiteSpace(_contract.iconKey) ? null : ContractsIconBasePath + _contract.iconKey;
                iconImage.sprite = SpriteLoader.TryLoadSprite(path, ContractsFallbackPath);
            }

            if (titleText != null)
            {
                titleText.text = _contract.title;
            }

            if (timerText != null)
            {
                timerText.text = _contract.RemainingText;
            }

            if (rewardText != null)
            {
                rewardText.text = $"{_contract.reward}g";
            }
        }

        private void ApplyVisualState(bool selected)
        {
            _backgroundImage ??= GetComponent<Image>();
            if (_backgroundImage == null)
            {
                return;
            }

            if (_isAssigned)
            {
                _backgroundImage.color = AssignedBackground;
                return;
            }

            _backgroundImage.color = selected ? SelectedBackground : NormalBackground;
        }
    }
}
