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

        private ContractData _contract;

        public ContractData Contract => _contract;

        public void Bind(ContractData contract)
        {
            _contract = contract;
            Refresh();
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
    }
}
