using TMPro;
using UnityEngine;

namespace GardenWise
{
    // owner 4 - forecast board: today, tomorrow and every day of the level
    public class WeatherBoard : MonoBehaviour
    {
        [SerializeField] private WeatherSystem weather;
        [SerializeField] private TMP_Text todayText;
        [SerializeField] private TMP_Text forecastText;

        private void OnEnable()
        {
            GameEvents.DayStarted += HandleDayStarted;
            GameEvents.WeatherChanged += HandleWeatherChanged;
        }

        private void OnDisable()
        {
            GameEvents.DayStarted -= HandleDayStarted;
            GameEvents.WeatherChanged -= HandleWeatherChanged;
        }

        private void HandleDayStarted(int day)
        {
            Refresh(day);
        }

        private void HandleWeatherChanged(WeatherType todayWeather)
        {
            int day = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 1;
            Refresh(day);
        }

        private void Refresh(int currentDay)
        {
            if (weather == null) return;

            if (todayText != null)
            {
                todayText.text = "Today: " + WeatherSystem.GetLabel(weather.Today) +
                                 "\nTomorrow: " + WeatherSystem.GetLabel(weather.Tomorrow);
            }

            if (forecastText != null) forecastText.text = BuildForecast(currentDay);
        }

        private string BuildForecast(int currentDay)
        {
            string line = "";
            for (int day = 1; day <= weather.NumberOfDays; day++)
            {
                string entry = "D" + day + " " + WeatherSystem.GetLabel(weather.GetWeather(day));
                if (day == currentDay) entry = "<b><u>" + entry + "</u></b>";
                line += entry + "    ";
            }
            return line;
        }
    }
}
