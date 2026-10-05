param([string]$GameRoot = $env:DRESSMAKER_GAME_ROOT)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    if (-not $GameRoot) { $GameRoot = Find-Game }
    if (-not $GameRoot) { $GameRoot = (Read-Host 'Paste the folder containing Dressmaker.exe').Trim().Trim('"') }
    $managed = Get-Managed $GameRoot
    if (-not $managed) { throw 'Game assembly not found.' }
    $lines = @('Dressmaker UI compatibility report', ('Game folder: ' + $GameRoot))
    foreach ($name in @('Assembly-CSharp.dll', 'DressmakerAutomation.Core.dll', 'DressmakerAutomation.SettingsUI.dll')) {
        $path = Join-Path $managed $name
        if (Test-Path -LiteralPath $path) { $lines += ($name + ' SHA256: ' + (Get-Sha256 $path)) }
        else { $lines += ($name + ': absent') }
    }
    foreach ($name in @('Dressmaker.exe', 'UnityPlayer.dll')) {
        $path = Join-Path $GameRoot $name
        if (Test-Path -LiteralPath $path) { $lines += ($name + ' version: ' + [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion) }
    }
    $report = Join-Path $PSScriptRoot 'compatibility-report.txt'
    $lines | Set-Content -LiteralPath $report -Encoding UTF8
    $lines | ForEach-Object { Write-Host $_ }
    Write-Host ('Report saved: ' + $report)
} catch { Write-Host ('Diagnosis failed: ' + $_.Exception.Message); exit 1 }
