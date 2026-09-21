using System.Globalization;
using System.Text;

namespace EsoData.Lua;

/// <summary>Reads data-only Lua assignments. Never executes Lua code.</summary>
public static class SavedVariables
{
    public static LuaTable Read(string path) => Parse(File.ReadAllText(path));
    public static LuaTable Parse(string text) => new Parser(text).Document();
    public static LuaTable ParseTable(string text) => new Parser(text).SingleTable();
    internal static LuaTable TablePrefix(string text) => new Parser(text).Table(0);

    private sealed class Parser(string text)
    {
        private int position;
        private char Current => position < text.Length ? text[position] : '\0';
        private FormatException Error(string message) => new($"{message} at character {position}.");
        private void Require(char c)
        {
            Skip();
            if (Current != c) throw Error($"Expected '{c}'");
            position++;
        }
        private bool Starts(string s) => text.AsSpan(position).StartsWith(s, StringComparison.Ordinal);
        private void Skip()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(Current) || Current == '\uFEFF') { position++; continue; }
                if (!Starts("--")) break;
                position += 2;
                if (LongBracket(out var equals)) ReadLong(equals);
                else while (position < text.Length && Current != '\n') position++;
            }
        }
        public LuaTable Document()
        {
            var root = new LuaTable();
            Skip();
            while (position < text.Length)
            {
                var name = Identifier();
                Require('=');
                root.Set(new(name), Value(0));
                Skip();
                if (Current == ';') position++;
                Skip();
            }
            return root;
        }
        public LuaTable SingleTable()
        {
            var value = Table(0);
            Skip();
            if (position != text.Length) throw Error("Unexpected code after table");
            return value;
        }
        private string Identifier()
        {
            Skip();
            var start = position;
            if (!(char.IsAsciiLetter(Current) || Current == '_')) throw Error("Expected identifier");
            while (char.IsAsciiLetterOrDigit(Current) || Current == '_') position++;
            return text[start..position];
        }
        private object? Value(int depth)
        {
            Skip();
            if (Current == '{') return Table(depth);
            if (Current is '"' or '\'') return Quoted();
            if (LongBracket(out var equals)) return ReadLong(equals);
            if (char.IsAsciiLetter(Current) || Current == '_') return Identifier() switch
            {
                "true" => true, "false" => false, "nil" => null,
                var name => throw Error($"Executable or unsupported Lua value '{name}'")
            };
            var start = position;
            while (char.IsAsciiLetterOrDigit(Current) || Current is '+' or '-' or '.') position++;
            if (start == position) throw Error("Expected data value");
            var token = text[start..position];
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(token.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out integer)) return integer;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) return number;
            throw Error($"Invalid number '{token}'");
        }
        public LuaTable Table(int depth)
        {
            if (depth > 128) throw Error("Table nesting is too deep");
            Require('{');
            var result = new LuaTable();
            long arrayIndex = 1;
            Skip();
            while (Current != '}')
            {
                if (position >= text.Length) throw Error("Unterminated table");
                LuaKey key;
                object? value;
                if (Current == '[' && !LongBracket(out _))
                {
                    position++;
                    var rawKey = Value(depth + 1);
                    key = rawKey switch
                    {
                        string s => new(s),
                        long n => new(n.ToString(CultureInfo.InvariantCulture), true),
                        _ => throw Error("Only string and integer table keys are supported")
                    };
                    Require(']'); Require('=');
                    value = Value(depth + 1);
                }
                else
                {
                    var saved = position;
                    string? name = null;
                    if (char.IsAsciiLetter(Current) || Current == '_') name = Identifier();
                    Skip();
                    if (name is not null && Current == '=')
                    {
                        position++;
                        key = new(name);
                    }
                    else
                    {
                        position = saved;
                        key = new((arrayIndex++).ToString(CultureInfo.InvariantCulture), true);
                    }
                    value = Value(depth + 1);
                }
                result.Set(key, value);
                Skip();
                if (Current is ',' or ';') { position++; Skip(); }
                else if (Current != '}') throw Error("Expected table separator");
            }
            position++;
            return result;
        }
        private bool LongBracket(out int equals)
        {
            equals = 0;
            if (Current != '[') return false;
            var end = position + 1;
            while (end < text.Length && text[end] == '=') { equals++; end++; }
            return end < text.Length && text[end] == '[';
        }
        private string ReadLong(int equals)
        {
            position += equals + 2;
            if (Current == '\r') { position++; if (Current == '\n') position++; }
            else if (Current == '\n') position++;
            var end = text.IndexOf("]" + new string('=', equals) + "]", position, StringComparison.Ordinal);
            if (end < 0) throw Error("Unterminated long string/comment");
            var result = text[position..end];
            position = end + equals + 2;
            return result;
        }
        private string Quoted()
        {
            var quote = Current;
            position++;
            var bytes = new List<byte>();
            while (position < text.Length && Current != quote)
            {
                if (Current != '\\')
                {
                    var start = position;
                    while (position < text.Length && Current != quote && Current != '\\') position++;
                    bytes.AddRange(Encoding.UTF8.GetBytes(text[start..position]));
                    continue;
                }
                position++;
                if (char.IsAsciiDigit(Current))
                {
                    var start = position;
                    while (position - start < 3 && char.IsAsciiDigit(Current)) position++;
                    if (!byte.TryParse(text[start..position], out var b)) throw Error("Invalid byte escape");
                    bytes.Add(b); continue;
                }
                var c = Current; position++;
                if (c == 'z') { while (char.IsWhiteSpace(Current)) position++; continue; }
                if (c == 'x')
                {
                    if (position + 2 > text.Length || !byte.TryParse(text.AsSpan(position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                        throw Error("Invalid hexadecimal escape");
                    bytes.Add(b); position += 2; continue;
                }
                if (c == '\r') { if (Current == '\n') position++; c = '\n'; }
                var escaped = c switch
                {
                    'n' => '\n', 'r' => '\r', 't' => '\t', 'a' => '\a', 'b' => '\b',
                    'f' => '\f', 'v' => '\v', '\\' => '\\', '\'' => '\'', '"' => '"', '\n' => '\n',
                    _ => throw Error($"Unsupported escape '\\{c}'")
                };
                bytes.Add((byte)escaped);
            }
            if (Current != quote) throw Error("Unterminated string");
            position++;
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
