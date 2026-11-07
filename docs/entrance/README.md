# Two-character Entrance prototype

Based on Cici's Oct 29 build draft and the supplied interactive concept. This
branch adds `Assets/Entrance/` without changing Gallery or Bedroom scene YAML.
The Unity Editor menu `Tools > InnerFrame > Entrance > Build XR entrance scene`
imports the textures (ASTC Android override), creates
`Assets/Entrance/Scenes/Entrance.unity`, and puts it first in Build Settings.
The scene uses the existing XR Origin prefab, world-space UI and XR UI Input
Module. Use Unity 6000.3.23f1 and the Android build profile.

Flow: Begin → select Boy or Lady in Red → New Room or Join Room → four digit
code → stepped image intro → Gallery. Signs and digits are editable TMP labels
on the supplied wood plate/tile artwork. A four digit code names the Gallery
Fusion Shared session, `InnerFrame_Gallery_<code>`, with a two-player cap. Users
who enter Gallery directly keep the original session. The character selection
is stored for this app session; the current Gallery avatar does not yet switch
models. The current Fusion Shared startup may create an empty room for an
unknown Join code; this needs the multiplayer owners' decision.

Entrance B audio, UI click sounds, handwritten lettering Foley, gold controller ray and yellow rounded selection outline are included. The Quest 3 test on 2026-11-06 confirmed audible output with the diagnostic build. The diagnostic probe was excluded from this branch. Android build uses the Entrance scene first.
