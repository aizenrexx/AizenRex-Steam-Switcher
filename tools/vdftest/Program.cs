// Proves the VDF logic on a COPY of the real loginusers.vdf.
// The real file is never touched.

// WPF projects do not get System.IO from ImplicitUsings, so it is explicit here.
using System.IO;
using SteamSwitcher.Core;

var scratch = Path.Combine(Path.GetTempPath(), "vdf-test-" + Guid.NewGuid().ToString("N")[..8]);
var cfg = Path.Combine(scratch, "config");
Directory.CreateDirectory(cfg);

int pass = 0, fail = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  PASS  {name}"); }
    else { fail++; Console.WriteLine($"  FAIL  {name}  {detail}"); }
}

// ---------------------------------------------------------------- TEST 1
// Round-trip a real file: parse it, write it, and confirm the accounts survive.
Console.WriteLine("TEST 1 - round-trip the real file (copy)");
var real = @"C:\Program Files (x86)\Steam\config\loginusers.vdf";
if (File.Exists(real))
{
    var copy = Path.Combine(cfg, "loginusers.vdf");
    File.Copy(real, copy, true);

    var before = LoginUsers.Read(scratch);
    Console.WriteLine($"  accounts parsed: {before.Count} -> {string.Join(", ", before.Select(a => a.AccountName))}");
    Check("parsed at least one account", before.Count > 0);

    var root = VdfDocument.Load(copy)!;
    VdfDocument.Save(copy, root);

    var after = LoginUsers.Read(scratch);
    Check("account count unchanged after rewrite", before.Count == after.Count, $"{before.Count} vs {after.Count}");
    Check("account name preserved",
        before.Count > 0 && after.Count > 0 && before[0].AccountName == after[0].AccountName);
}
else
{
    Console.WriteLine("  (real file absent, skipping)");
}

// ---------------------------------------------------------------- TEST 2
// THE BUG THAT WAS FIXED. Two accounts, and only the target may end up
// with MostRecent=1. The old regex set it on both.
Console.WriteLine();
Console.WriteLine("TEST 2 - two accounts, exactly one MostRecent (the old bug)");
var two = """
users
{
	"76561198000000001"
	{
		"AccountName"		"alpha"
		"PersonaName"		"Alpha"
		"RememberPassword"		"1"
		"MostRecent"		"1"
		"Timestamp"		"1700000000"
	}
	"76561198000000002"
	{
		"AccountName"		"bravo"
		"PersonaName"		"Bravo"
		"RememberPassword"		"1"
		"MostRecent"		"1"
		"Timestamp"		"1700000001"
	}
}
""";
File.WriteAllText(Path.Combine(cfg, "loginusers.vdf"), two);

var res = LoginUsers.SetActiveAccount(scratch, "bravo");
Console.WriteLine($"  SetActiveAccount -> {res.Success}: {res.Message}");

var rows = LoginUsers.Read(scratch);
var mostRecent = rows.Where(r => r.MostRecent).Select(r => r.AccountName).ToList();
Check("exactly one MostRecent", mostRecent.Count == 1, $"got {mostRecent.Count}: {string.Join(",", mostRecent)}");
Check("the target is the one flagged", mostRecent.FirstOrDefault() == "bravo", $"got {mostRecent.FirstOrDefault()}");
Check("other account preserved, not deleted", rows.Count == 2, $"count={rows.Count}");

// ---------------------------------------------------------------- TEST 3
// The second defect: a key that is absent must be ADDED. The old code
// only replaced existing keys, so AllowAutoLogin never turned on.
Console.WriteLine();
Console.WriteLine("TEST 3 - missing AllowAutoLogin must be inserted");
var noFlag = """
users
{
	"76561198000000003"
	{
		"AccountName"		"charlie"
		"PersonaName"		"Charlie"
	}
}
""";
File.WriteAllText(Path.Combine(cfg, "loginusers.vdf"), noFlag);

LoginUsers.SetActiveAccount(scratch, "charlie");
var text = File.ReadAllText(Path.Combine(cfg, "loginusers.vdf"));
Check("AllowAutoLogin was added", text.Contains("AllowAutoLogin"), "key still missing");
Check("AllowAutoLogin is 1", text.Contains("\"AllowAutoLogin\"\t\t\"1\""));
Check("RememberPassword was added", text.Contains("RememberPassword"));

// ---------------------------------------------------------------- TEST 4
Console.WriteLine();
Console.WriteLine("TEST 4 - unknown account is rejected, file left alone");
var snapshot = File.ReadAllText(Path.Combine(cfg, "loginusers.vdf"));
var bad = LoginUsers.SetActiveAccount(scratch, "does-not-exist");
Check("returns failure", !bad.Success, bad.Message);
Check("file unchanged", File.ReadAllText(Path.Combine(cfg, "loginusers.vdf")) == snapshot);

// ---------------------------------------------------------------- TEST 5
Console.WriteLine();
Console.WriteLine("TEST 5 - missing file is handled, not crashed on");
var empty = Path.Combine(Path.GetTempPath(), "vdf-empty-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(empty, "config"));
var missing = LoginUsers.SetActiveAccount(empty, "anyone");
Check("fails cleanly", !missing.Success, missing.Message);
Check("read returns empty list", LoginUsers.Read(empty).Count == 0);

// ---------------------------------------------------------------- TEST 6
Console.WriteLine();
Console.WriteLine("TEST 6 - SteamID32 derived from SteamID64");
File.WriteAllText(Path.Combine(cfg, "loginusers.vdf"), two);
var alpha = LoginUsers.Read(scratch).First(r => r.AccountName == "alpha");
// 76561198000000001 - 76561197960265728 = 39734273
Check("id32 correct", alpha.Account.SteamId32 == 39734273, $"got {alpha.Account.SteamId32}");

// ---------------------------------------------------------------- TEST 7
Console.WriteLine();
Console.WriteLine("TEST 7 - Steam detection on this machine");
var det = SteamInstaller.Detect();
Console.WriteLine($"  installed={det.Installed} path={det.Path} via={det.Source}");
Check("detection returns a verified path", !det.Installed || File.Exists(Path.Combine(det.Path!, "steam.exe")));

try { Directory.Delete(scratch, true); Directory.Delete(empty, true); } catch { }

Console.WriteLine();
Console.WriteLine($"==== {pass} passed, {fail} failed ====");
return fail == 0 ? 0 : 1;
