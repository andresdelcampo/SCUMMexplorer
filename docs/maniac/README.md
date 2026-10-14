# Maniac Mansion

| | |
|---|---|
| Families (`GameFamily`) | `ManiacClassic`, `ManiacEnhanced` |
| Fixtures (folders under `Games`) | `mmansion ega en` (classic, English), `mmansionE ega es` (enhanced, Spanish) |
| SCUMM version(s) and script dialect | classic v1 (`ScummScriptDialect.V2`), enhanced v2 (`V2`) |
| Tracker | [SUPPORT_STATUS.md](SUPPORT_STATUS.md) |
| Script map | [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md) |
| Other notes | none |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Classic (v1) | `mmansion ega en` | fixed-size index, tile rooms; actor colours from the game scripts and ScummVM tables | no |
| Enhanced (v2) | `mmansionE ega es` | one-byte object index, one-stream rooms | no |
