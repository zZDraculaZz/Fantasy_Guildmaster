using System;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.Map
{
    [RequireComponent(typeof(Button))]
    public sealed class RegionMarker : MonoBehaviour
    {
        private const string RegionIconBasePath = "Icons/Regions/";
        private const string RegionFallbackPath = "Icons/Regions/region_fallback";
        private const string GuildHqId = "guild_hq";
        private const string GuildHqIconPath = "Icons/UI/guild_hq";

        [SerializeField] private TMP_Text label;
        [SerializeField] private Image iconImage;

        private RectTransform _rectTransform;
        private RectTransform _mapRect;
        private RegionData _region;
        private Action<RegionData> _onSelected;

        public Vector2 AnchoredPosition => _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }

            if (iconImage == null)
            {
                var iconChild = transform.Find("Icon");
                if (iconChild != null)
                {
                    iconImage = iconChild.GetComponent<Image>();
                }
            }
        }

        public void Setup(RegionData region, RectTransform mapRect, Action<RegionData> onSelected)
        {
            _region = region;
            _mapRect = mapRect;
            _onSelected = onSelected;

            if (label != null)
            {
                label.text = region.name;
            }

            if (iconImage != null)
            {
                if (region.id == GuildHqId)
                {
                    iconImage.sprite = SpriteLoader.TryLoadSprite(GuildHqIconPath, RegionFallbackPath);
                }
                else
                {
                    var path = string.IsNullOrWhiteSpace(region.iconKey) ? null : RegionIconBasePath + region.iconKey;
                    iconImage.sprite = SpriteLoader.TryLoadSprite(path, RegionFallbackPath);
                }
            }

            var button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);

            UpdatePosition();
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdatePosition();
        }

        private void OnClicked()
        {
            _onSelected?.Invoke(_region);
        }

        private void UpdatePosition()
        {
            if (_region == null || _mapRect == null || _rectTransform == null)
            {
                return;
            }

            var normalized = _region.pos.ToVector2();
            var rect = _mapRect.rect;
            var x = (normalized.x - 0.5f) * rect.width;
            var y = (normalized.y - 0.5f) * rect.height;
            _rectTransform.anchoredPosition = new Vector2(x, y);
        }
    }
}
