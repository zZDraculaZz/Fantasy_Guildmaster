using TMPro;
using UnityEngine;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadStatusRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text squadNameText;
        [SerializeField] private TMP_Text statusTimerText;
        [SerializeField] private TMP_Text hpText;

        private bool _loggedBind;

        public void ConfigureRuntime(TMP_Text squadName, TMP_Text statusTimer, TMP_Text hp)
        {
            squadNameText = squadName;
            statusTimerText = statusTimer;
            hpText = hp;
        }

        public void SetData(string squadName, string statusTimer, string hp, Color statusColor)
        {
            EnsureReferences();

            // Force visible text styling at runtime to avoid hidden rows from stale TMP settings.
            EnsureVisibleTextStyle(squadNameText);
            EnsureVisibleTextStyle(statusTimerText);
            EnsureVisibleTextStyle(hpText);

            if (squadNameText != null)
            {
                squadNameText.text = squadName;
                squadNameText.color = Color.white;
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

            if (!_loggedBind)
            {
                Debug.Log($"[HUDDebug] Row bind ok: {squadName}");
                _loggedBind = true;
            }
        }

        private void EnsureReferences()
        {
            if (squadNameText == null)
            {
                squadNameText = FindText("NameText") ?? FindText("SquadName");
            }

            if (statusTimerText == null)
            {
                statusTimerText = FindText("StatusText") ?? FindText("StatusTimer");
            }

            if (hpText == null)
            {
                hpText = FindText("HpText") ?? FindText("Hp");
            }
        }

        private TMP_Text FindText(string name)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != name)
                {
                    continue;
                }

                return transforms[i].GetComponent<TMP_Text>();
            }

            return null;
        }

        private static void EnsureVisibleTextStyle(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.fontSize = text.fontSize <= 0f ? 14f : text.fontSize;
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, 1f);
            text.canvasRenderer.cullTransparentMesh = false;
            text.ForceMeshUpdate();
        }
    }
}
