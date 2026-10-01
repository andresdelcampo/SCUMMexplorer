# Compact, vertically centered spell rows

Game Status spell rows now use a 26-pixel height (previously 32). Spell names, note selectors, Learned/Can reverse checkboxes and Advanced provenance are vertically centered. Editing controls use the same alignment and retain WPF's original interaction styles.

Existing normal-clock visible StaffCheck harness rebuilt and run with:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/staff-correction-20261001/run-ui.ps1 -Output Exe/SpellRowLayoutSession-20261001/ui-1
```

Result: 47 PASS, zero failures, exit 0. Actual combo editing, Apply/Close, read-only reverse capability and minimum-size Advanced layout remain usable. Inspected fresh `status-transcend.png` and `status-advanced-minimum-sources.png`: text and checkboxes are centered within compact rows. Full report and screenshots remain under ignored `Exe/SpellRowLayoutSession-20261001/ui-1/`. Synthetic WPF verification, not physical input.

Final main-checkout Release build: `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`, zero warnings/errors. No engine changes; prior Core/source-route results remain applicable. No user app stopped or unrelated work modified.
