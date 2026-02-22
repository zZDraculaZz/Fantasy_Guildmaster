using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class SquadSelectPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private Button squadButtonPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<Button> _buttons = new();
        private bool _pauseHeld;

        public void Show(List<SquadData> idleSquads, Action<SquadData> onSelected)
        {
            gameObject.SetActive(true);
            if (!_pauseHeld)
            {
                GamePauseService.Push("SquadSelect");
                _pauseHeld = true;
            }

            if (titleText != null)
            {
                titleText.text = "Assign Squad";
            }

            ClearButtons();

            for (var i = 0; i < idleSquads.Count; i++)
            {
                var squad = idleSquads[i];
                var button = Instantiate(squadButtonPrefab, listRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    var exhaustedTag = squad.exhausted ? " - Exhausted" : string.Empty;
                    label.text = $"{squad.name} ({squad.membersCount}){exhaustedTag}";
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }

                button.interactable = !squad.exhausted;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (squad.exhausted)
                    {
                        Debug.LogWarning($"[Assign] blocked exhausted squad={squad.id} [TODO REMOVE]");
                        return;
                    }

                    onSelected?.Invoke(squad);
                    Hide();
                });

                _buttons.Add(button);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }

        private void OnDisable()
        {
            if (_pauseHeld)
            {
                GamePauseService.Pop("SquadSelect");
                _pauseHeld = false;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (_pauseHeld)
            {
                GamePauseService.Pop("SquadSelect");
                _pauseHeld = false;
            }
        }

        private void ClearButtons()
        {
            for (var i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                {
                    Destroy(_buttons[i].gameObject);
                }
            }

            _buttons.Clear();
        }
    }
}
