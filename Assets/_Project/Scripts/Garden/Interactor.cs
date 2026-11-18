using System;
using UnityEngine;

namespace GardenWise
{
    // owner 3 - does what the tool in hand does on the soil tile being aimed at (plant, water, mulch, harvest)
    // no key or mouse is read here: DesktopInput gives a ray from the camera, the VR rig will give the controller ray
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private ToolBelt toolBelt;
        [SerializeField] private float reach = 4f;
        [SerializeField] private LayerMask layers = ~0;

        [Header("Scene reference (owner 4), found automatically if empty")]
        [SerializeField] private WaterManager water;

        [Header("Only used when there is no WaterManager in the scene")]
        [SerializeField] private float fallbackLitresPerPour = 2f;

        public SoilTile TargetTile { get; private set; }
        public DayBell TargetBell { get; private set; }

        // short text for the player after each action ("Planted Radish", "The barrel is empty"...)
        public event Action<string> Message;

        // what just happened on which tile (for the particles, the sounds, the VR haptics...)
        public event Action<SoilTile> Planted;
        public event Action<SoilTile> Watered;
        public event Action<SoilTile> Mulched;
        public event Action<SoilTile> Harvested;

        private void Awake()
        {
            if (toolBelt == null) toolBelt = GetComponent<ToolBelt>();
            if (water == null) water = FindFirstObjectByType<WaterManager>();
        }

        // updates what the player is aiming at: call it every frame with the camera ray (desktop) or the controller ray (VR)
        public void Aim(Ray ray)
        {
            SoilTile tile = null;
            DayBell bell = null;

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, reach, layers, QueryTriggerInteraction.Ignore))
            {
                tile = hit.collider.GetComponentInParent<SoilTile>();
                if (tile == null) bell = hit.collider.GetComponentInParent<DayBell>();
            }

            SetTarget(tile);
            TargetBell = bell;
        }

        public void ClearAim()
        {
            SetTarget(null);
            TargetBell = null;
        }

        // click (desktop) or trigger (VR): rings the bell or uses the tool in hand on the tile aimed at
        public void UseCurrentTool()
        {
            if (TargetBell != null)
            {
                RingBell(TargetBell);
                return;
            }

            if (TargetTile == null) return;
            UseTool(toolBelt.CurrentTool, TargetTile);
        }

        // VR entry point too: a grabbed watering can poured on a tile can call UseTool(GardenTool.WateringCan, tile)
        public bool UseTool(GardenTool tool, SoilTile tile)
        {
            if (tile == null || !CanAct()) return false;

            switch (tool)
            {
                case GardenTool.SeedBag: return PlantSeed(tile);
                case GardenTool.WateringCan: return WaterTile(tile);
                case GardenTool.Mulch: return AddMulch(tile);
                default: return Harvest(tile);
            }
        }

        public bool RingBell(DayBell bell)
        {
            if (bell == null) return false;

            if (!bell.Ring())
            {
                Say("The bell only works during a day of the level");
                return false;
            }

            Say("Ding! The night goes by...");
            return true;
        }

        private bool PlantSeed(SoilTile tile)
        {
            CropData seed = toolBelt.SelectedSeed;

            if (tile.CurrentPlant != null) return Fail("There is already a plant here");
            if (seed == null) return Fail("No seed in the bag for this level");
            if (!tile.PlantCrop(seed)) return Fail("Nothing can be planted here (no plant prefab on the tile)");

            if (Planted != null) Planted(tile);
            Say("Planted " + seed.cropName + ". It likes a moisture of " + Mathf.RoundToInt(seed.idealMoistureMin) + " to " + Mathf.RoundToInt(seed.idealMoistureMax));
            return true;
        }

        private bool WaterTile(SoilTile tile)
        {
            WaterSource source = toolBelt.CurrentWaterSource;
            float before = tile.Moisture;

            if (water != null)
            {
                // owner 4 takes the litres, waters the tile and counts useful and wasted water for the score
                if (!water.WaterTile(tile, source))
                {
                    if (source == WaterSource.Tap) return Fail("The tap is not allowed in this level");
                    return Fail("The rain barrel is empty: wait for the rain or use the tap");
                }
            }
            else
            {
                tile.AddWater(fallbackLitresPerPour);
            }

            if (Watered != null) Watered(tile);
            string message = "Watered: moisture " + Mathf.RoundToInt(before) + " -> " + Mathf.RoundToInt(tile.Moisture);
            Plant plant = tile.CurrentPlant;
            if (plant != null && plant.Data != null && tile.Moisture > plant.Data.idealMoistureMax)
            {
                message += ". Careful, too wet for " + plant.Data.cropName;
            }
            Say(message);
            return true;
        }

        private bool AddMulch(SoilTile tile)
        {
            if (tile.HasMulch) return Fail("This tile already has mulch");

            tile.AddMulch();
            if (Mulched != null) Mulched(tile);
            Say("Mulch added: this soil now dries half as fast");
            return true;
        }

        private bool Harvest(SoilTile tile)
        {
            Plant plant = tile.CurrentPlant;
            if (plant == null) return Fail("Nothing to harvest here");

            if (plant.IsDead)
            {
                tile.ClearPlant();
                Say("Dead plant removed, the tile is free again");
                return true;
            }

            if (!plant.IsReady) return Fail(plant.Data.cropName + " is not ready yet (" + plant.Stage + ")");

            // the plant is removed right after, so it can never be harvested (and counted) twice
            string cropName = plant.Data.cropName;
            float quality = plant.Harvest();
            tile.ClearPlant();
            if (Harvested != null) Harvested(tile);

            Say("Harvested " + cropName + ", quality " + Mathf.RoundToInt(quality * 100f) + " %");
            return true;
        }

        // actions are only possible during a day of the level (always possible when there is no GameManager)
        private bool CanAct()
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.IsPlaying) return true;
            return Fail("Wait for the day to start");
        }

        private void SetTarget(SoilTile tile)
        {
            if (tile == TargetTile) return;

            if (TargetTile != null) TargetTile.SetHighlighted(false);
            TargetTile = tile;
            if (TargetTile != null) TargetTile.SetHighlighted(true);
        }

        private bool Fail(string message)
        {
            Say(message);
            return false;
        }

        private void Say(string message)
        {
            if (Message != null) Message(message);
        }
    }
}
