# Water and weather (owner 4: Oscar)

Scripts: `Assets/_Project/Scripts/Water/`. Prefabs: `Assets/_Project/Prefabs/Water/`.

## Test scene

Menu **GardenWise > Build Test_Water Scene** builds `Scenes/Tests/Test_Water.unity`, the test level `Data/Levels/Test_Water_Level.asset` (8 days: sun, sun, rain, sun, heat wave, heat wave, rain, sun) and the prefabs `WaterAndWeather`, `RainBarrel`, `Rain` and `WeatherBoard`.

In Play mode: keys 1-3 select a tile, B waters from the barrel, T from the tap, M adds mulch, Space ends the day (or use the buttons).

## Rules

| Rule | Value |
| --- | --- |
| Moisture change per night | sun -15, heat wave -30, rain +40, frost -5 |
| Mulch | the soil dries half as fast |
| Rain | never fills the soil above the plant's ideal max (75 with no plant) |
| Watering can | 2 L per pour = +20 moisture |
| Heat wave without mulch | half of each pour evaporates (counted as wasted) |
| Too wet | water above the plant's ideal max is counted as wasted |
| Rain barrel | 100 L max, +20 L per rainy day, free |
| Tap | unlimited, counts double in the water efficiency |

## How the other systems use it

- **Garden / player (owner 3):** one pour of the watering can = `water.WaterTile(tile, WaterSource.RainBarrel)` (or `WaterSource.Tap`). It takes the water, waters the tile and sends `GameEvents.WaterUsed` with the useful and wasted litres. It returns `false` if the barrel is empty or the tap is not allowed.
- **GameManager (owner 1):** nothing changes. `EndDay()` already calls `weather.ApplyNight(tile)`, `water.ApplyRain(weather.Today)` and `weather.NextDay(day)`. In the Main scene, drag the `WaterAndWeather` prefab and assign it to the GameManager's `weather` and `water` fields.
- **UI (owner 6):** `HudDisplay` keeps reading `water.BarrelLitres`. New read-only values: `BarrelCapacity`, `BarrelRatio`, `TapLitresUsed`, `RainLitresCollected`.
- **Plants (owner 2):** the rain fix reads `plant.Data.idealMoistureMax`, so a rainy night no longer makes a plant lose health.
- **Gaston (owner 5):** `WeatherSystem.GetLabel(weather.Tomorrow)` gives a short text for the context.
