# GardenWise

VR educational gardening app for Meta Quest 3S (ISEP IG.3504 group project).
Design doc and planning: see the team Google Doc and the Miro board "3D project".
Grow fruits and vegetables level by level, manage a limited water supply, and ask Gaston, the AI master gardener.

## Setup (once)

1. Install **Unity 6000.6.0f1** with Unity Hub (same version for everyone), with the **Android Build Support** module (OpenJDK + SDK & NDK) for the Meta Quest 3S.
2. Install Git LFS, then run `git lfs install` once.
3. Clone the repo with GitHub Desktop and open the folder in Unity Hub.
4. For Gaston: copy `Assets/StreamingAssets/ai_config.example.json` to `ai_config.json` and put your key in it. This file is ignored by Git, never commit a key.

## Folders

| Folder | Content |
| --- | --- |
| `Assets/_Project/Scripts/Core` | Shared: GameManager, GameEvents, enums, CropData, LevelData |
| `Assets/_Project/Scripts/<System>` | One folder per system: Plants, Garden, Water, Gardener, UI |
| `Assets/_Project/Prefabs/<System>` | The prefab each owner delivers |
| `Assets/_Project/Scenes/Tests` | One test scene per system: `Test_<System>.unity` |
| `Assets/_Project/Scenes/Main.unity` | Main scene, edited only by the integration lead |
| `Assets/_Project/Data` | CropData and LevelData assets |

## Rules

- Work only in your own folders and your own test scene.
- Branch `feature/<system>`, then a pull request to `main`. `main` must always open and run.
- Pull before you start, commit small and often.
- Any change to a file in `Scripts/Core` is announced in the group chat first.

## Owners

| # | System | Owner |
| --- | --- | --- |
| 1 | Game flow, levels, integration | |
| 2 | Plants and growth | |
| 3 | Garden and player interaction | |
| 4 | Water and weather | |
| 5 | AI gardener Gaston | |
| 6 | UI, score and save | |
