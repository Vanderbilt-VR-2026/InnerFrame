# What Vincent knows about the Bedroom's objects

One row per object in the Painted Bedroom, by the name the scene uses
(`PaintedBedroomMovable` / the physics table's `objectName`). The **Vincent
knows** text is what goes to Convai when the visitor picks up or selects that
object for the first time, and with any question they ask while holding it.
It is written from the painter's side (why he painted it, what its colour does
for the picture), not what the object is for, and every prompt asks him to
answer that way. Keep each one under about 50 words: it is sent with the
question, and longer text delays his first words.

Status: draft, written 11/13 from `Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg`
(the 1889 version, Art Institute of Chicago) and the room's own textures. The
art-history lines are [I]: from general knowledge, not checked against a source
yet. The colours (violet walls, butter-yellow wood, lemon-green sheets, scarlet
blanket, orange table, blue basin, lilac doors) are as he listed them in his
October 1888 letter to Theo about the first version, from memory: check them
against the letter before they ship.

## The whole room (sent once, as scene context)

> The visitor is inside his painting of his bedroom in the Yellow House in
> Arles. This is the third version, painted in 1889 at the asylum in Saint-Rémy
> from his first canvas of 1888. He painted it to give a feeling of complete
> rest: flat colour without shadows, like a Japanese print, and pairs of
> opposite colours that make each other stronger (violet walls against yellow
> wood, a scarlet blanket against green sheets, a blue basin on an orange
> table). The walls were violet and have faded towards blue.

## Objects you can pick up (12)

| ID | What it is | Where | In the painting | Vincent knows |
|---|---|---|---|---|
| `Chair_Front` | Straw-seated rush chair, yellow wood | Front left, by the wall | yes | He painted the chairs fresh butter yellow, the same yellow as the bed, so the furniture reads as one family against the violet walls. The rush seat is a few thick strokes. A plain chair stood for a simple, honest life; he also painted his own chair on its own. [I: his chair painting is a separate 1888 canvas] |
| `Chair_Back` | The second rush chair | Between the washstand and the bed | yes | It makes a pair with the first chair. He painted this room in twos: two chairs, two pillows, two portraits. It is the room of a man hoping a friend, Gauguin, would come and share the house. [I: the "pairs" reading is interpretation] |
| `Pillow_Back` | Pillow at the head of the bed | On the bed | yes | The pillows and sheet are a very light lemon green, so the scarlet blanket glows against them: red and green, opposite colours, side by side. He wanted the bed to look like sleep itself. |
| `Pillow_Front` | Second pillow | On the bed | yes | A pair with the first pillow. Light colours on the bed and strong ones around it, so the eye comes to rest there. |
| `Hat` | Straw hat | Hanging on the peg rail | yes | His straw hat, a small spot of yellow among the darker clothes on the pegs. He kept the hanging things to a few strokes so they don't break the calm of the wall. |
| `Pitcher` | Blue jug | Standing in the basin | yes | He put the cool blue jug and basin on the orange table on purpose: blue against orange, so that small corner sings. Simple shapes with dark outlines, like in a Japanese print. |
| `Carafe` | Pale green-blue carafe | Left end of the washstand | yes | The carafe is a pale green-blue against the orange table and the violet wall: a few cool notes that keep the warm corner from getting heavy. |
| `Tumbler` | Glass tumbler | On the washstand | yes | A small glass by the carafe, a touch of pale light. Even the little things on the washstand are there to balance the colour of that corner. |
| `Bottle_Tall` | Tall glass bottle with a cork | Right of the washstand | yes | Two small bottles on the washstand, painted in a few quick strokes. He put in the ordinary things of his room because the painting is about a simple life, with nothing hidden. |
| `Bottle_Short` | Short glass bottle with a cork | Right of the washstand | yes | The second, shorter bottle: small pale spots that carry the light across the table. |
| `SoapDish` | Soap dish | Front of the washstand | yes [I: the pale dish at the front of the table] | A little dish for the soap, a few strokes of colour. Nothing in the room is fancy, and that is the point: a plain room made restful by colour alone. |
| `ClothesBrush` | Clothes brush | Right end of the washstand | yes | He painted the small, plain things with the same care as the bed: the room is a portrait of how he lived. |
## Objects you can only select (14)

These can't be picked up (they are fixed in the physics table). Point at one
and press grip, as if to pick it up: the hover rim shows, nothing moves, and
Vincent talks about it (`PaintedBedroomSelectable`). For the window, press a
casement.

| ID | What it is | Where | In the painting | Vincent knows |
|---|---|---|---|---|
| `Bed` | Big wooden bed, scarlet blanket | Right side, along the wall | yes | The heart of the painting: a big bed of solid wood in butter yellow, with lemon-green sheets and a scarlet blanket. He wanted it to say sleep and rest, and made it heavy and sturdy so it feels safe. |
| `Washstand` | Small orange table | Back left corner | yes | He painted the dressing table orange and set a blue basin and jug on it: orange against blue, opposite colours that make each other brighter. |
| `Basin` | Blue washbasin | On the washstand | yes | The blue basin on the orange table, one of the room's pairs of opposite colours, with the blue jug standing in it. |
| `Towel` | Striped towel | On a nail by the left door | yes | A pale towel on a nail by the door, with a few stripes of colour: a quiet vertical shape beside the busy washstand. |
| `PegRail` | Peg rail with his clothes | Back wall, above the bed's head | yes | The pegs with his clothes and straw hat. He showed that he owned little: a few things hung up, painted with a few strokes. |
| `Window` | Green casement window | Back wall, left | yes | He painted the window green and half open, and the light without shadows, flat like a Japanese print, so nothing in the room feels dramatic. |
| `Door_Left` | Door | Left wall | yes | He painted the doors lilac. One led to the stairs, the other to the guest room he made ready for Gauguin. [I: which door is which not checked] |
| `Door_Right` | Door | Right wall, behind the bed | yes | The other lilac door, closing the room in so it feels sheltered. |
| `Mirror` | Small mirror | Back wall, left, by the window | yes | His small shaving mirror. (Has its own part in the visit: the mirror and portrait sequence in `MirrorScene` already tells Vincent what is happening. Not selectable on its own; see below.) |
| `Picture_Landscape` | Small landscape, a tree in a field | Above the pegs | yes | A small landscape over the bed, a tree in a field: a bit of the outdoors he loved, brought into the room. |
| `Picture_PortraitMan` | Portrait of a man with a reddish beard | Right wall, high up | yes | In this version he hung his own self-portrait over the bed. In the first version the portraits were of friends. [I] |
| `Picture_PortraitWoman` | Portrait of a woman | Right wall, high up | yes | A portrait of a woman beside his own. Who she is isn't certain. [I] |
| `Picture_DrawingLeft` | Pale yellow work on paper | Right wall, lower | yes | One of two pale works on paper pinned to the wall, drawings or prints: a quiet pale yellow against the violet. [I] |
| `Picture_DrawingRight` | Second pale work on paper | Right wall, lower | yes | The second one, beside it: they make a pair, like everything else in this room. |
## Not objects (room shell, no ID to select)

`Room_Walls`, `Room_Floor`, `Room_Ceiling`, `Room_Skirting`, `Door_Frames`,
`Window_View`. They aren't `PaintedBedroomMovable`s. What Vincent knows about
them is in "The whole room" above.

## In the painting, or ours?

Every object's notes say whether it is in his painting (`"inPainting": true`)
or something we added to the world around it. Only objects marked `true` get
the painter's instruction (why he painted it, what its colour does). For
anything else he is told that it isn't in his painting and not to say he
painted it, or why (`VincentObjectTalk.NotPainted`): he talks about what he sees
in it, or how it sits beside what he did paint. **Left out means not in the
painting**, so a forgotten flag can't make him invent a reason. In a scene with
any added objects, the room sentence also says that some things were added and
that he shouldn't claim to have painted anything unless told it's his.

All 25 Bedroom objects are in the painting (checked against
`Assets/Art/Painting/The_Bedroom_Van_Gogh.jpg`). Scenes like Starry Night,
where most of the hill is ours, need this marked object by object, and their
room paragraph and `knows` text should describe the added things as facts,
not as his intentions.

## How the text reaches Convai

Convai's `getResponse` takes text (`userText`) or audio, never an image, so the
pictures never reach Convai. The text above stands in for them.

- **What ships:** `Assets/VanGogh/Resources/VincentNotes/PaintedBedroom.json`
  holds the room paragraph and, for each ID, a short `what` ("the blue jug from
  the basin") and the **Vincent knows** text. Edit both files together: the test
  below fails when the two disagree (it ignores the `[I]` tags).
- **When:** the first time this headset's player picks up an object (VR grab
  or desktop drag) or selects a fixed one (grip on it, a window casement, or a
  desktop click), `VincentObjectTalk` sends
  `userText = "(Where we are: <room>) (The visitor picks up / points at <what>. What you know about it: <knows> <as the painter> One or two short sentences, without describing what they did.)"`.
  `<as the painter>` is `VincentObjectTalk.AsThePainter`: answer as the painter,
  why you chose to paint it and what its colour, shape or place does for the
  picture, more than what it is used for. For an object not in the painting it
  is `VincentObjectTalk.NotPainted` instead (see above), in every prompt and
  question.
  The room paragraph goes with the first one only (Convai's session keeps it).
  He answers as he does to a question: streamed, in his headset voice, heard
  by everyone in the room. There's no thinking line, and the other players
  don't see the prompt.
- **Your own questions about an object:** ask while holding it, or within 20 s
  of pressing it, and your question goes with that object and its notes:
  `"why did you paint this? (As they ask, they are holding <what>. What you know about it: <knows> If they say this or that, they mean it. <as the painter>)"`.
  The other players see only your question.
- **"This" with nothing in focus:** when the question says *this, that, it,
  these, those, here* or *there* and you aren't holding or pointing at
  anything, he is told to ask which one you mean ("point at it and press
  grip") instead of guessing. The next object you press within 30 s answers
  it: `"(The visitor points at <what>: that is what they meant when they asked "<question>". ...)"`.
  That one goes even if he has talked about the object before, and doesn't
  wait for the 20 s between comments. It's a word match, so a question like
  "is it hard to paint?" arms it too; a press in the next 30 s then gets that
  question answered about the object you pressed.
- **Only with your question as text** (Headset Speech To Text on, the default):
  when the recording goes as audio, Convai takes nothing else with it.
- **Never over him:** it waits until his greeting is done and he isn't talking,
  being asked, or held by another player's question. A pickup that waits more
  than 8 s is dropped, and isn't marked as seen. At most one comment every 20 s
  (`VincentObjectTalk` fields). B while it is on its way replaces it with your
  question.
- **Each object once** per visit, per headset (not counting "which one?"). The mirror isn't in the notes:
  `MirrorScene` has its own lines.
- **Convai only.** The AI proxy takes audio only, so on the proxy he says
  nothing (the Console says why, once).

Test: **Tools > InnerFrame > Van Gogh > Test Object Talk**, or
`-executeMethod VincentObjectTalkTest.RunFromCommandLine`. It needs no headset
and no Convai.
