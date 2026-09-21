using System.Collections;
using System.Globalization;

namespace EsoData.Lua;

/// <summary>A Lua table retaining the distinction between string and numeric keys.</summary>
public sealed class LuaTable : IEnumerable<KeyValuePair<LuaKey, object?>>
{
    private readonly Dictionary<LuaKey, object?> values = [];
    public int Count => values.Count;
    public object? this[string key] => Get(new(key));
    public object? this[long key] => Get(new(key.ToString(CultureInfo.InvariantCulture), true));
    public object? Get(LuaKey key) => values.GetValueOrDefault(key);
    public void Set(LuaKey key, object? value)
    {
        if (value is null) values.Remove(key);
        else values[key] = value;
    }
    public string? String(string key) => this[key] as string;
    public long? Integer(string key) => AsInteger(this[key]);
    public bool? Boolean(string key) => this[key] as bool?;
    public LuaTable? Table(string key) => this[key] as LuaTable;
    public IEnumerable<KeyValuePair<LuaKey, LuaTable>> Tables() => values
        .Where(x => x.Value is LuaTable).Select(x => new KeyValuePair<LuaKey, LuaTable>(x.Key, (LuaTable)x.Value!));
    public IEnumerable<object?> ArrayValues() => values.Where(x => x.Key.IsNumeric)
        .OrderBy(x => decimal.Parse(x.Key.Value, CultureInfo.InvariantCulture)).Select(x => x.Value);
    public IEnumerator<KeyValuePair<LuaKey, object?>> GetEnumerator() => values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public static long? AsInteger(object? value) => value switch
    {
        long n => n,
        double n when n == Math.Truncate(n) && n >= long.MinValue && n < (double)long.MaxValue => (long)n,
        string s when long.TryParse(s, CultureInfo.InvariantCulture, out var n) => n,
        _ => null
    };
}

public readonly record struct LuaKey(string Value, bool IsNumeric = false);
