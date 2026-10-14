# The Secret of Monkey Island

| | |
|---|---|
| Families (`GameFamily`) | `MonkeyFloppy`, `MonkeyCd` |
| Fixtures (folders under `Games`) | `monkey1 ega en` (floppy, four disks, 256 colours), `monkey1 vga en` (CD), also `monkey1 ega es`, `monkey1 vga es` |
| SCUMM version(s) and script dialect | floppy: v4 (`V4`); CD: v5 (`V5`) |
| Tracker | [SUPPORT_STATUS.md](SUPPORT_STATUS.md) |
| Script map | [MONKEY1_SCRIPT_MAP.md](MONKEY1_SCRIPT_MAP.md) |
| Other notes | none |
| Engine facts used | [resource formats](../engine/SCUMM_RESOURCE_FORMATS.md), [script reference](../engine/SCUMM_SCRIPT_MAP.md), [boot sequence](../engine/SCUMM_BOOT_SEQUENCE.md) |

## Editions

| Edition / release | Local folder | Differences that matter (opening, scripts, text, graphics, audio) | Checked in S9 |
|---|---|---|---|
| Floppy, 256 colours (VGA), four disks | `monkey1 ega en` (the folder name says EGA; the data is `DISK01.LEC` to `DISK04.LEC`) | v4 disks; the S4b reference (generic session boots to Room 33) | no |
| Floppy, 16 colours (EGA), eight disks | `monkey1 ega es` (Spanish) | v4 disks; boots the same way in the session (record of S4b) | no |
| CD | `monkey1 vga en`, `monkey1 vga es` | v5 archive, 24 CD audio tracks | no |
