$ErrorActionPreference = 'Stop'
$root  = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$build = Join-Path $root 'build'
$old   = Join-Path $build 'release'
$new   = Join-Path $build 'release2'

if (@(Get-Process SteamSwitcher -ErrorAction SilentlyContinue).Count -gt 0) {
    Write-Output 'ABORT: SteamSwitcher is running. Close it first.'
    exit 1
}
if (-not (Test-Path (Join-Path $new 'SteamSwitcher.exe'))) {
    Write-Output 'ABORT: the fixed build is missing.'
    exit 1
}

# Keep the user's real data from the old folder.
$carry = @('data','backups','exports','logs')
$stash = Join-Path $build '_carry_tmp'
New-Item -ItemType Directory -Path $stash -Force | Out-Null
foreach ($sub in $carry) {
    $src = Join-Path $old $sub
    if (Test-Path $src) { Copy-Item $src (Join-Path $stash $sub) -Recurse -Force -ErrorAction SilentlyContinue }
}

Remove-Item $old -Recurse -Force
Rename-Item $new 'release'

# Restore data that the fresh publish does not contain.
foreach ($sub in $carry) {
    $src = Join-Path $stash $sub
    $dst = Join-Path $old $sub
    if ((Test-Path $src) -and -not (Test-Path $dst)) {
        Copy-Item $src $dst -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Remove-Item $stash -Recurse -Force -ErrorAction SilentlyContinue

$exe = Join-Path $old 'SteamSwitcher.exe'
$f = Get-Item $exe
Write-Output 'DONE — single build folder again.'
Write-Output ''
Write-Output ('EXE   : ' + $exe)
Write-Output ('SIZE  : ' + [math]::Round($f.Length/1MB,2) + ' MB (launcher; runtime sits beside it)')
Write-Output ('BUILT : ' + $f.LastWriteTime)
Write-Output ''
Write-Output '=== build\ ==='
Get-ChildItem $build | ForEach-Object {
    Write-Output ("  " + $(if($_.PSIsContainer){"[dir] "}else{"      "}) + $_.Name)
}
