# Room 02 mountain mask investigation

Source investigation only; no geometry, box mask, actor offset or artwork was changed. Minor report: Bobbin hides a few pixels early near the mountain toward the village.

Decoded: Room 02 boxes 0-2 have mask 0; boxes 3-14 have mask 1. The renderer chooses the source walk-box plane (unless actor classes 20/21 override it), composes room and active object masks in paint order, then clips individual costume pixels at their actual room coordinates. There is no mountain-specific early-hide threshold to adjust.

MaskProbe samples only the ego on the game-input exit route and compares source-mask clipping with the same pose drawn unmasked. No actor/class/position/mask override. In one measured section on box 14, costume 3, frame 184 at (554,111) removes 1 pixel; frame 185 (552,111) removes 4; frame 186 (550,110) removes 7; frame 187 (548,110) removes 23; frame 188 (546,109) removes all 24. Source mask, actor footprint and pose are the evidence to compare, not an arbitrary desired X cutoff. Final route remains Room 02 after the three assumed clicks; this probe does not establish completed doorway navigation.

Build zero warnings/errors. Core/App unchanged, so prior 249 tests and arrival audit/doorway checks remain current. No WPF screenshot or ScummVM runtime comparison made. Correctness of the reported few-pixel difference remains unresolved until the same actor coordinate, walk frame, costume, camera and source mask can be compared with ScummVM. Do not shift masks or actors merely to make the silhouette look later.

Reproduce: dotnet run --project .verification/room02-mountain-mask-20260930/MaskProbe/MaskProbe.csproj -- Games/loom. The committed trace includes box corners, flags/masks and per-frame clipped/unmasked pixel counts. Earlier build attempt used a wrong probe constructor argument; final report comes from the corrected build.
