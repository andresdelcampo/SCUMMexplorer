# Checkpoint: S9 for the v5 copies, faster cutscene derivation, language choice (18 to 19 October 2026)

Run requests: "Keep going: make the v5 cutscene derivation faster (a shared booted snapshot per game), and in parallel run S9 for the other Monkey Island 2, Indy 4 and Monkey Island CD copies." (18 October) and "yes, we should allow choosing a language in those games that have multiple" (19 October). Agents were stopped by rate limits and resumed. Follows [18 October](20261018-S6-v5-S7-MonkeyCd.md).

| Item | Commits | Result | Record |
|---|---|---|---|
| S9 Monkey2 (`mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es`, `mi2 talkie es`) | e8a80047, 2aaf9f43, merges | Every copy passes every check. The floppy copies ask the copy protection: the boot presses Enter, types the game's own answer from `var[0x206]` and clicks the full game, all labelled assumed input (`ScummBootInputPlan.AnswerDigits`). The talkie patch adds 71 description cutscenes; the Spanish talkie pads verb names with '@' (dropped from titles) | `.verification/monkey2-editions-20261018/` |
| S9 Indy4 (`indy4 talkie vga es`, `indy4 classic vga es`) and MonkeyCd (the disc's SPANISH folder) | 267da469, e0fa05f7, 7bb09d1b, bad9c335, 04d49fa6, acacb14b, merges | All pass. The Spanish floppy's stone-disk protection (Room 81) is answered with the nonzero argument the room's own code passes (`ScummSessionBoot.NextStartArgument`). `monkey1 vga es` turned out to be the Spanish four-disk floppy (checked as MonkeyFloppy, passes). The real Spanish CD is a folder on the reference disc | `.verification/indy4-editions-20261018/`, `monkeycd-editions-20261018/` |
| Faster cutscene derivation | ba0ad26c, fc3805e9, 567a6ed6, 59364926, be8d6e87 | `ScummBootSnapshot`: one boot per game (or per worker for disk games), every run from a deep copy; entries handed out one at a time; a costume cache for `getActorFromPos`. All twelve cutscene lists byte-identical (SHA-256 in the record). First launch in the window: MI2 70 s (was 140–174), Indy 4 129 s (was 185); the Indy3 and Monkey Island 1 releases about 10 s in the harness | `.verification/cutscene-snapshot-20261018/` |
| Language choice (R-3.16, confirmed by the user 19 Oct) | 56d1d5e1, 0238498f, b83246ce, edde38d0, c632ac0d, 9beb9f61 | Only the Monkey Island 1 CD holds several languages (ENGLISH, FRENCH, GERMAN, ITALIAN, SPANISH). A list under the game's name chooses one; it reopens the game and is remembered per folder; caches keyed by `GameLibrary.Key`. Real window: Spanish verbs and "Mirar cartel" | `.verification/language-choice-20261019/` |

Also: the xUnit2029 warning in `ScummMonkey2PlayTests.cs` fixed. Suite on master at the end: 1616 tests (1611 passed, 5 skipped, 0 failed), 0 build warnings.

**Not done:** French, German and Italian of the Monkey Island CD have had no boot, route or cutscene runs (S9 for them). MI2 and Indy 4 cutscene lists still take over a minute on first launch: the rest is the script executor copying object states and variables on every run (next speed-up, shared engine code). The Indy 4 playable credits in Room 4 draw several lines on top of one another (S6 leftover). ScummVM runtime comparison not done.

**Offer:** run the App with server GC (Indy 4's harness derivation 100 s → 68 s); it changes the whole app's memory use, so it is the user's call.

**User checks:**
1. Monkey Island CD (`Games/monkey1 vga en`): the language list under the game's name; Spanish → Room 33, verbs Dar/Coger/Mirar…, "Mirar cartel"; restart the app: Spanish comes back. LOOM shows no list.
2. Time the first-launch cutscene progress line for MI2 (`mi2 talkie en`, Room 4) and Indy 4 (`indy4 vga en`, Room 4); play a few picker scenes.
3. `mi2 classic es` Room 4: the boot passes the recipe screen by itself; Spanish verbs and sentence line. Indy 4 Spanish talkie: accents and spoken lines.

**Next run:** S9 for the MI1 CD's French, German and Italian; the executor allocation speed-up; the Indy 4 credits overlap.
