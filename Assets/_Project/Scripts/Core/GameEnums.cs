namespace GardenWise
{
    // states of the global game loop (State pattern in the GameManager)
    public enum GameState
    {
        Menu,
        LevelIntro,
        Playing,
        Results
    }

    public enum WeatherType
    {
        Sunny,
        Rainy,
        HeatWave,
        Frost
    }

    public enum WaterSource
    {
        RainBarrel,
        Tap,
        Drip
    }

    public enum GrowthStage
    {
        Seed,
        Sprout,
        Young,
        Mature,
        Ready,
        Dead
    }

    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }
}
