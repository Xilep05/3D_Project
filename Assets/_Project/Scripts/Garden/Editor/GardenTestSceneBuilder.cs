using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 3 - menu "GardenWise > Build Test_Garden Scene": builds the test scene and the garden / player prefabs
    // run it again at any time to rebuild the scene from scratch
    public static class GardenTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Tests/Test_Garden.unity";
        private const string LevelPath = "Assets/_Project/Data/Levels/Test_Garden_Level.asset";
        private const string CropFolder = "Assets/_Project/Data/Crops";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Garden";
        private const string ArtFolder = "Assets/_Project/Art/Garden";

        // prefabs of owner 4, used when they exist in the project
        private const string WaterPrefabPath = "Assets/_Project/Prefabs/Water/WaterAndWeather.prefab";
        private const string BarrelPrefabPath = "Assets/_Project/Prefabs/Water/RainBarrel.prefab";
        private const string RainPrefabPath = "Assets/_Project/Prefabs/Water/Rain.prefab";

        private const float TileSize = 1.2f;
        private const float TileGap = 0.4f;
        private const float SoilHeight = 0.2f;
        private const float FrameHeight = 0.28f;
        private const float PlankThickness = 0.08f;

        [MenuItem("GardenWise/Build Test_Garden Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateFolder("Assets/_Project/Art", "Garden");
            CreateFolder("Assets/_Project/Prefabs", "Garden");
            CreateFolder("Assets/_Project/Data", "Crops");
            CreateFolder("Assets/_Project/Data", "Levels");

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // the player brings its own camera
            if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);

            CropData radish = GetCrop("Radish", 4, 40f, 70f, "Radishes grow fast and like regular watering.");
            CropData lettuce = GetCrop("Lettuce", 5, 50f, 80f, "Lettuce likes a soil that stays a bit wet.");
            CropData[] seeds = { radish, lettuce };
            LevelData level = GetTestLevel(seeds);

            // decor, light, sky and fog shared with the Main scene
            EnvironmentArt.CreateEnvironment();
            EnvironmentArt.ApplyLighting();

            // water and weather of owner 4
            WaterManager water;
            WeatherSystem weather;
            CreateWaterAndWeather(out water, out weather);

            // other systems needed to run the day loop in this test scene
            GameObject managers = new GameObject("TestManagers");
            GameManager game = managers.AddComponent<GameManager>();
            ScoreManager score = managers.AddComponent<ScoreManager>();

            // my prefabs: placeholder plant, 3x3 garden, bell, player
            Plant plantPrefab = CreatePlantPlaceholderPrefab();
            GardenGrid garden = CreateGarden(plantPrefab);
            CreateBell();
            GameObject player = CreatePlayer(seeds);

            // UI of the test scene
            Transform canvas = CreateCanvas();
            CreateText("Crosshair", canvas, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), 40f, TextAlignmentOptions.Center).text = "+";
            TextMeshProUGUI target = CreateText("Target", canvas, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(700f, 120f), 24f, TextAlignmentOptions.Top);
            TextMeshProUGUI tool = CreatePanelText("Tool", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(560f, 60f), 28f);
            tool.alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI message = CreateText("Message", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1400f, 50f), 26f, TextAlignmentOptions.Center);
            TextMeshProUGUI stats = CreatePanelText("Stats", canvas, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(720f, 300f), 22f);
            target.text = "";
            message.text = "";

            GardenTestControls controls = managers.AddComponent<GardenTestControls>();
            SetReference(controls, "toolBelt", player.GetComponent<ToolBelt>());
            SetReference(controls, "interactor", player.GetComponent<Interactor>());
            SetReference(controls, "water", water);
            SetReference(controls, "weather", weather);
            SetReference(controls, "score", score);
            SetReference(controls, "toolText", tool);
            SetReference(controls, "targetText", target);
            SetReference(controls, "statsText", stats);
            SetReference(controls, "messageText", message);

            // the GameManager starts the test level directly (no menu in this scene)
            SetReference(game, "garden", garden);
            SetReference(game, "weather", weather);
            SetReference(game, "water", water);
            SetReference(game, "testLevel", level);
            SetBool(game, "startTestLevelOnPlay", true);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Test_Garden scene built: " + ScenePath);
        }

        // ---------- data ----------

        // uses the real crop of owner 2 when it exists (same cropName), otherwise a test crop
        private static CropData GetCrop(string cropName, int daysToGrow, float idealMin, float idealMax, string tip)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CropData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Test_Garden_")) continue;

                CropData existing = AssetDatabase.LoadAssetAtPath<CropData>(path);
                if (existing != null && existing.cropName == cropName) return existing;
            }

            string testPath = CropFolder + "/Test_Garden_" + cropName + ".asset";
            CropData crop = AssetDatabase.LoadAssetAtPath<CropData>(testPath);
            if (crop == null)
            {
                crop = ScriptableObject.CreateInstance<CropData>();
                AssetDatabase.CreateAsset(crop, testPath);
            }

            crop.cropName = cropName;
            crop.season = Season.Spring;
            crop.idealMoistureMin = idealMin;
            crop.idealMoistureMax = idealMax;
            crop.daysToGrow = daysToGrow;
            crop.maxHealth = 3;
            crop.tip = tip;
            EditorUtility.SetDirty(crop);
            return crop;
        }

        private static LevelData GetTestLevel(CropData[] seeds)
        {
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }

            level.levelName = "Test Garden";
            level.season = Season.Spring;
            level.allowedCrops = seeds;
            level.numberOfDays = 8;
            level.barrelLitresAtStart = 60f;
            level.tapAllowed = true;
            level.weatherPerDay = new WeatherType[]
            {
                WeatherType.Sunny, WeatherType.Sunny, WeatherType.Rainy, WeatherType.Sunny,
                WeatherType.HeatWave, WeatherType.Sunny, WeatherType.Rainy, WeatherType.Sunny
            };
            level.introText = "Test level for the garden and the player interaction.";
            EditorUtility.SetDirty(level);
            return level;
        }

        // ---------- scene ----------

        private static void CreateWaterAndWeather(out WaterManager water, out WeatherSystem weather)
        {
            GameObject systems = InstantiatePrefab(WaterPrefabPath);
            if (systems == null)
            {
                systems = new GameObject("WaterAndWeather");
                systems.AddComponent<WeatherSystem>();
                systems.AddComponent<WaterManager>();
            }

            water = systems.GetComponentInChildren<WaterManager>();
            weather = systems.GetComponentInChildren<WeatherSystem>();
            if (water == null) water = systems.AddComponent<WaterManager>();
            if (weather == null) weather = systems.AddComponent<WeatherSystem>();
            SetReference(water, "weather", weather);

            GameObject barrel = InstantiatePrefab(BarrelPrefabPath);
            if (barrel != null)
            {
                barrel.transform.position = new Vector3(-3f, 0f, 4f);
                RainBarrel gauge = barrel.GetComponent<RainBarrel>();
                if (gauge != null) SetReference(gauge, "water", water);
            }

            GameObject rain = InstantiatePrefab(RainPrefabPath);
            if (rain != null) rain.transform.position = new Vector3(0f, 6f, 4f);
        }

        // ---------- prefabs of owner 3 ----------

        private static Plant CreatePlantPlaceholderPrefab()
        {
            GameObject root = new GameObject("Plant_Placeholder");
            root.AddComponent<Plant>();
            PlantPlaceholderView view = root.AddComponent<PlantPlaceholderView>();

            GameObject shape = CreatePart(PrimitiveType.Cylinder, "Shape", root.transform, Vector3.zero, new Vector3(0.12f, 0.025f, 0.12f), GetMaterial("Mat_PlantPlaceholder", new Color(0.3f, 0.7f, 0.25f)));
            SetReference(view, "shape", shape.transform);
            SetReference(view, "shapeRenderer", shape.GetComponent<Renderer>());

            string path = PrefabFolder + "/Plant_Placeholder.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Plant>();
        }

        private static GardenGrid CreateGarden(Plant plantPrefab)
        {
            GameObject root = new GameObject("Garden_3x3");
            root.transform.position = new Vector3(0f, 0f, 4f);
            GardenGrid grid = root.AddComponent<GardenGrid>();

            Material soil = EnvironmentArt.SoilMaterial();
            Material wood = EnvironmentArt.WoodMaterial();
            Material highlight = EnvironmentArt.HighlightMaterial();
            Material mulch = EnvironmentArt.Plain("Mat_Env_Mulch", new Color(0.86f, 0.72f, 0.42f), 0f);
            float step = TileSize + TileGap;

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    Vector3 position = new Vector3((column - 1) * step, 0f, (1 - row) * step);
                    CreateTile("SoilTile " + (row * 3 + column + 1), root.transform, position, plantPrefab, soil, wood, highlight, mulch);
                }
            }

            SavePrefab(root, "Garden_3x3");
            return grid;
        }

        // the tile root is not scaled, so the plants that grow on it are not squashed
        // each tile is a small raised bed: soil inside a wooden frame a bit higher than the soil
        private static void CreateTile(string name, Transform parent, Vector3 position, Plant plantPrefab, Material soil, Material wood, Material highlight, Material mulch)
        {
            GameObject tile = new GameObject(name);
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = position;

            float outer = TileSize + 2f * PlankThickness;
            BoxCollider box = tile.AddComponent<BoxCollider>();
            box.size = new Vector3(outer, FrameHeight, outer);
            box.center = new Vector3(0f, FrameHeight / 2f, 0f);

            GameObject soilPart = CreatePart(PrimitiveType.Cube, "Soil", tile.transform, new Vector3(0f, SoilHeight / 2f, 0f), new Vector3(TileSize, SoilHeight, TileSize), soil);
            CreateFrame("Frame", tile.transform, 0f, wood);

            // the highlight is a yellow frame slightly bigger than the wooden one, shown on top of it
            GameObject rim = CreateFrame("Highlight", tile.transform, 0.015f, highlight);
            GameObject mulchPart = CreatePart(PrimitiveType.Cube, "Mulch", tile.transform, new Vector3(0f, SoilHeight + 0.015f, 0f), new Vector3(TileSize - 0.02f, 0.03f, TileSize - 0.02f), mulch);
            rim.SetActive(false);
            mulchPart.SetActive(false);

            GameObject plantPoint = new GameObject("PlantPoint");
            plantPoint.transform.SetParent(tile.transform, false);
            plantPoint.transform.localPosition = new Vector3(0f, SoilHeight, 0f);

            SoilTile soilTile = tile.AddComponent<SoilTile>();
            SetReference(soilTile, "plantPrefab", plantPrefab);
            SetReference(soilTile, "plantPoint", plantPoint.transform);
            SetReference(soilTile, "highlight", rim);
            SetReference(soilTile, "mulchVisual", mulchPart);

            // colour from dry to wet, from owner 4
            MoistureTint tint = tile.AddComponent<MoistureTint>();
            SetReference(tint, "soilRenderer", soilPart.GetComponent<Renderer>());
        }

        // 4 planks around the soil; grow > 0 makes a frame slightly bigger (used for the highlight)
        private static GameObject CreateFrame(string name, Transform parent, float grow, Material material)
        {
            GameObject frame = new GameObject(name);
            frame.transform.SetParent(parent, false);

            float thickness = PlankThickness + 2f * grow;
            float height = FrameHeight + grow;
            float length = TileSize + 2f * PlankThickness + 2f * grow;
            float offset = TileSize / 2f + PlankThickness / 2f;
            float y = height / 2f;

            CreatePart(PrimitiveType.Cube, "Plank", frame.transform, new Vector3(0f, y, offset), new Vector3(length, height, thickness), material);
            CreatePart(PrimitiveType.Cube, "Plank", frame.transform, new Vector3(0f, y, -offset), new Vector3(length, height, thickness), material);
            CreatePart(PrimitiveType.Cube, "Plank", frame.transform, new Vector3(offset, y, 0f), new Vector3(thickness, height, TileSize), material);
            CreatePart(PrimitiveType.Cube, "Plank", frame.transform, new Vector3(-offset, y, 0f), new Vector3(thickness, height, TileSize), material);
            return frame;
        }

        private static void CreateBell()
        {
            GameObject root = new GameObject("DayBell");
            root.transform.position = new Vector3(3.2f, 0f, 2.5f);
            DayBell bell = root.AddComponent<DayBell>();

            Material wood = EnvironmentArt.DarkWoodMaterial();
            Material brass = EnvironmentArt.Plain("Mat_Env_Brass", new Color(0.88f, 0.68f, 0.25f), 0.75f);
            brass.SetFloat("_Metallic", 0.6f);

            CreatePart(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.1f, 0.75f, 0.1f), wood);
            CreatePart(PrimitiveType.Cube, "Arm", root.transform, new Vector3(0.2f, 1.5f, 0f), new Vector3(0.5f, 0.06f, 0.06f), wood);

            GameObject pivot = new GameObject("SwingPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0.4f, 1.47f, 0f);
            CreatePart(PrimitiveType.Sphere, "Bell", pivot.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.3f, 0.3f, 0.3f), brass);

            SetReference(bell, "swingPart", pivot.transform);
            SavePrefab(root, "DayBell");
        }

        private static GameObject CreatePlayer(CropData[] seeds)
        {
            GameObject root = new GameObject("Player");
            root.transform.position = new Vector3(0f, 0.05f, 0f);

            CharacterController body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.9f, 0f);

            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            pivot.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);

            Camera cam = pivot.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.Skybox;
            pivot.AddComponent<AudioListener>();
            pivot.tag = "MainCamera";

            PlayerController controller = root.AddComponent<PlayerController>();
            ToolBelt toolBelt = root.AddComponent<ToolBelt>();
            Interactor interactor = root.AddComponent<Interactor>();
            DesktopInput input = root.AddComponent<DesktopInput>();

            SetReference(controller, "cameraPivot", pivot.transform);
            SetObjectArray(toolBelt, "fallbackSeeds", seeds);
            SetReference(interactor, "toolBelt", toolBelt);
            SetReference(input, "player", controller);
            SetReference(input, "toolBelt", toolBelt);
            SetReference(input, "interactor", interactor);
            SetReference(input, "viewCamera", cam);

            SavePrefab(root, "Player");
            return root;
        }

        // ---------- UI ----------

        private static Transform CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return canvasObject.transform;
        }

        // a dark panel with a text inside
        private static TextMeshProUGUI CreatePanelText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject panel = new GameObject(name + " Panel", typeof(RectTransform), typeof(Image));
            Place(panel, parent, anchor, position, size);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            TextMeshProUGUI text = CreateText(name, panel.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 20f), fontSize, TextAlignmentOptions.TopLeft);
            return text;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            Place(textObject, parent, anchor, position, size);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = name;
            return text;
        }

        private static void Place(GameObject uiObject, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = (RectTransform)uiObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // ---------- helpers ----------

        // a primitive without collider (only the tile, the bell and the ground are hit by the ray)
        private static GameObject CreatePart(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;

            // the bell keeps its colliders so the player can click it
            if (parent.GetComponentInParent<DayBell>() == null) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static GameObject InstantiatePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = ArtFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SavePrefab(GameObject sceneObject, string name)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(sceneObject, PrefabFolder + "/" + name + ".prefab", InteractionMode.AutomatedAction);
        }

        private static void CreateFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning("Field '" + field + "' not found on " + target.GetType().Name);
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty list = serialized.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
