$ErrorActionPreference = 'Continue'
$root = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher'
$out  = Join-Path $root 'docs\_facts.txt'
New-Item -ItemType Directory -Force -Path (Join-Path $root 'docs') | Out-Null
$sb = New-Object System.Text.StringBuilder

function W($t) { [void]$sb.AppendLine($t) }

W '===== CORE SOURCE FILES ====='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher\Core') -File -Filter *.cs |
    Sort-Object Name | ForEach-Object {
        $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
        W ('  ' + $_.Name.PadRight(28) + ' ' + $_.Length.ToString().PadLeft(7) + ' B  ' + $lines.ToString().PadLeft(5) + ' lines')
    }

W ''
W '===== PAGES ====='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher\Pages') -File |
    Sort-Object Name | ForEach-Object {
        $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
        W ('  ' + $_.Name.PadRight(28) + ' ' + $_.Length.ToString().PadLeft(7) + ' B  ' + $lines.ToString().PadLeft(5) + ' lines')
    }

W ''
W '===== ROOT SRC FILES ====='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher') -File |
    Sort-Object Name | ForEach-Object {
        W ('  ' + $_.Name.PadRight(28) + ' ' + $_.Length.ToString().PadLeft(7) + ' B')
    }

W ''
W '===== THEMES ====='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher\Themes') -File -ErrorAction SilentlyContinue |
    ForEach-Object { W ('  ' + $_.Name.PadRight(28) + ' ' + $_.Length + ' B') }

W ''
W '===== PUBLIC TYPES AND MEMBERS PER CORE FILE ====='
Get-ChildItem (Join-Path $root 'src\SteamSwitcher\Core') -File -Filter *.cs |
    Sort-Object Name | ForEach-Object {
        W ('--- ' + $_.Name)
        Select-String -Path $_.FullName -Pattern '^\s*(public|internal)\s+(sealed\s+|static\s+|partial\s+|abstract\s+)*(class|record|enum|struct|interface)\s+\w+' |
            ForEach-Object { W ('    TYPE   ' + $_.Line.Trim()) }
        Select-String -Path $_.FullName -Pattern '^\s*public\s+(static\s+)?(async\s+)?[\w<>\[\],\?\.]+\s+\w+\s*\(' |
            ForEach-Object { W ('    METHOD ' + $_.Line.Trim()) }
    }

W ''
W '===== CSPROJ (verbatim) ====='
Get-Content (Join-Path $root 'src\SteamSwitcher\SteamSwitcher.csproj') | ForEach-Object { W ('  ' + $_) }

W ''
W '===== SETTINGS PROPERTIES ====='
Select-String -Path (Join-Path $root 'src\SteamSwitcher\Core\Settings.cs') -Pattern '^\s*public\s+.*\{\s*get' |
    ForEach-Object { W ('  ' + $_.Line.Trim()) }

W ''
W '===== MODELS ====='
Select-String -Path (Join-Path $root 'src\SteamSwitcher\Core\Models.cs') -Pattern '^\s*(public|internal)\s+.*(class|enum|record)|^\s*public\s+.*\{\s*get' |
    ForEach-Object { W ('  ' + $_.Line.Trim()) }

W ''
W '===== BUILD OUTPUT ====='
$rel = Join-Path $root 'build\release'
if (Test-Path $rel) {
    $f = @(Get-ChildItem $rel -File)
    W ('  root files: ' + $f.Count)
    $f | ForEach-Object { W ('    ' + $_.Name + '  ' + [int]($_.Length/1MB) + ' MB  built ' + $_.LastWriteTime) }
    $exe = Join-Path $rel 'SteamSwitcher.exe'
    if (Test-Path $exe) {
        $vi = (Get-Item $exe).VersionInfo
        W ('  ProductVersion: ' + $vi.ProductVersion)
        W ('  Company       : ' + $vi.CompanyName)
        W ('  ProductName   : ' + $vi.ProductName)
    }
}

W ''
W '===== BUILD FOLDER TREE ====='
Get-ChildItem (Join-Path $root 'build') -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { W ('  DIR ' + $_.Name) }

W ''
W '===== TOOLS ====='
Get-ChildItem (Join-Path $root 'tools') -File -ErrorAction SilentlyContinue |
    ForEach-Object { W ('  ' + $_.Name.PadRight(30) + ' ' + $_.Length + ' B') }

W ''
W '===== DOCS ====='
Get-ChildItem (Join-Path $root 'docs') -File -ErrorAction SilentlyContinue |
    ForEach-Object { W ('  ' + $_.Name.PadRight(30) + ' ' + $_.Length + ' B') }

W ''
W '===== TOP LEVEL ====='
Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
    $k = if ($_.PSIsContainer) { 'DIR ' } else { 'FILE' }
    W ('  ' + $k + ' ' + $_.Name)
}

W ''
W '===== LIVE STEAM STATE ====='
foreach ($p in @('C:\Program Files (x86)\Steam','C:\Program Files (x86)\Steam Main','C:\Program Files (x86)\Steam Daddy')) {
    if (Test-Path $p) {
        $acf = @(Get-ChildItem (Join-Path $p 'steamapps') -Filter '*.acf' -ErrorAction SilentlyContinue).Count
        $lua = @(Get-ChildItem (Join-Path $p 'config\stplug-in') -Filter '*.lua' -ErrorAction SilentlyContinue).Count
        W ('  ' + $p)
        W ('      exists=yes  acf=' + $acf + '  lua=' + $lua)
    } else {
        W ('  ' + $p + '  exists=NO')
    }
}

W ''
W '===== REGISTRY ====='
$k = 'HKCU:\Software\Valve\Steam'
if (Test-Path $k) {
    $r = Get-ItemProperty $k
    W ('  AutoLoginUser  : ' + $r.AutoLoginUser)
    W ('  SteamPath      : ' + $r.SteamPath)
}

W ''
W '===== VDF TEST HARNESS ====='
$vt = Join-Path $root 'tools\vdftest\Program.cs'
if (Test-Path $vt) {
    W ('  Program.cs ' + (Get-Item $vt).Length + ' B')
    Select-String -Path $vt -Pattern 'TEST \d' | ForEach-Object { W ('    ' + $_.Line.Trim()) }
}

[System.IO.File]::WriteAllText($out, $sb.ToString())
Write-Output ('WROTE ' + $out + '  ' + (Get-Item $out).Length + ' bytes')
Write-Output ''
Get-Content $out
