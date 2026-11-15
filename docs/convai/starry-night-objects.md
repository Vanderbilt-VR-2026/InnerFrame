# What Vincent knows about Starry Night

The Starry Night scene (owner: Cici) is mostly ours: a hilltop, a rock, a
lantern and a blanket built around the painting's sky, cypress and village.
So Vincent answers **questions** here, with the right context; he never comments
on his own when you press something (the story already gives every press a
scripted line).

Ask out loud (hold B). When your question names something below ("why did you
paint the cypress?"), its notes go with it to Convai. For what is in the
painting he answers as the painter; for what we added he is told it isn't in
his painting and not to say he painted it, or why
(`VincentObjectTalk.NotPainted`). The room paragraph goes with your first
question. Ask about "this" or "that" without naming anything and he asks you to
say which one they mean, for example the cypress, the moon, the village or the lantern; your next question then names it. Nothing here needs a press.

The shipped text is `Assets/VanGogh/Resources/VincentNotes/StarryNight.json`;
this list must say the same (`VincentObjectTalkTest` checks it, ignoring the
`[I]` tags). **Called** is the words a question can use (plurals count).

Status: draft, 11/13, from the scene's README ("What is made of what") and
general knowledge of the painting. The art-history lines are [I] until checked.

## The scene (sent with the first question)

Sent once per scene, with your first question, followed by every **What it is**
below in two lists (in the painting / made for this visit). Convai keeps it for
the rest of the visit, so "those houses down there" or "the bright one" make
sense to him even when no **Called** word matches. Each scene has its own Convai
conversation, so nothing from the Bedroom follows him here.

> The visitor is sitting with Vincent on a small hilltop above Saint-Rémy at
> night. The sky, the stars, the crescent moon, the cypress, the village with
> its church and the far hills are from his painting The Starry Night, made in
> June 1889 at the asylum in Saint-Rémy from the view east of his window before
> sunrise, and from memory. The hilltop itself, its grass, path, bushes and
> flowers, the rock he sits on, the lantern on its post, the blanket, the clouds
> and mist round the rim, his sketchbook and the torn page were made for this
> visit; they are not in the painting. In the visit the visitor turns the
> lantern down, wakes the stars he names, follows the wind across the sky, and
> stays until dawn.

## In the painting (7)

| ID | What it is | Called | In the painting | Vincent knows |
|---|---|---|---|---|
| `Stars` | The stars | star, stars, halo | yes | He painted the stars as whirling rings of light, far bigger than they look, each in its own halo, to show how alive the night felt to him. |
| `MorningStar` | The big bright star just right of the cypress | morning star, venus, brightest star, big star | yes | Often read as the morning star, Venus. He wrote of seeing the countryside from his window before sunrise with nothing but the morning star, which looked very big. [I: the Venus reading and his letter, paraphrased; check] |
| `Moon` | The crescent moon | moon, crescent | yes | A crescent moon in a blazing orange-yellow halo, top right, as bright as a sun: he painted the night full of light rather than dark. |
| `Swirl` | The great swirl across the sky | swirl, swirls, wind, sky, spiral | yes | The swirl through the middle of the sky: the wind, or the night itself, moving. He let the strokes curl and flow so the sky feels alive while the village below sleeps. |
| `Cypress` | The cypress | cypress, tree | yes | The cypress rises in front like a dark flame, joining the earth to the sky. He loved cypresses for their lines and their deep green, and said they were as beautiful as an Egyptian obelisk. [I: the obelisk remark is from a June 1889 letter, paraphrased; check] |
| `Village` | The village with its church below | village, church, spire, steeple, house, houses, building, buildings, town, windows, lights | yes | The quiet village with its lit windows and a pointed church spire, calm and small under the moving sky. The spire looks more like a church of his northern homeland than one from Provence. [I: the northern-church reading is interpretation] |
| `Hills` | The far hills behind the village | mountains, hills, far hills, alpilles, horizon | yes | The rolling hills behind the village, the Alpilles, painted with the same rhythm as the sky. [I] |

## Made for this visit, not in the painting (8)

| ID | What it is | Called | In the painting | Vincent knows |
|---|---|---|---|---|
| `Hilltop` | The grassy hilltop, its path, bushes and flowers | hilltop, grass, path, bush, bushes, flowers, this hill | no | Made for this visit, so the visitor can sit where the view is. In the painting the near ground is only the cypress and dark slopes. |
| `Rock` | The rock he sits on | rock, stone, boulder | no | The rock he sits on for this visit, made for it. |
| `Lantern` | The lantern on its post | lantern, lamp, post | no | A lantern on an iron post, made for this visit: the visitor turns it down so the stars can come out. |
| `Blanket` | The striped blanket on the grass | blanket, seat | no | A striped blanket in the painting's colours that marks where the visitor sits, made for this visit. |
| `Clouds` | The clouds and mist round the hill | cloud, clouds, mist, fog | no | The clouds and mist round the rim of the hill were made for this visit, so the world seems to fall away. |
| `Sketchbook` | His sketchbook | sketchbook, sketch, drawing, book | no | In this visit it is his sketchbook, open to a sketch of this view. He says the hills in it are right, but the sky is not yet. |
| `Page` | The torn page he leaves on the rock | page, paper, gift | no | A torn page with his sketch, which he leaves for the visitor at dawn. |
| `Charcoal` | The charcoal in his hand | charcoal, pencil | no | The stick of charcoal he holds, for sketching the view. |

## Not covered

- Vincent himself, the scene's story steps and your score: the story's own
  lines (`Art/Source/StarryNight/lines.txt`) cover them. `SceneContext` (the
  current step) still isn't sent to Convai.
- Holding the sketchbook, the page or the lantern doesn't count as "this": name
  it.
