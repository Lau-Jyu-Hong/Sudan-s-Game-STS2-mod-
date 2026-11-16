param([Parameter(Mandatory=$true)][string[]]$Names)
$ErrorActionPreference='Stop'
foreach($name in $Names){
 $s=(& "$PSScriptRoot/trial-control.ps1"|ConvertFrom-Json).result
 $sel=$s.Combat.HandCardSelection
 if(!$sel){throw 'No hand selection active'}
 $idx=-1
 for($i=0;$i -lt $sel.SelectableCards.Count;$i++){if($sel.SelectableCards[$i].Name -eq $name){$idx=$i;break}}
 if($idx -lt 0){throw "Selection candidate absent: $name"}
 $body=@{action='pick_card';card_index=$idx}|ConvertTo-Json -Compress
 $a=(& "$PSScriptRoot/trial-control.ps1" -Mode action -Body $body|ConvertFrom-Json).result
 if(!$a.Success){throw ($a|ConvertTo-Json -Compress)}
 Start-Sleep -Milliseconds 80
}
$s=(& "$PSScriptRoot/trial-control.ps1"|ConvertFrom-Json).result
if(!$s.Combat.HandCardSelection.CanConfirm){throw 'Selection cannot be confirmed'}
& "$PSScriptRoot/trial-step.ps1" -Body '{"action":"confirm_selection"}' -SettleMs 600
