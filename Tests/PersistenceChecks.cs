using System.Text.Json;
using BaitBeacon;

internal static class PersistenceChecks
{
    private sealed class Clock : TimeProvider
    {
        private long seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => seconds;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 22, 23, 59, 30, TimeSpan.Zero).AddSeconds(seconds);
        public void Advance(int value) => seconds += value;
    }

    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    public static void Run()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "BaitBeaconPersistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "config.json");
            var initial = new Configuration { Language = "ja" };
            File.WriteAllText(path, JsonSerializer.Serialize(initial));
            var clock = new Clock();
            using (var store = new ConfigurationStore(path, clock: clock))
            {
                for (var i = 0; i < 480; i++) { clock.Advance(60); store.Tick(); }
                Check(store.SaveCount == 0 && store.LogicalBytesWritten == 0, "8h unchanged settings: zero writes/bytes");
                var c = store.Configuration;
                for (var i = 0; i < 600; i++) { c.SelectedList = "drag/" + i; store.Update(c); }
                clock.Advance(10); store.Tick();
                Check(store.SaveCount == 0, "600 UI changes in 10s: no immediate save");
                clock.Advance(50); store.Tick();
                if (store.LastError is not null) throw new Exception("coalesced save failed", store.LastError);
                Check(store.SaveCount == 1 && !store.Dirty, "600 UI changes: one save at 60s");
                Console.WriteLine($"METRIC coalesced saves={store.SaveCount} payloadBytes={store.LogicalBytesWritten} priorBackupBytes={store.BackupBytesCreated}");
                c.HighlightEnabled = false; store.Update(c);
                c.HighlightEnabled = true; store.Update(c);
                clock.Advance(60); store.Tick();
                Check(store.SaveCount == 1, "reverted edits do not save");
                for (var i = 0; i < 4; i++) { c.SelectedList = "generation/" + i; store.Update(c); clock.Advance(60); store.Tick(); }
                var backups = Directory.GetFiles(store.BackupDirectory).OrderDescending().ToArray();
                Check(backups.Length == 3 && backups.Select(p => JsonSerializer.Deserialize<Configuration>(File.ReadAllText(p))!.SelectedList)
                    .SequenceEqual(new[] { "generation/2", "generation/1", "generation/0" }), "three most recent valid backup generations");
                c.SelectedList = "logout"; store.Update(c); store.Flush(); store.Flush();
                Check(store.SaveCount == 6 && !store.Dirty, "logout flush and duplicate event coalescing");
                c.SelectedList = "exit"; store.Update(c); store.Dispose();
                var written = store.LogicalBytesWritten;
                c.SelectedList = "too late"; store.Update(c); clock.Advance(60); store.Tick(); store.Retry(); store.Flush(); store.Dispose();
                Check(store.SaveCount == 7 && store.LogicalBytesWritten == written, "normal exit/Dispose flush and no writes after disposal");
            }
            File.WriteAllText(path, "{broken");
            using (var recovered = new ConfigurationStore(path))
            {
                Check(recovered.Recovered && recovered.Configuration.SelectedList == "logout", "corrupt primary restores newest valid backup");
                Check(JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path))!.SelectedList == "logout"
                    && Directory.GetFiles(root, "config.json.corrupt.*").Length == 1, "recovery repairs primary and preserves corrupt file");
            }
            using (var restarted = new ConfigurationStore(path))
                Check(!restarted.Recovered && restarted.Configuration.SelectedList == "logout" && restarted.LogicalBytesWritten == 0,
                    "restart does not roll back a valid primary or rewrite it");
            var corruptBackup = Directory.GetFiles(path + ".backups").OrderDescending().First();
            File.WriteAllText(corruptBackup, "broken backup");
            File.WriteAllText(path, "{\"Version\":\"wrong type\"}");
            using (var recovered = new ConfigurationStore(path))
                Check(recovered.Recovered && recovered.Configuration.SelectedList == "generation/3"
                    && File.ReadAllText(corruptBackup) == "broken backup", "malformed schema and corrupt newest backup: use older valid generation");

            var overlapPath = Path.Combine(root, "overlap.json");
            ConfigurationStore? concurrent = null;
            var injected = false;
            concurrent = new ConfigurationStore(overlapPath, beforeCommit: _ =>
            {
                if (injected) return;
                injected = true;
                concurrent!.Update(new Configuration { SelectedList = "during save" });
                concurrent.Flush(); // Reentrant flush must not run a second writer.
            });
            using (concurrent)
            {
                concurrent.Flush();
                Check(concurrent.SaveCount == 1 && concurrent.Dirty, "changes during save remain pending; reentrant writer excluded");
                Parallel.For(0, 10, _ => concurrent.Retry());
                Check(concurrent.SaveCount == 2 && !concurrent.Dirty, "ten concurrent flushes commit the new snapshot once");
            }

            foreach (var fault in new[] { "rename denied", "invalid temporary" })
            {
                var faultPath = Path.Combine(root, fault + ".json");
                File.WriteAllText(faultPath, JsonSerializer.Serialize(initial));
                var notifications = 0;
                var fail = true;
                clock = new Clock();
                using var failed = new ConfigurationStore(faultPath, _ => notifications++, clock, temp =>
                {
                    if (!fail) return;
                    if (fault == "invalid temporary") File.WriteAllText(temp, "broken");
                    else throw new IOException(fault);
                });
                failed.Update(new Configuration { Language = "en" });
                for (var i = 0; i < 480; i++) { clock.Advance(60); failed.Tick(); }
                Check(failed.SaveAttempts == 3 && failed.Stopped && notifications == 1 && failed.Dirty,
                    fault + ": three attempts, one notification over 8h");
                Check(JsonSerializer.Deserialize<Configuration>(File.ReadAllText(faultPath))!.Language == "ja"
                    && (!Directory.Exists(failed.BackupDirectory) || Directory.GetFiles(failed.BackupDirectory).Length == 0),
                    fault + ": original retained; no backup rotation");
                failed.Flush(); failed.Flush();
                Check(failed.SaveAttempts == 4 && notifications == 1, fault + ": final flush retries once");
                fail = false; failed.Retry();
                Check(failed.SaveCount == 1 && !failed.Stopped && !failed.Dirty, fault + ": explicit retry recovers");
                Console.WriteLine($"METRIC fault={fault} attempts={failed.SaveAttempts} saves={failed.SaveCount} payloadBytes={failed.LogicalBytesWritten} backupBytes={failed.BackupBytesCreated}");
            }

            var fullPath = Path.Combine(root, "disk-full.json");
            File.WriteAllText(fullPath, JsonSerializer.Serialize(initial));
            clock = new Clock();
            var isFull = true;
            var fullNotices = 0;
            using (var full = new ConfigurationStore(fullPath, _ => fullNotices++, clock,
                writePayload: (stream, bytes) =>
                {
                    if (!isFull) { stream.Write(bytes); return; }
                    stream.Write(bytes, 0, 17);
                    throw new IOException("Injected disk-full failure after a partial write.");
                }))
            {
                full.Update(new Configuration { Language = "en" });
                for (var i = 0; i < 480; i++) { clock.Advance(60); full.Tick(); }
                Check(full.Stopped && full.SaveAttempts == 3 && full.LogicalBytesWritten == 51 && fullNotices == 1,
                    "disk full during write: three partial attempts totaling 51 bytes, one notice");
                Check(JsonSerializer.Deserialize<Configuration>(File.ReadAllText(fullPath))!.Language == "ja"
                    && Directory.GetFiles(root, "disk-full.json.tmp.*").Length == 0,
                    "disk full retains original and releases temporary file handles");
                full.Flush(); full.Flush();
                Check(full.SaveAttempts == 4 && full.LogicalBytesWritten == 68, "disk full final flush is attempted only once");
                isFull = false; full.Retry();
                Check(full.SaveCount == 1 && !full.Dirty, "disk space restored: explicit retry commits pending settings");
                Console.WriteLine($"METRIC disk-full attempts={full.SaveAttempts} saves={full.SaveCount} payloadBytes={full.LogicalBytesWritten} backupBytes={full.BackupBytesCreated}");
            }

            var lockPath = Path.Combine(root, "locked.json");
            File.WriteAllText(lockPath, JsonSerializer.Serialize(initial));
            using (var locked = new ConfigurationStore(lockPath))
            {
                locked.Update(new Configuration { Language = "de" });
                using (var fileLock = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) locked.Flush();
                Check(locked.SaveCount == 0 && locked.Dirty && locked.LastError is IOException, "real file lock preserves pending settings");
                locked.Retry();
                Check(locked.SaveCount == 1, "file lock release and manual retry succeed");
            }
            ConfigurationStore blocked;
            using (var fileLock = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                blocked = new ConfigurationStore(lockPath);
                blocked.Update(new Configuration { Language = "fr" }); blocked.Flush();
                Check(blocked.LoadBlocked && blocked.SaveCount == 0, "locked primary at startup is not mistaken for corruption");
            }
            using (blocked)
            {
                blocked.Retry();
                Check(!blocked.LoadBlocked && blocked.Configuration.Language == "de" && blocked.SaveCount == 0,
                    "retry after startup lock loads valid primary without overwriting it");
            }
            var finalNotices = 0;
            using (var finalFailure = new ConfigurationStore(Path.Combine(root, "final-failure.json"),
                beforeCommit: _ => throw new IOException("exit failure"), onFinalFailure: _ => finalNotices++))
            {
                finalFailure.Dispose(); finalFailure.Dispose();
                Check(finalNotices == 1 && finalFailure.SaveAttempts == 1, "failed exit flush reported once; disposal is idempotent");
            }

            var dayPath = Path.Combine(root, "midnight.json");
            clock = new Clock();
            using (var day = new ConfigurationStore(dayPath, clock: clock))
            {
                day.Update(new Configuration { Language = "ko" });
                clock.Advance(60); day.Tick();
                Check(day.SaveCount == 1 && clock.GetUtcNow().Day == 23, "settings interval crosses midnight");
            }

            clock = new Clock();
            var calls = 0; var notices = 0;
            var circuit = new FailureCircuit(TimeSpan.FromSeconds(3), _ => notices++, clock);
            for (var sec = 0; sec < 3600; sec++)
            {
                for (var frame = 0; frame < 60; frame++) circuit.Run(() => { calls++; throw new IOException("overlay"); });
                clock.Advance(1);
            }
            Check(calls == 3 && notices == 1 && circuit.Stopped, "216000 draw frames: three failing calls and one notification");
            circuit.Retry(); circuit.Run(() => calls++);
            Check(!circuit.Stopped && circuit.Failures == 0 && calls == 4, "overlay explicit retry resumes");
            circuit.Run(() => throw new IOException("transient"));
            clock.Advance(3); circuit.Run(() => { });
            circuit.Run(() => throw new IOException("new episode"));
            Check(circuit.Failures == 1, "successful draw resets the consecutive failure count");

            var configDir = Path.Combine(root, "BaitBeacon");
            var companion = Path.Combine(root, "GatherbuddyReborn");
            Directory.CreateDirectory(configDir); Directory.CreateDirectory(companion);
            var listPath = Path.Combine(companion, "auto_gather_lists.json");
            File.WriteAllText(listPath, "[{\"Name\":\"Valid\",\"ItemIds\":[36679]}]");
            clock = new Clock(); notices = 0;
            var lists = new SavedLists(configDir, _ => notices++, clock);
            var valid = lists.Lists;
            for (var i = 0; i < 100; i++) { clock.Advance(3); lists.Reload(); }
            Check(lists.ReadAttempts == 1, "unchanged saved lists: one initial body read");
            File.WriteAllText(listPath, "{broken");
            for (var i = 0; i < 1200; i++) { clock.Advance(3); lists.Reload(); }
            Check(lists.ReadAttempts == 4 && notices == 1 && lists.Stopped && ReferenceEquals(valid, lists.Lists),
                "1h malformed list: three retries, one notification, last valid snapshot retained");
            File.WriteAllText(listPath, "[{\"Name\":\"Fixed\",\"ItemIds\":[]}]");
            Check(!lists.Reload(), "stopped reads require explicit retry");
            Check(lists.Reload(retry: true) && !lists.Stopped && lists.Lists[0].Name == "Fixed", "explicit list retry loads repaired file");
            using (var oversized = new FileStream(listPath, FileMode.Create)) oversized.SetLength(SavedLists.MaximumBytes + 1);
            lists.Reload(retry: true);
            Check(lists.Error == "read" && lists.Lists[0].Name == "Fixed", "oversized list rejected while retaining valid snapshot");
            File.Delete(listPath);
            lists.Reload(retry: true);
            for (var i = 0; i < 10; i++) { clock.Advance(3); lists.Reload(); }
            Check(lists.Stopped && lists.Lists[0].Name == "Fixed", "disappearing list retains data and stops after three failures");
            var notDirectory = Path.Combine(root, "not-directory");
            File.WriteAllText(notDirectory, "file");
            var badRoot = new SavedLists(Path.Combine(notDirectory, "BaitBeacon"), clock: clock);
            clock.Advance(3); badRoot.Reload(); clock.Advance(3); badRoot.Reload();
            Check(badRoot.Stopped, "directory discovery failures are covered by the same finite retry gate");
        }
        finally { Directory.Delete(root, true); }
    }
}
