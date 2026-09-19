$ErrorActionPreference = 'Continue'
$exe = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\build\release\SteamSwitcher.exe'
$log = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\build\release\logs\steam-switcher.log'

# Measures wall-clock time from launch to the window being responsive,
# repeated so the cold start and warm starts are both visible.

for ($i = 1; $i -le 3; $i++) {
    Get-Process SteamSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    if (Test-Path $log) { Remove-Item $log -Force -ErrorAction SilentlyContinue }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $exe -PassThru

    # Wait until the main window exists and the app pumps messages.
    $ready = $false
    while ($sw.ElapsedMilliseconds -lt 60000) {
        Start-Sleep -Milliseconds 100
        $p.Refresh()
        if ($p.HasExited) { break }
        if ($p.MainWindowHandle -ne 0 -and $p.Responding) { $ready = $true; break }
    }
    $sw.Stop()

    $label = if ($i -eq 1) { 'COLD' } else { "WARM $($i-1)" }
    if ($ready) {
        Write-Output ("$label : window ready in " + $sw.ElapsedMilliseconds + " ms")
    } else {
        Write-Output ("$label : NOT READY after " + $sw.ElapsedMilliseconds + " ms")
    }

    # What the app itself logged about its own startup.
    Start-Sleep -Seconds 3
    if (Test-Path $log) {
        $lines = Get-Content $log
        $first = $lines | Select-Object -First 1
        $last  = $lines | Select-Object -Last 1
        if ($first -match '^(\S+ \S+)' ) { $t0 = [datetime]::Parse($matches[1]) }
        if ($last  -match '^(\S+ \S+)' ) { $t1 = [datetime]::Parse($matches[1]) }
        if ($t0 -and $t1) {
            Write-Output ("        app-internal span: " + [int]($t1 - $t0).TotalMilliseconds + " ms")
        }
    }
}

Get-Process SteamSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output ''
Write-Output '=== EXE INFO ==='
$f = Get-Item $exe
Write-Output ("Size: " + [math]::Round($f.Length/1MB,2) + " MB")
Write-Output ("Files in release folder: " + @(Get-ChildItem (Split-Path $exe) -File).Count)
