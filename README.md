# SCUMMexplorer

An audio-visual, interactive explorer for classic SCUMM adventures. Walk through the rooms in any order, click objects and exits, replay the cutscenes, hear the original music and read the games' texts, all rebuilt from your own game files. It is not meant as a technical SCUMM viewer: the script evidence is there when you want it (Advanced mode), not in your way.

**Current scope.** DOS **LOOM EGA** is the main focus. The English release is the reference edition (the Spanish release also opens). The two LOOM self-running previews (`LOOMDEMO.EXE`, short and long) open too. **Indiana Jones and the Last Crusade EGA** is supported only for room backgrounds and texts, including its dialogue choices. Indy object art, scripts, sounds, the VGA edition and the other SCUMM games are not supported yet.

Game files are not included and never committed: you open a folder you own.

## What you can do today (LOOM EGA)

- **Pick a scene by its artwork** in the visual room picker, and see each room without black gaps. Room numbers and exit outlines are available on request.
- **Click the world.** A click walks Bobbin to an object and uses it through the game's own scripts; a double click also looks at it; exits lead on to the next room. Bobbin, the other actors and their costumes are drawn and animated, with the camera following, and room animations keep running as in the game.
- **Cutscenes and drafts play.** About 335 cutscene starts (generated from the game's own code, plus a few hand-built sequences) can be launched from **Play Cutscene**, each with a start setup derived from the scene's code; drafts and the staff go through the game's scripts. **Game Status** lets you set the staff, notes, learned drafts and story events yourself. Every assumption a start makes is shown in the picker's note.
- **Hear it.** Music and effects through AdLib (NukedOPL3Sharp), PC speaker, or No Sound; sounds and a room's music cues can be played on demand.
- **Read the games' texts** in the searchable **Game Texts** gallery (Advanced): extracted dialogue, object names and stored text with the original room artwork, and links that play the cutscene a text belongs to.
- **Open a LOOM demo.** The rooms are explorable like the full game's, and **The preview** (in Play Cutscene) plays the demo's own self-running tour until you open another room. A demo is another build of the game, not a subset: its scripts, rooms and index differ, so full-game names and scene catalogs are not applied to it.
- **Advanced mode** shows the raw resource tree, readable and raw script views with a glossary, the last action's timeline, hotspot lists, depth and walk-box overlays, and resource links.

Everything is labelled by how well the game's data supports it: a decoded branch is a *possible* outcome, calling a verb directly is an *exploration action*, a partial session is a *state preview*, and artwork the scripts leave undecided is filled with a labelled estimate that never changes the game state.

For Indiana Jones and the Last Crusade EGA, the app previews the indexed room backgrounds and extracts stored text. Its scripts use a slightly different opcode dialect (`wait`, `waitForActor` and `getActorX/Y` operands), and its multiple-choice dialogue labels are grouped as **Dialogue choices**, each followed by the replies spoken in its branch. This is a bounded text preview, not Indy gameplay. The standalone conversation map is generated from an EGA folder with `dotnet run --project tools/Indy3ConversationExport/Indy3ConversationExport.csproj -- "Games/indy3 ega en"`; it expands the decoded menus into collapsible, source-linked branches with the room backgrounds, and writes a `.graph.json` audit beside the HTML. Conditional state is left unevaluated and the map does not assign outcomes.

## Documentation

| Document | Purpose |
|---|---|
| [LOOM EGA plan](docs/LOOM_EGA_PLAN.md) | Product direction, state, open items and milestones |
| [Current work](docs/CURRENT_WORK.md) | The rules set for the project |
| [Next session](docs/NEXT_SESSION_PROMPT.md) | Hand-off: state, ordered tasks, tooling notes |
| [Changelog](docs/CHANGELOG.md) | Finished work with links to its validation |
| [Script map](docs/loom/LOOM_SCRIPT_MAP.md) | Decoded scripts, variables, object verbs and rooms |
| [English edition compatibility](docs/loom/ENGLISH_COMPAT_2026-10-04.md) | What differs between the English and Spanish data |
| [Cutscene discovery](docs/loom/LOOM_CUTSCENE_DISCOVERY.md) | How the cutscene catalog is generated and audited |
| [Object-state reference](docs/engine/SCUMM_OBJECT_STATES.md) | Format evidence, rendering rules and preview semantics |
| [Script coverage](docs/archive/LOOM_SCRIPT_COVERAGE.md) | Measured decoder coverage; not gameplay coverage |
| [Harness review](docs/archive/HARNESS_REVIEW_2026-10-04.md) | Which verification tools are kept and why |
| [Research](docs/engine/RESEARCH.md) | Technical references and prior research |

## Development

Windows WPF application targeting .NET 10. `SCUMMexplorer.Core` owns parsing, rendering and bounded script execution; `SCUMMexplorer.App` owns the desktop UI; `SCUMMexplorer.Tests` holds the Core tests; `.verification/` holds UI harnesses and evidence (see its README). Game data stays local in the folder you open.

From the repository root, with the .NET 10 SDK:

```powershell
dotnet build SCUMMexplorer.slnx
$env:SCUMM_SOUND_TEST_ROOT = "$PWD\Games\loom ega en"
$env:SCUMM_INDY3_EGA_TEST_ROOT = "$PWD\Games\indy3 ega en"
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build
```

The Debug executable is `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. The LOOM tests find `Games/loom ega en` (then `loom ega es`) next to the repository, or the folder in `SCUMM_LOOM_TEST_ROOT`; without `SCUMM_INDY3_EGA_TEST_ROOT` the Indy tests skip. Tests that need the demo folders skip when they are absent.

## Audio and third-party notices

AdLib rendering uses the separate [NukedOPL3Sharp](https://www.nuget.org/packages/NukedOPL3Sharp/) library, licensed under LGPL 2.1; [source and license](https://github.com/codengine/NukedOPL3Sharp) are available from its author. PC speaker rendering follows the SCUMM driver's envelopes and pitch modulation, with a filtered square wave integrated over each audio sample. See [third-party audio notices](THIRD_PARTY_NOTICES.md) for ScummVM references and licenses.
