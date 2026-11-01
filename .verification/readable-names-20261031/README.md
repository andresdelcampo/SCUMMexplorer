# Readable scripts name what each game's data names (31 October 2026)

| | |
|---|---|
| Item | ROADMAP work item 3, feature 10 (readable scripts) |
| Fixtures | `D:\Juegos\Lucas`: `indy3 ega en`, `monkey1 ega en`, `monkey1 vga en`, `mi2 mt32 en`, `indy4 vga en` |
| Tool | `Count/` (`dotnet run -c Release -- <folder>...`): every global, entry, exit, local and object-verb script of the game in the readable view, before and after; `count-run1.txt`. Window: `../scene-setup-20261031/WindowCheck` step `s:<id>` |

Until now the readable view named only LOOM's things (its reviewed glossary); every other game showed `object 864` and `actor 5`. `LoomGlossary` (used for every game) now reads, for each game without reviewed names, what the game's own data names (`LoomGlossaryGameNames.cs`), the first time a name is asked for:

- **Objects**: each room's stored object name (`IScummDataSource.ReadRoom(room).ObjectNames`, as the scene titles already use), evidence "decoded".
- **Actors**: the name an `actorOps … actorName "…"` gives (Indy 3 global script 118 +0x0034: actor 1 "Indy"; Monkey Island 2 global script 1 +0x0969: "Guybrush"), scoped to the script or room that sets it, since actor numbers are reused. Where a script does not name the actor itself, a game-wide name is used when one name is given in at least two thirds of the scripts that name that actor; its evidence line says so ("in 6 of the 6 scripts …; numbers are reused"). This is a reading, not proof: Indy 3's Script 118 shows actor 5 as "drunk soldier" because most scripts that name actor 5 call it that.
- **Verbs** (second step, same day): the name a `verbOps N, [… name "…"]` gives, when one name is given in at least two thirds of the scripts that name verb N (Indy 3: 0x01 "Open" … 0x0B "Pick up", 0x0D "Talk"; the dialogue choices 0x78-0x7B, renamed for every line, stay unnamed). The default game module passes them to the readable view (`GameNames`), where `startObject` and `doSentence` show them; LOOM keeps its own verb names.

## Results (references in the readable view of every script)

| Game | Scripts | Object references named | Actor references named | Verb references named |
|---|---|---|---|---|
| Indy 3 EGA | 1,571 | 0% → 51.3% (662 of 1,290) | 0% → 74.5% (2,113 of 2,835) | 0% → 57.0% (57 of 100) |
| Monkey Island 1 floppy | 2,037 | 0% → 65.3% (1,371 of 2,101) | 0% → 39.0% (1,669 of 4,283) | 0% → 43.5% (73 of 168) |
| Monkey Island 1 CD | 2,119 | 0% → 70.3% (1,458 of 2,074) | 0% → 39.2% (1,653 of 4,213) | 0% → 29.2% (49 of 168) |
| Monkey Island 2 | 3,284 | 0% → 78.5% (2,081 of 2,651) | 0% → 50.1% (2,980 of 5,954) | 0% → 60.9% (131 of 215) |
| Indy 4 | 4,120 | 0% → 84.8% (5,885 of 6,941) | 0% → 79.3% (11,635 of 14,675) | 0% → 79.6% (207 of 260) |
| **Total** | 13,131 | **0% → 76.1%** (11,457 of 15,057) | **0% → 62.7%** (20,050 of 31,960) | **0% → 56.8%** (517 of 911) |

What stays unnamed: objects whose data stores no name (or an empty one), actors no script names with `actorName`, and every variable and bit (a game's own variables have no names in its data; LOOM's come from its reviewed glossary).

Real window (`WindowCheck s:118`, Indy 3 EGA, private profile): the script page's readable view shows "0034  Indy: costume 10; …", "Marcus", "Henry", "Elsa" ([picture](window/s-118.png)). Verbs in the window (`WindowCheck s:1:verb`, `s:2:verb`): Script 1 +0x0569 "do the sentence: verb "Walk to" object 834 "locker room"", Script 2 +0x010F "run verb "Give" of local 1 with local 2". Runs `count-run1.txt` (objects, actors), `count-run2.txt` (with verbs). Test: `GameNamesReadableTests`.

## Costumes, and actors in getDist and faceActor (1 November)

Monkey Island 1 names few actors (`actorName` mostly for Guybrush and placeholders) and reuses actors 3 to 10 from scene to scene, so a number-wide name rarely holds. A costume belongs to one character more surely: the name given to the actor wearing a costume (in a script or room that gives that actor one costume) names the costume when it is given in at least two thirds of those places, and a script or room that dresses an actor in it without naming the actor uses it (`LoomGlossaryGameNames.cs`). Example: Script 143 +0x0172, actor 7 in costume 75, "Herman Toothrot" in 7 of 7 scripts. `getDist` and `faceActor` now read a number below the actor count as an actor (ScummVM `objIsActor`), as class tests already did. `Count` has a sample mode (`COUNT_SAMPLE=actor|object|verb`: unnamed references by number, an example each).

| Game | Objects named | Actors named | Verbs named | Together (README) |
|---|---|---|---|---|
| Indy 3 EGA | 53.5% (662 of 1,237) | 74.5% → 74.9% | 57.0% | 67% → 68% |
| Monkey Island 1 floppy | 66.4% | 39.0% → 40.6% | 43.5% | 48% → 49% |
| Monkey Island 1 CD | 71.6% | 39.2% → 40.9% | 29.2% | 49% → 50% |
| Monkey Island 2 | 80.3% | 50.1% → 50.7% | 60.9% | 59% → 60% |
| Indy 4 | 91.4% | 79.3% → 81.0% | 79.6% | 81% → 84% |

Objects named rise where "object 1" to "object 12" were actors (they now count as actor references). Run: `count-run3.txt`. Tests: `GameNamesReadableTests.AnActorUnnamedInItsScriptIsNamedByTheCostumeItWears`, `ADistanceToASmallNumberIsADistanceToAnActor`.

## Not done

- Variable and bit names for the other games: their script maps (`docs/<game>/*_SCRIPT_MAP.md`) hold 13 to 56 rows each, mostly engine variables that already have names, so there is little to turn into data. What stays unnamed is mostly what the game's data does not name: image-only objects (drawn or set to a state, with no stored name) and actors no script names.
