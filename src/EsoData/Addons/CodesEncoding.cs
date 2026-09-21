using EsoData.Lua;

namespace EsoData.Addons;

/// <summary>LibCodesCommonCode's integer and bitfield encoding, not MIME Base64.</summary>
public static class CodesEncoding
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#%";
    public static long DecodeInteger(ReadOnlySpan<char> encoded)
    {
        long value = 0;
        foreach (var c in encoded)
        {
            var digit = Alphabet.IndexOf(c);
            if (digit < 0) throw new FormatException($"Invalid LibCodes digit '{c}'.");
            value = checked(value * 64 + digit);
        }
        return value;
    }
    public static string EncodeInteger(long value, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        var chars = new char[width];
        for (var i = width - 1; i >= 0; i--) { chars[i] = Alphabet[(int)(value % 64)]; value /= 64; }
        if (value != 0) throw new ArgumentOutOfRangeException(nameof(value), "Value does not fit the field.");
        return new(chars);
    }
    public static bool? ReadBit(string data, int oneBasedIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(oneBasedIndex);
        var offset = (oneBasedIndex - 1) / 6;
        if (offset >= data.Length) return null;
        var digit = DecodeInteger(data.AsSpan(offset, 1));
        return (digit & (1L << (5 - (oneBasedIndex - 1) % 6))) != 0;
    }
    public static string? JoinChunks(object? data)
    {
        if (data is null) return null;
        if (data is string s) return s;
        if (data is not LuaTable table) throw new FormatException("Expected an encoded string or table of chunks.");
        var parts = table.Where(x => x.Key.IsNumeric).OrderBy(x => long.Parse(x.Key.Value)).ToArray();
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Key.Value != (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) || parts[i].Value is not string)
                throw new FormatException("Missing or invalid encoded chunk.");
        return string.Concat(parts.Select(x => (string)x.Value!));
    }
}
