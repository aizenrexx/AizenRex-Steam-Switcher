# Security and secrets

## What is never committed

`.gitignore` blocks all of these at the repository level:

| Category | Patterns |
|---|---|
| Build output | `bin/`, `obj/`, `Distribution/`, `*.exe`, `*.dll`, `*.zip` |
| **Real Steam data** | `*.vdf`, `*.cok`, `build/backups/`, `userdata/`, `exports/` |
| Secrets | `keys/`, `*.pem`, `*.pfx`, `*.snk`, `secrets/`, `.env`, `*token*`, `*password*` |
| Tooling | `.vs/`, `.vscode/`, `*.log`, `TestResults/` |

## Why `build/backups/` is blocked

That folder can contain a genuine `loginusers.vdf`, `config.vdf` and
`localconfig.vdf` - a real Steam account, with its SteamID and licence cache.
Committing it would publish someone's account data. It is ignored, and it must
stay ignored.

## The one credential the pipeline needs

`secrets.GITHUB_TOKEN` is provided by GitHub itself and is scoped to the run.
The workflow needs `contents: write` to create a release - that is the only
permission it requests.

Code signing is optional and comes from two repository secrets. Nothing else is
required, and no secret is ever written into a file that gets committed.

## Checking before you push

```powershell
git status --short          # nothing unexpected staged?
git diff --cached --name-only | Select-String -Pattern 'vdf|cok|pfx|pem|token'
```

The second command should print nothing.