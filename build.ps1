$ErrorActionPreference = 'Continue'
$proj = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\src\SteamSwitcher\SteamSwitcher.csproj'
$out  = 'H:\My Project Coding\Lindy My Boss\My Software\Steam Switcher\build\release'

# dotnet is not on PATH in this shell; use the known install location.
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { Write-Output 'dotnet.exe not found'; exit 1 }
Set-Alias dotnet $dotnet -Scope Script

Write-Output '=== BUILD (Release) ==='
$log = & dotnet build $proj -c Release -v minimal --nologo 2>&1
$code = $LASTEXITCODE
$log | ForEach-Object { $_ } | Select-Object -Last 40
Write-Output ("BUILD EXIT CODE: " + $code)

if ($code -ne 0) {
    Write-Output ''
    Write-Output '=== ERRORS ONLY ==='
    $log | Select-String -Pattern 'error|Error' | ForEach-Object { $_.Line }
    exit $code
}

Write-Output ''
Write-Output '=== PUBLISH single-file ==='
$plog = & dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out -v minimal --nologo 2>&1
$pcode = $LASTEXITCODE
$plog | Select-Object -Last 25
Write-Output ("PUBLISH EXIT CODE: " + $pcode)

if ($pcode -ne 0) {
    Write-Output ''
    Write-Output '=== PUBLISH ERRORS ==='
    $plog | Select-String -Pattern 'error' | ForEach-Object { $_.Line }
    exit $pcode
}

Write-Output ''
Write-Output '=== OUTPUT ==='
$exe = Join-Path $out 'SteamSwitcher.exe'
if (Test-Path $exe) {
    $f = Get-Item $exe
    Write-Output ("EXE: " + $f.FullName)
    Write-Output ("SIZE: " + [math]::Round($f.Length/1MB,2) + " MB")
    Write-Output ("BUILT: " + $f.LastWriteTime)
} else {
    Write-Output 'EXE NOT FOUND'
}
