# Gallery textures from ChatGPT

How to make the Gallery's surface textures with ChatGPT's image generator and bring them into the room. The room
itself (7 × 9 × 4 m, see [README.md](README.md)) follows the team's concept sheet: a small, warm 19th-century gallery,
painted in a loose, Van Gogh-like brushwork.

## 1. Generate

Start a new ChatGPT chat and **attach the concept sheet** (the image with Front View, Reverse View, Top View, Key
Assets), so every texture matches it. Then paste one prompt per image. Ask again ("same, but less contrast / smaller
boards / fewer strokes") until you like it. Download each result (PNG or WebP) and save it under the **file name** in the
table (any image extension), all in one folder outside the repo (for example `~/Desktop/gallery-textures/`).

Every prompt starts with the same house rules:

> **House rules (put at the start of every prompt):** Use the attached concept sheet as the style and colour
> reference. Make a texture image for a 3D game, not a picture of a room: the surface seen perfectly straight on,
> flat, orthographic, no perspective. Even, soft, shadowless light: no highlights, no vignette, no shadows painted in
> (the game lights it). Painted in the concept's loose oil-paint brushwork, visible strokes, warm and slightly muted.
> No text, no logos, no people, no objects other than the material. Fills the whole image edge to edge, no border.

| File | Ask for | Prompt (after the house rules) |
|---|---|---|
| `wall.png` | square, 1024 × 1024 | A **seamless, tileable** painted plaster wall in deep Pompeian red, as the concept's walls: broad, criss-crossing brushstrokes in warmer and darker reds, even overall, no blotch that stands out. About 2 m of wall across the image. |
| `wainscot.png` | landscape, 1536 × 1024 | Wooden wall panelling (wainscot) as in the concept: weathered grey-brown painted boards running **up and down**, about 12 cm wide, with fine gaps between them. **Seamless from left to right.** The bottom edge of the image is the floor, the top edge is where a rail will sit; about 1.5 m of wall across and 1 m up. |
| `floor.png` | square, 1024 × 1024 | A **seamless, tileable** wooden plank floor seen from directly above, as the concept's floor: warm honey-orange oak planks about 15 cm wide running left to right, staggered joints, painterly grain. About 2 m of floor across the image. |
| `ceiling.png` | square, 1024 × 1024 | A **seamless, tileable** ceiling surface as in the concept: warm grey-beige painted wood and plaster, soft brushwork, very even. About 2 m across the image. |
| `trim.png` | square, 1024 × 1024 | A **seamless, tileable** moulding profile for the wainscot's rail and the arch's frame: pale warm stone-coloured painted wood, as the concept's arch, its mouldings as stripes running left to right, with at least one wide plain panel (the script cuts the arch's and the skylight frame's plain wood from it). |
| `cornice.png` | square, 1024 × 1024 | A **seamless, tileable** cornice moulding profile as the concept's ceiling edge: the same pale painted wood, stripes running left to right, a row of small dentil blocks. The bottom of the image is the wall side, the top the ceiling side. |
| `rug.png` | portrait, 1024 × 1536 | The concept's rug, whole, seen from directly above: a Persian-style wool rug in deep red with a navy and cream border and a central medallion, a little worn. The rug fills the image exactly, its border touching all four edges; its long side runs up and down. |
| `frame.png` | landscape, 1536 × 1024 (3 : 1 works best) | A **seamless, tileable from left to right** texture of old gilded wood for an ornate picture frame: warm burnished gold leaf, deep ochre and soft brown in the hollows, gentle highlights painted flat, a little wear where the red bole shows at the edges. Strokes run left to right. No carved pattern, no ornaments, no lines across. The bottom edge of the image is the frame's inner edge, the top its outer edge, slightly darker towards both. |
| `bench_wood.png` | square, 1024 × 1024 | A **seamless, tileable** dark walnut wood for furniture, as the bench's legs and frame: straight fine grain running left to right, oiled, painterly, no knots or joints. About 60 cm across the image. |
| `bench_fabric.png` | square, 1024 × 1024 | A **seamless, tileable** upholstery for the bench's top, as in the concept: dark brown leather (or deep plum velvet), soft sheen painted flat, a faint grain, no buttons, seams or folds. About 50 cm across the image. |
| `door.png` | portrait, 1024 × 1536 | The concept's entrance door, whole and straight on: a pair of closed wooden doors with a **round-arched top**, warm dark wood with raised panels and a little glass high up, painterly. The doors fill the image exactly: the image's edges are the doors' edges (the arch's corners can be dark), no wall or frame round them, no handles (the room adds brass ones). |

Tips:

- "Seamless, tileable" matters for everything except the rug and the door: those surfaces repeat. ChatGPT gets it
  nearly right; the script below blends the edges so the seam disappears.
- Ask for "more even, no focal point" if a tiling image has one strong stroke or spot: it would repeat every 2 m.
- Keep the light flat. Shading painted into a texture looks wrong once the room's lamps light it again.
- The paintings' frames, the pedestals, the metal and the baseboard keep their own colours; they need no texture.

## 2. Bring them in

From the repo root (Blender 5.2):

```
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Art/Source/Gallery/make_textures.py -- ~/Desktop/gallery-textures
```

If ChatGPT gives several textures on one sheet, crop each out first (the door to its outer frame and the arch's apex).
It writes `Assets/Art/Gallery/Textures/GT_<Name>_BaseColor.jpg` for each file it finds (missing files are skipped):
tiling ones cropped square, their edges blended so they repeat without a seam, sized to 1024 (trim 512); the wainscot
blended left to right only; the rug cropped to 2 : 3 and the door to its 1.6 × 2.6 m shape. Then in Unity run
**Tools > InnerFrame > Gallery > Add Decor**: each texture goes on its surface at its real-world size (the table in
README.md). Surfaces without a texture keep their plain colour. Run **Check Decor** and look at the renders in
`Logs/GalleryDecor/`.

## 3. Record them

Add a row to `docs/ASSETS.md` for `Assets/Art/Gallery/Textures`: which images, generated with ChatGPT by whom and when,
from the team's concept sheet, under OpenAI's terms (as the `Assets/StarryNight/Textures` row records them).
