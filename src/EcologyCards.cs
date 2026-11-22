namespace ArtuMod;
public sealed class ArrayThrust():RewardCard(1,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy,"阵列突刺","造成{Damage:diff()}伤害。招募{Troops}杂兵。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(7,ValueProp.Move),new DynamicVar("Troops",1)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {await Hit(ctx,play,DynamicVars.Damage.IntValue);await ArmyPower.Gain(ctx,Owner,0,DynamicVars["Troops"].IntValue,play.Card);}
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
public sealed class SacrificeForDuty():RewardCard(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"以身受命","消耗1杂兵。抽{Cards}张牌。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Cards",2)];
    protected override bool IsPlayable=>Owner.Creature.GetPowerAmount<LevyPower>()>0;
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        var levy=Owner.Creature.GetPower<LevyPower>();if(levy is not {Amount:>0})return;
        await ArmyPower.Consume(ctx,levy,1,play.Card);await CardPileCmd.Draw(ctx,DynamicVars["Cards"].IntValue,Owner);
    }
    protected override void OnUpgrade()=>DynamicVars["Cards"].UpgradeValueBy(1);
}
public sealed class PrivateeringPay():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"私掠军饷","造成{Damage:diff()}伤害。失去10金币并获得1锐士；若金币不足，改为获得1罪证，不获得锐士。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(9,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        await Hit(ctx,play,DynamicVars.Damage.IntValue);
        if(Owner.Gold>=10){await PlayerCmd.LoseGold(10,Owner);await ArmyPower.Gain(ctx,Owner,3,1,play.Card);}
        else await CrimeRecord.Add(Owner,1);
    }
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(4);
}
