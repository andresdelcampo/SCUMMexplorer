# Independent Zak flight source review

18 October 2026. Scope: supplied English DOS v1 `D:/Juegos/Lucas/zak ega en` and v2 `D:/Juegos/Lucas/zakE ega en`; native flight choices, ticket state and retained Script 104 wait. Source decoding and explicitly described session diagnostics. No ScummVM executable, game/profile mutation or shared production edit.

## Findings supplied to the workers

- Object **51 is peanuts**, as its own Room 11 name says in each edition. Script 116 queries/picks it up at +0018/+0025 and prints `Here's your meal.` at +0033; Room 12 squirrel Give/Use consumes it; Script 144 describes eating the peanuts. The historical phrase "ticket object 51" was incorrect. Retain the earlier failed evidence and correct the current interpretation.
- Script 104 +01C2 starts Script 116; +01C4/+01C5/+01C9 waits while peanuts remain owner 15. These offsets match independently in both copies. This is the native meal-service continuation, separate from ticket purchase and boarding.
- The actual tickets are **82/83**, code stored in Room 46. Script 1 loads Room 46 and performs real pickups before the introduction; later `setOwnerOf` transfers therefore refer to retained inventory code. Room 46 ticket Read/Give starts Script 58 and Use starts 131. Ticket purchase must preserve this lifecycle, not insert a new invented ticket.
- Room 8 reservations terminal **194**, Use 11, starts Script **30**. Zak requires CashCard **18** held. Script 30 loads the game's own Room **45** menu and waits for word **121** to change from 1024. Destination objects **659–668** supply their own IDs. Confirm object **657**, fallback verb 255, writes source constant 1; Cancel **658** writes 0. My initial message suggesting Cancel 2 was corrected immediately from the decoded object code; it is not acceptance evidence.
- Script 30 computes the fare from its own table, debits the card and records departure/destination/price only after confirmation. It returns to the terminal, walks to the ticket dispenser and transfers ticket 82 to Zak: Classic **+0223**, enhanced **+023C**. The shared object IDs do not justify assuming shared instruction offsets.
- Room 8 gate **190**, Walk 13, starts Script **115**. This checks the reached per-character departure word, rejects missing tickets (`I need a ticket first.`) or the wrong airport, then consumes the ticket to owner 14, clears its departure word and boards Room 11. The alternate cut directly to arrival requires previously acquired cabin objects 49/50; it must not be selected by fabricated prerequisites.

The game's persistent script facts belong in the parent-owned [Zak script map](../../docs/zak/ZAK_SCRIPT_MAP.md); this record identifies the independent review and corrections, rather than introducing a competing script map.

## Commands and limits

Cwd `.worktrees/zak-flight-review`; every command through RTK:

```powershell
rtk proxy dotnet build .verification/zak-fidelity-20261018/SceneCheck/SceneCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet build .verification/zak-flight-review-20261018/SourceCheck/SourceCheck.csproj -p:NuGetAudit=false --verbosity quiet
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zakE ega en" dump 30 104 115 116
rtk proxy dotnet Exe/SceneCheck/Debug/net10.0/SceneCheck.dll "D:/Juegos/Lucas/zak ega en" dump 30 104 115 116
rtk proxy dotnet Exe/SourceCheck/Debug/net10.0/SourceCheck.dll "D:/Juegos/Lucas/zakE ega en" 8 11 12 45 46
rtk proxy dotnet Exe/SourceCheck/Debug/net10.0/SourceCheck.dll "D:/Juegos/Lucas/zakE ega en" names ticket cash credit airport terminal
```

Both harness builds passed, zero warnings/errors. A later diagnostic compile initially used a nonexistent inventory snapshot property, corrected to `CurrentActorId` before its successful run.

The retained `SceneCheck ... one 11 0 0` diagnostic showed ego still in Room 1 after the Room 11 preview began; at frame 1000 the session was back in Room 1 while Script 104 waited. My initial message inferred that pending Scripts 06/09 proved the opening had not finished. **That inference was wrong:** those are persistent lighting/interface and periodic world-state loops. Their existence is not evidence of an unfinished opening.

An explicit `SourceCheck "<fixture>" boot` control in both editions establishes the narrower cause. Native boot ends with control true, cutscene false, Room 1 and ego 1 in Room 1; Classic takes 1784 frames, enhanced 1808, neither hits its boot limit. `EnterRoomAgain(11)` begins the room's scripts without a literal `loadRoomWithEgo` arrival; ego remains in Room 1. Script 104's `actorFollowCamera` then follows ego to its actual Room 1, with trace continuation at +01C0. This is an unprepared direct preview under the existing no-arrival policy, not a demonstrated planner startup scheduling defect. Script 115's actual boarding uses `putActorInRoom`, `putActor` and `actorFollowCamera`, and therefore establishes state the literal-arrival preview lacks. These diagnostic runs are not completed scenes or gameplay acceptance. No arbitrary owner, currency, destination or actor edits are an acceptable correction.

## Native class lookup found on the preparation path

The Classic worker's source-correct knife/plastic-card route reaches a second omission: native `ifClassOfIs` is not the later engine's class-number-list operation. Official [ScummVM `script_v2.cpp`](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/script_v2.cpp), `o2_ifClassOfIs` lines 1035–1047, read 18 October, looks up the loaded or held object code, reads raw OBCD byte 6 and tests `(byte & mask) == mask`. A missing header jumps unconditionally. The shared owner was informed before any production edit; it must preserve these current-room/held-code and missing-object semantics and keep the opcode in its v2 partial. This is independent of fabricated CashCard or ticket state.

Original-game pictures, perceived timing, listening, a continuous earned airport approach and other destinations remain distinct from prepared Core/WPF checks. No registry level is raised by this source-only review.

## Result review

Reviewed shared correction `07d9eb1` against the official v2 routine. No actionable issue: raw mask metadata is additive, its reader takes byte 6, the executor stays in the V2 partial and implements **masked equality**, not whole-byte equality. The callback reuses the current-room/retained-inventory header locator; byte 6 is immutable, so reading the retained room's original decoded record is correct. An absent header jumps even for zero mask. An unreadable header remains unresolved, rather than becoming fabricated absence or success. No room, game, language or script-offset exception was introduced.

Read all nine new mask tests, including a proper-subset success (`0x83 & 0x81`), partial-mask failure (`0x01 & 0x03`), zero-mask success, missing-header zero-mask failure and unreadable-header stop. Later-engine class tables are deliberately irrelevant. Parent runs the integrated regressions; this independent record does not add their counts to its own harness builds.

### Airport logical-room investigation

The parent requested independent review when enhanced preparation appeared to return from an alien cutscene to physical airport resource 8 while losing logical airport 145. Official `o2_cutscene` saves the engine's current room; `o2_endCutscene` uses that saved room only for classic Maniac. Both Zak editions always follow the current ego. Consequently, changing saved cutscene-room state would not be a source-backed Zak correction. Official [camera.cpp](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/camera.cpp), `setCameraFollows` lines 57–69, starts the scene using the actor's own room when needed.

Read-only architecture review found the possible loss seam: the generic executor normalizes `putActorInRoom` aliases into physical resources, and `getActorRoom` reconstructs the alias from the current room only while the actor is present there. A later scene away from that actor cannot reconstruct the old logical airport. This is a diagnosis supplied to the exclusive Core owner, not approval of an unobserved fix. Native actor logical-room retention and actual ticket/flight route evidence must decide any correction; no script-offset or airport-specific exception is acceptable.

Reviewed resulting correction `a425b0a`: per-actor logical/physical pairs preserve native room numbers for queries and camera room loads while the existing actor snapshots, renderer and walker retain physical resources. Explicit room assignments replace the pair, zero room works, coordinate-only assignments preserve it, and mismatched physical state is ignored by the resolver. `loadRoomWithEgo` remembers the native target before entry. Later-engine behavior stays behind the existing version boundary. The cutscene snapshot is untouched.

Review found one omission: two deferred-follow dispatch guards compared only physical resources, suppressing a native change between aliases of the same resource. Owner correction `7d4a2cb` adds logical target equality for v1/v2 and leaves later-engine guards unchanged. Its full-session regression, explicitly synthetic, establishes the logical alias 145 after dispatch in each supplied fixture. This addresses the review finding; no further actionable Core issue was found.

### Family route review

Read Classic `65db70f` and both workers' final route-test additions before integration. Positive preparation acquires the knife through disclosed exploration, retrieves the CashCard with actual held-item Use, visits the seeded logical airport and skips only the reached alien override. Native reservations refund the old ticket, then an explicit rendered destination and Buy charge the fare, issue the ticket, and the gate consumes it. Script 116 picks up peanuts; Script 104 passes its meal wait and restores control in Room 11. The test assertions distinguish ticket ownership, departure validity and retained peanut code; they do not infer a ticket from the selected destination alone.

Negative assertions are meaningful: unsuitable held ticket cannot retrieve the CashCard; missing CashCard preserves existing ticket/money; wrong airport preserves the ticket and route; Cancel retains the native candidate destination but leaves no valid departure, ticket or price and the gate refuses boarding. No fabricated owners, balances, prerequisites or actor placements appear in the reviewed family paths. No actionable issue found. The parent checkpoint pins final family commit IDs and actual test/window counts. These are bounded prepared checks, not an earned bus journey, a complete flight adventure or original-interpreter audiovisual acceptance.
