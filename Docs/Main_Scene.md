# Main scene (integration)

Menu **GardenWise > Build Main Scene** builds `Assets/_Project/Scenes/Main.unity` from the prefabs of every owner, links them and puts Main first in the Build Settings. Run it again whenever a prefab changes: the scene is rebuilt from scratch, so never edit Main by hand.

## What is in Main

| Object | Owner | Linked to |
| --- | --- | --- |
| `GameFlow` (GameManager) | 1 | garden, weather, water. `startTestLevelOnPlay` is off: the game starts on the main menu |
| `WaterAndWeather`, `Rain`, `RainBarrel` | 4 | the barrel shows the WaterManager litres |
| `WeatherBoard` | 4 | inside the HUD panel, so it only shows while playing ("Today" moved under the top bar) |
| `Garden_3x3`, `DayBell`, `Player` | 3 | the ToolBelt shows the tool in the HUD, the Interactor waters through the WaterManager |
| `InteractionFeedback` | 3 | inside the HUD panel: crosshair, tile aimed at, result of the last action |
| `UI_Canvas` (menus, HUD, results, score) | 6 | MenuManager levels = `Level_FirstSprouts`, `Level_SummerDuo`; HUD barrel = WaterManager |
| `EventSystem` | - | needed by the menu buttons |

## Flow to check in Play mode

Main menu → Play → Level 1 → intro → Start → plant / water / bell for 8 days → results with stars → Continue → level select (level 2 unlocked, best score shown) → level 2 starts on a fresh garden, player back at the start.

## Still missing (other owners)

- **Plants (owner 2):** the tiles still use `Plant_Placeholder`. Put the real plant prefab in the `plantPrefab` field of the tiles in `Garden_3x3`.
- **Levels:** `allowedCrops` is empty in `Level_FirstSprouts` and `Level_SummerDuo`, so the seed bag uses the test radish and lettuce of the Player prefab. Add the real CropData assets to the levels.
- **Gaston (owner 5):** no NPC or chat prefab on `main` yet. Add it here when `feature/gardener` is merged.
