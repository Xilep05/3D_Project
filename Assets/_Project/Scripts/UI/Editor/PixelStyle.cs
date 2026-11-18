using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GardenWise
{
    // the pixel-art look of Gaston's panel, for the whole game: menus, HUD, weather panels
    // menu "GardenWise > Apply pixel style to the UI" restyles the UI and weather prefabs (no hand-edited YAML);
    // MainSceneBuilder also calls it for the texts it creates. Run it again after changing a prefab: it is idempotent
    public static class PixelStyle
    {
        public const string ArtFolder = "Assets/_Project/Art/Gardener";
        public const string FontPath = ArtFolder + "/Fonts/PixelifySans SDF.asset";
        public const string GreenFramePath = ArtFolder + "/UI/gaston_badge.png";
        public const string CreamFramePath = ArtFolder + "/UI/gaston_bubble.png";
        public const string FieldFramePath = ArtFolder + "/UI/gaston_field.png";

        private const string UIPrefab = "Assets/_Project/Prefabs/UI/UI_Canvas.prefab";
        private const string WeatherBoardPrefab = "Assets/_Project/Prefabs/Water/WeatherBoard.prefab";

        // the frames are drawn at 12.5 pixels per unit (8 units per drawn pixel): this brings them to 6 units
        public const float FrameScale = 8f / 6f;

        // GardenWise palette
        public static readonly Color32 Ink = new Color32(0x3A, 0x2A, 0x1E, 0xFF);
        public static readonly Color32 SoftInk = new Color32(0x7A, 0x66, 0x55, 0xFF);
        public static readonly Color32 Cream = new Color32(0xFF, 0xF8, 0xE7, 0xFF);
        public static readonly Color32 LeafGreen = new Color32(0x2F, 0x6B, 0x2F, 0xFF);
        public static readonly Color32 Sky = new Color32(0xBF, 0xE3, 0xF2, 0xFF);
        public static readonly Color32 Grass = new Color32(0x8F, 0xCF, 0x6A, 0xFF);
        public static readonly Color32 Sun = new Color32(0xF5, 0xC5, 0x42, 0xFF);

        // the full-screen panels of the UI: sky and grass behind, like Gaston's test scene
        private static readonly string[] ScreenPanels = { "MainMenuPanel", "LevelSelectPanel", "IntroPanel", "ResultPanel" };

        [MenuItem("GardenWise/Apply pixel style to the UI")]
        public static void ApplyToPrefabs()
        {
            EditPrefab(UIPrefab, StyleCanvas);
            EditPrefab(WeatherBoardPrefab, StyleWeatherBoard);
            AssetDatabase.SaveAssets();
            Debug.Log("[PixelStyle] UI_Canvas and WeatherBoard restyled");
        }

        private static void EditPrefab(string path, System.Action<GameObject> style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("[PixelStyle] Prefab not found: " + path);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                style(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ the UI of owner 6

        public static void StyleCanvas(GameObject canvas)
        {
            foreach (string name in ScreenPanels)
            {
                Transform panel = canvas.transform.Find(name);
                if (panel != null) StyleScreen(panel);
            }

            // intro and results: their texts stand on a cream sheet, like Gaston's bubble
            AddSheet(canvas.transform.Find("IntroPanel"), new Vector2(0f, 40f), new Vector2(1500f, 640f));
            AddSheet(canvas.transform.Find("ResultPanel"), new Vector2(0f, 40f), new Vector2(1500f, 780f));

            // HUD bar: Gaston's header, a little inside the screen edges
            Transform topBar = FindDeep(canvas.transform, "TopBar");
            if (topBar != null)
            {
                RectTransform bar = (RectTransform)topBar;
                bar.anchoredPosition = new Vector2(0f, -12f);
                bar.sizeDelta = new Vector2(-32f, 78f);
                Frame(topBar.GetComponent<Image>(), GreenFramePath);
            }

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true)) StyleButton(button);

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "StarsBack") image.color = new Color32(0x3A, 0x2A, 0x1E, 0x40);
                else if (image.name == "StarsFill") image.color = Sun;
            }

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, TextColour(text));
        }

        private static void StyleScreen(Transform panel)
        {
            Image sky = panel.GetComponent<Image>();
            if (sky != null)
            {
                sky.sprite = null;
                sky.color = Sky;
            }

            if (panel.Find("Grass") != null) return;

            GameObject grass = new GameObject("Grass", typeof(RectTransform), typeof(Image));
            grass.layer = panel.gameObject.layer;
            RectTransform rect = (RectTransform)grass.transform;
            rect.SetParent(panel, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0.3f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            Image image = grass.GetComponent<Image>();
            image.color = Grass;
            image.raycastTarget = false;
        }

        private static void AddSheet(Transform panel, Vector2 position, Vector2 size)
        {
            if (panel == null || panel.Find("Sheet") != null) return;

            GameObject sheet = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
            sheet.layer = panel.gameObject.layer;
            RectTransform rect = (RectTransform)sheet.transform;
            rect.SetParent(panel, false);
            rect.SetSiblingIndex(1); // above the grass, under the texts
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = sheet.GetComponent<Image>();
            image.raycastTarget = false;
            Frame(image, CreamFramePath);
        }

        private static void StyleButton(Button button)
        {
            // the level cards are cream sheets, the other buttons are green like Gaston's header
            bool card = button.GetComponent<LevelButton>() != null;
            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image != null) Frame(image, card ? CreamFramePath : GreenFramePath);

            ColorBlock colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(0.86f, 0.94f, 0.82f);
            colours.selectedColor = Color.white;
            colours.pressedColor = new Color(0.72f, 0.82f, 0.68f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colours.colorMultiplier = 1f;
            button.colors = colours;
            button.transition = Selectable.Transition.ColorTint;
        }

        private static Color32 TextColour(TMP_Text text)
        {
            if (InsideGreenFrame(text.transform)) return Cream;

            switch (text.name)
            {
                case "Title":
                case "LevelName":
                case "Speaker":
                    return LeafGreen;
                case "BestText":
                    return SoftInk;
                default:
                    return Ink;
            }
        }

        private static bool InsideGreenFrame(Transform transform)
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.GetComponent<LevelButton>() != null) return false;
                Image image = parent.GetComponent<Image>();
                if (image != null && image.sprite != null && AssetDatabase.GetAssetPath(image.sprite) == GreenFramePath) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ the weather panels of owner 4

        public static void StyleWeatherBoard(GameObject board)
        {
            foreach (Image image in board.GetComponentsInChildren<Image>(true))
            {
                if (image.name.EndsWith("Panel")) Frame(image, CreamFramePath);
            }

            foreach (TMP_Text text in board.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, Ink);
        }

        // ------------------------------------------------------------------ shared pieces

        // texts drawn over the 3D view (crosshair, aimed tile): cream, readable on the grass and the soil
        public static void StyleTextsOverWorld(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                StyleText(text, Cream);
                text.raycastTarget = false; // a click there takes the mouse back to the game
            }
        }

        public static void StyleText(TMP_Text text, Color colour)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }

            text.fontStyle &= ~FontStyles.Bold;
            text.color = colour;
            EditorUtility.SetDirty(text);
        }

        // a pixel frame that keeps its corners and stretches its middle (9-slice, 3 pixel border)
        public static void Frame(Image image, string spritePath)
        {
            if (image == null) return;

            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null && importer.spriteBorder != new Vector4(3f, 3f, 3f, 3f))
            {
                importer.spriteBorder = new Vector4(3f, 3f, 3f, 3f);
                importer.SaveAndReimport();
            }

            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = FrameScale;
            image.color = Color.white;
            EditorUtility.SetDirty(image);

            if (image.sprite == null) Debug.LogError("[PixelStyle] Sprite not found: " + spritePath);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            Queue<Transform> queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                if (current.name == name) return current;
                foreach (Transform child in current) queue.Enqueue(child);
            }

            return null;
        }
    }
}
