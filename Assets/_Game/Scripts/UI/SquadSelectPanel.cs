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
        public struct AssignmentOption
        {
            public bool isSolo;
            public string squadId;
            public string hunterId;
        }

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private Button squadButtonPrefab;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private TMP_Text requirementText;

        private readonly List<Button> _buttons = new();
        private bool _pauseHeld;

        public void Show(List<SquadData> idleSquads, List<HunterData> soloHunters, string requirementSummary, Action<AssignmentOption> onSelected)
        {
            if (errorText == null)
            {
                errorText = transform.Find("ErrorText")?.GetComponent<TMP_Text>();
            }

            if (requirementText == null)
            {
                requirementText = transform.Find("RequirementText")?.GetComponent<TMP_Text>();
            }

            gameObject.SetActive(true);
            if (!_pauseHeld)
            {
                GamePauseService.Push("SquadSelect");
                _pauseHeld = true;
            }

            if (titleText != null)
            {
                titleText.text = "Assign Party";
            }

            if (requirementText != null)
            {
                requirementText.gameObject.SetActive(true);
                requirementText.text = string.IsNullOrWhiteSpace(requirementSummary) ? "Requires: BOTH • Rank E" : $"Requires: {requirementSummary}";
                requirementText.textWrappingMode = TextWrappingModes.NoWrap;
                requirementText.overflowMode = TextOverflowModes.Ellipsis;
            }

            ClearError();
            ClearButtons();
            Debug.Log($"[SquadSelect] Show squads={idleSquads.Count} solos={soloHunters.Count} [TODO REMOVE]");

            AddSectionLabel("Squads");
            for (var i = 0; i < idleSquads.Count; i++)
            {
                var squad = idleSquads[i];
                var button = Instantiate(squadButtonPrefab, listRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    var exhaustedTag = squad.exhausted ? " (Resting)" : string.Empty;
                    label.text = $"[Squad] {squad.name} {RankUtil.FormatRank(RankUtil.GetMinRank(squad))}{exhaustedTag}";
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }

                button.interactable = !squad.exhausted;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    ClearError();
                    onSelected?.Invoke(new AssignmentOption { isSolo = false, squadId = squad.id });
                });
                _buttons.Add(button);
            }

            AddSectionLabel("Solo Hunters");
            for (var i = 0; i < soloHunters.Count; i++)
            {
                var hunter = soloHunters[i];
                if (hunter == null) continue;
                var button = Instantiate(squadButtonPrefab, listRoot);
                button.gameObject.SetActive(true);
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    var exhaustedTag = hunter.exhaustedToday ? " (Resting)" : string.Empty;
                    var loneWolfTag = hunter.loneWolf ? " LoneWolf" : string.Empty;
                    label.text = $"[Solo] {hunter.name} {RankUtil.FormatRank(hunter.rank)}{loneWolfTag}{exhaustedTag}";
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                }

                button.interactable = !hunter.exhaustedToday;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    ClearError();
                    onSelected?.Invoke(new AssignmentOption { isSolo = true, hunterId = hunter.id });
                });
                _buttons.Add(button);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }
        }


        public void ShowError(string message)
        {
            if (errorText == null)
            {
                return;
            }

            errorText.gameObject.SetActive(true);
            errorText.text = string.IsNullOrWhiteSpace(message) ? "Invalid selection." : message;
        }

        private void ClearError()
        {
            if (errorText == null)
            {
                return;
            }

            errorText.text = string.Empty;
            errorText.gameObject.SetActive(false);
        }

        private void AddSectionLabel(string text)
        {
            var button = Instantiate(squadButtonPrefab, listRoot);
            button.gameObject.SetActive(true);
            button.interactable = false;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = text;
                label.fontStyle = FontStyles.Bold;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }

            _buttons.Add(button);
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
            ClearError();
            if (requirementText != null)
            {
                requirementText.text = string.Empty;
                requirementText.gameObject.SetActive(false);
            }
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
