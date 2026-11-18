# What Vincent knows in Almond Studio

Almond Studio (Act 4, owner: Serena) is built from *Window in the Studio* (his
picture of his studio window at the asylum, autumn 1889), with the rest of the
room, the almond tree outside and the story made for the visit. As in Starry
Night, Vincent answers **questions** here (hold B); he doesn't comment on his
own when you take something: the story's lines already cover the letter, the
window and the brush.

When your question names something below ("who painted the plane trees?"), its
notes go with it to Convai. For what is in a painting of his he answers as the
painter; for what we added he is told it isn't in his painting and not to say
he painted it (`VincentObjectTalk.NotPainted`). The room paragraph goes with
your first question. Every question also carries **where the story is**
(`AS_Game.StoryNow`: the letter not yet read, the news, the window open, the
painting, the end), so "what should we paint?" makes sense to him.

"In the painting" here means *a painting of his*: the window, the sill and the
chest from *Window in the Studio*; the almond tree, which becomes *Almond
Blossom* (in the visit he hasn't painted it yet); and his four Saint-Rémy
canvases hung on the side walls, which are real works of his even though their
place on these walls is ours.

The shipped text is `Assets/VanGogh/Resources/VincentNotes/AlmondStudio.json`;
this list says the same. **Called** is the words a question can use (plurals
count).

Status: draft, 16 Nov 2026, from the scene's layout (`AS_Layout`), the Act 4
research notes (Theo's Letter 847, Vincent's reply, Letter 850) and the
museums' captions for the canvases. Not yet checked line by line against
vangoghletters.org and the museum pages: treat the art-history lines as [I].

## The scene (sent with the first question)

> The visitor is with Vincent in his studio at the asylum of Saint-Paul-de-
> Mausole in Saint-Rémy, on a bright morning in February 1890. The room is built
> from his picture of this studio's window, made in the autumn of 1889: the tall
> window with its round-arched top, radiating fanlight and two casements of
> small panes in a dark green frame, the deep reveal, the bottles, jar, glass
> and cup on the sill, the pictures pinned on the wall beside it, the low chest
> with boxes in the corner, and the red-orange ochre walls. The rest of the room
> was arranged for this visit: the table with two rush chairs, his easel, the
> paint box and brushes, a brush standing in the glass on the sill, the canvases
> and the Japanese print on the side walls, the door, Theo's letter and the
> birth card. Outside the window an almond tree is coming into blossom against a
> turquoise sky. In the visit, Theo's letter has just come: Jo has given birth
> to a son, named Vincent Willem after him. The visitor reads it to him, opens
> the window, takes the brush and wakes the almond tree into blossom; then the
> painting Almond Blossom appears on his easel.

## A painting of his (9)

| ID | What it is | Called | In the painting | Vincent knows |
|---|---|---|---|---|
| `Window` | The arched window of his studio | window, casement, fanlight, pane, arch, bars | yes | The window of his studio at the asylum, which he drew and painted in the autumn of 1889: a tall window with a round-arched top and a fanlight of radiating bars, two casements of small panes in a dark green frame, set in a deep reveal. He looked at the window itself, not only the view through it. |
| `SillStillLife` | The bottles, jar, glass and cup on the windowsill | bottle, jar, glass, cup, sill, still life | yes | The everyday things standing on the wide sill: a tall jar, a small bottle, a tall bottle, a glass and a cup, in his picture of the window. In this visit his brush stands in the glass. |
| `WallPictures` | The pictures pinned on the wall either side of the window | pictures, studies, drawing, drawings, sketches, wall | yes | Studies and drawings pinned up beside the window, as they are in his picture of the studio: work in progress around him, with no frames. |
| `Chest` | The low chest with boxes in the corner | chest, boxes, box, trunk | yes | The low chest with boxes stacked on it, at the lower right of his picture of the studio. |
| `AlmondTree` | The almond tree outside the window | almond, tree, blossom, branch, branches, flowers, petals, buds, sky | yes | Almond trees flower early in Provence, in February, among the first trees to blossom. In February 1890 he painted Almond Blossom: branches of white blossom against a blue sky, as a gift for his newborn nephew, to hang in Theo and Jo's bedroom; the branches are seen from below and cut by the edges, as in the Japanese prints he loved. In this visit he hasn't painted it yet: the tree outside is just coming into blossom, and he is deciding to paint it for the baby. |
| `PlaneTrees` | The canvas of the large plane trees | plane trees, plane tree, road menders, road, workmen | yes | His painting The Large Plane Trees, of men mending the road under huge plane trees on the main street of Saint-Rémy, from December 1889. It hangs here for this visit. |
| `WheatField` | The canvas of a wheat field with cypresses | wheat field, wheat, field, corn | yes | His painting Wheat Field with Cypresses, from 1889 in Saint-Rémy: ripe wheat, dark cypresses and swirling white clouds. It hangs here for this visit. |
| `Cypresses` | The canvas of cypresses | cypress, cypresses | yes | His painting Cypresses, from June 1889 in Saint-Rémy: two dark cypresses rising like flames. He loved their lines and their deep green. It hangs here for this visit. |
| `FirstSteps` | The canvas of a child's first steps, after Millet | first steps, millet, child, baby, father, mother | yes | His painting First Steps, after Millet, from January 1890: a father in his garden kneels with open arms as the mother holds up their child to walk to him. He painted copies after Millet's work at the asylum. It hangs here for this visit. |

## Made for this visit, not in a painting of his (8)

| ID | What it is | Called | In the painting | Vincent knows |
|---|---|---|---|---|
| `Letter` | Theo's letter | letter, theo's letter, news | no | Theo's letter from Paris of 31 January 1890: Jo has given birth to a boy, and they have named him Vincent Willem after Vincent; Theo hopes the boy will be as determined and as courageous as he is. It also tells him that Aurier has written about his work in the Mercure de France, and that his canvases are hanging in Brussels with Les Vingt. In his reply he asked why they had not named the boy after their father. |
| `BirthCard` | The printed birth card on the table | card, birth card, announcement | no | The printed card announcing the birth of Vincent Willem van Gogh in Paris, which came with the news. |
| `Brush` | The brush in the glass on the sill | brush, paintbrush | no | His brush, standing in the glass on the sill, for the visitor to take and paint the tree into blossom. |
| `Easel` | His easel | easel | no | His easel, by the door. At the end of the visit Almond Blossom appears on it. |
| `Table` | The table with two rush-seated chairs | table, chair, chairs | no | A plain wooden table and two rush-seated chairs, arranged for this visit. |
| `PaintBox` | His paint box and brushes on the table | paint box, paints, tubes, brush pot | no | His paint box with its tubes of colour, and his brushes, arranged on the table for this visit. |
| `Print` | The Japanese print of a courtesan with a letter | print, japanese, courtesan, eisen, woodblock | no | A Japanese woodblock print by Keisai Eisen of a courtesan with a letter, hung here for this visit; there is no record of a Japanese print in this studio. He loved and collected Japanese prints, and in Paris in 1887 he had painted a courtesan after Eisen. |
| `Door` | The door of the studio | door | no | The studio door, which stays shut in this visit. |

## Not covered

- Vincent himself and the story steps: the story's lines (`AS_Layout`) and the
  story sentence sent with each question cover them.
- Holding the brush or the letter doesn't count as "this": name it.
