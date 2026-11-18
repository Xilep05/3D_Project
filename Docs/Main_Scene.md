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

## Look of the game (shared by Main and Test_Garden)

`Scripts/Environment/Editor/EnvironmentArt.cs` generates everything, no imported file needed:

- **Textures** (`Art/Environment`): grass, soil, gravel and wooden planks, made with seamless noise. Menu **GardenWise > Regenerate Environment Textures** to make them again after a change.
- **Light**: warm late-afternoon sun with soft shadows, procedural sky, three-colour ambient light and a light haze.
- **Decor** (`Prefabs/Environment/Environment.prefab`): gravel plot and path, wooden fence with a gate, shed, flower border, trees, bushes, rocks and hills on the horizon. Everything is static-batched for the Quest.
- **Garden tiles**: each tile is a small raised bed in a wooden frame; the highlight is a yellow frame.

Rebuild order after a change: **Build Test_Garden Scene** first (it rebuilds the garden, bell and player prefabs), then **Build Main Scene**.

Next step: replace the simple shapes by the free CC0 models of Quaternius (Stylized Nature MegaKit, LowPoly Crops Pack) in `Art/ThirdParty`.

## Free 3D models (Art/ThirdParty, CC0)

- `Quaternius_Nature`: 25 models and 10 textures of the **Stylized Nature MegaKit** (Standard, CC0): trees, pines, bushes, flowers, grass tufts, ferns, mushrooms, rocks, pebbles. The decor uses them automatically when they are present (simple shapes otherwise).
- `Quaternius_Crops`: lettuce, beet (used for the radish), carrot, tomato and pumpkin of the **LowPoly Crops Pack** (CC0), 4 growth stages + crop. The test crops use them in `CropData.stageModels`; owner 2 can use the same models for the real crops.
- `Scripts/Environment/Editor/ThirdPartyImport.cs` sets the import (one material per texture, textures at most 1024 px) and `Art/Shaders/Foliage.shader` draws leaves, grass and petals with cut-out on both sides. Menu **GardenWise > Fix Third-Party Materials** if leaves look like plain squares.
- Only the models used are in the repo, to keep the Git LFS quota low. Credits: Quaternius (quaternius.com), CC0.
