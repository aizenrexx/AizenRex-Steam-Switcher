$ErrorActionPreference = 'Continue'
$root  = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$build = Join-Path $root 'build'

# Scratch output from my debugging sessions gets moved (not deleted) into one
# quarantine folder, so the tree is readable and nothing is lost.
$trash = Join-Path $root ('_scratch_' + (Get-Date -Format 'yyyyMMdd'))
New-Item -ItemType Directory -Path $trash -Force | Out-Null

$moved = 0
$failed = @()

# ---- 1. loose files in build\  (keep the folders: release, release2, screenshots)
foreach ($f in Get-ChildItem $build -File -ErrorAction SilentlyContinue) {
    try {
        Move-Item $f.FullName (Join-Path $trash $f.Name) -Force -ErrorAction Stop
        $moved++
    } catch { $failed += $f.Name }
}

# ---- 2. one-off scripts in the project root. build.ps1 stays: it is the real
#         build entry point. verify2/perf/launchtime stay useful, so they are
#         kept but moved into a tools folder rather than sitting at the root.
$tools = Join-Path $root 'tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null

$keepAsTools = @('verify2.ps1','perf.ps1','launchtime.ps1')
$junkScripts = @('inspect.ps1','verify.ps1','testupdate.ps1','killelevated.ps1','swap.ps1','cleanup.ps1')

foreach ($name in $keepAsTools) {
    $p = Join-Path $root $name
    if (Test-Path $p) {
        try { Move-Item $p (Join-Path $tools $name) -Force -ErrorAction Stop; $moved++ }
        catch { $failed += $name }
    }
}

foreach ($name in $junkScripts) {
    $p = Join-Path $root $name
    if (Test-Path $p) {
        # cleanup.ps1 is running right now; it cannot move itself reliably.
        if ($name -eq 'cleanup.ps1') { continue }
        try { Move-Item $p (Join-Path $trash $name) -Force -ErrorAction Stop; $moved++ }
        catch { $failed += $name }
    }
}

# ---- 3. the throwaway updater test project
$updtest = Join-Path $root 'updtest'
if (Test-Path $updtest) {
    try { Move-Item $updtest (Join-Path $trash 'updtest') -Force -ErrorAction Stop; $moved++ }
    catch { $failed += 'updtest' }
}

Write-Output "MOVED: $moved item(s) into $(Split-Path $trash -Leaf)"
if ($failed.Count -gt 0) { Write-Output ("COULD NOT MOVE: " + ($failed -join ', ')) }

Write-Output ''
Write-Output '=== PROJECT ROOT NOW ==='
Get-ChildItem $root | Sort-Object { $_.PSIsContainer } -Descending |
    ForEach-Object { Write-Output ("  " + $(if($_.PSIsContainer){"[dir] "}else{"      "}) + $_.Name) }

Write-Output ''
Write-Output '=== build\ NOW ==='
Get-ChildItem $build | ForEach-Object {
    $tag = if ($_.PSIsContainer) { "[dir] " } else { "      " }
    Write-Output ("  " + $tag + $_.Name)
}
