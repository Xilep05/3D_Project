using UnityEngine;

namespace GardenWise
{
    // owner 6 - only for the Test_UI scene: buttons that fake what the other systems will do
    public class UITestButtons : MonoBehaviour
    {
        [SerializeField] private WaterManager water;
        [SerializeField] private HudDisplay hud;
        [SerializeField] private float litresPerClick = 2f;

        private readonly string[] tools = { "Seed bag", "Watering can", "Mulch", "Harvest basket" };
        private int toolIndex;

        // owner 3 will raise this event when a seed is planted
        public void PlantSeed()
        {
            GameEvents.RaisePlantPlanted(null);
        }

        // owner 2 will raise this event with the real quality of the plant
        public void HarvestPlant()
        {
            GameEvents.RaisePlantHarvested(null, 1f);
        }

        public void WaterFromBarrel()
        {
            UseWater(WaterSource.RainBarrel);
        }

        public void WaterFromTap()
        {
            UseWater(WaterSource.Tap);
        }

        public void NextTool()
        {
            toolIndex = (toolIndex + 1) % tools.Length;
            hud.ShowTool(tools[toolIndex]);
        }

        public void AskGaston()
        {
            GameEvents.RaiseGardenerAsked("Test question");
        }

        public void EndDay()
        {
            GameManager.Instance.EndDay();
        }

        public void DeleteSave()
        {
            SaveSystem.Delete();
            Debug.Log("Save file deleted");
        }

        private void UseWater(WaterSource source)
        {
            if (!water.TryUseWater(litresPerClick, source)) return;
            water.ReportWater(litresPerClick, 0f, source);
        }
    }
}
