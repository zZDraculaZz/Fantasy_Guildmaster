using System;
using System.Collections;
using System.Collections.Generic;
using FantasyGuildmaster.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyGuildmaster.UI
{
    public sealed class GuildHallPanel : MonoBehaviour
    {
        private enum ViewMode
        {
            Forced,
            Hub,
            Dialogue
        }

        [SerializeField] private GameObject root;
        [SerializeField] private Image dimmerImage;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Image backgroundImage;

        [Header("Hub")]
        [SerializeField] private GameObject hubRoot;
        [SerializeField] private TMP_Text hubTitleText;
        [SerializeField] private TMP_Text hubSubtitleText;
        [SerializeField] private TMP_Text hubHintText;
        [SerializeField] private RectTransform stageRoot;
        [SerializeField] private Button nextDayButton;

        [Header("Dialogue")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button backButton;

        private readonly List<GameObject> _characterHotspots = new();
        private CanvasGroup _cg;
        private GuildHallEveningData _data;
        private GuildHallSceneData _activeScene;
        private Action _onNextDay;
        private Action _onRestApplied;
        private Action _onSceneFinished;
        private Coroutine _typeCoroutine;
        private int _lineIndex;
        private bool _lineComplete;
        private bool _forcedPlayed;
        private ViewMode _mode;

        private void Awake()
        {
            EnsureRuntimeBindings();
            Hide();
        }

        public void ConfigureRuntimeBindings(
            GameObject rootObject,
            Image dimmer,
            RectTransform content,
            Image background,
            GameObject runtimeHubRoot,
            TMP_Text runtimeHubTitle,
            TMP_Text runtimeHubSubtitle,
            TMP_Text runtimeHubHint,
            RectTransform runtimeStage,
            Button runtimeNextDay,
            GameObject runtimeDialogueRoot,
            TMP_Text runtimeSpeaker,
            TMP_Text runtimeDialogue,
            Button runtimeNext,
            Button runtimeSkip,
            Button runtimeBack)
        {
            root = rootObject;
            dimmerImage = dimmer;
            contentRoot = content;
            backgroundImage = background;
            hubRoot = runtimeHubRoot;
            hubTitleText = runtimeHubTitle;
            hubSubtitleText = runtimeHubSubtitle;
            hubHintText = runtimeHubHint;
            stageRoot = runtimeStage;
            nextDayButton = runtimeNextDay;
            dialogueRoot = runtimeDialogueRoot;
            speakerText = runtimeSpeaker;
            dialogueText = runtimeDialogue;
            nextButton = runtimeNext;
            skipButton = runtimeSkip;
            backButton = runtimeBack;
            EnsureRuntimeBindings();
        }

        public void ShowEvening(GuildHallEveningData data, Action onNextDay, Action onRestApplied)
        {
            EnsureRuntimeBindings();
            _data = data ?? new GuildHallEveningData();
            _onNextDay = onNextDay;
            _onRestApplied = onRestApplied;
            _forcedPlayed = false;

            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
            }

            if (contentRoot != null)
            {
                contentRoot.SetAsLastSibling();
            }

            if (_cg != null)
            {
                _cg.alpha = 1f;
                _cg.interactable = true;
                _cg.blocksRaycasts = true;
            }

            BindDialogueButtons();
            BindButton(nextDayButton, () => _onNextDay?.Invoke());
            BuildCharacterHotspots();

            Debug.Log($"[GuildHall] Show evening, forcedIntro={_data?.forcedIntroSceneId} [TODO REMOVE]");
            if (!string.IsNullOrWhiteSpace(_data?.forcedIntroSceneId) && !_forcedPlayed)
            {
                _mode = ViewMode.Forced;
                _forcedPlayed = true;
                StartScene(_data.forcedIntroSceneId, null);
                Debug.Log($"[GuildHall] Forced scene start id={_data.forcedIntroSceneId} [TODO REMOVE]");
            }
            else
            {
                ShowHubMode();
            }
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            StopTypewriter();
            _activeScene = null;
            _lineIndex = 0;
            _lineComplete = false;
            _onSceneFinished = null;
            _mode = ViewMode.Hub;

            if (_cg != null)
            {
                _cg.alpha = 0f;
                _cg.interactable = false;
                _cg.blocksRaycasts = false;
            }

            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void ShowHubMode()
        {
            _mode = ViewMode.Hub;
            StopTypewriter();

            if (hubRoot != null)
            {
                hubRoot.SetActive(true);
            }

            if (dialogueRoot != null)
            {
                dialogueRoot.SetActive(false);
            }

            if (hubTitleText != null)
            {
                hubTitleText.text = "GUILD HALL";
            }

            if (hubSubtitleText != null)
            {
                hubSubtitleText.text = "Evening activities";
            }

            if (hubHintText != null)
            {
                hubHintText.text = "Click a character to talk";
            }

            Debug.Log($"[GuildHall] Hub ready, characters={_characterHotspots.Count} [TODO REMOVE]");
        }

        private void StartScene(string sceneId, Action onFinished)
        {
            EnsureRuntimeBindings();
            _activeScene = _data?.FindScene(sceneId);
            _onSceneFinished = onFinished;
            Debug.Log($"[GuildHall] Start scene id={sceneId} [TODO REMOVE]");

            if (_activeScene == null || _activeScene.lines == null || _activeScene.lines.Count == 0)
            {
                _onSceneFinished?.Invoke();
                _onSceneFinished = null;
                ShowHubMode();
                return;
            }

            if (hubRoot != null)
            {
                hubRoot.SetActive(false);
            }

            if (dialogueRoot != null)
            {
                dialogueRoot.SetActive(true);
            }

            _mode = _mode == ViewMode.Forced ? ViewMode.Forced : ViewMode.Dialogue;
            _lineIndex = 0;
            ShowCurrentLine();
        }

        private void OnCharacterClicked(GuildHallCharacterData character)
        {
            if (_mode != ViewMode.Hub || character == null)
            {
                return;
            }

            Debug.Log($"[GuildHall] Click character id={character.id} [TODO REMOVE]");
            var onFinish = string.Equals(character.id, "rest", StringComparison.Ordinal)
                || string.Equals(character.talkSceneId, "rest_evening", StringComparison.Ordinal)
                ? _onRestApplied
                : null;
            StartScene(character.talkSceneId, onFinish);
        }

        private void BindDialogueButtons()
        {
            BindButton(nextButton, OnNextPressed);
            BindButton(skipButton, OnSkipPressed);
            BindButton(backButton, OnBackPressed);
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.interactable = true;
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void OnNextPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinishDialogue();
        }

        private void OnSkipPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinishDialogue();
        }

        private void OnBackPressed()
        {
            if (_mode == ViewMode.Forced)
            {
                return;
            }

            _onSceneFinished?.Invoke();
            _onSceneFinished = null;
            Debug.Log("[GuildHall] Scene end -> back to hub [TODO REMOVE]");
            ShowHubMode();
        }

        private void AdvanceOrFinishDialogue()
        {
            if (_activeScene == null || _activeScene.lines == null)
            {
                OnBackPressed();
                return;
            }

            if (_lineIndex < _activeScene.lines.Count - 1)
            {
                _lineIndex++;
                ShowCurrentLine();
                return;
            }

            if (_mode == ViewMode.Forced)
            {
                _onSceneFinished?.Invoke();
                _onSceneFinished = null;
                Debug.Log("[GuildHall] Scene end -> back to hub [TODO REMOVE]");
                ShowHubMode();
                return;
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(false);
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(false);
            }

            if (backButton != null)
            {
                backButton.gameObject.SetActive(true);
            }
        }

        private void ShowCurrentLine()
        {
            if (_activeScene == null || _activeScene.lines == null || _lineIndex < 0 || _lineIndex >= _activeScene.lines.Count)
            {
                return;
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(true);
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
            }

            if (backButton != null)
            {
                backButton.gameObject.SetActive(false);
                var backLabel = backButton.GetComponentInChildren<TMP_Text>(true);
                if (backLabel != null)
                {
                    backLabel.text = "Back to Hall";
                }
            }

            var line = _activeScene.lines[_lineIndex] ?? new GuildHallLineData();
            if (speakerText != null)
            {
                speakerText.text = string.IsNullOrWhiteSpace(line.speaker) ? "Narrator" : line.speaker;
            }

            if (dialogueText != null)
            {
                dialogueText.text = line.text ?? string.Empty;
                dialogueText.maxVisibleCharacters = 0;
            }

            _lineComplete = false;
            StopTypewriter();
            _typeCoroutine = StartCoroutine(TypeCurrentLine());
        }

        private IEnumerator TypeCurrentLine()
        {
            if (dialogueText == null)
            {
                _lineComplete = true;
                yield break;
            }

            dialogueText.ForceMeshUpdate();
            var total = dialogueText.textInfo.characterCount;
            var shown = 0;
            var timer = 0f;
            const float charsPerSecond = 40f;

            while (shown < total)
            {
                timer += Time.unscaledDeltaTime * charsPerSecond;
                var target = Mathf.Min(total, Mathf.FloorToInt(timer));
                if (target != shown)
                {
                    shown = target;
                    dialogueText.maxVisibleCharacters = shown;
                }

                yield return null;
            }

            dialogueText.maxVisibleCharacters = total;
            _lineComplete = true;
            _typeCoroutine = null;
        }

        private void CompleteCurrentLine()
        {
            StopTypewriter();
            if (dialogueText != null)
            {
                dialogueText.ForceMeshUpdate();
                dialogueText.maxVisibleCharacters = dialogueText.textInfo.characterCount;
            }

            _lineComplete = true;
        }

        private void StopTypewriter()
        {
            if (_typeCoroutine == null)
            {
                return;
            }

            StopCoroutine(_typeCoroutine);
            _typeCoroutine = null;
        }

        private void BuildCharacterHotspots()
        {
            if (stageRoot == null)
            {
                return;
            }

            for (var i = 0; i < _characterHotspots.Count; i++)
            {
                if (_characterHotspots[i] != null)
                {
                    Destroy(_characterHotspots[i]);
                }
            }

            _characterHotspots.Clear();

            if (_data?.characters == null)
            {
                return;
            }

            for (var i = 0; i < _data.characters.Count; i++)
            {
                var character = _data.characters[i];
                if (character == null || string.IsNullOrWhiteSpace(character.talkSceneId))
                {
                    continue;
                }

                var hotspot = new GameObject($"Character_{character.id}", typeof(RectTransform), typeof(Image), typeof(Button));
                hotspot.transform.SetParent(stageRoot, false);
                var rect = hotspot.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(character.posX, character.posY);
                rect.anchorMax = new Vector2(character.posX, character.posY);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(160f, 260f);

                var image = hotspot.GetComponent<Image>();
                image.color = new Color(0.65f, 0.65f, 0.72f, 0.92f);
                image.raycastTarget = true;

                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(hotspot.transform, false);
                var labelText = label.GetComponent<TextMeshProUGUI>();
                labelText.text = string.IsNullOrWhiteSpace(character.displayName) ? character.id : character.displayName;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.fontSize = 22f;
                labelText.raycastTarget = false;
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(0f, 40f);
                labelRect.anchoredPosition = new Vector2(0f, 8f);

                var button = hotspot.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                var captured = character;
                button.onClick.AddListener(() => OnCharacterClicked(captured));

                _characterHotspots.Add(hotspot);
            }

            stageRoot.SetAsLastSibling();
        }

        private void EnsureRuntimeBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            _cg = root.GetComponent<CanvasGroup>();
            if (_cg == null)
            {
                _cg = root.AddComponent<CanvasGroup>();
            }

            if (dimmerImage == null)
            {
                dimmerImage = root.GetComponent<Image>();
            }

            if (dimmerImage != null)
            {
                dimmerImage.raycastTarget = true;
                if (dimmerImage.color.a <= 0f)
                {
                    dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);
                }
            }

            if (contentRoot == null)
            {
                contentRoot = root.transform.Find("Content") as RectTransform;
            }

            if (backgroundImage == null)
            {
                backgroundImage = root.transform.Find("Content/Background")?.GetComponent<Image>();
            }

            if (hubRoot == null)
            {
                hubRoot = root.transform.Find("Content/Hub")?.gameObject;
            }

            if (dialogueRoot == null)
            {
                dialogueRoot = root.transform.Find("Content/Dialogue")?.gameObject;
            }

            hubTitleText = hubTitleText != null ? hubTitleText : root.transform.Find("Content/Hub/Title")?.GetComponent<TMP_Text>();
            hubSubtitleText = hubSubtitleText != null ? hubSubtitleText : root.transform.Find("Content/Hub/Subtitle")?.GetComponent<TMP_Text>();
            hubHintText = hubHintText != null ? hubHintText : root.transform.Find("Content/Hub/Hint")?.GetComponent<TMP_Text>();
            stageRoot = stageRoot != null ? stageRoot : root.transform.Find("Content/Hub/Stage") as RectTransform;
            nextDayButton = nextDayButton != null ? nextDayButton : root.transform.Find("Content/Hub/NextDayButton")?.GetComponent<Button>();

            speakerText = speakerText != null ? speakerText : root.transform.Find("Content/Dialogue/Speaker")?.GetComponent<TMP_Text>();
            dialogueText = dialogueText != null ? dialogueText : root.transform.Find("Content/Dialogue/Body")?.GetComponent<TMP_Text>();
            nextButton = nextButton != null ? nextButton : root.transform.Find("Content/Dialogue/Buttons/NextButton")?.GetComponent<Button>();
            skipButton = skipButton != null ? skipButton : root.transform.Find("Content/Dialogue/Buttons/SkipButton")?.GetComponent<Button>();
            backButton = backButton != null ? backButton : root.transform.Find("Content/Dialogue/Buttons/BackButton")?.GetComponent<Button>();

            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
            }

            var allText = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allText.Length; i++)
            {
                allText[i].raycastTarget = false;
            }
        }
    }
}
