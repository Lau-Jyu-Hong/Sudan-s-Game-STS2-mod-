namespace ArtuMod;
public abstract class RewardCard(int cost,CardType type,CardRarity rarity,TargetType target,string title,string text):ArtuCard(cost,type,rarity,target)
{
    protected string TitleText {get;}=title;
    protected string DescriptionText {get;}=text;
    public override List<(string,string)> Localization=>new CardLoc(TitleText,DescriptionText);
    protected Task Hit(PlayerChoiceContext ctx,CardPlay play,int damage)=>play.Target==null?Task.CompletedTask:DamageCmd.Attack(damage).FromCard(play.Card,play).Targeting(play.Target).Execute(ctx);
    protected override void OnUpgrade(){}
}
public sealed class DispatchTroops():RewardCard(1,CardType.Skill,CardRarity.Basic,TargetType.AnyEnemy,"调兵遣将","获得{Block:diff()}格挡，造成{Damage:diff()}伤害。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(5,ValueProp.Move),new DamageVar(6,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);await Hit(ctx,play,DynamicVars.Damage.IntValue);}
    protected override void OnUpgrade(){DynamicVars.Block.UpgradeValueBy(3);DynamicVars.Damage.UpgradeValueBy(3);}
}
public sealed class RoyalPass():RewardCard(0,CardType.Skill,CardRarity.Basic,TargetType.Self,"御用通行","[gold]读物[/gold]\n本回合下一张苏丹牌不用支付代价。消耗。")
{
    public override bool Reading=>true;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<PassPower>(ctx,1);
}
public sealed class WeighOptions():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"权衡利弊","抽4张牌，选择最多1张因此抽到的苏丹牌，弃置其他因此抽到的牌。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var drawn=(await CardPileCmd.Draw(ctx,4,Owner)).ToArray();var kept=await Select(ctx,drawn.Where(c=>c is SultanCard),true);await CardCmd.Discard(ctx,drawn.Where(c=>c!=kept&&c.Pile?.Type==PileType.Hand));}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class IndulgentLife():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"犬马声色","本回合下{Times}次使用苏丹牌时，重放1次；重放仍支付代价。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Times",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<ReplaySultanPower>(ctx,DynamicVars["Times"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Times"].UpgradeValueBy(1);
}
public sealed class HollowAuthority():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"色厉内茬","苏丹牌不消耗能量，但代价处理两次。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<DoubleCostPower>(ctx,1);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}
public sealed class RecruitTroops():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"招兵买马","获得{Troops}层杂兵。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>ArmyPower.Gain(ctx,Owner,0,DynamicVars["Troops"].IntValue,this);
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public sealed class CriminalRecruits():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"罪犯的投奔","获得{Troops}层杂兵和1层罪证。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",5)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await ArmyPower.Gain(ctx,Owner,0,DynamicVars["Troops"].IntValue,this);await AddPower<CrimePower>(ctx,1);}
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(2);
}
public sealed class CoinSlingshot():RewardCard(1,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy,"金币弹弓","失去5金币，造成{Damage}伤害。")
{
    protected override bool IsPlayable=>Owner.Gold>=5;
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(14,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Owner.Gold<5)return;await PlayerCmd.LoseGold(5,Owner);if(Util.State(Owner) is {} s)s.GoldLost+=5;await Hit(ctx,play,DynamicVars.Damage.IntValue);}
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(4);
}
public sealed class Chivalry():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"行侠仗义","造成{Damage}伤害。每造成6点实际生命损失，随机获得1层高级兵种。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(6,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Target==null)return;int before=play.Target.CurrentHp;await Hit(ctx,play,DynamicVars.Damage.IntValue);for(int i=0;i<Rules.AdvancedTroopsFromDamage(before,play.Target.CurrentHp);i++)await ArmyPower.Gain(ctx,Owner,Owner.RunState.Rng.CombatCardGeneration.NextInt(1,4),1,this);}
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
public sealed class Ironhead():RewardCard(2,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy,"上吧铁头","造成{Damage}伤害。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(15,ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>Hit(ctx,play,15);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Retain);
}
public sealed class Quickfoot():RewardCard(0,CardType.Attack,CardRarity.Common,TargetType.AnyEnemy,"快脚小圆","造成{Damage}伤害，获得{Block}格挡。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(2,ValueProp.Move),new BlockVar(2,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await Hit(ctx,play,DynamicVars.Damage.IntValue);await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);}
    protected override void OnUpgrade(){DynamicVars.Damage.UpgradeValueBy(1);DynamicVars.Block.UpgradeValueBy(1);}
}
public sealed class LoyalHound():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"忠犬护主","获得1层虚弱，获得{Block}格挡。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new BlockVar(12,ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await AddPower<WeakPower>(ctx,1);await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);}
    protected override void OnUpgrade()=>DynamicVars.Block.UpgradeValueBy(4);
}
public sealed class WifesTea():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"妻子的茶会","本回合每使用一张牌获得{Block}格挡。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Block",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<TeaPower>(ctx,DynamicVars["Block"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Block"].UpgradeValueBy(1);
}
public sealed class StateAffairs():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"处理政要","选择：抽2张；或抽4张、弃3张。升级后可再支付1能量选择两项。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        var choice=await Util.Choose(Owner,[ModelDb.Card<DrawTwoOption>(),ModelDb.Card<DrawDiscardOption>()],false,ctx);
        if(choice is DrawTwoOption)await CardPileCmd.Draw(ctx,2,Owner);else await FilterHand(ctx);
        if(IsUpgraded&&Owner.PlayerCombatState!.Energy>=1)
        {
            var both=await Util.Choose(Owner,[ModelDb.Card<ExtraAffairsOption>(),ModelDb.Card<StopOption>()],false,ctx);
            if(both is ExtraAffairsOption){await PlayerCmd.LoseEnergy(1,Owner);if(choice is DrawTwoOption)await FilterHand(ctx);else await CardPileCmd.Draw(ctx,2,Owner);}
        }
    }
    private async Task FilterHand(PlayerChoiceContext ctx)
    {await CardPileCmd.Draw(ctx,4,Owner);var selected=await CardSelectCmd.FromHandForDiscard(ctx,Owner,new CardSelectorPrefs(SelectionScreenPrompt,Math.Min(3,Owner.PlayerCombatState!.Hand.Cards.Count)),null,this);await CardCmd.Discard(ctx,selected);}
}
public abstract class OptionCard(string title,string text):RewardCard(0,CardType.Skill,CardRarity.Event,TargetType.Self,title,text)
{public override int MaxUpgradeLevel=>0;}
public sealed class DrawTwoOption():OptionCard("处理公文","抽2张牌。");
public sealed class DrawDiscardOption():OptionCard("筛选情报","抽4张牌，弃3张牌。");
public sealed class ExtraAffairsOption():OptionCard("一并处理","再支付1能量，选择另一项。");
public sealed class StopOption():OptionCard("到此为止","不再支付额外代价。");
public sealed class RemoveOption():OptionCard("抛弃皇命","永久移除所选苏丹牌。");
public sealed class PromoteOption():OptionCard("接受更大的皇命","所选苏丹牌换为同类型的高一级苏丹牌。");
public sealed class UpgradeOrdinaryOption():OptionCard("研习","升级最多3张普通牌。");
public sealed class PromoteSultanOption():OptionCard("窥探皇命","最多3张苏丹牌换为同类型高一级苏丹牌。");
public sealed class AllArmyAttack():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"全军出击","你的杂兵伤害对所有敌人生效。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await AddPower<AllArmyPower>(ctx,1);if(IsUpgraded)await ArmyPower.Gain(ctx,Owner,0,2,this);}
}
public sealed class ImperialExpedition():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"奉诏出征","消耗1张手牌中的征服苏丹牌，按品级获得1/2/3/4层士兵（升级：各+1）。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {if(await Select(ctx,Owner.PlayerCombatState!.Hand.Cards.Where(c=>c is SultanCard s&&s.Suit==SultanSuit.Conquest)) is not SultanCard chosen)return;await CardCmd.Exhaust(ctx,chosen);await ArmyPower.Gain(ctx,Owner,1,(int)chosen.Grade+1+(IsUpgraded?1:0),this);}
}
public sealed class BackroomDeal():RewardCard(3,CardType.Attack,CardRarity.Common,TargetType.Self,"暗箱操作","获得1层罪证和3层士兵，立即触发一次全部士兵的回合结束效果。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){await AddPower<CrimePower>(ctx,1);await ArmyPower.Gain(ctx,Owner,1,3,this);await Owner.Creature.GetPower<SoldierPower>()!.Trigger(ctx);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class TrainArmy():RewardCard(2,CardType.Power,CardRarity.Uncommon,TargetType.Self,"训练兵队","回合开始时，将最多{Troops}层杂兵转为等量随机高级兵种。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<TrainingPower>(ctx,DynamicVars["Troops"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public sealed class EliteTroops():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"精锐部队","获得{Troops}层锐士。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>ArmyPower.Gain(ctx,Owner,3,DynamicVars["Troops"].IntValue,this);
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public sealed class Drill():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"操练","消耗每种兵种各1层；每消耗1层获得1能量并抽1张牌。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var armies=ArmyPower.All(Owner).Where(p=>p.Amount>0).ToArray();await ArmyPower.ConsumeMany(ctx,Owner,armies.Select(a=>(a,1)),play.Card);await PlayerCmd.GainEnergy(armies.Length,Owner);await CardPileCmd.Draw(ctx,armies.Length,Owner);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class WarSong():RewardCard(1,CardType.Power,CardRarity.Rare,TargetType.Self,"强军战歌","每回合开始时获得1层精兵。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<WarSongPower>(ctx,1);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}
public sealed class Muster():RewardCard(2,CardType.Attack,CardRarity.Common,TargetType.Self,"整兵待战","触发所有至少4层兵种的回合结束效果。升级：其他已有兵种各+1层。立即结束回合。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var armies=ArmyPower.Formations(Owner).ToArray();foreach(var army in armies.Where(a=>a.amount>=4))await army.source.Trigger(ctx);
    if(IsUpgraded)foreach(var army in armies.Where(a=>a.amount is >0 and <4))await ArmyPower.Gain(ctx,Owner,army.kind,1,this);
    PlayerCmd.EndTurn(Owner,false);}
}
public sealed class ObeyOrders():RewardCard(2,CardType.Power,CardRarity.Common,TargetType.Self,"顺应皇命","每回合开始抽1张牌；若抽到苏丹牌，自动使用并正常处理代价。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<ObeyPower>(ctx,1);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class ReturnOnSpending():RewardCard(1,CardType.Power,CardRarity.Common,TargetType.Self,"有去有回","本场失去超过20金币，战斗结束后获得{Gold}金币。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new GoldVar(20)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} state)state.Refund+=DynamicVars.Gold.IntValue;return Task.CompletedTask;}
    protected override void OnUpgrade()=>DynamicVars.Gold.UpgradeValueBy(10);
}
public sealed class MoonDogBook():RewardCard(1,CardType.Skill,CardRarity.Common,TargetType.Self,"小狗与月亮之书","[gold]读物[/gold]\n获得3多重护甲。")
{
    public override bool Reading=>true;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<MultiArmorPower>(ctx,3);
}
public sealed class WhisperBook():RewardCard(2,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"耳语之书","[gold]读物[/gold]\n下回合无法造成或受到伤害。下回合结束时失去2力量。")
{
    public override bool Reading=>true;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<WhisperPower>(ctx,1);
}
public sealed class PastGlory():RewardCard(3,CardType.Power,CardRarity.Rare,TargetType.Self,"追忆过去的荣光","[gold]读物[/gold]\n生成杂兵时，改为等量随机高级兵种。")
{
    public override bool Reading=>true;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<GloryPower>(ctx,1);
}
public sealed class FlowingThoughts():RewardCard(1,CardType.Power,CardRarity.Common,TargetType.Self,"思潮翻涌","从抽牌堆选择一张[gold]思潮[/gold]牌加入手牌。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){var selected=await Select(ctx,Owner.PlayerCombatState!.DrawPile.Cards.Where(c=>c is ArtuCard a&&a.Thought));if(selected!=null)await CardPileCmd.Add(selected,PileType.Hand);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
[Pool(typeof(MegaCrit.Sts2.Core.Models.CardPools.EventCardPool))]
public sealed class Tribunal():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"审判庭","每回合开始时，一名敌人从历史次数最少的刑罚中随机选择：失去2力量；获得1脆弱、1易伤；此后每回合开始受到20伤害。")
{
    public override bool CanBeGeneratedInCombat=>false;public override bool CanBeGeneratedByModifiers=>false;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<TribunalPower>(ctx,1);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}
