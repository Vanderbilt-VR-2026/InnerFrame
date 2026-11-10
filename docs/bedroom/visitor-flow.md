# Painted Bedroom: the visitor's flow

Written for #105. It describes what the room does on `main` today, in the order a visitor meets it.
**[V]** = read in the code or docs. **[I]** = inferred, not tried. None of it has been walked through on a Quest
since the Tripo room (that walkthrough waits for the room size, #144).

1. **Enter.** From the Gallery, through The Bedroom's pedestal button. [V, `docs/starry-night/README.md`]
2. **Look around.** Point at something you can pick up and a thin pale rim appears around it (#94). Grip takes it.
   Light things follow the hand at once; the chairs lag a little, which is how they feel heavy. [V, code; feel is [I]]
3. **Vincent greets you,** about 2 s after you arrive. Ten seconds later he explains, and asks you to take the mirror
   off the back wall. A solid yellow outline shows it. [V, `docs/vangogh/README.md`]
4. **Hold the mirror near his face** (within 1 m). He paints for 24 s in total: charcoal sketch, colour, finished
   portrait, with a comment at each stage. Move it away and he pauses and asks for it back. Let go near him and it
   stays on a stand beside the canvas. [V, same]
5. **Talk to him at any time** with B. [V, same]
6. **He finishes and thanks you.** The mirror goes back on the wall when you let go. [V, same]
7. **Leave** with the player menu: left Menu button, **Back to Gallery**. [V, `docs/starry-night/README.md`, "as from
   the Bedroom"]

## What each object does

Set in `Assets/InnerFrameBedroomWeb/Resources/PaintedBedroomPhysicsTable.asset`, matched by object name.

| Objects | What you can do | Mass (kg) |
|---|---|---|
| Chair_Front, Chair_Back | Pick up, move, throw a little | 4 |
| Pitcher | Pick up, throw | 2 |
| Carafe, Bottle_Tall | Pick up, throw | 1.5 |
| Bottle_Short | Pick up, throw | 1.2 |
| Pillow_Front, Pillow_Back, Tumbler, SoapDish | Pick up, throw | 0.5 |
| ClothesBrush | Pick up, throw | 0.4 |
| Hat | Pick up, throw | 0.3 |
| Mirror | Off in the table; `MirrorScene` turns its grab on while Vincent is in the room | |
| Bed, Washstand, Basin, Towel, PegRail, Window, doors, pictures | Fixed | |

**How weight works:** at 1 kg or less an object keeps up with the hand. Above that it closes `1 / mass` of the gap to
the hand each physics step (50 a second), so 4 kg takes about 0.2 s to catch up, and throws are scaled down the same
way. Below 1 kg, mass only changes how it pushes other objects. [V, `PaintedBedroomMovable.ApplyWeightToGrab` and the
XR Interaction Toolkit's velocity tracking]

## Scene context for Vincent

`MirrorScene` keeps a one-sentence description of the moment in `SpeechToProxy.SceneContext` (mirror on the wall,
held, sketching, colour, paused, finished). [V]

- **Through the proxy:** sent with each question as the form field `scene`. [V, `SpeechToProxy.Ask`]
- **Through Convai: not sent.** `SpeechToProxy.AskConvai` posts only `charID`, `sessionID`, `voiceResponse`,
  `sample_rate` and the audio. So with Convai on, Vincent doesn't know where the mirror is or how far the portrait has
  got. [V] Whether Convai's `getResponse` has a field for this has not been checked. Owner: Sonya (#131).
