using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GardenWise
{
    // owner 4 - menu "GardenWise > Build Test_Water Scene": builds the test scene and the water prefabs
    // run it again at any time to rebuild the scene from scratch
    public static class WaterTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Tests/Test_Water.unity";
        private const string LevelPath = "Assets/_Project/Data/Levels/Test_Water_Level.asset";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Water";
        private const string ArtFolder = "Assets/_Project/Art/Water";

        [MenuItem("GardenWise/Build Test_Water Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            CreateFolder("Assets/_Project/Art", "Water");
            CreateFolder("Assets/_Project/Prefabs", "Water");

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LevelData level = GetTestLevel();

            SetupCamera();
            CreateGround();

            // water and weather system (delivered as one prefab)
            GameObject systems = new GameObject("WaterAndWeather");
            WeatherSystem weather = systems.AddComponent<WeatherSystem>();
            WaterManager water = systems.AddComponent<WaterManager>();
            SetReference(water, "weather", weather);
            SavePrefab(systems, "WaterAndWeather");

            // other systems needed to run the loop in this test scene
            GameObject managers = new GameObject("TestManagers");
            GameManager game = managers.AddComponent<GameManager>();
            ScoreManager score = managers.AddComponent<ScoreManager>();

            GardenGrid garden = new GameObject("Garden").AddComponent<GardenGrid>();
            SoilTile[] tiles = CreateTiles(garden.transform);

            RainBarrel barrel = CreateBarrel();
            SetReference(barrel, "water", water);

            CreateRain();

            // UI
            Transform canvas = CreateCanvas();
            WeatherBoard board = CreateWeatherBoard(canvas);
            SetReference(board, "weather", weather);

            TextMeshProUGUI stats = CreatePanelText("Stats", canvas, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(600f, 470f), 24f);
            TextMeshProUGUI message = CreateText("Message", canvas, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1100f, 50f), 26f, TextAlignmentOptions.Center);
            message.text = "";

            WaterTestControls controls = managers.AddComponent<WaterTestControls>();
            SetReference(controls, "water", water);
            SetReference(controls, "weather", weather);
            SetReference(controls, "score", score);
            SetReference(controls, "statsText", stats);
            SetReference(controls, "messageText", message);
            SetTiles(controls, tiles);
            CreateButtons(canvas, controls);

            // the GameManager starts the test level directly (no menu in this scene)
            SetReference(game, "garden", garden);
            SetReference(game, "weather", weather);
            SetReference(game, "water", water);
            SetReference(game, "testLevel", level);
            SetBool(game, "startTestLevelOnPlay", true);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Test_Water scene built: " + ScenePath);
        }

        private static LevelData GetTestLevel()
        {
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, LevelPath);
            }

            level.levelName = "Test Water";
            level.season = Season.Summer;
            level.numberOfDays = 8;
            level.barrelLitresAtStart = 60f;
            level.tapAllowed = true;
            level.weatherPerDay = new WeatherType[]
            {
                WeatherType.Sunny, WeatherType.Sunny, WeatherType.Rainy, WeatherType.Sunny,
                WeatherType.HeatWave, WeatherType.HeatWave, WeatherType.Rainy, WeatherType.Sunny
            };
            level.introText = "Test level for the water and weather system.";
            EditorUtility.SetDirty(level);
            return level;
        }

        private static void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(0.8f, 5.5f, -6.5f);
            cam.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            cam.backgroundColor = new Color(0.62f, 0.8f, 0.95f);
        }

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            ground.GetComponent<Renderer>().sharedMaterial = GetMaterial("Mat_Grass", new Color(0.42f, 0.62f, 0.32f));
        }

        private static SoilTile[] CreateTiles(Transform parent)
        {
            SoilTile[] tiles = new SoilTile[3];
            Material soil = GetMaterial("Mat_Soil", new Color(0.55f, 0.4f, 0.25f));

            for (int i = 0; i < tiles.Length; i++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "SoilTile " + (i + 1);
                tile.transform.SetParent(parent);
                tile.transform.localPosition = new Vector3((i - 1) * 1.6f, 0.1f, 0f);
                tile.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
                tile.GetComponent<Renderer>().sharedMaterial = soil;

                tiles[i] = tile.AddComponent<SoilTile>();
                tile.AddComponent<MoistureTint>();

                CreateLabel("Tile " + (i + 1), new Vector3((i - 1) * 1.6f, 0.9f, 0f));
            }
            return tiles;
        }

        private static RainBarrel CreateBarrel()
        {
            GameObject root = new GameObject("RainBarrel");
            root.transform.position = new Vector3(3.4f, 0f, 0.6f);
            RainBarrel barrel = root.AddComponent<RainBarrel>();

            Transform waterPart = CreateCylinder("Water", root.transform, GetMaterial("Mat_BarrelWater", new Color(0.2f, 0.5f, 0.95f)));
            Transform emptyPart = CreateCylinder("Empty", root.transform, GetMaterial("Mat_BarrelWood", new Color(0.45f, 0.3f, 0.18f)));

            SetReference(barrel, "waterPart", waterPart);
            SetReference(barrel, "emptyPart", emptyPart);
            SavePrefab(root, "RainBarrel");

            CreateLabel("Rain barrel", new Vector3(3.4f, 1.6f, 0.6f));
            return barrel;
        }

        private static Transform CreateCylinder(string name, Transform parent, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent);
            cylinder.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            cylinder.transform.localScale = new Vector3(0.9f, 0.3f, 0.9f);
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            return cylinder.transform;
        }

        private static void CreateRain()
        {
            GameObject rainObject = new GameObject("Rain");
            rainObject.transform.position = new Vector3(0.8f, 6f, 0f);
            rainObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem rain = rainObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = rain.main;
            main.playOnAwake = false;
            main.startLifetime = 1f;
            main.startSpeed = 7f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.75f, 0.85f, 1f, 0.8f);
            main.maxParticles = 3000;

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 600f;

            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(9f, 5f, 0.1f);

            ParticleSystemRenderer rainRenderer = rainObject.GetComponent<ParticleSystemRenderer>();
            rainRenderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.velocityScale = 0.04f;
            rainRenderer.lengthScale = 1f;

            rainObject.AddComponent<RainEffect>();
            SavePrefab(rainObject, "Rain");
        }

        private static void CreateLabel(string text, Vector3 position)
        {
            GameObject label = new GameObject("Label " + text);
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(38f, 0f, 0f);

            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
        }

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

        private static WeatherBoard CreateWeatherBoard(Transform canvas)
        {
            GameObject boardObject = new GameObject("WeatherBoard", typeof(RectTransform));
            RectTransform rect = (RectTransform)boardObject.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            WeatherBoard board = boardObject.AddComponent<WeatherBoard>();
            TextMeshProUGUI today = CreatePanelText("Today", rect, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(420f, 120f), 30f);
            TextMeshProUGUI forecast = CreatePanelText("Forecast", rect, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(1300f, 60f), 24f);
            forecast.alignment = TextAlignmentOptions.MidlineLeft;

            SetReference(board, "todayText", today);
            SetReference(board, "forecastText", forecast);
            SavePrefab(boardObject, "WeatherBoard");
            return board;
        }

        private static void CreateButtons(Transform canvas, WaterTestControls controls)
        {
            Button endDay = CreateButton("End day (Space)", canvas, 0);
            UnityEventTools.AddPersistentListener(endDay.onClick, controls.EndDay);

            Button mulch = CreateButton("Mulch (M)", canvas, 1);
            UnityEventTools.AddPersistentListener(mulch.onClick, controls.AddMulch);

            Button tap = CreateButton("Water: tap (T)", canvas, 2);
            UnityEventTools.AddPersistentListener(tap.onClick, controls.WaterFromTap);

            Button barrel = CreateButton("Water: barrel (B)", canvas, 3);
            UnityEventTools.AddPersistentListener(barrel.onClick, controls.WaterFromBarrel);

            for (int i = 2; i >= 0; i--)
            {
                Button select = CreateButton("Select tile " + (i + 1) + " (" + (i + 1) + ")", canvas, 6 - i);
                UnityEventTools.AddIntPersistentListener(select.onClick, controls.SelectTile, i);
            }
        }

        private static Button CreateButton(string label, Transform canvas, int row)
        {
            GameObject buttonObject = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            buttonObject.name = "Button " + label;
            Place(buttonObject, canvas, new Vector2(1f, 0f), new Vector2(-30f, 30f + row * 66f), new Vector2(300f, 56f));

            TextMeshProUGUI text = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            return buttonObject.GetComponent<Button>();
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
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetTiles(WaterTestControls controls, SoilTile[] tiles)
        {
            SerializedObject serialized = new SerializedObject(controls);
            SerializedProperty list = serialized.FindProperty("tiles");
            list.arraySize = tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
