# SCUMMexplorer

Explore SCUMM games through their rooms, objects, characters and sounds. The goal is to click through the world, try its interactions and discover its secrets, with script evidence available when wanted.

**Current scope: DOS LOOM EGA exploration, plus Indiana Jones and the Last Crusade EGA room-background previews.** Indy object art, scripts, sounds, and the VGA edition are not yet supported.

Start with the [LOOM EGA plan and progress tracker](docs/LOOM_EGA_PLAN.md) for the vision, assessment, priorities and next bounded work session.

## What you can do today

- Choose a scene by its original artwork in the visual room picker. Room numbers stay in tooltips or Advanced mode. Exit outlines are off until requested; hovering still reveals a supported object or possible exit.
- Click scene details to try decoded actions. Short scene feedback keeps the result in view, while unresolved script state stays clearly labelled. The original room music controls sit beneath the artwork.
- Open **Advanced** for the raw resource tree, script and state traces, hotspot lists, input assumptions, depth and walk-box overlays, and resource links. This keeps SCUMM terms and IDs available without making them the first view.
- Open a DOS LOOM EGA folder and browse room backgrounds, local object artwork and depth masks. The app remembers the last successfully opened folder.
- View the original framing, scaled to use the available room area. Wider rooms scroll with the left/right keys while the room is focused, the buttons below it, or the horizontal scrollbar. Taps move two source pixels; holding a key or button gradually accelerates. Turn off **Original framing** to see the full panorama. Room music controls sit centered at the bottom beneath the graphic, and **Show depth areas** stays selected when browsing other rooms in Advanced mode.
- Open an Indiana Jones and the Last Crusade EGA folder and preview indexed room backgrounds. Object art, scripts, sounds, and gameplay routes have not been decoded for Indy yet.
- See each room without black gaps. Images are painted in SCUMM's order (room directory redraw, then `drawObject` calls on top), and gaps that the partial script state leaves open are filled from the game's own script draw batches, preferring the frame a script draws right after loading the room, or else a seeded random stored image. Every fill is labelled as an estimate.
- Click room artwork to preview stored alternatives, including restoring hidden artwork at the same position. Showing an image refills the cut-outs inside it (for example a close-up face's eyes and mouth); hiding is offered only when the background behind has no gap. These changes affect the displayed view, not persistent game state.
- Click decoded object verbs directly on room artwork, including code-only hotspots. The Advanced hotspot list also provides keyboard access. A reached room-load instruction opens its destination; a script waiting on unresolved state stays pending. Statically discovered doorways remain possible destinations, and an exit outline can be enabled when wanted.
- Keep known script state across room visits, reset the session or advance a paused frame. Asset inspection is also available separately.
- Play indexed sounds and room cue candidates with PC speaker or AdLib music playback. Room playback is on demand. Sound effects currently use PC speaker; AdLib effects and MT-32 remain future work.
- Browse extracted dialogue, object names and stored text in the searchable **Game Texts** gallery. Room-linked entries show their original room artwork and can take you back to that room. Object-linked entries identify their source object where the decoded data establishes it. Empty, single-character and symbol-only entries are omitted. The **Conversations and cutscenes** view gathers spoken lines in source order, distinguishing decoded cutscene marker blocks from other script dialogue; source details remain available on demand. These groups are partial transcripts: conditional branches, script calls and timing do not yet establish a complete conversation as played.
- Inspect all decoded static costume poses together at a shared scale, bounded script disassembly and resource connections. Stored poses do not yet establish a timed animation.

The explorer is still partial. The text gallery decodes stored strings from indexed scripts, room scripts and object verbs, including DOS accents and visible control markers. Text on unsupported or unvisited script paths may be missing, and storage order does not establish cutscene order. In exploration mode, code-only hotspots take precedence where they overlap artwork; inspection mode keeps artwork cycling. Draft selection, actor walking and timed cutscenes are not yet a complete gameplay experience. Unknown script state is covered by labelled artwork estimates; these are previews, not verified first-visit states. Static script paths and possible music cues do not establish what actually occurs in a playthrough.

Stored text extraction also covers Indiana Jones and the Last Crusade EGA, whose scripts use a slightly different opcode dialect (`wait`, `waitForActor` and `getActorX/Y` operands). Its multiple-choice dialogue labels are grouped as **Dialogue choices**, each followed by the replies spoken in its selection branch. This is a bounded text preview, not Indy gameplay execution.

## Documentation

| Document | Purpose |
|---|---|
| [LOOM EGA plan](docs/LOOM_EGA_PLAN.md) | Current product direction, priorities and milestones |
| [Script map](docs/LOOM_SCRIPT_MAP.md) | Decoded scripts, variables, object verbs and rooms |
| [Changelog](docs/CHANGELOG.md) | Finished work with links to its validation |
| [Implementation audit](docs/EXPLORATION_AUDIT.md) | Existing capabilities, limitations and historical verification results |
| [Object-state reference](docs/SCUMM_OBJECT_STATES.md) | Format evidence, rendering rules and preview semantics |
| [Script coverage](docs/LOOM_SCRIPT_COVERAGE.md) | Measured decoder coverage; not gameplay coverage |
| [Research](docs/RESEARCH.md) | Technical references and prior research |

## Development

Windows WPF application targeting .NET 10. `SCUMMexplorer.Core` owns parsing, rendering and bounded execution; `SCUMMexplorer.App` owns the desktop UI; `SCUMMexplorer.Tests` contains focused Core tests. Game data stays local in the game folder you open.

From the repository root, with the .NET 10 SDK and RTK available:

```powershell
rtk proxy dotnet build SCUMMexplorer.slnx
```

The Debug executable is `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. Audio regression tests can use `SCUMM_SOUND_TEST_ROOT` to locate a supplied DOS LOOM folder. Indy EGA compatibility tests can use `SCUMM_INDY3_EGA_TEST_ROOT` to locate that edition's folder.

## Audio and third-party notices

AdLib rendering uses the separate [NukedOPL3Sharp](https://www.nuget.org/packages/NukedOPL3Sharp/) library, licensed under LGPL 2.1; [source and license](https://github.com/codengine/NukedOPL3Sharp) are available from its author. PC speaker rendering follows the SCUMM driver's envelopes and pitch modulation, with a filtered square wave integrated over each audio sample. See [third-party audio notices](THIRD_PARTY_NOTICES.md) for ScummVM references and licenses.
