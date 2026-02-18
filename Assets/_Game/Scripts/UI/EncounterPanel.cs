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

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            gameObject.SetActive(false);
        }

        public void ShowEncounter(EncounterData encounter, Action<EncounterOption> onOptionSelected)
        {
            gameObject.SetActive(true);
            titleText.text = encounter.title;
            descriptionText.text = encounter.description;

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
                continueButton.onClick.RemoveAllListeners();
            }

            RebuildOptions(encounter.options, onOptionSelected);
        }

        public void ShowResult(string resultText, Action onContinue)
        {
            descriptionText.text = resultText;
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
