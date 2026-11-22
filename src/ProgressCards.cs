namespace ArtuMod;

public sealed class CrocodilePool():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"鳄鱼池","移除1层罪证；若成功获得10格挡，否则抽2张。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        if(Owner.Creature.GetPower<CrimePower>() is {Amount:>0} crime)
        {await PowerCmd.ModifyAmount(ctx,crime,-1,Owner.Creature,play.Card);await CreatureCmd.GainBlock(Owner.Creature,10,ValueProp.Move,play);}
        else await CardPileCmd.Draw(ctx,2,Owner);
    }
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class HiddenDecree():RewardCard(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"藏起的圣旨","将手中1张苏丹牌放到抽牌堆底，抽{Cards}张。期限不重置。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Cards",1)];
    protected override bool IsPlayable=>Owner.PlayerCombatState?.Hand.Cards.Any(c=>c is SultanCard)==true;
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        var card=await Select(ctx,Owner.PlayerCombatState!.Hand.Cards.Where(c=>c is SultanCard));if(card==null)return;
        await CardPileCmd.Add(card,PileType.Draw,CardPilePosition.Bottom);await CardPileCmd.Draw(ctx,DynamicVars["Cards"].IntValue,Owner);
    }
    protected override void OnUpgrade()=>DynamicVars["Cards"].UpgradeValueBy(1);
}
public sealed class ShareKingsBurden():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"为君分忧","每成功支付一次苏丹牌代价，获得{Block}格挡。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Block",3)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<KingsBurdenPower>(ctx,DynamicVars["Block"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Block"].UpgradeValueBy(1);
}
public sealed class UnderKingsOrder():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"王命之下","造成{Damage:diff()}伤害。基础8；本回合每成功支付一次苏丹牌原代价，追加{Bonus}伤害。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new PaymentDamageVar(),new DynamicVar("Bonus",3)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>Hit(ctx,play,8+PaymentLedger.ThisTurn(Owner)*DynamicVars["Bonus"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Bonus"].UpgradeValueBy(1);
}
public sealed class MercenaryContract():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"雇佣契约","失去15金币，获得2士兵和{Elites}锐士。")
{
    protected override bool IsPlayable=>Owner.Gold>=15;
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Elites",1)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {if(Owner.Gold<15)return;await CardEconomy.Spend(Owner,15);await ArmyPower.Gain(ctx,Owner,1,2,play.Card);await ArmyPower.Gain(ctx,Owner,3,DynamicVars["Elites"].IntValue,play.Card);}
    protected override void OnUpgrade()=>DynamicVars["Elites"].UpgradeValueBy(1);
}
public sealed class BannerResonance():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"军旗共鸣","每回合首次获得某一种兵种时，额外获得{Troops}层该兵种；四种分别计算。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<BannerResonancePower>(ctx,DynamicVars["Troops"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(1);
}
public sealed class FieldReorganization():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"临阵整编","先获得2杂兵，再转换至多{Troops}杂兵为等量随机高级兵种；每转换一层获得3格挡。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",3)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        await ArmyPower.Gain(ctx,Owner,0,2,play.Card);
        int n=await V04.Convert(ctx,Owner,DynamicVars["Troops"].IntValue,null,play.Card);
        await CreatureCmd.GainBlock(Owner.Creature,n*3,ValueProp.Move,play);
    }
    protected override void OnUpgrade()=>DynamicVars["Troops"].UpgradeValueBy(2);
}
public sealed class WeakOverStrong():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"以弱胜强","[gold]读物[/gold]\n造成8伤害。首次使用时若目标当前生命高于自己，本次使用额外重放2次。")
{
    public override bool Reading=>true;
    public override int ModifyCardPlayCount(CardModel card,Creature? target,int count)
        =>card==this&&target?.CurrentHp>Owner.Creature.CurrentHp?count+2:count;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>Hit(ctx,play,8);
}
public sealed class JourneyWest():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"西行记","[gold]读物[/gold]\n从抽牌堆选1张非苏丹牌加入手牌，获得6格挡。消耗。")
{
    public override bool Reading=>true;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var card=await Select(ctx,Owner.PlayerCombatState!.DrawPile.Cards.Where(c=>c is not SultanCard));if(card!=null)await CardPileCmd.Add(card,PileType.Hand);await CreatureCmd.GainBlock(Owner.Creature,6,ValueProp.Move,play);}
}
public sealed class SecretAssembly():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"秘密集会","每回合首次使用思潮获得{Troops}士兵；首次使用读物抽{Cards}张。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Troops",1),new DynamicVar("Cards",1)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var power=await PowerCmd.Apply<SecretAssemblyPower>(ctx,Owner.Creature,DynamicVars["Troops"].IntValue,Owner.Creature,play.Card);if(power!=null)power.DrawCount+=DynamicVars["Cards"].IntValue;}
    protected override void OnUpgrade(){DynamicVars["Troops"].UpgradeValueBy(1);DynamicVars["Cards"].UpgradeValueBy(1);}
}
public sealed class FairyLantern():RewardCard(1,CardType.Power,CardRarity.Uncommon,TargetType.Self,"妖精的提灯","随机获得其一：3力量、5再生、抽3张。")
{
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {int n=Owner.RunState.Rng.CombatCardGeneration.NextInt(0,3);if(n==0)await AddPower<StrengthPower>(ctx,3);else if(n==1)await AddPower<RegenPower>(ctx,5);else await CardPileCmd.Draw(ctx,3,Owner);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class FollowFootsteps():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"亦步亦趋","复制本回合上一张实际使用的攻击牌的完整效果。复制苏丹牌不计原牌使用，也不重置原牌期限。")
{
    protected override bool IsPlayable=>EffectCopy.CanExecute(Owner,this);
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>EffectCopy.Execute(ctx,play);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Retain);
}
public sealed class KingsTreasury():RewardCard(3,CardType.Power,CardRarity.Uncommon,TargetType.Self,"王之宝库","战斗结束时，本战斗每使用过一种不同的苏丹牌获得{Gold}金币。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Gold",10)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {if(Util.State(Owner) is {} state)state.TreasuryRate+=10;return AddPower<KingsTreasuryPower>(ctx,10);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class IThink():RewardCard(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"俺寻思","弃置任意张其他手牌，抽等量牌。{Extra}")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Extra","")];
    protected override void OnUpgrade()=>((StringVar)DynamicVars["Extra"]).StringValue="额外抽1张牌。";
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var cards=(await CardSelectCmd.FromHandForDiscard(ctx,Owner,new CardSelectorPrefs(SelectionScreenPrompt,0,Owner.PlayerCombatState!.Hand.Cards.Count),c=>c!=this,this)).ToArray();await CardCmd.Discard(ctx,cards);await CardPileCmd.Draw(ctx,cards.Length+(IsUpgraded?1:0),Owner);}
}
public sealed class SoldierCharge():RewardCard(1,CardType.Attack,CardRarity.Uncommon,TargetType.AnyEnemy,"率兵冲锋","造成{Damage:diff()}伤害，获得{Block:diff()}格挡。若有士兵，消耗全部，每层额外造成{Bonus}伤害。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(4,ValueProp.Move),new BlockVar(2,ValueProp.Move),new DynamicVar("Bonus",6)];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        await Hit(ctx,play,DynamicVars.Damage.IntValue);await CreatureCmd.GainBlock(Owner.Creature,DynamicVars.Block,play);
        var army=Owner.Creature.GetPower<SoldierPower>();
        if(army is not {Amount:>0}||play.Target==null)return;
        int spent=army.Amount;
        await ArmyPower.Consume(ctx,army,spent,play.Card);await Hit(ctx,play,DynamicVars["Bonus"].IntValue*spent);
    }
    protected override void OnUpgrade(){DynamicVars.Damage.UpgradeValueBy(1);DynamicVars.Block.UpgradeValueBy(1);DynamicVars["Bonus"].UpgradeValueBy(3);}
}
public sealed class ArmyInterchange():RewardCard(2,CardType.Power,CardRarity.Uncommon,TargetType.Self,"互通有无","有士兵时，锐士结算额外获得每层1格挡；有锐士时，精兵每层额外造成1伤害。{Extra}")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Extra","")];
    protected override void OnUpgrade()=>((StringVar)DynamicVars["Extra"]).StringValue="有精兵时，士兵也计入精兵附加效果阈值。";
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var power=await PowerCmd.Apply<ArmyInterchangePower>(ctx,Owner.Creature,1,Owner.Creature,play.Card);if(power!=null&&IsUpgraded)power.CountSoldiersForThreshold=true;}
}
public sealed class ArmySpecialization():RewardCard(0,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"特化部队","本回合将所有高级兵种视为指定一种高级兵种，不改变真实层数。{Extra}")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Extra","")];
    protected override void OnUpgrade()=>((StringVar)DynamicVars["Extra"]).StringValue="另获得1层指定兵种。";
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var choice=await Util.Choose(Owner,[ModelDb.Card<ChooseSoldiers>(),ModelDb.Card<ChooseVeterans>(),ModelDb.Card<ChooseElites>()],false,ctx);int kind=choice is ChooseVeterans?2:choice is ChooseElites?3:1;var power=await PowerCmd.Apply<ArmySpecializationPower>(ctx,Owner.Creature,1,Owner.Creature,play.Card);if(power!=null)power.FormationKind=kind;if(IsUpgraded)await ArmyPower.Gain(ctx,Owner,kind,1,play.Card);}
}
public sealed class ChooseSoldiers():OptionCard("士兵","本回合将高级兵种视为士兵。");
public sealed class ChooseVeterans():OptionCard("精兵","本回合将高级兵种视为精兵。");
public sealed class ChooseElites():OptionCard("锐士","本回合将高级兵种视为锐士。");
public sealed class SupportFromAllSides():RewardCard(1,CardType.Skill,CardRarity.Uncommon,TargetType.Self,"八方支援","获得2杂兵以及士兵、精兵、锐士各1层。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play){for(int i=0;i<4;i++)await ArmyPower.Gain(ctx,Owner,i,i==0?2:1,play.Card);}
    protected override void OnUpgrade()=>RemoveKeyword(CardKeyword.Exhaust);
}

public sealed class CrimeOfDeceiving():RewardCard(1,CardType.Skill,CardRarity.Rare,TargetType.Self,"欺君之罪","选择手中1张苏丹牌；其本次使用的代价替换为获得1罪证。抽2张。")
{
    // v0.3's table defines replacement+draw. Its footer mentions undefined "holy decrees";
    // retain that unresolved addition in the update report instead of inventing cards/effects.
    protected override bool IsPlayable=>Owner.PlayerCombatState?.Hand.Cards.Any(c=>c is SultanCard)==true;
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {if(await Select(ctx,Owner.PlayerCombatState!.Hand.Cards.Where(c=>c is SultanCard)) is not SultanCard card)return;SultanPayments.ReplaceNextCost(card);await CardPileCmd.Draw(ctx,2,Owner);}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class TributeReform():RewardCard(0,CardType.Skill,CardRarity.Rare,TargetType.Self,"贡赋变法","失去{Gold}金币，获得2能量并抽2张。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Gold",20)];
    protected override bool IsPlayable=>Owner.Gold>=DynamicVars["Gold"].IntValue;
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {int gold=DynamicVars["Gold"].IntValue;if(Owner.Gold<gold)return;await CardEconomy.Spend(Owner,gold);await PlayerCmd.GainEnergy(2,Owner);await CardPileCmd.Draw(ctx,2,Owner);}
    protected override void OnUpgrade()=>DynamicVars["Gold"].UpgradeValueBy(-5);
}
public sealed class CavalryAdvance():RewardCard(2,CardType.Attack,CardRarity.Rare,TargetType.AllEnemies,"铁骑踏阵","对所有敌人造成{Damage}伤害；每有一种高级兵种，本次使用额外重放1次。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DamageVar(12,ValueProp.Move)];
    public override int ModifyCardPlayCount(CardModel card,Creature? target,int count)=>card==this?count+ArmyPower.Formations(Owner).Count(a=>a.kind>0):count;
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(play.Card,play).TargetingAllOpponents(Owner.Creature.CombatState!).Execute(ctx);
    protected override void OnUpgrade()=>DynamicVars.Damage.UpgradeValueBy(3);
}
public sealed class LivingOffWar():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"以战养战","每回合首次消耗兵种时，获得与该次消耗数量相等的一种随机高级兵种。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<LivingOffWarPower>(ctx,1);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class Library():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"藏书阁","每回合首次使用读物抽2张，并从因此抽到的牌中选1张本回合费用为0。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<LibraryPower>(ctx,1);
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class Notorious():RewardCard(2,CardType.Power,CardRarity.Rare,TargetType.Self,"恶名昭彰","每回合开始按罪证层数获得力量，最多{Strength}。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Strength",3)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<NotoriousPower>(ctx,DynamicVars["Strength"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Strength"].UpgradeValueBy(1);
}
public sealed class LastAdvice():RewardCard(2,CardType.Skill,CardRarity.Rare,TargetType.Self,"最后的谏言","从抽牌堆与弃牌堆合计选至多2张非苏丹牌加入手牌，本回合费用为0。消耗。")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {var candidates=Owner.PlayerCombatState!.DrawPile.Cards.Concat(Owner.PlayerCombatState.DiscardPile.Cards).Where(c=>c is not SultanCard).ToArray();if(candidates.Length==0)return;var cards=await CardSelectCmd.FromSimpleGrid(ctx,candidates,Owner,new CardSelectorPrefs(SelectionScreenPrompt,0,2));foreach(var card in cards){card.SetToFreeThisTurn();await CardPileCmd.Add(card,PileType.Hand);}}
    protected override void OnUpgrade()=>EnergyCost.UpgradeBy(-1);
}
public sealed class SuperBecky():RewardCard(3,CardType.Power,CardRarity.Rare,TargetType.Self,"超级贝姬夫人","抽4张牌，然后接管当前回合：按实时手牌奇数位置、再偶数位置两轮强制免费出牌，免除苏丹代价。完成后立即结束回合。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>BeckyController.DrawAndRun(ctx,Owner);
    protected override void OnUpgrade()=>AddKeyword(CardKeyword.Innate);
}
public static class CardEconomy
{
    public static async Task Spend(Player player,int amount)
    {await PlayerCmd.LoseGold(amount,player);}
}
