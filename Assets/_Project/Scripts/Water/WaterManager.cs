using UnityEngine;

namespace GardenWise
{
    // owner 4 - rain barrel, tap and the litres counted for the water score
    public class WaterManager : MonoBehaviour
    {
        [SerializeField] private float barrelCapacity = 100f;
        [SerializeField] private float rainLitresPerDay = 20f;

        public float BarrelLitres { get; private set; }
        public float TapLitresUsed { get; private set; }
        public bool TapAllowed { get; private set; } = true;

        public void SetupLevel(LevelData level)
        {
            BarrelLitres = Mathf.Min(level.barrelLitresAtStart, barrelCapacity);
            TapAllowed = level.tapAllowed;
            TapLitresUsed = 0f;
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

        // to call after SoilTile.AddWater, so the score knows what was useful or wasted
        public void ReportWater(float litres, float wastedLitres, WaterSource source)
        {
            if (litres - wastedLitres > 0f) GameEvents.RaiseWaterUsed(litres - wastedLitres, source, false);
            if (wastedLitres > 0f) GameEvents.RaiseWaterUsed(wastedLitres, source, true);
        }

        public void ApplyRain(WeatherType weather)
        {
            if (weather != WeatherType.Rainy) return;
            BarrelLitres = Mathf.Min(BarrelLitres + rainLitresPerDay, barrelCapacity);
        }
    }
}
