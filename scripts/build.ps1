param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$GameDir
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$localGame = (Resolve-Path -LiteralPath $GameDir).Path
foreach ($relative in @('MelonLoader/net6/MelonLoader.dll', 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $localGame $relative))) {
        throw "Required local game reference not found: $relative"
    }
}
$buildProject = Join-Path $sourceRoot 'XuxuMusic/XuxuMusic.csproj'
& dotnet build $buildProject -c Release "-p:GameDir=$localGame" -p:SkipGameCopy=true
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
Write-Output 'Build complete. No files were copied to the game Mods folder.'
