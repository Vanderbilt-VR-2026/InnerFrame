InnerFrame — Serena's painted Bedroom style V2

This update makes brush marks broader and more directional and adds dark silhouette outlines to prominent furniture, picture frames, doors, and window details. It updates only Assets/Scenes/Bedroom_StyleExperiment.unity. The original Bedroom scene and source prefab files remain as they were.

INSTALL ON MAC
1. In Finder, unzip InnerFrame_Serena_PaintedStyle_V2.zip.
2. Open the unzipped Assets folder. Drag SerenaStyleExperiment into the Unity project's Assets folder. Choose Merge when Finder asks. The new BedroomStyleExperimentV2.cs and PaintedOutline.shader should be added alongside the earlier experiment. Do not drag the enclosing ZIP folder into Unity.
3. In Unity, wait for importing. Resolve any Console errors before continuing.
4. Use the Mac menu bar: InnerFrame > Apply Serena Bedroom Style V2.
5. In the Scene view, look into the room from roughly the painting's angle and compare it with the reference. Save a screenshot, then inspect close up to check whether strokes and outlines read well.
6. On your own branch, commit Assets/Scenes/Bedroom_StyleExperiment.unity and Assets/SerenaStyleExperiment. Do not commit Unity's Library folder.

The screenshot shows an earlier enclosing ZIP folder was also imported into Assets. You can leave that folder alone for this experiment; use the new V2 menu command after adding the SerenaStyleExperiment folder directly under Assets.

NOTES
The menu tool overwrites its generated brush textures when run, reuses or creates materials, and reuses outline child objects on repeated runs. It needs the project's URP package. Outlines are an experiment and may need width adjustment for Quest performance and comfort: Assets/SerenaStyleExperiment/Generated/outline.mat has an Outline Width slider. This is a stylized approximation, not a projection of the original painting or a finished VR asset. The code was reviewed against your uploaded project but cannot be executed in your Mac Unity Editor here.
