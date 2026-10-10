# SCUMMexplorer

*Remember walking into the Guild of Glassmakers for the first time, or hunting for the one note that opens the sky?*

SCUMMexplorer lets you wander the old SCUMM adventures from the inside. Pick any room from its artwork, click objects and exits, watch cutscenes again, hear the music, read what everyone says, and see how the game is put together when you want to know. Everything is rebuilt live from **your own game files**. Nothing is included, nothing is copied.

## Games

- **LOOM** (EGA, DOS), the main focus, in these editions:
  - English, with or without the Roland (MT-32) upgrade
  - Spanish, with or without the Roland upgrade, and version 1.2
  - French
  - German
  - Italian
  - the short and the long self-running demo
- **LOOM** (CD, VGA, English), from a folder, a `.cue`/`.bin` or an `.iso`, with the voices and music from the disc

Point the app at a game folder and it recognises the game and the edition from the files themselves, even if the executable is missing. Other classic SCUMM games of the same generation open in a limited way: you get their room pictures and their texts.

## Getting started

1. Build and run the app (Windows, .NET 10; see *Development* below).
2. Press **Open Game Folder** and choose a folder you own. The app reopens it next time.
3. Pick a room and explore.

## For everyone: just play with it

- **Choose a room by its picture.** Every room is shown as the game first showed it, with no gaps. Wide rooms scroll with the arrow keys.
- **Click the world.** Bobbin walks to what you click and uses it, exactly as the game's own scripts decide. Double-click to look at something. Exits lead on to the next room. The other characters are there too, walking and talking, and the room's own animations keep running.
- **Cast drafts.** Play the distaff with the keyboard or the mouse, as in the game.
- **Play a cutscene.** Any room with a scene offers **Play Cutscene**; pause it, skip it, or watch it through to the end.
- **Change the story.** **Game Status** lets you pick up the staff, set how many notes you know, learn any draft, and mark scenes as done or undone, then walk on from there.
- **Listen.** Music and effects through AdLib, PC Speaker or the Roland MT-32 (see below); the CD edition plays its voices and its disc music. The choice is remembered.
- **Look closer.** Show doorways and hotspots on hover, switch to the original framing or fill the window, or go full screen (F11).

## For the curious: Advanced mode

Switch **Advanced** on, and the explorer opens up. Everything here reads from the game's own data, and every guess is labelled as a guess.

- **The whole game as a tree.** Rooms, costumes, scripts, sounds and music, and the game files, each with the names the community knows them by and where the name comes from.
- **Every script, two ways.** The decoded instructions, or a plain-language reading with names, conditions and branches written out. A "used by" index shows who calls what.
- **A cutscene picker for everything.** Search by title and filter by scenes, dialogue and reactions, or all playable. Each entry says how its start was set up, and anything the app had to assume is spelled out.
- **Game Texts.** Every spoken line, object name, verb label and dialogue menu, searchable, shown with the room's artwork. Read a conversation, visit its room, or play the cutscene it belongs to.
- **What just happened?** After each click: the game's reaction, a timeline of the scripts it ran, the raw trace, and the assumptions behind it.
- **Room inspection.** Doorway and hotspot lists, walk boxes, depth areas, the room's entry script, and an inspection mode where a click cycles an object's stored pictures without running any script.
- **Step and pause.** Advance a scene frame by frame.
- **Restart the story** from the very beginning in one click.
- **A sound player for every sound.** Play any music or effect on demand, with an AdLib level slider and the room's music cues. On the CD edition, browse every voice and music track on the disc.
- **Game Status in depth.** The raw story bits, where each value comes from, and every draft with its notes, flags and reversing.

## Roland MT-32 music (optional)

Choose **Roland MT-32** in the sound menu to hear the LOOM Roland upgrade's music, including the overture that only plays on this card. It needs two things that are not part of this program:

1. **A game folder with the Roland upgrade installed** (it has `82.LFL` to `85.LFL`).
2. **Your own MT-32 ROM images**, in one folder: `MT32_CONTROL.ROM` and `MT32_PCM.ROM`, or `CM32L_CONTROL.ROM` and `CM32L_PCM.ROM`. They are copyrighted and never shipped.

The app looks for that folder in this order: the `SCUMM_MT32_ROM_ROOT` environment variable, the folder you chose last time, then a folder named `mt32` beside the game folder. If it finds none it asks once and remembers.

## Development

Windows WPF application targeting .NET 10. From the repository root:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build
```

The Debug executable is `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. All development shell commands use RTK per the repository instructions.

| Document | Purpose |
|---|---|
| [Requirements](docs/REQUIREMENTS.md) | What the app must do |
| [Current work](docs/CURRENT_WORK.md) | State, priorities and session handoff |
| [Changelog](docs/CHANGELOG.md) | Finished work with its validation |
| [Script map](docs/loom/LOOM_SCRIPT_MAP.md) | Decoded scripts, variables, verbs and rooms |
| [Research](docs/engine/RESEARCH.md) | Technical references |

## License

SCUMMexplorer is licensed under the GNU General Public License, version 3 or later (see [LICENSE](LICENSE)). It adapts parts of ScummVM, which is GPL; game files and ROM images are never included.

## Audio and third-party notices

AdLib uses [NukedOPL3Sharp](https://www.nuget.org/packages/NukedOPL3Sharp/) (LGPL 2.1). The Roland MT-32 uses [Munt](https://github.com/munt/munt) (`mt32emu`, LGPL-2.1-or-later) through [Mt32emu.net](https://github.com/gregdivis/mt32emu.net). See [third-party audio notices](THIRD_PARTY_NOTICES.md).
