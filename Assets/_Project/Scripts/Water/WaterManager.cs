using UnityEngine;

namespace GardenWise
{
    // owner 4 - rain barrel, tap and the litres counted for the water score
    public class WaterManager : MonoBehaviour
    {
        [Header("Rain barrel")]
        [SerializeField] private float barrelCapacity = 100f;
        [SerializeField] private float rainLitresPerDay = 20f;

        [Header("Watering can")]
        [SerializeField] private float litresPerPour = 2f;
        [Range(0f, 1f)]
        [SerializeField] private float heatWaveLoss = 0.5f;

        [Header("Scene reference")]
        [SerializeField] private WeatherSystem weather;

        public float BarrelLitres { get; private set; } = 50f;
        public float TapLitresUsed { get; private set; }
        public float RainLitresCollected { get; private set; }
        public bool TapAllowed { get; private set; } = true;

        public float BarrelCapacity
        {
            get { return barrelCapacity; }
        }

        public float LitresPerPour
        {
            get { return litresPerPour; }
        }

        // 0 when the barrel is empty, 1 when it is full
        public float BarrelRatio
        {
            get { return barrelCapacity <= 0f ? 0f : BarrelLitres / barrelCapacity; }
        }

        public void SetupLevel(LevelData level)
        {
            BarrelLitres = Mathf.Min(level.barrelLitresAtStart, barrelCapacity);
            TapAllowed = level.tapAllowed;
            TapLitresUsed = 0f;
            RainLitresCollected = 0f;
        }

        // takes water from a source, returns false if not possible
        public bool TryUseWater(float litres, WaterSource source)
        {
            if (source == WaterSource.Tap)
            {
                if (!TapAllowed) return false;
                TapLitresUsed += litres;
                return true;
            }

            if (BarrelLitres < litres) return false;
            BarrelLitres -= litres;
            return true;
        }

        // one pour of the watering can on a tile (called by the player interaction, owner 3)
        public bool WaterTile(SoilTile tile, WaterSource source)
        {
            if (tile == null) return false;
            if (!TryUseWater(litresPerPour, source)) return false;

            // heat wave without mulch: part of the water evaporates before reaching the roots
            float evaporated = 0f;
            if (weather != null && weather.Today == WeatherType.HeatWave && !tile.HasMulch)
            {
                evaporated = litresPerPour * heatWaveLoss;
            }

            float litresInSoil = litresPerPour - evaporated;
            float moistureBefore = tile.Moisture;
            float overflow = tile.AddWater(litresInSoil);
            float tooWet = GetTooWetLitres(tile, moistureBefore, litresInSoil - overflow);

            ReportWater(litresPerPour, evaporated + overflow + tooWet, source);
            return true;
        }

        // to call after SoilTile.AddWater, so the score knows what was useful or wasted
        public void ReportWater(float litres, float wastedLitres, WaterSource source)
        {
            wastedLitres = Mathf.Clamp(wastedLitres, 0f, litres);
            if (litres - wastedLitres > 0f) GameEvents.RaiseWaterUsed(litres - wastedLitres, source, false);
            if (wastedLitres > 0f) GameEvents.RaiseWaterUsed(wastedLitres, source, true);
        }

        public void ApplyRain(WeatherType todayWeather)
        {
            if (todayWeather != WeatherType.Rainy) return;

            float before = BarrelLitres;
            BarrelLitres = Mathf.Min(BarrelLitres + rainLitresPerDay, barrelCapacity);
            RainLitresCollected += BarrelLitres - before;
        }

        // water that pushed the soil above the plant's ideal range is counted as wasted
        private float GetTooWetLitres(SoilTile tile, float moistureBefore, float litresSoaked)
        {
            Plant plant = tile.CurrentPlant;
            if (plant == null || plant.Data == null || litresSoaked <= 0f) return 0f;

            float gained = tile.Moisture - moistureBefore;
            if (gained <= 0f) return 0f;

            float limit = Mathf.Max(moistureBefore, plant.Data.idealMoistureMax);
            float tooWetMoisture = Mathf.Max(0f, tile.Moisture - limit);
            return litresSoaked * tooWetMoisture / gained;
        }
    }
}
