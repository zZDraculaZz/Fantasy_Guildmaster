using FantasyGuildmaster.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.Map
{
    public sealed class TravelToken : MonoBehaviour
    {
        private const string TravelTokenPath = "Icons/UI/travel_token";
        private const string FallbackPath = "Icons/UI/squad_token";

        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text squadNameText;

        private RectTransform _rectTransform;

        public string SquadId { get; private set; }

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            if (iconImage != null)
            {
                iconImage.sprite = SpriteLoader.TryLoadSprite(TravelTokenPath, FallbackPath);
            }
        }

        public void Bind(string squadId, string squadName)
        {
            SquadId = squadId;
            if (squadNameText != null)
            {
                squadNameText.text = squadName;
            }
        }

        public void UpdateView(Vector2 position, string remaining)
        {
            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            _rectTransform.anchoredPosition = position;

            if (timerText != null)
            {
                timerText.text = remaining;
            }
        }
    }
}
