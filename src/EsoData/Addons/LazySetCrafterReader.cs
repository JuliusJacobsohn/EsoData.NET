using EsoData.Items;
using EsoData.Lua;
using EsoData.Models;

namespace EsoData.Addons;

public sealed record CraftingRequest(string Scope, ItemLink Link, long Quantity, long? Reference, LuaTable Raw);
public sealed record CraftingQueueData(SourceInfo Source, IReadOnlyList<CraftingRequest> Requests, LuaTable Raw);

/// <summary>Reads saved active queues; favorites remain available in Raw.</summary>
public static class LazySetCrafterReader
{
    public static CraftingQueueData Read(string path) => Parse(SavedVariables.Read(path), ReaderSupport.Source("DolgubonsLazySetCrafter", path));
    public static CraftingQueueData Parse(LuaTable document, SourceInfo? source = null)
    {
        var root = document.Table("dolgubonslazysetcraftersavedvars")
            ?? throw new FormatException("Dolgubon's saved variables are missing.");
        var requests = new List<CraftingRequest>();
        foreach (var world in root.Tables())
        foreach (var account in world.Value.Tables())
        foreach (var scope in account.Value.Tables())
        foreach (var request in scope.Value.Table("queue")?.Tables() ?? [])
            if (ItemLink.TryParse(request.Value.String("Link") ?? "", out var link))
                requests.Add(new($"{world.Key.Value}/{account.Key.Value}/{scope.Key.Value}", link!,
                    LuaTable.AsInteger(request.Value.Table("Quantity")?[1]) ?? 1, request.Value.Integer("Reference"), request.Value));
        return new(source ?? new("DolgubonsLazySetCrafter"), requests, document);
    }
}
