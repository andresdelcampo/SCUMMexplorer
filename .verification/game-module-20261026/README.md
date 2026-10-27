# IGameModule and LOOM file separation (26 October 2026)

`Check` opens nine games (LOOM EGA reference and Spanish, LOOM demo, LOOM CD, Indy 3 EGA, Monkey Island floppy, MI2, Maniac Enhanced, Zak) through the real
`MainWindow.OpenGameFolder` and prints, per game, the global cutscene picker (count, first and last titles), the Game Texts cutscenes and the replay catalog size.
Run on the commit before the change and on the changed tree, the two reports are identical:

```
loom ega mt32 en: family=LoomEgaReference picker=383 first=[The Intro; Room 10 · Walk to Room 13; Room 08 · The Elders' trial] last=[Room 62 local 204] gallery=38 replays=328
loom ega es: family=LoomEga picker=220 first=[The Intro; Room 10 · Walk to Room 13; Room 08 · The Elders' trial] last=[Room 66 · Other draft response at Bobbin] gallery=4 replays=0
loom demo short ega en: family=LoomEgaDemo picker=1 first=[The preview] last=[The preview] gallery=0 replays=0
loom vga en: family=LoomCd picker=224 first=[The Intro; Room 02 · Entry; Room 02 · Reached Room 03] last=[Room 93 · Other draft response at shepherds] gallery=0 replays=0
indy3 ega en: family=Indy3Ega picker=153 first=[Room 01 · Open door; Room 01 · Walk to door; Room 01 · Turn on drinking fountain] last=[Room 94 · Entry] gallery=0 replays=0
monkey1 ega en: family=MonkeyFloppy picker=164 first=[Room 01 · Entry; Room 01 · Look at bottle; Room 07 · Open dusty book] last=[Room 85 · Walk to Sword Master's] gallery=0 replays=0
mi2 talkie en: family=Monkey2 picker=260 first=[Room 04 · Entry; Room 04 · Use log; Room 04 · Look at sign] last=[Room 97 · Action parrot] gallery=0 replays=0
mmansionE ega es: family=ManiacEnhanced picker=27 first=[Room 01 · Da contrato; Room 01 · Ir a puerta delantera; Room 01 · Empuja timbre] last=[Room 51 · Apagar interruptor] gallery=0 replays=0
zak ega en: family=ZakClassic picker=54 first=[Room 01 · Read phone bill; Room 01 · Use telephone; Room 02 · Entry] last=[Room 44 · Action Unnamed object] gallery=0 replays=0
DONE
```

Run: `dotnet run --project .verification/game-module-20261026/Check -- "D:\Juegos\Lucas" <private output>`. It proves the lists and the open/reset lifecycle are unchanged; it does not exercise playing a cutscene or the user's saved state.

Names seam (same day): the check also prints each game's first room page, first script page (title, subtitle, SHA of the readable text) and the Used by text; the reports before and after the names change were identical for the nine games (Used by is empty in Normal mode).

Sound seam (same day): the check also prints the session's sound owner (scene sound or CD voice), the room Sounds button and the voices option; identical before and after on the nine games.

Status seam (same day): the check also prints the Game Status button state and tooltip; identical before and after on the nine games.

Tree and room-card seam (same day): the check also hashes the first four room cards' pixels and lists the Advanced tree groups; identical before and after on the nine games.
