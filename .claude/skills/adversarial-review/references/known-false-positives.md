# Known false positives: read BEFORE reproducing a finding

Each row is something that looks like a defect in InnerFrame but isn't. A match
shifts the burden: the finder has to show why *this* instance differs. Add a
row whenever a review refutes something that will plausibly be raised again,
and say which PR or session proved it benign.

| Observation | Why it's not a bug | Seen in |
|---|---|---|
| A scene, prefab or `.asset` diff is tens of thousands of lines | Unity YAML is big by nature. Measure by folder and by commit; the real signal is folders nothing references, not line counts. | PR #7 review |
| The same `.meta` GUIDs under `Assets/Samples/XR Interaction Toolkit/...` on two branches | The package ships those GUIDs; every import gets the same ones. Different GUIDs for the same sample path would be the finding. | #5 vs #7 |
| A sample binary (`.fbx`, `.png`) differs between two branches | One side is an LFS pointer, the other the raw file. The raw side is the finding (C3); the diff itself isn't. | PR #7: 56 raw sample and texture files |
| `_XRMotionVectorsPass` toggled in a sample `.mat` | Unity's URP material upgrade on import flips it. Not hand-edited YAML. | PR #5 sample import |
| `ProjectSettings/SceneTemplateSettings.json` appears in a diff | Unity generates it when the Scene Templates window opens. Noise, unless the PR body claims it. | PR #5 "re-serialization noise" |
| Two editor classes named `...V2` and `...V3` "collide" | C# classes collide only on the same name in the same namespace. Different names don't. The `_V2`/`_V3` *folders* are still an AGENTS.md rule violation. | PR #7 style-experiment folders |
| C9 `not-in-assets-doc` warnings on a PR | Warnings never fail CI. On a branch based on a `main` from before #5 there is no `docs/ASSETS.md` at all, so every folder warns; that's the stale branch point, not 16 missing rows. | PR #7 checked with #5's checker |
