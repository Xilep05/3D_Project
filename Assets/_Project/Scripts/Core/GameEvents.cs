using System;

namespace GardenWise
{
    // static event hub shared by every system
    // any change here must be announced to the team first
    public static class GameEvents
    {
        public static event Action<GameState> GameStateChanged;
        public static event Action<int> DayStarted;
        public static event Action<WeatherType> WeatherChanged;
        public static event Action<Plant> PlantPlanted;
        public static event Action<Plant, float> PlantHarvested;
        public static event Action<Plant> PlantDied;
        public static event Action<float, WaterSource, bool> WaterUsed;
        public static event Action<string> GardenerAsked;

        public static void RaiseGameStateChanged(GameState state) { GameStateChanged?.Invoke(state); }
        public static void RaiseDayStarted(int day) { DayStarted?.Invoke(day); }
        public static void RaiseWeatherChanged(WeatherType weather) { WeatherChanged?.Invoke(weather); }
        public static void RaisePlantPlanted(Plant plant) { PlantPlanted?.Invoke(plant); }
        public static void RaisePlantHarvested(Plant plant, float quality) { PlantHarvested?.Invoke(plant, quality); }
        public static void RaisePlantDied(Plant plant) { PlantDied?.Invoke(plant); }
        public static void RaiseWaterUsed(float litres, WaterSource source, bool wasted) { WaterUsed?.Invoke(litres, source, wasted); }
        public static void RaiseGardenerAsked(string question) { GardenerAsked?.Invoke(question); }
    }
}
