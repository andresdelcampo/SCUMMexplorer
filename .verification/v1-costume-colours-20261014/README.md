# Classic v1 costume colours from the scripts (14 October 2026)

Stage S2 (Costumes), families ManiacClassic and ZakClassic. Fixtures: `Games/mmansion ega en` (Maniac Mansion, DOS EGA, English) and `Games/zak ega en` (Zak McKracken, DOS EGA, English). The `Games` folder of the main checkout no longer has a `FUTURE/` level; `SupportConformanceTests.Fixtures` still names `FUTURE/...` for these families, so that test skips them (see "Found on the way").

## What ScummVM does (source read 14 October 2026, `engines/scumm` of ScummVM master)

- `costume.cpp` `ClassicCostumeRenderer::byleRLEDecode_C64`: pixel code 1 and 2 of a v1 costume are the actor's two colours, chosen **by the actor's number**: Maniac Mansion `palette[1] = v1MMActorPalatte1[actor]`, `palette[2] = v1MMActorPalatte2[actor]` (two fixed 25-entry tables); Zak McKracken `palette[1] = 8` (10 on the C64 only), `palette[2] = _palette[actor]`, the actor's own palette slot. Code 3 is black.
- `script_v2.cpp` `o2_actorOps`: sub 4 `setActorCostume`, sub 2 `setPalette(i, arg)` with `i = act` in version 1 (so `palette[act] = value`).
- `actor.cpp` `Actor::setActorCostume` returns before touching the palette in v1 ("V1 zak uses palette[] as a dynamic costume color array"), so the palette **persists across costume changes**. `Actor::initActor` zeroes it.
- `scumm.cpp`: every actor `i` of a v1 game starts with `setActorCostume(i)` ("this is from IDB"); `_numActors` is 25 for Maniac, 13 otherwise; `runBootscript` runs script 1.

So the explorer's old rule (Maniac: tables with costume number = actor number; Zak: fixed 14) was an estimate of what the scripts do. `ScummV1ActorColors` (Core/Scumm/V1) now reads every global script and every room entry/exit/object-verb script, disassembles it in the v1/v2 dialect and collects the `actorOps` sub 4 and sub 2 instructions with their operands (a constant or a variable operand), then derives per costume:

1. A script gives the costume to an actor (sub 4): Maniac, the engine tables by that actor's number; Zak, the palette the scripts leave for that actor: the last palette instruction of the same actor operand in the same script after the costume instruction and before that actor's next costume instruction (the palette persists), else the closest one before it. An actor no script ever sets a palette for (the boot script's value counts as the start state) has that value (0 if none).
2. No script gives the costume away: actor n wears costume n from the start (engine rule above); valid when no script (other than the boot script) changes that actor's palette.
3. Otherwise a **labelled estimate** (`ScummV1ColorBasis.Estimate`): the palette depends on what ran before. The value used is what the boot script leaves in the actor's palette, else its first scripted value, else the old fixed 14.

## Commands

```
rtk proxy dotnet build .verification/V1CostumeColors/V1CostumeColors.csproj
rtk proxy dotnet Exe/V1CostumeColors/Debug/net10.0/V1CostumeColors.dll "Games/mmansion ega en" table   -> maniac-table.txt
rtk proxy dotnet Exe/V1CostumeColors/Debug/net10.0/V1CostumeColors.dll "Games/zak ega en" table        -> zak-table.txt
rtk proxy dotnet Exe/V1CostumeColors/Debug/net10.0/V1CostumeColors.dll "Games/<game>" sheet <out.bmp>  -> maniac-sheet.png, zak-sheet.png
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummV1ActorColors|...SupportMatrixTests|...SupportConformanceTests|...GameSupportTests|...OtherV4GameTests"   -> 31 passed
```

The sheets show each costume's fullest pose; **left = derived colours, right = the old estimate**.

## Numbers

| Game | Costumes | Script | Start state | Labelled estimate | Instructions read | With a variable operand |
|---|---|---|---|---|---|---|
| Maniac Mansion | 23 | 0 | 23 | 0 | 1 (sub 4 sets costume 0 on a variable actor: hides an actor) | 1 |
| Zak McKracken | 35 | 23 | 2 | 10 | 84 | 18 |

- **Maniac**: no script gives any actor a costume other than 0, so actor n wears costume n for the whole game; the 23 costumes take ScummVM's two tables at their own index, exactly as the old estimate did. **The picture does not change** (`maniac-sheet.png`: both columns identical). What changed is the evidence: the pairing is now shown from the scripts, not assumed.
- **Zak**: 25 costumes derived (23 from a costume instruction plus its palette, 2 start-state costumes 2 and 3 at palette 0); the picture changes for most (`zak-sheet.png`): e.g. costume 30 (boot script `Script 01` @11, palette 0 then 14 @10C in the same script: 14), costumes 22 (Room 19 entry script: palette 1 before the costume), 23, 24, 25 (palette 9 after), 21 (Room 38 entry script: the actor is variable `var[0x042]`, the palette 9 uses the same variable), costumes 12 to 15 and 6 (actors 2, 5 and 6 get no palette from any script besides the boot script: 0, black).
- Zak costumes still **estimated** (10): 1, 5 (given to actor 1 by scripts that set no palette; 14 from the boot script), 4, 8, 9, 18, 32 (palette in effect depends on what ran before), 16, 17, 35 (no actor of that number and no constant instruction names them; the 18 variable-operand instructions, mostly "restore the saved costume", may give them).

## Comparison with the bundled ScummVM

**Not run.** The bundled `scummvm/` has no scriptable state: setting an actor's costume and palette in a given room needs its debugger console typed interactively, and driving another application window is outside this run's rules (the checklist says "where you can run it"). The comparison made is **against ScummVM's source**: the derivation uses the same two code paths (`byleRLEDecode_C64` and `o2_actorOps`), and the Maniac pictures equal the previous ones, which were built from the same tables. The cells therefore stay partial (registry unchanged, below).

User check (real window): open `Games/zak ega en`, Costumes tab: costume 30 should be Zak in blue (suit 14), costumes 12 to 15 black; open `Games/mmansion ega en`: the 23 costumes look as before. Optionally in ScummVM (Zak, debugger `Ctrl+Alt+D`): `room 8`, `actor 10 costume 23` should show costume 23 with palette 9 once the Room 08 entry script ran.

## Uncertainties kept

- A palette instruction on a variable actor counts as the palette of the costume next to it (same variable, same script). Only Zak's Room 38 entry script does this; the variable's actor number is not resolved without running the script.
- Actor and palette operands that are variables (18 in Zak, 1 in Maniac) are not resolved; "last palette in the span" follows instruction order, not control flow (a branch inside the span could skip an instruction).
- A costume given to several actors with different palettes shows the first one found and reports the others as `Alternatives` (Zak costumes 7, 20, 26, 28).
- The skin colour is 8 for Zak (the DOS value; `10` is C64-only in ScummVM).
- The lights mode `LIGHTMODE_actor_use_colors` (dark rooms draw actors in colour 11 in ScummVM) is a run-time effect and is not applied.
- Maniac actor 4's skin colour 4 comes from ScummVM's table, which is not stored in the game's files; the table is ScummVM's data, not derived from the game.

## Found on the way

- `Games/` was reorganised since the conformance fixtures were written: there is no `FUTURE/` or `ALTERNATIVE LOOMS/` folder, so `SupportConformanceTests` skips every family whose fixture names them (Indy3, Monkey, Indy4, enhanced and classic Maniac/Zak, three LOOM editions). Not changed here (shared file); the new unit test looks in both `Games/<name>` and `Games/FUTURE/<name>`.
