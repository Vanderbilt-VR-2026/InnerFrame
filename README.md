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

## Team

Sonya Vu · Charlyne Dong · Rowling Lin · Serena Deng · Cici Luo

## Course

Built for *Projects in Virtual Reality Design* (CS 4249/5249 / CSET-CMA 3257) at Vanderbilt University, Fall 2026.
