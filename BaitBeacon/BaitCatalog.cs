using System.Reflection;
using System.Text.Json;

namespace BaitBeacon;

public sealed record BaitResult(IReadOnlySet<uint> BaitIds, IReadOnlySet<uint> UnknownFishIds);

public sealed class BaitCatalog
{
    private sealed class Recipe
    {
        public uint B { get; set; }
        public uint[] M { get; set; } = [];
        public uint[] P { get; set; } = [];
    }

    private readonly Dictionary<uint, Recipe> recipes;

    public BaitCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BaitBeacon.Data.fish-bait.json")
            ?? throw new InvalidOperationException("Fish bait data is missing.");
        recipes = JsonSerializer.Deserialize<Dictionary<uint, Recipe>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];
    }

    public BaitResult Resolve(IEnumerable<uint> fishIds, Func<uint, bool> isBait)
    {
        var baits = new HashSet<uint>();
        var unknown = new HashSet<uint>();
        var resolved = new Dictionary<uint, bool>();
        var visiting = new HashSet<uint>();
        foreach (var id in fishIds)
            if (!Visit(id, isBait, resolved, visiting, baits)) unknown.Add(id);
        return new BaitResult(baits, unknown);
    }

    private bool Visit(uint fish, Func<uint, bool> isBait, Dictionary<uint, bool> resolved,
        HashSet<uint> visiting, HashSet<uint> baits)
    {
        if (resolved.TryGetValue(fish, out var known)) return known;
        if (!visiting.Add(fish)) return false;
        if (!recipes.TryGetValue(fish, out var recipe))
        {
            visiting.Remove(fish);
            resolved[fish] = false;
            return false;
        }
        var found = false;
        if (recipe.B != 0)
        {
            if (isBait(recipe.B))
            {
                baits.Add(recipe.B);
                found = true;
            }
            else found = Visit(recipe.B, isBait, resolved, visiting, baits);
        }
        // GatherBuddy Reborn's explicit InitialBait takes precedence for a mooch
        // route. Only inherit the source fish's bait when it has no initial bait.
        if (recipe.B == 0)
            foreach (var mooch in recipe.M) found = Visit(mooch, isBait, resolved, visiting, baits) || found;
        foreach (var predator in recipe.P) found = Visit(predator, isBait, resolved, visiting, baits) || found;
        visiting.Remove(fish);
        resolved[fish] = found;
        return found;
    }
}
