#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Archive,
    [string]$Repository = 'pagict/DevConverter'
)

$ErrorActionPreference = 'Stop'
$pluginRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins'
$target = Join-Path $pluginRoot 'DevConverter'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('devconverter-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    if (-not $Archive) {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            throw 'GitHub CLI (gh) is required to download this private repository release.'
        }
        & gh release download --repo $Repository --pattern 'DevConverter-PowerToysRun-x64.zip' --dir $temp --clobber
        if ($LASTEXITCODE -ne 0) { throw 'Failed to download the latest DevConverter release.' }
        $Archive = Join-Path $temp 'DevConverter-PowerToysRun-x64.zip'
    }

    Expand-Archive -LiteralPath $Archive -DestinationPath $temp -Force
    $source = Join-Path $temp 'DevConverter'
    if (-not (Test-Path (Join-Path $source 'plugin.json'))) { throw 'The archive does not contain a DevConverter plugin.' }

    Get-Process PowerToys -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
    if (Test-Path $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    Copy-Item -LiteralPath $source -Destination $target -Recurse

    $powerToys = Join-Path $env:LOCALAPPDATA 'PowerToys\PowerToys.exe'
    if (Test-Path $powerToys) { Start-Process -FilePath $powerToys }
    Write-Host "Installed DevConverter to $target"
}
finally {
    if (Test-Path $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}
