using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 5 - Gaston's panel: a header that folds and unfolds the dialogue, a speech bubble
    // (loading icons, reply written letter by letter, tag saying where the reply comes from),
    // a text field and a microphone button
    public class GardenerPanel : MonoBehaviour
    {
        [SerializeField] private GardenerDialogue dialogue;
        [Tooltip("Optional: without it the microphone button is hidden.")]
        [SerializeField] private GardenerVoice voice;

        [Header("Fold / unfold")]
        [SerializeField] private RectTransform body;
        [SerializeField] private RectTransform foldArrow;
        [SerializeField] private bool openAtStart = true;
        [SerializeField] private float foldSeconds = 0.15f;

        [Header("Bubble")]
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text replyText;
        [SerializeField] private Graphic moreArrow;
        [Tooltip("Typewriter speed. 0 = the text appears at once.")]
        [SerializeField] private float charactersPerSecond = 45f;
        [TextArea]
        [SerializeField] private string idleText = "Pose-moi une question sur ton jardin !";

        [Header("Waiting (the AI thinks, the microphone listens)")]
        [SerializeField] private GameObject waitingGroup;
        [SerializeField] private TMP_Text waitingText;
        [SerializeField] private Image[] iconSlots;
        [Tooltip("Any sprites, any number. They are shown a few at a time, the next ones the next time.")]
        [SerializeField] private Sprite[] thinkingIcons;
        [SerializeField] private float hopHeight = 12f;
        [SerializeField] private float hopSeconds = 0.9f;

        [Header("Source tag")]
        [SerializeField] private Image tagBackground;
        [SerializeField] private TMP_Text tagText;

        [Header("Keyword colours: [vert]word[/vert] and [or]word[/or]")]
        [SerializeField] private Color keywordGreen = new Color32(0x2F, 0x7D, 0x32, 0xFF);
        [SerializeField] private Color keywordGold = new Color32(0xB2, 0x6A, 0x00, 0xFF);

        [Header("Input")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Image micIcon;
        [SerializeField] private TMP_Text counterText;

        private static readonly Color32 Cream = new Color32(0xFF, 0xF8, 0xE7, 0xFF);
        private static readonly Color32 Sun = new Color32(0xF5, 0xC5, 0x42, 0xFF);
        private static readonly Color32 AiTagBack = new Color32(0xDC, 0xEF, 0xC9, 0xFF);
        private static readonly Color32 AiTagText = new Color32(0x2F, 0x6B, 0x2F, 0xFF);
        private static readonly Color32 OfflineTagBack = new Color32(0xF3, 0xD3, 0xC2, 0xFF);
        private static readonly Color32 OfflineTagText = new Color32(0x8F, 0x3D, 0x1C, 0xFF);
        private static readonly Color32 NoticeTagBack = new Color32(0xEA, 0xE4, 0xD0, 0xFF);
        private static readonly Color32 NoticeTagText = new Color32(0x3A, 0x2A, 0x1E, 0xFF);

        public bool IsOpen { get; private set; }

        private float bodyHeight;
        private float fold;
        private float shownCharacters;
        private int totalCharacters;
        private bool replyIsFinal;
        private Vector2[] iconRestPositions;
        private int nextIcon;
        private bool iconsWereSeen;
        private GardenerVoice.VoiceState shownVoiceState;

        private void Awake()
        {
            // rich text is on, but GardenerMarkup only lets our two colour markers through
            replyText.richText = true;
            replyText.parseCtrlCharacters = false;
            questionText.richText = false;
            questionText.text = "";

            bodyHeight = body.sizeDelta.y;
            iconRestPositions = new Vector2[iconSlots.Length];
            for (int i = 0; i < iconSlots.Length; i++) iconRestPositions[i] = iconSlots[i].rectTransform.anchoredPosition;

            if (micIcon != null && voice == null) micIcon.gameObject.SetActive(false);

            IsOpen = openAtStart;
            fold = IsOpen ? 1f : 0f;
            ApplyFold();

            waitingGroup.SetActive(false);
            tagBackground.gameObject.SetActive(false);
            SetReply(idleText, false);
            SkipTypewriter();
        }

        private void OnEnable()
        {
            dialogue.QuestionAsked += HandleQuestionAsked;
            dialogue.ReplyUpdated += HandleReplyUpdated;
            dialogue.ReplyFinished += HandleReplyFinished;
            inputField.onSubmit.AddListener(HandleSubmit);
            ShowCounter();
        }

        private void OnDisable()
        {
            dialogue.QuestionAsked -= HandleQuestionAsked;
            dialogue.ReplyUpdated -= HandleReplyUpdated;
            dialogue.ReplyFinished -= HandleReplyFinished;
            inputField.onSubmit.RemoveListener(HandleSubmit);
        }

        private void Update()
        {
            UpdateFold();
            UpdateVoiceState();

            if (shownCharacters < totalCharacters)
            {
                shownCharacters = Mathf.Min(totalCharacters, shownCharacters + charactersPerSecond * Time.unscaledDeltaTime);
                replyText.maxVisibleCharacters = Mathf.FloorToInt(shownCharacters);
            }

            ShowArrow();
            if (waitingGroup.activeInHierarchy) HopIcons();
        }

        // ------------------------------------------------------------------ fold / unfold

        // for the header button, and for the game (a controller button in VR for example)
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            IsOpen = true;
            body.gameObject.SetActive(true);
        }

        public void Close()
        {
            IsOpen = false;
        }

        private void UpdateFold()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(fold, target)) return;

            fold = foldSeconds <= 0f ? target : Mathf.MoveTowards(fold, target, Time.unscaledDeltaTime / foldSeconds);
            ApplyFold();
        }

        // the body unrolls in a few steps, like a menu of an old console game
        private void ApplyFold()
        {
            float stepped = Mathf.Round(fold * 5f) / 5f;
            body.sizeDelta = new Vector2(body.sizeDelta.x, bodyHeight * stepped);
            body.gameObject.SetActive(stepped > 0f || IsOpen);
            if (foldArrow != null) foldArrow.localScale = new Vector3(1f, IsOpen ? -1f : 1f, 1f);
        }

        // ------------------------------------------------------------------ dialogue events

        private void HandleQuestionAsked(string question)
        {
            Open();
            questionText.text = (IsEnglish() ? "You: " : "Toi : ") + question;
            shownCharacters = 0f;
            totalCharacters = 0;
            replyIsFinal = false;

            tagBackground.gameObject.SetActive(false);
            ShowWaiting(IsEnglish() ? "Gaston is thinking…" : "Gaston réfléchit…", true);
            ShowCounter();
        }

        // only the local model sends its reply piece by piece
        private void HandleReplyUpdated(string replySoFar)
        {
            SetReply(replySoFar, true);
            ShowTag(GardenerReplySource.LocalModel);
        }

        private void HandleReplyFinished(string reply, GardenerReplySource source)
        {
            replyIsFinal = true;
            SetReply(reply, false);
            ShowTag(source);
        }

        // ------------------------------------------------------------------ bubble

        // shows the whole reply at once (for a "skip" button, and for the self-check)
        public void SkipTypewriter()
        {
            shownCharacters = totalCharacters;
            replyText.maxVisibleCharacters = totalCharacters;
            ShowArrow();
        }

        private void SetReply(string raw, bool streaming)
        {
            HideWaiting();
            replyText.gameObject.SetActive(true);

            replyText.text = GardenerMarkup.ToRichText(raw, keywordGreen, keywordGold, streaming);
            replyText.ForceMeshUpdate(true);
            totalCharacters = replyText.textInfo.characterCount;

            if (charactersPerSecond <= 0f) shownCharacters = totalCharacters;
            shownCharacters = Mathf.Min(shownCharacters, totalCharacters);
            replyText.maxVisibleCharacters = Mathf.FloorToInt(shownCharacters);
            ShowArrow();
        }

        private void ShowWaiting(string label, bool withIcons)
        {
            replyText.gameObject.SetActive(false);
            moreArrow.gameObject.SetActive(false);
            waitingText.text = label;

            bool hasIcons = withIcons && thinkingIcons != null && thinkingIcons.Length > 0;
            for (int i = 0; i < iconSlots.Length; i++)
            {
                iconSlots[i].enabled = hasIcons;
                if (hasIcons) iconSlots[i].sprite = thinkingIcons[(nextIcon + i) % thinkingIcons.Length];
            }

            iconsWereSeen = false;
            waitingGroup.SetActive(true);
        }

        private void HideWaiting()
        {
            if (!waitingGroup.activeSelf) return;

            // the list only moves on when the icons were really on screen (an offline tip hides them at once)
            if (iconsWereSeen && thinkingIcons != null && thinkingIcons.Length > 0)
            {
                nextIcon = (nextIcon + iconSlots.Length) % thinkingIcons.Length;
            }

            waitingGroup.SetActive(false);
        }

        // a hop in a few steps, not a smooth curve
        private void HopIcons()
        {
            iconsWereSeen = true;

            for (int i = 0; i < iconSlots.Length; i++)
            {
                float phase = Mathf.Repeat(Time.unscaledTime - i * 0.15f, hopSeconds) / hopSeconds;
                float height = phase < 0.125f || phase >= 0.5f ? 0f : (phase >= 0.25f && phase < 0.375f ? hopHeight : hopHeight * 0.5f);
                iconSlots[i].rectTransform.anchoredPosition = iconRestPositions[i] + Vector2.up * height;
            }
        }

        // the blinking arrow means "Gaston has finished"
        private void ShowArrow()
        {
            bool finished = replyIsFinal && shownCharacters >= totalCharacters && replyText.gameObject.activeSelf;
            if (moreArrow.gameObject.activeSelf != finished) moreArrow.gameObject.SetActive(finished);
            if (finished) moreArrow.enabled = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
        }

        // the player must always know if the text comes from the local model or from a pre-written tip
        private void ShowTag(GardenerReplySource source)
        {
            switch (source)
            {
                case GardenerReplySource.LocalModel:
                    tagText.text = IsEnglish() ? "Local AI" : "IA locale";
                    tagBackground.color = AiTagBack;
                    tagText.color = AiTagText;
                    break;
                case GardenerReplySource.OfflineTip:
                    tagText.text = IsEnglish() ? "Offline tip" : "Hors ligne · conseil préécrit";
                    tagBackground.color = OfflineTagBack;
                    tagText.color = OfflineTagText;
                    break;
                default:
                    tagText.text = "Info";
                    tagBackground.color = NoticeTagBack;
                    tagText.color = NoticeTagText;
                    break;
            }

            tagBackground.gameObject.SetActive(true);
        }

        private void ShowCounter()
        {
            if (counterText != null) counterText.text = "Questions : " + dialogue.QuestionCount;
        }

        private bool IsEnglish()
        {
            return dialogue.Language == GardenerLanguage.English;
        }

        // ------------------------------------------------------------------ keyboard and microphone

        private void HandleSubmit(string text)
        {
            dialogue.Ask(text);
            inputField.text = "";
            if (isActiveAndEnabled) StartCoroutine(FocusNextFrame());
        }

        // the field loses the focus right after Enter: take it back so the player can keep typing
        private IEnumerator FocusNextFrame()
        {
            yield return null;
            if (inputField.isActiveAndEnabled) inputField.ActivateInputField();
        }

        // for the microphone button: one click to talk, one click when finished
        public void ToggleMicrophone()
        {
            if (voice != null) voice.ToggleListening();
        }

        private void UpdateVoiceState()
        {
            if (voice == null || voice.State == shownVoiceState) return;

            shownVoiceState = voice.State;
            if (micIcon != null) micIcon.color = shownVoiceState == GardenerVoice.VoiceState.Listening ? Sun : Cream;

            if (shownVoiceState == GardenerVoice.VoiceState.Listening)
            {
                Open();
                questionText.text = "";
                tagBackground.gameObject.SetActive(false);
                ShowWaiting(IsEnglish() ? "I'm listening… click the mic when you're done." : "Je t'écoute… reclique sur le micro quand tu as fini.", false);
            }
            else if (shownVoiceState == GardenerVoice.VoiceState.Transcribing)
            {
                ShowWaiting(IsEnglish() ? "Let me write that down…" : "Je note ce que tu as dit…", false);
            }
            else if (waitingGroup.activeSelf && !dialogue.IsAnswering)
            {
                // nothing was understood: say why instead of leaving the bubble waiting
                replyIsFinal = false;
                SetReply(voice.LastProblem.Length > 0 ? voice.LastProblem : idleText, false);
                SkipTypewriter();
            }
        }
    }

    // turns the model's text into safe TextMesh Pro text
    // only [vert]...[/vert] and [or]...[/or] become colours, everything else is shown exactly as written:
    // no HTML and no TextMesh Pro tag coming from the model is ever interpreted
    public static class GardenerMarkup
    {
        private const string GreenOpen = "[vert]";
        private const string GreenClose = "[/vert]";
        private const string GoldOpen = "[or]";
        private const string GoldClose = "[/or]";

        private static readonly string[] Markers = { GreenOpen, GreenClose, GoldOpen, GoldClose };
        private static readonly Regex MarkerPattern = new Regex(@"\[/?(vert|or)\]|\*\*", RegexOptions.IgnoreCase);

        // markers left alone in the text, the ** some models add around words, and the only tag that could end a <noparse> block
        private static readonly Regex Leftovers = new Regex(@"\[/?(vert|or)\]|\*\*|</noparse>", RegexOptions.IgnoreCase);

        // a model sometimes forgets the "]" of an opening marker: "[orlaitue[/or]"
        private static readonly Regex BrokenOpening = new Regex(@"\[(vert|or)([^\[\]]{1,40})\[/\1\]", RegexOptions.IgnoreCase);

        // streaming = the text is still being written: a marker not closed yet already colours what follows
        public static string ToRichText(string raw, Color green, Color gold, bool streaming)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = BrokenOpening.Replace(raw, "[$1]$2[/$1]");
            if (streaming) raw = HideUnfinishedMarker(raw);

            StringBuilder result = new StringBuilder();
            int position = 0;

            while (position < raw.Length)
            {
                int greenAt = raw.IndexOf(GreenOpen, position, StringComparison.OrdinalIgnoreCase);
                int goldAt = raw.IndexOf(GoldOpen, position, StringComparison.OrdinalIgnoreCase);

                if (greenAt < 0 && goldAt < 0)
                {
                    AppendPlain(result, raw.Substring(position));
                    break;
                }

                bool isGreen = goldAt < 0 || (greenAt >= 0 && greenAt < goldAt);
                int open = isGreen ? greenAt : goldAt;
                string openMarker = isGreen ? GreenOpen : GoldOpen;
                string closeMarker = isGreen ? GreenClose : GoldClose;
                Color colour = isGreen ? green : gold;

                AppendPlain(result, raw.Substring(position, open - position));

                int contentStart = open + openMarker.Length;
                int close = raw.IndexOf(closeMarker, contentStart, StringComparison.OrdinalIgnoreCase);

                if (close < 0)
                {
                    // never closed: coloured while the text is coming, plain once the reply is complete
                    string rest = raw.Substring(contentStart);
                    if (streaming) AppendColoured(result, rest, colour);
                    else AppendPlain(result, rest);
                    break;
                }

                AppendColoured(result, raw.Substring(contentStart, close - contentStart), colour);
                position = close + closeMarker.Length;
            }

            return result.ToString();
        }

        // the same text without any marker, for the voice
        public static string ToPlainText(string raw, bool streaming)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            raw = BrokenOpening.Replace(raw, "[$1]$2[/$1]");
            if (streaming) raw = HideUnfinishedMarker(raw);
            return MarkerPattern.Replace(raw, "");
        }

        private static void AppendPlain(StringBuilder result, string text)
        {
            text = Clean(text);
            if (text.Length == 0) return;

            result.Append("<noparse>").Append(text).Append("</noparse>");
        }

        private static void AppendColoured(StringBuilder result, string text, Color colour)
        {
            text = Clean(text);
            if (text.Length == 0) return;

            result.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(colour)).Append("><noparse>")
                  .Append(text).Append("</noparse></color>");
        }

        // removes stray markers and anything that could close our <noparse> block
        private static string Clean(string text)
        {
            string cleaned = Leftovers.Replace(text, "");

            // removing a piece can glue a new one together, so repeat until nothing changes
            while (cleaned != text)
            {
                text = cleaned;
                cleaned = Leftovers.Replace(text, "");
            }

            return cleaned;
        }

        // while streaming, the end of the text can be half a marker ("[ve"): wait for the rest before showing it
        private static string HideUnfinishedMarker(string raw)
        {
            int bracket = raw.LastIndexOf('[');
            if (bracket < 0) return raw;

            string tail = raw.Substring(bracket);
            if (tail.IndexOf(']') >= 0) return raw;

            foreach (string marker in Markers)
            {
                if (tail.Length < marker.Length && marker.StartsWith(tail, StringComparison.OrdinalIgnoreCase))
                {
                    return raw.Substring(0, bracket);
                }
            }

            return raw;
        }
    }
}
