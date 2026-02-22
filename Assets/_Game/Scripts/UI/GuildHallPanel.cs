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
            Hub,
            Dialogue
        }

        [SerializeField] private GameObject root;
        [SerializeField] private Image dimmerImage;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform stageLayer;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private Button nextDayButton;

        [Header("Dialogue Bar")]
        [SerializeField] private GameObject dialogueBar;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;

        private readonly List<GameObject> _characterHotspots = new();
        private CanvasGroup _cg;
        private GuildHallEveningData _data;
        private GuildHallSceneData _activeScene;
        private Coroutine _typeCoroutine;
        private Action _onNextDay;
        private Action _onRestApplied;
        private Action _onSceneFinished;
        private int _lineIndex;
        private int _dayIndex;
        private bool _lineComplete;
        private bool _hubInputEnabled;
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
            RectTransform stage,
            TMP_Text runtimeTitle,
            TMP_Text runtimeDay,
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
            nextDayButton = runtimeNextDay;
            dialogueBar = runtimeDialogueBar;
            speakerText = runtimeSpeaker;
            dialogueText = runtimeBody;
            nextButton = runtimeNext;
            skipButton = runtimeSkip;
            EnsureRuntimeBindings();
        }

        public void ShowEvening(GuildHallEveningData data, int dayIndex, Action onNextDay, Action onRestApplied)
        {
            EnsureRuntimeBindings();
            _data = data ?? new GuildHallEveningData();
            _dayIndex = dayIndex;
            _onNextDay = onNextDay;
            _onRestApplied = onRestApplied;

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

            BindCoreButtons();
            BuildCharacterHotspots();
            ShowHub();
            Debug.Log($"[GuildHall] Enter overlay day={_dayIndex} [TODO REMOVE]");

            if (!string.IsNullOrWhiteSpace(_data.forcedIntroSceneId))
            {
                _hubInputEnabled = false;
                StartScene(_data.forcedIntroSceneId, null);
                Debug.Log($"[GuildHall] Forced intro start id={_data.forcedIntroSceneId} [TODO REMOVE]");
            }
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            StopTypewriter();
            _activeScene = null;
            _lineIndex = 0;
            _lineComplete = false;
            _hubInputEnabled = false;

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

        private void BindCoreButtons()
        {
            BindButton(nextDayButton, OnNextDayClicked);
            BindButton(nextButton, OnNextPressed);
            BindButton(skipButton, OnSkipPressed);
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

        private void OnNextDayClicked()
        {
            if (_mode == ViewMode.Dialogue)
            {
                return;
            }

            Debug.Log("[GuildHall] Next Day [TODO REMOVE]");
            _onNextDay?.Invoke();
        }

        private void ShowHub()
        {
            _mode = ViewMode.Hub;
            _hubInputEnabled = true;

            if (titleText != null)
            {
                titleText.text = "GUILD HALL";
            }

            if (dayText != null)
            {
                dayText.text = $"Day {_dayIndex}";
            }

            if (dialogueBar != null)
            {
                dialogueBar.SetActive(false);
            }

            if (nextDayButton != null)
            {
                nextDayButton.interactable = true;
            }

            Debug.Log($"[GuildHall] Hub ready characters={_characterHotspots.Count} [TODO REMOVE]");
        }

        private void StartScene(string sceneId, Action onFinished)
        {
            _activeScene = _data?.FindScene(sceneId);
            _onSceneFinished = onFinished;

            if (_activeScene == null || _activeScene.lines == null || _activeScene.lines.Count == 0)
            {
                _onSceneFinished?.Invoke();
                _onSceneFinished = null;
                ShowHub();
                return;
            }

            _mode = ViewMode.Dialogue;
            _lineIndex = 0;
            _lineComplete = false;
            _hubInputEnabled = false;

            if (dialogueBar != null)
            {
                dialogueBar.SetActive(true);
                dialogueBar.transform.SetAsLastSibling();
            }

            if (nextDayButton != null)
            {
                nextDayButton.interactable = false;
            }

            ShowCurrentLine();
        }

        private void OnCharacterClicked(GuildHallCharacterData character)
        {
            if (!_hubInputEnabled || _mode != ViewMode.Hub || character == null)
            {
                return;
            }

            Debug.Log($"[GuildHall] Click character id={character.id} scene={character.talkSceneId} [TODO REMOVE]");

            Action onFinish = null;
            if (string.Equals(character.talkSceneId, "rest_evening", StringComparison.Ordinal))
            {
                onFinish = _onRestApplied;
            }

            if (string.Equals(character.talkSceneId, "open_roster", StringComparison.Ordinal))
            {
                StartScene("open_roster", onFinish);
                return;
            }

            StartScene(character.talkSceneId, onFinish);
        }

        private void OnNextPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinishScene();
        }

        private void OnSkipPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinishScene();
        }

        private void AdvanceOrFinishScene()
        {
            if (_activeScene == null || _activeScene.lines == null)
            {
                EndSceneToHub();
                return;
            }

            if (_lineIndex < _activeScene.lines.Count - 1)
            {
                _lineIndex++;
                ShowCurrentLine();
                return;
            }

            EndSceneToHub();
        }

        private void EndSceneToHub()
        {
            _onSceneFinished?.Invoke();
            _onSceneFinished = null;
            Debug.Log("[GuildHall] Scene end -> hub [TODO REMOVE]");
            ShowHub();
        }

        private void ShowCurrentLine()
        {
            if (_activeScene == null || _activeScene.lines == null || _lineIndex < 0 || _lineIndex >= _activeScene.lines.Count)
            {
                return;
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
            if (stageLayer == null)
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
                hotspot.transform.SetParent(stageLayer, false);

                var rect = hotspot.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(character.posX, character.posY);
                rect.anchorMax = new Vector2(character.posX, character.posY);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(180f, 300f);

                var image = hotspot.GetComponent<Image>();
                image.color = new Color(0.65f, 0.65f, 0.72f, 0.92f);
                image.raycastTarget = true;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(hotspot.transform, false);
                var label = labelGo.GetComponent<TextMeshProUGUI>();
                label.text = string.IsNullOrWhiteSpace(character.displayName) ? character.id : character.displayName;
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 22f;
                label.raycastTarget = false;
                var labelRect = labelGo.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(0f, 46f);
                labelRect.anchoredPosition = new Vector2(0f, 10f);

                var button = hotspot.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.interactable = true;
                var captured = character;
                button.onClick.AddListener(() => OnCharacterClicked(captured));

                _characterHotspots.Add(hotspot);
            }

            stageLayer.SetAsLastSibling();
        }

        private void EnsureRuntimeBindings()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (root.transform is RectTransform rootRect)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
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
                    dimmerImage.color = new Color(0f, 0f, 0f, 0.65f);
                }
            }

            contentRoot = contentRoot != null ? contentRoot : root.transform.Find("Content") as RectTransform;
            backgroundImage = backgroundImage != null ? backgroundImage : root.transform.Find("Content/BackgroundLayer")?.GetComponent<Image>();
            stageLayer = stageLayer != null ? stageLayer : root.transform.Find("Content/StageLayer") as RectTransform;
            titleText = titleText != null ? titleText : root.transform.Find("Content/TopBar/Title")?.GetComponent<TMP_Text>();
            dayText = dayText != null ? dayText : root.transform.Find("Content/TopBar/Day")?.GetComponent<TMP_Text>();
            nextDayButton = nextDayButton != null ? nextDayButton : root.transform.Find("Content/TopBar/NextDayButton")?.GetComponent<Button>();
            dialogueBar = dialogueBar != null ? dialogueBar : root.transform.Find("Content/DialogueBar")?.gameObject;
            speakerText = speakerText != null ? speakerText : root.transform.Find("Content/DialogueBar/Speaker")?.GetComponent<TMP_Text>();
            dialogueText = dialogueText != null ? dialogueText : root.transform.Find("Content/DialogueBar/Body")?.GetComponent<TMP_Text>();
            nextButton = nextButton != null ? nextButton : root.transform.Find("Content/DialogueBar/Buttons/NextButton")?.GetComponent<Button>();
            skipButton = skipButton != null ? skipButton : root.transform.Find("Content/DialogueBar/Buttons/SkipButton")?.GetComponent<Button>();

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
