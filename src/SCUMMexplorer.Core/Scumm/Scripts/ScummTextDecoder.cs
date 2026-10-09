using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Turns the disassembler's escaped SCUMM byte strings into DOS CP437 text.</summary>
public static class ScummTextDecoder
{
    private static readonly Encoding Cp437 = CreateCp437();
    private static readonly Regex LiteralPattern = new("\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.Compiled);

    public static IReadOnlyList<string> DecodeLiterals(string operands)
    {
        var results = new List<string>();
        foreach (Match match in LiteralPattern.Matches(operands))
        {
            var bytes = ParseBytes(match.Groups[1].Value);
            results.Add(DecodeBytes(bytes));
        }
        return results;
    }

    public static string DecodeRawString(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder();
        for (var i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            if (value == 0xFF && i + 1 < bytes.Length)
            {
                var control = bytes[++i];
                var rawBytes = new byte[bytes.Length];
                bytes.CopyTo(rawBytes);
                AppendControl(output, control, rawBytes, ref i);
            }
            else output.Append(DecodeByte(value));
        }
        return output.ToString();
    }

    /// <summary>The bytes of a disassembler string literal's escaped contents (without the quotes).</summary>
    public static byte[] ParseEscaped(string value) => ParseBytes(value).ToArray();

    /// <summary>Charset glyph codes as DOS CP437 text.</summary>
    public static string DecodeGlyphs(IEnumerable<byte> glyphs) => string.Concat(glyphs.Select(DecodeByte));

    /// <summary>
    /// ScummVM <c>convertMessageToString</c> for v3: codes 1, 2, 3 and 8 are kept,
    /// 9, 10, 12, 13 and 14 are kept with their operand, 4 writes a variable's
    /// value, and '@' (a filler) is dropped. Codes 5, 6 and 7 (a verb, a name, a
    /// string) are not modelled; <paramref name="note"/> says so.
    /// </summary>
    public static List<byte> ConvertMessage(IReadOnlyList<byte> text, IReadOnlyDictionary<string, ushort> variables, out string? note) =>
        ConvertMessage(text, variables, null, out note);

    /// <summary>
    /// A variable number as ScummVM <c>readVar</c> reads it (v3): 0x8000 a bit
    /// variable, 0x4000 a local of the running script (the low 12 bits), else a
    /// global word; null for an indexed (0x2000) number, which is not modelled here.
    /// </summary>
    public static string? VariableKey(int number) =>
        (number & 0x2000) != 0 ? null
        : (number & 0x8000) != 0 ? $"bit[0x{number & 0x7FFF:X3}]"
        : (number & 0x4000) != 0 ? $"local[0x{number & 0x0FFF:X3}]"
        : $"var[0x{number & 0x1FFF:X3}]";

    /// <summary>
    /// <see cref="ConvertMessage(IReadOnlyList{byte}, IReadOnlyDictionary{string, ushort}, out string?)"/>
    /// with code 6 (<c>convertNameMessage</c>): the name of the object whose number
    /// the variable holds, from <paramref name="objectName"/>. Actors' names (numbers below 13) are not modelled.
    /// </summary>
    public static List<byte> ConvertMessage(IReadOnlyList<byte> text, IReadOnlyDictionary<string, ushort> variables,
        Func<int, byte[]?>? objectName, out string? note)
    {
        note = null;
        var output = new List<byte>();
        for (var i = 0; i < text.Count; i++)
        {
            var c = text[i];
            if (c == 0) break;
            if (c != 0xFF)
            {
                if (c != (byte)'@') output.Add(c);
                continue;
            }
            if (i + 1 >= text.Count) break;
            var code = text[++i];
            if (code is 1 or 2 or 3 or 8)
            {
                output.Add(0xFF);
                output.Add(code);
                continue;
            }
            if (i + 2 >= text.Count) break;
            var value = text[i + 1] | (text[i + 2] << 8);
            switch (code)
            {
                case 4:
                    if (TryReadVariable(value, variables, out var number))
                        output.AddRange(Encoding.ASCII.GetBytes(((short)number).ToString(CultureInfo.InvariantCulture)));
                    else note = $"the text shows the value of variable 0x{value:X4}, which is unknown here";
                    break;
                case 6:
                    // convertNameMessage: readVar, then getObjOrActorName (numbers below _numActors are actors).
                    if (!TryReadVariable(value, variables, out var named))
                        note = $"the text shows the name of the object in variable 0x{value:X4}, which is unknown here";
                    else if (named == 0) { }
                    else if (named < ScummActorTable.Count)
                        note = $"the text shows actor {named}'s name, which is not modelled and left out";
                    else if (objectName?.Invoke(named) is { } name)
                        output.AddRange(ConvertMessage(name, variables, null, out _));
                    else
                        note = $"the text shows object {named}'s name, which the preview does not have (the object is not in the current room)";
                    break;
                case 5 or 7:
                    note = $"the text includes {(code == 5 ? "a verb's name" : "a string")} (code {code}), which is not modelled and left out";
                    break;
                case 9 or 10 or 12 or 13 or 14:
                    output.Add(0xFF);
                    output.Add(code);
                    output.Add(text[i + 1]);
                    output.Add(text[i + 2]);
                    break;
                default:
                    // An invalid code: ScummVM drops the two bytes and reads on.
                    continue;
            }
            i += 2;
        }
        return output;
    }

    private static bool TryReadVariable(int number, IReadOnlyDictionary<string, ushort> variables, out ushort value)
    {
        value = 0;
        var key = VariableKey(number);
        if (key is null) return false;
        if (!key.StartsWith("bit", StringComparison.Ordinal)) return variables.TryGetValue(key, out value);
        var bit = number & 0x7FFF;
        if (!variables.TryGetValue($"var[0x{bit >> 4:X3}]", out var word)) return false;
        value = (ushort)((word >> (bit & 15)) & 1);
        return true;
    }

    private static List<byte> ParseBytes(string value)
    {
        var result = new List<byte>();
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                if (value[i + 1] == 'x' && i + 3 < value.Length &&
                    byte.TryParse(value.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                {
                    result.Add(hex);
                    i += 3;
                    continue;
                }
                result.Add((byte)value[++i]);
                continue;
            }
            result.Add((byte)value[i]);
        }
        return result;
    }

    private static string DecodeBytes(IReadOnlyList<byte> bytes)
    {
        var output = new StringBuilder();
        for (var i = 0; i < bytes.Count; i++)
        {
            var value = bytes[i];
            if (value == 0xFF && i + 1 < bytes.Count)
            {
                var control = bytes[++i];
                AppendControl(output, control, bytes, ref i);
            }
            else output.Append(DecodeByte(value));
        }
        return output.ToString();
    }

    private static string DecodeByte(byte value) => value < 0x80 ? ((char)value).ToString() : Cp437.GetString([value]);

    private static void AppendControl(StringBuilder output, byte control, IReadOnlyList<byte> bytes, ref int index)
    {
        if (control == 1) { output.AppendLine(); return; }
        if (control == 2) { output.Append("[continue]"); return; }
        if (control == 3) { output.Append("[pause]"); return; }
        output.Append($"[text control {control:X2}");
        if (control is not (1 or 2 or 3 or 8))
        {
            for (var parameter = 0; parameter < 2 && index + 1 < bytes.Count; parameter++)
                output.Append(parameter == 0 ? ": " : " ").Append(bytes[++index].ToString("X2", CultureInfo.InvariantCulture));
        }
        output.Append(']');
    }

    private static Encoding CreateCp437()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(437);
    }
}
