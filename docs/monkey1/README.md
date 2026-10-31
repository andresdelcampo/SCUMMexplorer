# The Secret of Monkey Island

| | |
|---|---|
| Families (`GameFamily`) | `MonkeyFloppy`, `MonkeyCd` |
| Fixtures (folders under `Games`) | `monkey1 ega en` (floppy, four disks, 256 colours), `monkey1 vga en` (CD), also `monkey1 ega es`, `monkey1 vga es` (both floppies) |
| SCUMM version(s) and script dialect | floppy: v4 (`V4`); CD: v5 (`V5`) |
| Tracker | [SUPPORT_STATUS.md](../archive/monkey1/SUPPORT_STATUS.md) |
| Script map | [MONKEY1_SCRIPT_MAP.md](MONKEY1_SCRIPT_MAP.md) |
| Other notes | none |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Floppy, 256 colours (VGA), four disks | `monkey1 ega en` (the folder name says EGA; the data is `DISK01.LEC` to `DISK04.LEC`), `monkey1 vga es` (Spanish) | v4 disks; the S4b reference (generic session boots to Room 33); the Spanish copy's protection quiz does not wait and accepts any year | Spanish: yes (18 Oct, [record](../../.verification/monkeycd-editions-20261018/README.md)) |
| Floppy, 16 colours (EGA), eight disks | `monkey1 ega es` (Spanish) | v4 disks; boots the same way in the session (record of S4b) | yes (17 Oct, [record](../../.verification/monkey1-editions-20261017/README.md)) |
| CD | `monkey1 vga en` (one disc with ENGLISH, FRENCH, GERMAN, ITALIAN and SPANISH folders) | v5 archive per folder, 24 CD audio tracks shared; the app opens ENGLISH and offers the others in its language list (R-3.16); texts, verb names and German Script 12's sentence words (" an ", " mit ") are each folder's own; ids, rooms, scenes and sounds equal | SPANISH: yes (18 Oct, [record](../../.verification/monkeycd-editions-20261018/README.md)); FRENCH, GERMAN, ITALIAN: yes (19 Oct, [record](../../.verification/monkeycd-editions-20261019/README.md)) |
