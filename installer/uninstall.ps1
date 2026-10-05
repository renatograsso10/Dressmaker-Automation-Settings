param([string]$StateRoot = $env:DRESSMAKER_UI_STATE_ROOT)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $StateRoot) { $StateRoot = Join-Path $env:LOCALAPPDATA 'DressmakerAutomationUI' }
$StateRoot = [IO.Path]::GetFullPath($StateRoot)
$recordFile = Join-Path $StateRoot 'active-install.json'
try {
    if (-not (Test-Path -LiteralPath $recordFile)) { throw 'No installation record found.' }
    $record = Get-Content -LiteralPath $recordFile -Raw | ConvertFrom-Json
    $backupPrefix = [IO.Path]::GetFullPath((Join-Path $StateRoot 'backups')).TrimEnd('\') + '\'
    if (-not ([IO.Path]::GetFullPath($record.BackupDir)).StartsWith($backupPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path is outside the installer backup folder.' }
    if ((Get-Managed $record.GameRoot) -ne $record.Managed) { throw 'Game layout changed. No files restored.' }
    Assert-Closed $record.GameRoot
    foreach ($entry in $record.Files) {
        $destination = Join-Path $record.Managed $entry.Name
        if ((Get-Sha256 $destination) -ne $entry.InstalledHash) { throw ('Installed file changed: ' + $entry.Name + '. Refusing to overwrite an update or another mod.') }
        if ($entry.PreviousHash -and (Get-Sha256 (Join-Path $record.BackupDir $entry.Name)) -ne $entry.PreviousHash) { throw ('Backup checksum failed: ' + $entry.Name) }
    }
    $safety = Join-Path $StateRoot ('uninstall-safety-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $safety | Out-Null
    foreach ($entry in $record.Files) { Copy-Item -LiteralPath (Join-Path $record.Managed $entry.Name) -Destination $safety }
    try { Restore-Backup $record }
    catch {
        foreach ($entry in $record.Files) { Copy-Item -LiteralPath (Join-Path $safety $entry.Name) -Destination (Join-Path $record.Managed $entry.Name) -Force }
        throw
    }
    Move-Item -LiteralPath $recordFile -Destination (Join-Path $safety 'active-install.json')
    Write-Host 'Uninstalled. Files restored to their state before this UI installer ran.' -ForegroundColor Green
    Write-Host 'Your CFG settings and game saves are retained.'
}
catch { Write-Host ('Uninstall failed: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
