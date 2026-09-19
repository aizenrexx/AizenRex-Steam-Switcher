$ErrorActionPreference = 'Continue'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
$root   = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$proj   = Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj'
$final  = Join-Path $root 'build\release'
$staging = Join-Path $root 'build\_publish_new'

Set-Location $root

# Refuse to touch the live folder while the app is running; a locked exe is
# what broke the previous publish.
$running = @(Get-Process -Name 'SteamSwitcher' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Output ('ABORT: SteamSwitcher is running (PID ' + ($running.Id -join ', ') + '). Close it first.')
    exit 2
}

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue }

Write-Output '=== PUBLISH ==='
$log = & $dotnet publish $proj -c Release -r win-x64 --self-contained true -o $staging --nologo 2>&1
$code = $LASTEXITCODE
Write-Output ('publish exit: ' + $code)
if ($code -ne 0) {
    $log | Select-String -Pattern ': error' | ForEach-Object { $_.Line } | Select-Object -First 15
    exit 1
}

$exe = Join-Path $staging 'SteamSwitcher.exe'
if (-not (Test-Path $exe)) { Write-Output 'ABORT: SteamSwitcher.exe missing from the publish output.'; exit 1 }
Write-Output ('published files : ' + @(Get-ChildItem $staging -Recurse -File).Count)

# Carry over the user's data so an update never wipes settings or history.
Write-Output ''
Write-Output '=== CARRY OVER USER DATA ==='
foreach ($sub in @('data','backups','exports','logs')) {
    $src = Join-Path $final $sub
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $staging $sub) -Recurse -Force -ErrorAction SilentlyContinue
        Write-Output ('  carried: ' + $sub)
    }
}

# Swap only after the new build is proven present.
Write-Output ''
Write-Output '=== SWAP ==='
$old = Join-Path $root ('build\_old_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
if (Test-Path $final) {
    Rename-Item $final $old -ErrorAction SilentlyContinue
    if (Test-Path $final) { Write-Output 'ABORT: could not move the old release folder (locked).'; exit 1 }
    Write-Output ('  old build parked at: ' + (Split-Path $old -Leaf))
}
Rename-Item $staging $final
Write-Output ('  new build live at: ' + $final)

# Launch once and read the log back, so "it works" is measured, not assumed.
Write-Output ''
Write-Output '=== SMOKE TEST ==='
$logFile = Join-Path $final 'logs\steam-switcher.log'
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath (Join-Path $final 'SteamSwitcher.exe') -PassThru
$ready = $false
while ($sw.Elapsed.TotalSeconds -lt 40) {
    Start-Sleep -Milliseconds 400
    $p.Refresh()
    if ($p.HasExited) { break }
    if ($p.MainWindowHandle -ne 0) { $ready = $true; break }
}
$sw.Stop()

if ($ready) { Write-Output ('  WINDOW READY in ' + [int]$sw.Elapsed.TotalMilliseconds + ' ms') }
elseif ($p.HasExited) { Write-Output ('  CRASHED - exit code ' + $p.ExitCode) }
else { Write-Output '  NO WINDOW within 40s' }

Start-Sleep -Seconds 3
if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2 }
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }

Write-Output ''
Write-Output '=== LOG ==='
if (Test-Path $logFile) {
    $t = Get-Content $logFile -Raw
    $errs = ([regex]::Matches($t, '(?im)^\s*\[?(ERROR|FATAL)')).Count
    $exc  = ([regex]::Matches($t, 'Exception')).Count
    Write-Output ('  ERROR lines : ' + $errs)
    Write-Output ('  Exceptions  : ' + $exc)
    Write-Output '  --- first-run / setup lines ---'
    Get-Content $logFile | Select-String -Pattern 'setup|First-run|profiles configured|Dashboard' | Select-Object -First 8 | ForEach-Object { Write-Output ('    ' + $_.Line) }
    if ($errs -gt 0 -or $exc -gt 0) {
        Write-Output '  --- problems ---'
        Get-Content $logFile | Select-String -Pattern 'ERROR|FATAL|Exception' | Select-Object -First 10 | ForEach-Object { Write-Output ('    ' + $_.Line) }
    }
} else {
    Write-Output '  no log file produced'
}
