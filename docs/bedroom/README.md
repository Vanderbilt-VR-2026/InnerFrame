# Van Gogh Bedroom graybox (Quest 3 prototype)

## Open and edit

- Branch: `rowling-lin`, based on shared `main` at `993d943aff23f242ead821a6ccdad3df23abe7cf`.
- Open the **InnerFrame project folder** in Unity Hub with Unity **6000.3.23f1**. Do not open the `.unity` file as a separate project.
- Let Package Manager restore dependencies. Open **`Assets/Scenes/Bedroom.unity`**, or use **Tools > Van Gogh Bedroom > Open Bedroom**.
- Geometry, gray materials, editable furniture prefabs and interaction scripts are in `Assets/VanGoghBedroom/`.
- Required XR Origin/controller assets are under `Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/`. Their original `.meta` GUIDs are preserved.
- XR Interaction Toolkit **3.3.2** and OpenXR **1.16.1** are required. Their transitive packages are pinned by `Packages/packages-lock.json`.
- Keep `.meta` files when moving or sharing assets. Do not copy `Library`, `Temp`, `Logs`, `obj`, or old APKs into Git.
- The existing SampleScene and all other files from main are retained. This branch does not include unmerged Gallery/XR work from teammate feature branches.

## Controls configured for Quest 3

- Left stick: move relative to head direction (1.25 m/s).
- Right stick: 30-degree snap turns.
- Either controller: hold Grip to grab, release to place or gently throw.
- Eight grab objects: **two chairs, two pillows, water jug, washbasin, tumbler, brush**.
- Both doors: select the handle with Grip and pull; their hinge rotation is limited to 0–100 degrees. Door leaves are not free grab objects.
- Walls, floor, bed, washstand and safety boundaries have fixed colliders. Head penetration into structure fades the view to black; returning restores visibility.
- Physics layers 8/9/10 are BedroomStructure/BedroomLoose/BedroomPlayer. Loose objects do not collide with the player capsule, so holding furniture cannot lift the player. Movable chairs are therefore not fixed player obstacles.

## Desktop preview

Without an active XR display, Play mode uses the scene's comparison camera. Hold right mouse and use WASD to move, Q/E vertically, Shift for faster movement, and R to reset the camera. This preview does not simulate Quest controller grabbing or headset tracking.

## Build and test on Quest 3

1. Install Android Build Support, SDK/NDK Tools and OpenJDK for this Unity version in Unity Hub.
2. Enable Developer Mode on the Quest, connect a USB data cable and accept USB debugging in the headset.
3. Open File > Build Profiles, select Android and switch platform.
4. In the scene list for your test build, enable `Assets/Scenes/Bedroom.unity` as the first scene. Retain other team scenes; disable them for this test if needed rather than deleting them.
5. Under Project Settings > XR Plug-in Management > Android, verify OpenXR and Initialize XR on Startup. Under OpenXR, verify Meta Quest Support and Oculus Touch Controller Profile. Resolve Project Validation errors before building.
6. Verify ARM64, IL2CPP, Vulkan, Linear color space and Android minimum API 32. The project product name and application identifiers are preserved from InnerFrame; coordinate the final application ID with the team.
7. Choose Quest 3 as Run Device and Build And Run; open the installed application from Unknown Sources if needed.

### Requested hardware tests

- Check head and both controller tracking, floor level and viewing height.
- Move and turn; check collision against walls, floor, bed and washstand.
- Use each hand to grab, reposition, place and gently throw all eight objects. Check for unwanted bouncing, objects falling through floors or sticking in walls.
- Open and close both doors, release the handle and test the angle limits; the doors must remain on their hinges.
- Check doorway landing platforms, room boundaries and head blackout/recovery while respecting the headset's system boundary.
- Check tracking after removing/replacing the headset and after brief sleep. Play for several minutes and report stuttering or discomfort.
- Report the object, action and observed issue; screenshots or short videos help reproduce failures.

## Verification and limits

Unity opened this scene successfully after transfer. The editor reference check found **597 GameObjects, 8 grab objects, 2 hinged doors, 8 XR interactors, and 0 reference issues**. The Unity Console showed **0 errors and 0 warnings** after opening and compiling the transferred scene.

Use **Tools > Van Gogh Bedroom > Validate Bedroom References** to repeat the reference check. Its report is written to `Library/BedroomChecks/BedroomValidation.txt` (ignored by Git).

No physical Quest 3 is available here. Headset/controller behavior, comfort, frame rate and Android execution of the integrated InnerFrame project still require real-device testing. Previous standalone Bedroom APKs are not evidence that this integrated InnerFrame build has been tested.
