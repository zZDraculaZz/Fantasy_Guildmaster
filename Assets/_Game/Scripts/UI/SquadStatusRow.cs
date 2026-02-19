using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text statusTimerText;
        [SerializeField] private TMP_Text hpText;

        public void ConfigureRuntime(TMP_Text squadName, TMP_Text statusTimer, TMP_Text hp)
        {
            squadNameText = squadName;
            statusTimerText = statusTimer;
            hpText = hp;
        }

        public void SetData(string squadName, string statusTimer, string hp, Color statusColor)
        {
            if (squadNameText != null)
            {
                squadNameText.text = squadName;
            }

            if (statusTimerText != null)
            {
                statusTimerText.text = statusTimer;
                statusTimerText.color = statusColor;
            }

            if (hpText != null)
            {
                hpText.text = hp;
                hpText.color = statusColor;
            }
        }
    }
}
