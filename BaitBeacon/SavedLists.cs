using System.Text.Json;

namespace BaitBeacon;

public sealed record SavedList(string Key, string Name, string Folder, uint[] ItemIds);

public sealed class SavedLists
{
    private readonly string? root;
    private string? path;
    private DateTime lastWrite;
    public string? Error { get; private set; }
    public IReadOnlyList<SavedList> Lists { get; private set; } = [];
    public bool FileExists => path is not null && File.Exists(path);

    public SavedLists(string ownConfigDirectory)
    {
        root = Directory.GetParent(ownConfigDirectory)?.FullName;
        Reload();
    }

    private void FindPath()
    {
        if (root is null) return;
        // Dalamud stores this companion data in a sibling plugin config directory.
        var directory = Directory.EnumerateDirectories(root)
            .FirstOrDefault(x => string.Equals(Path.GetFileName(x), "GatherbuddyReborn", StringComparison.OrdinalIgnoreCase));
        path = directory is null ? null : Path.Combine(directory, "auto_gather_lists.json");
    }

    public void Reload()
    {
        if (path is null || !File.Exists(path)) FindPath();
        if (!FileExists)
        {
            Lists = [];
            Error = "missing";
            return;
        }

        try
        {
            var stamp = File.GetLastWriteTimeUtc(path!);
            if (stamp == lastWrite && Error is null) return;
            using var stream = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var json = JsonDocument.Parse(stream);
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected an array.");
            var parsed = new List<SavedList>();
            foreach (var row in json.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("Name", out var nameValue)) continue;
                var name = nameValue.GetString() ?? string.Empty;
                if (name.Length == 0) continue;
                var folder = row.TryGetProperty("FolderPath", out var folderValue) ? folderValue.GetString() ?? string.Empty : string.Empty;
                var disabled = new HashSet<uint>();
                if (row.TryGetProperty("EnabledItems", out var enabledValue) && enabledValue.ValueKind == JsonValueKind.Object)
                    foreach (var item in enabledValue.EnumerateObject())
                        if (uint.TryParse(item.Name, out var id) && item.Value.ValueKind == JsonValueKind.False)
                            disabled.Add(id);
                var ids = row.TryGetProperty("ItemIds", out var items) && items.ValueKind == JsonValueKind.Array
                    ? items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number)
                        .Select(x => x.GetUInt32()).Where(x => x != 0 && !disabled.Contains(x)).Distinct().ToArray()
                    : [];
                parsed.Add(new SavedList(folder + "/" + name, name, folder, ids));
            }
            Lists = parsed;
            lastWrite = stamp;
            Error = null;
        }
        catch (Exception)
        {
            // Keep the last valid snapshot while GBR may be rewriting the file.
            Error = "read";
        }
    }
}
