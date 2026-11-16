namespace ArtuMod;

public sealed class BlockReprisal():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,
    "格挡反震","获得{Block:diff()}格挡。此后你的格挡变为0时，对所有敌人施加1层虚弱，触发一次。")
{
    public override string PortraitPath=>"res://ArtuMod/images/loyalhound.tres";
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(9,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);
        await AddPower<BlockReprisalPower>(ctx,1);
    }
    protected override void OnUpgrade()=>DynamicVars.Block.UpgradeValueBy(3);
}

public sealed class DestructiveStrike():RewardCard(2,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy,
    "毁灭打击","造成{Damage:diff()}伤害，给予目标{Vulnerable:diff()}层易伤。")
{
    public override string PortraitPath=>"res://ArtuMod/images/ironhead.tres";
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(10,ValueProp.Move),new DynamicVar("Vulnerable",1)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        await Hit(ctx,play,DynamicVars.Damage.IntValue);
        if(play.Target is {IsDead:false} target)await AddPower<VulnerablePower>(ctx,DynamicVars["Vulnerable"].IntValue,target);
    }
    protected override void OnUpgrade()=>DynamicVars["Vulnerable"].UpgradeValueBy(1);
}

public sealed class BlockReprisalPower():ArtuPower("格挡反震",
    "你的格挡变为0时，对所有敌人施加{Amount}层虚弱，然后移除此效果。受击、主动移除及回合开始清除格挡均可触发。")
{
    public override Task AfterBlockBroken(PlayerChoiceContext ctx,Creature target,Creature? breaker)
        =>target==Owner?Trigger(ctx):Task.CompletedTask;
    public override Task AfterBlockCleared(Creature creature)
        =>creature==Owner&&Owner.Block==0?Trigger(new ThrowingPlayerChoiceContext()):Task.CompletedTask;
    private async Task Trigger(PlayerChoiceContext ctx)
    {
        if(Amount<=0||Owner.IsDead)return;
        int weak=Amount;
        // Remove first: overlapping block hooks and later block losses must not fire this use again.
        await PowerCmd.Remove(this);
        foreach(var enemy in Util.Enemies(Owner.Player!).ToArray())
            await PowerCmd.Apply<WeakPower>(ctx,enemy,weak,Owner,null);
    }
}
