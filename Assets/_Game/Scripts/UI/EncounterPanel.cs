using System;
using System.Collections.Generic;
using FantasyGuildmaster.Encounter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class EncounterPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private RectTransform optionsRoot;
        [SerializeField] private Button optionButtonPrefab;
        [SerializeField] private Button continueButton;

        private readonly List<Button> _optionButtons = new();
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }



        public void ConfigureRuntimeBindings(TMP_Text runtimeTitleText, TMP_Text runtimeDescriptionText, RectTransform runtimeOptionsRoot, Button runtimeOptionButtonPrefab, Button runtimeContinueButton)
        {
            titleText = runtimeTitleText;
            descriptionText = runtimeDescriptionText;
            optionsRoot = runtimeOptionsRoot;
            optionButtonPrefab = runtimeOptionButtonPrefab;
            continueButton = runtimeContinueButton;
        }


        private void EnsureVisibleAndInteractiveState()
        {
            gameObject.SetActive(true);

            if (transform is RectTransform rootRect)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            EnsureParentCanvas();

            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            transform.SetAsLastSibling();

            Debug.Log($"[EncounterDebug] Panel active={gameObject.activeSelf}, alpha={_canvasGroup.alpha}, siblingIndex={transform.GetSiblingIndex()}, parent={(transform.parent != null ? transform.parent.name : "none")}");
        }

        private void EnsureParentCanvas()
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                var canvasGo = new GameObject("EncounterCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;

                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                transform.SetParent(canvasGo.transform, false);
                return;
            }

            var raycaster = parentCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                parentCanvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            var overlayCanvas = GetComponent<Canvas>();
            if (overlayCanvas != null && overlayCanvas.overrideSorting)
            {
                overlayCanvas.sortingOrder = 999;
            }
        }

        public void ShowEncounter(EncounterData encounter, Action<EncounterOption> onOptionSelected)
        {
            EnsureVisibleAndInteractiveState();

            if (titleText != null)
            {
                titleText.text = encounter.title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = encounter.description;
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
                continueButton.onClick.RemoveAllListeners();
            }

            RebuildOptions(encounter.options, onOptionSelected);
        }

        public void ShowResult(string resultText, Action onContinue)
        {
            EnsureVisibleAndInteractiveState();

            if (descriptionText != null)
            {
                descriptionText.text = resultText;
            }

            ClearOptionButtons();

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(() =>
                {
                    gameObject.SetActive(false);
                    onContinue?.Invoke();
                });
            }
        }

        private void RebuildOptions(List<EncounterOption> options, Action<EncounterOption> onOptionSelected)
        {
            ClearOptionButtons();

            if (optionButtonPrefab == null || optionsRoot == null || options == null)
            {
                return;
            }

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = Instantiate(optionButtonPrefab, optionsRoot);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = option.text;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onOptionSelected?.Invoke(option));
                _optionButtons.Add(button);
            }
        }

        private void ClearOptionButtons()
        {
            for (var i = 0; i < _optionButtons.Count; i++)
            {
                if (_optionButtons[i] != null)
                {
                    Destroy(_optionButtons[i].gameObject);
                }
            }

            _optionButtons.Clear();
        }
    }
}
