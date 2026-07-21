#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Archive
)

$ErrorActionPreference = 'Stop'
$pluginRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins'
$target = Join-Path $pluginRoot 'DevConverter'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('devconverter-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    if (-not $Archive) {
        $Archive = Join-Path $temp 'DevConverter-PowerToysRun-x64.zip'
        $downloadUrl = 'https://github.com/pagict/DevConverter/releases/latest/download/DevConverter-PowerToysRun-x64.zip'
        Invoke-WebRequest -Uri $downloadUrl -OutFile $Archive
    }

    Expand-Archive -LiteralPath $Archive -DestinationPath $temp -Force
    $source = Join-Path $temp 'DevConverter'
    if (-not (Test-Path (Join-Path $source 'plugin.json'))) { throw 'The archive does not contain a DevConverter plugin.' }

    Get-Process | Where-Object ProcessName -Like 'PowerToys*' | Stop-Process -Force
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
