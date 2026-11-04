using UnityEngine;

namespace GardenWise
{
    // one asset per crop (Create > GardenWise > Crop Data), saved in Data/Crops
    [CreateAssetMenu(fileName = "NewCrop", menuName = "GardenWise/Crop Data")]
    public class CropData : ScriptableObject
    {
        public string cropName = "Radish";
        public Season season = Season.Spring;

        [Header("Water")]
        [Range(0, 100)] public float idealMoistureMin = 40f;
        [Range(0, 100)] public float idealMoistureMax = 70f;

        [Header("Growth")]
        public int daysToGrow = 6;
        public int maxHealth = 3;

        [Header("Visuals: one model per stage (Seed, Sprout, Young, Mature, Ready)")]
        public GameObject[] stageModels = new GameObject[5];

        [Header("Education")]
        [TextArea] public string tip = "Radishes like regular watering and grow fast.";
    }
}
