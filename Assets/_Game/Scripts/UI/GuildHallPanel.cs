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

        [Header("Hub")]
        [SerializeField] private GameObject hubRoot;
        [SerializeField] private TMP_Text hubTitleText;
        [SerializeField] private TMP_Text hubSubtitleText;
        [SerializeField] private Button talkQuartermasterButton;
        [SerializeField] private Button talkCaptainButton;
        [SerializeField] private Button reviewRosterButton;
        [SerializeField] private Button restButton;
        [SerializeField] private Button nextDayButton;

        [Header("Dialogue")]
        [SerializeField] private GameObject dialogueRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button backButton;

        private CanvasGroup _cg;
        private GuildHallEveningData _data;
        private GuildHallSceneData _activeScene;
        private Action _onNextDay;
        private Action _onRestApplied;
        private Action _onSceneFinished;
        private Coroutine _typeCoroutine;
        private int _lineIndex;
        private bool _lineComplete;

        private void Awake()
        {
            EnsureRuntimeBindings();
            Hide();
        }

        public void ConfigureRuntimeBindings(
            GameObject rootObject,
            Image dimmer,
            RectTransform content,
            GameObject runtimeHubRoot,
            TMP_Text runtimeHubTitle,
            TMP_Text runtimeHubSubtitle,
            Button runtimeTalkQuartermaster,
            Button runtimeTalkCaptain,
            Button runtimeReviewRoster,
            Button runtimeRest,
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
            hubRoot = runtimeHubRoot;
            hubTitleText = runtimeHubTitle;
            hubSubtitleText = runtimeHubSubtitle;
            talkQuartermasterButton = runtimeTalkQuartermaster;
            talkCaptainButton = runtimeTalkCaptain;
            reviewRosterButton = runtimeReviewRoster;
            restButton = runtimeRest;
            nextDayButton = runtimeNextDay;
            dialogueRoot = runtimeDialogueRoot;
            speakerText = runtimeSpeaker;
            dialogueText = runtimeDialogue;
            nextButton = runtimeNext;
            skipButton = runtimeSkip;
            backButton = runtimeBack;
            EnsureRuntimeBindings();
        }

        public void ShowHub(GuildHallEveningData data, Action onNextDay, Action onRestApplied)
        {
            EnsureRuntimeBindings();
            _data = data ?? new GuildHallEveningData();
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

            BindHubButtons();
            BindDialogueButtons();
            SetHubMode();
            Debug.Log("[GuildHall] Enter HUB [TODO REMOVE]");
        }

        public void Hide()
        {
            EnsureRuntimeBindings();
            StopTypewriter();
            _activeScene = null;
            _lineIndex = 0;
            _lineComplete = false;
            _onSceneFinished = null;

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

        private void BindHubButtons()
        {
            BindButton(talkQuartermasterButton, () => StartScene("talk_quartermaster"));
            BindButton(talkCaptainButton, () => StartScene("talk_captain"));
            BindButton(reviewRosterButton, () => StartScene("review_roster"));
            BindButton(restButton, () => StartScene("rest_evening", () => _onRestApplied?.Invoke()));
            BindButton(nextDayButton, () => _onNextDay?.Invoke());

            if (hubTitleText != null)
            {
                hubTitleText.text = "GUILD HALL";
            }

            if (hubSubtitleText != null)
            {
                hubSubtitleText.text = "Evening activities";
            }
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

        private void SetHubMode()
        {
            StopTypewriter();
            if (hubRoot != null)
            {
                hubRoot.SetActive(true);
            }

            if (dialogueRoot != null)
            {
                dialogueRoot.SetActive(false);
            }
        }

        private void StartScene(string sceneId, Action onFinished = null)
        {
            EnsureRuntimeBindings();
            _activeScene = _data?.FindScene(sceneId);
            _onSceneFinished = onFinished;
            Debug.Log($"[GuildHall] Start scene id={sceneId} [TODO REMOVE]");

            if (_activeScene == null || _activeScene.lines == null || _activeScene.lines.Count == 0)
            {
                Debug.LogWarning($"[GuildHall] Scene '{sceneId}' is missing/empty. [TODO REMOVE]");
                _onSceneFinished?.Invoke();
                _onSceneFinished = null;
                SetHubMode();
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

            if (backButton != null)
            {
                var backLabel = backButton.GetComponentInChildren<TMP_Text>(true);
                if (backLabel != null)
                {
                    backLabel.text = "Back to Hall";
                }

                backButton.gameObject.SetActive(false);
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(true);
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
            }

            _lineIndex = 0;
            ShowCurrentLine();
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
            _onSceneFinished?.Invoke();
            _onSceneFinished = null;
            SetHubMode();
            Debug.Log("[GuildHall] Back to HUB [TODO REMOVE]");
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
            Debug.Log($"[GuildHall] Line {_lineIndex + 1}/{_activeScene.lines.Count} speaker={speakerText?.text} [TODO REMOVE]");
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
                    dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);
                }
            }

            if (contentRoot == null)
            {
                var content = root.transform.Find("Content");
                contentRoot = content as RectTransform;
            }

            if (hubRoot == null)
            {
                var hub = root.transform.Find("Content/Hub");
                hubRoot = hub != null ? hub.gameObject : null;
            }

            if (dialogueRoot == null)
            {
                var dialogue = root.transform.Find("Content/Dialogue");
                dialogueRoot = dialogue != null ? dialogue.gameObject : null;
            }

            hubTitleText = hubTitleText != null ? hubTitleText : root.transform.Find("Content/Hub/Title")?.GetComponent<TMP_Text>();
            hubSubtitleText = hubSubtitleText != null ? hubSubtitleText : root.transform.Find("Content/Hub/Subtitle")?.GetComponent<TMP_Text>();
            talkQuartermasterButton = talkQuartermasterButton != null ? talkQuartermasterButton : root.transform.Find("Content/Hub/Buttons/TalkQuartermasterButton")?.GetComponent<Button>();
            talkCaptainButton = talkCaptainButton != null ? talkCaptainButton : root.transform.Find("Content/Hub/Buttons/TalkCaptainButton")?.GetComponent<Button>();
            reviewRosterButton = reviewRosterButton != null ? reviewRosterButton : root.transform.Find("Content/Hub/Buttons/ReviewRosterButton")?.GetComponent<Button>();
            restButton = restButton != null ? restButton : root.transform.Find("Content/Hub/Buttons/RestButton")?.GetComponent<Button>();
            nextDayButton = nextDayButton != null ? nextDayButton : root.transform.Find("Content/Hub/Buttons/NextDayButton")?.GetComponent<Button>();

            speakerText = speakerText != null ? speakerText : root.transform.Find("Content/Dialogue/Speaker")?.GetComponent<TMP_Text>();
            dialogueText = dialogueText != null ? dialogueText : root.transform.Find("Content/Dialogue/Body")?.GetComponent<TMP_Text>();
            nextButton = nextButton != null ? nextButton : root.transform.Find("Content/Dialogue/Buttons/NextButton")?.GetComponent<Button>();
            skipButton = skipButton != null ? skipButton : root.transform.Find("Content/Dialogue/Buttons/SkipButton")?.GetComponent<Button>();
            backButton = backButton != null ? backButton : root.transform.Find("Content/Dialogue/Buttons/BackButton")?.GetComponent<Button>();

            var allText = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < allText.Length; i++)
            {
                allText[i].raycastTarget = false;
            }
        }
    }
}
