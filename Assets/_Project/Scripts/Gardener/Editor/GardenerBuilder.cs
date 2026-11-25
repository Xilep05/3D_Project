using System.IO;
using System.Text;
using System.Linq;
using LLMUnity;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        private const string PluginFolder = "Assets/_Project/Plugins/SherpaOnnx";

        // the colours of the game's UI (UI_Canvas, owner 6): Gaston looks like the rest of the game
        private static readonly Color32 PanelGreen = new Color32(0x1C, 0x3D, 0x2B, 0xEB);
        private static readonly Color32 ButtonGreen = new Color32(0x40, 0x85, 0x47, 0xFF);
        private static readonly Color32 BodyText = new Color32(0xF5, 0xF5, 0xE8, 0xFF);

        // Inter (Figma mock-up), made into TextMesh Pro fonts by PrepareFonts
        private const string FontFolder = ArtFolder + "/Fonts";
        private static TMP_FontAsset Regular;
        private static TMP_FontAsset SemiBold;
        private static Material KeyShadow;

        // only the backdrop of the test scene
        private static readonly Color32 Sky = new Color32(0xBF, 0xE3, 0xF2, 0xFF);
        private static readonly Color32 Grass = new Color32(0x8F, 0xCF, 0x6A, 0xFF);


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
            PrepareFonts();
        }

        // ------------------------------------------------------------------ prefab

        // one self-contained object: drop it under a canvas and Gaston works (dialogue, local model, voice, panel)
        // sizes are in canvas units for a 1920 x 1080 canvas: twice the sizes of the Figma mock-up
        private static void BuildPrefab()
        {
            RectTransform root = AddRect("Gaston_Dialogue", null, 0f, 0f, 824f, 560f);
            root.anchoredPosition = new Vector2(24f, -24f);

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

            // --- left column: the round portrait, and under it the keys
            RectTransform portrait = AddRect("Portrait", root, 0f, 0f, 128f, 128f);
            Image disc = AddSprite(portrait, UiFolder + "/disc.png");
            portrait.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(AddSprite(AddRect("Picture", portrait, 0f, 0f, 0f, 0f), UiFolder + "/gaston_portrait.png").rectTransform);
            Image portraitRing = AddSprite(AddRect("Ring", root, 0f, 0f, 128f, 128f), UiFolder + "/ring.png");
            portraitRing.color = GardenerPanel.Cream;
            disc.color = Color.white;

            RectTransform openHint = AddRect("OpenHint", root, 0f, 144f, 128f, 44f);
            TMP_Text typeKeyText = AddKey(openHint, "TypeKey", "T").text;
            Centre(typeKeyText.transform.parent as RectTransform);

            RectTransform voiceGroup = AddRect("Voice", root, 0f, 144f, 128f, 120f);
            RectTransform mic = AddRect("MicButton", voiceGroup, 34f, 0f, 60f, 60f);
            Image micFill = AddSprite(mic, UiFolder + "/disc.png");
            micFill.color = PanelGreen;
            Image micRing = AddSprite(AddRect("Ring", mic, 0f, 0f, 60f, 60f), UiFolder + "/ring.png");
            micRing.color = GardenerPanel.Cream;
            Image micIcon = AddSprite(AddRect("Icon", mic, 15f, 15f, 30f, 30f), UiFolder + "/icon_mic.png");
            micIcon.color = GardenerPanel.Cream;
            Button micButton = AddButton(micFill, PanelGreen);
            RectTransform talkHolder = AddRect("TalkKeyHolder", voiceGroup, 0f, 76f, 128f, 44f);
            (Image talkKeyFrame, TMP_Text talkKeyText) = AddKey(talkHolder, "TalkKey", "V");
            Centre(talkKeyFrame.rectTransform);

            // --- right column: the bubble, then "press Esc to close"; it grows downwards with the text
            RectTransform column = AddRect("BubbleColumn", root, 168f, 0f, 656f, 0f);
            CanvasGroup bubbleGroup = column.gameObject.AddComponent<CanvasGroup>();
            VerticalLayoutGroup columnLayout = AddVertical(column, 0, 16f);
            columnLayout.childAlignment = TextAnchor.UpperLeft;
            columnLayout.childForceExpandWidth = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Figma: 2 px cream frame, 4 px of dark fill, a 1 px line, 12 px inside (all doubled here)
            RectTransform bubble = AddRect("Bubble", column, 0f, 0f, 656f, 0f);
            AddFrame(bubble, GardenerPanel.Cream, "round_fill", 1f);
            LayoutElement bubbleSize = bubble.gameObject.AddComponent<LayoutElement>();
            bubbleSize.minWidth = bubbleSize.preferredWidth = 656f;
            AddVertical(bubble, 4, 0f);
            RectTransform fill = AddRect("Fill", bubble, 0f, 0f, 0f, 0f);
            AddFrame(fill, PanelGreen, "round_fill", 1.33f);
            AddVertical(fill, 8, 0f);
            RectTransform inner = AddRect("Inner", fill, 0f, 0f, 0f, 0f);
            AddFrame(inner, ButtonGreen, "round_line", 2f);
            AddVertical(inner, 24, 16f);

            RectTransform alert = AddRect("Alert", inner, 0f, 0f, 0f, 0f);
            HorizontalLayoutGroup alertLayout = alert.gameObject.AddComponent<HorizontalLayoutGroup>();
            alertLayout.spacing = 12f;
            alertLayout.childAlignment = TextAnchor.MiddleLeft;
            alertLayout.childControlWidth = alertLayout.childControlHeight = true;
            alertLayout.childForceExpandWidth = alertLayout.childForceExpandHeight = false;
            Image alertIcon = AddSprite(AddRect("Icon", alert, 0f, 0f, 30f, 30f), UiFolder + "/icon_alert.png");
            alertIcon.color = GardenerPanel.Gold;
            Fixed(alertIcon.gameObject, 30f, 30f);
            TextMeshProUGUI alertText = AddText(AddRect("AlertText", alert, 0f, 0f, 0f, 0f), "IA distante non configurée : réponses préécrites", 24f, GardenerPanel.Gold, TextAlignmentOptions.MidlineLeft, SemiBold);
            alertText.textWrappingMode = TextWrappingModes.Normal;

            AddText(AddRect("NameText", inner, 0f, 0f, 0f, 0f), "Gaston", 26f, GardenerPanel.Cream, TextAlignmentOptions.TopLeft, SemiBold);

            // the reply is clipped by its viewport: as high as the lines written so far (GardenerPanel)
            RectTransform viewport = AddRect("ReplyViewport", inner, 0f, 0f, 0f, 44f);
            viewport.gameObject.AddComponent<RectMask2D>();
            LayoutElement viewportSize = viewport.gameObject.AddComponent<LayoutElement>();
            viewportSize.preferredHeight = 44f;
            RectTransform replyRect = AddRect("ReplyText", viewport, 0f, 0f, 0f, 1000f);
            replyRect.anchorMin = new Vector2(0f, 1f);
            replyRect.anchorMax = new Vector2(1f, 1f);
            replyRect.pivot = new Vector2(0.5f, 1f);
            replyRect.sizeDelta = new Vector2(0f, 1000f);
            replyRect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI reply = AddText(replyRect, "", 30f, BodyText, TextAlignmentOptions.TopLeft, Regular);
            reply.lineSpacing = 22f; // 44 px lines, as in the mock-up (15 / 22)

            // thinking: hopping fruits and vegetables (ThinkingIcons)
            RectTransform waiting = AddRect("Waiting", inner, 0f, 0f, 0f, 0f);
            Fixed(waiting.gameObject, -1f, 48f);
            Image[] slots = new Image[4];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = AddRect("Icon" + (i + 1), waiting, i * 56f, 8f, 40f, 40f).gameObject.AddComponent<Image>();
                slots[i].raycastTarget = false;
            }

            TMP_InputField input = AddInputField(inner, "QuestionInput", "Écris ta question…");

            // the tail pointing at Gaston covers the frame where it joins the bubble
            Image tailFill = AddSprite(AddRect("TailFill", bubble, -28f, 46f, 32f, 36f), UiFolder + "/tail_fill.png");
            tailFill.color = PanelGreen;
            Image tailLine = AddSprite(AddRect("TailLine", bubble, -28f, 46f, 32f, 36f), UiFolder + "/tail_line.png");
            tailLine.color = GardenerPanel.Cream;
            tailFill.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tailLine.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // "Appuie sur [Échap] pour fermer", on a dark strip so it reads on the sky and on the grass
            RectTransform closeHint = AddRect("CloseHint", column, 0f, 0f, 0f, 0f);
            AddFrame(closeHint, new Color(0f, 0f, 0f, 0.45f), "round_fill", 1f);
            HorizontalLayoutGroup hintLayout = closeHint.gameObject.AddComponent<HorizontalLayoutGroup>();
            hintLayout.padding = new RectOffset(16, 16, 8, 8);
            hintLayout.spacing = 10f;
            hintLayout.childAlignment = TextAnchor.MiddleLeft;
            hintLayout.childControlWidth = hintLayout.childControlHeight = true;
            hintLayout.childForceExpandWidth = hintLayout.childForceExpandHeight = false;
            ContentSizeFitter hintFitter = closeHint.gameObject.AddComponent<ContentSizeFitter>();
            hintFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            AddText(AddRect("Before", closeHint, 0f, 0f, 0f, 0f), "Appuie sur", 22f, Color.white, TextAlignmentOptions.MidlineLeft, SemiBold);
            TMP_Text closeKeyText = AddKey(closeHint, "CloseKey", "Échap").text;
            AddText(AddRect("After", closeHint, 0f, 0f, 0f, 0f), "pour fermer", 22f, Color.white, TextAlignmentOptions.MidlineLeft, SemiBold);

            GardenerPanel panel = root.gameObject.AddComponent<GardenerPanel>();
            SetReference(panel, "dialogue", dialogue);
            SetReference(panel, "voice", voice);
            SetReference(panel, "bubbleGroup", bubbleGroup);
            SetReference(panel, "openHint", openHint.gameObject);
            SetReference(panel, "replyText", reply);
            SetReference(panel, "replyViewport", viewportSize);
            SetReference(panel, "waitingGroup", waiting.gameObject);
            SetReferences(panel, "iconSlots", slots);
            SetReferences(panel, "thinkingIcons", LoadSprites(IconsFolder));
            SetReference(panel, "alertGroup", alert.gameObject);
            SetReference(panel, "alertText", alertText);
            SetReference(panel, "inputField", input);
            SetReference(panel, "voiceGroup", voiceGroup.gameObject);
            SetReference(panel, "micRing", micRing);
            SetReference(panel, "micIcon", micIcon);
            SetReference(panel, "talkKeyFrame", talkKeyFrame);
            SetReference(panel, "typeKeyText", typeKeyText);
            SetReference(panel, "talkKeyText", talkKeyText);
            SetReference(panel, "closeKeyText", closeKeyText);

            UnityEventTools.AddVoidPersistentListener(micButton.onClick, panel.ToggleMicrophone);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath, out saved);
            Object.DestroyImmediate(root.gameObject);

            if (saved) Debug.Log("[Gaston] Prefab saved: " + PrefabPath);
            else Debug.LogError("[Gaston] Could not save " + PrefabPath);
        }

        // a key drawn as in the mock-up: only its outline, the letter inside, in cream; as wide as its label
        private static (Image frame, TMP_Text text) AddKey(Transform parent, string name, string label)
        {
            RectTransform key = AddRect(name, parent, 0f, 0f, 44f, 44f);
            Image frame = AddFrame(key, GardenerPanel.Cream, "round_line", 1.33f);
            HorizontalLayoutGroup layout = key.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = key.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement size = key.gameObject.AddComponent<LayoutElement>();
            size.minWidth = 44f;
            size.minHeight = size.preferredHeight = 44f;

            TextMeshProUGUI text = AddText(AddRect("Label", key, 0f, 0f, 0f, 0f), label, 22f, GardenerPanel.Cream, TextAlignmentOptions.Center, SemiBold);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            // a soft dark shadow under the outline and the letter: still only an outline, but readable on the sky
            Shadow shadow = key.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -2f);
            if (KeyShadow != null) text.fontSharedMaterial = KeyShadow;
            return (frame, text);
        }

        // a key alone under the portrait: centred in its row
        private static void Centre(RectTransform key)
        {
            key.anchorMin = key.anchorMax = key.pivot = new Vector2(0.5f, 1f);
            key.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static VerticalLayoutGroup AddVertical(RectTransform rect, int padding, float spacing)
        {
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static void Fixed(GameObject target, float width, float height)
        {
            LayoutElement size = target.AddComponent<LayoutElement>();
            if (width >= 0f) size.minWidth = size.preferredWidth = width;
            if (height >= 0f) size.minHeight = size.preferredHeight = height;
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
            AddText(title, "Test_Gardener", 28f, ButtonGreen, TextAlignmentOptions.BottomLeft);

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

        // a rounded shape from UI/round_fill.png or round_line.png (9-slice, 16 px corners):
        // a higher multiplier makes the corners and the line thinner (2 = 8 px corners, 2 px line)
        private static Image AddFrame(RectTransform rect, Color colour, string shape, float multiplier)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/" + shape + ".png");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
            image.color = colour;
            image.raycastTarget = false;
            if (image.sprite == null) Debug.LogError("[Gaston] Sprite not found: " + UiFolder + "/" + shape + ".png");
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

        private static TextMeshProUGUI AddText(RectTransform rect, string content, float size, Color color, TextAlignmentOptions alignment,
                                               TMP_FontAsset font = null)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        // the text field inside the bubble: a thin green line around it, like the bubble's inner line
        private static TMP_InputField AddInputField(Transform parent, string name, string placeholder)
        {
            Sprite line = AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/round_line.png");
            GameObject inputObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources { inputField = line });
            inputObject.name = name;
            foreach (Transform child in inputObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = (RectTransform)inputObject.transform;
            rect.SetParent(parent, false);
            Fixed(inputObject, -1f, 56f);

            Image background = inputObject.GetComponent<Image>();
            background.sprite = line;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 2f;
            background.color = ButtonGreen;

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;
            input.customCaretColor = true;
            input.caretColor = BodyText;
            input.caretWidth = 2;
            input.selectionColor = new Color32(0x40, 0x85, 0x47, 0xC0);

            TMP_Text text = input.textComponent;
            TMP_Text placeholderText = (TMP_Text)input.placeholder;

            foreach (TMP_Text field in new[] { text, placeholderText })
            {
                if (Regular != null) field.font = Regular;
                field.fontSize = 24f;
                field.fontStyle = FontStyles.Normal;
                field.alignment = TextAlignmentOptions.MidlineLeft;
            }

            text.color = BodyText;
            placeholderText.text = placeholder;
            placeholderText.color = new Color32(0xF5, 0xF5, 0xE8, 0x8C);

            // the default margins are made for a 30 px high field
            input.textViewport.offsetMin = new Vector2(16f, 4f);
            input.textViewport.offsetMax = new Vector2(-16f, -4f);
            return input;
        }

        // Inter Regular and SemiBold (Fonts/*.ttf, OFL) as TextMesh Pro fonts, made once: every Latin letter used in
        // French is drawn in advance and the atlas is then frozen, so playing never rewrites the asset.
        // Anything else (a rare symbol from the model) falls back to the game's font
        private static void PrepareFonts()
        {
            Regular = MakeFont("Inter-Regular");
            SemiBold = MakeFont("Inter-SemiBold");
            KeyShadow = MakeShadowMaterial(SemiBold, "Inter-SemiBold Shadow");
        }

        // the font's material with a dark underlay, for the letters of the keys
        private static Material MakeShadowMaterial(TMP_FontAsset font, string name)
        {
            string path = FontFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null || font == null) return existing;

            Material material = new Material(font.material) { name = name };
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            material.SetFloat("_UnderlayOffsetY", -0.5f);
            material.SetFloat("_UnderlayDilate", 0.3f);
            material.SetFloat("_UnderlaySoftness", 0.4f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static TMP_FontAsset MakeFont(string name)
        {
            string path = FontFolder + "/" + name + " SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            Font source = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "/" + name + ".ttf");
            if (source == null)
            {
                Debug.LogError("[Gaston] Font missing: " + FontFolder + "/" + name + ".ttf");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            font.name = name + " SDF";

            StringBuilder characters = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) characters.Append(c);
            for (char c = '\u00A0'; c <= '\u00FF'; c++) characters.Append(c);
            characters.Append("ŒœŸ‘’‚“”„–—…•€′″«»");
            font.TryAddCharacters(characters.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log("[Gaston] " + name + " has no glyph for: " + missing);

            AssetDatabase.CreateAsset(font, path);
            for (int i = 0; i < font.atlasTextures.Length; i++)
            {
                font.atlasTextures[i].name = name + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(font.atlasTextures[i], font);
            }

            font.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (TMP_Settings.defaultFontAsset != null)
                font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gaston] Font made: " + path);
            return font;
        }

        // every sprite of a folder, in the order of the file names
        private static Object[] LoadSprites(string folder)
        {
            return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                .Where(sprite => sprite != null)
                .ToArray<Object>();
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

    // any picture dropped in Art/Gardener becomes a UI sprite the first time Unity sees it
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
            // the thinking icons are little pixel-art fruits: kept sharp. The rest (portrait, shapes, icons) is smooth
            bool pixelArt = assetPath.StartsWith(GardenerBuilder.ArtFolder + "/ThinkingIcons/");
            importer.spritePixelsPerUnit = pixelArt ? 12.5f : 100f;
            importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            // rounded shapes stretched as 9-slice: 16 px corners stay round at any size
            if (Path.GetFileName(assetPath).StartsWith("round_")) importer.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
        }
    }
}
