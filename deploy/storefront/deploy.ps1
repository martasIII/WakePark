<#
.SYNOPSIS
    Ruční nasazení webu a API z vývojového počítače — záloha pro případ, že nejde GitHub.

.DESCRIPTION
    Běžně nasazuje GitHub Actions po zeleném CI (obraz z ghcr.io, deploy.sh na serveru).
    Tenhle skript postaví oba obrazy lokálně, přenese je jako archiv a spustí je se značkou
    „manual“ se stejnými kroky jako deploy.sh (migrace, import katalogu). Další nasazení z CI ho zase nahradí verzí z registru.

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
$images = @{
    'ghcr.io/martasiii/wakepark-storefront:manual' = 'src/frontend/WakePark.Storefront/Dockerfile'
    'ghcr.io/martasiii/wakepark-api:manual'        = 'src/backend/WakePark.Api/Dockerfile'
}
$archive = Join-Path ([IO.Path]::GetTempPath()) 'wakepark-images.tar'

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    # Docker i ssh píšou průběh na stderr. Windows PowerShell 5.1 by ho při 'Stop' bral
    # jako chybu, i když příkaz uspěl — o úspěchu proto rozhoduje jen návratový kód.
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What selhalo (kód $LASTEXITCODE)." }
}

Write-Host '=== build obrazů ===' -ForegroundColor Cyan
foreach ($image in $images.Keys) {
    # Linuxová platforma explicitně: na VPS běží amd64 bez ohledu na to, kde se staví.
    Invoke-Checked { docker build --platform linux/amd64 -f (Join-Path $repoRoot $images[$image]) -t $image $repoRoot } "Build $image"
}

Write-Host '=== přenos na server ===' -ForegroundColor Cyan
Invoke-Checked { docker save --output $archive @($images.Keys) } 'Export obrazů'
Invoke-Checked { scp $archive "${SshHost}:/tmp/wakepark-images.tar" } 'Přenos obrazů'
Remove-Item $archive

Write-Host '=== spuštění ===' -ForegroundColor Cyan
$remote = @"
set -e
docker load --input /tmp/wakepark-images.tar
rm /tmp/wakepark-images.tar
cd $RemoteDir
export WAKEPARK_TAG=manual
docker compose up -d --wait db
docker compose run --rm --no-deps api migrate
docker compose run --rm --no-deps api import-catalog
docker compose up -d --remove-orphans
docker image prune -f > /dev/null
for i in `$(seq 1 30); do
  if curl -fsS http://127.0.0.1:5081/eshop 2> /dev/null | grep -q 'class="product"'; then echo 'web ukazuje nabídku'; exit 0; fi
  sleep 1
done
echo 'web po 30 s neukazuje nabídku — docker compose logs' >&2
docker compose logs --tail 50
exit 1
"@
Invoke-Checked { $remote -replace "`r", '' | ssh $SshHost 'bash -s' } 'Spuštění na serveru'

Write-Host "`nNasazeno: https://novy.wake-dzban.cz" -ForegroundColor Green
