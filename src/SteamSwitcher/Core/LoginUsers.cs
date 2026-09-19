using System.IO;

namespace SteamSwitcher.Core;

/// <summary>
/// Structured access to config\loginusers.vdf.
/// <para>
/// Replaces the old regex approach, which had two measured defects:
/// it flagged EVERY account instead of one (Regex.Replace with no count),
/// and it could not add a key that was absent, so AllowAutoLogin never
/// actually turned on.
/// </para>
/// </summary>
/// <summary>
/// One stored login, with the flags that decide who Steam signs in as.
/// Extends the shared <see cref="SteamAccount"/> model rather than
/// duplicating it.
/// </summary>
public sealed class StoredLogin
{
    public SteamAccount Account { get; init; } = new();
    public bool MostRecent { get; init; }
    public bool RememberPassword { get; init; }

    public string SteamId64 => Account.SteamId64;
    public string AccountName => Account.AccountName;
    public string PersonaName => Account.PersonaName;
    public string Display => Account.Display;
}

public static class LoginUsers
{
    public static string PathFor(string steamDir) =>
        Path.Combine(steamDir, "config", "loginusers.vdf");

    /// <summary>Every account Steam has stored for this folder.</summary>
    public static List<StoredLogin> Read(string steamDir)
    {
        var result = new List<StoredLogin>();

        var root = VdfDocument.Load(PathFor(steamDir));
        var users = root?.Child("users");
        if (users is null) return result;

        foreach (var acct in users.Children.Where(c => c.IsSection))
        {
            // The section key is the SteamID64; its lower 32 bits are the
            // account id Steam uses for userdata folders and the registry.
            long id32 = 0;
            if (long.TryParse(acct.Key, out var id64) && id64 > 0)
                id32 = id64 & 0xFFFFFFFFL;

            result.Add(new StoredLogin
            {
                Account = new SteamAccount
                {
                    SteamId64 = acct.Key,
                    SteamId32 = id32,
                    AccountName = acct.ChildValue("AccountName") ?? "",
                    PersonaName = acct.ChildValue("PersonaName") ?? ""
                },
                MostRecent = acct.ChildValue("MostRecent") == "1",
                RememberPassword = acct.ChildValue("RememberPassword") == "1"
            });
        }

        return result;
    }

    /// <summary>
    /// Marks exactly one account as the remembered, most-recent login and
    /// clears those flags on every other account, so Steam has no ambiguity
    /// about who to sign in as.
    /// </summary>
    public static OpResult SetActiveAccount(string steamDir, string accountName)
    {
        var path = PathFor(steamDir);

        var root = VdfDocument.Load(path);
        var users = root?.Child("users");
        if (root is null || users is null)
            return OpResult.Fail("loginusers.vdf not found; Steam will ask for a login.");

        BackupOnce(path);

        var matched = false;

        foreach (var acct in users.Children.Where(c => c.IsSection))
        {
            var isTarget = string.Equals(
                acct.ChildValue("AccountName"), accountName, StringComparison.OrdinalIgnoreCase);

            // SetChild inserts the key when missing, which the regex could not do.
            acct.SetChild("MostRecent", isTarget ? "1" : "0");

            if (isTarget)
            {
                matched = true;
                acct.SetChild("RememberPassword", "1");
                acct.SetChild("AllowAutoLogin", "1");
            }
        }

        if (!matched)
            return OpResult.Fail($"{accountName} is not stored in this profile yet.");

        if (!VdfDocument.Save(path, root))
            return OpResult.Fail("Could not write loginusers.vdf.");

        Log.Info($"loginusers.vdf: {accountName} set as the only MostRecent account.");
        return OpResult.Ok($"Auto-login set to {accountName}.");
    }

    /// <summary>Keeps one pristine copy from before we ever edited the file.</summary>
    private static void BackupOnce(string path)
    {
        try
        {
            var backup = path + ".switcher-original";
            if (!File.Exists(backup) && File.Exists(path))
                File.Copy(path, backup);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not back up loginusers.vdf: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for a NEW account to appear, used by the wizard while the user
    /// signs in. Returns the new account, or null on timeout.
    /// </summary>
    public static async Task<StoredLogin?> WaitForNewLoginAsync(
        string steamDir,
        IReadOnlyCollection<string> knownIds,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (ct.IsCancellationRequested) return null;

            foreach (var acct in Read(steamDir))
            {
                if (!knownIds.Contains(acct.SteamId64) && !string.IsNullOrWhiteSpace(acct.AccountName))
                {
                    Log.Info($"New login detected: {acct.AccountName} ({acct.SteamId64})");
                    return acct;
                }
            }

            try { await Task.Delay(2000, ct); }
            catch (OperationCanceledException) { return null; }
        }

        return null;
    }
}
