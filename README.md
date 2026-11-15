# InnerFrame

InnerFrame is a VR experience for Meta Quest 3 where people step inside Van Gogh's paintings, explore them together, and talk with the artist.
Since Sprint 4 it is one journey through his last two years, from *The Bedroom* (Arles, 1888) to *Almond Blossom* (Saint-Rémy, 1890).

## Team and roles

Roles are flexible, and design and QA are everyone's job.

| Member        | Major      | Relevant skills                                                            | Responsibilities                                                                    |
| ------------- | ---------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Sonya Vu      | CS         | Programming, HCI                                                           | AI dialogue, programming interaction                                                |
| Charlyne Dong | CS         | Product ideation and development, agents/AI, prototyping                   | Project management, concept, player experience                                      |
| Rowling Lin   | Counseling | Graphic design, UX/UI design, Figma, interaction design                    | Storyboarding, prototyping, interaction design, visual design                       |
| Serena Deng   | CS         | Product ideation and development, programming, concept development         | Prototyping, player experience, programming interaction                             |
| Cici Luo      | CS         | Software engineering, networking, backend systems, Git, system integration | Multiplayer, player synchronization, shared interactions, Unity systems integration |

## Sprint 4 (Oct 29 – Nov 15)

Sprint 3 gave us one painted room with Vincent in it. Sprint 4 turned it into **a journey**: a new opening, a rebuilt Gallery, and **three new paintings to step into**, *Café Terrace at Night*, *The Starry Night* and *Almond Blossom*, each with its own music and, in two of them, its own small game with Vincent. Vincent now answers straight from the headset, knows which scene he's in, and every player hears him.

**Demo video:** recorded on a Quest 3 on Nov 15: [InnerFrame Sprint 4 demo (Google Drive)](https://drive.google.com/file/d/1JyyuADIitzsAxQNRA9j22kRiIk3k6dWJ/view?usp=sharing)

**APK:** [InnerFrame-sprint4.apk (Google Drive)](https://drive.google.com/file/d/1RxKJHL6M9EJXow99q4mLI0xeaeGZbmk2/view?usp=sharing), built from this branch (`sprint_4`, `a83d32d`). Anyone with the link can download it.
**Board:** [InnerFrame Dashboard](https://github.com/orgs/Vanderbilt-VR-2026/projects/12): 14 epics; every task tagged [S1]–[S5] and linked to its epic.

![From the demo: the opening, the Gallery, Café Terrace at Night, The Starry Night and Almond Blossom|800](docs/sprint4/journey.jpg)

### Run it

- **Quest 3:** install the APK above with Meta Quest Developer Hub (**Add a build**) or `adb install -r InnerFrame-sprint4.apk`; or, with the Quest on USB in Developer Mode, **File > Build Profiles > Android > Build And Run**. If it says *Installation error*, an older InnerFrame build signed on another computer is in the way: uninstall it first (`adb uninstall com.UnityTechnologies.com.unity.template.urpblank`), then install again.
- **Editor:** Unity **6000.3.23f1**. Clone, run `bash scripts/setup.sh` (Git LFS), open `Assets/Entrance/Scenes/Entrance.unity` and press Play: the build starts there, then the Gallery. Without a headset, the Editor has a desktop player.

> [!IMPORTANT]
> **Vincent's answers need Convai and the internet.** The headset talks to Convai directly: no Mac, no proxy, no USB. This APK has the team's Convai key built in, so Vincent answers with no setup. Without Convai, his scripted lines still play in his voice, but he can't answer your own questions.
>
> **To build with Convai:** paste Vincent's Character ID into `Assets/VanGogh/Resources/ConvaiDirectSettings`, and your Convai API key, alone on one line, into `Assets/VanGogh/Resources/ConvaiApiKey.txt`. Git ignores that file: never commit it. The key is packed into the APK, so a build with a key stays inside the team. Details: [docs/convai/README.md](docs/convai/README.md).

| Action | Quest | Editor |
|---|---|---|
| Move / turn | Left stick / right stick (smooth turn) | W A S D + mouse |
| Teleport | Right stick | |
| Point at a star, a letter, a branch… | Ray + **trigger** | Click |
| Grab | Grip | Click / E |
| Enter a painting | Point at it in the Gallery, pull the trigger, then **Enter** | Centre dot or mouse, E or click |
| Ask Vincent | Hold **B** | Hold **T** |
| Talk to other players | Hold **X** | Hold **V** |
| Menu, **Back to Gallery** | ≡ (left) | M |

### Iteration since Sprint 3

**From the class review (Nov 3):**

| Feedback | What changed |
|---|---|
| The Bedroom doesn't feel the right size | Measured against real furniture and eye height; the room is now real-sized for a standing visitor, with furniture refitted to the target image, size chosen on a Quest ([#193](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/193)) |
| "Hold B" is too small; where does the mirror go? | A reusable dialogue box with text about 5× larger, gold outlines on the mirror, and Vincent always finishes his portrait ([#135](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/135)) |
| Robotic voice; off-topic answers | Convai straight from the headset ([#139](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/139)); one voice for scripted and live lines ([#179](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/179), [#180](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/180)); he knows each scene's objects ([#200](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/200)) |
| Multiplayer lag | Shared objects no longer lag while held and stay smooth for whoever watches ([#139](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/139)) |
| The first view is empty | A new opening scene, and a rebuilt, lit Gallery with the paintings in story order ([#186](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/186), [#187](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/187), [#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188), [#199](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/199)) |
| The image looks blurry | A sharper-image pass is ready ([#201](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/201)), merged after the freeze, so it's in Sprint 5 |

**From our own playtest (Nov 5):** the lantern is easier to find and trigger ([#183](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/183)); one **Back to Gallery** in every scene ([#181](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/181)); hints for entering a painting and getting back ([#182](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/182)); the story order fixed: Bedroom → Café Terrace → Starry Night → Almond Blossom.

**Sprint 3's "Next" list:** *Sunflowers* was dropped for *Café Terrace* and *Almond Blossom*. Convai integration, the opening scene, scene music and the artwork cards, object and footstep sounds, and the Bedroom's interactions landed, and each new painting got its own way to play (compared below). Player avatars and the frame-rate budget ([#98](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/98)) move to Sprint 5.

### What's in this branch (`sprint_4`)

#### The story

The Gallery is the hub, and its paintings are the chapters of Vincent's last two years, in order:

| # | Painting | Where and when | Time of day in the Gallery | Who Vincent is here |
|---|---|---|---|---|
| 1 | *The Bedroom* | Arles, 1888 | Afternoon | At home in the Yellow House, painting his self-portrait |
| 2 | *Café Terrace at Night* | Arles, 1888 | Late afternoon | (not in the scene yet) |
| 3 | *The Starry Night* | Saint-Rémy, 1889 | Evening | In the asylum's months, sketching the night from a hill |
| 4 | *Almond Blossom* | Saint-Rémy, 1890 | Morning | Hearing that his nephew is born, and painting for him |

It starts in hope in Arles, goes through the asylum at Saint-Rémy, and ends on one last moment of hope.

#### A new opening

You no longer start in the Gallery. The **Entrance** ([#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188), Rowling) opens on a painting with its own score, *Letter to Vincent*:

- *InnerFrame* written in a starry sky above a painted town; point at **Begin**.
- **Choose Who You'll Be** (**Boy** or **Girl**), then **Enter the Story**.
- **New Room** gives a 4-digit room code to share; **Join Room** takes one, so friends land in the same Gallery.
- A controller tutorial; *Van Gogh* written by hand across the screen, with the sound of a pen; then it dissolves into gold, which opens on the Gallery. **Skip** jumps ahead.
- Buttons click when chosen, and the controller's ray is gold, with a yellow outline on what it points at.

![The opening: Choose Who You'll Be|600](docs/sprint4/entrance.jpg)

#### The Gallery, rebuilt

- **A smaller, warmer 19th-century room** ([#186](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/186), Sonya): 7 × 9 m (was 10 × 14 m), painted textures, a skylight, carved gilt frames, plants, a statue and wall lanterns; sunlight through the skylight and a spotlight on each painting ([#187](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/187)).
- **Five paintings at one scale** ([#199](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/199), Cici): Vincent's self-portrait by the door, then the four chapters, numbered, with yellow walls, a Tripo arch and a 3D corridor.
- **Point and pull the trigger** on a painting: its frame lights up and a small menu opens with **Enter**. The pedestals are gone.
- **A card beside each painting** (Rowling), with its number and time of day, and **More** for the artist and the painting (the Café's with his 1888 *Self-Portrait Dedicated to Paul Gauguin*).
- **Getting back:** every painting says once how to return, and the menu's **Back to Gallery** works the same everywhere ([#181](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/181), [#182](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/182)).

![The Gallery, and the menu that opens when you point at Almond Blossom|800](docs/sprint4/gallery.jpg)

#### Music

Every place has its own music, and it crossfades (2.4 s) as you move between them ([#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188), Rowling). While Vincent speaks or you ask him something, the music drops to 25% and comes back after.

| Where | Music |
|---|---|
| Opening | *Letter to Vincent*, original score |
| Gallery | Warm strings loop, original arrangement |
| The Bedroom | *Hiraeth*, Scott Buckley (CC BY 4.0) |
| Café Terrace at Night | *Parisian*, Kevin MacLeod (CC BY 4.0) |
| The Starry Night | *Adrift Among Infinite Stars*, Scott Buckley (CC BY 4.0) |

Credits are in the menu and in [docs/ASSETS.md](docs/ASSETS.md).

#### The four paintings, and how each one plays

Each painting asks something different of the player. None has a timer or a way to fail.

| | The Bedroom | Café Terrace at Night | The Starry Night | Almond Blossom |
|---|---|---|---|---|
| **What you do** | Explore and handle his things; hold the mirror for his self-portrait | Walk into the square | Help him turn a real night into his painting | Help him paint the almond tree into bloom |
| **Kind of play** | Free exploration, physics | Atmosphere | A guided sequence in the sky | A story act: letter, task, reward |
| **Main input** | Grip (pick up), select | Thumbsticks | Trigger: point, then hold and trace | Trigger: point, then hold and sweep |
| **Vincent** | At his easel; comments on each object you pick up, answers questions | Not yet | Sits by a rock, names the stars, points with his charcoal, answers questions | Gives you Theo's letter, reacts, asks for your help, shows you the painting |
| **Ending** | His self-portrait finishes | (open) | Dawn, and his sketch to take home | *Almond Blossom* on his easel, and what became of it |
| **Together** | Yes | Not yet | Yes | Not yet |

**The Bedroom** (Rowling's scene; Charlyne, Sonya). The Sprint 3 room, made to feel real-sized ([#193](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/193)) with clearer window glass and a painted wood frame ([#177](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/177)). Objects have believable weights, a pale rim shows what can be picked up, and you hear footsteps and objects being picked up and landing ([#176](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/176), [#178](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/178)). The window opens, and the mirror stops at the walls ([#192](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/192)). **New:** pick up or select anything, and Vincent says a sentence or two about it as its painter ([#200](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/200)).

**Café Terrace at Night** (Charlyne, [#141](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/141); brought in by [#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188)). The square in Arles at night: the yellow wall and awning, the terrace, cobbles between blue houses, a starry sky. Its light is baked in Blender so it runs without realtime lights. This sprint it is the environment and its music only; Vincent, the people and the game come next.

![Café Terrace at Night, with stand-ins for the people|600](docs/sprint4/cafe-terrace.jpg)

**The Starry Night** (Cici; [#136](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/136), [#137](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/137), [#140](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/140), [#183](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/183)). A hilltop above Saint-Rémy at night, with Vincent on a rock with his sketchbook. **The sky is the game:** it starts as a real night and becomes his painting as you help him, one step at a time, each named in the hint box:

1. Walk up to Vincent and sit beside him.
2. **Dim the lantern** on its post: point and pull the trigger.
3. **Wake the stars:** point at the star he names and pull the trigger; it blooms.
4. **Trace the wind:** hold the trigger and follow the dots along the swirl; the sky turns into the painting.
5. **Dawn:** the sky pales and the stars fade. **Take his page**, his sketch of the night.

Other players on the hill see the same stars and wind ([#175](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/175), Serena), and you can ask Vincent about the cypress or the village below ([#200](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/200)).

![The Starry Night: tracing the wind, and Vincent on his rock|800](docs/sprint4/starry-night.jpg)

**Almond Blossom** (Serena; [#202](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/202), [#203](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/203)). His studio at the asylum, February 1890, built in 3D after *Window in the Studio*, with the almond tree outside. The last chapter is a short story in five beats:

1. **The letter:** Vincent holds out a letter from his brother Theo. Take it, and it comes up to your eyes: Theo's wife Jo has had a boy, named Vincent after him.
2. **The invitation:** his joy; he points at the almond tree and asks you to help him paint it for the baby's room. **Open the window.**
3. **The brush:** take the brush from the glass on the sill.
4. **Paint:** hold the trigger and sweep the beam along the branches. The buds open where you paint, petals drift into the studio, and once most are open the rest bloom in a wave.
5. **The painting:** he turns to his easel, where *Almond Blossom* grows, and a card tells what became of it.

![Almond Blossom: Theo's letter, in front of your eyes|600](docs/sprint4/almond-letter.jpg)
![Almond Blossom: the tree in bloom, and the card at the end|800](docs/sprint4/almond-bloom.jpg)

#### Everywhere

- **Vincent's voice:** his scripted lines and his answers in one Piper voice, heard by every player, with a short "let me think" right away ([#179](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/179), [#180](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/180)).
- **Smooth turning** on the right stick in every scene ([#190](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/190)).
- **Shared objects** follow your hand closely and show who holds them with an outline in their colour ([#139](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/139)).
- **One dialogue box** for Vincent's words in every scene ([#135](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/135), [#184](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/184)).

### Contributions

| Member | Sprint 4 |
| --- | --- |
| **Cici** | The Starry Night: the scene, its art, Vincent v2 seated and talking, the lantern, and its place in the Gallery ([#136](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/136), [#137](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/137), [#140](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/140), [#183](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/183)). Gallery Part B: five paintings, point-to-enter menus, arch and corridor ([#199](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/199)). The reusable dialogue box ([#135](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/135), [#184](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/184)), the Tripo + Mixamo skill ([#134](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/134)) and the asset register check ([#197](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/197)). Reviews and merges for the team, including bringing #188 up to date ([#196](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/196)). |
| **Sonya** | Vincent through Convai, and shared objects that feel right ([#139](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/139)). His Piper voice on every headset, streamed answers ([#179](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/179), [#180](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/180)). Vincent knows each scene: Bedroom object talk, Starry Night context, one conversation per scene ([#200](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/200)). Back to Gallery everywhere and the enter / return hints ([#181](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/181), [#182](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/182)). The Gallery room and its lighting ([#186](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/186), [#187](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/187)). |
| **Rowling** | The new opening with its *Letter to Vincent* score and gold transition, the controller tutorial, the painting cards with Vincent's portraits, and the music for the Gallery and every painting ([#174](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/174), [#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188)). Quest testing of the Gallery. |
| **Charlyne** | Café Terrace at Night, the environment ([#141](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/141)). The Bedroom: real size, window, mirror, object weights, hover rim, footsteps and object sounds ([#176](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/176), [#177](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/177), [#178](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/178), [#192](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/192), [#193](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/193)). Smooth turning ([#190](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/190)). Sprint planning, the board, the spec and the project hub. |
| **Serena** | Gamification and the journey design, *One Day with Vincent*. Almond Blossom as Act 4: the studio, Theo's letter, the almond tree painted into bloom, the ending, and its Quest controls ([#202](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/202), [#203](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/203)). The Starry Night shared between players ([#175](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/175)). The Sprint 4 APK, this README and the demo video. |

### Tests

**On a Quest 3** (Serena, Nov 15, APK from `sprint_4` at `a83d32d`, single player; the demo video is from this run)
- **The whole journey:** the opening → Gallery → each painting → **Back to Gallery**, for all four paintings; it ran immersive, not as a 2D window.
- **Almond Blossom with the Quest controllers** ([#203](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/203)): letter, window, brush, painting the tree into bloom and the ending card, all with the trigger.
- **The Starry Night:** sit beside Vincent, wake the stars, trace the wind, dawn, take his page.
- **Café Terrace** (Charlyne, Nov 11, café-only test build): ran immersive at 72/72 fps, about 4.5 ms per frame, headset still. Size chosen in the headset.
- **The Bedroom** (Charlyne, Nov 11–12, local test builds of [#178](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/178), [#192](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/192), [#193](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/193) and [#176](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/176) together): chairs no longer lag in the hand, held objects stay out of the walls, room size chosen in the headset.
- **The Gallery** (Rowling): works; see the flicker under Known issues.

**In the Unity Editor** (from each pull request; details there)
- **The opening, cards and music** ([#188](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/188)): 176 Play-mode checks for the Gallery cards, 83 for the opening and its audio, 45 for the Café music and the crossfades between scenes.
- **The Starry Night** ([#137](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/137), [#140](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/140), [#183](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/183)): a scripted playthrough, the layout and budget check, the way in from the Gallery and back, and the fixes from the Nov 5 playtest.
- **The Bedroom:** the hover-rim test ([#178](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/178)), wall-blocking and window tests ([#192](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/192)), sound triggers ([#176](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/176)).
- **Vincent's voice** ([#180](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/180)): his first real sentence 2.0–3.2 s after you let go of B (7 runs, Editor on a Mac with real Convai), down from 5–7 s.

**Every pull request:** `bash scripts/ci-local.sh` passed (the same checks as GitHub: Git LFS, missing `.meta` files, blocked packages, conflicts with `main`).

**Not tested yet:** two players in the new scenes on Quest; Vincent's scene knowledge ([#200](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/200)) on a headset; the Bedroom's frame rate with the real-sized room.

### Known issues

- **Gallery:** from some angles parts of the room flicker or disappear (flagged by Rowling; Serena fixes it in Sprint 5); the Starry Night's spotlight lights the Café painting; the pointing rim outlines the Bedroom's whole room instead of its frame ([#204](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/204), open).
- **Not networked yet:** Café Terrace and Almond Blossom are single-player. The Gallery, the Bedroom and The Starry Night are shared.
- **Café Terrace** has no Vincent and no interactions yet; the people are stand-ins.
- **The image is soft on Quest.** The sharper-image settings ([#201](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/201)) were merged after the freeze.
- **Bedroom lighting:** the baked daylight ([#189](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/189)) isn't in this build; the room still uses flat lighting.
- **Almond Blossom:** three Tripo props (chest, still life, paint box) are still at full size and need reducing for Quest.
- **Convai key:** a build made with a key carries it inside the APK, and this APK's link is public for now; the key may be moved out of the build or rotated in Sprint 5. The free Photon plan allows 20 players at once and is for development only.

## Next: Sprint 5 (Nov 15 – Dec 3): connect everything, then stabilise

- **The journey end to end:** guided mode with Vincent between paintings, free mode from the Gallery ([#143](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/143), [#162](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/162)); Café Terrace into the journey with its own room and Vincent ([#206](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/206)); what Vincent gives you at the end of each painting ([#169](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/169)–[#171](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/171)).
- **Together:** player avatars ([#132](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/132)); two players in every scene, including Café Terrace and Almond Blossom.
- **Menus and onboarding:** one immersive menu and the same controls everywhere ([#198](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/198)).
- **Look and performance on Quest:** sharper image ([#201](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/201)), baked light in the Gallery and the Bedroom ([#194](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/194), [#189](https://github.com/Vanderbilt-VR-2026/InnerFrame/pull/189)), the frame-rate budget ([#98](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/98)), lighter Almond Blossom props.
- **Music and plaques for every painting** ([#133](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/133), [#148](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/148)–[#153](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/153)).
- **A usability test** with people outside the team ([#99](https://github.com/Vanderbilt-VR-2026/InnerFrame/issues/99)).

---
Earlier sprints: [Sprint 1 README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/4a446d02e9332498396e2c648747f50fe0cc165a/README.md) (getting started, APK install) · [Sprint 2 README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/sprint_2/README.md) · [Sprint 3 README](https://github.com/Vanderbilt-VR-2026/InnerFrame/blob/sprint_3/README.md) · Day-to-day work: [CONTRIBUTING.md](CONTRIBUTING.md) · *CS 4249 / CSET-CMA 3257: Virtual Reality Design*, Vanderbilt University, Fall 2026.
