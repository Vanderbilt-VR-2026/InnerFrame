# InnerFrame

InnerFrame is a multi-user VR experience that lets people step inside a famous painting, explore its world together, and interact with the artist who created it.

Our first experience recreates Vincent van Gogh's *The Bedroom* as an explorable 3D room that keeps the painting's distinctive visual style.

## The experience

1. A group of people meets in a shared virtual gallery.
2. They find *The Bedroom*, along with some context about the painting.
3. They step through the canvas into the room itself.
4. Inside, they explore together, interact with objects, and talk with a 3D Van Gogh about the painting.

## Getting started

You only need to do these steps once per computer.

1. **Install the tools**
   - [Unity Hub](https://unity.com/download), then Unity **6000.3.23f1**. Please use this exact version. When installing it, also tick **Android Build Support**, which you need for building to the Quest.
   - Git and Git LFS:
     - Mac: `brew install git git-lfs`
     - Windows: install [Git for Windows](https://git-scm.com/download/win). It includes Git LFS; keep that box ticked.

2. **Download the project.** Put it in a folder that isn't synced by iCloud or OneDrive. On Windows, run these commands in Git Bash.

```bash
   git clone https://github.com/Vanderbilt-VR-2026/InnerFrame.git
   cd InnerFrame
   bash scripts/setup.sh
```

   `setup.sh` turns on Git LFS for this project and downloads the big files, such as models and textures. From then on, `git pull` downloads new ones automatically.

3. **Open it in Unity.** In Unity Hub, click **Add → Add project from disk** and choose the `InnerFrame` folder. The first time you open it, Unity takes a few minutes to import everything.

**Target device:** Meta Quest 3

For day-to-day work (branches, pushing, and what the automatic check does), see [CONTRIBUTING.md](CONTRIBUTING.md).

## Team and roles

Roles are flexible, and design and QA are everyone's job.

| Member | Major | Relevant skills | Responsibilities |
|---|---|---|---|
| Sonya Vu | CS | Programming, HCI | AI dialogue, programming interaction |
| Charlyne Dong | CS | Product ideation and development, agents/AI, prototyping | Project management, concept, player experience |
| Rowling Lin | Counseling | Graphic design, UX/UI design, Figma, interaction design | Storyboarding, prototyping, interaction design, visual design |
| Serena Deng | CS | Product ideation and development, programming, concept development | Prototyping, player experience, programming interaction |
| Cici Luo | CS | Software engineering, networking, backend systems, Git, system integration | Multiplayer, player synchronization, shared interactions, Unity systems integration |

### Who did what in Sprint 1

| Member | Sprint 1 focus |
|---|---|
| Cici Luo | **Gallery and entry experience**: the VR gallery and the walk up to *The Bedroom*. Also the engineering foundation: Unity/XR setup (OpenXR + XR Interaction Toolkit for Quest 3), Git LFS and repo structure, asset checks that run locally and in CI, and review/merge onto `main`. |
| Rowling Lin | **Playable Bedroom**: graybox layout at the right scale, controller movement, grabbable objects, hinged doors, collision surfaces. |
| Sonya Vu | **Gallery ↔ Bedroom transition** (`SceneChangeButton`, `SceneFader`): press the pedestal button to fade out, load the other scene, and fade back in. Also painting-to-world prototyping. |
| Serena Deng | **Painterly visual direction**: stylized furniture and props in Blender with brushstroke textures, brought into Unity in `Bedroom_StyleExperiment` on top of Rowling's layout. Built the Sprint 1 APK. |
| Charlyne Dong | **Painting-to-world prototyping and project management**: World Labs/Marble and Meshy experiments, sprint specs, backlog, and the demo video. |

## Course

Built for *Projects in Virtual Reality Design* (CS 4249/5249 / CSET-CMA 3257) at Vanderbilt University, Fall 2026.

## Sprint 1 — Meta Quest Build

**Demo videos:** [InnerFrame Sprint 1 demo (Google Drive)](https://drive.google.com/drive/folders/1R5MCu-oDcUeqRx6421TgvTctF0386qeQ?usp=sharing)

[Download InnerFrame-Sprint1.apk](https://drive.google.com/file/d/17uiKnb5k6u_mXPp79ScKJSJxzH3hdjVu/view?usp=sharing)

### What's in this build
- Start in the **Gallery** and move around.
- Press the button on the pedestal by *The Bedroom* to fade into the Bedroom.
- The Bedroom in this build is the painterly version (`Bedroom_StyleExperiment`): Serena's stylized furniture on Rowling's graybox layout. Move around, pick up the tumbler and brush, open the hinged doors.
- Press the pedestal button in the Bedroom to fade back to the Gallery.
- Rowling's original graybox is still in `Assets/Scenes/Bedroom.unity` but isn't in the build.

### What we explored alongside it
We tested three ways to turn the painting into a 3D world:
- **World Labs/Marble** keeps the painting's overall look, but the room comes out as one piece with no separate objects to grab.
- **Meshy** makes separate objects that keep the painterly style (the bed kept its yellow wood and red blanket), but they are too heavy for Quest without cleanup (one bed was about 790k faces).
- A hand-built **Blender/Unity** room is easy to make interactive but looks much less like the painting.

Sprint 2 plan: combine them. Marble for the room, Meshy for the few objects you pick up (chair, pitcher).

### Install
1. Download the APK to your computer.
2. Connect your Meta Quest to your computer with a USB cable.
3. Enable Developer Mode and accept the headset’s USB debugging prompt.
4. Install the APK using Meta Quest Developer Hub.
5. Launch InnerFrame on the headset.

Built on October 1, 2026.
