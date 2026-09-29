# Asset provenance

## Bedroom painting-style prototype

Contributor: Serena Deng. Feature branch: `serena/bedroom-style-prototype`.

### Creation workflow

The bedroom style assets were developed through a ChatGPT-assisted, reference-guided procedural modeling and texturing workflow. ChatGPT helped author scripts that build geometry and generate painted PNG textures. Serena ran and previewed the assets in Blender, then exported/imported the resulting assets into Unity and positioned them in the bedroom style scene.

The reviewed Blender scripts include `InnerFrame_Window_Blender.py`, `InnerFrame_Window_Blender_V2.py`, and `InnerFrame_Bed_Ceramics_Blender.py`. They build objects from primitives/custom meshes and generate paint textures using seeded randomness, color variation, and brush-stroke marks. These scripted textures should be described as procedurally generated, rather than assumed to be AI image-generation outputs.

| Asset group | Recorded origin / use | Attribution status |
| --- | --- | --- |
| Bedroom style models and scripted paint textures in `Assets/InnerFrame_Unity_Assets/` | Reference-guided procedural asset workflow; Blender generation/preview followed by Unity import and placement. The folder README describes the assets as procedural interpretations. | Workflow recorded; a complete per-file inventory and any license declaration remain to be verified. |
| Wall-picture artwork textures, where present | Earlier development notes record crops from the reference painting. | Exact reference image URL, provider, and reproduction license remain to be confirmed before treating these as cleared for public redistribution. |
| Original bedroom graybox prefabs and interactions in `Assets/VanGoghBedroom/` | Supplied by Rowling's graybox work; retained as the base for the style experiment. | Separate from Serena's style asset creation workflow. |

### Scope and remaining checks

This entry records the supported development workflow; it does not assign a blanket license to all project assets. The available documentation does not establish the origin of every texture in every experiment folder.

Before finalizing attribution:
- Record the exact source and applicable license of the reference image used for artwork crops.
- Verify the final retained model/texture inventory against the generation scripts and any other inputs.
- Record any externally sourced or image-generated assets separately if found.
- Update `Assets/InnerFrame_Unity_Assets/README.txt`, whose model/texture counts no longer match the expanded folder.

Preserve the original graybox components, colliders, and interactions when carrying over visual replacements.
