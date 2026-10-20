# Talkie speech: Monkey Island 2's fan talkie and mouth sync (19–20 October 2026)

| | |
|---|---|
| Copies | `Games/mi2 talkie en`, `Games/mi2 talkie es` (fan "Ultimate Talkie Edition" 0.2 beta), `Games/indy4 vga en` (CD image), `Games/indy4 talkie vga es`; data read from `D:\Juegos\Lucas` |
| Requirements | R-7.7 to R-7.18 (play, scenes), speech as in the S6/S7 Indy4 records |
| Earlier records | [indy4-play-20261017](../indy4-play-20261017/README.md), [indy4-cutscenes-20261018](../indy4-cutscenes-20261018/README.md), [monkey2-play-20261017](../monkey2-play-20261017/README.md), [monkey2-cutscenes-20261018](../monkey2-cutscenes-20261018/README.md), [monkey2-editions-20261018](../monkey2-editions-20261018/README.md) |

## How the MI2 patch stores its speech (read from its data)

- `monster.sou` (268,148,253 bytes, both copies) is laid out like Indy 4's `MONSTER.SOU`: the 8-byte `SOU ` header, then 6808 back-to-back samples, each a `VCTL` block followed by a Creative Voice File (8-bit unsigned, 22,222 Hz: VOC rate byte 0xD3). `ScummSpeechBank.ListSamples` walks all 6808 without a gap.
- Every `VCTL` block is 10 bytes: tag, size 10, and **one mouth-sync word, 0x0FFF (4095)**. Indy 4's 7029 blocks are 8 to 82 bytes (0 to 37 words; the first sample, offset 8, holds 13 15 23 26 33).
- The texts address it the Indy 4 way: four `\xFF\x0A` codes give the block's offset (operand bytes 0, 1, 4, 5) and the block size (8, 9, 12, 13), read by the existing `ScummMessageTable` code-10 path (ScummVM `handleNextCharsetCode` case 10). Example: the sign in Room 7 (object 46 verb 8) carries offset 0x00A485E0, Largo's choice "Then who keeps up the law…" 0x00B40C6E.
- The Spanish talkie's lines carry the same offsets; its bank is byte-for-byte the same size and speaks English (as the Spanish Indy 4 talkie).
- The readme's "monkey2.sof/.sog" (FLAC/Ogg) variants are for ScummVM builds; these copies hold the plain DOS `monster.sou`.

**Why the earlier records said "not played":** nothing in the code stopped it. `GameFolderScanner` already found `monster.sou` for every v5 game, `ScummGameSession` already timed lines from it, and `TalkieSpeechPlayer` is attached for any game with a bank. The S6/S7/S9 records had not checked it. This run checks it in tests and through the window.

## Mouth sync

ScummVM `engines/scumm/sound.cpp` on GitHub master, read 19 October 2026 (no runtime run):
- `startTalkSound`, plain `MONSTER.SOU` path: `num = (size - 8) >> 1` big-endian words after the `VCTL` header go into `_mouthSyncTimes`, ended by 0xFFFF; the speech timer is reset and `_mouthSyncMode = true`.
- `speechTimerHandler`/`incrementSpeechTimer`: `_curSoundPos` counts every fourth tick of the engine timer (`getTimerFrequency`, about 240 Hz for v5 per `scumm.h` PIT constants), so 60 a second, the session's jiffy.
- `isMouthSyncOff(pos)`: toggles at each time passed, starting "on"; no delay before v6.
- `processSfxQueues`: for the talking actor (below 0x80, talk animations on, in the current room): if the line has finished, or the mouth goes off while open, the talk-stop frame; if it goes on while closed, the talk-start frame.

Implemented generically (`ScummMessages.MouthSync.cs`, `ScummSpeechBank.ReadMouthSync`, wired in `ScummGameSession` after the boot): the words come from each copy's own bank. Result per game:
- **Indy 4:** the mouth follows the sample. First Indy 4 line after the Esc skip (sample offset 8, 48 jiffies, times 13 15 23 26 33): mouth closed at +24, open at +30, closed at +36 (6-jiffy frames), both copies.
- **MI2 talkie:** one word, 4095, in every block: the mouth stays open until the sample ends, then closes (plain talk animation, **labelled**: the patch gives no mouth-sync data). Before this change the mouth stayed open until the text was removed; now it closes when the voice ends, as ScummVM does.
- Lines whose length is unknown (the headless boot, unreadable samples) keep the old plain talk animation; the boot is unchanged.

## Commands

```
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~Indy4|FullyQualifiedName~Monkey2|FullyQualifiedName~Talkie|FullyQualifiedName~Speech|FullyQualifiedName~Message|FullyQualifiedName~Architecture|FullyQualifiedName~SupportConformance"
rtk proxy dotnet build .verification/talkie-speech-20261019/WindowCheck/TalkieSpeechWindowCheck.csproj
Exe\TalkieSpeechWindowCheck\Debug\net10.0-windows\TalkieSpeechWindowCheck.exe "D:\Juegos\Lucas\mi2 talkie en" <scratch> "Room 74 · Pick up voodoo key to jail" "Room 09 · Talk to barkeeper"
rtk proxy dotnet Exe/CutsceneList/Debug/net10.0/CutsceneList.dll "D:\Juegos\Lucas\<indy4 vga en | mi2 talkie en>" scan run    (before = commit c2d5f167~1 sources, after = this branch)
```

## Results

- Tests (new `ScummTalkieSpeechTests`, and `ScummMonkey2PlayTests` route assertions):
  - the four banks: 6808 / 7029 samples; MI2 blocks all 10 bytes with [4095]; Indy 4's first sample's times and `IsMouthSyncOff` at 0..36 jiffies;
  - MI2 talkie en and es on the S6 route: the sign's line starts sample 0x00A485E0 (56 jiffies) and Largo's choice 0x00B40C6E (141 jiffies), each lasting as long as its own wave; the floppy copies speak nothing;
  - Indy 4 mouth sync for one line (above), both copies; Indy 4's speech tests unchanged.
  - The filter above (plus `PlayTests` and `SoundPolish`) after merging master with the sound polish (merge 42a8141f): 200 of 200 pass, 0 skipped, 2 min 51 s; solution build without errors.
- Window check (real `MainWindow`, private `SCUMM_EXPLORER_PROFILE_ROOT` and room cache in the scratch folder, MI2 talkie en): the list finished (165 picker entries). "Room 74 · Pick up voodoo key to jail": the speech player played sample 232545265/10 (140 jiffies) as LeChuck (actor 3) says "Guybrush Threepwood^". "Room 09 · Talk to barkeeper": two lines played (32089130/10, 37 jiffies; 32102836/10, 34 jiffies), mouths closed at the end of each (actor 1 at +42 jiffies, actor 7 at +36: the first frame after the sample). Its first phase (play in Room 4 before the list) did not see the play loop start within the harness's wait, so only the scenes are evidence.
- Cutscene lists: `CutsceneList scan run` for `indy4 vga en` (431 lines) and `mi2 talkie en` (264 lines) identical before and after, timings removed. LOOM, Indy 3 and Monkey 1 have no speech bank, so the change cannot reach them.

## Limits and estimates

- Frames: the mouth changes on the first frame after a time is passed (the session's frame length), as ScummVM's per-loop `processSfxQueues` does; not compared at runtime.
- Talk scripts (`_talkScript`) and `no_talk_anim` are not modelled (as before); actors at 0x80 and above get no mouth sync.
- MI2: the patch's 4095 is a placeholder; a line longer than 68 s would close the mouth early (none checked).

## User checks (listening and looking; the agent cannot hear)

1. Open `D:\Juegos\Lucas\mi2 talkie en`, let the list finish, play "Room 09 · Talk to barkeeper" and "Room 74 · Pick up voodoo key to jail": the voices should match the texts and stop with them; mouths move while a voice plays and close when it ends.
2. Same in `mi2 talkie es` (Spanish texts, English voices).
3. Open `D:\Juegos\Lucas\indy4 vga en`, skip the prologue (Esc): in Smith's scene, Indy's and Smith's mouths should open and close with the words rather than stay open.
