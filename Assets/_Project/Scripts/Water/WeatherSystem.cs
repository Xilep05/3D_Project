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

        private WeatherType[] weatherPerDay = new WeatherType[0];

        public WeatherType Today { get; private set; }
        public WeatherType Tomorrow { get; private set; }

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

            tile.ChangeMoisture(change);
        }

        private WeatherType GetWeather(int day)
        {
            int index = day - 1;
            if (index < 0 || index >= weatherPerDay.Length) return WeatherType.Sunny;
            return weatherPerDay[index];
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
