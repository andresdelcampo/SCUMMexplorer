# Scene order, first step (8 November 2026)

ROADMAP work item 3c (user, 2 November: "some always predate others, so the bits are all set correctly when playing them, across all games").

## What it does

`ScummSceneSetupSearch.Order.cs`. The game's code is read once per game: every object verb, room entry and local script, and every global script except the boot (Script 1, whose other branches, such as Monkey Island 2's part selection at +0x0009, are not story) and helpers shared by more than 3 scenes. For each piece, the story flags it sets to constants (bits raised, variables from 0x40; the last constant written wins).

When a picked scene's setup needs a flag that exactly one piece of code outside the scene sets, that code ran first. The scene also gets:
- the other flags that code sets ("…, which sets it: earlier code that sets bit[…]");
- the flag values its tests need on the way to that write (read as the search reads a scene's way to its cutscene: `Ranked` with a goal instruction), followed back up to 4 steps.

Flags the scene sets itself, values the setup already has, and values against a test on the scene's own way are left alone. The whole set is kept only when the scene still begins and reaches the rooms it reached without it (one extra run per scene).

## Measured (run 65 against run 64, `../scene-setup-20261031/*-run65.txt`, `MEASURE_ORDER=1`)

| Copy | Setups | Given earlier state | Ways changed |
|---|---|---|---|
| Indy 3 EGA / 256 | 56 / 56 | 0 / 0 | none |
| Monkey Island 1 EGA / VGA | 48 / 47 | 2 / 2 | none |
| Monkey Island 2 | 82 | 11 | none |
| Indy 4 | 192 | 10 | none |
| Maniac Mansion classic / enhanced | 18 / 17 | 1 / 1 (3 offered, 2 dropped by the run) | none |
| Zak McKracken classic / enhanced | 27 / 27 | 0 / 0 | none |

27 of 570 setups (4.7%). Scenes started at their cutscene unchanged: Indy 3 4.5%, Monkey Island 1 7.9% and 7.5%, Monkey Island 2 5.3%, Indy 4 3.1% (5.0%); 10 show nothing. Checked in the window on Monkey Island 2 Room 5's entry (the pirates' story plays, `window-mi2-room5/`).

## What it does not reach yet

- **Scenes a fresh start begins** (about 60% of each game) have no setup, so no anchor: nothing says where in the story they sit.
- **Setups that need no flag.** Indy 3: 8 of its 62 setups need a story flag; the rest need object states, held items, places. Of those 8, 4 flags are set in two places, 3 by no constant.
- **Flags set in several places** (two scenes that both open a door): no one predecessor.

A wider order needs where a scene sits in the story by other evidence: the rooms reachable at that point (exits gated by flags and items), the items held. That is the next step of 3c.

## Tools

`Measure` with `MEASURE_ORDER=1`: for each flag a setup needs, the code that sets it (`ScummSceneSetupSearch.CodeSetting`), and what earlier code adds. Test: `UserReports20261107Tests.APickedSceneGetsTheStateOfTheCodeBeforeIt` (Monkey Island 2 Room 5).
