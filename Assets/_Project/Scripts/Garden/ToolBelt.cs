using System;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - remembers the tool in the player's hand, the seed in the seed bag and the water source of the can
    // it only stores choices: the desktop keys (DesktopInput) or later the VR controllers call Select / NextSeed / ToggleWaterSource
    public class ToolBelt : MonoBehaviour
    {
        [Header("Seeds used when no level is running (test scene)")]
        [SerializeField] private CropData[] fallbackSeeds = new CropData[0];

        [Header("Optional: HUD of owner 6, shows the tool name")]
        [SerializeField] private HudDisplay hud;

        private CropData[] seeds = new CropData[0];
        private int seedIndex;

        public GardenTool CurrentTool { get; private set; } = GardenTool.SeedBag;
        public WaterSource CurrentWaterSource { get; private set; } = WaterSource.RainBarrel;

        public CropData SelectedSeed
        {
            get { return seeds.Length == 0 ? null : seeds[seedIndex]; }
        }

        // raised every time the tool, the seed or the water source changes
        public event Action<ToolBelt> Changed;

        private void OnEnable()
        {
            GameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.GameStateChanged -= HandleGameStateChanged;
        }

        private void Start()
        {
            RefreshSeeds();
            Notify();
        }

        // pressing the key of the tool already in hand changes its option (next seed, or barrel / tap)
        public void Select(GardenTool tool)
        {
            if (tool == CurrentTool)
            {
                if (tool == GardenTool.SeedBag) NextSeed();
                else if (tool == GardenTool.WateringCan) ToggleWaterSource();
                return;
            }

            CurrentTool = tool;
            Notify();
        }

        // +1 or -1, used by the mouse wheel
        public void SelectNext(int direction)
        {
            int count = Enum.GetValues(typeof(GardenTool)).Length;
            int index = ((int)CurrentTool + direction) % count;
            if (index < 0) index += count;

            CurrentTool = (GardenTool)index;
            Notify();
        }

        public void NextSeed()
        {
            if (seeds.Length == 0) return;
            seedIndex = (seedIndex + 1) % seeds.Length;
            Notify();
        }

        public void ToggleWaterSource()
        {
            CurrentWaterSource = CurrentWaterSource == WaterSource.Tap ? WaterSource.RainBarrel : WaterSource.Tap;
            Notify();
        }

        public string GetLabel()
        {
            switch (CurrentTool)
            {
                case GardenTool.SeedBag:
                    return "Seed bag (" + (SelectedSeed != null ? SelectedSeed.cropName : "empty") + ")";
                case GardenTool.WateringCan:
                    return "Watering can (" + (CurrentWaterSource == WaterSource.Tap ? "tap" : "rain barrel") + ")";
                case GardenTool.Mulch:
                    return "Mulch";
                default:
                    return "Harvest basket";
            }
        }

        // a new level gives new seeds: the crops allowed by its LevelData
        private void HandleGameStateChanged(GameState state)
        {
            if (state != GameState.LevelIntro) return;
            RefreshSeeds();
            Notify();
        }

        private void RefreshSeeds()
        {
            GameManager game = GameManager.Instance;
            CropData[] levelSeeds = game != null && game.CurrentLevel != null ? game.CurrentLevel.allowedCrops : null;

            if (levelSeeds != null && levelSeeds.Length > 0) seeds = levelSeeds;
            else seeds = fallbackSeeds != null ? fallbackSeeds : new CropData[0];

            seedIndex = 0;
        }

        private void Notify()
        {
            if (hud != null) hud.ShowTool(GetLabel());
            if (Changed != null) Changed(this);
        }
    }
}
