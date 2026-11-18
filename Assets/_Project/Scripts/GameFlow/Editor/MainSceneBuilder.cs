using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GardenWise
{
    // owner 1 (integration) - menu "GardenWise > Build Main Scene": assembles Main.unity from the prefabs of every owner
    // and links them together. Run it again whenever a prefab changes: the scene is rebuilt from scratch.
    public static class MainSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        private const string GameFlowPrefab = "Assets/_Project/Prefabs/GameFlow/GameFlow.prefab";
        private const string UIPrefab = "Assets/_Project/Prefabs/UI/UI_Canvas.prefab";
        private const string WaterPrefab = "Assets/_Project/Prefabs/Water/WaterAndWeather.prefab";
        private const string RainPrefab = "Assets/_Project/Prefabs/Water/Rain.prefab";
        private const string BarrelPrefab = "Assets/_Project/Prefabs/Water/RainBarrel.prefab";
        private const string BoardPrefab = "Assets/_Project/Prefabs/Water/WeatherBoard.prefab";
        private const string GardenPrefab = "Assets/_Project/Prefabs/Garden/Garden_3x3.prefab";
        private const string BellPrefab = "Assets/_Project/Prefabs/Garden/DayBell.prefab";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Garden/Player.prefab";
        private const string GrassMaterial = "Assets/_Project/Art/Garden/Mat_Grass.mat";

        // level select order: level 1, level 2 (levels 3 to 5 are bonus, added here when they exist)
        private static readonly string[] LevelPaths =
        {
            "Assets/_Project/Data/Levels/Level_FirstSprouts.asset",
            "Assets/_Project/Data/Levels/Level_SummerDuo.asset"
        };

        [MenuItem("GardenWise/Build Main Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            List<string> missing = new List<string>();
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // the player brings its own camera
            if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);

            CreateGround();

            // ---- systems ----
            GameObject flow = Place(GameFlowPrefab, Vector3.zero, missing);
            GameObject waterAndWeather = Place(WaterPrefab, Vector3.zero, missing);
            Place(RainPrefab, new Vector3(0f, 6f, 4f), missing);
            GameObject barrel = Place(BarrelPrefab, new Vector3(-3f, 0f, 4f), missing);

            // ---- garden and player (owner 3) ----
            GameObject garden = Place(GardenPrefab, new Vector3(0f, 0f, 4f), missing);
            Place(BellPrefab, new Vector3(3.2f, 0f, 2.5f), missing);
            GameObject player = Place(PlayerPrefab, new Vector3(0f, 0.05f, 0f), missing);

            // ---- UI (owner 6) ----
            GameObject ui = Place(UIPrefab, Vector3.zero, missing);

            GameManager game = Find<GameManager>(flow);
            WaterManager water = Find<WaterManager>(waterAndWeather);
            WeatherSystem weather = Find<WeatherSystem>(waterAndWeather);
            GardenGrid grid = Find<GardenGrid>(garden);
            ToolBelt toolBelt = Find<ToolBelt>(player);
            Interactor interactor = Find<Interactor>(player);
            MenuManager menu = Find<MenuManager>(ui);
            HudDisplay hud = Find<HudDisplay>(ui);

            // ---- links between the systems ----
            if (game != null)
            {
                SetReference(game, "garden", grid);
                SetReference(game, "weather", weather);
                SetReference(game, "water", water);
                SetBool(game, "startTestLevelOnPlay", false);
            }

            if (water != null) SetReference(water, "weather", weather);
            if (barrel != null) SetReference(Find<RainBarrel>(barrel), "water", water);
            if (hud != null) SetReference(hud, "water", water);
            if (toolBelt != null) SetReference(toolBelt, "hud", hud);
            if (interactor != null) SetReference(interactor, "water", water);

            if (menu != null) SetObjectArray(menu, "levels", LoadLevels(missing));

            // HUD extras: weather board (owner 4), crosshair and interaction feedback (owner 3)
            GameObject hudPanel = menu != null ? GetObjectField(menu, "hudPanel") as GameObject : null;
            if (hudPanel != null)
            {
                AddWeatherBoard(hudPanel.transform, weather, missing);
                AddInteractionFeedback(hudPanel.transform, interactor);
            }
            else
            {
                missing.Add("HudPanel of the MenuManager");
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            if (missing.Count == 0) Debug.Log("Main scene built: " + ScenePath);
            else Debug.LogWarning("Main scene built, but missing: " + string.Join(", ", missing));
        }

        // ---------- pieces ----------

        private static void CreateGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);

            Material grass = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterial);
            if (grass != null) ground.GetComponent<Renderer>().sharedMaterial = grass;
        }

        private static LevelData[] LoadLevels(List<string> missing)
        {
            List<LevelData> levels = new List<LevelData>();
            foreach (string path in LevelPaths)
            {
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level != null) levels.Add(level);
                else missing.Add(path);
            }
            return levels.ToArray();
        }

        // the weather board is a screen panel: it goes in the HUD, so it is only visible while playing
        private static void AddWeatherBoard(Transform hudPanel, WeatherSystem weather, List<string> missing)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefab);
            if (prefab == null)
            {
                missing.Add(BoardPrefab);
                return;
            }

            GameObject board = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hudPanel);
            SetReference(Find<WeatherBoard>(board), "weather", weather);

            // "Today" panel moved under the HUD top bar (90 px) so it does not cover the tool name
            Transform today = board.transform.Find("Today Panel");
            if (today != null) ((RectTransform)today).anchoredPosition = new Vector2(-30f, -120f);
        }

        private static void AddInteractionFeedback(Transform hudPanel, Interactor interactor)
        {
            GameObject root = new GameObject("InteractionFeedback", typeof(RectTransform));
            RectTransform rect = (RectTransform)root.transform;
            rect.SetParent(hudPanel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateText("Crosshair", root.transform, new Vector2(0f, 0f), new Vector2(60f, 60f), 40f, TextAlignmentOptions.Center).text = "+";
            TextMeshProUGUI target = CreateText("TargetText", root.transform, new Vector2(0f, -100f), new Vector2(800f, 90f), 24f, TextAlignmentOptions.Top);
            TextMeshProUGUI message = CreateText("MessageText", root.transform, new Vector2(0f, -330f), new Vector2(1400f, 60f), 28f, TextAlignmentOptions.Center);
            target.text = "";
            message.text = "";

            InteractionFeedback feedback = root.AddComponent<InteractionFeedback>();
            SetReference(feedback, "interactor", interactor);
            SetReference(feedback, "targetText", target);
            SetReference(feedback, "messageText", message);
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- helpers ----------

        private static GameObject Place(string path, Vector3 position, List<string> missing)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                missing.Add(path);
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return instance;
        }

        private static T Find<T>(GameObject root) where T : Component
        {
            return root != null ? root.GetComponentInChildren<T>(true) : null;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            if (target == null) return;

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

        private static Object GetObjectField(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property != null ? property.objectReferenceValue : null;
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
