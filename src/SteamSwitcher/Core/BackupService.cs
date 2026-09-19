using System.IO;
using System.Text.Json;

namespace SteamSwitcher.Core;

/// <summary>
/// Copies the small, valuable parts of a Steam profile (login and config data)
/// into &lt;App&gt;\backups. Game files are never copied.
/// </summary>
public static class BackupService
{
    private static readonly string[] Targets =
    {
        @"config\loginusers.vdf",
        @"config\config.vdf",
        @"config\libraryfolders.vdf",
        @"userdata"
    };

    private sealed class Manifest
    {
        public DateTime Created { get; set; }
        public string Profile { get; set; } = "";
        public string Source { get; set; } = "";
        public List<string> Items { get; set; } = new();
    }

    public static OpResult Create(string sourceDir, string profileName)
    {
        if (!Directory.Exists(sourceDir))
            return OpResult.Fail($"Profile folder does not exist: {sourceDir}");

        AppPaths.EnsureAll();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var safeName = profileName.ToLowerInvariant().Replace(' ', '-');
        var destination = Path.Combine(AppPaths.BackupDir, $"{safeName}-{stamp}");

        if (!AppPaths.IsInsideAppDir(destination))
            return OpResult.Fail("Backup destination is outside the application folder.");

        var copied = new List<string>();

        try
        {
            Directory.CreateDirectory(destination);

            foreach (var relative in Targets)
            {
                var src = Path.Combine(sourceDir, relative);
                var dst = Path.Combine(destination, relative);

                if (File.Exists(src))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, overwrite: true);
                    copied.Add(relative);
                }
                else if (Directory.Exists(src))
                {
                    CopyTree(src, dst);
                    copied.Add(relative);
                }
            }

            if (copied.Count == 0)
            {
                Directory.Delete(destination, recursive: true);
                return OpResult.Fail("No Steam configuration data was found to back up.");
            }

            var manifest = new Manifest
            {
                Created = DateTime.Now,
                Profile = profileName,
                Source = sourceDir,
                Items = copied
            };

            File.WriteAllText(
                Path.Combine(destination, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            Log.Info($"Backup created: {Path.GetFileName(destination)} ({copied.Count} items).");
            return OpResult.Ok($"Backup saved as {Path.GetFileName(destination)} ({copied.Count} items).");
        }
        catch (Exception ex)
        {
            try { if (Directory.Exists(destination)) Directory.Delete(destination, recursive: true); } catch { }
            Log.Error("Backup failed", ex);
            return OpResult.Fail($"Backup failed: {ex.Message}");
        }
    }

    public static List<BackupEntry> List()
    {
        AppPaths.EnsureAll();
        var entries = new List<BackupEntry>();

        try
        {
            foreach (var folder in Directory.EnumerateDirectories(AppPaths.BackupDir))
            {
                var manifestPath = Path.Combine(folder, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath));
                    entries.Add(new BackupEntry
                    {
                        Name = Path.GetFileName(folder),
                        Path = folder,
                        Created = manifest?.Created ?? Directory.GetCreationTime(folder),
                        Profile = manifest?.Profile ?? "",
                        Items = manifest?.Items ?? new List<string>(),
                        SizeBytes = SteamInspector.FolderSize(folder, 20_000)
                    });
                }
                catch (Exception ex)
                {
                    Log.Warn($"Skipping unreadable backup {Path.GetFileName(folder)}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not list backups: {ex.Message}");
        }

        return entries.OrderByDescending(e => e.Created).ToList();
    }

    public static OpResult Restore(BackupEntry backup, string targetDir)
    {
        if (!Directory.Exists(backup.Path))
            return OpResult.Fail("Backup folder no longer exists.");

        if (!Directory.Exists(targetDir))
            return OpResult.Fail($"Target profile folder does not exist: {targetDir}");

        if (backup.Items.Count == 0)
            return OpResult.Fail("This backup has no recorded items.");

        try
        {
            foreach (var relative in backup.Items)
            {
                var src = Path.Combine(backup.Path, relative);
                var dst = Path.Combine(targetDir, relative);

                if (File.Exists(src))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, overwrite: true);
                }
                else if (Directory.Exists(src))
                {
                    CopyTree(src, dst);
                }
            }

            Log.Info($"Restored backup {backup.Name} into {targetDir}.");
            Store.AddHistory("Restore backup", true, $"{backup.Name} -> {targetDir}");
            return OpResult.Ok($"Restored {backup.Items.Count} item(s) from {backup.Name}.");
        }
        catch (Exception ex)
        {
            Log.Error("Restore failed", ex);
            Store.AddHistory("Restore backup", false, ex.Message);
            return OpResult.Fail($"Restore failed: {ex.Message}");
        }
    }

    public static OpResult Delete(BackupEntry backup)
    {
        if (!AppPaths.IsInsideAppDir(backup.Path))
            return OpResult.Fail("Refusing to delete a folder outside the application directory.");

        try
        {
            Directory.Delete(backup.Path, recursive: true);
            Log.Info($"Deleted backup {backup.Name}.");
            return OpResult.Ok($"Deleted {backup.Name}.");
        }
        catch (Exception ex)
        {
            return OpResult.Fail($"Could not delete backup: {ex.Message}");
        }
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, destination));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            try { File.Copy(file, file.Replace(source, destination), overwrite: true); }
            catch (Exception ex) { Log.Debug($"Skipped {Path.GetFileName(file)}: {ex.Message}"); }
        }
    }
}
