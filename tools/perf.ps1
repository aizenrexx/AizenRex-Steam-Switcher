$ErrorActionPreference = 'Continue'

# Measures the real cost of the folders ReadStatus() walks at startup,
# so the optimisation targets the actual bottleneck instead of a guess.

$dirs = @(
    'C:\Program Files (x86)\Steam',
    'C:\Program Files (x86)\Steam Main',
    'C:\Program Files (x86)\Steam Daddy'
)

foreach ($d in $dirs) {
    Write-Output ('=== ' + $d + ' ===')
    if (-not (Test-Path $d)) { Write-Output '  DOES NOT EXIST'; Write-Output ''; continue }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $steamapps = Join-Path $d 'steamapps'
    $acf = 0
    if (Test-Path $steamapps) {
        $acf = @(Get-ChildItem $steamapps -Filter 'appmanifest_*.acf' -File -ErrorAction SilentlyContinue).Count
    }
    $t1 = $sw.ElapsedMilliseconds
    Write-Output ("  appmanifest count : $acf   (${t1} ms)")

    # The recursive lua scan - run twice in DetectKind AND Inspect.
    $sw.Restart()
    $stplug = Join-Path $d 'config\stplug-in'
    $lua = 0
    if (Test-Path $stplug) {
        $lua = @(Get-ChildItem $stplug -Filter '*.lua' -Recurse -File -ErrorAction SilentlyContinue).Count
    }
    $t2 = $sw.ElapsedMilliseconds
    Write-Output ("  lua files (RECURSIVE, runs 2x) : $lua   (${t2} ms each)")

    $sw.Restart()
    $depot = Join-Path $d 'depotcache'
    $man = 0
    if (Test-Path $depot) {
        $man = @(Get-ChildItem $depot -Filter '*.manifest' -File -ErrorAction SilentlyContinue).Count
    }
    $t3 = $sw.ElapsedMilliseconds
    Write-Output ("  depot manifests   : $man   (${t3} ms)")

    $total = $t1 + ($t2 * 2) + $t3
    Write-Output ("  >>> approx per-folder cost: ${total} ms")
    Write-Output ''
}

Write-Output '=== WHAT DASHBOARD SEES ==='
foreach ($d in $dirs) {
    $exists = Test-Path $d
    Write-Output ("  " + $d.PadRight(42) + " exists=" + $exists)
}
