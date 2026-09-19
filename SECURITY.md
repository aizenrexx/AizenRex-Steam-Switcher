# Security Policy

## Supported versions

Only the newest published release is supported.

## Reporting a vulnerability

Open a private security advisory on this repository
(**Security -> Advisories -> Report a vulnerability**) rather than a public issue.

Please include:

- The version affected (Settings -> About shows it)
- What an attacker would need to be able to do
- Steps to reproduce
- The relevant log lines

You can expect an acknowledgement within a few days.

## What this app does and does not do

**Does**

- Rename folders inside `C:\Program Files (x86)` - needs administrator rights
- Read and rewrite Steam's `loginusers.vdf`, `config.vdf`, `localconfig.vdf`
- Write its own settings, history, journal and backups next to the executable
- Contact the GitHub releases API to check for updates
- Download and verify a published release when you ask it to

**Does not**

- Send any data anywhere. Nothing about your accounts leaves the machine
- Modify Steam itself, inject into it, or touch game files
- Store a password. It handles the auto-login *flag*, not credentials
- Write outside its own folder, except the Steam config it is asked to switch

## Data safety

Every switch is journaled before it starts and backed up before it writes.
An interrupted switch is detected on the next launch and can be repaired.
Backups are never deleted automatically.