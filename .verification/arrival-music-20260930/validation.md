# Music on catalog arrival

The user now hears the room's script-started music after choosing Rooms 16,24,30,36, without clicking first. Generic fix: attach the scene sound follower before visiting, and start the ordinary bounded frame clock once the catalog room is visible if scripts remain pending. Playback-driven room loads retain their existing clock. No input, forced sound ID or room-specific condition added. The arrival's estimate labels and source event certainty remain.

Decoded: entries 16 +0x009A / 24 +0x010C / 30 +0x0091 / 36 +0x00F0 start 039 [30/28/28/27]. Script 039 yields at +0x0021, then +0x0038 starts the sound. Previously catalog visits stopped at the yield until input/Continue resumed it.

ArrivalCheck final/arrival-check.txt: 8 PASS, zero failures. Actual WPF MediaOpened confirms music for all four rooms on their arrival frames with no game click; room remains selected. Synthetic catalog selection, normal playback clock, no physical input or independent listening.

DoorwayClickCheck required rooms 02,03,04,08,10,37: 16/16, zero failures (11 game clicks, five labelled direct-verb fallbacks). RoomAudit 76 rooms, zero remaining gaps (three before filling, zero after); all 76 room PNG pixel hashes unchanged, so no refresh. Audit verifies static first-visit state, while ArrivalCheck verifies App arrival frames. Blockers skipped because executor and state rules unchanged.

Core suite 249 passes from the prior task; no Core changes since that run. Main Release build zero warnings/errors. Cwd D:\Documents\Personal\Code Projects\SCUMMexplorer, command dotnet build src/SCUMMexplorer.App/SCUMMexplorer.App.csproj -c Release, executable Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe.

Final integration: BusyCheck rerun after adding arrival playback, waiting for those real arrival frames before expecting an idle scene start: busy-final/busy-check.txt 11/11. Final full committed suite with supplied LOOM audio and Indy data configured: 249/249. No broader checks needed.
