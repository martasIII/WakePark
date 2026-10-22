<#
.SYNOPSIS
    Stáhne čerstvou zálohu databáze e-shopu ze serveru na tento počítač.

.DESCRIPTION
    Záloha mimo server (ADR 0006): na VPS se databáze zálohuje každou noc, ale když umře
    server, zemřou s ním i zálohy. Tenhle skript:

      1. na serveru spustí backup-db.sh (čerstvý pg_dump),
      2. ověří, že archiv není poškozený (gzip -t),
      3. stáhne ho přes scp a porovná velikost,
      4. smaže místní zálohy starší než $KeepDays dní (nejnovějších 10 nechá vždy).

    Spouští ho Plánovač úloh Windows každý den (a hned po zapnutí počítače, když se den
    zmeškal). Průběh se zapisuje do backup.log ve složce se zálohami.

    Zálohy obsahují osobní údaje zákazníků — složka proto NENÍ v OneDrivu ani jinde v cloudu.

.EXAMPLE
    .\scripts\backup-from-server.ps1
#>
[CmdletBinding()]
param(
    [string]$SshHost = 'dochazka',
    [string]$Target = (Join-Path $env:USERPROFILE 'Zalohy\WakePark'),
    [int]$KeepDays = 60
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $Target | Out-Null
$log = Join-Path $Target 'backup.log'

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Message
    Add-Content -Path $log -Value $line -Encoding UTF8
    Write-Host $line
}

function Invoke-Remote([string]$Command) {
    # ssh píše průběh na stderr; o úspěchu rozhoduje jen návratový kód (Windows PowerShell 5.1).
    $ErrorActionPreference = 'Continue'
    $output = & ssh -o BatchMode=yes -o ConnectTimeout=20 $SshHost $Command 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Příkaz na serveru selhal (kód $LASTEXITCODE): $Command" }
    return $output
}

try {
    $remote = (Invoke-Remote '/opt/wakepark/storefront/backup-db.sh && ls -1t /var/backups/wakepark/wakepark-*.sql.gz | head -1' |
        Select-Object -Last 1).Trim()
    if ($remote -notmatch '^/var/backups/wakepark/wakepark-\d{4}-\d{2}-\d{2}\.sql\.gz$') {
        throw "Neočekávaný název zálohy na serveru: '$remote'"
    }

    Invoke-Remote "gzip -t $remote" | Out-Null
    $remoteSize = [long](Invoke-Remote "stat -c %s $remote" | Select-Object -Last 1)

    $local = Join-Path $Target ('wakepark-{0:yyyy-MM-dd_HHmm}.sql.gz' -f (Get-Date))
    $ErrorActionPreference = 'Continue'
    & scp -q -o BatchMode=yes "${SshHost}:$remote" $local 2>$null
    $scpExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($scpExit -ne 0) { throw "Stažení selhalo (scp, kód $scpExit)." }

    $localSize = (Get-Item $local).Length
    if ($localSize -ne $remoteSize) {
        Remove-Item $local -Force
        throw "Stažená záloha má $localSize B, na serveru $remoteSize B — smazána, zkusí se příště."
    }

    # Úklid: starší než $KeepDays dní pryč, ale nejnovějších 10 zůstane vždy.
    Get-ChildItem $Target -Filter 'wakepark-*.sql.gz' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -Skip 10 |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } |
        Remove-Item -Force

    Write-Log ("OK  {0}  ({1:N0} kB)" -f (Split-Path $local -Leaf), ($localSize / 1KB))
    exit 0
}
catch {
    Write-Log "CHYBA  $($_.Exception.Message)"
    exit 1
}
