using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 5 - Gaston in the top-left corner: his portrait, and under it the key that opens his speech bubble.
    // Open: the bubble (reply written letter by letter, growing line by line, hopping fruits while he thinks,
    // an alert when the replies are pre-written), the microphone and its key, and the key that closes it.
    // The player only talks to him: hold the talk key, ask, release
    public class GardenerPanel : MonoBehaviour
    {
        [SerializeField] private GardenerDialogue dialogue;
        [Tooltip("Optional: without it the microphone is hidden.")]
        [SerializeField] private GardenerVoice voice;

        [Header("Open / close")]
        [Tooltip("The bubble and the line under it: shown only while open.")]
        [SerializeField] private CanvasGroup bubbleGroup;
        [Tooltip("The key under the portrait: shown only while closed.")]
        [SerializeField] private GameObject openHint;
        [SerializeField] private bool openAtStart;
        [SerializeField] private float popSeconds = 0.12f;

        [Header("Bubble")]
        [SerializeField] private TMP_Text replyText;
        [Tooltip("Clips the reply: as high as the lines written so far, at most Max Lines.")]
        [SerializeField] private LayoutElement replyViewport;
        [SerializeField] private int maxLines = 8;
        [Tooltip("Typewriter speed. 0 = the text appears at once.")]
        [SerializeField] private float charactersPerSecond = 45f;
        [Tooltip("{talk} becomes the name of the talk key.")]
        [TextArea]
        [SerializeField] private string idleText = "Bonjour ! Maintiens [or]{talk}[/or] et pose-moi ta question sur ton jardin.";

        [Header("Thinking: hopping fruits and vegetables")]
        [SerializeField] private GameObject waitingGroup;
        [SerializeField] private Image[] iconSlots;
        [Tooltip("Any sprites, any number. They are shown a few at a time, the next ones the next time.")]
        [SerializeField] private Sprite[] thinkingIcons;
        [SerializeField] private float hopHeight = 12f;
        [SerializeField] private float hopSeconds = 0.9f;

        [Header("Alert: the online AI is missing, or no AI could answer")]
        [SerializeField] private GameObject alertGroup;
        [SerializeField] private TMP_Text alertText;

        [Header("Keyword colours: [vert]word[/vert] and [or]word[/or]")]
        [SerializeField] private Color keywordGreen = new Color32(0x9B, 0xE0, 0x7A, 0xFF);
        [SerializeField] private Color keywordGold = new Color32(0xFF, 0xCC, 0x33, 0xFF);

        [Header("Microphone (shown only while open)")]
        [SerializeField] private GameObject voiceGroup;
        [SerializeField] private Image micRing;
        [SerializeField] private Image micIcon;
        [SerializeField] private Image talkKeyFrame;

        [Header("Keys")]
        [Tooltip("Desktop: a key opens or closes the bubble, a key held down to talk, a key closes it.")]
        [SerializeField] private bool keyboardShortcuts = true;
        [Tooltip("Opens or closes the bubble.")]
        [SerializeField] private KeyCode typeKey = KeyCode.T;
        [SerializeField] private KeyCode talkKey = KeyCode.V;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private TMP_Text typeKeyText;
        [SerializeField] private TMP_Text talkKeyText;
        [SerializeField] private TMP_Text closeKeyText;

        [Header("In the game")]
        [Tooltip("With a GameManager in the scene, Gaston only shows while a level is played (not in the menus).")]
        [SerializeField] private bool onlyWhilePlaying = true;

        // the cream of the frames and keys, and the gold of the game for what is active
        public static readonly Color32 Cream = new Color32(0xE3, 0xE0, 0xBF, 0xFF);
        public static readonly Color32 Gold = new Color32(0xFF, 0xCC, 0x33, 0xFF);

        public bool IsOpen { get; private set; }

        private float pop;
        private float shownCharacters;
        private int totalCharacters;
        private float shownHeight = -1f;
        private float shownTop;
        private Vector2[] iconRestPositions;
        private int nextIcon;
        private bool iconsWereSeen;
        private GardenerVoice.VoiceState shownVoiceState;
        private CanvasGroup visibility;
        private bool talkKeyHeld;
        private bool showingVoiceText;

        private void Awake()
        {
            // rich text is on, but GardenerMarkup only lets our two colour markers through
            replyText.richText = true;
            replyText.parseCtrlCharacters = false;

            iconRestPositions = new Vector2[iconSlots.Length];
            for (int i = 0; i < iconSlots.Length; i++) iconRestPositions[i] = iconSlots[i].rectTransform.anchoredPosition;

            typeKeyText.text = KeyName(typeKey);
            talkKeyText.text = KeyName(talkKey);
            closeKeyText.text = KeyName(closeKey);

            IsOpen = openAtStart;
            pop = IsOpen ? 1f : 0f;
            ApplyPop();

            waitingGroup.SetActive(false);
            alertGroup.SetActive(false);
            SetReply(IdleText, false);
            SkipTypewriter();

            // hidden with a CanvasGroup, never switched off: the models keep loading and Gaston can still speak
            visibility = GetComponent<CanvasGroup>();
            if (visibility == null) visibility = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            dialogue.QuestionAsked += HandleQuestionAsked;
            dialogue.ReplyUpdated += HandleReplyUpdated;
            dialogue.ReplyFinished += HandleReplyFinished;
            GameEvents.GameStateChanged += HandleGameStateChanged;
            ShowIfPlaying(GameManager.Instance != null ? GameManager.Instance.State : GameState.Playing);
        }

        private void OnDisable()
        {
            dialogue.QuestionAsked -= HandleQuestionAsked;
            dialogue.ReplyUpdated -= HandleReplyUpdated;
            dialogue.ReplyFinished -= HandleReplyFinished;
            GameEvents.GameStateChanged -= HandleGameStateChanged;
        }

        private void Update()
        {
            UpdatePop();
            UpdateVoiceState();
            if (keyboardShortcuts) ReadShortcuts();

            if (shownCharacters < totalCharacters)
            {
                shownCharacters = Mathf.Min(totalCharacters, shownCharacters + charactersPerSecond * Time.unscaledDeltaTime);
                replyText.maxVisibleCharacters = Mathf.FloorToInt(shownCharacters);
            }

            UpdateReplyHeight();
            if (waitingGroup.activeInHierarchy) HopIcons();
        }

        // ------------------------------------------------------------------ open / close

        // for a button, and for the game (a controller button in VR for example)
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            IsOpen = true;
            bubbleGroup.gameObject.SetActive(true);
        }

        public void Close()
        {
            IsOpen = false;
        }

        public bool IsVisible
        {
            get { return visibility == null || visibility.alpha > 0f; }
        }

        private void HandleGameStateChanged(GameState state)
        {
            ShowIfPlaying(state);
        }

        private void ShowIfPlaying(GameState state)
        {
            bool show = !onlyWhilePlaying || GameManager.Instance == null || state == GameState.Playing;
            visibility.alpha = show ? 1f : 0f;
            visibility.interactable = show;
            visibility.blocksRaycasts = show;
        }

        private void UpdatePop()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(pop, target)) return;

            pop = popSeconds <= 0f ? target : Mathf.MoveTowards(pop, target, Time.unscaledDeltaTime / popSeconds);
            ApplyPop();
        }

        // the bubble fades in and grows a little from Gaston's side; the keys under the portrait follow what is shown
        private void ApplyPop()
        {
            float eased = pop * pop * (3f - 2f * pop);
            bubbleGroup.alpha = eased;
            bubbleGroup.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
            bubbleGroup.interactable = bubbleGroup.blocksRaycasts = IsOpen;
            bubbleGroup.gameObject.SetActive(eased > 0f || IsOpen);

            openHint.SetActive(!IsOpen);
            voiceGroup.SetActive(IsOpen && voice != null);
        }

        // ------------------------------------------------------------------ keyboard (desktop)

        // T: open or close the bubble. V held down: talk, released: Gaston listens to what was said. Esc: close
        private void ReadShortcuts()
        {
            if (!IsVisible) return;

            if (Input.GetKeyDown(closeKey) && IsOpen) Close();
            else if (Input.GetKeyDown(typeKey)) Toggle();

            if (voice == null) return;
            if (Input.GetKeyDown(talkKey))
            {
                talkKeyHeld = true;
                Open();
                voice.StartListening();
                ShowVoiceProblem();
            }
            else if (talkKeyHeld && Input.GetKeyUp(talkKey))
            {
                talkKeyHeld = false;
                voice.StopListening();
            }
        }

        private string IdleText
        {
            get { return idleText.Replace("{talk}", KeyName(talkKey)); }
        }

        private static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Escape: return "Échap";
                case KeyCode.Return: return "Entrée";
                case KeyCode.Space: return "Espace";
                case KeyCode.Tab: return "Tab";
                default: return key.ToString().Replace("Alpha", "");
            }
        }

        // ------------------------------------------------------------------ dialogue events

        private void HandleQuestionAsked(string question)
        {
            Open();
            showingVoiceText = false;
            shownCharacters = 0f;
            totalCharacters = 0;
            alertGroup.SetActive(false);
            ShowWaiting();
        }

        // only the local model sends its reply piece by piece
        private void HandleReplyUpdated(string replySoFar)
        {
            showingVoiceText = false;
            SetReply(replySoFar, true);
        }

        private void HandleReplyFinished(string reply, GardenerReplySource source)
        {
            showingVoiceText = false;
            SetReply(reply, false);
            ShowAlert(source);
        }

        // the player must know when the online AI is not used (the local AI is slower and simpler), and when no AI answered
        private void ShowAlert(GardenerReplySource source)
        {
            bool english = IsEnglish();
            bool configured = dialogue.OnlineConfigured;
            string text = null;

            if (source == GardenerReplySource.Unavailable)
                text = english ? "No AI available" : "Aucune IA disponible";
            else if (source == GardenerReplySource.LocalModel)
                text = configured
                    ? (english ? "Online AI unavailable: local AI" : "IA distante indisponible : IA locale")
                    : (english ? "Online AI not set up: local AI" : "IA distante non configurée : IA locale");

            alertGroup.SetActive(text != null);
            if (text != null) alertText.text = text;
        }

        // ------------------------------------------------------------------ bubble

        // shows the whole reply at once (for a "skip" button, and for the self-check)
        public void SkipTypewriter()
        {
            shownCharacters = totalCharacters;
            replyText.maxVisibleCharacters = totalCharacters;
            UpdateReplyHeight();
        }

        private void SetReply(string raw, bool streaming)
        {
            HideWaiting();
            replyViewport.gameObject.SetActive(true);

            replyText.text = GardenerMarkup.ToRichText(raw, keywordGreen, keywordGold, streaming);
            replyText.ForceMeshUpdate(true);
            totalCharacters = replyText.textInfo.characterCount;

            if (charactersPerSecond <= 0f) shownCharacters = totalCharacters;
            shownCharacters = Mathf.Min(shownCharacters, totalCharacters);
            replyText.maxVisibleCharacters = Mathf.FloorToInt(shownCharacters);
            UpdateReplyHeight();
        }

        // number of lines written so far (at least one): the bubble grows with them
        public int ShownLines
        {
            get
            {
                TMP_TextInfo info = replyText.textInfo;
                int visible = Mathf.Min(Mathf.FloorToInt(shownCharacters), info.characterCount);
                return visible <= 0 ? 1 : info.characterInfo[visible - 1].lineNumber + 1;
            }
        }

        // as high as the lines written, at most maxLines; beyond that the text scrolls up to show the last ones.
        // Measured on the text itself (top of the first line shown to the bottom of the last one)
        private void UpdateReplyHeight()
        {
            if (!replyViewport.gameObject.activeSelf) return;

            TMP_TextInfo info = replyText.textInfo;
            int lines = Mathf.Min(ShownLines, info.lineCount);
            float top = 0f;
            float height = replyText.fontSize * 1.45f;

            if (lines > 0)
            {
                int first = Mathf.Max(0, lines - maxLines);
                top = first == 0 ? 0f : info.lineInfo[first].ascender;
                height = top - info.lineInfo[lines - 1].descender;
            }

            // past maxLines the height stays the same but the text moves up: check both
            height = Mathf.Ceil(height);
            if (Mathf.Approximately(height, shownHeight) && Mathf.Approximately(top, shownTop)) return;

            shownHeight = height;
            shownTop = top;
            replyViewport.preferredHeight = height;
            replyText.rectTransform.anchoredPosition = new Vector2(0f, -top);
        }

        private void ShowWaiting()
        {
            replyViewport.gameObject.SetActive(false);
            shownHeight = -1f;

            bool hasIcons = thinkingIcons != null && thinkingIcons.Length > 0;
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

        // a hop in a few steps, not a smooth curve, like the pixel-art fruits
        private void HopIcons()
        {
            iconsWereSeen = true;

            for (int i = 0; i < iconSlots.Length; i++)
            {
                float phase = Mathf.Repeat(Time.unscaledTime - i * 0.12f, hopSeconds) / hopSeconds;
                float height = phase < 0.125f || phase >= 0.5f ? 0f : (phase >= 0.25f && phase < 0.375f ? hopHeight : hopHeight * 0.5f);
                iconSlots[i].rectTransform.anchoredPosition = iconRestPositions[i] + Vector2.up * height;
            }
        }

        private bool IsEnglish()
        {
            return dialogue.Language == GardenerLanguage.English;
        }

        // ------------------------------------------------------------------ microphone

        // for the microphone button: one click to talk, one click when finished
        public void ToggleMicrophone()
        {
            if (voice == null) return;
            voice.ToggleListening();
            ShowVoiceProblem();
        }

        // the microphone did not start (models still loading, no microphone, refused by the system): say why at once,
        // otherwise the bubble opens and nothing happens
        private void ShowVoiceProblem()
        {
            if (voice.State != GardenerVoice.VoiceState.Idle || voice.LastProblem.Length == 0 || dialogue.IsAnswering) return;
            Open();
            alertGroup.SetActive(false);
            SetReply(voice.LastProblem, false);
            SkipTypewriter();
        }

        private void UpdateVoiceState()
        {
            if (voice == null || voice.State == shownVoiceState) return;

            shownVoiceState = voice.State;
            bool listening = shownVoiceState == GardenerVoice.VoiceState.Listening;
            Color colour = listening ? (Color)Gold : Cream;
            micRing.color = colour;
            micIcon.color = colour;
            talkKeyFrame.color = colour;
            talkKeyText.color = colour;

            if (listening)
            {
                Open();
                alertGroup.SetActive(false);
                string key = "[or]" + KeyName(talkKey) + "[/or]";
                SetReply(talkKeyHeld
                    ? (IsEnglish() ? "I'm listening… release " + key + " when you're done." : "Je t'écoute… relâche " + key + " quand tu as fini.")
                    : (IsEnglish() ? "I'm listening… click the microphone when you're done." : "Je t'écoute… reclique sur le micro quand tu as fini."), false);
                SkipTypewriter();
                showingVoiceText = true;
            }
            else if (shownVoiceState == GardenerVoice.VoiceState.Transcribing)
            {
                SetReply(IsEnglish() ? "Let me write that down…" : "Je note ce que tu as dit…", false);
                SkipTypewriter();
                showingVoiceText = true;
            }
            else if (showingVoiceText)
            {
                showingVoiceText = false;
                // nothing was understood: say why instead of leaving "I'm listening" in the bubble
                SetReply(voice.LastProblem.Length > 0 ? voice.LastProblem : IdleText, false);
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
