namespace ArtuMod;
public sealed class TemporaryRecruitment():RewardCard(0,CardType.Skill,CardRarity.Common,TargetType.Self,"临时征募",
    "获得{Block:diff()}格挡与{Troops}杂兵。本回合实际将杂兵转化为高级兵种时，补充等量杂兵；本牌累计最多补充{Limit}层。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(4,ValueProp.Move),new DynamicVar("Troops",1),new DynamicVar("Limit",3)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);await ArmyPower.Gain(ctx,Owner,0,DynamicVars["Troops"].IntValue,play.Card);await AddPower<TemporaryRecruitmentPower>(ctx,DynamicVars["Limit"].IntValue);}
    protected override void OnUpgrade(){DynamicVars.Block.UpgradeValueBy(2);DynamicVars["Troops"].UpgradeValueBy(1);DynamicVars["Limit"].UpgradeValueBy(2);}
}
public sealed class RecruitmentNotice():RewardCard(2,CardType.Power,CardRarity.Uncommon,TargetType.Self,"征兵告示","每回合开始获得2杂兵。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<RecruitmentNoticePower>(ctx,2);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class TemporaryRecruitmentPower():ArtuPower("临时征募","本回合真实转化杂兵后补充等量杂兵，本牌剩余额度{Amount}层；补充不计转化。")
{
    public override PowerInstanceType InstanceType=>PowerInstanceType.Instanced;
    public async Task OnConverted(PlayerChoiceContext ctx,int converted,CardModel? source)
    {int n=Math.Min(converted,Amount);if(n<=0)return;await PowerCmd.ModifyAmount(ctx,this,-n,Owner,null);await ArmyPower.Gain(ctx,Owner.Player!,0,n,source);}
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class RecruitmentNoticePower():ArtuPower("征兵告示","每回合开始获得{Amount}杂兵。")
{
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)=>player==Owner.Player?ArmyPower.Gain(ctx,player,0,Amount):Task.CompletedTask;
}
