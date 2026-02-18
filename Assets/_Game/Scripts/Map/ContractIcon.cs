using FantasyGuildmaster.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.Map
{
    public sealed class ContractIcon : MonoBehaviour
    {
        private const string ContractsIconBasePath = "Icons/Contracts/";
        private const string ContractsFallbackPath = "Icons/Contracts/contract_generic";

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text timerText;

        private ContractData _contract;
        private RectTransform _rectTransform;

        public ContractData Contract => _contract;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        public void Bind(ContractData contract)
        {
            _contract = contract;
            Refresh();
        }

        public void SetAnchoredPosition(Vector2 position)
        {
            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            _rectTransform.anchoredPosition = position;
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

            if (timerText != null)
            {
                timerText.text = _contract.RemainingText;
            }
        }
    }
}
