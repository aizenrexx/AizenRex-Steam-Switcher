$ErrorActionPreference = 'Continue'

# Read-only survey of what actually persists a Steam login on THIS machine,
# so the first-run design is based on the real layout, not assumptions.

$dirs = @(
    'C:\Program Files (x86)\Steam',
    'C:\Program Files (x86)\Steam Daddy',
    'C:\Program Files (x86)\Steam Main'
)

foreach ($d in $dirs) {
    Write-Output ('=== ' + $d + ' ===')
    if (-not (Test-Path $d)) { Write-Output '  (absent)'; Write-Output ''; continue }

    # ssfn* live in the Steam ROOT, and are per-machine+account auth blobs.
    $ssfn = @(Get-ChildItem $d -Filter 'ssfn*' -File -ErrorAction SilentlyContinue)
    Write-Output ("  ssfn files in root : " + $ssfn.Count)
    foreach ($s in $ssfn) { Write-Output ("      " + $s.Name + "   " + $s.Length + " bytes   " + $s.LastWriteTime) }

    foreach ($rel in @('config\config.vdf','config\loginusers.vdf')) {
        $p = Join-Path $d $rel
        if (Test-Path $p) {
            $f = Get-Item $p
            Write-Output ("  " + $rel.PadRight(22) + " " + $f.Length + " bytes   " + $f.LastWriteTime)
        } else {
            Write-Output ("  " + $rel.PadRight(22) + " MISSING")
        }
    }

    # Which accounts are stored, and how many claim MostRecent.
    $lu = Join-Path $d 'config\loginusers.vdf'
    if (Test-Path $lu) {
        $txt = Get-Content $lu -Raw
        $ids  = [regex]::Matches($txt, '"(\d{17})"')
        $most = [regex]::Matches($txt, '(?i)"MostRecent"\s*"1"')
        $rem  = [regex]::Matches($txt, '(?i)"RememberPassword"\s*"1"')
        $auto = [regex]::Matches($txt, '(?i)"AllowAutoLogin"\s*"1"')
        Write-Output ("  accounts stored    : " + $ids.Count)
        Write-Output ("  MostRecent=1       : " + $most.Count + "   <-- must be exactly 1")
        Write-Output ("  RememberPassword=1 : " + $rem.Count)
        Write-Output ("  AllowAutoLogin=1   : " + $auto.Count)
        foreach ($m in [regex]::Matches($txt, '(?i)"AccountName"\s*"([^"]*)"')) {
            Write-Output ("      account: " + $m.Groups[1].Value)
        }
    }

    $userdata = Join-Path $d 'userdata'
    if (Test-Path $userdata) {
        $kids = @(Get-ChildItem $userdata -Directory -ErrorAction SilentlyContinue)
        Write-Output ("  userdata profiles  : " + $kids.Count + "  [" + (($kids | ForEach-Object { $_.Name }) -join ', ') + "]")
    }
    Write-Output ''
}

Write-Output '=== REGISTRY HKCU\Software\Valve\Steam ==='
$k = 'HKCU:\Software\Valve\Steam'
if (Test-Path $k) {
    foreach ($n in @('AutoLoginUser','RememberPassword','SteamPath','SteamExe','LastGameNameUsed')) {
        $v = (Get-ItemProperty -Path $k -Name $n -ErrorAction SilentlyContinue).$n
        Write-Output ("  " + $n.PadRight(18) + " = " + $(if($null -ne $v){$v}else{'(not set)'}))
    }
    $ap = Join-Path $k 'ActiveProcess'
    if (Test-Path $ap) {
        $au = (Get-ItemProperty -Path $ap -Name 'ActiveUser' -ErrorAction SilentlyContinue).ActiveUser
        Write-Output ("  ActiveProcess\ActiveUser = " + $(if($null -ne $au){$au}else{'(not set)'}))
    }
} else {
    Write-Output '  key not present'
}

Write-Output ''
Write-Output '=== Is Steam installed at all? (fresh-PC simulation) ==='
Write-Output ("  HKCU Valve key exists : " + (Test-Path 'HKCU:\Software\Valve\Steam'))
Write-Output ("  HKLM Valve key exists : " + (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'))
$uninst = Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam' -ErrorAction SilentlyContinue
Write-Output ("  Steam uninstall entry : " + $(if($uninst){'present -> ' + $uninst.InstallLocation}else{'absent'}))
