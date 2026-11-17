using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SherpaOnnx;
using UnityEngine;

namespace GardenWise
{
    // owner 5 - Gaston's ears and voice, both on the device (sherpa-onnx, no server):
    // microphone -> text -> GardenerDialogue.Ask, and reply -> speech
    // the model files sit in StreamingAssets; without them the voice simply stays off, the rest of Gaston works
    public class GardenerVoice : MonoBehaviour
    {
        public enum VoiceState
        {
            Idle,
            Listening,
            Transcribing
        }

        [SerializeField] private GardenerDialogue dialogue;
        [SerializeField] private AudioSource speaker;
        [Tooltip("Processor threads for each voice model.")]
        [SerializeField] private int threads = 6;

        [Header("Listening")]
        [Tooltip("Folder in StreamingAssets. Parakeet (encoder / decoder / joiner .int8.onnx + tokens.txt) " +
                 "or Whisper (<name>-encoder.int8.onnx, <name>-decoder.int8.onnx, <name>-tokens.txt).")]
        [SerializeField] private string listenFolder = "Gardener/stt-parakeet";
        [Tooltip("Name of the microphone to use. Empty = the default microphone of the computer.")]
        [SerializeField] private string microphone = "";
        [SerializeField] private int maxListenSeconds = 20;
        [Tooltip("In the editor, keeps the last recording in Logs/GardenerLastRecording.wav to hear what the microphone really caught.")]
        [SerializeField] private bool keepLastRecording = true;

        [Header("Speaking (Pocket TTS, by Kyutai)")]
        [Tooltip("Folder in StreamingAssets with lm_main.int8.onnx, lm_flow.int8.onnx, encoder.onnx, decoder.int8.onnx...")]
        [SerializeField] private string speakFolder = "Gardener/tts-pocket";
        [SerializeField] private bool speakReplies = true;
        [Tooltip("Gaston speaks with the voice of this short recording (about 10 s of one person talking). " +
                 "Drag another clip here to change his voice.")]
        [SerializeField] private AudioClip voiceSample;
        [Tooltip("How lively the voice is. Low = flat but steady, high = more intonation, sometimes a strange word.")]
        [Range(0.4f, 1.2f)] [SerializeField] private float liveliness = 0.7f;
        [Tooltip("1 = the natural pauses of the voice. Lower = shorter pauses, faster delivery.")]
        [Range(0.2f, 1f)] [SerializeField] private float pauses = 1f;
        [Tooltip("How many sentences Gaston prepares in one go. 1 = he starts talking sooner, more = the reply flows better.")]
        [Range(1, 4)] [SerializeField] private int sentencesTogether = 1;

        public VoiceState State { get; private set; }

        // why the last recording gave nothing, in the player's language ("" when all is well)
        public string LastProblem { get; private set; } = "";

        // one line for the Console and the tests: are the models really loaded?
        public string Status { get; private set; } = "voice loading";

        public bool ModelsLoaded { get; private set; }

        public bool CanListen
        {
            get { return recognizer != null && Microphone.devices.Length > 0; }
        }

        public bool CanSpeak
        {
            get { return tts != null && sampleAudio != null; }
        }

        public bool IsSpeaking
        {
            get { return speaker.isPlaying || preparing || sentencesToSay.Count > 0 || clipsToPlay.Count > 0; }
        }

        // samples per second of what Synthesize returns (given by the voice model)
        public int SpeechSampleRate { get; private set; } = 24000;

        // null = the default microphone
        private string Device
        {
            get { return string.IsNullOrEmpty(microphone) ? null : microphone; }
        }

        private static readonly Regex Tags = new Regex(@"<[^>]{1,24}>");
        private static readonly OfflineTtsCallbackProgressWithArg KeepGoing = OnSpeechProgress;

        private readonly object listenLock = new object();
        private readonly object speakLock = new object();
        private readonly Queue<string> sentencesToSay = new Queue<string>();
        private readonly Queue<AudioClip> clipsToPlay = new Queue<AudioClip>();

        private OfflineRecognizer recognizer;
        private OfflineTts tts;
        private float[] sampleAudio;
        private int sampleRate;
        private AudioClip recording;
        private AudioClip playingClip;
        private int spokenCharacters;
        private int speechSession;
        private bool preparing;
        private bool destroyed;

        private void OnEnable()
        {
            dialogue.QuestionAsked += HandleQuestionAsked;
            dialogue.ReplyUpdated += HandleReplyUpdated;
            dialogue.ReplyFinished += HandleReplyFinished;
        }

        private void OnDisable()
        {
            dialogue.QuestionAsked -= HandleQuestionAsked;
            dialogue.ReplyUpdated -= HandleReplyUpdated;
            dialogue.ReplyFinished -= HandleReplyFinished;
        }

        private void Start()
        {
            SetVoiceSample(voiceSample);
            LoadModels();
        }

        private void Update()
        {
            // the recording stops by itself after maxListenSeconds
            if (State == VoiceState.Listening && !Microphone.IsRecording(Device)) StopListening();

            if (!preparing && sentencesToSay.Count > 0) PrepareNextSentence();

            if (!speaker.isPlaying && clipsToPlay.Count > 0)
            {
                if (playingClip != null) Destroy(playingClip);
                playingClip = clipsToPlay.Dequeue();
                speaker.clip = playingClip;
                speaker.Play();
            }
        }

        private void OnDestroy()
        {
            destroyed = true;
            if (Microphone.IsRecording(Device)) Microphone.End(Device);

            // the locks wait for a transcription or a sentence still being computed
            lock (listenLock)
            {
                if (recognizer != null) recognizer.Dispose();
                recognizer = null;
            }

            lock (speakLock)
            {
                if (tts != null) tts.Dispose();
                tts = null;
            }
        }

        // ------------------------------------------------------------------ loading

        // the models load on another thread so the game does not freeze
        private async void LoadModels()
        {
            string listenPath = Path.Combine(Application.streamingAssetsPath, listenFolder);
            string speakPath = Path.Combine(Application.streamingAssetsPath, speakFolder);
            string language = dialogue.Language == GardenerLanguage.English ? "en" : "fr";
            float start = Time.realtimeSinceStartup;
            string problem = "";

            try
            {
                PreloadOnnxRuntime();

                if (Directory.Exists(speakPath))
                {
                    OfflineTts loaded = await Task.Run(() => CreateTts(speakPath));
                    if (destroyed) loaded.Dispose();
                    else
                    {
                        tts = loaded;
                        SpeechSampleRate = loaded.SampleRate;
                    }
                }
                else problem += " No speaking model in " + speakPath + ".";

                if (Directory.Exists(listenPath))
                {
                    OfflineRecognizer loaded = await Task.Run(() => CreateRecognizer(listenPath, language));
                    if (destroyed) loaded.Dispose();
                    else recognizer = loaded;
                }
                else problem += " No listening model in " + listenPath + ".";
            }
            catch (Exception exception)
            {
                // library missing on this platform, or a broken model file: Gaston stays silent but keeps working
                problem += " " + exception.GetType().Name + ": " + exception.Message;
            }

            if (destroyed) return;
            if (tts != null && sampleAudio == null) problem += " No voice sample on GardenerVoice: Gaston cannot speak.";

            ModelsLoaded = true;
            Status = "speaking " + (CanSpeak ? "ready (" + speakFolder + ")" : "off") +
                     ", listening " + (recognizer != null ? "ready (" + listenFolder + ")" : "off") +
                     ", microphones: " + Microphone.devices.Length +
                     " (loaded in " + (Time.realtimeSinceStartup - start).ToString("0.0") + " s)" + problem;

            if (problem.Length > 0) Debug.LogWarning("[Gaston] Voice: " + Status);
            else Debug.Log("[Gaston] Voice: " + Status);
        }

        private OfflineTts CreateTts(string folder)
        {
            OfflineTtsConfig config = new OfflineTtsConfig();
            config.Model.NumThreads = threads;
            config.Model.Provider = "cpu";

            // no built-in voice: the model copies the voice of the sample given with each sentence
            config.Model.Pocket.LmFlow = Path.Combine(folder, "lm_flow.int8.onnx");
            config.Model.Pocket.LmMain = Path.Combine(folder, "lm_main.int8.onnx");
            config.Model.Pocket.Encoder = Path.Combine(folder, "encoder.onnx");
            config.Model.Pocket.Decoder = Path.Combine(folder, "decoder.int8.onnx");
            config.Model.Pocket.TextConditioner = Path.Combine(folder, "text_conditioner.onnx");
            config.Model.Pocket.VocabJson = Path.Combine(folder, "vocab.json");
            config.Model.Pocket.TokenScoresJson = Path.Combine(folder, "token_scores.json");
            return new OfflineTts(config);
        }

        private OfflineRecognizer CreateRecognizer(string folder, string language)
        {
            OfflineRecognizerConfig config = new OfflineRecognizerConfig();
            config.ModelConfig.NumThreads = threads;
            config.ModelConfig.Provider = "cpu";

            if (File.Exists(Path.Combine(folder, "joiner.int8.onnx")))
            {
                // Parakeet (NVIDIA): finds the language by itself
                config.ModelConfig.Transducer.Encoder = Path.Combine(folder, "encoder.int8.onnx");
                config.ModelConfig.Transducer.Decoder = Path.Combine(folder, "decoder.int8.onnx");
                config.ModelConfig.Transducer.Joiner = Path.Combine(folder, "joiner.int8.onnx");
                config.ModelConfig.Tokens = Path.Combine(folder, "tokens.txt");
                config.ModelConfig.ModelType = "nemo_transducer";
                return new OfflineRecognizer(config);
            }

            // Whisper: the files start with the model name ("small-encoder.int8.onnx")
            string[] encoders = Directory.GetFiles(folder, "*-encoder.int8.onnx");
            if (encoders.Length == 0) throw new FileNotFoundException("no listening model in " + folder);

            string name = Path.GetFileName(encoders[0]).Replace("-encoder.int8.onnx", "");
            config.ModelConfig.Whisper.Encoder = encoders[0];
            config.ModelConfig.Whisper.Decoder = Path.Combine(folder, name + "-decoder.int8.onnx");
            config.ModelConfig.Whisper.Language = language;
            config.ModelConfig.Whisper.Task = "transcribe";
            config.ModelConfig.Tokens = Path.Combine(folder, name + "-tokens.txt");
            return new OfflineRecognizer(config);
        }

        // ------------------------------------------------------------------ listening

        // for a microphone button: one call to start talking, one call when finished
        public void ToggleListening()
        {
            if (State == VoiceState.Listening) StopListening();
            else if (State == VoiceState.Idle) StartListening();
        }

        public void StartListening()
        {
            if (State != VoiceState.Idle) return;

            LastProblem = "";
            if (recognizer == null)
            {
                LastProblem = Say("Mes oreilles ne sont pas prêtes. Écris-moi ta question !", "My ears are not ready. Type your question!");
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                LastProblem = Say("Je ne trouve pas de micro. Écris-moi ta question !", "I can't find a microphone. Type your question!");
                return;
            }

            // Gaston stops talking to listen, and does not record his own voice
            StopSpeaking();
            recording = Microphone.Start(Device, false, maxListenSeconds, 16000);
            State = VoiceState.Listening;
        }

        public void StopListening()
        {
            if (State != VoiceState.Listening) return;

            int length = Microphone.IsRecording(Device) ? Microphone.GetPosition(Device) : recording.samples;
            Microphone.End(Device);

            float[] samples = new float[0];
            int rate = recording.frequency;
            if (length > 0)
            {
                float[] raw = new float[length * recording.channels];
                recording.GetData(raw, 0);
                samples = ToMono(raw, recording.channels);
            }

            Destroy(recording);
            recording = null;
            TranscribeAndAsk(samples, rate);
        }

        private async void TranscribeAndAsk(float[] samples, int rate)
        {
            State = VoiceState.Transcribing;
            string text = "";
            float loudest = GetLoudest(samples);

#if UNITY_EDITOR
            if (keepLastRecording && samples.Length > 0) SaveWav("Logs/GardenerLastRecording.wav", samples, rate);
#endif

            // a recording too short or silent would make the model invent words
            if (samples.Length > rate * 0.3f && loudest > 0.02f)
            {
                try
                {
                    // a quiet microphone is brought up to a normal level first
                    text = await Transcribe(Amplify(samples, loudest), rate);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Gaston] Voice: could not transcribe: " + exception.Message);
                }
            }

            if (destroyed) return;
            State = VoiceState.Idle;

            if (string.IsNullOrWhiteSpace(text))
            {
                LastProblem = Say("Je n'ai rien entendu. Réessaie en parlant plus près du micro !", "I heard nothing. Try again, closer to the microphone!");
                return;
            }

            Debug.Log("[Gaston] Voice: heard \"" + text + "\" (" + (samples.Length / (float)rate).ToString("0.0") +
                      " s, loudest sample " + loudest.ToString("0.00") + ")");
            dialogue.Ask(text);
        }

        // sound -> text, on another thread (also used directly by the self-check)
        public Task<string> Transcribe(float[] samples, int rate)
        {
            return Task.Run(() =>
            {
                lock (listenLock)
                {
                    if (recognizer == null) return "";

                    using (OfflineStream stream = recognizer.CreateStream())
                    {
                        stream.AcceptWaveform(rate, samples);
                        recognizer.Decode(stream);
                        return (stream.Result.Text ?? "").Trim();
                    }
                }
            });
        }

        // ------------------------------------------------------------------ speaking

        // the recording whose voice Gaston copies (it must be readable: "Decompress On Load" in its import settings)
        public void SetVoiceSample(AudioClip clip)
        {
            voiceSample = clip;
            sampleAudio = null;
            if (clip == null) return;

            float[] raw = new float[clip.samples * clip.channels];
            if (!clip.GetData(raw, 0))
            {
                Debug.LogWarning("[Gaston] Voice: cannot read the voice sample " + clip.name + ".");
                return;
            }

            sampleAudio = ToMono(raw, clip.channels);
            sampleRate = clip.frequency;
        }

        private void HandleQuestionAsked(string question)
        {
            StopSpeaking();
        }

        private void HandleReplyUpdated(string replySoFar)
        {
            QueueSentences(replySoFar, false);
        }

        private void HandleReplyFinished(string reply, GardenerReplySource source)
        {
            QueueSentences(reply, true);
        }

        // sentence by sentence: Gaston starts talking while the model is still writing the rest
        private void QueueSentences(string reply, bool final)
        {
            if (!speakReplies || !CanSpeak) return;

            string plain = Tags.Replace(GardenerMarkup.ToPlainText(reply, !final), "");
            while (spokenCharacters < plain.Length)
            {
                int end = FindBreathEnd(plain, spokenCharacters, final);
                if (end < 0) break;

                string breath = plain.Substring(spokenCharacters, end - spokenCharacters).Trim();
                spokenCharacters = end;
                if (breath.Length > 0) sentencesToSay.Enqueue(breath);
            }
        }

        // the whole reply as one sound, cut exactly like the live voice (for the test recordings)
        public async Task<float[]> SynthesizeReply(string text)
        {
            List<float> all = new List<float>();
            int from = 0;

            while (from < text.Length)
            {
                int end = FindBreathEnd(text, from, true);
                float[] part = await Synthesize(text.Substring(from, end - from).Trim());
                if (part != null) all.AddRange(part);
                from = end;
            }

            return all.ToArray();
        }

        public void Speak(string text)
        {
            StopSpeaking();
            bool wasSpeaking = speakReplies;
            speakReplies = true;
            QueueSentences(text, true);
            speakReplies = wasSpeaking;
        }

        public void StopSpeaking()
        {
            speechSession++;
            spokenCharacters = 0;
            sentencesToSay.Clear();
            while (clipsToPlay.Count > 0) Destroy(clipsToPlay.Dequeue());
            speaker.Stop();
        }

        private async void PrepareNextSentence()
        {
            preparing = true;
            int session = speechSession;
            string sentence = sentencesToSay.Dequeue();
            float[] samples = null;

            try
            {
                samples = await Synthesize(sentence);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Gaston] Voice: could not say \"" + sentence + "\": " + exception.Message);
            }

            preparing = false;

            // the player asked something else meanwhile: this sentence is not wanted any more
            if (destroyed || session != speechSession || samples == null || samples.Length == 0) return;

            AudioClip clip = AudioClip.Create("Gaston", samples.Length, 1, SpeechSampleRate, false);
            clip.SetData(samples, 0);
            clipsToPlay.Enqueue(clip);
        }

        // text -> sound, on another thread (also used directly by the self-check)
        public Task<float[]> Synthesize(string text)
        {
            float[] reference = sampleAudio;
            int referenceRate = sampleRate;
            float chosenPauses = pauses;
            string chosenLiveliness = liveliness.ToString("0.00", CultureInfo.InvariantCulture);

            return Task.Run(() =>
            {
                lock (speakLock)
                {
                    if (tts == null || reference == null) return null;

                    OfflineTtsGenerationConfig config = new OfflineTtsGenerationConfig();
                    config.ReferenceAudio = reference;
                    config.ReferenceSampleRate = referenceRate;
                    config.SilenceScale = chosenPauses;
                    config.Extra["max_reference_audio_len"] = 12;
                    config.Extra["temperature"] = chosenLiveliness;

                    // the text given here is already one "breath": the engine must not cut it again
                    config.Extra["min_char_in_sentence"] = 1000;
                    config.Extra["max_char_in_sentence"] = 2000;

                    OfflineTtsGeneratedAudio audio = tts.GenerateWithConfig(text, config, KeepGoing);
                    float[] samples = audio.Samples;
                    audio.Dispose();
                    return samples;
                }
            });
        }

        [AOT.MonoPInvokeCallback(typeof(OfflineTtsCallbackProgressWithArg))]
        private static int OnSpeechProgress(IntPtr samples, int count, float progress, IntPtr arg)
        {
            return 1;
        }

        // ------------------------------------------------------------------ small helpers

        // end of the next group of sentences said in one go, -1 when the group is not complete yet
        // a very short sentence ("Ah non !") never goes alone: said by itself it sounds flat
        private int FindBreathEnd(string text, int from, bool final)
        {
            int end = from;
            for (int count = 0; count < sentencesTogether || end - from < 40; count++)
            {
                int next = FindSentenceEnd(text, end, final);
                if (next < 0) return -1;

                end = next;
                if (end >= text.Length) break;
            }

            return end;
        }

        // end of the first sentence after "from" (position just after its punctuation), -1 when it is not finished yet
        private static int FindSentenceEnd(string text, int from, bool final)
        {
            for (int i = from; i < text.Length; i++)
            {
                char letter = text[i];
                if (letter != '.' && letter != '!' && letter != '?' && letter != '…') continue;

                bool lastLetter = i == text.Length - 1;
                if (lastLetter ? final : char.IsWhiteSpace(text[i + 1])) return i + 1;
            }

            return final ? text.Length : -1;
        }

        private static float[] ToMono(float[] samples, int channels)
        {
            if (channels <= 1) return samples;

            float[] mono = new float[samples.Length / channels];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += samples[i * channels + c];
                mono[i] = sum / channels;
            }

            return mono;
        }

        private static float GetLoudest(float[] samples)
        {
            float loudest = 0f;
            foreach (float sample in samples) loudest = Mathf.Max(loudest, Mathf.Abs(sample));
            return loudest;
        }

        // louder copy whose loudest sample reaches 0.9 (never more than 20 times louder)
        private static float[] Amplify(float[] samples, float loudest)
        {
            float gain = Mathf.Min(0.9f / Mathf.Max(loudest, 0.0001f), 20f);
            if (gain <= 1.05f) return samples;

            float[] louder = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++) louder[i] = samples[i] * gain;
            return louder;
        }

        // 16-bit mono WAV file
        public static void SaveWav(string path, float[] samples, int rate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                foreach (float sample in samples) writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767f));
            }
        }

        private string Say(string french, string english)
        {
            return dialogue.Language == GardenerLanguage.English ? english : french;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string path);
#endif

        // Windows keeps an old onnxruntime.dll in System32: load ours first, or the voice library picks the wrong one
        private static void PreloadOnnxRuntime()
        {
#if UNITY_EDITOR_WIN
            string[] found = Directory.GetFiles(Path.Combine(Application.dataPath, "_Project"), "onnxruntime.dll", SearchOption.AllDirectories);
            if (found.Length > 0) LoadLibraryW(found[0]);
#elif UNITY_STANDALONE_WIN
            string path = Path.Combine(Application.dataPath, "Plugins", "x86_64", "onnxruntime.dll");
            if (File.Exists(path)) LoadLibraryW(path);
#endif
        }
    }
}
