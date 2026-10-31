# Readable scripts name what each game's data names (31 October 2026)

| | |
|---|---|
| Item | ROADMAP work item 3, feature 10 (readable scripts) |
| Fixtures | `D:\Juegos\Lucas`: `indy3 ega en`, `monkey1 ega en`, `monkey1 vga en`, `mi2 mt32 en`, `indy4 vga en` |
| Tool | `Count/` (`dotnet run -c Release -- <folder>...`): every global, entry, exit, local and object-verb script of the game in the readable view, before and after; `count-run1.txt`. Window: `../scene-setup-20261031/WindowCheck` step `s:<id>` |

Until now the readable view named only LOOM's things (its reviewed glossary); every other game showed `object 864` and `actor 5`. `LoomGlossary` (used for every game) now reads, for each game without reviewed names, what the game's own data names (`LoomGlossaryGameNames.cs`), the first time a name is asked for:

- **Objects**: each room's stored object name (`IScummDataSource.ReadRoom(room).ObjectNames`, as the scene titles already use), evidence "decoded".
- **Actors**: the name an `actorOps … actorName "…"` gives (Indy 3 global script 118 +0x0034: actor 1 "Indy"; Monkey Island 2 global script 1 +0x0969: "Guybrush"), scoped to the script or room that sets it, since actor numbers are reused. Where a script does not name the actor itself, a game-wide name is used when one name is given in at least two thirds of the scripts that name that actor; its evidence line says so ("in 6 of the 6 scripts …; numbers are reused"). This is a reading, not proof: Indy 3's Script 118 shows actor 5 as "drunk soldier" because most scripts that name actor 5 call it that.

## Results (references in the readable view of every script)

| Game | Scripts | Object references named | Actor references named |
|---|---|---|---|
| Indy 3 EGA | 1,571 | 0% → 51.3% (662 of 1,290) | 0% → 74.5% (2,113 of 2,835) |
| Monkey Island 1 floppy | 2,037 | 0% → 65.3% (1,371 of 2,101) | 0% → 39.0% (1,669 of 4,283) |
| Monkey Island 1 CD | 2,119 | 0% → 70.3% (1,458 of 2,074) | 0% → 39.2% (1,653 of 4,213) |
| Monkey Island 2 | 3,284 | 0% → 78.5% (2,081 of 2,651) | 0% → 50.1% (2,980 of 5,954) |
| Indy 4 | 4,120 | 0% → 84.8% (5,885 of 6,941) | 0% → 79.3% (11,635 of 14,675) |
| **Total** | 13,131 | **0% → 76.1%** (11,457 of 15,057) | **0% → 62.7%** (20,050 of 31,960) |

What stays unnamed: objects whose data stores no name (or an empty one), actors no script names with `actorName`, and every variable and bit (a game's own variables have no names in its data; LOOM's come from its reviewed glossary).

Real window (`WindowCheck s:118`, Indy 3 EGA, private profile): the script page's readable view shows "0034  Indy: costume 10; …", "Marcus", "Henry", "Elsa" ([picture](window/s-118.png)). Test: `GameNamesReadableTests`.

## Not done

- Variable and bit names for the other games: their script maps (`docs/<game>/*_SCRIPT_MAP.md`) would need reviewed rows turned into data, as LOOM's glossary is.
- Verb names in the readable view of the other games (their verbs are named in the session; the presenter takes a verb-name function only for LOOM).
