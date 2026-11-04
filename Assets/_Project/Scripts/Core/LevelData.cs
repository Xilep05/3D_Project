using UnityEngine;

namespace GardenWise
{
    // one asset per level (Create > GardenWise > Level Data), saved in Data/Levels
    [CreateAssetMenu(fileName = "NewLevel", menuName = "GardenWise/Level Data")]
    public class LevelData : ScriptableObject
    {
        public string levelName = "First Sprouts";
        public Season season = Season.Spring;
        public CropData[] allowedCrops;
        public int numberOfDays = 8;

        [Header("Water")]
        public float barrelLitresAtStart = 60f;
        public bool tapAllowed = true;

        [Header("Weather, one entry per day")]
        public WeatherType[] weatherPerDay;

        [Header("Stars (in %)")]
        public float harvestFor2Stars = 60f;
        public float waterFor2Stars = 50f;
        public float harvestFor3Stars = 80f;
        public float waterFor3Stars = 75f;

        [Header("Gaston")]
        [TextArea] public string introText = "Welcome! Let's grow radishes and lettuce this spring.";
    }
}
