<#
.SYNOPSIS
    Sestaví řešení a spustí všechny testovací projekty.

.DESCRIPTION
    `dotnet test` se v .NET SDK 10.0.400 spouští v režimu `--server dotnettestcli`, který
    aktuální Microsoft.Testing.Platform (2.3.3) pod xunit.v3 neobslouží — proces naběhne,
    nenajde ani jeden test a skončí s kódem 5 ("Zero tests ran"). Testovací projekty jsou
    přitom samostatně spustitelné aplikace, takže je pouštíme přes `dotnet run`, což funguje
    spolehlivě a dává stejný výstup.

    Až se protokol v SDK srovná, půjde tenhle skript nahradit prostým `dotnet test`.

.EXAMPLE
    .\scripts\test.ps1
    .\scripts\test.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipIntegration
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failed = @()

Write-Host '=== build ===' -ForegroundColor Cyan
dotnet build (Join-Path $repoRoot 'WakePark.sln') --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Build selhal.' -ForegroundColor Red
    exit 1
}

$projects = Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Filter '*.csproj' -Recurse
if ($SkipIntegration) { $projects = $projects | Where-Object { $_.BaseName -notlike '*IntegrationTests' } }

$projects | ForEach-Object {
    Write-Host "`n=== $($_.BaseName) ===" -ForegroundColor Cyan
    dotnet run --project $_.FullName --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { $failed += $_.BaseName }
}

if ($failed.Count -gt 0) {
    Write-Host "`nNeproslo: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}

Write-Host "`nVsechny testy prosly." -ForegroundColor Green
