# Maniac Mansion

| | |
|---|---|
| Families (`GameFamily`) | `ManiacClassic`, `ManiacEnhanced` |
| Fixtures (folders under `Games`) | `mmansion ega en` (classic, English), `mmansionE ega es` (enhanced, Spanish) |
| SCUMM version(s) and script dialect | classic v1 (`ScummScriptDialect.V1`), enhanced v2 (`V2`) |
| Tracker | [SUPPORT_STATUS.md](SUPPORT_STATUS.md) |
| Script map | [MANIAC_SCRIPT_MAP.md](MANIAC_SCRIPT_MAP.md) |
| Play evidence | [Classic English](../../.verification/maniacclassic-play-20261017/README.md), [Enhanced Spanish](../../.verification/maniacenhanced-play-20261017/README.md) |
| Cutscene evidence | [Classic English](../../.verification/maniacclassic-cutscenes-20261018/README.md), [Enhanced Spanish](../../.verification/maniacenhanced-cutscenes-20261018/README.md) |
| Edition checks | [Classic English](../../.verification/maniacclassic-editions-20261018/README.md), [Enhanced Spanish](../../.verification/maniacenhanced-editions-20261018/README.md) |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Classic (v1) | `mmansion ega en` | fixed-size index, tile rooms; actor colours from the game scripts and ScummVM tables | Available-copy checks pass; other classic releases blocked, no fixture |
| Enhanced (v2) | `mmansionE ega es` | one-byte object index, one-stream rooms; exact reviewed Spanish executable supplies native font | Available-copy checks pass, including all 128 font slots; other enhanced releases blocked, no fixture |

The two rows are separate families, not alternate editions of one family. S9 on 18 October 2026 verifies the available references and their compatibility boundaries; it does not claim another language, demo or patched release works. Registry levels remain unchanged. Existing original-game visual/timing/listening uncertainties remain open.
