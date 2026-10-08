# Remaining source checks — 8 October 2026

This extends the [draft checkpoint](../draft-input-20261008/README.md) with the scenes that cause and teach the last drafts, and inventories nonliteral call operands. It does not repeat the replay, portal, catalog-return, staff-layout or native-walking audits. No production behavior or displayed draft name changes. Native Computer Use remains stopped; the standalone Room 34 reveal remains parked.

## Silence: stronger own-source association

The earlier record examined Hetchel's response but left the name at assumed manual order. The preceding scene supplies a stronger chain in **each edition's own data**:

| Edition | Script 101 text | Script 101 plays slot 12 | Room 09 local 223 plays slot 13 | Local 223 / 224 mark last-note knowledge for slot 12 / 13 |
| --- | --- | --- | --- | --- |
| English Roland EGA | +059E, “Silence.” | +05AD → local 212 [12] | +01C4 → local 212 [13] | +02A1 / +03C0 → Script 34 |
| Spanish Roland EGA | +05D9, “Silencio.” | +05E9 → local 212 [12] | +01CA → local 212 [13] | +02A6 / +03D4 → Script 34 |
| English VGA | +06ED, “Silence.” | +0701 → local 212 [12] | +01A0 → local 212 [13] | +02C5 / +0682 → Script 34 |

Slots here are **zero-based**, matching Script 23 and `LoomDraftStatus.Slot`. Local 212 +0003 calls Script 23 with its argument. Script 23 doubles the slot, reads its pattern and response words from the own-edition table (EGA base 032, VGA base 064). Slot 12 maps to 4E/4F; slot 13 maps to 50/51. Script 26 separates the response word into the forward and reverse verb bytes and dispatches through Script 7. The checked Room 09 Hetchel callbacks send 4F to local 223 and 51 to local 224.

English/Spanish local 223 +0037 explicitly restores speech; VGA's own corresponding line is shortened to thanks and the next instructions to Bobbin. The causative Silence text, played slot and reversed callback together support the existing **Silence** association beyond manual order. No object needs an explicit 4E entry for this source scene to demonstrate its effect. The unrelated prisoner's “Silence” in Script 90 is not draft-name evidence.

The reverse's **effect** is restored speech; its exact canonical name is still unestablished. Keep the existing reverse label. Script 34 sets **4000 last-note knowledge**, not 1000 reversibility or 2000 learned knowledge; these flags must not be conflated. Detached status snapshots retain their disclosed seeded patterns and do not claim earned learning or a new casting route.

## Shaping: effect chain, exact name still uncertain

Local 223 plays slot 13 through local 212, then starts local 227 (E3). That helper changes Hetchel's actor/pose presentation to the eaten-bird form; 51 starts local 224, which restores actor 5's costume 34 (English/Spanish +003B, VGA +0041) and later establishes slot 13's last-note knowledge through Script 34. This ties the pair to the same form-changing story sequence in all three editions.

No matching printed label identifies slot 13 as “Shaping” or names its reverse. The English substring hits about a *bladeshaper* and shaping steel belong to the sword conversation, not this draft. Preserve **Shaping** as a reading/association and do not invent a stronger canonical name from a costume number. Unknown actor roles likewise remain scoped or numeric.

## Dynamic operands: finite inventory and source limits

The source scan covers globals, entries/exits, locals and explicit object verbs. It counts **11 English EGA, 11 Spanish EGA and 8 VGA call sites with a nonliteral first operand**. These are operand sites, not unsupported engine operations or missing scenes. No dynamic `chainScript` target occurs in this decoded inventory.

| Site | Source classification |
| --- | --- |
| Global 6 (two), 7 and 8 | Ordinary hit-tested walk/use/look/aim dispatch. Global 7 also takes a dynamic verb. An arbitrary target cannot be named without the particular input/state. |
| EGA Global 44 (two) | Opening loops initialize local 1 to 200 and increment through 209, starting Room 01 locals 200–209 with table-derived coordinates. VGA has no corresponding dynamic sites here. |
| Room 02 local 203 | Ending/credits loop initializes local 0 to 205 and increments through 222. The bounded source target set is locals 205–222; completion/reachability is a separate question. |
| Room 10 local 205 | Its four own-room look callers pass CE/CF/D0/D1 (206–209). English/Spanish objects 297–300; VGA 222–225. These resolve this helper's first dynamic target for those literal callers. |
| Room 23 local 210 | Its four own-room Terror callbacks pass (actor 2, local 215) through (actor 5, local 218). English/Spanish objects 422–425; VGA 347–350. This resolves local 1 for those callers. |
| Room 69 local 200; EGA Room 70 local 200 | `findObject` selects the clicked menu object; dispatch uses fallback FF. These are input-dependent menu handlers. VGA has no Room 70 dynamic site. |

Loop bounds use the established old SCUMM comparison order: relational instructions compare the right value against the left, and jump to the encoded target when false. They include their terminal target before the following increment ends the loop. Do not silently reverse that convention when reading the raw operand text.

The counts retain English global 0's incomplete decode and the four existing Spanish interior-entry read errors. Undecoded bytes, indirect entry into bytecode, dynamic room context and callers outside this decoded inventory remain limits. These finite source classifications do not justify a generic symbolic `Used by` change or another engine patch.

## Verification and reproduction

**33/33 focused source assertions pass** across the three editions. The probe saves own-edition offsets, decoded instruction fingerprints, terminology sites, nonliteral operands and literal helper caller arguments. Fingerprints hash offset/decoded-byte pairs, **not whole raw resources**. Generated reports/dumps remain ignored under `out-source/`. Only detached Core sessions are constructed; no UI/profile, native input, listening or game-file writes.

From `D:\Documents\Personal\Code Projects\SCUMMexplorer`:

```powershell
rtk proxy dotnet run --project .verification/source-gaps-20261008/Probe/Probe.csproj -- Games .verification/source-gaps-20261008/out-source
```

The harness rebuilds before running and includes the repository failure policy. All 368 relative links in the eight changed Markdown files resolve; `git diff --check` passes. A broad Core/replay/WPF rerun is unnecessary for this source/documentation checkpoint. The exact remaining original-game picture, input and audio comparisons are maintained in [the live dossier](../../docs/loom/FINAL_GAPS_HANDOFF.md).
