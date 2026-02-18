using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text timerText;

        public void SetData(string squadName, string status, string timer, Color statusColor)
        {
            if (squadNameText != null)
            {
                squadNameText.text = squadName;
            }

            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = statusColor;
            }

            if (timerText != null)
            {
                timerText.text = string.IsNullOrWhiteSpace(timer) ? "—" : timer;
                timerText.color = statusColor;
            }
        }
    }
}
