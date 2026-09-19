$ErrorActionPreference = 'Continue'
$root = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'

Write-Output '=== build\ top level ==='
Get-ChildItem (Join-Path $root 'build') -ErrorAction SilentlyContinue | ForEach-Object {
    $kind = 'FILE'
    if ($_.PSIsContainer) { $kind = 'DIR ' }
    Write-Output ('  ' + $kind + '  ' + $_.Name)
}

Write-Output ''
Write-Output '=== build\release clutter ==='
$rel = Join-Path $root 'build\release'
$files = @(Get-ChildItem $rel -File -ErrorAction SilentlyContinue)
$dirs  = @(Get-ChildItem $rel -Directory -ErrorAction SilentlyContinue)
Write-Output ('  files at root : ' + $files.Count)
Write-Output ('  folders       : ' + $dirs.Count)
foreach ($d in $dirs) { Write-Output ('      DIR  ' + $d.Name) }

Write-Output ''
Write-Output '  --- by extension ---'
$files | Group-Object Extension | Sort-Object Count -Descending | ForEach-Object {
    $ext = $_.Name
    if ([string]::IsNullOrWhiteSpace($ext)) { $ext = '(none)' }
    Write-Output ('      ' + $ext.PadRight(10) + ' x' + $_.Count)
}

Write-Output ''
Write-Output '  --- the exe itself ---'
$exe = Join-Path $rel 'SteamSwitcher.exe'
if (Test-Path $exe) {
    $i = Get-Item $exe
    Write-Output ('      SteamSwitcher.exe  ' + [int]($i.Length/1KB) + ' KB   built ' + $i.LastWriteTime)
}

Write-Output ''
Write-Output '=== csproj publish settings ==='
$csproj = Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj'
foreach ($k in @('PublishSingleFile','SelfContained','IncludeNativeLibrariesForSelfExtract','IncludeAllContentForSelfExtract','EnableCompressionInSingleFile','RuntimeIdentifier','PublishReadyToRun','InvariantGlobalization','UseWPF')) {
    $line = Select-String -Path $csproj -Pattern $k -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($line) { Write-Output ('  ' + $line.Line.Trim()) }
    else { Write-Output ('  ' + $k + ' : (not set)') }
}

Write-Output ''
Write-Output '=== total size ==='
$sum = ($files | Measure-Object Length -Sum).Sum
$all = (Get-ChildItem $rel -Recurse -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
Write-Output ('  root files : ' + [int]($sum/1MB) + ' MB')
Write-Output ('  everything : ' + [int]($all/1MB) + ' MB')
