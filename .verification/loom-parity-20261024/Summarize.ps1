param([string]$Dir, [string]$Name, [int]$Max = 120)
$pattern = '^===|popup after|^\s+\[(Playback|Skip|RoomCutscene|Interact|RoomSounds|CutsceneMenu|GameStatus|GameIntro|ObjectsNever|DoorwaysNever|RoomMask|WalkBox|SessionMode|RoomNavigation|ObjectList|RoomObjectPanel|RoomEntry|Generic|Advance)|^\s+\[-\] Button|^\s+\[-\] Text "[A-Z ]+"$|^\s+\(|previewOnly|Live native'
"##### $Name"
Get-Content "$Dir/$Name/audit-*.txt" |
  Where-Object { $_ -match $pattern -and $_ -notmatch 'GenericRoomActionsHost\] Text' } |
  ForEach-Object { if ($_.Length -gt 170) { $_.Substring(0, 170) } else { $_ } } |
  Select-Object -First $Max
