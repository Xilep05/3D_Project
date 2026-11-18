using System.IO;
using System.Linq;
using LLMUnity;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 5 - builds Gaston's prefab and the Test_Gardener scene from code (menu GardenWise > Gardener),
    // so the .prefab and .unity files are always written by Unity and never edited by hand
    public static class GardenerBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Tests/Test_Gardener.unity";
        public const string PrefabPath = "Assets/_Project/Prefabs/Gardener/Gaston_Dialogue.prefab";
        public const string ArtFolder = "Assets/_Project/Art/Gardener";

        private const string UiFolder = ArtFolder + "/UI";
        private const string IconsFolder = ArtFolder + "/ThinkingIcons";
        private const string FontFilePath = ArtFolder + "/Fonts/PixelifySans.ttf";
        private const string FontAssetPath = ArtFolder + "/Fonts/PixelifySans SDF.asset";
        private const string PluginFolder = "Assets/_Project/Plugins/SherpaOnnx";

        // the GardenWise palette and frames are shared with the rest of the UI (PixelStyle)
        private const float FrameScale = PixelStyle.FrameScale;
        private static readonly Color32 Ink = PixelStyle.Ink;
        private static readonly Color32 SoftInk = PixelStyle.SoftInk;
        private static readonly Color32 Cream = PixelStyle.Cream;
        private static readonly Color32 LeafGreen = PixelStyle.LeafGreen;
        private static readonly Color32 Sky = PixelStyle.Sky;
        private static readonly Color32 Grass = PixelStyle.Grass;

        private static TMP_FontAsset pixelFont;

        // from the command line: Unity -batchmode -executeMethod GardenWise.GardenerBuilder.Build  (no -quit)
        public static void Build()
        {
            PrepareAssets();
            BuildPrefab();
            BuildScene();
            ExitWhenModelLibraryIsReady();
        }

        [MenuItem("GardenWise/Gardener/Build test scene")]
        private static void BuildSceneFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Test_Gardener", "Rebuild the scene? The current one is replaced.", "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // the prefab is kept as it is, so what you changed in it (icons, voice, prompt) stays
            PrepareAssets();
            if (!File.Exists(PrefabPath)) BuildPrefab();
            BuildScene();
        }

        [MenuItem("GardenWise/Gardener/Rebuild dialogue prefab")]
        private static void RebuildPrefabFromMenu()
        {
            if (File.Exists(PrefabPath) &&
                !EditorUtility.DisplayDialog("Gaston_Dialogue", "Rebuild the prefab? What you changed in it by hand is lost.", "Rebuild", "Cancel"))
            {
                return;
            }

            PrepareAssets();
            BuildPrefab();
        }

        private static void PrepareAssets()
        {
            ConfigureVoiceLibrary();
            pixelFont = GetPixelFont();
        }

        // ------------------------------------------------------------------ prefab

        // one self-contained object: drop it under a canvas and Gaston works (dialogue, local model, voice, panel)
        // sizes are in canvas units for a 1920 x 1080 canvas: the panel is one third of the screen wide
        private static void BuildPrefab()
        {
            RectTransform root = AddRect("Gaston_Dialogue", null, 0f, 0f, 636f, 508f);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-24f, -24f);

            GardenContextBuilder context = root.gameObject.AddComponent<GardenContextBuilder>();
            GardenerDialogue dialogue = root.gameObject.AddComponent<GardenerDialogue>();

            // --- the local model, on its own object so Gaston can switch it off when the file is missing
            GameObject model = new GameObject("LocalModel");
            model.transform.SetParent(root, false);
            LLM llm = model.AddComponent<LLM>();
            LLMAgent agent = model.AddComponent<LLMAgent>();
            llm.dontDestroyOnLoad = false;
            SetNumber(llm, "_numGPULayers", 99); // uses the graphics card when there is one, else the processor
            SetNumber(llm, "_contextSize", 4096);
            SetReference(agent, "_llm", llm);
            agent.numPredict = 160;
            agent.temperature = 0.4f;
            agent.topK = 20;
            agent.topP = 0.9f;

            SetReference(dialogue, "garden", context);
            SetReference(dialogue, "llm", llm);
            SetReference(dialogue, "agent", agent);

            // --- voice
            AudioSource speaker = root.gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            GardenerVoice voice = root.gameObject.AddComponent<GardenerVoice>();
            SetReference(voice, "dialogue", dialogue);
            SetReference(voice, "speaker", speaker);
            SetReference(voice, "voiceSample", AssetDatabase.LoadAssetAtPath<AudioClip>(ArtFolder + "/Voices/cml_1770.wav"));

            // --- header: always visible, folds and unfolds the rest
            RectTransform header = AddRect("Header", root, 0f, 0f, 636f, 72f);
            AddFrame(header, UiFolder + "/gaston_badge.png");
            AddSprite(AddRect("Emblem", header, 18f, 12f, 48f, 48f), IconsFolder + "/08_sunflower.png");
            AddText(AddRect("NameText", header, 78f, 0f, 220f, 72f), "Gaston", 34f, Cream, TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI counter = AddText(AddRect("CounterText", header, 300f, 0f, 192f, 72f), "Questions : 0", 20f, Cream, TextAlignmentOptions.MidlineRight);

            Image micIcon = AddSprite(AddRect("MicButton", header, 510f, 12f, 48f, 48f), UiFolder + "/icon_mic.png");
            Button micButton = AddButton(micIcon, Cream);
            Image foldIcon = AddSprite(AddRect("FoldButton", header, 570f, 12f, 48f, 48f), UiFolder + "/icon_chevron.png");
            foldIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            foldIcon.rectTransform.anchoredPosition = new Vector2(594f, -36f);
            Button foldButton = AddButton(foldIcon, Cream);

            // --- body: clipped by a mask so it can unroll
            RectTransform body = AddRect("Body", root, 0f, 80f, 636f, 428f);
            body.gameObject.AddComponent<RectMask2D>();

            RectTransform bubble = AddRect("Bubble", body, 0f, 0f, 636f, 360f);
            AddFrame(bubble, UiFolder + "/gaston_bubble.png");

            TextMeshProUGUI question = AddText(AddRect("QuestionText", bubble, 30f, 18f, 400f, 30f), "", 20f, SoftInk, TextAlignmentOptions.MidlineLeft);
            question.textWrappingMode = TextWrappingModes.NoWrap;
            question.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI reply = AddText(AddRect("ReplyText", bubble, 30f, 58f, 576f, 262f), "", 30f, Ink, TextAlignmentOptions.TopLeft);
            reply.enableAutoSizing = true;
            reply.fontSizeMin = 20f;
            reply.fontSizeMax = 30f;

            Image arrow = AddSprite(AddRect("MoreArrow", bubble, 582f, 312f, 24f, 24f), UiFolder + "/icon_chevron.png");
            arrow.color = LeafGreen;
            arrow.gameObject.SetActive(false);

            // tag "IA locale" / "Hors ligne", sized by its text
            RectTransform tag = AddRect("Tag", bubble, 0f, 0f, 0f, 0f);
            tag.anchorMin = tag.anchorMax = tag.pivot = Vector2.one;
            tag.anchoredPosition = new Vector2(-24f, -16f);
            Image tagBackground = tag.gameObject.AddComponent<Image>();
            tagBackground.raycastTarget = false;

            HorizontalLayoutGroup tagLayout = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            tagLayout.padding = new RectOffset(12, 12, 3, 3);
            tagLayout.childAlignment = TextAnchor.MiddleCenter;
            tagLayout.childControlWidth = true;
            tagLayout.childControlHeight = true;
            tagLayout.childForceExpandWidth = false;
            tagLayout.childForceExpandHeight = false;

            ContentSizeFitter tagFitter = tag.gameObject.AddComponent<ContentSizeFitter>();
            tagFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tagFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI tagText = AddText(AddRect("TagText", tag, 0f, 0f, 0f, 0f), "IA locale", 18f, LeafGreen, TextAlignmentOptions.Center);
            tagText.textWrappingMode = TextWrappingModes.NoWrap;
            tag.gameObject.SetActive(false);

            // waiting: three hopping icons and a short text
            RectTransform waiting = AddRect("Waiting", bubble, 30f, 110f, 576f, 140f);
            Image[] slots = new Image[3];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = AddRect("Icon" + (i + 1), waiting, i * 72f, 40f, 60f, 60f).gameObject.AddComponent<Image>();
                slots[i].raycastTarget = false;
            }

            TextMeshProUGUI waitingText = AddText(AddRect("WaitingText", waiting, 228f, 0f, 348f, 140f), "Gaston réfléchit…", 26f, Ink, TextAlignmentOptions.MidlineLeft);
            waiting.gameObject.SetActive(false);

            TMP_InputField input = AddInputField(body, "QuestionInput", "Pourquoi ma laitue va mal ?");

            GardenerPanel panel = root.gameObject.AddComponent<GardenerPanel>();
            SetReference(panel, "dialogue", dialogue);
            SetReference(panel, "voice", voice);
            SetReference(panel, "body", body);
            SetReference(panel, "foldArrow", foldIcon.rectTransform);
            SetReference(panel, "questionText", question);
            SetReference(panel, "replyText", reply);
            SetReference(panel, "moreArrow", arrow);
            SetReference(panel, "waitingGroup", waiting.gameObject);
            SetReference(panel, "waitingText", waitingText);
            SetReferences(panel, "iconSlots", slots);
            SetReferences(panel, "thinkingIcons", LoadSprites(IconsFolder));
            SetReference(panel, "tagBackground", tagBackground);
            SetReference(panel, "tagText", tagText);
            SetReference(panel, "inputField", input);
            SetReference(panel, "micIcon", micIcon);
            SetReference(panel, "counterText", counter);

            UnityEventTools.AddVoidPersistentListener(foldButton.onClick, panel.Toggle);
            UnityEventTools.AddVoidPersistentListener(micButton.onClick, panel.ToggleMicrophone);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath, out saved);
            Object.DestroyImmediate(root.gameObject);

            if (saved) Debug.Log("[Gaston] Prefab saved: " + PrefabPath);
            else Debug.LogError("[Gaston] Could not save " + PrefabPath);
        }

        // ------------------------------------------------------------------ test scene

        private static void BuildScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Gaston] Prefab missing: " + PrefabPath);
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Sky;

            // same canvas settings as Test_UI and the game canvas
            GameObject canvasObject = new GameObject("Test_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // the project uses the old Input Manager, like Test_UI
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Transform canvas = canvasObject.transform;

            // a strip of grass stands for the game behind the panel
            RectTransform grass = AddRect("Grass", canvas, 0f, 0f, 0f, 320f);
            grass.anchorMin = grass.pivot = Vector2.zero;
            grass.anchorMax = new Vector2(1f, 0f);
            grass.anchoredPosition = Vector2.zero;
            grass.gameObject.AddComponent<Image>().color = Grass;

            RectTransform title = AddRect("Title", canvas, 0f, 0f, 1000f, 50f);
            title.anchorMin = title.anchorMax = title.pivot = Vector2.zero;
            title.anchoredPosition = new Vector2(30f, 20f);
            AddText(title, "Test_Gardener", 28f, LeafGreen, TextAlignmentOptions.BottomLeft);

            // the prefab sits where it will be in the game: top right, one third of the screen
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);

            // in this scene Gaston looks at a test garden instead of the real systems
            SerializedObject context = new SerializedObject(instance.GetComponent<GardenContextBuilder>());
            context.FindProperty("useTestGarden").boolValue = true;
            context.ApplyModifiedPropertiesWithoutUndo();

            if (EditorSceneManager.SaveScene(scene, ScenePath)) Debug.Log("[Gaston] Test scene saved: " + ScenePath);
            else Debug.LogError("[Gaston] Could not save " + ScenePath);
        }

        // ------------------------------------------------------------------ fonts, plugins, model library

        // makes the TextMesh Pro font from PixelifySans.ttf the first time; without the file the default font is used
        private static TMP_FontAsset GetPixelFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontFilePath);
            if (font == null)
            {
                Debug.LogWarning("[Gaston] " + FontFilePath + " not found: the default font is used.");
                return null;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 1024, AtlasPopulationMode.Dynamic, false);

            // every character French and English need, drawn once so the asset never changes afterwards
            string characters = "";
            for (int code = 32; code <= 126; code++) characters += (char)code;
            for (int code = 160; code <= 255; code++) characters += (char)code;
            characters += "œŒ…’‘“”–—€•";

            string missing;
            fontAsset.TryAddCharacters(characters, out missing);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            fontAsset.material.name = "PixelifySans SDF Material";
            fontAsset.atlasTexture.name = "PixelifySans SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        // the voice library (sherpa-onnx 2.0.1), one copy per system: Windows x86_64 and macOS Apple Silicon,
        // each used by the editor and the build of its own system only
        private static void ConfigureVoiceLibrary()
        {
            foreach (string file in new[] { "sherpa-onnx-c-api.dll", "onnxruntime.dll" })
                ConfigureNativePlugin("/x86_64/" + file, "Windows", "x86_64", BuildTarget.StandaloneWindows64);

            foreach (string file in new[] { "libsherpa-onnx-c-api.dylib", "libonnxruntime.dylib" })
                ConfigureNativePlugin("/macOS/" + file, "OSX", "ARM64", BuildTarget.StandaloneOSX);
        }

        private static void ConfigureNativePlugin(string file, string os, string cpu, BuildTarget target)
        {
            PluginImporter plugin = AssetImporter.GetAtPath(PluginFolder + file) as PluginImporter;
            if (plugin == null)
            {
                Debug.LogWarning("[Gaston] Voice library missing: " + PluginFolder + file);
                return;
            }

            if (!plugin.GetCompatibleWithAnyPlatform() && plugin.GetCompatibleWithEditor() &&
                plugin.GetEditorData("OS") == os && plugin.GetCompatibleWithPlatform(target)) return;

            plugin.SetCompatibleWithAnyPlatform(false);
            plugin.SetCompatibleWithEditor(true);
            plugin.SetEditorData("OS", os);
            plugin.SetEditorData("CPU", cpu);
            plugin.SetCompatibleWithPlatform(target, true);
            if (target == BuildTarget.StandaloneOSX) plugin.SetPlatformData(target, "CPU", cpu);
            plugin.SaveAndReimport();
        }

        // LLMUnity unpacks its native library into StreamingAssets after the editor starts: wait for it before quitting
        private static void ExitWhenModelLibraryIsReady()
        {
            if (!Application.isBatchMode) return;

            string doneFile = Path.Combine(LLMUnitySetup.libraryPath, "setup", Path.GetFileName(LLMUnitySetup.LlamaLibURL) + ".complete");
            double deadline = EditorApplication.timeSinceStartup + 1200.0;

            EditorApplication.update += () =>
            {
                bool ready = File.Exists(doneFile) && LLMUnitySetup.libraryProgress >= 1f;
                if (!ready && EditorApplication.timeSinceStartup < deadline) return;

                if (ready) Debug.Log("[Gaston] LLMUnity library ready: " + LLMUnitySetup.libraryPath);
                else Debug.LogError("[Gaston] LLMUnity library was not ready in time.");
                EditorApplication.Exit(ready ? 0 : 3);
            };
        }

        // ------------------------------------------------------------------ small helpers

        // a UI object placed from the top-left corner of its parent
        private static RectTransform AddRect(string name, Transform parent, float x, float yFromTop, float width, float height)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image AddSprite(RectTransform rect, string spritePath)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.raycastTarget = false;

            if (image.sprite == null) Debug.LogError("[Gaston] Sprite not found: " + spritePath);
            return image;
        }

        // a pixel frame that keeps its corners and stretches its middle (9-slice)
        private static Image AddFrame(RectTransform rect, string spritePath)
        {
            Image image = AddSprite(rect, spritePath);
            PixelStyle.Frame(image, spritePath);
            image.raycastTarget = false;
            return image;
        }

        private static Button AddButton(Image icon, Color colour)
        {
            icon.color = colour;
            icon.raycastTarget = true;

            Button button = icon.gameObject.AddComponent<Button>();
            button.targetGraphic = icon;
            return button;
        }

        private static TextMeshProUGUI AddText(RectTransform rect, string content, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (pixelFont != null) text.font = pixelFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_InputField AddInputField(Transform parent, string name, string placeholder)
        {
            Sprite fieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/gaston_field.png");
            GameObject inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources { inputField = fieldSprite });
            inputObject.name = name;
            foreach (Transform child in inputObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = (RectTransform)inputObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -368f);
            rect.sizeDelta = new Vector2(636f, 60f);

            Image background = inputObject.GetComponent<Image>();
            background.sprite = fieldSprite;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = FrameScale;
            background.color = Color.white;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;
            input.customCaretColor = true;
            input.caretColor = Ink;
            input.caretWidth = 3;
            input.selectionColor = new Color32(0xC0, 0xDD, 0x97, 0xFF);

            TMP_Text text = input.textComponent;
            TMP_Text placeholderText = (TMP_Text)input.placeholder;
            if (pixelFont != null)
            {
                input.fontAsset = pixelFont;
                text.font = pixelFont;
                placeholderText.font = pixelFont;
            }

            text.fontSize = 26f;
            text.color = Ink;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            placeholderText.text = placeholder;
            placeholderText.fontSize = 26f;
            placeholderText.fontStyle = FontStyles.Normal;
            placeholderText.color = new Color32(0x3A, 0x2A, 0x1E, 0x80);
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

            // the default margins are made for a 30 px high field
            input.textViewport.offsetMin = new Vector2(24f, 6f);
            input.textViewport.offsetMax = new Vector2(-24f, -6f);
            return input;
        }

        // every sprite of a folder, in the order of the file names
        private static Sprite[] LoadSprites(string folder)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray();
        }

        // fills a private [SerializeField] the same way the Inspector does
        private static void SetReference(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Gaston] Field not found: " + target.GetType().Name + "." + fieldName);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(Object target, string fieldName, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray)
            {
                Debug.LogError("[Gaston] List not found: " + target.GetType().Name + "." + fieldName);
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetNumber(Object target, string fieldName, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError("[Gaston] Field not found: " + target.GetType().Name + "." + fieldName);
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // any picture dropped in Art/Gardener becomes a crisp pixel art sprite the first time Unity sees it
    public class GardenerArtImporter : AssetPostprocessor
    {
        // a voice sample must stay readable by script: Pocket TTS copies the voice from its sound data
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(GardenerBuilder.ArtFolder + "/Voices/")) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GardenerBuilder.ArtFolder + "/")) return;

            // settings already chosen once (by this script or by hand in the Inspector): leave them alone
            TextureImporter importer = (TextureImporter)assetImporter;
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 12.5f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
