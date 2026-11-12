# Integration review — 2026-11-11

Three independent review angles: code/state behavior; asset GUIDs/shared settings; public-repository contents and evidence. Findings below were traced against the actual files.

| Finding | Verdict | Evidence / resolution |
|---|---|---|
| B reminder replaces activity instructions | Confirmed, fixed | SN_Conversation now keeps instructions above a separate B row; 39 conversation assertions include actual row placement and width for seven story states. |
| Conversation reconstructs generic Dawn title instead of restoring the page title | Confirmed, fixed | VG_DialogueUI exposes HintTitle; bridge stores/restores exact title and line; regression assertions cover page title and hidden hint. |
| Concatenated reminder might exceed hint width | Resolved by measured layout | VG_Hint supports an instruction row above its button row; per-state plate width checks pass. Existing single-line UI suite passes. |
| Missing/duplicate asset GUIDs | Refuted | Static scan: no duplicate GUIDs, no missing/orphan metas among copied files, changed serialized references resolve to project, pinned packages or built-ins. |
| Gallery changed to an older design | Refuted | Scene SHA256 is 941a43ece88ee2c3442d39d7013075c94531c39dd9c114c3319e1b394816b0e9, identical to approved GalleryIntegration. |
| XR setup duplicated or guardrails changed | Refuted | Packages/XR setup unchanged; one XR Origin per Entrance/Gallery; min API 32, GameActivity and Meta Quest Support remain set. |
| Credentials or caches included | Refuted | PhotonAppSettings and Convai credentials excluded; no Library, Temp, APK, ZIP or private desktop recording staged. |
| Old card docs claim no GitHub integration/shared setting change | Confirmed, fixed | Current README links integration; historical card verification labelled historical; both shared settings described. |

Unity 6000.3.23f1 on the upload checkout, starting with no Library: import/compilation completed. Final runs: 157 Starry Editor assertions, 22 menu assertions, 39 conversation assertions, all passed (218 total). The extra batch Play suites were explicitly skipped with -quit; the separately recorded manual Gallery/Starry/menu round trip was performed in the delivered project before the additional hint correction. Live AI replies, multiplayer and headset/controller behavior remain unverified.

No unresolved confirmed blocker for this draft branch. Headset validation and agreement on overlap/merge order remain before any merge. See overlap.txt. No Quest deployment or merge was performed.
