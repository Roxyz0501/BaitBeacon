using System.Text.Json;

namespace BaitBeacon;

public sealed record SavedList(string Key, string Name, string Folder, uint[] ItemIds);

public sealed class SavedLists
{
    private readonly string? root;
    private string? path;
    private DateTime lastWrite;
    private long lastLength = -1;
    private bool hasLoaded;
    private readonly FailureCircuit failures;
    public const int MaximumBytes = 8 * 1024 * 1024;
    public bool Stopped => failures.Stopped;
    public Exception? LastError => failures.LastError;
    public long ReadAttempts { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyList<SavedList> Lists { get; private set; } = [];
    public bool FileExists => path is not null && File.Exists(path);

    public SavedLists(string ownConfigDirectory, Action<Exception>? onStopped = null, TimeProvider? clock = null)
    {
        root = Directory.GetParent(ownConfigDirectory)?.FullName;
        failures = new FailureCircuit(TimeSpan.FromSeconds(3), onStopped, clock);
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

    public bool Reload(bool retry = false)
    {
        if (retry) { failures.Retry(); lastLength = -1; }
        var changed = false;
        var success = failures.Run(() =>
        {
            if (path is null || !File.Exists(path)) FindPath();
            if (!FileExists)
            {
                if (hasLoaded) throw new FileNotFoundException("The previously loaded list file is missing.");
                Error = "missing";
                return;
            }
            var stamp = File.GetLastWriteTimeUtc(path!);
            var length = new FileInfo(path!).Length;
            if (stamp == lastWrite && length == lastLength && Error is null) return;
            ReadAttempts++;
            using var stream = new FileStream(path!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var observedLength = stream.Length;
            if (observedLength > MaximumBytes) throw new IOException("List file exceeds the 8 MiB limit.");
            // Read at most the observed length; a concurrent rewrite is retried, never published partially.
            var bytes = new byte[checked((int)observedLength)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) throw new IOException("List file changed while reading.");
            using var json = JsonDocument.Parse(bytes);
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
            lastLength = length;
            hasLoaded = true;
            Error = null;
            changed = true;
        });
        if (!success && failures.LastError is not null)
            // Keep the last valid snapshot while GBR may be rewriting the file.
            Error = "read";
        return changed;
    }
}
