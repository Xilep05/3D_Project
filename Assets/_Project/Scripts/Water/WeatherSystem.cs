using UnityEngine;

namespace GardenWise
{
    // owner 4 - weather of each day and how much the soil dries at night
    public class WeatherSystem : MonoBehaviour
    {
        [Header("Moisture change per night")]
        [SerializeField] private float sunnyChange = -15f;
        [SerializeField] private float heatWaveChange = -30f;
        [SerializeField] private float rainyChange = 40f;
        [SerializeField] private float frostChange = -5f;

        [Header("Rain")]
        [Tooltip("The rain never fills the soil above this value (the extra water drains away)")]
        [SerializeField] private float soilRainCap = 75f;

        private WeatherType[] weatherPerDay = new WeatherType[0];

        public WeatherType Today { get; private set; }
        public WeatherType Tomorrow { get; private set; }

        public int NumberOfDays
        {
            get { return weatherPerDay.Length; }
        }

        public void SetupLevel(LevelData level)
        {
            if (level.weatherPerDay != null) weatherPerDay = level.weatherPerDay;
            NextDay(1);
        }

        public void NextDay(int day)
        {
            Today = GetWeather(day);
            Tomorrow = GetWeather(day + 1);
            GameEvents.RaiseWeatherChanged(Today);
        }

        public void ApplyNight(SoilTile tile)
        {
            float change = GetMoistureChange(Today);

            // mulch keeps half of the water in the soil
            if (change < 0f && tile.HasMulch) change *= 0.5f;

            if (change > 0f) change = GetRainChange(tile, change);

            tile.ChangeMoisture(change);
        }

        // weather of any day of the level (sunny after the last day)
        public WeatherType GetWeather(int day)
        {
            int index = day - 1;
            if (index < 0 || index >= weatherPerDay.Length) return WeatherType.Sunny;
            return weatherPerDay[index];
        }

        public static string GetLabel(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rainy: return "Rain";
                case WeatherType.HeatWave: return "Heat wave";
                case WeatherType.Frost: return "Frost";
                default: return "Sun";
            }
        }

        // a rainy night used to push the soil above the plant's ideal range, so the plant lost health:
        // now the rain stops at the plant's ideal maximum (or at the soil cap when nothing is planted)
        private float GetRainChange(SoilTile tile, float rain)
        {
            float cap = soilRainCap;
            Plant plant = tile.CurrentPlant;
            if (plant != null && plant.Data != null) cap = Mathf.Min(cap, plant.Data.idealMoistureMax);

            float room = Mathf.Max(0f, cap - tile.Moisture);
            return Mathf.Min(rain, room);
        }

        private float GetMoistureChange(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rainy: return rainyChange;
                case WeatherType.HeatWave: return heatWaveChange;
                case WeatherType.Frost: return frostChange;
                default: return sunnyChange;
            }
        }
    }
}
