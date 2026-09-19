using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SteamSwitcher.Core;

/// <summary>
/// Makes Steam sign in to the account that belongs to the folder now active,
/// by updating config/loginusers.vdf and HKCU\Software\Valve\Steam.
/// </summary>
public static partial class AutoLoginSync
{
    [GeneratedRegex(@"""MostRecent""\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex MostRecentRegex();

    [GeneratedRegex(@"""RememberPassword""\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex RememberRegex();

    [GeneratedRegex(@"""AllowAutoLogin""\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex AllowAutoLoginRegex();

    /// <summary>
    /// Flags the given account as the most recent, remembered login inside
    /// loginusers.vdf. A backup copy is written next to it first.
    /// </summary>
    public static OpResult UpdateLoginUsers(string steamDir, string accountName)
    {
        var vdf = Path.Combine(steamDir, "config", "loginusers.vdf");
        if (!File.Exists(vdf))
            return OpResult.Fail("loginusers.vdf not found; Steam will ask for a login.");

        try
        {
            var content = File.ReadAllText(vdf);

            if (MostRecentRegex().IsMatch(content))
                content = MostRecentRegex().Replace(content, "\"MostRecent\"\t\t\"1\"");

            if (RememberRegex().IsMatch(content))
                content = RememberRegex().Replace(content, "\"RememberPassword\"\t\t\"1\"");

            if (AllowAutoLoginRegex().IsMatch(content))
                content = AllowAutoLoginRegex().Replace(content, "\"AllowAutoLogin\"\t\t\"1\"");

            File.WriteAllText(vdf, content);
            Log.Info($"loginusers.vdf updated for auto-login: {accountName}");
            return OpResult.Ok($"Auto-login prepared for {accountName}.");
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not update loginusers.vdf: {ex.Message}");
            return OpResult.Fail($"Could not update loginusers.vdf: {ex.Message}");
        }
    }

    /// <summary>Points the Steam client at the given account on next launch.</summary>
    public static OpResult UpdateRegistry(string accountName, long steamId32)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            return OpResult.Fail("No account name to write.");

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam", writable: true))
            {
                key?.SetValue("AutoLoginUser", accountName, RegistryValueKind.String);
                key?.SetValue("RememberPassword", 1, RegistryValueKind.DWord);
            }

            if (steamId32 > 0 && steamId32 <= uint.MaxValue)
            {
                try
                {
                    using var active = Registry.CurrentUser.CreateSubKey(@"Software\Valve\Steam\ActiveProcess", writable: true);
                    active?.SetValue("ActiveUser", unchecked((int)(uint)steamId32), RegistryValueKind.DWord);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Could not set ActiveProcess\\ActiveUser: {ex.Message}");
                }
            }

            Log.Info($"Registry auto-login set to {accountName} (id32 {steamId32}).");
            return OpResult.Ok($"Registry set to sign in as {accountName}.");
        }
        catch (Exception ex)
        {
            Log.Error("Registry auto-login update failed", ex);
            return OpResult.Fail($"Registry update failed: {ex.Message}");
        }
    }

    /// <summary>Reads the folder's account and applies both VDF and registry changes.</summary>
    public static OpResult SyncFor(string steamDir)
    {
        var account = SteamInspector.ReadAccount(steamDir);
        if (account is null || string.IsNullOrWhiteSpace(account.AccountName))
            return OpResult.Fail("No stored account found in this profile; Steam will ask for a login.");

        UpdateLoginUsers(steamDir, account.AccountName);
        var registry = UpdateRegistry(account.AccountName, account.SteamId32);

        return registry.Success
            ? OpResult.Ok($"Signed in as {account.Display}.")
            : registry;
    }
}
