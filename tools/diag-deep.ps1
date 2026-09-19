$ErrorActionPreference = 'Continue'
$root = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$sp   = Join-Path $root 'src\SteamSwitcher\Pages\SettingsPage.xaml'

Write-Output '=== A. is the running exe the new one? ==='
$p = @(Get-Process -Name 'SteamSwitcher' -ErrorAction SilentlyContinue)
Write-Output ('  instances running: ' + $p.Count)
foreach ($x in $p) {
    try {
        Write-Output ('    PID ' + $x.Id + '  started ' + $x.StartTime)
        Write-Output ('      path: ' + $x.Path)
        if (Test-Path $x.Path) {
            Write-Output ('      exe built: ' + (Get-Item $x.Path).LastWriteTime)
        }
    } catch { Write-Output ('    PID ' + $x.Id + ' (details blocked - elevated)') }
}
$live = Join-Path $root 'build\release\SteamSwitcher.exe'
if (Test-Path $live) { Write-Output ('  on-disk exe built: ' + (Get-Item $live).LastWriteTime) }

Write-Output ''
Write-Output '=== B. does the app expose a version anywhere? ==='
foreach ($k in @('AssemblyVersion','Version','FileVersion','InformationalVersion')) {
    $l = Select-String -Path (Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj') -Pattern $k -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($l) { Write-Output ('  csproj: ' + $l.Line.Trim()) } else { Write-Output ('  csproj ' + $k + ': (not set)') }
}
if (Test-Path $live) {
    $vi = (Get-Item $live).VersionInfo
    Write-Output ('  exe FileVersion    : ' + $vi.FileVersion)
    Write-Output ('  exe ProductVersion : ' + $vi.ProductVersion)
}
$anyVersionUi = @(Select-String -Path (Join-Path $root 'src\SteamSwitcher\Pages\*.xaml') -Pattern 'Version' -ErrorAction SilentlyContinue).Count
Write-Output ('  "Version" mentions across all pages XAML: ' + $anyVersionUi)

Write-Output ''
Write-Output '=== C. SettingsPage structure: what is inside the scroll host? ==='
$txt = Get-Content $sp -Raw
Write-Output ('  file bytes: ' + $txt.Length)
foreach ($tag in @('ScrollViewer','StackPanel','ItemsControl','TabControl','Grid')) {
    $n = ([regex]::Matches($txt, '<' + $tag)).Count
    Write-Output ('  <' + $tag + '  x' + $n)
}

Write-Output ''
Write-Output '  --- order of visible section headers, with line numbers ---'
$lines = Get-Content $sp
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'Text="(Folder paths|Switching behaviour|SteamDaddy|Appearance|Account safety lock|UPDATE LINK|Notice from the SteamDaddy team)"') {
        Write-Output ('    line ' + ($i+1).ToString().PadLeft(4) + ' : ' + $matches[1])
    }
}

Write-Output ''
Write-Output '  --- any Visibility=Collapsed / Height limits that could hide sections ---'
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match 'Visibility="Collapsed"|MaxHeight|Height="\d+"') {
        Write-Output ('    line ' + ($i+1).ToString().PadLeft(4) + ' : ' + $lines[$i].Trim())
    }
}

Write-Output ''
Write-Output '=== D. is SettingsPage.xaml actually compiled into the project? ==='
$csproj = Get-Content (Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj') -Raw
Write-Output ('  explicit Page/Remove entries: ' + ([regex]::Matches($csproj, 'SettingsPage')).Count)
Write-Output ('  EnableDefaultItems: ' + $(if ($csproj -match 'EnableDefaultItems') { 'SET (check it)' } else { 'default (auto-include)' }))

Write-Output ''
Write-Output '=== E. pages folder inventory ==='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher\Pages') -File | ForEach-Object {
    Write-Output ('  ' + $_.Name.PadRight(34) + ' ' + $_.Length + ' bytes   ' + $_.LastWriteTime)
}
