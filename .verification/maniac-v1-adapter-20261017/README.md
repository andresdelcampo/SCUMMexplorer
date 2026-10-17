# Classic v1 data-source groundwork

ScummV1DataSource uses the existing ScummV1Index fixed layouts (Maniac: 800 objects/200 scripts; Zak: 775/155), object bytes beginning at index offset 2, and the V1 script dialect. Shared old room object/verb/entry/exit and walkbox parsing is inherited from the v2 adapter; classic dimensions are byte 4 and byte 5 multiplied by 8, and images use the existing tiled decoder and game colour table.

Source basis: ScummVM resource_v2.cpp `readClassicIndexFile`, room.cpp `setupRoomSubBlocks`, object.cpp old object headers, and gfx_v1.cpp tiled graphics; existing repository readers document these layouts. No ScummVM executable was run.

Validation: `dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV1DataSourceTests|FullyQualifiedName~ScummManiacEnhancedSessionTests" -v minimal`: 3 passed, 0 failed. Both classic fixtures from TestGames.Root were present; every discovered room's dimensions match the standalone renderer, every object's verb entry decodes, all image pixels are EGA indices, and index owners/states match stored bytes. Enhanced adapter regression included. NU1900 reflects unavailable vulnerability-feed networking.

This is adapter groundwork, not a boot or S4b completion claim. Factory wiring and classic runtime semantics remain coordinated with the parent.
