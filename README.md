# InnerFrame

InnerFrame is a VR experience for Meta Quest 3 where people step inside a famous painting, explore it together, and talk with the artist.
The first painting is Vincent van Gogh's *The Bedroom* (1889).

## Team and roles

Roles are flexible, and design and QA are everyone's job.

| Member        | Major      | Relevant skills                                                            | Responsibilities                                                                    |
| ------------- | ---------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Sonya Vu      | CS         | Programming, HCI                                                           | AI dialogue, programming interaction                                                |
| Charlyne Dong | CS         | Product ideation and development, agents/AI, prototyping                   | Project management, concept, player experience                                      |
| Rowling Lin   | Counseling | Graphic design, UX/UI design, Figma, interaction design                    | Storyboarding, prototyping, interaction design, visual design                       |
| Serena Deng   | CS         | Product ideation and development, programming, concept development         | Prototyping, player experience, programming interaction                             |
| Cici Luo      | CS         | Software engineering, networking, backend systems, Git, system integration | Multiplayer, player synchronization, shared interactions, Unity systems integration |

## Sprint 3 (Oct 15 – 29)

Sprint 2 gave us one painted room you could walk around and touch. Sprint 3 brought it to life: **Vincent is in the room and you can talk to him**, **other players are in the room with you**, and the room itself was refined object by object against the painting.

**Demo videos:**
- [Talking to Vincent (Google Drive)](https://drive.google.com/file/d/1By2kR-1te0LT-Me9GGv40InFYGAyXVat/view?usp=sharing): asking Vincent questions and his AI answers.
- [Multiplayer test (Google Drive)](https://drive.google.com/file/d/1wI7SEcz5pCkFCM_bx09htdvD6e4RiZov/view?usp=sharing): two players in the Painted Bedroom, sharing objects and holding the mirror while he paints his self-portrait.

**APK:** [InnerFrame-sprint3.apk (Google Drive)](https://drive.google.com/file/d/1NI4j2W_qiFJgR4KCf8WKev_z2hbBEXhM/view?usp=sharing), built from this branch (`sprint_3`).
**Board:** [InnerFrame Dashboard](https://github.com/orgs/Vanderbilt-VR-2026/projects/12): 14 epics, each task tagged with its sprint.

### Run it

- **Editor:** Unity **6000.3.23f1**. Clone, run `bash scripts/setup.sh` (Git LFS), open the project, open `Assets/Scenes/Gallery.unity` and press Play. Without a headset, the Editor has a desktop player (W A S D and the mouse).
- **Quest 3:** install the APK above (Meta Quest Developer Hub → Device manager, or `adb install -r`), or File > Build Profiles > **Android** > Build And Run with the Quest on USB in Developer Mode.

> [!IMPORTANT]
> **Vincent's AI answers don't work with the APK alone.** The APK plays his scripted mirror lines by itself, but his answers to your questions come from a small proxy that must run **on a Mac** (his voice uses macOS's `say`). Without it, he can't answer.
>
> **Dependencies on the Mac:**
> 1. [Ollama](https://ollama.com/download) and its model: `ollama pull qwen2.5:7b` (about 4.7 GB).
> 2. The proxy's Python packages, once, in `Tools/VanGoghProxy`: `python3 -m venv .venv` then `.venv/bin/pip install -r requirements.txt`. The first start also downloads a speech-to-text model (about 150 MB).
> 3. Start it and leave it running: `.venv/bin/python proxy.py`. Free local models, no API key.
>
> **Connecting:** the Editor works as soon as the proxy runs. A Quest must be plugged into that Mac with USB, then run `adb reverse tcp:8000 tcp:8000` (again after every reconnect). Over Wi-Fi instead, the APK has to be built with the Mac's address. Details: [`Tools/VanGoghProxy/README.md`](Tools/VanGoghProxy/README.md), [docs/vangogh/README.md](docs/vangogh/README.md).

| Action | Quest | Editor |
|---|---|---|
| Teleport / walk | Right / left thumbstick | W A S D + mouse |
| Grab | Grip | Click / E |
| Ask Vincent | Hold **B** | Hold **T** |
| Talk to other players | Hold **X** | Hold **V** |
| Menu | ≡ (left) | M |

### Iteration since Sprint 2

Sprint 2's README set Sprint 3's first goals. Where they stand:

- Refine the room object by object (#31): **done** for the bed, chairs, pillows, washstand set, towel, wall pictures, window, clothes and doors ([#107](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/107), [#111](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/111), [#123](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/123), [#124](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/124), [#125](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/125)). Only the brush box is still being remade.
- The painterly look (#30): flat lighting, new floor, wall and door textures, and colours measured against the painting landed.
- Physics on the furniture: a dropped pillow used to float about 10 cm above the bed, because the bed and pillows were boxes. They now use their own shapes, so pillows land on the bed and the pitcher stands in the basin ([#123](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/123), [#124](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/124)).
- Additional implementations: talking to Vincent, the mirror scene, multiplayer and voice chat.

### What's in this branch (`sprint_3`)

- **Gallery** (`Assets/Scenes/Gallery.unity`): *The Bedroom* on its pedestal, and now Van Gogh's 1887 self-portrait beside it at the same scale. You can ask the portrait questions. The pedestal button takes you to the Painted Bedroom.
- **Painted Bedroom** (`Assets/InnerFrameBedroomWeb/Scenes/PaintedBedroom.unity`), refined object by object against the painting:
  - **New furniture:** Tripo models for the bed (blanket included), both chairs and pillows, the washstand with its basin, pitcher, carafe, soap dish, clothes brush, glass tumbler and two bottles, the towel, and the peg rail with its garments and the straw hat, each sized to the painting.
  - **New surfaces:** painted, tileable textures for the floor, walls and doors.
  - **Closer colours:** the window, the clothes on the pegs, the hat and the doors, each measured against the painting and corrected; the coat repainted with the painting's long brushstrokes.
  - **The wall pictures** cut from the painting itself, and **flat lighting** so the forms come from the brushstrokes, not Unity's shading.
  - **Better physics:** a dropped pillow lands on the bed and the pitcher stands in the basin, using the objects' own shapes instead of boxes.
![The bed at the start of Sprint 3 and now, next to the painting|591](docs/sprint3/bed-before-after.png)
![The washstand and chairs at the start of Sprint 3 and now, next to the painting|594](docs/sprint3/washstand-chairs-before-after.png)
- **Vincent, in 3D**, standing at his easel. He breathes, turns toward you, and leans with his voice.
- **Talk to Vincent:** hold **B**, ask a question, let go. He answers out loud in character (Arles, October 1888, or Saint-Rémy, September 1889, with `VANGOGH_PERSONA_YEAR=1889`), with captions on a painted wooden sign.
![Vincent van Gogh standing at his easel in the Painted Bedroom|454](docs/sprint3/vincent-easel.png)
- **Hold the mirror:** Vincent asks you to take the mirror off the wall and hold it beside his canvas so he can see himself. While you hold it there, his self-portrait goes from a blank canvas to a sketch, to colour, to finished. Move it away and he asks for it back.
- **Multiplayer:** everyone in the same scene is in the same room and sees each other. In the Painted Bedroom you appear as a painted Van Gogh head and hands, with a hat band in your own colour.
  - **Shared objects:** pick up, carry or drop an object, and everyone sees it.
  - **Shared Vincent:** one headset runs the mirror scene and everyone sees the same canvas and hears each line once. When one player asks him something, the others see who is asking and hear the answer.
  - **Voice chat:** hold **X** to talk to the room. Each voice comes from that player's avatar.

### Contributions

| Member       | Sprint 3                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Sonya**    | Merged the Sprint 2 build into `main`: physics, player menu, teleport and rig settings ([#103](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/103)). Multiplayer with Photon Fusion: rooms per scene, avatars, the painted Van Gogh avatar, the desktop test player ([#116](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/116)). Shared Painted Bedroom objects ([#121](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/121)). This README and the demo video.                                                                                                                                                                                                                                                                                                     |
| **Cici**     | Vincent's voice proxy: speech to text, the AI, his voice ([#113](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/113)). Talk to Van Gogh: the Gallery portrait and the 3D Vincent ([#114](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/114)). The mirror scene, painted-sign captions, a living Vincent, Tripo easel and palette ([#118](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/118)). Asset pipeline skills for per-object design ([#115](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/115)).                                                                                                                                                                                                                                                   |
| **Serena**   | Voice chat between players with Photon Voice 2 ([#119](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/119)). Shared Vincent: questions, answers and the mirror scene across players ([#120](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/120)). Coordinated the two-player test and recording.                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| **Charlyne** | The Painted Bedroom's look: moved the room into one Blender file and refined every object toward the painting ([#107](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/107), [#111](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/111)); Tripo furniture with new floor, wall and door textures and drop-tested colliders ([#123](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/123), [#124](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/124), [#125](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/125)); one copy of the room ([#108](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/108), [#110](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/110)). Sprint master: rebuilt the board (14 epics) and wrote the Sprint 3 spec. |
| **Rowling**  | The historically grounded 1889 Van Gogh persona, with 30 historical Q&A tests ([#117](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/117)). The painting's introduction in the Gallery: a wall placard beside *The Bedroom*, and a detail panel that opens with **More** and follows your view (draft, [#112](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/112)).                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |

### Tests

**On a Quest 3** (Sonya, Oct 29, APK from `main`, both demo videos recorded from these runs)
- **Two players in one room:** the Quest and a Unity Editor player joined the same Gallery room (`Joined InnerFrame_Gallery as player 2 (2 in room)`) and saw each other.
- **Voice chat:** push-to-talk works both ways, Quest to Editor and Editor to Quest. The Editor first sent silence until its microphone input was fixed on the Mac.
- **Talking to Vincent:** spoken questions went through the proxy on a MacBook Air (M4) and he answered in character, about 2.4 s per question (hearing 0.45 s, thinking 1.6 s, speaking 0.4 s). Asked about his ear, he gave the fixed in-character line instead of an AI answer.

**Vincent's AI** (proxy and persona)
- **Speed** ([#113](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/113)): recorded questions answered in 1.9–3.0 s on an M4 Pro.
- **Later-life guard** ([#113](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/113)): 9 of 9 questions about his ear or his death got the fixed line, without calling the AI.
- **1889 persona** ([#117](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/117)): 30 historical questions and 9 regression tests; the 1888 persona still keeps its cut-off.

**The mirror scene and Vincent** ([#118](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/118), scripted in the Editor)
- Greeting, explanation and request timing; the canvas at 50% and 100%; the "bring it back" line; the facing check; the mirror rehanging exactly on its nail.
- Every caption page fits the painted sign, and his palette and brush stay in his hands while he breathes and turns.

**The room** ([#107](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/107)–[#125](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/125))
- **Drop tests:** each object lifted 30 cm and dropped lands where it should: pillows on the mattress, the pitcher in the basin (also from 60 cm), bottles, tumbler and the rest on the table.
- **Blender to Unity:** exporting the room from Blender gives the same data byte for byte (`room_io.py check`).
- **Fit:** the table, its objects, the chairs, peg rail and hat touch nothing they shouldn't.

**Every pull request:** `bash scripts/ci-local.sh` passed (the same checks as GitHub: Git LFS, missing `.meta` files, blocked packages, conflicts with `main`).

**Not tested yet:** frame rate on the Quest with the new Tripo room ([#98](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/98)); two Quests on different networks; Rowling's info panel on a headset ([#112](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/112)).

### Known issues

- Vincent needs the proxy running on a laptop nearby; where it runs on Demo Day is still open.
- The macOS voice sounds robotic, and the 1889 persona's limits are enforced by its prompt rather than a fixed rule.
- Vincent has no rig: no mouth movement or blinking.
- On other players' screens, the held mirror moves at the network's rate, so it can look a little steppy.
- The free Photon plan allows 20 players at once and is for development only.
- The new room hasn't been tested on a Quest yet, and its frame rate with the Tripo models still needs measuring ([#98](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/98), [#109](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/109)). It opens in the Unity Editor with no errors from the room, and the physics drop tests pass.
- The brush box is out of the room until its Tripo version is ready. When it comes back, **InnerFrame → Multiplayer → Make Painted Bedroom Objects Shared** has to be run again so it's shared.
- When one player asks Vincent, the others don't hear the question: voice chat pauses while B is held, so they see it as text and hear only his answer ([#126](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/126)).

## Next: Sprint 4

- **Carried over from Sprint 3**
	- Frame-rate budget, and removing the unused shadow pass ([#98](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/98), [#109](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/109)).
	- Painterly colour grading and outlines ([#30](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/30)).
	- A smooth entry into the painting ([#16](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/16)), the gallery entrance ([#104](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/104)), object interactions ([#105](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/105)) and sounds ([#106](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/106)).
	- The brush box, as a Tripo model.

- **Story flow:** the gallery follows Van Gogh's last two years, from hope in Arles, through the asylum at Saint-Rémy, to one last moment of hope.
![Who Vincent is in each painting: The Bedroom, Café Terrace at Night, The Starry Night and Almond Blossom|800](docs/sprint3/vincent-chapters.png)
	- **Sunflowers** (Charlyne): Arles, 1888, painted for Gauguin's room in the Yellow House.
	- **Café Terrace at Night:** Arles, 1888, a busy café under the stars. The crowd stays still; only Vincent talks.
	- **The Bedroom** (Charlyne): Arles, 1888. Keep refining the room and its interactions.
	- **The Starry Night** (Cici): Saint-Rémy, 1889. Vincent on a hill above the town at night, styled after *The Little Prince*.
	- **Almond Blossom:** Saint-Rémy, 1890, painted for the birth of his nephew. It ends the journey on hope.

- **Intro scene** (Rowling): players start in a painted town at night under a swirling sky, and point at **Begin** to enter the gallery. Concept:
![Concept for the intro scene: InnerFrame written in a starry sky above a painted town, with a Begin sign|282](docs/sprint3/intro-scene-concept.png)

- **Gamification design doc** (Serena): the full journey across scenes, how people talk to Vincent, and how a **portrait deck** ties the paintings together.

- **Improve the flow of each painting**
	- **Conversation** (Sonya): Convai integration, for better conversation quality and a more consistent character.
	- **Multiplayer rule:** the first player to start a conversation gets Vincent's answer; if they leave, the other can carry on.
	- **Avatars** (Sonya): a player look in each painting's style, like the Bedroom's painted head and hands.
	- **Music and audio** (Rowling): music between paintings that follows the story's mood, plus object and footstep sounds ([#106](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/106)).
	- **Artist info panel** (Rowling, [#112](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/112)): a placard beside each painting, and a panel that opens with **More**, about the artist and the painting, with **Enter** to step inside.

- **Info panel example draft**
![The detail panel for The Bedroom|291](docs/sprint3/artwork-detail-panel.png)

| Who      | Sprint 4                                                       |
| -------- | -------------------------------------------------------------- |
| Cici     | The Starry Night; the entrance / intro scene                   |
| Charlyne | Sunflowers; Bedroom refinement (visuals and conversation flow) |
| Serena   | Gamification design doc; user journey; conversation flow       |
| Sonya    | Convai integration; avatar for gallery and the painting scenes |
| Rowling  | Music and audio between scenes, lobby and intro scene          |

---
Getting started, Sprint 1 and APK install: [Sprint 1 README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/4a446d02e9332498396e2c648747f50fe0cc165a/README.md) · [Sprint 2 README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/sprint_2/README.md) · Day-to-day work: [CONTRIBUTING.md](CONTRIBUTING.md) · *CS 4249 / CSET-CMA 3257: Virtual Reality Design*, Vanderbilt University, Fall 2026.
