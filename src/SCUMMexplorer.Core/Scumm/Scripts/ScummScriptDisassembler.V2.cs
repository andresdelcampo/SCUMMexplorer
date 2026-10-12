using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// SCUMM v1 and v2 bytecode (Maniac Mansion, Zak McKracken), from ScummVM's <c>ScummEngine_v2::setupOpcodes</c> and the <c>o2_*</c> functions
/// (script_v2.cpp, fetched 12 October 2026). The opcode byte keeps the v3 parameter bits (0x80, 0x40, 0x20 make parameter 1, 2, 3 a variable), but a
/// variable is one byte and a result variable is one byte (no 0x2000 indexing, no local or bit variables), a print carries its text directly
/// (no sub-opcodes) and a verb's name follows its definition in the script.
/// </summary>
internal static partial class ScummScriptDisassembler
{
    // Eight opcodes per row, as in ScummVM's table (the high bits of the byte keep the parameter-mode flags; the rows are 0x00, 0x08, ... 0xF8).
    private static readonly string[] V2Table =
    [
        "stopObjectCode putActor startMusic getActorRoom isGreaterEqual drawObject getActorElevation setStateIntrinsicOn",
        "isNotEqual faceActor assignVarWordIndirect setObjPreposition resourceRoutines walkActorToActor putActorAtObject ifStateIntrinsicOn",
        "getObjectOwner animateActor panCameraTo actorOps print actorFromPos getRandomNr setStateTouchable",
        "jumpRelative doSentence move setBitVar startSound ifClassOfIs walkActorTo ifStateTouchable",
        "stopMusic putActor saveLoadGame getActorY loadRoomWithEgo drawObject setVarRange setStateLocked",
        "equalZero setOwnerOf addIndirect delayVariable assignVarByte putActorInRoom delay ifStateLocked",
        "setBoxFlags getBitVar setCameraAt roomOps getDist findObject walkActorToObject setStatePickupable",
        "isLessEqual doSentence subtract waitForActor stopSound setActorElevation walkActorTo ifStatePickupable",
        "cutscene putActor startScript getActorX isLess drawObject increment setStateIntrinsicOff",
        "isEqual faceActor chainScript setObjPreposition waitForSentence walkActorToActor putActorAtObject ifStateIntrinsicOff",
        "pickupObject animateActor actorFollowCamera actorOps setObjectName actorFromPos getActorMoving setStateUntouchable",
        "beginOverride doSentence add setBitVar dummy ifClassOfIs walkActorTo ifStateUntouchable",
        "cursorCommand putActor stopScript getActorFacing loadRoomWithEgo drawObject getClosestObjActor setStateUnlocked",
        "isScriptRunning setOwnerOf subIndirect dummy getObjPreposition putActorInRoom dummy ifStateUnlocked",
        "lights getActorCostume loadRoom roomOps getDist findObject walkActorToObject setStateUnpickupable",
        "isGreater doSentence verbOps getActorWalkBox isSoundRunning setActorElevation walkActorTo ifStateUnpickupable",
        // 0x80 to 0xFF: the same, except for these rows.
        "breakHere putActor startMusic getActorRoom isGreaterEqual drawObject getActorElevation setStateIntrinsicOn",
        "isNotEqual faceActor assignVarWordIndirect setObjPreposition resourceRoutines walkActorToActor putActorAtObject ifStateIntrinsicOn",
        "getObjectOwner animateActor panCameraTo actorOps print actorFromPos getRandomNr setStateTouchable",
        "restart doSentence move setBitVar startSound ifClassOfIs walkActorTo ifStateTouchable",
        "stopObjectCode putActor saveLoadGame getActorY loadRoomWithEgo drawObject setVarRange setStateLocked",
        "notEqualZero setOwnerOf addIndirect switchCostumeSet drawSentence putActorInRoom waitForMessage ifStateLocked",
        "setBoxFlags getBitVar setCameraAt roomOps getDist findObject walkActorToObject setStatePickupable",
        "isLessEqual doSentence subtract waitForActor stopSound setActorElevation walkActorTo ifStatePickupable",
        "endCutscene putActor startScript getActorX isLess drawObject decrement setStateIntrinsicOff",
        "isEqual faceActor chainScript setObjPreposition pseudoRoom walkActorToActor putActorAtObject ifStateIntrinsicOff",
        "pickupObject animateActor actorFollowCamera actorOps setObjectName actorFromPos getActorMoving setStateUntouchable",
        "printEgo doSentence add setBitVar dummy ifClassOfIs walkActorTo ifStateUntouchable",
        "cursorCommand putActor stopScript getActorFacing loadRoomWithEgo drawObject getClosestObjActor setStateUnlocked",
        "isScriptRunning setOwnerOf subIndirect dummy getObjPreposition putActorInRoom dummy ifStateUnlocked",
        "lights getActorCostume loadRoom roomOps getDist findObject walkActorToObject setStateUnpickupable",
        "isGreater doSentence verbOps getActorWalkBox isSoundRunning setActorElevation walkActorTo ifStateUnpickupable"
    ];

    private static readonly string[][] V2Names = V2Table.Select(row => row.Split(' ')).ToArray();

    /// <summary>The handler name of a v2 opcode byte (rows 0 to 15 cover 0x00-0x7F, rows 16 to 31 cover 0x80-0xFF).</summary>
    private static string V2Name(byte opcode) => V2Names[(opcode >> 3)][opcode & 7];

    private static (string Operation, string Operands) DecodeV2Instruction(Cursor cursor, byte opcode)
    {
        var name = V2Name(opcode);
        string B(byte mask) => V2Param(cursor, 1, opcode, mask);
        string W(byte mask) => V2Param(cursor, 2, opcode, mask);
        string R() => V2Var(cursor);
        string Raw() => $"0x{cursor.ReadByte():X2}";
        string Jump(string operands)
        {
            var displacement = cursor.ReadInt16();
            var jump = $"{displacement:+#;-#;0}  ->  0x{cursor.Position + displacement:X4}";
            return string.IsNullOrEmpty(operands) ? jump : $"{operands}, {jump}";
        }

        switch (name)
        {
            case "stopObjectCode": case "breakHere": case "endCutscene": case "cutscene": case "restart":
            case "stopMusic": case "drawSentence": case "waitForMessage": case "waitForSentence": case "dummy":
            case "switchCostumeSet":
                return (name, "");
            case "jumpRelative":
            {
                var displacement = cursor.ReadInt16();
                return ("jumpRelative", $"{displacement:+#;-#;0}  ->  0x{cursor.Position + displacement:X4}");
            }
            case "putActor": case "walkActorTo": return (name, Join(B(0x80), B(0x40), B(0x20)));
            case "startMusic": case "stopSound": case "startSound": case "panCameraTo": case "setCameraAt": case "waitForActor":
            case "startScript": case "chainScript": case "stopScript": case "loadRoom": case "actorFollowCamera":
                return (name, B(0x80));
            case "getActorRoom": case "getActorElevation": case "getActorY": case "getActorX": case "getActorWalkBox": case "getActorMoving":
            case "getActorFacing": case "getActorCostume": case "isScriptRunning": case "isSoundRunning": case "getRandomNr":
            case "saveLoadGame":
                return (name, Join(R(), B(0x80)));
            case "getObjectOwner": case "getObjPreposition": case "getClosestObjActor":
                return (name, Join(R(), W(0x80)));
            case "isGreaterEqual": case "isLess": case "isGreater": case "isLessEqual": case "isNotEqual": case "isEqual":
                return (name, Jump(Join(R(), W(0x80))));
            case "equalZero": case "notEqualZero":
                return (name, Jump(R()));
            case "drawObject": { var obj = W(0x80); var x = B(0x40); var y = B(0x20); return ("drawObject", Join(obj, "at", x, y)); }
            case "setStateIntrinsicOn": case "setStateIntrinsicOff": case "setStateTouchable": case "setStateUntouchable":
            case "setStateLocked": case "setStateUnlocked": case "setStatePickupable": case "setStateUnpickupable":
                return ("setState", Join(W(0x80), name["setState".Length..]));
            case "ifStateIntrinsicOn": case "ifStateIntrinsicOff": case "ifStateTouchable": case "ifStateUntouchable":
            case "ifStateLocked": case "ifStateUnlocked": case "ifStatePickupable": case "ifStateUnpickupable":
                return ("ifState", Jump(Join(W(0x80), name["ifState".Length..])));
            case "ifClassOfIs": return ("ifClassOfIs", Jump(Join(W(0x80), B(0x40))));
            case "faceActor": case "walkActorToObject": case "putActorAtObject": return (name, Join(B(0x80), W(0x40)));
            case "walkActorToActor": return (name, Join(B(0x80), B(0x40), Raw()));
            case "animateActor": case "putActorInRoom": case "setActorElevation": return (name, Join(B(0x80), B(0x40)));
            case "assignVarWordIndirect": case "addIndirect": case "subIndirect": case "move": case "add": case "subtract":
                return (name, Join(R(), W(0x80)));
            case "assignVarByte": return (name, Join(R(), Raw()));
            case "increment": case "decrement": return (name, R());
            case "delayVariable": return (name, R());
            case "delay": { var a = cursor.ReadByte(); var b = cursor.ReadByte(); var c = cursor.ReadByte(); return ("delay", $"{a | b << 8 | c << 16}"); }
            case "setObjPreposition": return (name, Join(W(0x80), Raw()));
            case "resourceRoutines": return (name, Join(B(0x80), Raw()));
            case "actorOps": case "roomOps": return (name, Join(B(0x80), B(0x40), $"sub={Raw()}"));
            case "actorFromPos": case "findObject": return (name, Join(R(), B(0x80), B(0x40)));
            case "print": case "printEgo": return DecodeV2Print(cursor, opcode, name);
            case "setObjectName": return (name, Join(W(0x80), ReadV2PlainString(cursor)));
            case "doSentence": return ("doSentence", DecodeV2Sentence(cursor, opcode));
            case "setBitVar": return (name, Join($"0x{cursor.ReadUInt16():X4}", B(0x80), B(0x40)));
            case "getBitVar": return (name, Join(R(), $"0x{cursor.ReadUInt16():X4}", B(0x80)));
            case "loadRoomWithEgo":
            {
                var obj = W(0x80); var room = B(0x40);
                var x = (sbyte)cursor.ReadByte(); var y = (sbyte)cursor.ReadByte();
                return (name, Join(obj, room, $"walkTo=({x}, {y})"));
            }
            case "setVarRange":
            {
                var target = R();
                var count = cursor.ReadByte();
                var values = new List<string>();
                for (var i = 0; i < count; i++) values.Add(opcode >= 0x80 ? $"0x{cursor.ReadUInt16():X4}" : $"0x{cursor.ReadByte():X2}");
                return (name, Join(target, $"[{string.Join(", ", values)}]"));
            }
            case "setOwnerOf": return (name, Join(W(0x80), B(0x40)));
            case "setBoxFlags": return (name, Join(B(0x80), Raw()));
            case "getDist": return (name, Join(R(), W(0x80), W(0x40)));
            case "pickupObject": return (name, W(0x80));
            case "cursorCommand": return (name, W(0x80));
            case "lights": return (name, Join(B(0x80), Raw(), Raw()));
            case "beginOverride": { var a = Raw(); var w = $"0x{cursor.ReadUInt16():X4}"; return (name, Join(a, w)); }
            case "pseudoRoom":
            {
                var room = cursor.ReadByte();
                var resources = new List<string>();
                for (var i = 0; i < 256; i++)
                {
                    var value = cursor.ReadByte();
                    if (value == 0) return (name, Join($"0x{room:X2}", $"[{string.Join(", ", resources)}]"));
                    resources.Add($"0x{value:X2}");
                }
                throw new UnsupportedBytecodeException("pseudoRoom did not reach its zero terminator");
            }
            case "verbOps": return DecodeV2VerbOps(cursor, opcode);
            default:
                throw new UnsupportedBytecodeException($"v2 opcode 0x{opcode:X2} ({name}) is not decoded");
        }
    }

    private static string DecodeV2Sentence(Cursor cursor, byte opcode)
    {
        var verb = V2Param(cursor, 1, opcode, 0x80);
        if (verb is "0xFC" or "0xFB") return verb;
        return Join(verb, V2Param(cursor, 2, opcode, 0x40), V2Param(cursor, 2, opcode, 0x20), $"sub=0x{cursor.ReadByte():X2}");
    }

    private static (string, string) DecodeV2VerbOps(Cursor cursor, byte opcode)
    {
        var verb = cursor.ReadByte();
        switch (verb)
        {
            case 0: return ("verbOps", Join("0x00", $"[delete {V2Param(cursor, 1, opcode, 0x80)}]"));
            case 0xFF:
            {
                var id = cursor.ReadByte(); var state = cursor.ReadByte();
                return ("verbOps", Join($"0x{id:X2}", $"[state 0x{state:X2}]"));
            }
            default:
            {
                var x = cursor.ReadByte(); var y = cursor.ReadByte();
                var slot = V2Param(cursor, 1, opcode, 0x80);
                var prep = cursor.ReadByte();
                return ("verbOps", Join($"0x{verb:X2}", $"[position 0x{x:X2}, 0x{y:X2}; slot {slot}; preposition 0x{prep:X2}; name, {ReadV2VerbName(cursor)}]"));
            }
        }
    }

    private static (string, string) DecodeV2Print(Cursor cursor, byte opcode, string name)
    {
        var actor = name == "printEgo" ? "ego" : V2Param(cursor, 1, opcode, 0x80);
        // ScummEngine_v2::decodeParseString: a byte with bit 7 set is followed by a space; 1 to 3 are text controls (newline, keep text, wait), 4 to 7
        // carry one more byte; the result is the modern 0xFF-prefixed form, which the shared string escapes and ScummTextDecoder read.
        var bytes = new List<byte>();
        for (var i = 0; i < 4096; i++)
        {
            var raw = cursor.ReadByte();
            if (raw == 0) return (name, Join(actor, $"[text \"{EscapeText(bytes)}\"]"));
            var c = (byte)(raw & 0x7F);
            if (c < 8)
            {
                bytes.Add(0xFF);
                bytes.Add(c);
                if (c > 3) { bytes.Add(cursor.ReadByte()); bytes.Add(0); }
            }
            else bytes.Add(c);
            if ((raw & 0x80) != 0) bytes.Add((byte)' ');
        }
        throw new UnsupportedBytecodeException("v2 print text did not reach its zero terminator within 4096 bytes");
    }

    /// <summary>A verb's label: the stored name is padded with <c>@</c> characters (and spaces) to the width of the verb's slot; the padding is dropped.</summary>
    private static string ReadV2VerbName(Cursor cursor)
    {
        var plain = ReadV2PlainString(cursor);
        return "\"" + plain[1..^1].Replace("@", "").Trim() + "\"";
    }

    /// <summary>An object or verb name in a v2 script: plain bytes up to a zero.</summary>
    private static string ReadV2PlainString(Cursor cursor)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < 256; i++)
        {
            var value = cursor.ReadByte();
            if (value == 0) return $"\"{EscapeText(bytes)}\"";
            bytes.Add(value);
        }
        throw new UnsupportedBytecodeException("v2 name did not reach its zero terminator within 256 bytes");
    }

    private static string V2Var(Cursor cursor) => $"var[0x{cursor.ReadByte():X3}]";

    private static string V2Param(Cursor cursor, int width, byte opcode, byte variableMask)
    {
        if ((opcode & variableMask) != 0) return V2Var(cursor);
        return width == 1 ? $"0x{cursor.ReadByte():X2}" : $"0x{cursor.ReadUInt16():X4}";
    }
}
