$ErrorActionPreference = 'Continue'
$exe = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\build\release2\SteamSwitcher.exe'
$log = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\build\release2\logs\steam-switcher.log'

if (Test-Path $log) { Remove-Item $log -Force -ErrorAction SilentlyContinue }

for ($i = 1; $i -le 2; $i++) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $exe -PassThru
    $ready = $false
    while ($sw.ElapsedMilliseconds -lt 60000) {
        Start-Sleep -Milliseconds 100
        $p.Refresh()
        if ($p.HasExited) { break }
        if ($p.MainWindowHandle -ne 0 -and $p.Responding) { $ready = $true; break }
    }
    $sw.Stop()

    $label = if ($i -eq 1) { 'COLD' } else { 'WARM' }
    if ($ready) {
        Write-Output "$label : window ready in $($sw.ElapsedMilliseconds) ms"
    } elseif ($p.HasExited) {
        Write-Output "$label : PROCESS EXITED after $($sw.ElapsedMilliseconds) ms (exit $($p.ExitCode))"
    } else {
        Write-Output "$label : NOT READY after $($sw.ElapsedMilliseconds) ms"
    }

    Start-Sleep -Seconds 4
    # Leave the first instance running only long enough to write its log.
    if ($i -eq 1) { Start-Sleep -Seconds 2 }
}

Write-Output ''
Write-Output '=== LOG: errors only ==='
if (Test-Path $log) {
    $errors = Select-String -Path $log -Pattern 'ERROR|Exception' -ErrorAction SilentlyContinue
    if ($errors) {
        $errors | ForEach-Object { $_.Line } | Select-Object -First 12
    } else {
        Write-Output 'NO ERRORS / NO EXCEPTIONS'
    }
    Write-Output ''
    Write-Output '=== LOG: startup lines ==='
    Get-Content $log | Select-Object -First 8
} else {
    Write-Output 'NO LOG WRITTEN'
}
