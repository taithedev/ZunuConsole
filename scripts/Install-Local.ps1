param(
    [string]$GorillaTagDir = "C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag",
    [string]$BuildConfiguration = "Release"
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\ZunuConsole.csproj'
$dll = Join-Path $PSScriptRoot "..\bin\$BuildConfiguration\ZunuConsole.dll"
$pluginDir = Join-Path $GorillaTagDir 'BepInEx\plugins'
if (!(Test-Path (Join-Path $GorillaTagDir 'BepInEx\core\BepInEx.dll'))) { throw 'Install BepInEx 5 first.' }
& msbuild $project "/p:Configuration=$BuildConfiguration" "/p:GorillaTagDir=$GorillaTagDir"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -Force $dll (Join-Path $pluginDir 'ZunuConsole.dll')
Write-Host "Installed ZunuConsole.dll to $pluginDir"
