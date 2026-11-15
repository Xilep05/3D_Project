using UnityEngine;

namespace GardenWise
{
    // owner 4 - rain particles on rainy days only
    [RequireComponent(typeof(ParticleSystem))]
    public class RainEffect : MonoBehaviour
    {
        private ParticleSystem rain;

        private void Awake()
        {
            rain = GetComponent<ParticleSystem>();
            rain.Stop();
        }

        private void OnEnable()
        {
            GameEvents.WeatherChanged += HandleWeatherChanged;
        }

        private void OnDisable()
        {
            GameEvents.WeatherChanged -= HandleWeatherChanged;
        }

        private void HandleWeatherChanged(WeatherType todayWeather)
        {
            if (todayWeather == WeatherType.Rainy) rain.Play();
            else rain.Stop();
        }
    }
}
