# InnerFrame

InnerFrame is a VR experience for Meta Quest 3 where people step inside a famous painting, explore it together, and (in a later sprint) talk with the artist.
The first painting is Vincent van Gogh's *The Bedroom* (1889).

## Team and roles

Roles are flexible, and design and QA are everyone's job.

| Member | Major | Relevant skills | Responsibilities |
|---|---|---|---|
| Sonya Vu | CS | Programming, HCI | AI dialogue, programming interaction |
| Charlyne Dong | CS | Product ideation and development, agents/AI, prototyping | Project management, concept, player experience |
| Rowling Lin | Counseling | Graphic design, UX/UI design, Figma, interaction design | Storyboarding, prototyping, interaction design, visual design |
| Serena Deng | CS | Product ideation and development, programming, concept development | Prototyping, player experience, programming interaction |
| Cici Luo | CS | Software engineering, networking, backend systems, Git, system integration | Multiplayer, player synchronization, shared interactions, Unity systems integration |

## Sprint 2 (Oct 1 – 15)

Sprint 2 was an exploration sprint. We compared several ways of turning the painting into a 3D world ([#15](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/15)) and converged on Serena's **Painted Bedroom** as the shared base: every object is a separate, grabbable model generated from code, with brushstroke textures and no paid tools.

**Demo video:** [InnerFrame Sprint 2 demo (Google Drive)](https://drive.google.com/file/d/1cwC70pKSI5lhj8zq6YSISSVEiMI4LnO9/view?usp=sharing), recorded on Quest 3 from this build.

### What's in this branch (`sprint_2`)
- **Gallery** (`Assets/Scenes/Gallery.unity`): *The Bedroom* on its pedestal. The pedestal button loads the Painted Bedroom.
- **Painted Bedroom** (`Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity`): the whole room after the Sprint 2 colour and brushwork pass. The generator source is in `Art/Source/PaintedBedroom/web/`.
- **Physics** (`Assets/InnerFrameBedroomWeb/Resources/PaintedBedroomPhysicsTable.asset`): small objects, pillows, the hat and the chairs can be grabbed and fall with gravity. The bed, washstand, doors, window and pictures are fixed.
- **Player menu** (`Assets/Script/Menu/`): left controller ≡ opens it. **Back** returns to the previous scene; **Talk** is a placeholder. See [docs/menu/README.md](docs/menu/README.md).
- **XR rig**: 2 m interaction rays, 0.8 rig scale, thumbstick teleport. Set by *Tools > InnerFrame > Apply XR Rig Settings*.
- **Build list**: Gallery, then PaintedBedroom.

### Run it
- **Editor:** Unity **6000.3.23f1**. Clone, run `bash scripts/setup.sh` (Git LFS), open the project, open `Assets/Scenes/Gallery.unity` and press Play. Without a headset, open `PaintedBedroom.unity` instead: it has a desktop mode (WASD and the mouse, **M** for the menu).
- **Quest 3:** File > Build Profiles > **Android** (not a Meta Quest profile) > Switch Platform, then Build And Run with the Quest connected over USB in Developer Mode. The build list is already set. The XR settings to check are in [docs/bedroom/README.md](docs/bedroom/README.md#build-and-test-on-quest-3); skip its step 4.
- **Controls:** right thumbstick to teleport, left thumbstick to walk, Grip to grab (up close or with the ray), ≡ for the menu.

### Tested on Quest 3
- [#29](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/29), 10/15, on the previous commit `367052e` with local test changes: immersive launch, 72–73 fps (min 71), grab works with both controllers.
- This build (`d852c60`) was tested on Quest 3 by Sonya on 10/15, the build the demo video was recorded from. Details to follow in #29.

### Known issues
- In #29 the Gallery → Painted Bedroom transition was blocked. This build points the button at the Painted Bedroom and lengthens the rays to 2 m; the written retest is pending in #29.
- In #29 held objects, including the bed, went through the floor. The large objects are now fixed; dropped objects can still sink slightly into the floor.
- Frame rate with post-processing on hasn't been measured.
- Joystick walking caused nausea in testing; teleport is the comfortable way to move.
- Some geometry flickers, and part of the jug handle disappears from some angles.
- After *Import and Build*, run *Tools > InnerFrame > Apply XR Rig Settings* again, or thumbstick teleport stops landing on the floor.
- This build isn't on `main` yet: its last commit came after the 12:00 freeze and is in [PR #103](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/103), awaiting review.

### Next: Sprint 3
We stop exploring new pipelines and improve this one model. First: merge PR #103 into `main` ([#28](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/28)) and finish the Quest check of the full Gallery ↔ Bedroom path ([#29](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/29)). Then per-object refinement in Blender ([#31](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/31)) and the painterly look ([#30](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/30)). Board: [InnerFrame Dashboard](https://github.com/orgs/Vanderbilt-VR-2026/projects/12).

---
Getting started, Sprint 1 and APK install: [previous README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/4a446d02e9332498396e2c648747f50fe0cc165a/README.md) · Day-to-day work: [CONTRIBUTING.md](CONTRIBUTING.md) · *CS 4249 / CSET-CMA 3257: Virtual Reality Design*, Vanderbilt University, Fall 2026.
