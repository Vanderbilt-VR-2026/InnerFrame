InnerFrame — Serena's painted Bedroom style V3

The four PNGs were generated with your supplied Bedroom image as a style and color reference: wall, floor, golden wood, and a little landscape for the framed picture. V3 applies them to the existing 3D reconstruction. The dark silhouette outline experiment from V2 is included.

INSTALL ON MAC
1. Unzip InnerFrame_Serena_PaintedStyle_V3.zip in Finder.
2. Open its Assets folder. Drag SerenaStyleExperiment into the actual Unity project's Assets folder (the one that contains Scenes, VanGoghBedroom, XR). Choose Merge if Finder asks. Do not drag the enclosing ZIP folder into Unity.
3. Wait for Unity to import scripts and images. If the Console has red errors, take a screenshot before continuing.
4. In the Mac menu bar choose InnerFrame > Apply Serena Bedroom Style V3. It opens and saves Assets/Scenes/Bedroom_StyleExperiment.unity.
5. Take a screenshot from the front opening and inspect the room from inside. To compare, V2 can be reapplied from its own menu. Commit only when satisfied, on your own branch.

The PNGs are imported under Assets/SerenaStyleExperiment/PaintedTextures and the new materials under Assets/SerenaStyleExperiment/PaintedMaterials. The image files are diffuse color textures rather than geometry. Unity's UV mapping may repeat or distort them on small pieces. The source scene and prefabs remain unchanged, though the duplicate Bedroom_StyleExperiment scene is saved. The add-on has been checked against your uploaded scene and URP package, but cannot be executed in your Unity Editor here.

Visual limitations: this is a painterly approximation, not an exact replica of the painting's impossible 2D perspective. The wall and floor tiles may show repetitions. The one framed landscape is generated for the prototype; the original artwork inside the historical painting should be researched separately if exact art fidelity is required. On Quest, inspect image clarity and frame rate before using these textures and outlines in the final build.
