# Garden and player interaction (owner 3: Victor)

Scripts: `Assets/_Project/Scripts/Garden/`. Prefabs: `Assets/_Project/Prefabs/Garden/`.

## Test scene

Menu **GardenWise > Build Test_Garden Scene** builds `Scenes/Tests/Test_Garden.unity`, the test level `Data/Levels/Test_Garden_Level.asset` (8 days: sun, sun, rain, sun, heat wave, sun, rain, sun), two test crops (`Data/Crops/Test_Garden_Radish` and `Test_Garden_Lettuce`, only created if owner 2 has no Radish / Lettuce crop yet) and the prefabs `Player`, `Garden_3x3`, `DayBell` and `Plant_Placeholder`.
It also places the `WaterAndWeather`, `RainBarrel` and `Rain` prefabs of owner 4 when they are in the project.

In Play mode:

| Input | Action |
| --- | --- |
| ZQSD / WASD / arrows | walk |
| Mouse | look around |
| 1 / 2 / 3 / 4 or mouse wheel | seed bag / watering can / mulch / harvest basket |
| 1 again | next seed (radish, lettuce) |
| 2 again | water source: rain barrel or tap |
| Left click | use the tool on the tile in the crosshair, or ring the bell |
| Space | end the day (same as the bell) |
| Esc | free the mouse (click to take it back) |

Demo for 18 Nov (2 min): plant a radish and a lettuce, water them from the barrel, add mulch on one tile, ring the bell a few times (the soil dries, the rain fills the barrel, the plants grow), water from the tap, harvest the golden radish, show that a second click on the empty tile does nothing.

## How it is built

| Script | Role |
| --- | --- |
| `PlayerController` | first-person body: `Move(Vector2)` and `Look(Vector2)`, reads no key |
| `ToolBelt` | the tool in hand, the selected seed (from `LevelData.allowedCrops`) and the water source |
| `Interactor` | `Aim(Ray)` finds the tile or the bell, `UseCurrentTool()` / `UseTool(tool, tile)` does the action |
| `DesktopInput` | the only script that reads the keyboard and the mouse |
| `SoilTile`, `GardenGrid` | one soil square (moisture, mulch, plant, highlight) and the 3x3 grid |
| `DayBell` | rings and calls `GameManager.EndDay()` |
| `GardenTool` | enum of the 4 tools |
| `PlantPlaceholderView`, `GardenTestControls` | test only |

**Input separate from interaction:** for the VR version, `DesktopInput` and `PlayerController` are replaced by the XR Origin (teleport for Léa, who gets motion sick). The XR rig calls the same methods: `interactor.Aim(controllerRay)` and `interactor.UseCurrentTool()`, or `interactor.UseTool(GardenTool.WateringCan, tile)` when a grabbed can is poured on a tile. `ToolBelt`, `Interactor` and `SoilTile` do not change.

**New level, fresh garden:** on `LevelIntro`, `GardenGrid` resets every tile (no plant, no mulch, moisture 50) and `PlayerController` goes back to its start position. `InteractionFeedback` shows the crosshair, the tile aimed at and the last action in the HUD of the Main scene.

**Harvest counted once:** the `Interactor` removes the plant right after `plant.Harvest()`, so the same plant can never send `PlantHarvested` twice. A dead plant is removed with the basket, without score.

## How the other systems use it

- **Game flow (owner 1):** in the Main scene, drag `Player`, `Garden_3x3` and `DayBell`, and assign `Garden_3x3` to the GameManager's `garden` field. The bell calls `GameManager.Instance.EndDay()`. The player only moves and acts while the state is `Playing`; in the menus and on the results screen the mouse is free.
- **Plants (owner 2):** `Garden_3x3` uses `Plant_Placeholder` as the plant prefab of its 9 tiles. When your plant prefab (with `PlantVisual`) is ready, put it in the `plantPrefab` field of the tiles of `Garden_3x3` and the placeholder can be deleted. The seeds come from `LevelData.allowedCrops`.
- **Water (owner 4):** one click with the can = `water.WaterTile(tile, source)`. This branch is built on `feature/water`: **merge PR #1 first**. The tiles use `MoistureTint`.
- **UI (owner 6):** in the Main scene, drag the `HudDisplay` into the `hud` field of the Player's `ToolBelt`: it then calls `hud.ShowTool(name)` every time the tool changes. `ToolBelt.Changed` is also available.
- **Gaston (owner 5):** `interactor.TargetTile` gives the tile the player looks at, if Gaston should talk about it.
