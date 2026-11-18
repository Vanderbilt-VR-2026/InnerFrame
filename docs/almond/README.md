# Almond Blossom studio (Act 4)

Serena's scene (owner to confirm with the team). Concept page: https://claude.ai/artifact/2hC8PdVxAdJ7TZjEDYgm1D

Vincent's studio at the asylum in Saint-Rémy, February 1890, built in 3D after *Window in the Studio* (Van Gogh
Museum). A letter from Theo has just arrived: Jo has had a boy, named Vincent after him. Vincent asks you to help
him paint the almond tree outside the window for the baby's room; you open the window, take a brush and paint the
tree into bloom. At the end he turns to his easel, where *Almond Blossom* appears, and a card tells what became of
it. No fail state, no timer; one player is enough.

## How to open it

1. Pull this branch and open the project in Unity 6000.3.23f1.
2. Open `Assets/AlmondStudio/Scenes/AlmondStudio.unity`.
3. Menu **Tools > InnerFrame > Almond Studio > Build**. It makes the materials, assigns the models and pictures,
   builds Vincent's animation controller, updates `AS_Layout.asset` (migrations) and marks the almond tree. Safe to
   run again; run it after pulling new models or pictures.
4. Press **Play**. The whole studio is built at runtime by `AS_Bootstrap` from `AS_Layout.asset`.

**From the Gallery.** Point at *Almond Blossom* in the Gallery, trigger, then **Enter**: the fade takes you into the
studio. The Gallery's **Tools > InnerFrame > Gallery > Build Layout** sets this up once the studio scene is in the
project (`GalleryLayoutSetup.AlmondStudioEntrance`): Almond Blossom's row in `GalleryLayout.asset` gets the scene
`AlmondStudio`, Gallery.unity gets `Entrance_AlmondBlossom`, and the scene is added, enabled, to the Build Settings'
list (a shared setting, named in the PR). In the studio, the menu (≡ on the left controller, M on a desktop) has
**Back to Gallery**, as in every painting.

**Models.** The Tripo props are in `Art/Props` and the almond tree in `Models`. The tree (10,000 faces) and the
casement (3,000) were reduced with Tripo's retopology; the chest, sill still life and paint box are still Tripo's raw
output (56–65 MB each), to be reduced for Quest. The tree in the scene carries its scattered buds (`AS_TripoTree`).

## The story

| State (`AS_Game`) | What happens |
|---|---|
| Arrive | Vincent holds out Theo's letter, pinched between his thumb and finger (`AS_HeldLetter`), arm held out (Mixamo *Giving*, stopped at `giveHoldAt`). Hint "Take the letter" |
| ReadLetter | The letter flies from his hand to in front of your eyes and grows (`AS_LetterView`); a line of background, then Theo's news. Trigger puts it down |
| Invite | His joy; he turns to the window, points at the tree (Mixamo *Pointing*), turns back and asks you to paint it. Hint "Open the window" |
| TakeBrush | The window opens; take the brush standing in the glass on the sill |
| Paint | Hold the trigger and sweep the beam over the branches: buds near it open (`AS_Blossoms`). Once `blossomFinish` (75%) of the buds you can see through the window are open, the rest open in a wave |
| Done | A petal shower drifts into the studio and settles on the floor; after `endEaselDelay` he turns to his easel, *Almond Blossom* grows onto it, hint "Turn around"; when you look at it, the epilogue card (`AS_EpilogueView`) |

Vincent is Cici's Vincent v2 (read only), on Cici's idle; he looks at you (`AS_VincentLook`, look-at IK) and nods
and sways while he talks (in code, no clip).

**He speaks, and you can talk to him (16 Nov).** His story lines are spoken in his headset voice (Piper through
`VincentVoice`, the voice of his Convai answers in every scene), made in the background in the order he says them; a
line whose voice isn't ready within `lineVoiceWait` (3 s) shows as text only, as before. **Hold B** (B or T in the
Editor) to ask him anything: `SpeechToProxy` on his body, Convai with the shared dialogue box, as in The Starry Night
(set up once per machine: [`docs/convai/README.md`](../convai/README.md)). While you ask and he answers, his story
lines wait and the hint shows "Listening…"; a line you cut off with B is said again afterwards. Each question carries
where the story is (`AS_Game.StoryNow`) and, when it names something in the room, that thing's notes
([`docs/convai/almond-studio-objects.md`](../convai/almond-studio-objects.md)). The music ducks under him.

## Controls

| Headset | Editor (no headset) | What it does |
|---|---|---|
| Point at the letter + trigger (or grip), or trigger within 1.8 m | **L** | Takes Theo's letter |
| Trigger | **Space** | Puts the letter down |
| Point at a casement + trigger, or grip on it (near or by its ray) | **O** | Opens the window |
| Point at the brush + trigger, or trigger with the hand on it; or grip on it | **G** | Takes the brush (trigger: it stays in that hand; grip: until you let go) |
| Hold the trigger, sweeping the beam over the branches | Hold **Space**, aiming the view's centre | Opens the buds near the beam |
| Hold **B** (right controller), speak, let go | Hold **B** or **T** (microphone) | Ask Vincent (Convai) |
| | **N** | Finishes the painting (testing) |
| | **R** | Starts over |
| | W A S D, mouse | Walk and look (DesktopTestRig) |

Both buttons work on a Quest: the hints say "trigger", and XRI's grip (Select) works as before. The trigger acts on what
the hand points at (`AS_Game.PointAndPress`, from the Starter rig's `Left Controller` / `Right Controller` forward).
The casements' colliders are solid, not triggers: the Starter rig's Near-Far Interactor ignores trigger colliders, so
until 15 Nov the window could not be opened with a controller at all. They switch off once the window opens.
| | **P**, **1 2 3**, **C**, **[ ]** | Comparison views over the reference pictures (below) |

## How it works

| File | What it does |
|---|---|
| `Scripts/AS_Bootstrap.cs` | Builds the room, window, furniture, Vincent, the sky, the brush, the letter, the end painting and the epilogue at runtime; Editor keys |
| `Scripts/AS_Game.cs` | The story's states, Vincent's poses and turns, lines and hints, the ending |
| `Scripts/AS_Layout.cs` + `AS_Layout.asset` | Every number and line: room, window, furniture, Vincent, letter, blossoms, sky, ending |
| `Scripts/AS_LetterView.cs`, `AS_HeldLetter.cs` | The letter in his hand and the letter you read |
| `Scripts/AS_VincentLook.cs`, `AS_FeetOnFloor.cs` | His look-at, talking motion, feet on the floor |
| `Scripts/AS_TripoTree.cs`, `AS_Blossoms.cs` + `Shaders/AS_Blossom.shader` | The buds scattered on the Tripo tree, one mesh of painted blossoms opening under the beam, the petal shower |
| `Scripts/AS_EpilogueView.cs` | The closing card: the painting and its history |
| `Scripts/AS_Window.cs`, `AS_Input.cs`, `AS_Guides.cs` | The casements; trigger and haptics (headset) or keys (Editor); the brush's beam |
| `Shaders/AS_Painted.shader`, `AS_Sky.shader` | Flat painted shading for the room; the sky (a seamless painted picture, `Textures/Sky/AS_Sky`, repeated round a sphere) |
| `Editor/AS_Setup.cs`, `Editor/AS_TripoTreeTools.cs` | The Build menu; finding the tree and scattering its buds |
| `Scripts/AS_Tree.cs`, `AS_Branches.cs`, `Data/AS_Branches.json` | The earlier generated tree and branch paths (off: `showGeneratedTree`) |

**The almond tree.** Build marks the Tripo tree outside the window with `AS_TripoTree` and scatters buds over its
outer crown, if it has none. To redo them: select the tree, change `count`, `minSpacing`, `minHeight`, `minReach`
or `seed`, and run **Tools > InnerFrame > Almond Studio > Scatter Blossoms On Selected Tree**; save the scene.

**Tuning** (all on `AS_Layout`, live in Play): `giveHoldAt`, `pointHoldAt`, `pointTurn`, `pointSeconds` (Vincent);
`letterGripInset/Roll/Tip` (the letter in his hand); `blossomCluster`, `blossomSize`, `blossomBeamDegrees`,
`blossomFinish`, `blossomShower`, `blossomStreamPerSecond`, `blossomFloorPetals`, `blossomMaxDrifting` (the
painting and the petals); `skyTiles`, `skyTint`; `endEaselDelay`, `endPainting*`, `epilogueText` (the ending).

**Almond Blossom** is `Textures/Reference/AS_AlmondBlossom.jpg`; without it Build uses the Gallery's copy
(`Assets/Art/Gallery/Paintings/Almond_Blossom_Van_Gogh.jpg`).

**Comparison views (Editor).** In Play: **P** switches to a fixed camera; **1**, **2**, **3** choose the view (his
painting, the S02 arrival concept, the S03 turned-round concept); **C** lays that view's picture over it, half
see-through (**[** and **]** change how much). Pictures in `Textures/Reference/`.

## Facts and ours

- **From the record:** *Window in the Studio* (1889); Theo's letter of 31 January 1890 announcing the birth of
  Vincent Willem; *Almond Blossom* (February 1890), painted for the newborn nephew, hung above the piano in Theo
  and Jo's home, now in the Van Gogh Museum, which the nephew founded (Van Gogh Museum).
- **Ours:** Vincent's spoken lines; the letter's wording (a paraphrase of Theo's January 1890 letters, to check at
  vangoghletters.org); painting the tree into bloom; the room beyond the painting's one wall.

## What is verified

- [V] Plays through in the Unity Editor on Serena's Mac (12 Nov 2026): letter, invitation, window, brush, the
  blossoms opening, the petal shower and the epilogue card, seen in her screenshots. From her working copy, not
  a clean worktree; with the Tripo models present locally.
- [V] `bash scripts/ci-local.sh`: PASS on this branch (14 Nov 2026).
- [V] Every GUID the Almond Studio scene, prefab, materials and controller use resolves to a committed `.meta` or
  to Unity/URP (checked 14 Nov on `340447a`); the tree is no longer a missing prefab.
- [I] Not yet opened from a clean worktree.
- [I] Never built for or tested on a Quest. The controller fixes of 15 Nov (trigger on the letter, window and brush;
  solid casement colliders) were written without a compiler or a headset.

## Hardware tests

1. Arrive: Vincent holds the letter out at a reachable height; point and select takes it; it reads well at 45 cm.
2. The invitation: he turns to the window and points at the tree, then turns back.
3. Open the window, take the brush, sweep the branches: buds open under the beam; the wave comes.
4. The petal shower drifts into the room and settles; frame rate during it (OVR Metrics, target 72+).
5. "Turn around": the painting on the easel, then the card, legible at 75 cm.
6. Load hitch when the scene opens; the sky through the window (no seam).

## Next steps

1. Reduce the chest, sill still life and paint box for Quest (Tripo retopology, as for the tree and casement).
2. A clean-worktree check and a Quest build.
3. The Gallery after the act (Transition 5, with Cici): an empty frame that *Almond Blossom* fills when you come back.
4. The optional second player (with Sonya).
