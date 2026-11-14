using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 6 - HUD shown while playing: day, water left in the barrel and current tool
    public class HudDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text waterText;
        [SerializeField] private TMP_Text toolText;

        [Header("Scene reference (owner 4)")]
        [SerializeField] private WaterManager water;

        private void OnEnable()
        {
            GameEvents.DayStarted += HandleDayStarted;
            GameEvents.WaterUsed += HandleWaterUsed;
        }

        private void OnDisable()
        {
            GameEvents.DayStarted -= HandleDayStarted;
            GameEvents.WaterUsed -= HandleWaterUsed;
        }

        private void HandleDayStarted(int day)
        {
            dayText.text = "Day " + day + " / " + GameManager.Instance.CurrentLevel.numberOfDays;

            // the rain can fill the barrel during the night
            ShowWater();
        }

        private void HandleWaterUsed(float litres, WaterSource source, bool wasted)
        {
            ShowWater();
        }

        private void ShowWater()
        {
            if (water == null) return;
            waterText.text = "Barrel: " + Mathf.RoundToInt(water.BarrelLitres) + " L";
        }

        // called when the player takes another tool (owner 3)
        public void ShowTool(string toolName)
        {
            toolText.text = "Tool: " + toolName;
        }
    }
}
