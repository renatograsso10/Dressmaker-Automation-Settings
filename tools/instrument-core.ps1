param(
    [Parameter(Mandatory = $true)][string]$OriginalCore,
    [Parameter(Mandatory = $true)][string]$SettingsUI,
    [Parameter(Mandatory = $true)][string]$OutputCore,
    [string]$Cecil = (Join-Path $PSScriptRoot '..\patcher\win\Mono.Cecil.dll')
)
$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($Cecil)) | Out-Null
$core = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($OriginalCore))
$ui = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($SettingsUI))
try {
    $initialize = $core.MainModule.GetType('DressmakerAutomation.AutomationHooks').Methods | Where-Object { $_.Name -eq 'Initialize' }
    $uiInitialize = $ui.MainModule.GetType('DressmakerAutomation.SettingsUI.Bootstrap').Methods | Where-Object { $_.Name -eq 'Initialize' }
    $returns = @($initialize.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret })
    if ($returns.Count -ne 1) { throw 'Unexpected initializer shape' }
    if ($core.MainModule.AssemblyReferences | Where-Object { $_.Name -eq 'DressmakerAutomation.SettingsUI' }) { throw 'Core already instrumented' }
    $il = $initialize.Body.GetILProcessor()
    $il.InsertBefore($returns[0], $il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $il.InsertBefore($returns[0], $il.Create([Mono.Cecil.Cil.OpCodes]::Call, $core.MainModule.ImportReference($uiInitialize)))
    $core.Write([IO.Path]::GetFullPath($OutputCore))
} finally { $core.Dispose(); $ui.Dispose() }
