Doorbell 448,32 8x8; view left 352
Selected verb 9
Frame 59, room 26: Oh, the doorbell!
Frame 100, room 26: I wonder if that's my package?
Frame 176, room 26:  
Frame 629, room 1: Can't you read the sign?
Frame 680, room 1: NOW GET OUT OF HERE!
After continuation: room 1, control True, scenes 2
Script 174 queries 484; observed post-query values 255
Script 174 begin +0000 : C8=11, C9=0; 
Script 174 instruction +0000 getClosestObjActor: C8=11, C9=0; 
Script 174 instruction +0003 isEqual: C8=11, C9=255; 
Script 174 instruction +0009 getActorRoom: C8=11, C9=255; 
Script 174 instruction +000C isNotEqual: C8=11, C9=255; 
Script 174 instruction +0012 move: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
Script 174 instruction +0023 increment: C8=11, C9=255; 
Script 174 instruction +0025 isLess: C8=11, C9=255; 
Script 174 instruction +002B move: C8=11, C9=255; 
Script 174 instruction +002E stopObjectCode: C8=255, C9=255; 
Script 174 end +002E : C8=255, C9=255; getActorRoom reads actor 11: actor 11's putActor at +0x00C6 follows a seeded pick, so what follows is an estimate (+0x0009); getActorRoom reads actor 1: actor 1's putActor at +0x028C follows a seeded pick, so what follows is an estimate (+0x0016); getActorRoom reads actor 3: the ego's arrival at Room 44 / Object 258 / Verb 0x0D +0x0013 follows a seeded pick, so what follows is an estimate (+0x0016); getActorRoom reads actor 5: actor 5's putActor at +0x0293 follows a seeded pick, so what follows is an estimate (+0x0016)
Script 174 begin +0000 : C8=11, C9=255; 
Script 174 instruction +0000 getClosestObjActor: C8=11, C9=255; 
Script 174 instruction +0003 isEqual: C8=11, C9=255; 
Script 174 instruction +0009 getActorRoom: C8=11, C9=255; 
Script 174 instruction +000C isNotEqual: C8=11, C9=255; 
Script 174 instruction +0012 move: C8=11, C9=255; 
Script 174 instruction +0016 getActorRoom: C8=11, C9=255; 
Script 174 instruction +0019 isEqual: C8=11, C9=255; 
ScummActorSnapshot { Number = 3, Room = 1, X = 440, Y = 90, WalkBox = 3, Visible = True, IgnoreBoxes = False, Unknown = , Estimate = the ego's arrival at Room 44 / Object 258 / Verb 0x0D +0x0013 follows a seeded pick, PositionKnown = True, Look = ScummActorLook { Costume = 3, Elevation = 0, Facing = 0, Frame = 4, InitFrame = 2, WalkFrame = 0, StandFrame = 1, TalkStartFrame = 5, TalkStopFrame = 4, WalkSpeedX = 8, WalkSpeedY = 2, Palette = System.Byte[], Animation = SCUMMexplorer.Core.Scumm.ScummCostumeAnimationState, Estimate = , PaletteEstimate = , Scale = 255, Direction = 3 }, Moving = 0 }
ScummActorSnapshot { Number = 11, Room = 26, X = 80, Y = 104, WalkBox = 3, Visible = False, IgnoreBoxes = False, Unknown = , Estimate = actor 11's putActorAtObject at +0x0069 follows a seeded pick, PositionKnown = True, Look = ScummActorLook { Costume = 11, Elevation = 0, Facing = 0, Frame = 4, InitFrame = 2, WalkFrame = 0, StandFrame = 1, TalkStartFrame = 5, TalkStopFrame = 4, WalkSpeedX = 8, WalkSpeedY = 2, Palette = System.Byte[], Animation = SCUMMexplorer.Core.Scumm.ScummCostumeAnimationState, Estimate = , PaletteEstimate = , Scale = 255, Direction = 3 }, Moving = 0 }
PASS: genuine boot/party/exit/key route, native Push sentence, doorbell scene and 3000-frame continuation.
