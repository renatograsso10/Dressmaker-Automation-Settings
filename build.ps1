param(
    [Parameter(Mandatory = $true)][string]$GameManaged,
    [Parameter(Mandatory = $true)][string]$OriginalCore,
    [Parameter(Mandatory = $true)][string]$PatcherDirectory
)
$ErrorActionPreference = 'Stop'
$GameManaged = [IO.Path]::GetFullPath($GameManaged)
$OriginalCore = [IO.Path]::GetFullPath($OriginalCore)
$PatcherDirectory = [IO.Path]::GetFullPath($PatcherDirectory)
$cecil = Join-Path $PatcherDirectory 'Mono.Cecil.dll'
foreach ($path in @($OriginalCore,$cecil,(Join-Path $GameManaged 'Assembly-CSharp.dll'),(Join-Path $PatcherDirectory 'DressmakerAutomation.Patcher.dll'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Missing dependency: ' + $path) }
}
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$package = Join-Path $artifacts 'DressmakerAutomation-UI-Windows'
New-Item -ItemType Directory -Path $package -Force | Out-Null
& dotnet build (Join-Path $PSScriptRoot 'src\SettingsUI.csproj') -c Release "-p:GameManaged=$GameManaged"
if ($LASTEXITCODE -ne 0) { throw 'UI build failed' }
$ui = Join-Path $PSScriptRoot 'src\bin\Release\netstandard2.1\DressmakerAutomation.SettingsUI.dll'
Copy-Item -LiteralPath $ui -Destination $package -Force
& (Join-Path $PSScriptRoot 'tools\instrument-core.ps1') -OriginalCore $OriginalCore -SettingsUI $ui -OutputCore (Join-Path $package 'DressmakerAutomation.Core.dll') -Cecil $cecil
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'installer') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $package -Force }
$patcherTarget = Join-Path $package 'patcher\win'
New-Item -ItemType Directory -Path $patcherTarget -Force | Out-Null
Get-ChildItem -LiteralPath $PatcherDirectory -File | Where-Object { $_.Extension -in '.dll','.json' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $patcherTarget -Force }
foreach ($name in @('LICENSE','README.txt','NOTICE.txt')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $package -Force }
$licenses = Join-Path $package 'THIRD_PARTY_LICENSES'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_LICENSES\Mono.Cecil-LICENSE.txt') -Destination $licenses -Force
. (Join-Path $PSScriptRoot 'installer\Common.ps1')
$hashes = [ordered]@{}
foreach ($name in @('DressmakerAutomation.Core.dll','DressmakerAutomation.SettingsUI.dll','patcher\win\DressmakerAutomation.Patcher.dll','patcher\win\Mono.Cecil.dll')) { $hashes[$name] = Get-Sha256 (Join-Path $package $name) }
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'payload-hashes.json') -Encoding UTF8
$zip = Join-Path $artifacts 'DressmakerAutomation-UI-Windows-1.0.4.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
Write-Output ('Built: ' + $zip)
