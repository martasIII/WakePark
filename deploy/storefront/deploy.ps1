<#
.SYNOPSIS
    Ruční nasazení veřejného webu z vývojového počítače — záloha pro případ, že nejde GitHub.

.DESCRIPTION
    Běžně nasazuje GitHub Actions po zeleném CI (obraz z ghcr.io, deploy.sh na serveru).
    Tenhle skript postaví obraz lokálně, přenese ho jako archiv a spustí ho se značkou
    „manual“. Další nasazení z CI ho zase nahradí verzí z registru.

    Předpoklady: běžící Docker Desktop, SSH alias `dochazka` (root, klíč) a jednorázové
    nastavení serveru podle deploy/storefront/README.md.

.EXAMPLE
    .\deploy\storefront\deploy.ps1
#>
[CmdletBinding()]
param(
    [string]$SshHost = 'dochazka',
    [string]$RemoteDir = '/opt/wakepark/storefront'
)

$ErrorActionPreference = 'Stop'
# Výstup ze ssh (UTF-8) by se v konzoli Windows PowerShellu zobrazil rozbitě.
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$image = 'ghcr.io/martasiii/wakepark-storefront:manual'
$archive = Join-Path ([IO.Path]::GetTempPath()) 'wakepark-storefront.tar'

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    # Docker i ssh píšou průběh na stderr. Windows PowerShell 5.1 by ho při 'Stop' bral
    # jako chybu, i když příkaz uspěl — o úspěchu proto rozhoduje jen návratový kód.
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What selhalo (kód $LASTEXITCODE)." }
}

Write-Host '=== build obrazu ===' -ForegroundColor Cyan
# Linuxová platforma explicitně: na VPS běží amd64 bez ohledu na to, kde se staví.
Invoke-Checked { docker build --platform linux/amd64 -f (Join-Path $repoRoot 'src/frontend/WakePark.Storefront/Dockerfile') -t $image $repoRoot } 'Build obrazu'

Write-Host '=== přenos na server ===' -ForegroundColor Cyan
Invoke-Checked { docker save --output $archive $image } 'Export obrazu'
Invoke-Checked { scp $archive "${SshHost}:/tmp/wakepark-storefront.tar" } 'Přenos obrazu'
Remove-Item $archive

Write-Host '=== spuštění ===' -ForegroundColor Cyan
$remote = @"
set -e
docker load --input /tmp/wakepark-storefront.tar
rm /tmp/wakepark-storefront.tar
cd $RemoteDir
STOREFRONT_TAG=manual docker compose up -d
docker image prune -f > /dev/null
for i in `$(seq 1 20); do
  if curl -fsS http://127.0.0.1:5081/health > /dev/null; then echo 'web odpovídá'; exit 0; fi
  sleep 1
done
echo 'web po 20 s neodpovídá — docker compose logs' >&2
docker compose logs --tail 50
exit 1
"@
Invoke-Checked { $remote -replace "`r", '' | ssh $SshHost 'bash -s' } 'Spuštění na serveru'

Write-Host "`nNasazeno: https://novy.wake-dzban.cz" -ForegroundColor Green
