using System;
using FantasyGuildmaster.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class GuildHallCharacterWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image standeeImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private Image disabledOverlay;
        [SerializeField] private Button button;

        private GuildHallCharacterData _data;
        private Action<GuildHallCharacterData> _onClick;
        private Action<string> _onLockedHint;
        private Vector3 _baseScale = Vector3.one;

        public void Setup(GuildHallCharacterData data, Action<GuildHallCharacterData> onClick, Action<string> onLockedHint)
        {
            EnsureBindings();
            _data = data;
            _onClick = onClick;
            _onLockedHint = onLockedHint;
            _baseScale = transform.localScale;

            if (nameText != null)
            {
                var nameRect = nameText.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 0f);
                nameRect.pivot = new Vector2(0.5f, 0f);
                nameRect.offsetMin = new Vector2(8f, 8f);
                nameRect.offsetMax = new Vector2(-8f, 38f);

                nameText.text = string.IsNullOrWhiteSpace(data?.displayName) ? data?.id : data.displayName;
                nameText.raycastTarget = false;
                nameText.enableAutoSizing = true;
                nameText.fontSizeMax = 20f;
                nameText.fontSizeMin = 12f;
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
                nameText.overflowMode = TextOverflowModes.Ellipsis;
                nameText.alignment = TextAlignmentOptions.Center;
                nameText.ForceMeshUpdate();
            }

            if (badgeText != null)
            {
                var badgeRect = badgeText.rectTransform;
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(0f, 1f);
                badgeRect.pivot = new Vector2(0f, 1f);
                badgeRect.sizeDelta = new Vector2(32f, 32f);
                badgeRect.anchoredPosition = new Vector2(6f, -6f);

                badgeText.raycastTarget = false;
                badgeText.enableAutoSizing = false;
                badgeText.fontSize = 24f;
                badgeText.textWrappingMode = TextWrappingModes.NoWrap;
                badgeText.overflowMode = TextOverflowModes.Overflow;
                badgeText.alignment = TextAlignmentOptions.Center;
                badgeText.text = BadgeToSymbol(data?.badge);
                badgeText.gameObject.SetActive(!string.IsNullOrEmpty(badgeText.text));
            }

            if (standeeImage != null)
            {
                standeeImage.color = data != null && data.enabled
                    ? new Color(0.66f, 0.66f, 0.74f, 0.92f)
                    : new Color(0.35f, 0.35f, 0.4f, 0.75f);
            }

            if (disabledOverlay != null)
            {
                disabledOverlay.gameObject.SetActive(data != null && !data.enabled);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                button.onClick.AddListener(OnClicked);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _baseScale * 1.03f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }

        private void OnClicked()
        {
            if (_data == null)
            {
                return;
            }

            if (_data.enabled)
            {
                _onClick?.Invoke(_data);
                return;
            }

            var reason = string.IsNullOrWhiteSpace(_data.lockedReason) ? "Not available right now." : _data.lockedReason;
            _onLockedHint?.Invoke(reason);
        }

        private void EnsureBindings()
        {
            if (standeeImage == null)
            {
                standeeImage = GetComponent<Image>();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (nameText == null)
            {
                nameText = transform.Find("Name")?.GetComponent<TMP_Text>();
            }

            if (badgeText == null)
            {
                badgeText = transform.Find("Badge")?.GetComponent<TMP_Text>();
            }

            if (disabledOverlay == null)
            {
                disabledOverlay = transform.Find("DisabledOverlay")?.GetComponent<Image>();
            }
        }

        private static string BadgeToSymbol(string badge)
        {
            return badge switch
            {
                "important" => "!",
                "normal" => "…",
                "shop" => "$",
                _ => string.Empty
            };
        }
    }
}
