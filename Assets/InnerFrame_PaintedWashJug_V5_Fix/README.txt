Painted wash jug experiment for InnerFrame (Unity 6000 / URP)

1. Delete the earlier InnerFrame_PaintedWashJug_V5 folder you dragged into
   Unity's Assets. That copy searched for the wrong jug name.
2. In Finder, unzip this package. Drag the SerenaStyleExperiment folder inside Assets
   into your Unity project's Assets folder. Merge it with the existing folder if prompted.
   Do not drag the outer Assets folder into Unity.
3. Open Assets/Scenes/Bedroom_StyleExperiment.unity, your experimental copy.
4. Wait for Unity to finish importing. Use the top menu:
   InnerFrame > Serena > Apply Painted Wash Jug To Open Scene
5. Save the scene (Command-S). Frame the jug with F in the Scene view, rotate
   around it, then test picking it up in Play mode.

This changes only the WaterJug instance in the currently open scene. It does not
alter Rowling's WaterJug.prefab. The new body mesh, texture and material are
saved under Assets/SerenaStyleExperiment/JugExperiment.

If the jug looks wrong, Command-Z before saving, or reopen the scene without
saving. Re-running the menu command recreates the material and mesh.
