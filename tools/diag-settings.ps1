$ErrorActionPreference = 'Continue'
$root = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'

Write-Output '=== SteamSwitcher.exe copies ==='
Get-ChildItem (Join-Path $root 'build') -Filter 'SteamSwitcher.exe' -Recurse -File -ErrorAction SilentlyContinue |
    ForEach-Object {
        Write-Output ('  ' + $_.FullName)
        Write-Output ('      built ' + $_.LastWriteTime)
    }

Write-Output ''
Write-Output '=== running instance ==='
$p = @(Get-Process -Name 'SteamSwitcher' -ErrorAction SilentlyContinue)
Write-Output ('  running: ' + $p.Count)
foreach ($x in $p) {
    try { Write-Output ('    PID ' + $x.Id + ' -> ' + $x.Path) }
    catch { Write-Output ('    PID ' + $x.Id + ' -> (path unavailable)') }
}

Write-Output ''
Write-Output '=== markers in SettingsPage.xaml (source) ==='
$sp = Join-Path $root 'src\SteamSwitcher\Pages\SettingsPage.xaml'
foreach ($m in @('SteamDaddyUpdateUrl','UpdateUrlBox','UpstreamNotice','SilentElevation','Credit','Aizen','Riyad')) {
    $hit = @(Select-String -Path $sp -Pattern $m -SimpleMatch -ErrorAction SilentlyContinue).Count
    $state = 'MISSING'
    if ($hit -gt 0) { $state = "PRESENT ($hit)" }
    Write-Output ('  ' + $m.PadRight(22) + ' : ' + $state)
}

Write-Output ''
Write-Output '=== card/section headers in SettingsPage ==='
$matches = Select-String -Path $sp -Pattern 'Text="([A-Z][^"]{3,45})"' -AllMatches
foreach ($line in $matches) {
    foreach ($mm in $line.Matches) {
        Write-Output ('  ' + $mm.Groups[1].Value)
    }
}

Write-Output ''
Write-Output '=== file sizes ==='
foreach ($f in @('Pages\SettingsPage.xaml','Pages\SettingsPage.xaml.cs')) {
    $full = Join-Path $root ('src\SteamSwitcher\' + $f)
    if (Test-Path $full) {
        Write-Output ('  ' + $f + '  ' + (Get-Item $full).Length + ' bytes')
    }
}
