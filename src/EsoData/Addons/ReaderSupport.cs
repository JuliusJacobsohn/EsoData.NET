using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

internal static class ReaderSupport
{
    public static SourceInfo Source(string addon, string path) => new(addon, Path.GetFullPath(path), File.GetLastWriteTimeUtc(path));
    public static DateTimeOffset? Time(long? seconds) => seconds is >= -62135596800 and <= 253402300799
        ? DateTimeOffset.FromUnixTimeSeconds(seconds.Value) : null;
    public static string Text(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public static int? Int(LuaTable table, string key) => table.Integer(key) is long n ? checked((int)n) : null;
    public static IEnumerable<LuaTable> Descendants(LuaTable table)
    {
        yield return table;
        foreach (var child in table.Tables())
            foreach (var nested in Descendants(child.Value)) yield return nested;
    }
}
