using System.Text.Json;

namespace BaitBeacon;

/// <summary>One synchronous writer for small configuration snapshots; no timers/tasks survive disposal.</summary>
public sealed class ConfigurationStore : IDisposable
{
    private sealed record Snapshot(int Version, string? Language, string SelectedList,
        bool HighlightEnabled, bool HighlightInventory, bool HighlightSaddlebag, bool HighlightRetainer)
    {
        public static Snapshot From(Configuration c) => new(c.Version, c.Language, c.SelectedList,
            c.HighlightEnabled, c.HighlightInventory, c.HighlightSaddlebag, c.HighlightRetainer);
        public Configuration ToConfig() => new() { Version = Version, Language = Language, SelectedList = SelectedList,
            HighlightEnabled = HighlightEnabled, HighlightInventory = HighlightInventory,
            HighlightSaddlebag = HighlightSaddlebag, HighlightRetainer = HighlightRetainer };
    }

    private readonly object gate = new();
    private readonly string path;
    private readonly TimeProvider time;
    private readonly Action<Exception>? onStopped;
    private readonly Action<Exception>? onFinalFailure;
    private readonly Action<string>? beforeCommit;
    private readonly Action<Stream, byte[]>? writePayload;
    private Snapshot current;
    private Snapshot? saved;
    private Snapshot? attemptedFinal;
    private long checkedAt;
    private int failures;
    private bool disposed;
    private bool writing;
    private bool notified;
    public bool LoadBlocked { get; private set; }
    public Configuration Configuration { get; }
    public bool Recovered { get; private set; }
    public string? LoadWarning { get; private set; }
    public Exception? LastError { get; private set; }
    public bool Stopped { get { lock (gate) return failures >= 3; } }
    public bool Dirty { get { lock (gate) return current != saved; } }
    public long SaveCount { get; private set; }
    public long LogicalBytesWritten { get; private set; }
    public long BackupBytesCreated { get; private set; }
    public long SaveAttempts { get; private set; }
    public string BackupDirectory => path + ".backups";

    public ConfigurationStore(string path, Action<Exception>? onStopped = null,
        TimeProvider? clock = null, Action<string>? beforeCommit = null, Action<Exception>? onFinalFailure = null,
        Action<Stream, byte[]>? writePayload = null)
    {
        this.path = path;
        this.onStopped = onStopped;
        this.onFinalFailure = onFinalFailure;
        this.beforeCommit = beforeCommit;
        this.writePayload = writePayload;
        time = clock ?? TimeProvider.System;
        checkedAt = time.GetTimestamp();
        Configuration = Load();
        current = Snapshot.From(Configuration);
    }

    // A UI change captures seven small fields, not JSON. Reverting edits cancels pending writes.
    public void Update(Configuration config)
    {
        lock (gate)
        {
            if (disposed) return;
            var next = Snapshot.From(config);
            if (next != current) attemptedFinal = null;
            current = next;
        }
    }

    public void Tick()
    {
        lock (gate)
        {
            if (disposed || time.GetElapsedTime(checkedAt) < TimeSpan.FromSeconds(60)) return;
            checkedAt = time.GetTimestamp();
            if (failures < 3) Save();
        }
    }

    public void Retry()
    {
        lock (gate)
        {
            if (disposed) return;
            if (LoadBlocked)
            {
                LoadBlocked = false;
                LoadWarning = null;
                LastError = null;
                var loaded = Load();
                if (LoadBlocked) return;
                Configuration.Version = loaded.Version;
                Configuration.Language = loaded.Language;
                Configuration.SelectedList = loaded.SelectedList;
                Configuration.HighlightEnabled = loaded.HighlightEnabled;
                Configuration.HighlightInventory = loaded.HighlightInventory;
                Configuration.HighlightSaddlebag = loaded.HighlightSaddlebag;
                Configuration.HighlightRetainer = loaded.HighlightRetainer;
                current = Snapshot.From(loaded);
            }
            failures = 0;
            notified = false;
            attemptedFinal = null;
            LastError = null;
            checkedAt = time.GetTimestamp();
            Save();
        }
    }

    public void Flush()
    {
        lock (gate)
        {
            if (disposed || writing || current == attemptedFinal) return;
            attemptedFinal = current;
            Save(); // One final attempt even after automatic retries stopped; duplicate lifecycle events coalesce.
            if (current != saved && LastError is not null && !notified)
            {
                notified = true;
                onFinalFailure?.Invoke(LastError);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            try { Flush(); }
            finally { disposed = true; }
        }
    }

    private void Save()
    {
        if (writing || LoadBlocked || current == saved) return;
        writing = true;
        var snapshot = current;
        string? temp = null;
        try
        {
            SaveAttempts++;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot);
            temp = WriteTemporary(bytes);
            beforeCommit?.Invoke(temp);
            if (!TryRead(temp, out _)) throw new InvalidDataException("Temporary configuration was corrupted.");
            string? backup = null;
            long backupBytes = 0;
            if (File.Exists(path))
            {
                if (TryRead(path, out _))
                {
                    Directory.CreateDirectory(BackupDirectory);
                    var number = Backups().Select(p => long.Parse(Path.GetFileNameWithoutExtension(p))).DefaultIfEmpty(0).Max() + 1;
                    backup = Path.Combine(BackupDirectory, $"{number:D20}.json");
                    backupBytes = new FileInfo(path).Length;
                }
                else PreserveCorrupt();
                File.Replace(temp, path, backup);
            }
            else File.Move(temp, path);
            temp = null;
            saved = snapshot;
            SaveCount++;
            BackupBytesCreated += backupBytes;
            failures = 0;
            notified = false;
            LastError = null;
            // Retention follows a successful atomic replacement. A cleanup failure cannot undo a save.
            try { foreach (var old in Backups().Where(p => TryRead(p, out _)).Skip(3)) File.Delete(old); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        catch (Exception ex)
        {
            LastError = ex;
            if (++failures == 3 && !notified) { notified = true; onStopped?.Invoke(ex); }
        }
        finally
        {
            if (temp is not null)
            {
                try { File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            writing = false;
        }
    }

    private Configuration Load()
    {
        if (TryRead(path, out var config, out var readError))
        {
            saved = Snapshot.From(config!);
            return config!;
        }
        // An unreadable primary is not evidence of corruption: never replace it with older/default data.
        if (readError is UnauthorizedAccessException
            || (readError is IOException && readError is not FileNotFoundException && readError is not DirectoryNotFoundException))
        {
            LoadBlocked = true;
            failures = 3;
            LastError = readError;
            LoadWarning = readError.Message;
            return new Configuration();
        }
        try
        {
            foreach (var backup in Backups())
            {
                if (!TryRead(backup, out config)) continue;
                // Keep recovery in memory even if the disk cannot currently be repaired.
                Recovered = true;
                try
                {
                    var temp = WriteTemporary(JsonSerializer.SerializeToUtf8Bytes(Snapshot.From(config!)));
                    try
                    {
                        if (File.Exists(path)) { PreserveCorrupt(); File.Replace(temp, path, null); }
                        else File.Move(temp, path);
                        saved = Snapshot.From(config!);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                }
                catch (Exception ex) { LoadWarning = ex.Message; }
                return config!;
            }
        }
        catch (Exception ex) { LoadWarning = ex.Message; }
        if (File.Exists(path)) LoadWarning ??= "No valid configuration or backup.";
        return new Configuration();
    }

    private string WriteTemporary(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                try
                {
                    if (writePayload is null) stream.Write(bytes);
                    else writePayload(stream, bytes);
                }
                finally { LogicalBytesWritten += stream.Position; }
                stream.Flush(true);
            }
            if (!TryRead(temp, out _)) throw new InvalidDataException("Configuration validation failed.");
            return temp;
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private void PreserveCorrupt() => File.Copy(path, path + ".corrupt." + Guid.NewGuid().ToString("N"));

    private IEnumerable<string> Backups() => Directory.Exists(BackupDirectory)
        ? Directory.EnumerateFiles(BackupDirectory, "*.json")
            .Where(p => long.TryParse(Path.GetFileNameWithoutExtension(p), out _))
            .OrderByDescending(p => Path.GetFileName(p), StringComparer.Ordinal)
        : [];

    private static bool TryRead(string file, out Configuration? config) => TryRead(file, out config, out _);

    private static bool TryRead(string file, out Configuration? config, out Exception? error)
    {
        config = null;
        error = null;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 1024 * 1024) return false;
            using var json = JsonDocument.Parse(stream);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v) || v != 1) return false;
            config = root.Deserialize<Configuration>();
            return config is not null && config.SelectedList is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { error = ex; return false; }
    }
}
