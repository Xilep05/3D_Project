# VR on the Meta Quest 3S

The **Main** scene runs on both platforms: `PlatformSwitcher` uses the desktop Player when no headset runs (editor, Mac/PC build) and the **XR Origin** of the XR Interaction Toolkit on the Quest. The ToolBelt, Interactor, effects, HUD and Gaston are shared.

Quest Link does not exist on macOS: on a Mac the game is tested in the editor (desktop or XR Device Simulator) and **built as an Android app installed on the headset over USB-C**.

## Once per computer

1. Unity Hub > Installs > 6000.6.0f1 > ⚙ > Add modules > **Android Build Support** (with OpenJDK and Android SDK & NDK Tools).
2. Headset holder: turn on **developer mode** (Meta Horizon app on the phone > Devices > Headset settings > Developer mode; it needs a Meta developer account). Plug the headset in with a USB-C data cable and accept **Allow USB debugging** inside the headset.

## Once for the project (then commit Packages, ProjectSettings and Assets/Samples)

1. Window > Package Manager > Unity Registry > **XR Interaction Toolkit** > Install. In its **Samples** tab: import **Starter Assets** and **XR Device Simulator**.
2. Edit > Project Settings > **XR Plug-in Management** > Install, then in the **Android** tab tick **OpenXR**. In **OpenXR** (Android tab): Feature Groups > tick **Meta Quest Support**; Enabled Interaction Profiles > + **Oculus Touch Controller Profile** and + **Meta Quest Touch Plus Controller Profile**.
3. Project Settings > Player > Other Settings > **Active Input Handling = Both** (Unity restarts). The desktop scripts use the old input, the XR Interaction Toolkit the new one.
4. File > Build Profiles > **Android** > Switch Platform (long the first time).
5. Project Settings > XR Plug-in Management > **Project Validation** > **Fix All** (IL2CPP, ARM64, minimum API level, Vulkan...).
6. Player > Other Settings > Package Name: `com.isep.gardenwise`.
7. **GardenWise > Build Main Scene**: adds the XR Origin, the controller pointer, teleport on the ground and the PlatformSwitcher.

## Controls in the headset

| Controller | Action |
| --- | --- |
| Right stick forward, release | teleport (aim the arc at the ground) |
| Right stick left / right | snap turn |
| Left stick | walk |
| Right pointer | aims at a tile (yellow line) or the bell |
| Right trigger | use the tool / ring the bell |
| A | next tool |
| B | tool option: next seed, or barrel / tap |
| Point at a panel + trigger | menu buttons (the screen UI floats in front of you) |

## Build and run

File > Build Profiles > Android: scene list with **Main** first, Run Device = the Quest (plugged in) > **Build And Run**. The app then appears in the headset under Library > Unknown sources.

## Test in the editor without the headset

Select **PlatformSwitcher** in Main and tick **Force VR In Editor**, and in Project Settings > XR Interaction Toolkit tick **Use XR Device Simulator in scenes**. Teleport and looking around work; the trigger / A / B of the garden are read from real controllers only, so test them in the headset.

## Next steps

- Lighter rendering on the Quest: a second URP asset for Android without SSAO and HDR, post-processing off on the XR camera (already off by default).
- Grabbing the watering can and the seed bag (XR Grab Interactable), quick-question menu for Gaston.
