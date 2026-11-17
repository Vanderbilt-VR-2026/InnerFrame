# Painted Bedroom: daylight from Blender

How the Bedroom is lit, and how to re-bake it after the room changes. Unity shows light that Cycles
computed; Unity's own lights are not tuned by hand any more.

## The idea

- **Fixed objects** (walls, floor, ceiling, bed, washstand, doors, window, pictures, towel, peg rail):
  one lightmap each, baked in Blender with Cycles from the same `.blend` the room is exported from.
  The lightmap holds light only (not colour), so the Unity shader shows `albedo x light`, the thing
  Cycles rendered. The sun's share of that light is in the lightmap's alpha, so a realtime shadow from a
  picked-up object darkens just the sunlit floor.
- **Objects you can pick up** (chairs, pillows, pitcher, bottles...): Unity light probes, but their values
  come from Cycles too (`bake_probes.py` renders a tiny panorama at every probe and
  `PaintedBedroomProbeImport` turns each into Unity's spherical harmonics). The sun is Unity's realtime
  directional light with the Cycles sun's direction, colour and strength, so those objects cast shadows.
- **The light rig** (sun, window sky light, no fill, no material glow) lives in one place:
  `RIG` at the top of `Art/Source/PaintedBedroom/blender/bake_lightmaps.py`. The decisions behind it
  are in `notes/painterly-research/FINDINGS.md` (10/29 - 11/11).

## Re-bake (after any change to the room's geometry, textures or the rig)

From `Art/Source/PaintedBedroom/blender/`, with Blender 5.2:

```
blender -b painted_bedroom.blend -P bake_lightmaps.py -- --out /tmp/lm --uv-blend /tmp/lm/uv.blend --verify /tmp/lm/verify.png
blender -b painted_bedroom.blend -P bake_probes.py -- --out /tmp/lm/probes.json
blender -b /tmp/lm/uv.blend -P room_io.py -- export --lightmaps /tmp/lm
cp /tmp/lm/probes.json ../../../../Assets/InnerFrameBedroomWeb/Data/probes.json
```

Then in Unity: **Tools > InnerFrame > Painted Bedroom > Import and Build (no Blender)**, then
**Tools > InnerFrame > Painted Bedroom > Import Cycles Probes** (it runs Unity's own bake once to create
the probe set, then writes the Cycles values), then **InnerFrame > Multiplayer > Make Painted Bedroom
Objects Shared** and **Tools > InnerFrame > Apply XR Rig Settings (2 m ray, 0.8 scale, teleport floor)**
(Import and Build drops those), and save the scene.
About 35 min for the bake, 10 for the probes, 10 for Unity, on an M-series Mac (CPU: Metal crashed).

`--verify` renders the painter's view twice, Cycles against albedo x lightmap; the two have been within
10-13/255 on average (the lightmap carries no specular and is 8-bit). The lightmap PNGs import
uncompressed (block compression bands the smooth ceiling) with mipmaps: 19 textures, 14.5 MB of RGBA32,
19.4 MB with the mip chain, on the headset.

## Files

| What | Where |
|---|---|
| Bake scripts, the rig | `Art/Source/PaintedBedroom/blender/bake_lightmaps.py`, `bake_probes.py` |
| Export with lightmap UVs | `room_io.py export --lightmaps DIR` (writes `uvs2` and `lm_<object>.png`) |
| Lightmaps, probes | `Assets/InnerFrameBedroomWeb/Data/Textures/lm_*.png`, `Data/probes.json` |
| Shader | `Assets/InnerFrameBedroomWeb/Shaders/PaintedLightmapped.shader` |
| Importer (per-object lightmap materials, uv2) | `Editor/PaintedBedroomImporter.cs` |
| Probe import | `Editor/PaintedBedroomProbeImport.cs` |
| Sun, probe grid, look volume | `Editor/PaintedBedroomBuilder.cs` (`SetUpLighting`) |
| Review renders, before/after sheets | `notes/painterly-research/unity_light/`, `blender_light/` |

## What it looks like (Unity, 11/11, fixed views)

![Unity overview](images/lighting-unity-overview.png)

A chair moved into the floor's sun patch, shadows on and off:

![Shadow test](images/lighting-shadow-test.png)
