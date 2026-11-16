param([Parameter(Mandatory)][string]$GameDir)
$ErrorActionPreference='Stop'
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw '请先退出游戏，再安装或更新模组。'}
if(!(Test-Path -LiteralPath (Join-Path $GameDir 'SlayTheSpire2.exe'))){throw '游戏目录不正确。'}
$taskSource=Join-Path $PSScriptRoot 'package\ArtuMod'
$taskTarget=Join-Path $GameDir 'mods\ArtuMod'
$taskFiles=@('ArtuMod.dll','ArtuMod.pck','ArtuMod.json','CREDITS.md','ICON_CREDITS.md','LICENSE_ICONS.txt')
foreach($taskFile in $taskFiles){if(!(Test-Path -LiteralPath (Join-Path $taskSource $taskFile))){throw "缺少 $taskFile"}}
if(Test-Path -LiteralPath $taskTarget){
    $taskBackup=Join-Path $PSScriptRoot ('backups\ArtuMod-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
    foreach($taskFile in $taskFiles){$taskOld=Join-Path $taskTarget $taskFile;if(Test-Path -LiteralPath $taskOld){Copy-Item -LiteralPath $taskOld -Destination $taskBackup}}
}
New-Item -ItemType Directory -Path $taskTarget -Force | Out-Null
foreach($taskFile in $taskFiles){Copy-Item -LiteralPath (Join-Path $taskSource $taskFile) -Destination (Join-Path $taskTarget $taskFile) -Force}
Write-Output "已安装到 $taskTarget。启动游戏并启用阿尔图及 BaseLib。"
