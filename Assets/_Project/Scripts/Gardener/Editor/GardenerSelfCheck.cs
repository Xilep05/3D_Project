using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GardenWise
{
    // owner 5 - plays Test_Gardener and checks Gaston by itself (menu GardenWise > Gardener):
    // panel, colour markers, offline tips, the real local model, the voice (speech and listening)
    // also runs from the command line: Unity -batchmode -executeMethod GardenWise.GardenerSelfCheck.Run
    // results: Console, and Logs/GardenerSelfCheck_*.png, Logs/GardenerSelfCheck_llm_<model>.txt, Logs/GardenerVoices/*.wav
    [InitializeOnLoad]
    public static class GardenerSelfCheck
    {
        private const string RunningKey = "GardenWise.GardenerSelfCheck.Running";
        private const string StartTimeKey = "GardenWise.GardenerSelfCheck.StartTime";
        private const double TimeoutSeconds = 900.0;
        private const int FramesBeforeChecks = 5;
        private const string LogFolder = "Logs";

        private static readonly Color Green = new Color32(0x2F, 0x7D, 0x32, 0xFF);
        private static readonly Color Gold = new Color32(0xB2, 0x6A, 0x00, 0xFF);

        private static readonly List<string> failures = new List<string>();
        private static int checksDone;
        private static int framesInPlayMode;

        // entering play mode reloads the scripts: this picks the run up again on the other side
        static GardenerSelfCheck()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;

            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorApplication.update += Watchdog;
        }

        [MenuItem("GardenWise/Gardener/Run self-check")]
        public static void Run()
        {
            if (!File.Exists(GardenerBuilder.ScenePath))
            {
                Debug.LogError("[SelfCheck] Scene missing, use GardenWise > Gardener > Build test scene first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(GardenerBuilder.ScenePath);

            // GARDENER_MODEL=<file.gguf>: try another model for this run only (the scene is not saved)
            string model = Environment.GetEnvironmentVariable("GARDENER_MODEL");
            if (!string.IsNullOrEmpty(model))
            {
                SerializedObject dialogue = new SerializedObject(Object.FindAnyObjectByType<GardenerDialogue>());
                dialogue.FindProperty("modelFile").stringValue = model;
                dialogue.ApplyModifiedPropertiesWithoutUndo();
            }

            // GARDENER_LISTEN=<folder in StreamingAssets>: try another listening model for this run only
            string listen = Environment.GetEnvironmentVariable("GARDENER_LISTEN");
            if (!string.IsNullOrEmpty(listen))
            {
                SerializedObject voice = new SerializedObject(Object.FindAnyObjectByType<GardenerVoice>());
                voice.FindProperty("listenFolder").stringValue = listen;
                voice.ApplyModifiedPropertiesWithoutUndo();
            }

            SessionState.SetBool(RunningKey, true);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorApplication.update += Watchdog;
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;

            framesInPlayMode = 0;
            EditorApplication.update += WaitThenCheck;
        }

        // lets Awake, OnEnable and Start run before checking anything
        private static void WaitThenCheck()
        {
            framesInPlayMode++;
            if (framesInPlayMode < FramesBeforeChecks) return;

            EditorApplication.update -= WaitThenCheck;
            RunAll();
        }

        private static async void RunAll()
        {
            float volume = AudioListener.volume;
            AudioListener.volume = 0f; // Gaston really speaks during the checks: keep the room quiet

            // GARDENER_CHECK=model: only the model, long list of questions. =voices: one WAV per voice sample.
            // =listen: accuracy of the listening model on those voices
            string only = Environment.GetEnvironmentVariable("GARDENER_CHECK") ?? "";

            try
            {
                if (only == "model") await CheckModelOnly(false);
                else if (only == "online") await CheckModelOnly(true);
                else if (only == "voices") await CheckVoice(Object.FindAnyObjectByType<GardenerVoice>(), true);
                else if (only == "listen") await CheckListening(Object.FindAnyObjectByType<GardenerVoice>());
                else if (only == "voicelab") await CompareVoiceSettings(Object.FindAnyObjectByType<GardenerVoice>());
                else
                {
                    CheckMarkup();
                    await CheckScene();
                }
            }
            catch (Exception exception)
            {
                failures.Add("Exception: " + exception);
            }

            AudioListener.volume = volume;
            Finish();
        }

        // ------------------------------------------------------------------ colour markers (pure text)

        private static void CheckMarkup()
        {
            Check(Rich("Ta [vert]laitue[/vert] a [or]soif[/or].", false) ==
                  "<noparse>Ta </noparse><color=#2F7D32><noparse>laitue</noparse></color><noparse> a </noparse>" +
                  "<color=#B26A00><noparse>soif</noparse></color><noparse>.</noparse>",
                "markup: [vert] and [or] become the two colours");
            Check(Rich("<b>gras</b> <color=red>x</color>", false) == "<noparse><b>gras</b> <color=red>x</color></noparse>",
                "markup: HTML written by the model stays plain text");
            Check(Rich("a</noparse><size=200>b", false) == "<noparse>a<size=200>b</noparse>", "markup: the model cannot close the protection");
            Check(CountOf(Rich("a</nop</noparse>arse><size=200>b", false), "</noparse>") == 1, "markup: nor by hiding the closing tag in two pieces");
            Check(Rich("Bonjour [ve", true) == "<noparse>Bonjour </noparse>", "markup: half a marker is hidden while the text is coming");
            Check(Rich("Arrose [vert]ce so", true) == "<noparse>Arrose </noparse><color=#2F7D32><noparse>ce so</noparse></color>",
                "markup: an open marker already colours while the text is coming");

            string unclosed = Rich("Arrose [vert]ce soir", false);
            Check(!unclosed.Contains("<color") && !unclosed.Contains("[vert]") && unclosed.Contains("ce soir"),
                "markup: a marker never closed gives plain text in the final reply");
            Check(Rich("eau[/or] claire", false) == "<noparse>eau claire</noparse>", "markup: a closing marker alone is removed");
            Check(Rich("[VERT]Radis[/Vert]", false) == "<color=#2F7D32><noparse>Radis</noparse></color>", "markup: markers work in upper case too");
            Check(Rich("[bleu]x[/bleu]", false) == "<noparse>[bleu]x[/bleu]</noparse>", "markup: an unknown marker is not a colour");
            Check(Rich("", false) == "" && Rich(null, false) == "", "markup: an empty reply gives an empty text");
            Check(GardenerMarkup.ToPlainText("Ta [vert]laitue[/vert] a [or]soif[/or] [ve", true) == "Ta laitue a soif ",
                "markup: the voice gets the text without any marker");
        }

        // ------------------------------------------------------------------ the real scene, in play mode

        private static async Task CheckScene()
        {
            GardenerDialogue dialogue = Object.FindAnyObjectByType<GardenerDialogue>();
            GardenerPanel panel = Object.FindAnyObjectByType<GardenerPanel>();
            GardenerVoice voice = Object.FindAnyObjectByType<GardenerVoice>();
            GardenContextBuilder garden = Object.FindAnyObjectByType<GardenContextBuilder>();
            TMP_InputField input = Object.FindAnyObjectByType<TMP_InputField>();

            Check(dialogue != null && panel != null && voice != null && garden != null && input != null,
                "the scene has Gaston's dialogue, panel, voice, garden context and an input field");
            if (failures.Count > 0) return;

            RectTransform root = (RectTransform)panel.transform;
            RectTransform body = Field<RectTransform>(panel, "body");
            TMP_Text reply = Field<TMP_Text>(panel, "replyText");
            TMP_Text question = Field<TMP_Text>(panel, "questionText");
            TMP_Text tagText = Field<TMP_Text>(panel, "tagText");
            TMP_Text counter = Field<TMP_Text>(panel, "counterText");
            Image tagBackground = Field<Image>(panel, "tagBackground");
            Graphic arrow = Field<Graphic>(panel, "moreArrow");
            GameObject waiting = Field<GameObject>(panel, "waitingGroup");
            Image bubble = body.Find("Bubble").GetComponent<Image>();

            Check(body != null && reply != null && question != null && tagText != null && counter != null && tagBackground != null && arrow != null && waiting != null,
                "the panel has its body, reply, question, tag, counter, arrow and waiting group");
            if (failures.Count > 0) return;

            int eventsHeard = 0;
            string lastQuestionHeard = null;
            Action<string> listener = asked => { eventsHeard++; lastQuestionHeard = asked; };
            GameEvents.GardenerAsked += listener;

            GardenerReplySource lastSource = GardenerReplySource.Notice;
            string lastReply = "";
            dialogue.ReplyFinished += (text, source) => { lastReply = text; lastSource = source; };

            // 1. the panel: top right, one third of the screen, pixel art, folds and unfolds
            Check(root.anchorMin == Vector2.one && root.anchorMax == Vector2.one && root.pivot == Vector2.one, "the panel is anchored to the top right corner");
            Check(root.rect.width > 1920f * 0.3f && root.rect.width < 1920f * 0.36f, "the panel is about one third of the screen wide");
            Check(bubble.type == Image.Type.Sliced && bubble.sprite != null && bubble.sprite.border == new Vector4(3f, 3f, 3f, 3f), "the bubble is a 9-slice pixel frame");
            Check(bubble.sprite.texture.filterMode == FilterMode.Point, "the bubble pixels stay sharp (point filter)");
            Check(reply.font != null && reply.font.name.Contains("Pixelify"), "the reply uses the pixel font");
            Check(panel.IsOpen && body.gameObject.activeSelf, "the panel starts unfolded");
            Check(!waiting.activeSelf && !tagBackground.gameObject.activeSelf && !arrow.gameObject.activeSelf, "before any question: no loading icons, no tag, no arrow");
            Check(reply.GetParsedText().Contains("Pose-moi une question"), "before any question: Gaston invites the player to ask");

            panel.Toggle();
            await WaitUntil(() => !body.gameObject.activeSelf, 3f);
            Check(!panel.IsOpen && !body.gameObject.activeSelf, "the header button folds the panel");
            Capture("GardenerSelfCheck_folded.png");
            panel.Toggle();
            await WaitUntil(() => body.gameObject.activeSelf && Mathf.Approximately(body.sizeDelta.y, 428f), 3f);
            Check(panel.IsOpen && Mathf.Approximately(body.sizeDelta.y, 428f), "and unfolds it again to its full height");

            // 2. the garden context comes from the test garden
            string context = garden.Describe();
            Check(context.Contains("Laitue") && context.Contains("Radis") && context.Contains("Carotte"), "the context lists the three test plants");
            Check(context.Contains("Demain ce sera la canicule") && context.Contains("42 L"), "the context gives tomorrow's weather and the water left");
            Check(context.Contains("santé 2/3") && context.Contains("terre trop sèche (22, idéal 50 à 80). À faire : l'arroser"), "the context gives the health of a plant and what its soil needs");

            // 3. offline: a typed question, exactly as the Enter key sends it
            // (the online model, set up or not on this computer, is checked on its own in part 5)
            dialogue.UseOnlineModel = false;
            dialogue.UseLocalModel = false;
            const string firstQuestion = "Pourquoi ma laitue va mal et quand arroser ?";
            input.text = firstQuestion;
            input.onSubmit.Invoke(input.text);

            Check(eventsHeard == 1 && lastQuestionHeard == firstQuestion, "GameEvents.GardenerAsked goes out once, with the question");
            Check(dialogue.QuestionCount == 1 && counter.text.Contains("1"), "the counter shows 1 question");
            Check(question.text.Contains(firstQuestion), "the bubble shows the question that was understood");
            Check(input.text == "", "the input field is emptied after sending");
            Check(reply.maxVisibleCharacters == 0 && !arrow.gameObject.activeSelf, "the reply starts hidden and is written letter by letter");

            panel.SkipTypewriter();
            string firstReply = reply.GetParsedText();
            Check(firstReply.StartsWith("Arrose le matin"), "the whole offline tip is shown once written");
            Check(tagBackground.gameObject.activeSelf && tagText.text.Contains("Hors ligne") && lastSource == GardenerReplySource.OfflineTip, "the reply is tagged as an offline tip");
            Check(!tagText.text.Contains("IA"), "an offline tip is never tagged as the local AI");
            Check(arrow.gameObject.activeSelf && !waiting.activeSelf && !dialogue.IsAnswering, "arrow shown, no loading icons, dialogue free again");
            arrow.enabled = true;
            Capture("GardenerSelfCheck_offline.png");

            input.onSubmit.Invoke("   ");
            Check(eventsHeard == 1 && dialogue.QuestionCount == 1, "an empty question is not counted and sends no event");

            for (int i = 0; i < 12; i++) dialogue.Ask("Question " + i);
            panel.SkipTypewriter();
            Check(eventsHeard == 13 && dialogue.QuestionCount == 13, "13 questions in a row are all counted, there is no limit");
            Check(reply.GetParsedText() != firstReply && tagText.text.Contains("Hors ligne"), "the tips rotate and stay tagged as offline tips");

            // 4. a model played by the test: thinking, streaming, colours, safety
            Action<string> pushPartial = null;
            Action<string> complete = null;
            string messageSeen = null;
            dialogue.TestModel = (message, onPartial, onDone) => { messageSeen = message; pushPartial = onPartial; complete = onDone; };
            dialogue.UseLocalModel = true;

            dialogue.Ask(firstQuestion);
            Check(messageSeen != null && messageSeen.Contains("Laitue") && messageSeen.EndsWith(firstQuestion), "the model gets the garden context, then the question");
            Check(waiting.activeSelf && !reply.gameObject.activeSelf && !tagBackground.gameObject.activeSelf && dialogue.IsAnswering, "while the AI thinks: loading icons, no reply, no tag");

            Image[] slots = waiting.GetComponentsInChildren<Image>();
            Check(slots.Length == 3 && slots.All(slot => slot.enabled && slot.sprite != null) && slots.Select(slot => slot.sprite).Distinct().Count() == 3, "three different loading icons are shown");
            Check(slots.All(slot => slot.sprite.texture.filterMode == FilterMode.Point), "the loading icons are sharp pixel art");
            Capture("GardenerSelfCheck_thinking.png");

            pushPartial("Ta [vert]lai");
            Check(!waiting.activeSelf && reply.gameObject.activeSelf && tagText.text.Contains("IA locale"), "the first words replace the loading icons, tagged local AI");
            Check(reply.text.Contains("<color=#2F7D32>") && !reply.text.Contains("[vert]"), "a keyword is coloured as soon as it starts");
            pushPartial("Ta [vert]laitue[/vert] a soif. [o");
            Check(!reply.text.Contains("[o"), "half a marker is never shown");

            complete("Ta [vert]laitue[/vert] a soif. <b>Arrose</b>-la [or]ce soir[/or] !");
            panel.SkipTypewriter();
            Check(reply.GetParsedText() == "Ta laitue a soif. <b>Arrose</b>-la ce soir !", "final reply: markers become colours, HTML stays visible as plain text");
            Check(tagText.text.Contains("IA locale") && arrow.gameObject.activeSelf, "final reply: tagged local AI, arrow shown");

            dialogue.Ask("Première question");
            Action<string> lateAnswer = complete;
            dialogue.Ask("Deuxième question");
            lateAnswer("Vieille réponse");
            Check(waiting.activeSelf && dialogue.IsAnswering, "an answer that comes too late is ignored");
            complete("Nouvelle réponse");
            panel.SkipTypewriter();
            Check(reply.GetParsedText() == "Nouvelle réponse", "only the answer to the last question is shown");

            dialogue.Ask("Question pendant une panne");
            complete("");
            panel.SkipTypewriter();
            Check(tagText.text.Contains("Hors ligne") && reply.GetParsedText().Length > 0, "an empty AI answer falls back to a tagged offline tip");

            dialogue.Ask("Une longue question");
            complete("Ta [vert]laitue[/vert] manque d'eau : sa terre est à [or]22[/or] alors qu'elle aime entre 50 et 80. " +
                     "Arrose-la doucement [vert]ce soir[/vert] avec l'eau du récupérateur, pas en plein soleil. " +
                     "Demain c'est la [or]canicule[/or], alors pense aussi au paillage pour garder la terre fraîche !");
            panel.SkipTypewriter();
            Check(!reply.isTextOverflowing && reply.fontSize >= 20f, "a long three-sentence reply fits in the bubble");
            arrow.enabled = true;
            Capture("GardenerSelfCheck_long.png");

            // 5. an online model played by the test: first choice, and the local model takes over when it fails
            string onlineMessage = null;
            Action<string> onlineDone = null;
            dialogue.TestOnline = (message, onDone) => { onlineMessage = message; onlineDone = onDone; };
            dialogue.UseOnlineModel = true;
            messageSeen = null;

            dialogue.Ask(firstQuestion);
            Check(onlineMessage != null && onlineMessage.Contains("Laitue") && onlineMessage.EndsWith(firstQuestion) && messageSeen == null,
                "with an online model set up, it gets the question first (the local model waits)");
            onlineDone("Ta [vert]laitue[/vert] a soif, arrose-la [or]ce soir[/or] !");
            panel.SkipTypewriter();
            Check(lastSource == GardenerReplySource.OnlineModel && tagText.text.Contains("IA en ligne") && reply.GetParsedText() == "Ta laitue a soif, arrose-la ce soir !",
                "the online reply is shown with its colours, tagged online AI");

            dialogue.Ask("Question sans internet");
            onlineDone(null);
            Check(messageSeen != null && messageSeen.EndsWith("Question sans internet") && dialogue.IsAnswering,
                "when the online model fails, the same question goes to the local model");
            complete("Réponse du modèle local");
            panel.SkipTypewriter();
            Check(lastSource == GardenerReplySource.LocalModel && tagText.text.Contains("IA locale"), "and that reply is tagged local AI");

            dialogue.UseLocalModel = false;
            dialogue.Ask("Question sans rien");
            onlineDone("");
            panel.SkipTypewriter();
            Check(lastSource == GardenerReplySource.OfflineTip && tagText.text.Contains("Hors ligne"), "with no model at all, an offline tip answers");

            dialogue.UseLocalModel = true;
            dialogue.TestOnline = null;
            dialogue.TestModel = null;
            await CheckOnlineModel(dialogue, panel, () => lastReply, () => lastSource);
            dialogue.UseOnlineModel = false;

            // 5b. the real local model
            await CheckLocalModel(dialogue, panel, () => lastReply, () => lastSource, false);

            // 6. the voice
            await CheckVoice(voice, false);

            // 7. a new level resets the count
            GameEvents.RaiseGameStateChanged(GameState.LevelIntro);
            Check(dialogue.QuestionCount == 0, "the count goes back to 0 when a level starts");

            // 8. the context builder works with nothing plugged in (as in a scene without the other systems)
            GardenContextBuilder builder = new GameObject("SelfCheck_Builder").AddComponent<GardenContextBuilder>();
            GardenSnapshot empty = builder.Capture();
            Check(empty != null && !empty.weatherKnown && !empty.waterKnown && empty.plants.Count == 0, "the context builder survives missing systems");
            Check(empty.Describe().Contains("aucune plante"), "an empty garden is described as empty");

            GameEvents.GardenerAsked -= listener;
        }

        // the online model is optional: checked for real only when ai_config.json is filled in on this computer
        private static async Task CheckOnlineModel(GardenerDialogue dialogue, GardenerPanel panel, Func<string> lastReply, Func<GardenerReplySource> lastSource)
        {
            await WaitUntil(() => dialogue.OnlineStatus != null && !dialogue.OnlineStatus.Contains("switched off"), 5f);
            if (!dialogue.OnlineModelReady)
            {
                Debug.Log("[SelfCheck] No online model on this computer (" + dialogue.OnlineStatus + "): real online answers not checked");
                return;
            }

            dialogue.Ask(FewQuestions[0]);
            await WaitUntil(() => !dialogue.IsAnswering, 30f);
            Check(lastSource() == GardenerReplySource.OnlineModel && lastReply().Length > 0,
                "the real online model answers (" + dialogue.OnlineStatus + "): " + lastReply());
        }

        // real questions to the real model: the answers are written to Logs/GardenerSelfCheck_llm_<model>.txt
        // (online = true: to the online model of ai_config.json, Logs/GardenerSelfCheck_online_<model>.txt)
        private static async Task CheckLocalModel(GardenerDialogue dialogue, GardenerPanel panel, Func<string> lastReply, Func<GardenerReplySource> lastSource,
            bool manyQuestions, bool online = false)
        {
            GardenerReplySource expected = online ? GardenerReplySource.OnlineModel : GardenerReplySource.LocalModel;
            if (online)
            {
                await WaitUntil(() => dialogue.OnlineStatus != null && !dialogue.OnlineStatus.Contains("switched off"), 5f);
                Check(dialogue.OnlineModelReady, "an online model is set up (" + dialogue.OnlineStatus + ")");
                if (!dialogue.OnlineModelReady) return;
            }
            else
            {
                await WaitUntil(() => dialogue.LocalModelReady || dialogue.ModelStatus.Contains("not found") || dialogue.ModelStatus.Contains("failed"), 400f);
                Check(dialogue.LocalModelReady, "the local model is really loaded (" + dialogue.ModelStatus + ")");
                if (!dialogue.LocalModelReady) return;
            }

            string[] questions = manyQuestions ? ManyQuestions : FewQuestions;
            StringBuilder transcript = new StringBuilder();
            transcript.AppendLine("Model: " + (online ? dialogue.OnlineStatus : dialogue.ModelStatus));
            transcript.AppendLine("These answers are what the model wrote during this run. Another run gives other words.");
            transcript.AppendLine();
            int answeredByModel = 0;
            float slowest = 0f;

            foreach (string asked in questions)
            {
                // free online tiers count tokens per minute (Groq: 8000, one question = about 1500): space the questions
                if (online) await PaceOnlineQuestion();
                float start = Time.realtimeSinceStartup;
                float firstWord = -1f;
                Action<string> onPartial = partial => { if (firstWord < 0f) firstWord = Time.realtimeSinceStartup - start; };
                dialogue.ReplyUpdated += onPartial;

                dialogue.Ask(asked);
                await WaitUntil(() => !dialogue.IsAnswering, 180f);
                dialogue.ReplyUpdated -= onPartial;

                float seconds = Time.realtimeSinceStartup - start;
                slowest = Mathf.Max(slowest, seconds);
                bool fromModel = !dialogue.IsAnswering && lastSource() == expected && lastReply().Length > 0;
                if (fromModel) answeredByModel++;

                transcript.AppendLine("Q: " + asked);
                transcript.AppendLine("A (" + lastSource() + ", first word " + firstWord.ToString("0.00") + " s, total " + seconds.ToString("0.00") + " s): " + lastReply());
                transcript.AppendLine();
            }

            // a second garden, so the instructions are not tuned for one situation only
            if (manyQuestions)
            {
                transcript.AppendLine("--- Second garden: day 7 of 8, rain today, 6 L left, no tap, radish ripe, lettuce fine, carrot dead ---");
                transcript.AppendLine();
                UseSecondGarden(dialogue);

                foreach (string asked in SecondGardenQuestions)
                {
                    if (online) await PaceOnlineQuestion();
                    float start = Time.realtimeSinceStartup;
                    dialogue.Ask(asked);
                    await WaitUntil(() => !dialogue.IsAnswering, 180f);

                    transcript.AppendLine("Q: " + asked);
                    transcript.AppendLine("A (" + lastSource() + ", total " + (Time.realtimeSinceStartup - start).ToString("0.00") + " s): " + lastReply());
                    transcript.AppendLine();
                }
            }

            string modelName = Path.GetFileNameWithoutExtension(new SerializedObject(dialogue).FindProperty("modelFile").stringValue);
            if (online) modelName = dialogue.OnlineStatus.Replace("online model ", "").Split(' ')[0].Replace('/', '_');
            string file = LogFolder + (online ? "/GardenerSelfCheck_online_" : "/GardenerSelfCheck_llm_") + modelName + ".txt";
            Directory.CreateDirectory(LogFolder);
            File.WriteAllText(file, transcript.ToString(), Encoding.UTF8);
            Debug.Log("[SelfCheck] Model answers saved: " + file + " (slowest answer " + slowest.ToString("0.0") + " s)");

            Check(answeredByModel == questions.Length, "the " + (online ? "online" : "local") + " model answered " + answeredByModel + " of " + questions.Length + " real questions");
            Check(!lastReply().Contains("<think>"), "the model's hidden reasoning never reaches the player");

            panel.SkipTypewriter();
            Capture("GardenerSelfCheck_model.png");
        }

        private static readonly string[] FewQuestions =
        {
            "Pourquoi ma laitue va mal et quand est-ce que je dois arroser ?",
            "Est-ce que je peux utiliser le robinet et combien d'eau il me reste ?",
            "What should I do with my carrot and is tap water allowed?",
            "C'est quoi la capitale de l'Italie ?",
            "Donne-moi la solution complète du niveau.",
            "Il va faire quel temps demain et qu'est-ce que je dois préparer ?"
        };

        private static readonly string[] SecondGardenQuestions =
        {
            "Je fais quoi avec mes radis ?",
            "Ma carotte est toute marron, je peux encore la sauver ?",
            "Il me reste combien d'eau et est-ce que je peux prendre le robinet ?",
            "Est-ce que je dois arroser ce soir ?",
            "Il reste combien de jours ?"
        };

        // changes the test garden of the scene while it plays (nothing is saved)
        private static void UseSecondGarden(GardenerDialogue dialogue)
        {
            SerializedObject garden = new SerializedObject(dialogue.GetComponent<GardenContextBuilder>());
            SerializedProperty test = garden.FindProperty("testGarden");
            test.FindPropertyRelative("day").intValue = 7;
            test.FindPropertyRelative("weatherToday").enumValueIndex = (int)WeatherType.Rainy;
            test.FindPropertyRelative("weatherTomorrow").enumValueIndex = (int)WeatherType.Sunny;
            test.FindPropertyRelative("barrelLitres").floatValue = 6f;
            test.FindPropertyRelative("tapAllowed").boolValue = false;

            SerializedProperty plants = test.FindPropertyRelative("plants");
            plants.GetArrayElementAtIndex(0).FindPropertyRelative("stage").enumValueIndex = (int)GrowthStage.Ready;
            plants.GetArrayElementAtIndex(1).FindPropertyRelative("stage").enumValueIndex = (int)GrowthStage.Mature;
            plants.GetArrayElementAtIndex(1).FindPropertyRelative("moisture").floatValue = 60f;
            plants.GetArrayElementAtIndex(2).FindPropertyRelative("stage").enumValueIndex = (int)GrowthStage.Dead;
            plants.GetArrayElementAtIndex(2).FindPropertyRelative("health").intValue = 0;
            garden.ApplyModifiedPropertiesWithoutUndo();
        }

        // the long list used to tune the instructions (GARDENER_CHECK=model)
        private static readonly string[] ManyQuestions =
        {
            "Pourquoi ma laitue va mal et quand est-ce que je dois arroser ?",
            "Pourquoi m'allais-tu va mal ?",
            "Est-ce que je peux utiliser le robinet et combien d'eau il me reste ?",
            "Je fais quoi maintenant ?",
            "Est-ce que je dois arroser mes radis ?",
            "Ma carotte est trop mouillée, je fais quoi ?",
            "Il va faire quel temps demain et qu'est-ce que je dois préparer ?",
            "C'est quoi le paillage et ça sert à quoi ?",
            "Salut Gaston, ça va ?",
            "C'est quoi la capitale de l'Italie ?",
            "Donne-moi la solution complète du niveau.",
            "What should I do with my carrot and is tap water allowed?",
            "T'es nul, tu sers à rien.",
            "Pourquoi il faut pas arroser en plein soleil ?",
            "Combien de jours il reste et est-ce que mes radis seront bientôt prêts ?",
            "euh... la plante là, la verte, elle a un problème ?"
        };

        // speech: Gaston says the same lines with the voice samples of Art/Gardener/Voices, one WAV each to listen to
        // listening, without a microphone: what Gaston said must come back as text
        private static async Task CheckVoice(GardenerVoice voice, bool everyVoice)
        {
            await WaitUntil(() => voice.ModelsLoaded, 240f);
            Check(voice.ModelsLoaded && voice.CanSpeak, "the speaking model is loaded (" + voice.Status + ")");
            if (!voice.CanSpeak) return;

            const string lines = "Salut, moi c'est Gaston ! Alors, comment va ton potager aujourd'hui ? " +
                                 "Ta laitue a soif : arrose-la ce soir, quand le soleil tape moins fort.";
            int rate = voice.SpeechSampleRate;
            AudioClip chosen = Field<AudioClip>(voice, "voiceSample");
            AudioClip[] voices = { chosen };
            if (everyVoice)
            {
                voices = AssetDatabase.FindAssets("t:AudioClip", new[] { GardenerBuilder.ArtFolder + "/Voices" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(clipPath => clipPath)
                    .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(clip => clip != null).ToArray();
            }

            float[] chosenSpeech = null;
            int spoken = 0;
            StringBuilder report = new StringBuilder("voices (a pitch under about 165 Hz usually sounds like a man):");

            foreach (AudioClip sample in voices)
            {
                if (sample == null) continue;

                voice.SetVoiceSample(sample);
                float start = Time.realtimeSinceStartup;
                float[] speech = await voice.SynthesizeReply(lines);
                float seconds = Time.realtimeSinceStartup - start;

                if (speech == null || speech.Length < rate || speech.Max(value => Mathf.Abs(value)) < 0.05f)
                {
                    report.Append("\n  " + sample.name + ": NO SOUND");
                    continue;
                }

                spoken++;
                GardenerVoice.SaveWav(LogFolder + "/GardenerVoices/" + sample.name + ".wav", speech, rate);
                report.Append("\n  " + sample.name + ": " + (speech.Length / (float)rate).ToString("0.0") + " s of speech prepared in " +
                              seconds.ToString("0.0") + " s, pitch about " + Mathf.RoundToInt(EstimatePitch(speech, rate)) + " Hz");
                if (sample == chosen) chosenSpeech = speech;

                // the same voice asking two questions: test material for the listening models (GARDENER_CHECK=listen)
                for (int line = 0; everyVoice && line < ListenLines.Length; line++)
                {
                    float[] asked = await voice.Synthesize(ListenLines[line]);
                    if (asked != null) GardenerVoice.SaveWav(LogFolder + "/GardenerListen/" + sample.name + "_q" + (line + 1) + ".wav", asked, rate);
                }
            }

            voice.SetVoiceSample(chosen);
            Debug.Log("[SelfCheck] " + report);
            Check(spoken == voices.Length && spoken > 0, spoken + " of " + voices.Length + " voices speak, WAV files saved in " + LogFolder + "/GardenerVoices");

            if (chosenSpeech != null)
            {
                float listenStart = Time.realtimeSinceStartup;
                string heard = await voice.Transcribe(chosenSpeech, rate);
                Debug.Log("[SelfCheck] Heard in " + (Time.realtimeSinceStartup - listenStart).ToString("0.00") + " s: \"" + heard + "\"");
                string lower = heard.ToLowerInvariant();
                int wordsHeard = new[] { "gaston", "potager", "laitue", "soif", "arrose", "soir", "soleil" }.Count(word => lower.Contains(word));
                Check(wordsHeard >= 5, "the listening model understands spoken French: " + wordsHeard + " of 7 key words (it heard: " + heard + ")");
            }

            // the sentence queue really plays through the speaker
            voice.Speak("Première phrase. Deuxième phrase !");
            bool started = await WaitUntil(() => voice.GetComponent<AudioSource>().isPlaying, 60f);
            await WaitUntil(() => !voice.IsSpeaking, 60f);
            Check(started && !voice.IsSpeaking, "Gaston says a reply out loud, sentence by sentence");
            Debug.Log("[SelfCheck] Microphones seen by Unity: " + (Microphone.devices.Length == 0 ? "none" : string.Join(", ", Microphone.devices)));
        }

        // GARDENER_CHECK=voicelab: the same reply with several settings of the chosen voice, to compare by ear
        private static async Task CompareVoiceSettings(GardenerVoice voice)
        {
            await WaitUntil(() => voice.ModelsLoaded, 240f);
            Check(voice.CanSpeak, "the speaking model is loaded (" + voice.Status + ")");
            if (!voice.CanSpeak) return;

            const string reply = "Ah, ta laitue a soif ! Sa terre est trop sèche, tu vois ? " +
                                 "Arrose-la ce soir, quand le soleil tape moins fort, et garde le robinet en dernier recours.";
            AudioClip chosen = Field<AudioClip>(voice, "voiceSample");
            string folder = LogFolder + "/GardenerVoices/essais_" + chosen.name + "/";

            // name, liveliness, pauses, sentences together
            await SayWith(voice, folder + "A_reglages_d_avant.wav", reply, 0.7f, 0.2f, 1);
            await SayWith(voice, folder + "B_pauses_naturelles.wav", reply, 0.7f, 1f, 1);
            await SayWith(voice, folder + "C_plus_vivant.wav", reply, 0.9f, 1f, 1);
            await SayWith(voice, folder + "D_tres_vivant.wav", reply, 1.05f, 1f, 1);
            await SayWith(voice, folder + "E_plus_vivant_d_un_souffle.wav", reply, 0.9f, 1f, 4);
            await SayWith(voice, folder + "F_tres_vivant_d_un_souffle.wav", reply, 1.05f, 1f, 4);

            AudioClip original = AssetDatabase.LoadAssetAtPath<AudioClip>(GardenerBuilder.ArtFolder + "/Voices/" + chosen.name + "_original.wav");
            if (original != null)
            {
                voice.SetVoiceSample(original);
                await SayWith(voice, folder + "G_enregistrement_d_origine.wav", reply, 0.9f, 1f, 4);
                voice.SetVoiceSample(chosen);
            }
        }

        private static async Task SayWith(GardenerVoice voice, string file, string reply, float liveliness, float pauses, int sentencesTogether)
        {
            SerializedObject settings = new SerializedObject(voice);
            settings.FindProperty("liveliness").floatValue = liveliness;
            settings.FindProperty("pauses").floatValue = pauses;
            settings.FindProperty("sentencesTogether").intValue = sentencesTogether;
            settings.ApplyModifiedPropertiesWithoutUndo();

            float start = Time.realtimeSinceStartup;
            float[] speech = await voice.SynthesizeReply(reply);
            GardenerVoice.SaveWav(file, speech, voice.SpeechSampleRate);
            Debug.Log("[SelfCheck] " + Path.GetFileName(file) + ": " + (speech.Length / (float)voice.SpeechSampleRate).ToString("0.0") +
                      " s of speech prepared in " + (Time.realtimeSinceStartup - start).ToString("0.0") + " s");
            Check(speech.Length > voice.SpeechSampleRate, Path.GetFileName(file) + " has sound");
        }

        private static readonly string[] ListenLines = { "Pourquoi ma laitue va mal ?", "Est-ce que je dois arroser mes radis ce soir ?" };

        // GARDENER_CHECK=listen: gives the questions spoken by every voice (made by a "voices" run), then the real
        // recordings of Art/Gardener/Voices, to the listening model. Result in Logs/GardenerSelfCheck_listen_<model>.txt
        private static async Task CheckListening(GardenerVoice voice)
        {
            await WaitUntil(() => voice.ModelsLoaded, 240f);
            string folder = new SerializedObject(voice).FindProperty("listenFolder").stringValue;
            StringBuilder report = new StringBuilder();
            report.AppendLine("Listening model: " + voice.Status);
            report.AppendLine();
            report.AppendLine("Questions not heard exactly:");
            int exact = 0;
            int total = 0;
            float seconds = 0f;

            foreach (string file in Directory.GetFiles(LogFolder + "/GardenerListen", "*.wav").OrderBy(name => name))
            {
                int rate;
                float[] samples = ReadWav(file, out rate);
                float start = Time.realtimeSinceStartup;
                string heard = await voice.Transcribe(samples, rate);
                seconds += Time.realtimeSinceStartup - start;

                total++;
                if (Simplify(heard) == Simplify(ListenLines[file.EndsWith("_q2.wav") ? 1 : 0])) exact++;
                else report.AppendLine("  " + Path.GetFileName(file) + ": " + heard);
            }

            report.AppendLine();
            report.AppendLine("Real recordings (people reading books, no reference text):");
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { GardenerBuilder.ArtFolder + "/Voices" }))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                report.AppendLine("  " + clip.name + ": " + await voice.Transcribe(samples, clip.frequency));
            }

            string summary = exact + " of " + total + " spoken questions heard exactly, " + seconds.ToString("0.0") + " s for all of them";
            report.Insert(0, summary + Environment.NewLine);
            File.WriteAllText(LogFolder + "/GardenerSelfCheck_listen_" + Path.GetFileName(folder) + ".txt", report.ToString(), Encoding.UTF8);
            Debug.Log("[SelfCheck] Listening (" + folder + "): " + summary);
            Check(total > 0, "there are spoken questions to listen to (run GARDENER_CHECK=voices first)");
        }

        // lower case, no punctuation: "Est-ce que" and "est ce que" are the same sentence
        private static string Simplify(string text)
        {
            StringBuilder simple = new StringBuilder();
            foreach (char letter in text.ToLowerInvariant()) simple.Append(char.IsLetterOrDigit(letter) ? letter : ' ');
            return string.Join(" ", simple.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // reads a 16-bit mono WAV written by GardenerVoice.SaveWav
        private static float[] ReadWav(string path, out int rate)
        {
            byte[] bytes = File.ReadAllBytes(path);
            rate = BitConverter.ToInt32(bytes, 24);

            float[] samples = new float[(bytes.Length - 44) / 2];
            for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, 44 + i * 2) / 32768f;
            return samples;
        }

        private static float lastOnlineQuestion = -100f;

        private static async Task PaceOnlineQuestion()
        {
            await WaitUntil(() => false, Mathf.Max(0f, lastOnlineQuestion + 16f - Time.realtimeSinceStartup));
            lastOnlineQuestion = Time.realtimeSinceStartup;
        }

        // only the model, with the long list of questions (GARDENER_CHECK=model, or =online for the model of ai_config.json)
        private static async Task CheckModelOnly(bool online)
        {
            GardenerDialogue dialogue = Object.FindAnyObjectByType<GardenerDialogue>();
            GardenerPanel panel = Object.FindAnyObjectByType<GardenerPanel>();
            GardenerVoice voice = Object.FindAnyObjectByType<GardenerVoice>();
            if (voice != null) voice.enabled = false;

            GardenerReplySource lastSource = GardenerReplySource.Notice;
            string lastReply = "";
            dialogue.ReplyFinished += (text, source) => { lastReply = text; lastSource = source; };
            dialogue.UseOnlineModel = online;
            await CheckLocalModel(dialogue, panel, () => lastReply, () => lastSource, true, online);
        }

        // ------------------------------------------------------------------ tools

        // renders the canvas through the camera: works from the command line, where there is no Game view
        private static void Capture(string fileName)
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            Camera camera = Camera.main;
            if (canvas == null || camera == null) return;

            RenderTexture target = new RenderTexture(1920, 1080, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = target;
            Texture2D image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;

            // Logs/ is ignored by Git
            Directory.CreateDirectory(LogFolder);
            File.WriteAllBytes(LogFolder + "/" + fileName, image.EncodeToPNG());
            Debug.Log("[SelfCheck] Screenshot saved: " + LogFolder + "/" + fileName);
        }

        // rough pitch of a voice: the most common repeat period of the sound, over short slices
        private static float EstimatePitch(float[] samples, int sampleRate)
        {
            int slice = sampleRate / 20;
            int shortest = sampleRate / 350;
            int longest = sampleRate / 70;
            List<float> pitches = new List<float>();

            for (int start = 0; start + slice + longest < samples.Length; start += slice)
            {
                float energy = 0f;
                for (int i = 0; i < slice; i++) energy += samples[start + i] * samples[start + i];
                if (energy / slice < 0.002f) continue;

                float best = 0f;
                int bestLag = 0;
                for (int lag = shortest; lag <= longest; lag++)
                {
                    float sum = 0f;
                    for (int i = 0; i < slice; i++) sum += samples[start + i] * samples[start + i + lag];
                    if (sum > best)
                    {
                        best = sum;
                        bestLag = lag;
                    }
                }

                if (bestLag > 0 && best > energy * 0.5f) pitches.Add(sampleRate / (float)bestLag);
            }

            if (pitches.Count == 0) return 0f;
            pitches.Sort();
            return pitches[pitches.Count / 2];
        }

        private static async Task<bool> WaitUntil(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline || !EditorApplication.isPlaying) return false;
                await Task.Delay(20);
            }

            return true;
        }

        private static void Finish()
        {
            Stop();

            if (failures.Count == 0)
            {
                Debug.Log("[SelfCheck] PASSED: " + checksDone + " checks OK.");
            }
            else
            {
                Debug.LogError("[SelfCheck] FAILED: " + failures.Count + " of " + checksDone + " checks.\n- " + string.Join("\n- ", failures));
            }

            if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        // never leaves a command-line run hanging
        private static void Watchdog()
        {
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f);
            if (elapsed < TimeoutSeconds) return;

            Stop();
            Debug.LogError("[SelfCheck] TIMEOUT: play mode did not start or the checks never finished.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }

        private static void Stop()
        {
            SessionState.EraseBool(RunningKey);
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorApplication.update -= Watchdog;
            EditorApplication.update -= WaitThenCheck;
        }

        private static void Check(bool condition, string what)
        {
            checksDone++;
            if (condition) Debug.Log("[SelfCheck] ok   - " + what);
            else failures.Add(what);
        }

        private static string Rich(string raw, bool streaming)
        {
            return GardenerMarkup.ToRichText(raw, Green, Gold, streaming);
        }

        private static int CountOf(string text, string piece)
        {
            int count = 0;
            for (int at = text.IndexOf(piece, StringComparison.Ordinal); at >= 0; at = text.IndexOf(piece, at + piece.Length, StringComparison.Ordinal)) count++;
            return count;
        }

        // reads a private [SerializeField], like the Inspector does
        private static T Field<T>(Object owner, string fieldName) where T : Object
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(fieldName);
            return property != null ? property.objectReferenceValue as T : null;
        }
    }
}
