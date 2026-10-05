$ErrorActionPreference = 'Stop'

function Get-Sha256([string]$Path) {
    # Works in both Windows PowerShell 5.1 and PowerShell 7, without module discovery.
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Get-Managed([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { return $null }
    $matches = @(Get-ChildItem -LiteralPath $Root -Directory -Filter '*_Data' | ForEach-Object {
        $managed = Join-Path $_.FullName 'Managed'
        if (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll')) { $managed }
    })
    if ($matches.Count -gt 1) { throw 'Multiple game assemblies found. Choose the Dressmaker folder.' }
    if ($matches.Count -eq 1) { return $matches[0] }
    return $null
}

function Find-Game {
    $steamRoots = @($env:DRESSMAKER_STEAM_HOME, 'C:\Program Files (x86)\Steam', 'C:\Program Files\Steam')
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        try {
            $item = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
            if ($item.SteamPath) { $steamRoots += $item.SteamPath }
            if ($item.InstallPath) { $steamRoots += $item.InstallPath }
        } catch { }
    }
    foreach ($steamRoot in ($steamRoots | Where-Object { $_ } | Select-Object -Unique)) {
        $libraries = @($steamRoot)
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($line in Get-Content -LiteralPath $vdf) {
                if ($line -match '"path"\s+"([^"]+)"') { $libraries += $Matches[1].Replace('\\', '\') }
            }
        }
        foreach ($library in $libraries) {
            $candidate = Join-Path $library 'steamapps\common\Dressmaker'
            if (Get-Managed $candidate) { return $candidate }
        }
    }
    return $null
}

function Assert-Closed([string]$Root) {
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    foreach ($process in Get-Process -ErrorAction SilentlyContinue) {
        try { $processPath = $process.Path } catch { $processPath = $null }
        if ($processPath -and $processPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Dressmaker is running. Close the game before installing or uninstalling.'
        }
    }
}

function Restore-Backup($Record) {
    $backup = [IO.Path]::GetFullPath($Record.BackupDir)
    foreach ($entry in $Record.Files) {
        $destination = Join-Path $Record.Managed $entry.Name
        if ($entry.PreviousHash) { Copy-Item -LiteralPath (Join-Path $backup $entry.Name) -Destination $destination -Force }
        elseif (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Force }
    }
}
