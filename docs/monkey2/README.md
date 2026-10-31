# Monkey Island 2: LeChuck's Revenge

| | |
|---|---|
| Families (`GameFamily`) | `Monkey2` |
| Fixtures (folders under `Games`) | `mi2 talkie en` and `mi2 talkie es` (talkie), `mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es` (floppy releases) |
| SCUMM version(s) and script dialect | v5 (`V5`) |
| Tracker | [SUPPORT_STATUS.md](../archive/monkey2/SUPPORT_STATUS.md) |
| Script map | [MONKEY2_SCRIPT_MAP.md](MONKEY2_SCRIPT_MAP.md) |
| Other notes | none |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Floppy (MT-32, Sound Blaster; English and Spanish) | `mi2 mt32 en`, `mi2 sb16 en`, `mi2 classic es` | no speech; the boot asks the recipe copy protection (Script 128/130, answer in `var[0x206]`); 450 cutscene triggers, 190 groups; the Spanish interface says "Mirar a" in the sentence line | yes, 18 Oct ([record](../../.verification/monkey2-editions-20261018/README.md)) |
| Talkie (fan "Talkie CD 0.2 beta" patch; English reference, Spanish) | `mi2 talkie en`, `mi2 talkie es` | `MONSTER.SOU` speech codes in the texts; no copy protection at boot parameter 0; many descriptions wrapped in cutscenes (536 triggers, 260 groups); Rooms 45 and 50 patched; the Spanish copy pads names with '@' | yes, 18 Oct (Spanish; English is the reference) |
