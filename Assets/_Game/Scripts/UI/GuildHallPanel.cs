using System;
using System.Collections.Generic;
using FantasyGuildmaster.Core;
using FantasyGuildmaster.Data;
using FantasyGuildmaster.Effects;
using FantasyGuildmaster.Map;
using FantasyGuildmaster.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class GuildHallPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image dimmerImage;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform stageLayer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private Button nextDayButton;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private GameObject dialogueBar;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;

        private readonly List<Button> _runtimeButtons = new();
        private CanvasGroup _cg;
        private bool _pauseHeld;
        private GuildHallEveningData _data;
        private EveningSessionState _session;
        private int _dayIndex;
        private int _runSeed;
        private Action _onNextDay;
        private Action<List<ResolvedEffect>, string, string> _applyEffects;
        private Func<List<HunterData>> _getHunters;
        private Func<List<SquadData>> _getSquads;
        private Func<int> _getForcedCount;
        private Func<string> _dequeueForced;
        private Func<string, bool> _consumeDayTriggeredScene;
        private bool _awaitingContinue;

        private void Awake() { EnsureRuntimeBindings(); Hide(); }
        private void OnDisable() { if (_pauseHeld) { GamePauseService.Pop("GuildHall"); _pauseHeld = false; } }


        public void ConfigureRuntimeBindings(
            GameObject rootObject,
            Image dimmer,
            RectTransform content,
            Image background,
            RectTransform stage,
            TMP_Text runtimeTitle,
            TMP_Text runtimeDay,
            TMP_Text runtimeHint,
            Button runtimeNextDay,
            GameObject runtimeDialogueBar,
            TMP_Text runtimeSpeaker,
            TMP_Text runtimeBody,
            Button runtimeNext,
            Button runtimeSkip)
        {
            root = rootObject;
            dimmerImage = dimmer;
            contentRoot = content;
            backgroundImage = background;
            stageLayer = stage;
            titleText = runtimeTitle;
            dayText = runtimeDay;
            hintText = runtimeHint;
            nextDayButton = runtimeNextDay;
            dialogueBar = runtimeDialogueBar;
            speakerText = runtimeSpeaker;
            dialogueText = runtimeBody;
            nextButton = runtimeNext;
            skipButton = runtimeSkip;
            EnsureRuntimeBindings();
        }

        public void ShowEvening(
            GuildHallEveningData data,
            int dayIndex,
            int runSeed,
            EveningSessionState session,
            Action onNextDay,
            Action<List<ResolvedEffect>, string, string> applyEffects,
            Func<List<HunterData>> getHunters,
            Func<List<SquadData>> getSquads,
            Func<int> getForcedCount,
            Func<string> dequeueForced,
            Func<string, bool> consumeDayTriggeredScene)
        {
            EnsureRuntimeBindings();
            _data = data ?? new GuildHallEveningData();
            _dayIndex = dayIndex;
            _runSeed = runSeed;
            _session = session ?? new EveningSessionState { dayIndex = dayIndex };
            _onNextDay = onNextDay;
            _applyEffects = applyEffects;
            _getHunters = getHunters;
            _getSquads = getSquads;
            _getForcedCount = getForcedCount;
            _dequeueForced = dequeueForced;
            _consumeDayTriggeredScene = consumeDayTriggeredScene;

            if (root != null) root.SetActive(true);
            if (_cg != null) { _cg.alpha = 1f; _cg.interactable = true; _cg.blocksRaycasts = true; }
            if (!_pauseHeld) { GamePauseService.Push("GuildHall"); _pauseHeld = true; }

            BindButton(nextDayButton, OnNextDayClicked);
            BindButton(nextButton, ContinueFromPopup);
            BindButton(skipButton, ContinueFromPopup);
            ShowNextForcedOrHub();
        }

        public void Hide()
        {
            ClearRuntimeButtons();
            _awaitingContinue = false;
            if (_cg != null) { _cg.alpha = 0f; _cg.interactable = false; _cg.blocksRaycasts = false; }
            if (root != null) root.SetActive(false);
            if (_pauseHeld) { GamePauseService.Pop("GuildHall"); _pauseHeld = false; }
        }

        private void ShowNextForcedOrHub()
        {
            if (_getForcedCount != null && _getForcedCount() > 0)
            {
                var id = _dequeueForced != null ? _dequeueForced() : null;
                ShowForcedSceneById(id);
                return;
            }

            if (!_session.forcedIntroFinished && _data?.forcedScenes != null)
            {
                for (var i = 0; i < _data.forcedScenes.Count; i++)
                {
                    var scene = _data.forcedScenes[i];
                    if (scene?.trigger == null || !string.Equals(scene.trigger.type, "DAY_EQUALS", StringComparison.Ordinal) || scene.trigger.day != _dayIndex)
                    {
                        continue;
                    }

                    if (_consumeDayTriggeredScene != null && !_consumeDayTriggeredScene(scene.id))
                    {
                        continue;
                    }

                    _session.forcedIntroFinished = true;
                    ShowForcedScene(scene);
                    return;
                }

                _session.forcedIntroFinished = true;
            }

            ShowHub();
        }

        private void ShowForcedSceneById(string sceneId)
        {
            var scene = _data?.FindForcedScene(sceneId);
            if (scene == null)
            {
                ShowHub();
                return;
            }

            ShowForcedScene(scene);
        }

        private void ShowForcedScene(GuildHallForcedSceneData scene)
        {
            SetTop($"Day {_dayIndex}", "Forced Scene");
            if (dialogueBar != null) dialogueBar.SetActive(false);
            ClearRuntimeButtons();
            if (hintText != null) hintText.text = scene.text ?? string.Empty;

            var choices = scene.choices != null && scene.choices.Count > 0 ? scene.choices : new List<GuildHallChoiceData> { new GuildHallChoiceData { label = "Continue" } };
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                var index = i;
                AddRuntimeButton(string.IsNullOrWhiteSpace(choice?.label) ? "Continue" : choice.label, () =>
                {
                    var defs = choice?.effects ?? new List<EffectDef>();
                    var seed = DeterministicRng.Hash(_runSeed, _dayIndex, unchecked((int)DeterministicRng.HashString(scene.id)), index);
                    var resolved = EffectResolver.Resolve(defs, seed, 3100);
                    _applyEffects?.Invoke(resolved, null, null);
                    ShowNextForcedOrHub();
                }, true);
            }
        }

        private void ShowHub()
        {
            SetTop($"Day {_dayIndex}", $"Actions: {_session.apLeft}/{_session.maxAP}");
            if (dialogueBar != null) dialogueBar.SetActive(false);
            if (hintText != null) hintText.text = "Evening hub actions";
            ClearRuntimeButtons();

            var restCost = _data.GetHubActionCost("rest", 1);
            var talkCost = _data.GetHubActionCost("talk", 1);
            var treatCost = _data.GetHubActionCost("treat", 1);

            AddRuntimeButton("Rest", OnRestClicked, _session.apLeft >= restCost);
            AddRuntimeButton("Talk", OnTalkClicked, _session.apLeft >= talkCost);
            AddRuntimeButton("Treat (Soon)", null, false);

            if (nextDayButton != null)
            {
                nextDayButton.interactable = true;
            }
        }

        private void OnRestClicked()
        {
            var cost = _data.GetHubActionCost("rest", 1);
            if (_session.apLeft < cost)
            {
                return;
            }

            var hunters = _getHunters != null ? _getHunters() : new List<HunterData>();
            var squads = _getSquads != null ? _getSquads() : new List<SquadData>();
            ClearRuntimeButtons();
            SetTop($"Day {_dayIndex}", $"Actions: {_session.apLeft}/{_session.maxAP}");
            if (hintText != null) hintText.text = "Choose exhausted target to rest";

            var found = false;
            for (var i = 0; i < hunters.Count; i++)
            {
                var hunter = hunters[i];
                if (hunter == null || !hunter.exhaustedToday) continue;
                found = true;
                AddRuntimeButton($"Hunter: {hunter.name}", () => ApplyRest(cost, hunter.id, null), true);
            }

            for (var i = 0; i < squads.Count; i++)
            {
                var squad = squads[i];
                if (squad == null || !squad.exhausted) continue;
                found = true;
                AddRuntimeButton($"Squad: {squad.name}", () => ApplyRest(cost, null, squad.id), true);
            }

            AddRuntimeButton("Back", ShowHub, true);
            if (!found && hintText != null)
            {
                hintText.text = "No exhausted hunter or squad available.";
            }
        }

        private void ApplyRest(int cost, string hunterId, string squadId)
        {
            var effects = new List<ResolvedEffect>
            {
                new ResolvedEffect { type = EffectTypes.ClearExhaust, id = !string.IsNullOrWhiteSpace(hunterId) ? hunterId : squadId }
            };

            _applyEffects?.Invoke(effects, hunterId, squadId);
            _session.apLeft = Mathf.Max(0, _session.apLeft - cost);
            _session.performedActionIds.Add("rest");
            ShowHub();
        }

        private void OnTalkClicked()
        {
            var cost = _data.GetHubActionCost("talk", 1);
            if (_session.apLeft < cost)
            {
                return;
            }

            ClearRuntimeButtons();
            SetTop($"Day {_dayIndex}", $"Actions: {_session.apLeft}/{_session.maxAP}");
            if (hintText != null) hintText.text = "Guild chatter drifts through the hall.";
            AddRuntimeButton("Promise support (+1 REP)", () => ApplyTalkChoice(cost, 0), true);
            AddRuntimeButton("Follow rumor (TAG + rumor_undead)", () => ApplyTalkChoice(cost, 1), true);
            AddRuntimeButton("Back", ShowHub, true);
        }

        private void ApplyTalkChoice(int cost, int choiceIndex)
        {
            var defs = new List<EffectDef>();
            if (choiceIndex == 0)
            {
                defs.Add(new EffectDef { type = EffectTypes.Rep, delta = 1 });
            }
            else
            {
                defs.Add(new EffectDef { type = EffectTypes.TagAdd, id = "rumor_undead" });
            }

            var seed = DeterministicRng.Hash(_runSeed, _dayIndex, 777, choiceIndex);
            var resolved = EffectResolver.Resolve(defs, seed, 4100);
            _applyEffects?.Invoke(resolved, null, null);
            _session.apLeft = Mathf.Max(0, _session.apLeft - cost);
            _session.performedActionIds.Add("talk");
            ShowHub();
        }

        private void OnNextDayClicked()
        {
            if (_session.apLeft > 0)
            {
                ClearRuntimeButtons();
                if (hintText != null) hintText.text = $"You still have actions left ({_session.apLeft}). End evening anyway?";
                AddRuntimeButton("End Evening", () => _onNextDay?.Invoke(), true);
                AddRuntimeButton("Back", ShowHub, true);
                return;
            }

            _onNextDay?.Invoke();
        }

        private void ContinueFromPopup()
        {
            if (!_awaitingContinue)
            {
                return;
            }

            _awaitingContinue = false;
            ShowHub();
        }

        private void SetTop(string day, string title)
        {
            if (dayText != null) dayText.text = day;
            if (titleText != null) titleText.text = title;
        }

        private void AddRuntimeButton(string label, Action onClick, bool interactable)
        {
            if (stageLayer == null)
            {
                return;
            }

            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(stageLayer, false);
            var rect = go.GetComponent<RectTransform>();
            var index = _runtimeButtons.Count;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 52f);
            rect.anchoredPosition = new Vector2(0f, 120f - (index * 60f));

            var image = go.GetComponent<Image>();
            image.color = interactable ? new Color(0.18f, 0.2f, 0.24f, 0.92f) : new Color(0.1f, 0.1f, 0.1f, 0.65f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.fontSize = 26f;
            text.color = Color.white;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            button.interactable = interactable && onClick != null;
            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick.Invoke());
            }

            _runtimeButtons.Add(button);
        }

        private void ClearRuntimeButtons()
        {
            for (var i = 0; i < _runtimeButtons.Count; i++)
            {
                if (_runtimeButtons[i] != null)
                {
                    Destroy(_runtimeButtons[i].gameObject);
                }
            }

            _runtimeButtons.Clear();
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void EnsureRuntimeBindings()
        {
            if (root == null) root = gameObject;
            _cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            contentRoot = contentRoot != null ? contentRoot : root.transform.Find("Content") as RectTransform;
            backgroundImage = backgroundImage != null ? backgroundImage : root.transform.Find("Content/BackgroundLayer")?.GetComponent<Image>();
            stageLayer = stageLayer != null ? stageLayer : root.transform.Find("Content/StageLayer") as RectTransform;
            titleText = titleText != null ? titleText : root.transform.Find("Content/TopBar/Title")?.GetComponent<TMP_Text>();
            dayText = dayText != null ? dayText : root.transform.Find("Content/TopBar/Day")?.GetComponent<TMP_Text>();
            hintText = hintText != null ? hintText : root.transform.Find("Content/TopBar/Hint")?.GetComponent<TMP_Text>();
            nextDayButton = nextDayButton != null ? nextDayButton : root.transform.Find("Content/TopBar/NextDayButton")?.GetComponent<Button>();
            dialogueBar = dialogueBar != null ? dialogueBar : root.transform.Find("Content/DialogueBar")?.gameObject;
            speakerText = speakerText != null ? speakerText : root.transform.Find("Content/DialogueBar/Speaker")?.GetComponent<TMP_Text>();
            dialogueText = dialogueText != null ? dialogueText : root.transform.Find("Content/DialogueBar/Body")?.GetComponent<TMP_Text>();
            nextButton = nextButton != null ? nextButton : root.transform.Find("Content/DialogueBar/Buttons/NextButton")?.GetComponent<Button>();
            skipButton = skipButton != null ? skipButton : root.transform.Find("Content/DialogueBar/Buttons/SkipButton")?.GetComponent<Button>();
        }
    }
}
