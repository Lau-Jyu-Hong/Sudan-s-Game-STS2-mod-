param([string]$Body='', [int]$SettleMs=350)
$ErrorActionPreference='Stop'
if($Body){
 $ar=(& "$PSScriptRoot/trial-control.ps1" -Mode action -Body $Body|ConvertFrom-Json)
 [ordered]@{action=($Body|ConvertFrom-Json).action;ms=$ar.elapsed_ms;ok=$ar.result.Success;error=$ar.result.Error;cards=$ar.result.played_card_name}|ConvertTo-Json -Compress
 if(!$ar.result.Success){return}
 Start-Sleep -Milliseconds $SettleMs
}
$timer=[Diagnostics.Stopwatch]::StartNew()
do {
    $s=(& "$PSScriptRoot/trial-control.ps1"|ConvertFrom-Json).result
    if($s.WaitingForInput -or $s.Combat.CardSelection -or $s.Combat.HandCardSelection){break}
    Start-Sleep -Milliseconds 250
}while($timer.Elapsed.TotalSeconds -lt 6)
$o=[ordered]@{phase=$s.Phase;ready=$s.WaitingForInput;floor=$s.Run.CurrentFloor;gold=$s.Run.Gold}
if($s.Combat){
 $c=$s.Combat
 $o.turn=$c.RoundNumber; $o.energy=$c.Energy; $o.hp=$c.Player.CurrentHp; $o.block=$c.Player.Block
 $known=@('岩石苏丹牌－纵欲','岩石苏丹牌－奢靡','岩石苏丹牌－杀戮','岩石苏丹牌－征服','御用通行','调兵遣将','俺寻思','谋反的意图','不洁的思绪','知己知彼')
 $o.hand=@(for($i=0;$i -lt $c.Hand.Count;$i++){$h=$c.Hand[$i];$v=[ordered]@{i=$i;name=$h.Name;cost=$h.Cost;play=$h.CanPlay};if($h.Damage){$v.damage=$h.Damage};if($h.Block){$v.block=$h.Block};if($h.Name -notin $known -or $h.Description -match '\[(red|green)\]'){$v.text=$h.Description};if($h.ValidTargetIds.Count){$v.targets=$h.ValidTargetIds};$v})
 $o.enemies=@($c.Enemies|ForEach-Object{[ordered]@{id=$_.CombatId;name=$_.Name;hp=$_.CurrentHp;block=$_.Block;intent=$_.IntentLabel;buffs=@($_.Buffs|Select-Object Name,StackCount)}})
 $o.selection=$c.CardSelection; $o.hand_selection=$c.HandCardSelection; $o.potions=$c.Potions
}
if($s.Map){$o.map=$s.Map.AvailableNodes}
if($s.Reward){$o.reward=[ordered]@{items=@($s.Reward.Items|Select-Object Index,Type,Description);cards=$s.Reward.CardChoices;selection=$s.Reward.CardSelection;can_skip=$s.Reward.CanSkip}}
if($s.Event){$o.event=$s.Event}
if($s.Rest){$o.rest=$s.Rest}
if($s.Shop){$o.shop=$s.Shop}
if($s.Menu){$o.menu=$s.Menu}
if($s.Treasure){$o.treasure=$s.Treasure}
$o|ConvertTo-Json -Depth 30 -Compress
