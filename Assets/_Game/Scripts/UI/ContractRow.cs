using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class ContractRow : MonoBehaviour
    {
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
