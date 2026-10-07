# InnerFrame

InnerFrame is a multi-user VR experience that lets people step inside a famous painting, explore its world together, and interact with the artist who created it.

Our first experience recreates Vincent van Gogh's *The Bedroom* as an explorable 3D room that keeps the painting's distinctive visual style.

**Sprint 1:** [demo videos](https://drive.google.com/drive/folders/1R5MCu-oDcUeqRx6421TgvTctF0386qeQ?usp=sharing) · [Quest APK](https://drive.google.com/file/d/17uiKnb5k6u_mXPp79ScKJSJxzH3hdjVu/view?usp=sharing) · [what's in the build](#sprint-1-sep-17--oct-1) · [team and roles](#team-and-roles)

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

Built for *CS 4249 / CSET-CMA 3257: Virtual Reality Design* at Vanderbilt University, Fall 2026.

## Sprint 1 (Sep 17 – Oct 1)

| | |
|---|---|
| Demo videos | [InnerFrame Sprint 1 demo (Google Drive)](https://drive.google.com/drive/folders/1R5MCu-oDcUeqRx6421TgvTctF0386qeQ?usp=sharing) |
| Quest build | [InnerFrame-Sprint1.apk](https://drive.google.com/file/d/17uiKnb5k6u_mXPp79ScKJSJxzH3hdjVu/view?usp=sharing) (install steps below) |
| Backlog | [InnerFrame Dashboard](https://github.com/orgs/Vanderbilt-VR-2026/projects/12) (project board, 6 epics) · [Issues](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues?q=is%3Aissue) |
| Headset test | [#12](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/12): the full path on a real Quest 3, built from `main` |

**Sprint goal:** a rough but playable path from the Gallery into the painting, and a decision on how to turn the painting into a 3D world. Visual polish was not the goal.

### What works in the build
1. Launch on Quest 3. You start in the **Gallery**, in immersive VR.
2. Teleport across the gallery floor to *The Bedroom*.
3. Press the button on its pedestal. The view fades out and you land in the **Bedroom**.
4. In the Bedroom: walk around, grab the tumbler and the brush (Grip), and open the hinged doors.
5. Press the pedestal button in the Bedroom to fade back to the Gallery.

The Bedroom in the build is the painterly version (`Bedroom_StyleExperiment`): Serena's stylized furniture on Rowling's layout. Rowling's original graybox, with more grabbable objects, is in `Assets/Scenes/Bedroom.unity` (see [docs/bedroom/README.md](docs/bedroom/README.md)) but isn't in the build.

All of this was checked on a Quest 3 in [#12](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/12) (immersive launch, tracking, teleport, the round trip, grabbing, no visible lag).

### Known issues
- The controller ray reaches too far, so objects can be used from across the room ([#18](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/18)).
- The Bedroom's return button needs a better spot ([#17](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/17)).
- The transition fades to plain black instead of an image ([#16](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/16)).
- Single player only. Multiplayer, voice and the 3D Van Gogh come in later sprints.

### Painting-to-world experiments
We tested three ways to turn the painting into a 3D world ([#15](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/15)):

| Approach | Good at | Problem |
|---|---|---|
| World Labs / Marble | The whole room keeps the painting's look | One baked scene: no separate objects to grab |
| Meshy | Separate objects that keep the painterly style (the bed kept its yellow wood and red blanket) | Very heavy: one bed was about 790k faces, too much for Quest without cleanup |
| Blender + Unity (in the build now) | Clean objects that are easy to make interactive | Looks less like the painting |

### Next: Sprint 2 (Oct 1 – 15)
Combine the three: Marble builds the room, Meshy builds only the objects you interact with (chair and pitcher first), Blender cleans them up when needed, and Unity adds physics and VR interaction. The first problem to solve: the Marble room already has a chair baked in, so the real, grabbable chair needs a way to replace it. Basic multiplayer also starts in Sprint 2.

### Install the APK
1. Download the APK to your computer.
2. Connect your Meta Quest to your computer with a USB cable.
3. Enable Developer Mode and accept the headset’s USB debugging prompt.
4. Install the APK using Meta Quest Developer Hub.
5. Launch InnerFrame on the headset.

### AI Usage

I used **Claude (Anthropic)** as my only AI tool, as a personal assistant rather than a team member. I made the design decisions, and Claude helped me carry them out.

**What I used it for**
- Writing the C# scripts for the interactions
- Generating environment assets in Blender
- Walking me through applying the interactions to objects in Unity
- Getting assets from Blender into Unity

**Who started what**
In Blender, I described what I wanted and Claude built it directly in the scene through MCP, then I tweaked the results by hand. In Unity, I went the other way: I set up the scene and decided what each object should do, then Claude wrote the scripts and told me how to apply them. Claude never touched Unity directly.

**How it was connected**
Blender was linked to Claude through MCP. Unity wasn't connected at all. I copied the scripts from the chat into `.cs` files and dragged them onto objects. For assets, I exported from Blender as FBX and imported them into Unity.

**Example prompts**
- "Can you recreate the bedroom in van gogh's painting in blender and retain the bushstroke style?."
- "How do I transfer this environment into unity?"
- "How do make an interaction that transition from scene 1 (gallery) to scene 2 (the bedroom)?"


Built on October 1, 2026.
