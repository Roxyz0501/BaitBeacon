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
        var visited = new HashSet<uint>();
        foreach (var id in fishIds) Visit(id, isBait, visited, baits, unknown);
        return new BaitResult(baits, unknown);
    }

    private void Visit(uint fish, Func<uint, bool> isBait, HashSet<uint> visited, HashSet<uint> baits, HashSet<uint> unknown)
    {
        if (!visited.Add(fish)) return;
        if (!recipes.TryGetValue(fish, out var recipe))
        {
            unknown.Add(fish);
            return;
        }
        if (recipe.B != 0)
        {
            if (isBait(recipe.B)) baits.Add(recipe.B);
            else Visit(recipe.B, isBait, visited, baits, unknown);
        }
        foreach (var mooch in recipe.M) Visit(mooch, isBait, visited, baits, unknown);
        foreach (var predator in recipe.P) Visit(predator, isBait, visited, baits, unknown);
        if (recipe.B == 0 && recipe.M.Length == 0 && recipe.P.Length == 0) unknown.Add(fish);
    }
}
