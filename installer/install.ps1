param([string]$GameRoot = $env:DRESSMAKER_GAME_ROOT, [string]$StateRoot = $env:DRESSMAKER_UI_STATE_ROOT, [switch]$NonInteractive)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
if (-not $StateRoot) { $StateRoot = Join-Path $env:LOCALAPPDATA 'DressmakerAutomationUI' }
$StateRoot = [IO.Path]::GetFullPath($StateRoot)
$recordFile = Join-Path $StateRoot 'active-install.json'
$stage = $null
$record = $null
$committing = $false
$originalHash = '223583ff66b7b7ff799050c6f6265a3bc080f53376c912f240128bc55e469438'
$patchedHash = '3dc961aaaaac0f0f171df62ebbb3092eb54d52a8541802590101cbef1873a749'
$compatibleBuilds = @{
    $originalHash = $patchedHash
    '7eb845dccd48bd0fa73b7fba1ae00a00b29f76826ed465689af349ae506d295e' = '602a244b247e0b0240fcc27cd338283df9549b51ff46b3714200819cbd5c6f7d'
}
try {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'payload-hashes.json') -Raw | ConvertFrom-Json
    foreach ($property in $manifest.PSObject.Properties) {
        $file = Join-Path $PSScriptRoot $property.Name
        if (-not (Test-Path -LiteralPath $file) -or (Get-Sha256 $file) -ne $property.Value) { throw ('Package checksum failed: ' + $property.Name) }
    }
    if (-not $GameRoot) { $GameRoot = Find-Game }
    if (-not $GameRoot -and -not $NonInteractive) { $GameRoot = (Read-Host 'Paste the folder containing Dressmaker.exe').Trim().Trim('"') }
    if (-not $GameRoot) { throw 'Game folder not found. Pass -GameRoot or set DRESSMAKER_GAME_ROOT.' }
    $GameRoot = [IO.Path]::GetFullPath($GameRoot)
    $managed = Get-Managed $GameRoot
    if (-not $managed) { throw 'Dressmaker_Data\Managed\Assembly-CSharp.dll not found.' }
    Assert-Closed $GameRoot
    $gameDll = Join-Path $managed 'Assembly-CSharp.dll'
    $currentHash = Get-Sha256 $gameDll
    if (Test-Path -LiteralPath $recordFile) {
        $active = Get-Content -LiteralPath $recordFile -Raw | ConvertFrom-Json
        if ($active.GameRoot -ne $GameRoot) { throw 'UI already installed in a different game folder. Uninstall that copy first.' }
        foreach ($entry in $active.Files) {
            if ((Get-Sha256 (Join-Path $managed $entry.Name)) -ne $entry.InstalledHash) { throw ('Installed file changed: ' + $entry.Name + '. Restore or uninstall before reinstalling.') }
            if ($entry.Name -ne 'Assembly-CSharp.dll' -and $entry.InstalledHash -ne $manifest.($entry.Name)) { throw 'A different UI build is installed. Uninstall it before installing this build.' }
        }
        Write-Host 'Automation UI is already installed. Open Dressmaker and press F8.'
        exit 0
    }
    $needsPatch = $compatibleBuilds.ContainsKey($currentHash)
    if (-not $needsPatch -and $currentHash -notin $compatibleBuilds.Values) { throw ('Unsupported or modified game DLL. No files changed.' + [Environment]::NewLine + 'Game DLL: ' + $gameDll + [Environment]::NewLine + 'SHA256: ' + $currentHash + [Environment]::NewLine + 'Run diagnose.bat and share its report so this game build can be checked.') }
    $expectedPatchedHash = if ($needsPatch) { $compatibleBuilds[$currentHash] } else { $currentHash }
    New-Item -ItemType Directory -Path $StateRoot -Force | Out-Null
    $stage = Join-Path $StateRoot ('stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $candidate = Join-Path $stage 'Assembly-CSharp.dll'
    if ($needsPatch) {
        $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnetCommand) { throw '.NET Runtime 6 or newer required to patch the game.' }
        $patchOutput = & $dotnetCommand.Source (Join-Path $PSScriptRoot 'patcher\win\DressmakerAutomation.Patcher.dll') $gameDll (Join-Path $PSScriptRoot 'DressmakerAutomation.Core.dll') $candidate 2>&1
        if ($LASTEXITCODE -ne 0) { throw ('Patcher failed: ' + ($patchOutput -join [Environment]::NewLine)) }
        $patchOutput | ForEach-Object { Write-Host $_ }
        if (($patchOutput -join "`n") -match 'FAILED' -or (Get-Sha256 $candidate) -ne $expectedPatchedHash) { throw 'Patch result did not match the verified Windows port. No game files changed.' }
    }
    else { Copy-Item -LiteralPath $gameDll -Destination $candidate }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DressmakerAutomation.Core.dll') -Destination $stage
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DressmakerAutomation.SettingsUI.dll') -Destination $stage
    $backupDir = Join-Path $StateRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    $files = @()
    foreach ($name in @('DressmakerAutomation.Core.dll', 'DressmakerAutomation.SettingsUI.dll', 'Assembly-CSharp.dll')) {
        $destination = Join-Path $managed $name
        $previousHash = $null
        if (Test-Path -LiteralPath $destination) {
            Copy-Item -LiteralPath $destination -Destination (Join-Path $backupDir $name)
            $previousHash = Get-Sha256 $destination
            if ((Get-Sha256 (Join-Path $backupDir $name)) -ne $previousHash) { throw 'Backup verification failed. No game files changed.' }
        }
        $files += [pscustomobject]@{ Name = $name; PreviousHash = $previousHash; InstalledHash = (Get-Sha256 (Join-Path $stage $name)) }
    }
    $record = [pscustomobject]@{ Version = '1.0.4'; GameRoot = $GameRoot; Managed = $managed; BackupDir = $backupDir; Files = $files }
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $backupDir 'install-record.json') -Encoding UTF8
    $committing = $true
    foreach ($entry in $files) { Copy-Item -LiteralPath (Join-Path $stage $entry.Name) -Destination (Join-Path $managed $entry.Name) -Force }
    foreach ($entry in $files) { if ((Get-Sha256 (Join-Path $managed $entry.Name)) -ne $entry.InstalledHash) { throw 'Installed file verification failed.' } }
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordFile -Encoding UTF8
    $committing = $false
    Write-Host 'Installed: Dressmaker Automation + in-game settings.' -ForegroundColor Green
    Write-Host 'Launch the game. Settings open once at startup. Press F8 to hide or show them.'
    Write-Host 'Change settings, then click Save & apply. No restart required.'
    Write-Host ('Original files backed up: ' + $backupDir)
}
catch {
    if ($committing -and $record) {
        try { Restore-Backup $record; Write-Host 'Original files restored after installation failure.' }
        catch { Write-Host ('Rollback failed. Restore files from: ' + $record.BackupDir) -ForegroundColor Red }
    }
    Write-Host ('Install failed: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    if ($stage -and (Test-Path -LiteralPath $stage)) {
        Get-ChildItem -LiteralPath $stage -File | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
        Remove-Item -LiteralPath $stage -Force
    }
}
