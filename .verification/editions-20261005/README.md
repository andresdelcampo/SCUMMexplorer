# LOOM EGA editions, first pass (5 October 2026)

Reference: `Games/loom ega mt32 en`. Tried: `loom ega en`, `es`, `fr`, `de`, `hebrew`, `v1.2 es`, `v1.2 de` (all 74 `.LFL` files, no 82–85).

Run with `GlossaryCheck <edition> <reference> <out>` (`glossary-*.md`) and `RoomAudit <edition> <out>` (room BMPs not committed).

| Check | Result |
|---|---|
| Folder opens and is identified as LOOM | all seven |
| `RoomAudit`: first-visit rooms, remaining gaps | 0 gaps after filling in every edition (3 before filling, as in the reference) |
| Glossary: same / moved / absent | 75–76 same, 32–33 moved, 0 absent in every edition |

Not yet checked: sounds degrading cleanly without files 82–85 in the real window, text extraction and the Game Texts window per language, the Hebrew copy's text and font handling (right-to-left or patched?), what v1.2 changed against the plain editions, the cutscene catalog on each edition.

## Second pass (5 October)

- **Sounds without files 82–85** (plain `loom ega en`): `SoundPanelCheck` shows sound 24 as "No playable audio" and sound 28 plays as AdLib; the app only offers the Roland device when `SceneSoundPlayer.HasRolandSounds` finds the files (`MainWindow.xaml.cs` lines 232 and 2598), so nothing breaks. Record: `sound-plain-en/`.
- **Texts** (`TextGalleryCheck`, plain en / fr / hebrew): 1245 / 1225 / 1224 entries, 2 unresolved (Room 70 local scripts 203 and 204, truncated operands, same in all), object names 133 each. The harness's last check, "Search narrows to matching original text", fails in all three; it probably searches for an English word and was not run against the reference this time, so unexamined. Records: `texts-*/`.
- Still open: whether the Hebrew strings are real Hebrew and how the font draws them; v1.2 differences; cutscene catalog per edition.
