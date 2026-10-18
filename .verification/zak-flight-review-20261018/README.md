# Independent Zak flight source review

18 October 2026. Scope: supplied English DOS v1 `D:/Juegos/Lucas/zak ega en` and v2 `D:/Juegos/Lucas/zakE ega en`; native flight choices, ticket state and retained Script 104 wait. Source decoding only unless explicitly described otherwise. No ScummVM executable, game/profile mutation or shared production edit.

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

Both harness builds passed, zero warnings/errors. The retained `SceneCheck ... one 11 0 0` diagnostic also showed why a raw fresh preview is not an earned boarding route: original opening slots remained pending, ego was still in Room 1, and by frame 1000 the session was back in Room 1 while Script 104 waited. That run was diagnostic and is not a completed scene or gameplay acceptance. Workers validate from the settled native opening and actual ticket choice. No arbitrary owner, currency, destination or actor edits are an acceptable correction.

Original-game pictures, perceived timing, listening, a continuous earned airport approach and other destinations remain distinct from prepared Core/WPF checks. No registry level is raised by this source-only review.

## Result review

Pending the workers' finished route, negative tests and any source-backed shared correction.
