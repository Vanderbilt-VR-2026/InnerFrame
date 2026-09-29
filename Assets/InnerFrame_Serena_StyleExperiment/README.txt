InnerFrame — Serena's Sprint 1 painted-style prototype

This is an add-on for the Unity project you uploaded. It uses Rowling's existing geometry and applies painted colors and procedural brush marks to your duplicate scene. It does not create new geometry or change the original Bedroom scene.

INSTALL ON MAC
1. Quit Unity or wait until it has finished compiling.
2. Double-click InnerFrame_Serena_StyleExperiment.zip to unzip it.
3. Open the unzipped folder, then drag its Assets/SerenaStyleExperiment folder into your Unity project's Assets folder in Finder. Do not replace your whole Assets folder.
4. Return to Unity and wait for scripts to import. If the Console shows red errors, capture them and stop before running the menu item.
5. From the Mac menu bar choose InnerFrame > Apply Serena Bedroom Style.
6. Unity opens Assets/Scenes/Bedroom_StyleExperiment.unity and saves the colored scene. View it from the opening side of the room.
7. Save a screenshot and compare it to your before screenshot and the painting. Commit Assets/Scenes/Bedroom_StyleExperiment.unity and Assets/SerenaStyleExperiment on your own branch.

WHAT THIS TESTS
A palette and lightweight repeated brush texture applied to the existing geometry. Colors approximate the supplied painting: blue walls and doors, brown floor, yellow bed and chairs, brown washstand, red blanket. The texture is intentionally simple; assess it from a VR view as well as the Scene view. No external model generation is used in this experiment.

IMPLEMENTATION NOTES
The menu tool creates materials and 256-pixel brush textures under Assets/SerenaStyleExperiment/Generated. It applies renderer material overrides in Bedroom_StyleExperiment only and does not edit prefab source files. You can re-run it, but the generated textures are only created if absent. This script has been inspected against your uploaded Unity YAML and packages (URP 17.3); it has not been run in your Unity Editor here.
