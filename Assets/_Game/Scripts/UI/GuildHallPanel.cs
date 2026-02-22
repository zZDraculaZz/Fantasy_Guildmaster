using System;
using System.Collections;
using FantasyGuildmaster.Data;
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
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextDayButton;

        private CanvasGroup _cg;
        private GuildHallSceneData _activeScene;
        private Action _onNextDay;
        private int _lineIndex;
        private bool _lineComplete;
        private Coroutine _typeCoroutine;

        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            EnsureRuntimeBindings();
            Hide();
        }

        public void ConfigureRuntimeBindings(
            GameObject rootObject,
            Image dimmer,
            RectTransform content,
            TMP_Text speaker,
            TMP_Text dialogue,
            Button next,
            Button skip,
            Button nextDay)
        {
            root = rootObject;
            dimmerImage = dimmer;
            contentRoot = content;
            speakerText = speaker;
            dialogueText = dialogue;
            nextButton = next;
            skipButton = skip;
            nextDayButton = nextDay;
            EnsureRuntimeBindings();
        }

        public void Show(string sceneId, GuildHallEveningData data, Action onNextDay)
        {
            EnsureRuntimeBindings();
            _activeScene = data?.FindScene(sceneId);
            _onNextDay = onNextDay;

            if (_activeScene == null || _activeScene.lines == null || _activeScene.lines.Count == 0)
            {
                Debug.LogWarning($"[GuildHall] Scene '{sceneId}' is missing/empty. [TODO REMOVE]");
                _onNextDay?.Invoke();
                return;
            }

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

            BindButtons();

            _lineIndex = 0;
            _lineComplete = false;
            if (nextDayButton != null)
            {
                nextDayButton.gameObject.SetActive(false);
            }

            ShowCurrentLine();
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            StopTypewriter();
            _activeScene = null;
            _lineIndex = 0;
            _lineComplete = false;
            _onNextDay = null;

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

        private void BindButtons()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.interactable = true;
                nextButton.onClick.AddListener(OnNextPressed);
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.interactable = true;
                skipButton.onClick.AddListener(OnSkipPressed);
            }

            if (nextDayButton != null)
            {
                nextDayButton.onClick.RemoveAllListeners();
                nextDayButton.interactable = true;
                nextDayButton.onClick.AddListener(() => _onNextDay?.Invoke());
            }
        }

        private void OnNextPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinish();
        }

        private void OnSkipPressed()
        {
            if (!_lineComplete)
            {
                CompleteCurrentLine();
                return;
            }

            AdvanceOrFinish();
        }

        private void AdvanceOrFinish()
        {
            if (_activeScene == null || _activeScene.lines == null)
            {
                return;
            }

            if (_lineIndex < _activeScene.lines.Count - 1)
            {
                _lineIndex++;
                ShowCurrentLine();
                return;
            }

            if (nextDayButton != null)
            {
                nextDayButton.gameObject.SetActive(true);
            }
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

            Debug.Log($"[GuildHall] Line {_lineIndex + 1}/{_activeScene.lines.Count} speaker={speakerText?.text} [TODO REMOVE]");
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
                    dimmerImage.color = new Color(0f, 0f, 0f, 0.7f);
                }
            }

            if (contentRoot == null && root != null)
            {
                var content = root.transform.Find("Content");
                contentRoot = content as RectTransform;
            }

            if (speakerText == null && root != null)
            {
                var speaker = root.transform.Find("Content/Speaker");
                speakerText = speaker != null ? speaker.GetComponent<TMP_Text>() : null;
            }

            if (dialogueText == null && root != null)
            {
                var dialogue = root.transform.Find("Content/Dialogue");
                dialogueText = dialogue != null ? dialogue.GetComponent<TMP_Text>() : null;
            }

            if (nextButton == null && root != null)
            {
                var next = root.transform.Find("Content/Buttons/NextButton");
                nextButton = next != null ? next.GetComponent<Button>() : null;
            }

            if (skipButton == null && root != null)
            {
                var skip = root.transform.Find("Content/Buttons/SkipButton");
                skipButton = skip != null ? skip.GetComponent<Button>() : null;
            }

            if (nextDayButton == null && root != null)
            {
                var nextDay = root.transform.Find("Content/Buttons/NextDayButton");
                nextDayButton = nextDay != null ? nextDay.GetComponent<Button>() : null;
            }

            if (nextButton == null)
            {
                nextButton = root.GetComponentInChildren<Button>(true);
            }

            var allText = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allText.Length; i++)
            {
                allText[i].raycastTarget = false;
            }
        }
    }
}
