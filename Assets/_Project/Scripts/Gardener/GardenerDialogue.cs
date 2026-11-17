using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;
using UnityEngine.Networking;

namespace GardenWise
{
    public enum GardenerLanguage
    {
        French,
        English
    }

    // where a reply comes from: the UI always shows it, a pre-written tip never passes for the AI
    public enum GardenerReplySource
    {
        LocalModel,
        OfflineTip,
        Notice,
        OnlineModel
    }

    // owner 5 - the single entry point to talk to Gaston, whatever the input (keyboard, voice...)
    // each question is independent: fresh garden context, no history, no limit
    // the answer comes, in this order, from an online model (only when ai_config.json is filled in and the internet works),
    // the local model (LLMUnity, no server, no key), or offline tips when neither can answer
    [DefaultExecutionOrder(-10)] // Awake must run before LLMUnity's components, see Awake
    public class GardenerDialogue : MonoBehaviour
    {
        [Header("Garden")]
        [SerializeField] private GardenContextBuilder garden;

        [Header("Local model")]
        [SerializeField] private bool useLocalModel = true;
        [SerializeField] private LLM llm;
        [SerializeField] private LLMAgent agent;
        [Tooltip("GGUF file. Looked for in LLMUnity's models folder (LLMUnitySetup.modelDownloadPath: AppData/Roaming/LLMUnity/models on Windows, ~/.config/LLMUnity/models on macOS), then in StreamingAssets.")]
        [SerializeField] private string modelFile = "gemma-3-4b-it-Q4_K_M.gguf";

        [Header("Online model (first choice when its file is filled in)")]
        [SerializeField] private bool useOnlineModel = true;
        [Tooltip("File in StreamingAssets, never in Git: apiUrl, apiKey, model of any OpenAI-compatible service (Groq...). " +
                 "Copy ai_config.example.json. Missing or not filled in: Gaston uses the local model.")]
        [SerializeField] private string onlineConfigFile = "ai_config.json";
        [Tooltip("Seconds to wait for the online model before the local model answers instead.")]
        [SerializeField] private float onlineTimeout = 8f;
        [Tooltip("After a refusal (daily quota reached, too many requests), the online model is left aside for this long.")]
        [SerializeField] private float onlinePause = 120f;

        // rules and a few examples of style, no list of game commands: the model answers freely from the garden state
        // (in French like the players; a small model follows examples much better than rules alone)
        [TextArea(10, 30)]
        [SerializeField] private string instructions =
            "Tu es Gaston, le maître jardinier du jeu GardenWise. Tu parles à un joueur débutant, souvent un élève de 11 à 14 ans.\n" +
            "Ton caractère : amical, complice, enthousiaste, un peu drôle, jamais moqueur. Tu tutoies toujours le joueur.\n" +
            "\n" +
            "Règles :\n" +
            "- Réponds dans la langue de la question du joueur.\n" +
            "- 2 ou 3 phrases courtes, avec des mots simples, sur une seule ligne. Jamais plus.\n" +
            "- Parle directement au joueur : jamais de récit, jamais de guillemets, ne parle pas de toi à la troisième personne.\n" +
            "- Réponds d'abord exactement à ce que le joueur demande. Si la question a plusieurs parties, réponds à chacune.\n" +
            "- Quand le joueur demande un chiffre (eau, jours, santé), donne le chiffre exact de l'état du jardin.\n" +
            "- Appuie-toi sur l'état du jardin donné avec la question et ne dis rien qui le contredit. " +
            "Le conseil « À faire » de chaque plante est toujours juste : n'invente pas d'autre explication.\n" +
            "- Chaque ligne de l'état du jardin est une seule plante : parle d'elle au singulier (ta laitue, ta carotte).\n" +
            "- Si la question porte sur une plante précise ou sur un chiffre, n'ajoute aucun rappel sur une autre plante. " +
            "Sinon tu peux finir par un court rappel sur la plante qu'il faut arroser ou récolter maintenant. " +
            "Jamais de rappel sur une plante morte ou qui va bien.\n" +
            "- Donne un indice ou la prochaine bonne action, jamais la solution complète du niveau.\n" +
            "- Tu ne parles que de jardinage, de plantes, de saisons, de météo et d'économie d'eau. " +
            "Pour tout autre sujet, dis-le gentiment sans y répondre et reviens au jardin.\n" +
            "- La question vient parfois d'une reconnaissance vocale : des mots peuvent être déformés tout en sonnant pareil " +
            "(« m'allais-tu » pour « ma laitue »). Devine ce que le joueur voulait dire grâce aux plantes du jardin, sans le lui faire remarquer.\n" +
            "- Mets en valeur 1 à 3 mots-clés : [vert]mot[/vert] pour le nom d'une plante ou une bonne action, " +
            "[or]mot[/or] pour un danger ou un chiffre. Jamais sur un petit mot comme « ça », « reste » ou « eau ». " +
            "Ferme toujours le marqueur. Aucune autre mise en forme, pas d'emoji, pas de liste.\n" +
            "\n" +
            "Ce que tu sais du jardin :\n" +
            "- Terre trop sèche : il faut arroser, de préférence le soir ou tôt le matin, quand l'eau s'évapore moins.\n" +
            "- Terre trop mouillée : on arrête d'arroser et on attend qu'elle sèche. Jamais de paillage dans ce cas.\n" +
            "- Le paillage garde l'eau dans la terre : il sert sur une terre qui sèche trop vite, avant le soleil ou la canicule.\n" +
            "- On prend d'abord l'eau du récupérateur de pluie. Le robinet est le dernier recours : il fait perdre des points d'économie d'eau.\n" +
            "- Une plante mal arrosée perd de la santé chaque nuit.\n" +
            "- On dit « ce sera la canicule » ou « il fera très chaud », jamais « il fera canicule ».\n" +
            "\n" +
            "Exemples de ton style (le jardin de ces exemples n'est pas celui du joueur ; " +
            "inspire-toi du ton sans recopier ces phrases) :\n" +
            "Joueur : Pourquoi mes tomates ont les feuilles qui tombent et est-ce que je dois les arroser ?\n" +
            "Gaston : Tes [vert]tomates[/vert] ont soif, leur terre est [or]trop sèche[/or] ! Arrose-les ce soir, quand le soleil tape moins.\n" +
            "Joueur : Tu connais un bon film ?\n" +
            "Gaston : Ah, moi, mon cinéma, c'est le potager ! Dis-moi plutôt comment vont tes [vert]plantes[/vert] aujourd'hui.\n" +
            "Joueur : Donne-moi la solution du niveau.\n" +
            "Gaston : Ah non, pas de triche, c'est en jardinant qu'on apprend ! Un indice quand même : regarde quelle [vert]plante[/vert] a la terre la plus [or]sèche[/or].\n" +
            "Joueur : What is mulch for?\n" +
            "Gaston : Mulch is a blanket for the soil: it keeps the water in, so you water less. Put some around your [vert]plants[/vert] before a [or]hot day[/or]!";

        [Tooltip("The reply is cut after this many sentences, whatever the model writes.")]
        [SerializeField] private int maxSentences = 3;

        [Header("Offline tips (when no model can answer)")]
        [TextArea]
        [SerializeField] private string[] frenchTips =
        {
            "Arrose le matin ou le soir, pas en plein midi : l'eau s'évapore moins.",
            "Utilise d'abord l'eau du récupérateur de pluie. Le robinet, c'est en dernier recours.",
            "Le paillage garde la terre humide plus longtemps, donc tu arroses moins.",
            "Trop d'eau, c'est aussi mauvais que pas assez : les racines peuvent pourrir.",
            "Regarde la météo de demain avant d'arroser : s'il pleut, la pluie fera le travail."
        };

        [TextArea]
        [SerializeField] private string[] englishTips =
        {
            "Water in the morning or the evening, not at midday: less water evaporates.",
            "Use the rain barrel first. Tap water should be the last option.",
            "Mulch keeps the soil wet longer, so you need less water.",
            "Too much water is as bad as too little: the roots can rot.",
            "Check tomorrow's weather before watering: if it rains, the rain does the job."
        };

        [Header("Settings")]
        [SerializeField] private GardenerLanguage language = GardenerLanguage.French;
        [SerializeField] private bool logToConsole = true;

        // local events for Gaston's own UI and voice (the other systems only listen to GameEvents.GardenerAsked)
        public event Action<string> QuestionAsked;
        public event Action<string> ReplyUpdated;
        public event Action<string, GardenerReplySource> ReplyFinished;

        // only for the self-check: stands in for the local model (message, onPartial, onDone)
        public Action<string, Action<string>, Action<string>> TestModel;

        // only for the self-check: stands in for the online model (message, onDone with null when it fails)
        public Action<string, Action<string>> TestOnline;

        public GardenerLanguage Language
        {
            get { return language; }
        }

        // questions asked since the level started
        public int QuestionCount { get; private set; }
        public bool IsAnswering { get; private set; }

        // one line for the Console and the tests: is the model really loaded?
        public string ModelStatus { get; private set; }

        public bool UseLocalModel
        {
            get { return useLocalModel; }
            set { useLocalModel = value; }
        }

        public bool UseOnlineModel
        {
            get { return useOnlineModel; }
            set { useOnlineModel = value; }
        }

        // one line for the Console and the tests: is the online model set up?
        public string OnlineStatus { get; private set; }

        public bool OnlineModelReady
        {
            get
            {
                if (!useOnlineModel) return false;
                if (TestOnline != null) return true;
                return online != null && Time.realtimeSinceStartup >= onlinePausedUntil &&
                       Application.internetReachability != NetworkReachability.NotReachable;
            }
        }

        public bool LocalModelReady
        {
            get
            {
                if (!useLocalModel) return false;
                if (TestModel != null) return true;
                return modelFound && warmedUp && llm != null && llm.started && !llm.failed && agent != null && agent.llmAgent != null;
            }
        }

        private bool modelFound;
        private bool warmUpStarted;
        private bool warmedUp;
        private bool readyLogged;
        private bool askedLocalModel;
        private int requestId;
        private int nextTip;
        private OnlineConfig online;
        private float onlinePausedUntil;
        private UnityWebRequest onlineRequest;

        // the content of ai_config.json
        [Serializable]
        private class OnlineConfig
        {
            public string apiUrl;
            public string apiKey;
            public string model;
            public string reasoningEffort; // optional, only for the models that think first (gpt-oss: "low")
        }

        [Serializable]
        private class OnlineResponse
        {
            public OnlineChoice[] choices;
            public OnlineError error;
        }

        [Serializable]
        private class OnlineChoice
        {
            public OnlineMessage message;
        }

        [Serializable]
        private class OnlineMessage
        {
            public string content;
        }

        [Serializable]
        private class OnlineError
        {
            public string message;
        }

        // runs before LLMUnity starts: give it the model file, or switch it off when the file is not on this PC
        private void Awake()
        {
            ModelStatus = "local model switched off";
            if (!useLocalModel || llm == null || agent == null) return;

            string path = FindModelFile();
            modelFound = path != null;

            if (!modelFound)
            {
                llm.gameObject.SetActive(false);
                ModelStatus = "model file " + modelFile + " not found, Gaston uses the offline tips";
                Debug.LogWarning("[Gaston] " + ModelStatus + ". Put it in " + LLMUnitySetup.modelDownloadPath);
                return;
            }

            llm.SetModel(path);
            agent.systemPrompt = instructions;
            ModelStatus = "loading " + modelFile;
        }

        private void Start()
        {
            OnlineStatus = "online model switched off";
            if (useOnlineModel) StartCoroutine(LoadOnlineConfig());
        }

        // on Android (Quest) StreamingAssets is inside the package: it can only be read with a web request
        private IEnumerator LoadOnlineConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, onlineConfigFile);
            string json = null;

            if (path.Contains("://"))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success) json = request.downloadHandler.text;
                }
            }
            else if (File.Exists(path))
            {
                json = File.ReadAllText(path);
            }

            OnlineConfig config = null;
            try
            {
                if (json != null) config = JsonUtility.FromJson<OnlineConfig>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Gaston] " + onlineConfigFile + " cannot be read: " + exception.Message);
            }

            if (config == null || IsBlank(config.apiUrl) || IsBlank(config.apiKey) || IsBlank(config.model))
            {
                OnlineStatus = "no online model (" + onlineConfigFile + " missing or not filled in), Gaston uses the local model";
                if (logToConsole) Debug.Log("[Gaston] " + OnlineStatus);
                yield break;
            }

            online = config;
            OnlineStatus = "online model " + config.model + " (" + new Uri(config.apiUrl).Host + ")";
            if (logToConsole) Debug.Log("[Gaston] Online model ready: " + OnlineStatus);
        }

        private static bool IsBlank(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.StartsWith("PASTE_", StringComparison.Ordinal);
        }

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
            CancelCurrentAnswer();
        }

        private void Update()
        {
            if (readyLogged || !modelFound) return;

            if (llm.failed)
            {
                readyLogged = true;
                ModelStatus = "the local model failed to start, Gaston uses the offline tips";
                Debug.LogWarning("[Gaston] " + ModelStatus);
            }
            else if (!warmUpStarted && llm.started && agent.llmAgent != null)
            {
                WarmUp();
            }
            else if (LocalModelReady)
            {
                readyLogged = true;
                ModelStatus = modelFile + " loaded in " + Time.realtimeSinceStartup.ToString("0.0") + " s (" + llm.architecture + ")";
                if (logToConsole) Debug.Log("[Gaston] Local model ready: " + ModelStatus);
            }
        }

        // the very first run of a model is slow (it reads the instructions, the graphics card gets ready):
        // do it once at start, so the player's first question is not the one that waits
        private async void WarmUp()
        {
            warmUpStarted = true;
            ModelStatus = "warming up " + modelFile;

            try
            {
                await agent.Warmup();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Gaston] Warm-up failed: " + exception.Message);
            }

            warmedUp = true;
        }

        public void Ask(string question)
        {
            question = question == null ? "" : question.Trim();
            if (question.Length == 0) return;

            // a new question replaces the answer still being written
            CancelCurrentAnswer();

            string context = garden != null ? garden.Describe() : "État du jardin : inconnu.\n";

            QuestionCount++;
            IsAnswering = true;
            requestId++;

            if (logToConsole) Debug.Log("[Gaston] Question " + QuestionCount + ": " + question + "\n" + context);

            GameEvents.RaiseGardenerAsked(question);
            QuestionAsked?.Invoke(question);

            // a small model forgets the "same language" rule: tell it which one, right before the question
            string answerIn = LooksEnglish(question) ? "Answer in English." : "Réponds en français.";

            string message = context + "\n" + answerIn + "\nQuestion du joueur : " + question;
            if (OnlineModelReady) AskOnlineModel(message, requestId);
            else AskLocalModelOrTip(message, requestId);
        }

        private void AskLocalModelOrTip(string message, int id)
        {
            if (LocalModelReady) AskLocalModel(message, id);
            else GiveOfflineTip(id);
        }

        // rough guess from very common words, enough to tell English from French
        private static bool LooksEnglish(string text)
        {
            string[] english = { "the", "what", "how", "why", "should", "is", "are", "my", "do", "does", "can", "with", "water", "when", "which" };
            string[] french = { "le", "la", "les", "est", "que", "quoi", "je", "mon", "ma", "mes", "pourquoi", "comment", "quand", "dois", "eau" };

            int score = 0;
            foreach (string word in text.ToLowerInvariant().Split(' ', '\'', '?', '!', ',', '.', '-'))
            {
                if (Array.IndexOf(english, word) >= 0) score++;
                if (Array.IndexOf(french, word) >= 0) score--;
            }

            return score > 0;
        }

        // the whole reply at once (no streaming): these services answer in about a second
        private async void AskOnlineModel(string message, int id)
        {
            float start = Time.realtimeSinceStartup;

            if (TestOnline != null)
            {
                TestOnline(message, text => FinishOnlineAnswer(id, message, text, start));
                return;
            }

            string reply = await PostToOnlineModel(message);
            if (this == null) return;
            FinishOnlineAnswer(id, message, reply, start);
        }

        private async Task<string> PostToOnlineModel(string message)
        {
            StringBuilder body = new StringBuilder();
            body.Append("{\"model\":").Append(JsonText(online.model));
            body.Append(",\"messages\":[{\"role\":\"system\",\"content\":").Append(JsonText(instructions));
            body.Append("},{\"role\":\"user\",\"content\":").Append(JsonText(message)).Append("}]");
            body.Append(",\"temperature\":0.4,\"max_tokens\":1024");
            if (!string.IsNullOrWhiteSpace(online.reasoningEffort)) body.Append(",\"reasoning_effort\":").Append(JsonText(online.reasoningEffort));
            body.Append('}');

            UnityWebRequest request = new UnityWebRequest(online.apiUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + online.apiKey);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(onlineTimeout));
            onlineRequest = request;

            try
            {
                UnityWebRequestAsyncOperation sending = request.SendWebRequest();
                while (!sending.isDone) await Task.Yield();

                string text = request.downloadHandler != null ? request.downloadHandler.text : null;
                OnlineResponse response = null;
                try
                {
                    if (!string.IsNullOrEmpty(text)) response = JsonUtility.FromJson<OnlineResponse>(text);
                }
                catch (Exception)
                {
                    // not JSON (a proxy page, a cut answer): treated as a failure below
                }

                if (request.result == UnityWebRequest.Result.Success && response != null && response.choices != null &&
                    response.choices.Length > 0 && response.choices[0].message != null)
                    return response.choices[0].message.content;

                // 401/403: wrong key, 429: quota or too many requests. Leave the service aside for a while
                long code = request.responseCode;
                if (code == 401 || code == 403 || code == 429) onlinePausedUntil = Time.realtimeSinceStartup + onlinePause;
                string reason = response != null && response.error != null ? response.error.message : request.error;
                bool cancelled = onlineRequest != request; // a newer question replaced this one
                if (!cancelled)
                    Debug.LogWarning("[Gaston] The online model could not answer (" + code + " " + reason + "), the local model answers instead");
                return null;
            }
            finally
            {
                if (onlineRequest == request) onlineRequest = null;
                request.Dispose();
            }
        }

        private void FinishOnlineAnswer(int id, string message, string reply, float start)
        {
            if (id != requestId) return;

            bool cut;
            reply = KeepFirstSentences(RemoveThinking(reply), maxSentences, out cut);
            if (reply.Length == 0)
            {
                // no internet, quota reached, empty answer: the local model takes over, then the tips
                AskLocalModelOrTip(message, id);
                return;
            }

            if (logToConsole)
                Debug.Log("[Gaston] Online model: " + reply.Length + " characters in " + (Time.realtimeSinceStartup - start).ToString("0.00") + " s");

            Finish(id, reply, GardenerReplySource.OnlineModel);
        }

        // a C# string as a JSON string, quotes included
        private static string JsonText(string value)
        {
            StringBuilder json = new StringBuilder("\"");
            foreach (char letter in value ?? "")
            {
                switch (letter)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    default:
                        if (letter < ' ') json.Append("\\u").Append(((int)letter).ToString("x4"));
                        else json.Append(letter);
                        break;
                }
            }

            return json.Append('"').ToString();
        }

        private async void AskLocalModel(string message, int id)
        {
            float start = Time.realtimeSinceStartup;
            float firstWord = -1f;
            string reply = null;
            string replySoFar = "";
            bool longEnough = false;
            askedLocalModel = true;

            Action<string> onPartial = partial =>
            {
                // an old answer must never write over a newer question
                if (id != requestId || longEnough) return;
                if (firstWord < 0f) firstWord = Time.realtimeSinceStartup - start;

                replySoFar = KeepFirstSentences(RemoveThinking(partial), maxSentences, out longEnough);
                ReplyUpdated?.Invoke(replySoFar);

                // the model wants to keep talking: stop it there
                if (longEnough && TestModel == null) agent.CancelRequests();
            };

            try
            {
                if (TestModel != null)
                {
                    TestModel(message, onPartial, text => FinishLocalAnswer(id, text, start, firstWord));
                    return;
                }

                // addToHistory = false: the model never sees the previous questions
                agent.systemPrompt = instructions;
                reply = await agent.Chat(message, onPartial, null, false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Gaston] The local model could not answer: " + exception.Message);
            }

            if (this == null) return;
            FinishLocalAnswer(id, longEnough || string.IsNullOrWhiteSpace(reply) ? replySoFar : reply, start, firstWord);
        }

        private void FinishLocalAnswer(int id, string reply, float start, float firstWord)
        {
            if (id != requestId) return;

            bool cut;
            reply = KeepFirstSentences(RemoveThinking(reply), maxSentences, out cut);
            if (reply.Length == 0)
            {
                // the model failed: fall back to a tip, and the UI says it is one
                GiveOfflineTip(id);
                return;
            }

            if (logToConsole)
            {
                float total = Time.realtimeSinceStartup - start;
                Debug.Log("[Gaston] Local model: first word after " + Mathf.Max(firstWord, 0f).ToString("0.00") + " s, " +
                          reply.Length + " characters in " + total.ToString("0.00") + " s");
            }

            Finish(id, reply, GardenerReplySource.LocalModel);
        }

        // pre-written, does not read the question: always shown as an offline tip
        private void GiveOfflineTip(int id)
        {
            string[] tips = language == GardenerLanguage.English ? englishTips : frenchTips;
            if (tips == null || tips.Length == 0)
            {
                Finish(id, language == GardenerLanguage.English ? "Gaston has no answer right now." : "Gaston n'a pas de réponse pour l'instant.",
                    GardenerReplySource.Notice);
                return;
            }

            string tip = tips[nextTip % tips.Length];
            nextTip++;
            Finish(id, tip, GardenerReplySource.OfflineTip);
        }

        private void Finish(int id, string reply, GardenerReplySource source)
        {
            if (id != requestId) return;

            IsAnswering = false;
            askedLocalModel = false;

            if (logToConsole) Debug.Log("[Gaston] Reply (" + source + "): " + reply);
            ReplyFinished?.Invoke(reply, source);
        }

        private void CancelCurrentAnswer()
        {
            if (!IsAnswering) return;

            requestId++;
            IsAnswering = false;
            if (askedLocalModel && TestModel == null && agent != null) agent.CancelRequests();
            askedLocalModel = false;
            UnityWebRequest pending = onlineRequest;
            onlineRequest = null;
            if (pending != null) pending.Abort();
        }

        // every level starts with a fresh count, like the ScoreManager
        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.LevelIntro) return;

            CancelCurrentAnswer();
            QuestionCount = 0;
        }

        private string FindModelFile()
        {
            string[] places =
            {
                LLM.GetLLMManagerAssetRuntime(modelFile),
                Path.Combine(LLMUnitySetup.modelDownloadPath, modelFile),
                Path.Combine(Application.streamingAssetsPath, modelFile),
                Path.Combine(Application.persistentDataPath, modelFile)
            };

            foreach (string place in places)
            {
                if (!string.IsNullOrEmpty(place) && File.Exists(place)) return place.Replace('\\', '/');
            }

            return null;
        }

        // keeps the first sentences of a text; cut = there was more after them
        private static string KeepFirstSentences(string text, int count, out bool cut)
        {
            cut = false;
            if (count <= 0) return text;

            int sentences = 0;
            int sentenceStart = 0;
            for (int i = 0; i < text.Length - 1; i++)
            {
                char letter = text[i];
                bool endsSentence = letter == '.' || letter == '!' || letter == '?' || letter == '…';
                if (!endsSentence || !char.IsWhiteSpace(text[i + 1])) continue;

                // "Ah !" or "Oui." is not a sentence of its own
                if (i - sentenceStart < 15) continue;

                sentenceStart = i + 1;
                sentences++;
                if (sentences < count) continue;

                cut = true;
                return text.Substring(0, i + 1);
            }

            return text;
        }

        // some models write their reasoning between <think> tags before the answer: the player never sees it
        private static string RemoveThinking(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            int end = text.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (end >= 0) text = text.Substring(end + "</think>".Length);
            else if (text.TrimStart().StartsWith("<think>", StringComparison.OrdinalIgnoreCase)) return "";

            // and some start with their own name, as in the examples ("Gaston : ...")
            text = text.Trim();
            int colon = text.IndexOf(':');
            if (colon >= 6 && colon <= 8 && text.StartsWith("Gaston", StringComparison.OrdinalIgnoreCase)) text = text.Substring(colon + 1);

            // one line only, whatever the model writes
            return string.Join(" ", text.Split(new[] { '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
