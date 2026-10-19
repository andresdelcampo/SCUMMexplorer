# Zak McKracken and the Alien Mindbenders

| | |
|---|---|
| Families (`GameFamily`) | `ZakClassic`, `ZakEnhanced` |
| Fixtures (folders under `Games`) | `zak ega en` (classic, English), `zakE ega en` (enhanced, English) |
| SCUMM version(s) and script dialect | classic v1 (`ScummScriptDialect.V1`), enhanced v2 (`V2`) |
| Support requirements | [Maniac and Zak support boundaries](../REQUIREMENTS.md#maniac-and-zak-support-boundaries) |
| Script map | [ZAK_SCRIPT_MAP.md](ZAK_SCRIPT_MAP.md) |
| Inventory state evidence | [Classic](../../.verification/zakclassic-state-20261018/README.md), [Enhanced](../../.verification/zakenhanced-state-20261018/README.md), [integration checkpoint](../checkpoints/20261018-S8-ManiacZak.md) |
| Other notes | none |
| Palette context follow-up | [19 October checkpoint](../checkpoints/20261019-Palettes-ManiacZak.md), [transformation evidence](../../.verification/zakclassic-transform-palettes-20261019/README.md), [Mars evidence](../../.verification/zakclassic-mars-palettes-20261019/README.md), [context provider](../../.verification/zakclassic-palette-core-20261019/README.md) |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Classic (v1) | `zak ega en` | 24 of 35 costume colours derived, 11 labelled estimates (S4b refresh); own v1 scripts and actor format | available English copy checked; alternate editions blocked (one local copy), [S9 record](../../.verification/zakclassic-editions-20261018/README.md) |
| Enhanced (v2) | `zakE ega en` | own v2 adapter/dialect, native font | available English copy checked; alternate editions blocked (one local copy), [S9 record](../../.verification/zakenhanced-editions-20261018/README.md) |

Engine fidelity (18 October): two formerly capped starts now finish per edition; native ticket/terminal waits remain, lighting is rendered, and classic dynamic palettes persist. Card estimates and Cutscenes remain Partial. [Checkpoint](../checkpoints/20261018-Fidelity-ManiacZak.md).

Palette contexts (19 October): Classic Zak offers display-only colour variants from its source, native opening and separately entered room previews. Seven of the eleven estimate cards gain contextual colour records; this does not establish universal palettes, earned transformations or Mars access. All eleven global estimates and the Partial costume level remain. Transformation/Mars source conditions and the stopped native-selector attempts are recorded at the links above. The enhanced edition and Classic Maniac's accepted colours are unchanged.
