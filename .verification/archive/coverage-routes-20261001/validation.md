# Source route investigation after native sound completion

Research checkpoint; no production Core/App/recipe changes in this task. Current catalog remains **329 starts, 163/235 automatic sites, 55 owners with gaps and 123 unresolved static edges**. Reaching a site in a diagnostic sequence does not promote it or prove original-game input fidelity. Seven incomplete automatic roots remain excluded.

Fresh folder checked against git before creation. Generic `RouteProbe` accepts visit/direct-verb/click/frame actions; reflection reads pending script slots, never writes prerequisites. Counts below use an unchanged 3,000-frame per-stage ceiling and 32 settled frames. Full packets include source uncertainties, actors, pending freeze counts, locals and trace under ignored `Exe/VerificationSession-20261001/coverage-routes-2` and `late-routes-1`. Compact digests retain every scope boundary, final stage/pending state, focused uncertainty evidence and SHA-256 of the full packet. Boot/presentation uncertainties remain in those full packets, with their total/unique counts in the digest.

## Watcher053 and actor-box087

| Diagnostic start | Observed result | Interpretation |
| --- | --- | --- |
| Fresh catalog Room13, arrival32, floor click(112,131) | 053 begins; no end, possible Room14 load at +0x0107; still Room13 | Seeded catalog arrival does not establish the complete route. |
| Fresh Room10, direct295/0x38; settle052; floor click(112,131) in same session | 052 ends +0x017C after334 frames; 053 ends +0x02AC after367; rooms13/14/12 reached | Existing transition establishes the actor certainty needed by the watcher. Source local201 sees box change to2 and class24 absent on325 before chaining053 (+0x0026). This is a usable *sequence* candidate, not the fresh Room13 entry root. |
| Fresh Room38, direct610/0x36 | 087 remains in Room37 after3,000 frames, polling actor5 box2 at +0x047C..+0x0480; no end | Direct fresh start lacks the prior actor state. |
| Existing firewood start: Room36 direct599/0x38, arrival32, click(109,102); settle086; same-session610/0x36 | 086 ends +0x055C after866; 087 ends +0x09B9 after1,339; final Room38; 2/2 scopes | 086 clears actor5 classes at +0x0015 and establishes later actor setup. 087 places actor5 at603 and walks to604 (+0x044D..+0x0454), reaches box2 and finishes. No actor/class writes by the probe. |

`routes.json`, [digest](route-results.json). These assume catalog opening and stated input/direct verbs; they do not fix the unresolved original-game Room36 exit click, simulate a draft, or authorize arbitrary source-state synthesis. Automatic 053/087 entries and their site gaps stay unchanged. A catalog promotion needs a generic multi-action sequence representation with audited stage completion, actual shared memberships and visible replay validation; the current one-verb/arrival-click format cannot express “finish052, then click” without changing its semantics.

`RouteUiCheck` **8/8** visible MainWindow assertions. Existing052/086 preludes run in explicitly instant harness mode;053 and087 run on the normal Dispatcher clock, in the same live sessions. Floor input uses `HandleRoomPoint`;087 uses an explicitly diagnostic direct look through `SendInputToGame`. Both end at the exact source boundary, show the actual final room and restore sound/start controls. Inspected `route-ui-1/watcher-completed.png`, `actor-box-playing.png`, `actor-box-completed.png`. Real visible WPF layout and synthetic handlers; no physical mouse, original-game run, draft casting or independent listening. No production replay promotion.

## Room41 unmatched scopes

Both object633 responses (0x41/0x4B) start local204 with arguments1/0; local204 +0x0000 starts local213 with argument0. Local213 opens a cutscene at +0x0000, then waits for draft-note bitD67 to clear (+0x000B), followed by draft bitD66 (+0x0016). Its matching end is +0x00A9. Script020 establishes D66 and calls031 for note animation;031 sets D67 at +0x0000 and clears it at +0x0243/+0x031D. A direct response is not that draft-input sequence.

Native-clock probes end after1,686/1,680 frames in Room43 with **3 begins/2 ends**: local204 ends +0x031E,090 ends +0x0730, local213 never reaches its end before room-local removal. Script06 remains frozen once. `InCutscene == false` is insufficient evidence of completion. Both roots remain unresolved; do not clear bits, unfreeze scripts, silently balance scope counts or extend a bound to promote them. Next investigate actual draft input plus room-local scheduling/lifetime from the source.

## Late-game096 and inventory correction

Room43 object639/0x38 gates096 on **D7B, D61 and DA3 all nonzero**, respecting false-branch disassembly semantics. Script095 sets D7B/DA3 at +0x0720/+0x0725. Room45 object656/0x36 sets D61 at +0x000A; object655/0x38 starts092, which returns to43. These source actions establish the bits; no probe writes them.

Three individually recorded experiments in `late-routes.json` / [digest](late-route-results.json):

* Fresh43 +639 use: no scope, reaches44.
* Fresh45 +staff look,655 use,639 use: two matching suppression scopes, returns45; no096.
* Fresh44 +653 look completes095 (+0x079D;858frames) in45; staff look ends +0x0074;655 use completes092 +0x0039 in43;639 use **reaches096 +0x0000**, but its actor4 wait leaves the +0x0049 Room45 load possible, not dispatched. Final43; **4 begins/3 ends**, script06/local204 frozen.639 is class24 untouchable, so this remains an explicit direct-verb exploration experiment. Actor4 movement follows a seeded pick; the actor4 prerequisite is still unresolved. No promotion, membership or game-click claim.

Fresh source inventory corrects “ending owners without literal roots”: the only six outstanding owners with **no discovered literal roots** are shared helpers004/005/009/012/013/022. Late/story owners including096 have possible roots whose fresh probes fail or do not start the required scope. [Complete inventory](unrooted-sites-2.json) retains all123 static unresolved edges and every outstanding owner with no completed literal preview. An owner with one working branch can still have site-level gaps. This is not full-game coverage.

## Validation and build

RouteProbe and RouteUiCheck builds: zero warnings/errors. This task changes verification/docs only; the preceding sound task's253 tests, identical-pixel76-room audit, Blockers audit, doorways16/16, controls19/19, input18/18, audio10/10 and catalog global21/21/room20/20/all38new114assertions remain applicable. No new production behavior/pixels warrants repeating those checks.

After the user closed the app, **main checkout Release solution rebuilt successfully**, zero warnings/errors. Cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`; command `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`; user executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. No user process stopped by the agent. Other harnesses use isolated BaseOutputPath when necessary. Unrelated untracked object-click-room17/room-object-interaction/LoomObjectLookAuditTests preserved; Games/scummvm untouched.

Session token usage tool unavailable; account limits are not session usage. Stop conservatively at this completed, committed research checkpoint, without a half-written sequence implementation. Next work and exact artifacts are in `docs/NEXT_SESSION_PROMPT.md`.
