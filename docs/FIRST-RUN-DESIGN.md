# First-Run Setup Wizard — design & research findings

Target scenario: a **completely fresh PC**. No Steam, no profiles, nothing.
User installs our exe only, and everything else happens automatically.

---

## 1. THE CRITICAL FINDING (this changes the design)

**Modern Steam does NOT keep the login inside the Steam folder.**

Measured on this machine (`tools/probe-token.ps1`, `probe-token2.ps1`):

| Location | Result |
|---|---|
| `ssfn*` files in Steam root | **0 found** — the old sentry-file method is dead |
| `config.vdf` → `ConnectCache` | **absent** in both folders |
| `%LOCALAPPDATA%\Steam\local.vdf` | **3127 bytes, `ConnectCache` with 2 entries** |

`local.vdf` ConnectCache entries: `134258561`, `90abe4c51` — blob 1484 hex chars each.
Two entries = **one login token per account**, and the two accounts on this PC
are `nullcove` (Steam Main) and `toolsteamx` (Steam).

### Why this matters
`local.vdf` lives in `%LOCALAPPDATA%\Steam`, which is **shared by every Steam
folder** and is NOT swapped when we swap the install directory. Cross-checked
against upstream sources: Valve deprecated sentry/ssfn files in mid-2023 and
moved to JWT refresh tokens (~200-day life) stored as `ConnectCache` in
`local.vdf`, encrypted per-Windows-user with `CryptProtectData` (DPAPI).

**Consequences for us:**
1. Because ConnectCache holds one entry PER ACCOUNT, both accounts can stay
   logged in simultaneously. So "login once, never log out again" is
   achievable — this is the mechanism that makes it possible.
2. We must **never delete or overwrite `local.vdf`**. Doing so logs out
   *every* account at once. Any "reset/clean" feature must leave it alone.
3. DPAPI-encrypted per Windows user means tokens are **not portable** to
   another PC or another Windows account. On a friend's PC, he must log in
   once himself — we cannot copy a login across machines, and we should not
   try (that is account-sharing and would look like theft to Valve).
4. `local.vdf` was last modified 09/04, while switching happened today —
   confirming a switch does **not** disturb the tokens. Good: our switching
   is already login-safe.

---

## 2. BUG FOUND IN CURRENT CODE (pre-existing, unrelated to the wizard)

`probe-login.ps1` measured, in BOTH folders:

```
MostRecent=1       : 0   <-- should be exactly 1
AllowAutoLogin=1   : 0
```

`AutoLoginSync.UpdateLoginUsers` uses `Regex.Replace` **without a count**, and
only when `IsMatch` is true. Two real defects:

- **Defect A — writes to every account.** `Replace` with no count rewrites
  `MostRecent` for *all* accounts in the file. With 2+ accounts, several get
  `MostRecent=1`, and Steam then picks unpredictably. Right now each folder
  has 1 account so the damage is hidden; it appears as soon as a folder holds
  two accounts.
- **Defect B — cannot add a missing key.** The keys are currently `"0"`/absent,
  and if `AllowAutoLogin` is absent entirely, `IsMatch` is false and the key is
  never added. So auto-login silently never engages. This matches the measured
  `AllowAutoLogin=1 : 0`.

Fix: parse the VDF into a tree, set flags on the **target account only**, clear
them on all others, insert keys when missing, write atomically.
Do NOT reuse regex-replace for structured VDF.

---

## 3. WIZARD FLOW (fresh PC)

```
exe runs  ->  FirstRunDetector.NeedsSetup()?
                 |
                 +-- no  -> normal Dashboard
                 |
                 +-- yes -> Setup Wizard
```

`NeedsSetup()` is true when no profile folder is configured/exists. It must
NOT rely on a bare registry check, since an uninstalled Steam can leave keys
behind (this PC still shows an uninstall entry with an empty InstallLocation).

### Step 1 — Choose layout
- **Dual** (Main + Daddy) — the normal case
- **Single** — one profile only
- Custom folder names/paths, with defaults pre-filled

### Step 2 — Ensure Steam exists
- Detect existing Steam (registry `SteamPath` + verify `steam.exe` on disk).
- If absent: download `SteamSetup.exe` from
  `https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe`
  and run **`SteamSetup.exe /S`** (verified silent switch; `/D=<path>` sets the
  directory and must come last). Show progress; verify `steam.exe` afterwards.
- Never assume success: re-check the file, and surface a clear error if missing.

### Step 3 — Create the profile folders
- Create each chosen folder, then let Steam populate it on first launch.
- For the second profile, copy only the *bootstrap* files, never `userdata`,
  and never touch `local.vdf`.

### Step 4 — Login, once per account
- Launch Steam for profile 1, user logs in **with "Remember my password" ticked**.
  Wizard waits and watches `loginusers.vdf` for a new account entry.
- Repeat for profile 2 (dual mode only).
- After each login, record `AccountName` + `SteamID64` into our settings so
  the Dashboard can identify the active profile (this also fixes the existing
  "active profile unidentified" bug).
- **Persistence guarantee:** with `RememberPassword=1` the token lands in
  ConnectCache and survives switches indefinitely. Logging in a second time is
  only needed if the user signs out manually or the ~200-day token expires.

### Step 5 — SteamDaddy (optional, for Daddy profile)
- Reuse the existing `SteamDaddyUpdater`, which already downloads, SHA256-verifies
  and installs, honouring the custom update URL.
- Keep it optional and non-fatal: a SteamDaddy failure must never block setup.

---

## 4. HARD SAFETY RULES

1. **Never delete/modify `%LOCALAPPDATA%\Steam\local.vdf`** — it is every
   account's login token.
2. **Never store the user's Steam password.** We only tick Steam's own
   "remember me" and let Valve's client hold the token.
3. **Never try to copy a login to another PC** — DPAPI-bound, and it is
   account-sharing.
4. Back up `loginusers.vdf` before every edit (already done today).
5. Every destructive step asks first; setup must be resumable if it fails
   halfway.
6. Verify each step actually happened (file exists, process running) instead
   of assuming.

---

## 5. WHAT I STILL NEED FROM THE USER

- Does the friend-PC case need to end up with **his own** accounts (correct,
  supported), or your accounts on his machine (not possible/allowed — DPAPI +
  Valve ToS)?
- Default install path for a fresh PC: keep `C:\Program Files (x86)\...`?
- Should the wizard also handle "Steam already installed, but no profiles yet"?
  (I assume yes — adopt the existing folder as Main.)
