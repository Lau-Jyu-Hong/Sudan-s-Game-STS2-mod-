param(
    [string]$GameDir='',
    [string]$BaseLibPath='',
    [string]$DotNet='dotnet'
)
$ErrorActionPreference='Stop'
$taskProject=Join-Path $PSScriptRoot 'src\ArtuMod.csproj'
if(!(Test-Path -LiteralPath (Join-Path $GameDir 'data_sts2_windows_x86_64\sts2.dll'))){throw '找不到 sts2.dll，请指定正确的 -GameDir。'}
if(!(Test-Path -LiteralPath $BaseLibPath)){throw '找不到 BaseLib.dll，请指定正确的 -BaseLibPath。'}
& $DotNet build $taskProject -c Release --nologo --configfile (Join-Path $PSScriptRoot 'src\NuGet.Config') "-p:GameDir=$GameDir" "-p:BaseLibPath=$BaseLibPath"
if($LASTEXITCODE -ne 0){throw '编译失败。'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\bin\Release\net9.0\ArtuMod.dll') -Destination (Join-Path $PSScriptRoot 'package\ArtuMod\ArtuMod.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CREDITS.md') -Destination (Join-Path $PSScriptRoot 'package\ArtuMod\CREDITS.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\status-icons\来源与对应表.md') -Destination (Join-Path $PSScriptRoot 'package\ArtuMod\ICON_CREDITS.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\status-icons\UPSTREAM-LICENSE.txt') -Destination (Join-Path $PSScriptRoot 'package\ArtuMod\LICENSE_ICONS.txt') -Force
Write-Output '编译成功。package\ArtuMod 包含安装用文件。'
