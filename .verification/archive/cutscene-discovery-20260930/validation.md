# Source-derived cutscene discovery

Reading: current App manual menu uses three KnownCutscenes plus a separate opening; runtime execution uses reached session instructions and is independent. Gallery shares this hand-maintained catalog.

Decoded: reproducible discover.py reads the four-column code.tsv and records cutscene sites, callers, room commands, state reads and unresolved targets/scope. Current snapshot: 1374 owners, 216 candidate owners, 235 sites, 123 unresolved target/scope rows (including five variable targets). Compact inventory includes input SHA256. Known 046/052 object callers and 086 room watcher cross-checked against decoded rows. Literal calls to room-local scripts resolve only when their owner is present in the same room scope; missing scope stays unresolved.

No new scene start or state was invented. Documentation proposes candidate generation plus evidence-backed promotion. See docs/loom/LOOM_CUTSCENE_DISCOVERY.md. No user-visible menu entries added until supported starts are validated.

App/Core unchanged in this task. Prior Release build zero warnings/errors, suite 249, BusyCheck 11/11 remain current. WPF/audit/doorway/render checks not rerun for this documentation/offline discovery task.
