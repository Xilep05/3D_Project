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
| `Gaston_Dialogue` | 5 | under `UI_Canvas` (not in the HUD panel, so his models keep loading), top left under the HUD bar; reads the garden, water and weather. Only shows while playing, says the intro of each level |
| `EventSystem` | - | needed by the menu buttons |

## Flow to check in Play mode

Main menu → Play → Level 1 → intro → Start → plant / water / bell for 8 days → results with stars → Continue → level select (level 2 unlocked, best score shown) → level 2 starts on a fresh garden, player back at the start.

## Still missing (other owners)

- **Plants (owner 2):** the tiles still use `Plant_Placeholder`. Put the real plant prefab in the `plantPrefab` field of the tiles in `Garden_3x3`.
- **Levels:** `allowedCrops` is empty in `Level_FirstSprouts` and `Level_SummerDuo`, so the seed bag uses the test radish and lettuce of the Player prefab. Add the real CropData assets to the levels.

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

## Life, effects and URP (feature/visuals)

- **Wind**: leaves, grass and petals sway gently (both foliage shaders, more at the top than at the base).
- **Butterflies** around the flowers (`Butterfly`) and **birds** circling high (`BirdFlock`), part of `Environment.prefab`.
- **Particles** (`InteractionEffects` on the Player): water drops when watering, earth when planting, straw when mulching, golden sparkles when harvesting. The `Interactor` raises `Planted`, `Watered`, `Mulched`, `Harvested` with the tile.
- **Rain barrel** (owner 4's prefab, dressed in the scenes only): wooden staves and metal hoops around the gauge, the water level is seen from above. `RainBarrel` itself is unchanged.
- **URP**: materials made by the builders use Standard or URP Lit depending on the active pipeline; `FoliageURP.shader` is the URP version of the foliage. `Scripts/Environment/Editor/URP` (compiled only when the URP package is installed) sets 4x MSAA, shadows, SSAO and a post-processing profile (`Art/Environment/PostFX_Profile.asset`: bloom, tone mapping, colour, white balance, vignette), adds a global Volume to each scene built and turns post-processing on for the cameras. Menu **GardenWise > Setup URP Look (open scene)** applies it to any open scene.
- Switching to URP: Package Manager > Universal RP > Install, then Window > Rendering > Render Pipeline Converter > Built-in to URP (Rendering Settings + Material Upgrade) > Convert, then GardenWise > Fix Third-Party Materials and rebuild the scenes.

## Gaston: controls

- Gaston sits in the top-left corner: his portrait, and under it the **T** key. **T** opens his speech bubble and the text field (Enter sends, the player stands still while typing), **V** held down to talk (the microphone and the V key show only while the bubble is open, and turn gold while he listens), **Esc** closes the bubble. Gaston uses the default microphone of the computer, or the one named in the Microphone field of `GardenerVoice`.
- When the replies are pre-written tips (no online AI set up, or unreachable), the bubble says so in gold at its top.
- Gaston answers with an online model when `Assets/StreamingAssets/ai_config.json` is filled in (see the README), else with the local model, else with written tips.
