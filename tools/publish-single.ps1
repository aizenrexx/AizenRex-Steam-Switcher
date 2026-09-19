$ErrorActionPreference = 'Continue'
$dotnet  = 'C:\Program Files\dotnet\dotnet.exe'
$root    = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$proj    = Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj'
$final   = Join-Path $root 'build\release'
$staging = Join-Path $root 'build\_stage_single'

Set-Location $root

$running = @(Get-Process -Name 'SteamSwitcher' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Output ('ABORT: SteamSwitcher is running (PID ' + ($running.Id -join ', ') + '). Close it first.')
    exit 2
}

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue }

Write-Output '=== PUBLISH (single file) ==='
# Everything is bundled into one exe. Compression stays OFF: it previously
# pushed cold start to ~39s, and disk space is not the problem being solved.
$log = & $dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:DebugType=none `
    -o $staging --nologo 2>&1

$code = $LASTEXITCODE
Write-Output ('publish exit: ' + $code)
if ($code -ne 0) {
    $log | Select-String -Pattern ': error' | ForEach-Object { $_.Line } | Select-Object -First 15
    exit 1
}

$exe = Join-Path $staging 'SteamSwitcher.exe'
if (-not (Test-Path $exe)) { Write-Output 'ABORT: exe missing from publish output.'; exit 1 }

Write-Output ''
Write-Output '=== what publish produced ==='
Get-ChildItem $staging -File | ForEach-Object {
    Write-Output ('  ' + $_.Name.PadRight(34) + ' ' + [int]($_.Length/1MB) + ' MB')
}

# Strip anything that is not the exe. .pdb debug files are the usual leftover.
$extra = @(Get-ChildItem $staging -File | Where-Object { $_.Name -ne 'SteamSwitcher.exe' })
foreach ($f in $extra) {
    Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
    Write-Output ('  removed: ' + $f.Name)
}

Write-Output ''
Write-Output '=== CARRY OVER USER DATA ==='
foreach ($sub in @('data','backups','exports','logs')) {
    $src = Join-Path $final $sub
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $staging $sub) -Recurse -Force -ErrorAction SilentlyContinue
        Write-Output ('  carried: ' + $sub)
    }
}

Write-Output ''
Write-Output '=== SWAP ==='
$old = Join-Path $root ('build\_old_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
if (Test-Path $final) {
    Rename-Item $final $old -ErrorAction SilentlyContinue
    if (Test-Path $final) { Write-Output 'ABORT: old release folder is locked.'; exit 1 }
    Write-Output ('  old build parked: ' + (Split-Path $old -Leaf))
}
Rename-Item $staging $final
Write-Output '  new build live'

Write-Output ''
Write-Output '=== RESULT ==='
$rootFiles = @(Get-ChildItem $final -File)
Write-Output ('  files at root : ' + $rootFiles.Count)
foreach ($f in $rootFiles) {
    Write-Output ('      ' + $f.Name + '  ' + [int]($f.Length/1MB) + ' MB')
}
$dirs = @(Get-ChildItem $final -Directory)
foreach ($d in $dirs) { Write-Output ('      DIR ' + $d.Name + '  (your data)') }

Write-Output ''
Write-Output '=== SMOKE TEST ==='
$logFile = Join-Path $final 'logs\steam-switcher.log'
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

# Two runs: the first pays a one-time extraction + Defender scan cost.
for ($i = 1; $i -le 2; $i++) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath (Join-Path $final 'SteamSwitcher.exe') -PassThru
    $ready = $false
    while ($sw.Elapsed.TotalSeconds -lt 60) {
        Start-Sleep -Milliseconds 250
        $p.Refresh()
        if ($p.HasExited) { break }
        if ($p.MainWindowHandle -ne 0) { $ready = $true; break }
    }
    $sw.Stop()

    if ($ready)          { Write-Output ('  run ' + $i + ': READY ' + [int]$sw.Elapsed.TotalMilliseconds + ' ms') }
    elseif ($p.HasExited){ Write-Output ('  run ' + $i + ': CRASHED exit ' + $p.ExitCode) }
    else                 { Write-Output ('  run ' + $i + ': NO WINDOW') }

    Start-Sleep -Seconds 2
    if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2 }
    if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 1
}

Write-Output ''
Write-Output '=== LOG ==='
if (Test-Path $logFile) {
    $t = Get-Content $logFile -Raw
    Write-Output ('  ERROR lines : ' + ([regex]::Matches($t, '(?im)^\s*\[?(ERROR|FATAL)')).Count)
    Write-Output ('  Exceptions  : ' + ([regex]::Matches($t, 'Exception')).Count)
} else {
    Write-Output '  no log produced'
}
