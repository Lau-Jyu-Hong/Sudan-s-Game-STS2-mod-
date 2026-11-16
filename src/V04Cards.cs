namespace ArtuMod;
// Stable model IDs; all new ordinary rewards inherit the existing Artu pool registration.
public sealed class KnowYourEnemy():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"知己知彼","造成{Damage:diff()}伤害，抽1张牌。若目标意图为攻击，再抽1张。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(12,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){bool attacking=play.Target?.Monster?.NextMove?.Intents.Any(i=>i is MegaCrit.Sts2.Core.MonsterMoves.Intents.AttackIntent)==true;await Hit(ctx,play,DynamicVars.Damage.IntValue);await CardPileCmd.Draw(ctx,attacking?2:1,Owner);}
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
public sealed class RobPeterToPayPaul():RewardCard(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"拆东补西","弃掉另一张手牌，获得2能量。{Extra}")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Extra")];
    protected override bool IsPlayable=>Owner.PlayerCombatState!.Hand.Cards.Any(c=>c!=this);
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var card=await Select(ctx,Owner.PlayerCombatState!.Hand.Cards.Where(c=>c!=play.Card));if(card==null)return;await CardCmd.Discard(ctx,card);await PlayerCmd.GainEnergy(2,Owner);if(IsUpgraded)await CardPileCmd.Draw(ctx,1,Owner);}
    protected override void OnUpgrade()=>((StringVar)DynamicVars["Extra"]).StringValue="抽1张牌。";
}
public sealed class CriminalDefense():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"罪人辩词","获得{Block:diff()}格挡。有罪证时抽2张牌。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(9,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);if(V04.Crime(Owner)>0)await CardPileCmd.Draw(ctx,2,Owner);}
    protected override void OnUpgrade()=>DynamicVars.Block.UpgradeValueBy(3);
}
public sealed class SecretMessenger():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"密使传旨","获得{Block:diff()}格挡。从弃牌堆选择1张苏丹牌加入手牌。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(6,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);var card=await Select(ctx,Owner.PlayerCombatState!.DiscardPile.Cards.OfType<SultanCard>());if(card!=null)await CardPileCmd.Add(card,PileType.Hand);}
    protected override void OnUpgrade()=>DynamicVars.Block.UpgradeValueBy(3);
}
public sealed class MarchingDrum():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"擂鼓进军","选择实际拥有的一种兵种，额外结算其回合结束效果1次，然后获得该兵种{Troops}层。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1)];
    protected override bool IsPlayable=>ArmyPower.All(Owner).Any(a=>a.Amount>0);
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var kinds=ArmyPower.All(Owner).Where(a=>a.Amount>0).Select(a=>a.Kind).ToArray();if(kinds.Length==0)return;int kind=await V04.ChooseArmy(ctx,Owner,kinds);var army=ArmyPower.All(Owner).FirstOrDefault(a=>a.Kind==kind);if(army==null)return;await army.Trigger(ctx);await ArmyPower.Gain(ctx,Owner,kind,DynamicVars["Troops"].IntValue,play.Card);}
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public sealed class CollectiveWisdom():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"集思广益","抽{Cards}张牌，弃1张牌。此前本场使用过思潮牌时，获得1能量。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Cards",2)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){bool thought=V04.Data(Owner).Thoughts.Count>0;await CardPileCmd.Draw(ctx,DynamicVars["Cards"].IntValue,Owner);var card=await Select(ctx,Owner.PlayerCombatState!.Hand.Cards.Where(c=>c!=play.Card));if(card!=null)await CardCmd.Discard(ctx,card);if(thought)await PlayerCmd.GainEnergy(1,Owner);}
    protected override void OnUpgrade()=>DynamicVars["Cards"].UpgradeValueBy(1);
}
public sealed class ScribesManuscript():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"抄写员的手稿","[gold]读物[/gold]\n查看抽牌堆顶5张牌，选择至多2张加入手牌，其余放入弃牌堆。消耗。")
{
    public override bool Reading=>true;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var top=Owner.PlayerCombatState!.DrawPile.Cards.Take(5).ToArray();if(top.Length==0)return;var chosen=(await CardSelectCmd.FromSimpleGrid(ctx,top,Owner,new CardSelectorPrefs(SelectionScreenPrompt,0,Math.Min(2,top.Length)))).ToHashSet();foreach(var card in top)await CardPileCmd.Add(card,chosen.Contains(card)?PileType.Hand:PileType.Discard);}
}
public sealed class ArmyRegularization():RewardCard(1,CardType.Skill,CardRarity.Rare,TargetType.Self,"军队正规化","先获得2杂兵，再将全部杂兵真实转化为选择的一种高级兵种，每转化1层获得1格挡。{ExhaustText}")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("ExhaustText","消耗。")];
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await ArmyPower.Gain(ctx,Owner,0,2,play.Card);if(Owner.Creature.GetPowerAmount<LevyPower>()==0)return;int kind=await V04.ChooseArmy(ctx,Owner,[1,2,3]);int n=await V04.Convert(ctx,Owner,int.MaxValue,kind,play.Card);await CreatureCmd.GainBlock(Owner.Creature,n,ValueProp.Move,play);}
    protected override void OnUpgrade(){RemoveKeyword(CardKeyword.Exhaust);((StringVar)DynamicVars["ExhaustText"]).StringValue="";}
}
public sealed class RevolutionaryAcademy():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"革命军校","获得3杂兵，选择一种高级兵种。每回合开始先获得2杂兵，再将至多{Troops}层杂兵真实转化为该兵种。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",4)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await ArmyPower.Gain(ctx,Owner,0,3,play.Card);int kind=await V04.ChooseArmy(ctx,Owner,[1,2,3]);var power=await PowerCmd.Apply<AcademyPower>(ctx,Owner.Creature,DynamicVars["Troops"].IntValue,Owner.Creature,play.Card);if(power!=null)power.Kind=kind;}
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(2);
}
public sealed class AllByKingsGrace():RewardCard(1,CardType.Skill,CardRarity.Rare,TargetType.Self,"全凭君恩","本场苏丹牌0费、免代价、额外重放1次；履行状态与期限冻结，不因使用重置。{Penalty}消耗。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Penalty","战后每个曾免费使用的苏丹牌实例，履行倒计时额外−1。")];
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var d=V04.Data(Owner);d.Grace=true;d.GracePenalty|=!IsUpgraded;await AddPower<KingsGracePower>(ctx,1);}
    protected override void OnUpgrade()=>((StringVar)DynamicVars["Penalty"]).StringValue="";
}
public sealed class SpiralWaltz():RewardCard(1,CardType.Skill,CardRarity.Rare,TargetType.Self,"螺旋圆舞曲","[gold]读物[/gold]\n下一张能力牌的效果额外结算一次。")
{public override bool Reading=>true;protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<SpiralWaltzPower>(ctx,1);}
public sealed class OpenThought():RewardCard(0,CardType.Skill,CardRarity.Rare,TargetType.Self,"洞开的思想","[gold]读物[/gold]\n本场不能从卡牌直接效果获得格挡。获得8力量。")
{public override bool Reading=>true;protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await AddPower<OpenThoughtPower>(ctx,1);await AddPower<StrengthPower>(ctx,8);}}
public sealed class LifeScepter():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"生命权杖","每回合一次，需要支付苏丹代价时，可以替换为使选择的一名敌人获得{Regen}再生。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Regen",7)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<LifeScepterPower>(ctx,DynamicVars["Regen"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Regen"].UpgradeValueBy(-2);
}
public sealed class ProfitFromCrime():RewardCard(1,CardType.Power,CardRarity.Rare,TargetType.Self,"以罪谋利","每回合首次获得罪证时，抽2张牌，获得1能量。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<CrimeProfitPower>(ctx,1);protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);}
public sealed class BurnTheBoats():RewardCard(2,CardType.Attack,CardRarity.Rare,TargetType.RandomEnemy,"破釜沉舟","消耗全部兵种。每层对随机敌人造成{Damage:diff()}伤害，获得{Block:diff()}格挡。\n当前可消耗{Troops}层。\n预计总伤害{TotalDamage}，总格挡{TotalBlock}。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(6,ValueProp.Move),new BlockVar(2,ValueProp.Move)];
    protected override void AddExtraArgsToDescription(LocString loc)
    {
        int n=IsMutable&&RunState!=null?ArmyPower.All(Owner).Sum(a=>Math.Max(0,a.Amount)):0;
        loc.Add("Troops",n);loc.Add("TotalDamage",DamageEstimate(n));
        loc.Add("TotalBlock",Math.Max(0,(int)DynamicVars.Block.PreviewValue)*n);
    }
    public string DamageEstimate(int n)
    {
        if(!IsMutable||RunState==null||n==0)return "0";
        var enemies=Util.Enemies(Owner).ToArray();if(enemies.Length==0)return "0";
        int PerHit(Creature e,decimal damage)=>Math.Max(0,(int)MegaCrit.Sts2.Core.Hooks.Hook.ModifyDamage(Owner.RunState,CombatState,e,Owner.Creature,damage,ValueProp.Move,this,null,MegaCrit.Sts2.Core.Hooks.ModifyDamageHookType.All,CardPreviewMode.Normal,out _));
        int vigor=Owner.Creature.GetPowerAmount<VigorPower>();
        var first=enemies.Select(e=>PerHit(e,DynamicVars.Damage.BaseValue)).ToArray();
        var later=enemies.Select(e=>PerHit(e,DynamicVars.Damage.BaseValue-vigor)).ToArray();
        int min=first.Min()+later.Min()*(n-1),max=first.Max()+later.Max()*(n-1);
        return min==max?min.ToString():$"{min}～{max}";
    }
    protected override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips=>base.ExtraHoverTips.Concat([
        new MegaCrit.Sts2.Core.HoverTips.HoverTip(new LocString("powers","ARTU_TERM_兵种总计"),"总计为跨敌人的全部独立命中，不是单体或群攻。不同敌人的易伤/减伤不同则显示区间；随机选择、击杀溢出、活力消耗及后续触发可能改变实际总量。格挡和伤害不合并结算。")]);
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var costs=ArmyPower.All(Owner).Select(a=>(a,a.Amount)).ToArray();int n=costs.Sum(c=>c.Amount);await ArmyPower.ConsumeMany(ctx,Owner,costs,play.Card);for(int i=0;i<n&&!Owner.Creature.IsDead;i++){var enemy=Util.RandomEnemy(Owner);if(enemy!=null)await DamageCmd.Attack(DynamicVars.Damage.IntValue).FromCard(play.Card,play).Targeting(enemy).Execute(ctx);await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);}}
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(2);
}
public sealed class UndergroundPress():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"地下印坊","每回合首次使用读物时，从抽牌堆选择1张非苏丹牌加入手牌。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<UndergroundPressPower>(ctx,1);protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);}
public sealed class MoneyOpensDoors():RewardCard(0,CardType.Skill,CardRarity.Rare,TargetType.Self,"金钱开路","失去{Gold}金币。从抽牌堆或弃牌堆选1张非苏丹牌入手，本场费用变为0并获得保留。消耗。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Gold",20)];
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override bool IsPlayable=>Owner.Gold>=DynamicVars["Gold"].IntValue;
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){int cost=DynamicVars["Gold"].IntValue;if(Owner.Gold<cost)return;await PlayerCmd.LoseGold(cost,Owner);var card=await V04.Fetch(ctx,Owner,c=>c is not SultanCard);if(card==null)return;card.EnergyCost.SetThisCombat(0);card.AddKeyword(CardKeyword.Retain);}
    protected override void OnUpgrade()=>DynamicVars["Gold"].UpgradeValueBy(-10);
}
public sealed class RoyalConscription():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"王命征召","每回合首次使用苏丹牌时，选择一种高级兵种，获得{Troops}层。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",3)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<RoyalConscriptionPower>(ctx,DynamicVars["Troops"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public abstract class ThirdThought(string title,string text):RewardCard(1,CardType.Power,CardRarity.Rare,TargetType.Self,title,"[gold]思潮[/gold]\n"+text){public override bool Thought=>true;}
public sealed class AtonementPrayer():ThirdThought("赎罪祈愿","永久移除1罪证。每回合首抽非苏丹牌本回合0费。使用后胜利时无罪证且未新增苏丹之怒，虔信+1；累计3取得进化资格，奖励可选择保留或进化为完美世界。")
{protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await V04.RemoveCrime(ctx,Owner,1);await AddPower<AtonementPower>(ctx,1);}}
public sealed class AncientFaith():ThirdThought("古老的信仰","每回合第一次主动使用苏丹牌后额外失去2HP，并免代价重放1次。使用后本场使用至少两类苏丹牌获胜，狂信+1；累计3取得进化资格，奖励可选择保留或进化为新世界。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<AncientFaithPower>(ctx,1);}
public sealed class RebellionIntent():ThirdThought("谋反的意图","每回合首次使用其他非苏丹牌时获得1锐士；此前本回合未用苏丹牌时，额外结算所有兵种1次。使用后本场触发至少2次额外结算而获胜，谋逆+1；累计3取得进化资格，奖励可选择保留或进化为弑君的计划。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<RebellionIntentPower>(ctx,1);}
public abstract class FinalThought(int cost,CardType type,string title,string text):RewardCard(cost,type,CardRarity.Ancient,TargetType.Self,title,"[gold]思潮[/gold]\n"+text)
{public override bool Thought=>true;public override bool CanBeGeneratedInCombat=>false;public override bool CanBeGeneratedByModifiers=>false;}
public sealed class PerfectWorld():FinalThought(1,CardType.Power,"完美世界","每回合按第一张主动使用牌决定路线：若为苏丹牌，本回合苏丹牌免代价；若为非苏丹牌，本回合非苏丹牌0费、使用后消耗。两者互斥；不追溯此前费用或效果，自动使用与重放不改变路线。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<PerfectWorldPower>(ctx,1);}
public sealed class NewWorld():FinalThought(1,CardType.Power,"新世界","每回合首次主动用苏丹牌后失去3HP，免代价重放2次，然后抽2张牌并获得1能量。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<NewWorldPower>(ctx,1);}
public sealed class RegicidePlan():FinalThought(1,CardType.Power,"弑君的计划","每回合结束额外结算所有兵种2次。本场未使用苏丹牌而获胜后，可从永久牌组移除1张苏丹牌。")
{protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<RegicidePower>(ctx,1);}
public sealed class RevolutionConcept():FinalThought(1,CardType.Power,"革命的构思","[gold]读物[/gold]\n从抽牌堆或弃牌堆选1张非苏丹牌入手。每回合首次使用其他读物获得1能量；首次使用其他思潮获得士兵、精兵、锐士各1层。")
{public override bool Reading=>true;protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await V04.Fetch(ctx,Owner,c=>c is not SultanCard);await AddPower<RevolutionConceptPower>(ctx,1);}}
public sealed class RevolutionPlan():RewardCard(0,CardType.Skill,CardRarity.Ancient,TargetType.Self,"革命的计划","革命成功！立即赢得本局游戏。保留。")
{
    public override int MaxUpgradeLevel=>0;public override bool CanBeGeneratedInCombat=>false;public override bool CanBeGeneratedByModifiers=>false;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Retain];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>RevolutionEnding.Win(Owner);
}
public sealed class NoteInBook():RewardCard(0,CardType.Power,CardRarity.Ancient,TargetType.Self,"夹在书中的字条","[gold]读物　思潮[/gold]\n启用已完成的革命议程强化。悬浮查看条件、进度与奖励；点击角色下方“议程”可查看完整清单。")
{
    protected override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips=>base.ExtraHoverTips.Concat(AgendaDetails.Tips(ArtuTerms.OwnerOf(this)));
    public override bool Reading=>true;public override bool Thought=>true;public override bool CanBeGeneratedInCombat=>false;public override bool CanBeGeneratedByModifiers=>false;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Retain];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){V04.Data(Owner).NoteActive=true;await AddPower<RevolutionNotePower>(ctx,1);await RevolutionAgenda.Refresh(Owner,ctx);}
}
public sealed class ScepterReplaceOption():OptionCard("替换代价","使选择的敌人获得再生，替换本次苏丹代价。");
public sealed class ScepterDeclineOption():OptionCard("正常支付","不使用权杖，本回合仍可在之后使用一次替换。");
