namespace ArtuMod;
public abstract class SultanCard(SultanSuit suit,SultanGrade grade) : ArtuCard(1,CardType.Curse,CardRarity.Event,
    suit==SultanSuit.Luxury||suit==SultanSuit.Conquest&&grade==SultanGrade.Bronze||suit==SultanSuit.Lust&&grade==SultanGrade.Gold?TargetType.Self:TargetType.AnyEnemy)
{
    public SultanSuit Suit {get;}=suit;
    public SultanGrade Grade {get;}=grade;
    [SavedProperty] public int BattlesRemaining {get;set;}=Rules.BattleDeadline(grade);
    [SavedProperty] public int TurnsSinceUse {get;set;}
    [SavedProperty] public bool UsedThisBattle {get;set;}
    [SavedProperty] public bool UsedThisTurn {get;set;}
    [SavedProperty] public bool FailedThisBattle {get;set;}
    [SavedProperty] public int LastCountedTurn {get;set;}
    public SultanCard Original => DeckVersion as SultanCard??this;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat=>false;
    public override bool CanBeGeneratedByModifiers=>false;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Eternal];
    protected override IEnumerable<DynamicVar> CanonicalVars=>Suit==SultanSuit.Slaughter
        ?[new DamageVar(Rules.SlaughterDamage(Grade),Grade>=SultanGrade.Silver?ValueProp.Move|ValueProp.Unblockable:ValueProp.Move)]
        :[];
    protected override bool IsPlayable => (V04Replay.Free(this)||Owner.Creature.HasPower<PassPower>())?CanResolveWaived():Owner.Creature.GetPower<LifeScepterPower>()?.Available==true||Util.Tyranny(Owner)||SultanPayments.HasReplacement(this)||CanPayAny();
    protected override void AddExtraArgsToDescription(LocString loc)
    {
        loc.Add("Battles",Original.BattlesRemaining);
        loc.Add("Turns",Math.Max(0,Rules.GoldTurnDeadline-Original.TurnsSinceUse));
        loc.Add("Frozen",IsMutable&&RunState!=null&&V04.Data(Owner).Grace?"（冻结中）":"");
        loc.Add("Fulfilled",IsMutable&&RunState!=null&&Original.UsedThisBattle?"（本场已履行）":"");
    }
    protected override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips=>base.ExtraHoverTips.Concat([
        new MegaCrit.Sts2.Core.HoverTips.HoverTip(new LocString("powers","ARTU_TERM_期限"),DeadlineText()+"\n逾期："+ExpiryText()+"\n另外获得1层苏丹之怒。每张原牌独立计时。"){Id="ArtuDeadline:"+Id}]);
    public string ExpiryText()=>(Suit,Grade) switch{
        (SultanSuit.Slaughter,SultanGrade.Stone)=>"失去10生命。",
        (SultanSuit.Slaughter,SultanGrade.Bronze)=>"失去20生命。",
        (SultanSuit.Slaughter,SultanGrade.Silver)=>"失去30生命。",
        (SultanSuit.Slaughter,SultanGrade.Gold)=>"永久将最大生命减半，余下本局无法提高最大生命。",
        (SultanSuit.Conquest,SultanGrade.Stone)=>"下一场战斗开场获得5虚弱，仅该场施加一次。",
        (SultanSuit.Conquest,SultanGrade.Bronze)=>"此后每场战斗开场少1力量（多次逾期累计）。",
        (SultanSuit.Conquest,SultanGrade.Silver)=>"未来3场战斗不能获得敏捷。",
        (SultanSuit.Conquest,SultanGrade.Gold)=>"整局不能获得力量或敏捷；此后每场战斗所有敌人开场获得1无实体。",
        (SultanSuit.Luxury,SultanGrade.Stone)=>"下一次商店购买商品价格翻倍，直至完成一次购买。",
        (SultanSuit.Luxury,SultanGrade.Bronze)=>"下一家商店全部商品价格翻倍。",
        (SultanSuit.Luxury,SultanGrade.Silver)=>"下一家商店不能购买商品。",
        (SultanSuit.Luxury,SultanGrade.Gold)=>"整局不能获得金币，已有金币仍可使用。",
        (SultanSuit.Lust,SultanGrade.Stone)=>"下一场战斗开场获得5易伤，仅该场施加一次。",
        (SultanSuit.Lust,SultanGrade.Bronze)=>"未来3场战斗所有敌人开场获得5滚石。",
        (SultanSuit.Lust,SultanGrade.Silver)=>"未来3场战斗中，自身治疗时所有敌人回复同等生命。",
        _=>"整局不能治疗；战斗中本应治疗自己的效果改为治疗所有敌人同等生命。"
    };
    public string DeadlineText()
    {
        string text=$"总期限：{Rules.BattleDeadline(Grade)}场战斗；当前剩余{Original.BattlesRemaining}场。";
        if(Grade==SultanGrade.Gold)text+=$"\n另有跨战斗{Rules.GoldTurnDeadline}个自己的回合期限，当前剩余{Math.Max(0,Rules.GoldTurnDeadline-Original.TurnsSinceUse)}回合。";
        if(IsMutable&&RunState!=null&&CombatManager.Instance.IsInProgress)text+=Original.UsedThisBattle?"\n本场已履行，胜利时不会推进本牌场数倒计时。":"\n本场尚未履行。";
        if(IsMutable&&RunState!=null&&V04.Data(Owner).Grace)text+="\n本场履行状态与期限冻结。普通全凭君恩的战后额外扣减仍按该牌规则执行。";
        return text;
    }
    public bool CanPayAny()=>Suit switch {
        SultanSuit.Slaughter=>Owner.Creature.CurrentHp>=Rules.SlaughterHpCost(Grade),
        SultanSuit.Conquest when Grade==SultanGrade.Stone=>Owner.Creature.Block>0,
        SultanSuit.Conquest when Grade==SultanGrade.Gold=>Util.Enemies(Owner).Any(CanReceiveGoldBlock),
        SultanSuit.Luxury when Grade==SultanGrade.Bronze=>BronzeCandidates().Any(),
        SultanSuit.Luxury=>Owner.Gold>=Rules.LuxuryGoldCost(Grade),
        _=>Util.Enemies(Owner).Any()
    };
    // Only resources used by the positive effect must exist when its cost is waived.
    // Pure costs (HP, gold, giving enemy buffs) are not silently deducted or treated as paid.
    IEnumerable<CardModel> BronzeCandidates()=>PileType.Hand.GetPile(Owner).Cards.Where(c=>c is SultanCard&&c!=this&&c is not BronzeLuxury);
    public bool CanResolveWaived()=>Suit==SultanSuit.Conquest&&Grade==SultanGrade.Stone?Owner.Creature.Block>0:Suit==SultanSuit.Luxury&&Grade==SultanGrade.Bronze?BronzeCandidates().Any():TargetType!=TargetType.AnyEnemy||Util.Enemies(Owner).Any();
    bool CanReceiveGoldBlock(Creature target)=>MegaCrit.Sts2.Core.Hooks.Hook.ModifyBlock(target.CombatState!,target,50,ValueProp.Unpowered,this,null,out _)>=50;
    public bool CanPayTarget(Creature? target)=>CanPayAny()&&
        !(Grade==SultanGrade.Gold&&Suit==SultanSuit.Conquest&&(target==null||!CanReceiveGoldBlock(target)));
    public static SultanCard Get(SultanSuit suit,SultanGrade grade)
    {
        Type[,] types={{typeof(StoneSlaughter),typeof(BronzeSlaughter),typeof(SilverSlaughter),typeof(GoldSlaughter)},
                      {typeof(StoneConquest),typeof(BronzeConquest),typeof(SilverConquest),typeof(GoldConquest)},
                      {typeof(StoneLuxury),typeof(BronzeLuxury),typeof(SilverLuxury),typeof(GoldLuxury)},
                      {typeof(StoneLust),typeof(BronzeLust),typeof(SilverLust),typeof(GoldLust)}};
        return (SultanCard)ModelDb.GetById<CardModel>(ModelDb.GetId(types[(int)suit,(int)grade]));
    }
    public override List<(string,string)> Localization=>new CardLoc(
        new[]{"岩石","青铜","白银","黄金"}[(int)Grade]+"苏丹牌－"+new[]{"杀戮","征服","奢靡","纵欲"}[(int)Suit],
        "[gold]苏丹牌[/gold]\n"+Text()+$"\n总期限{Rules.BattleDeadline(Grade)}场；剩余{{Battles}}场{{Frozen}}{{Fulfilled}}"+
        (Grade==SultanGrade.Gold?"，剩余{Turns}个自己的回合（跨战斗）。":"。"));
    private string Text()=> (Suit,Grade) switch {
        (SultanSuit.Slaughter,_)=>$"[gold]代价[/gold]：失去{Rules.SlaughterHpCost(Grade)}生命。造成{{Damage:diff()}}"+(Grade>=SultanGrade.Silver?"点无视格挡的伤害。":"点伤害。")+(Grade==SultanGrade.Gold?"若杀死目标，随机对另一敌人重复，直到未能杀死。":""),
        (SultanSuit.Conquest,SultanGrade.Stone)=>"[gold]代价[/gold]：失去全部格挡（至少1点）。每满4点格挡，施加1虚弱与1易伤；免代价时按当前格挡计算且不失去格挡。",
        (SultanSuit.Conquest,SultanGrade.Bronze)=>"[gold]代价[/gold]：失去1敏捷。获得2力量。",
        (SultanSuit.Conquest,SultanGrade.Silver)=>"[gold]代价[/gold]：失去2敏捷。移除目标一半格挡；直到你的下个回合，目标无法获得格挡，负面状态不会自然减少。",
        (SultanSuit.Conquest,SultanGrade.Gold)=>"[gold]代价[/gold]：目标获得50格挡。若本回合打破其全部格挡，获得5力量和5敏捷。",
        (SultanSuit.Luxury,SultanGrade.Stone)=>"[gold]代价[/gold]：失去5金币。获得10格挡。",
        (SultanSuit.Luxury,SultanGrade.Bronze)=>"[gold]代价[/gold]：消耗手牌中另一张苏丹牌（不能选择青铜奢靡）。使用并重放它1次；每次均须支付其代价。免代价时仍须选择，但不因此消耗被选牌。",
        (SultanSuit.Luxury,SultanGrade.Silver)=>"[gold]代价[/gold]：失去15金币。接下来3张非苏丹牌耗能变为0。",
        (SultanSuit.Luxury,SultanGrade.Gold)=>"[gold]代价[/gold]：失去25金币。接下来3个自己的回合，每回合前2张非苏丹牌耗能变为0。",
        (SultanSuit.Lust,SultanGrade.Stone)=>"[gold]代价[/gold]：目标获得2再生。获得2再生。",
        (SultanSuit.Lust,SultanGrade.Bronze)=>"[gold]代价[/gold]：目标获得10格挡。获得5再生。",
        (SultanSuit.Lust,SultanGrade.Silver)=>"[gold]代价[/gold]：目标获得3再生、3覆甲。获得1无实体、6再生。",
        _=>"[gold]代价[/gold]：所有敌人获得5再生、1无实体、5覆甲。获得2无实体、10再生。"
    };
    protected override async Task OnPlay(PlayerChoiceContext ctx,CardPlay play)
    {
        var target=TargetType==TargetType.AnyEnemy?play.Target??Util.RandomEnemy(Owner):null;
        var state=Util.State(Owner);
        int lostBlock=0;
        CardModel? sacrificed=null;
        bool paid=false;
        bool actualUse=play.Card==this;
        bool replacement=actualUse&&SultanPayments.ConsumeReplacement(this);
        var pass=actualUse?Owner.Creature.GetPower<PassPower>():null;
        bool waived=V04Replay.Free(this,play)||pass!=null;
        if(waived)
        {
            if(!CanResolveWaived())return;
            // Capture dependent inputs before skipping their deduction, for every exemption source.
            if(Suit==SultanSuit.Conquest&&Grade==SultanGrade.Stone)lostBlock=Owner.Creature.Block;
            if(Suit==SultanSuit.Luxury&&Grade==SultanGrade.Bronze){sacrificed=await Select(ctx,BronzeCandidates());if(sacrificed==null)return;}
            if(pass!=null&&!V04Replay.Free(this,play))await PowerCmd.Decrement(pass);
            paid=true;
        }
        else if(replacement){await PowerCmd.Apply<CrimePower>(ctx,Owner.Creature,1,Owner.Creature,play.Card);await PaymentLedger.Record(ctx,Owner,false);paid=true;}
        else
        {
            bool doubleCost=actualUse&&Owner.Creature.HasPower<DoubleCostPower>();
            int count=doubleCost?2:1;
            for(int i=0;i<count;i++)
            {
                if(Owner.Creature.GetPower<LifeScepterPower>() is {} scepter&&await scepter.TryReplace(ctx))
                {paid=true;await PaymentLedger.Record(ctx,Owner,false);continue;}
                // A second zero-block iteration under DoubleCost is deliberately allowed.
                if(i>0&&Suit==SultanSuit.Conquest&&Grade==SultanGrade.Stone&&Owner.Creature.Block==0)break;
                if(!CanPayTarget(target))
                {
                    if(!actualUse||!Util.Tyranny(Owner))break;
                    if(state!=null){int before=state.Mockery;state.Mockery++;SultanPayments.LogFailure(ctx,play,this,before,state.Mockery);await state.EnforceHp();}
                    paid=true;
                    if(Owner.Creature.IsDead)return;
                    continue;
                }
                switch(Suit)
                {
                    case SultanSuit.Slaughter: await Util.LoseHp(ctx,Owner,Rules.SlaughterHpCost(Grade),this);break;
                    case SultanSuit.Conquest:
                        if(Grade==SultanGrade.Stone){lostBlock+=(int)Owner.Creature.Block;await CreatureCmd.LoseBlock(ctx,Owner.Creature,Owner.Creature.Block,Owner.Creature);}
                        else if(Grade<SultanGrade.Gold)await AddPower<DexterityPower>(ctx,Grade==SultanGrade.Bronze?-1:-2);
                        else if(target!=null){int before=target.Block;await CreatureCmd.GainBlock(target,50,ValueProp.Unpowered,null);if(target.Block-before<50)continue;}
                        break;
                    case SultanSuit.Luxury:
                        if(Grade==SultanGrade.Bronze)
                        {
                            var payment=await Select(ctx,BronzeCandidates());
                            if(payment==null)continue;
                            sacrificed??=payment;
                            await CardCmd.Exhaust(ctx,payment);
                        }
                        else {int cost=Rules.LuxuryGoldCost(Grade);await PlayerCmd.LoseGold(cost,Owner);}
                        break;
                    case SultanSuit.Lust:
                        var enemies=Grade==SultanGrade.Gold?Util.Enemies(Owner).ToArray():target==null?[]:new[]{target};
                        foreach(var enemy in enemies)
                        {
                            if(Grade==SultanGrade.Bronze){await CreatureCmd.GainBlock(enemy,10,ValueProp.Unpowered,null);TurnOrderDiagnostics.Record("bronze lust block granted",Owner.Creature.CombatState);}
                            else await AddPower<RegenPower>(ctx,Grade==SultanGrade.Stone?2:Grade==SultanGrade.Silver?3:5,enemy);
                            if(Grade>=SultanGrade.Silver)await AddPower<PlatingPower>(ctx,Grade==SultanGrade.Silver?3:5,enemy);
                            if(Grade==SultanGrade.Gold)await AddPower<IntangiblePower>(ctx,1,enemy);
                        } break;
                }
                paid=true;await PaymentLedger.Record(ctx,Owner);
            }
        }
        if(!paid||Owner.Creature.IsDead)return;
        if(actualUse)
        {
            var original=Original;
            if(V04.Data(Owner).Grace)V04.Data(Owner).GraceUsed.Add(original);
            else {original.BattlesRemaining=Rules.BattleDeadline(Grade);original.TurnsSinceUse=0;original.UsedThisBattle=true;original.UsedThisTurn=true;}
        }
        switch(Suit)
        {
            case SultanSuit.Slaughter:
                while(target!=null&&!target.IsDead)
                {
                    if(Grade>=SultanGrade.Silver)
                        await CreatureCmd.Damage(ctx,target,Rules.SlaughterDamage(Grade),ValueProp.Move|ValueProp.Unblockable,Owner.Creature,play.Card,play);
                    else await DamageCmd.Attack(Rules.SlaughterDamage(Grade)).FromCard(play.Card,play).Targeting(target).Execute(ctx);
                    if(Grade!=SultanGrade.Gold||!target.IsDead)break;
                    target=Util.RandomEnemy(Owner);
                }break;
            case SultanSuit.Conquest:
                if(Grade==SultanGrade.Stone&&target!=null){await AddPower<WeakPower>(ctx,lostBlock/4,target);await AddPower<VulnerablePower>(ctx,lostBlock/4,target);}
                if(Grade==SultanGrade.Bronze)await AddPower<StrengthPower>(ctx,2);
                if(Grade==SultanGrade.Silver&&target!=null)
                {
                    await CreatureCmd.LoseBlock(ctx,target,target.Block/2,Owner.Creature);
                    var lockPower=await PowerCmd.Apply<SubjugatedPower>(ctx,target,1,Owner.Creature,this);
                    if(lockPower!=null)lockPower.CommanderId=Owner.NetId;
                }
                if(Grade==SultanGrade.Gold&&target!=null)
                {
                    var challenge=await PowerCmd.Apply<ConquestChallengePower>(ctx,Owner.Creature,1,Owner.Creature,this);
                    challenge?.Targets.Add(target);
                }break;
            case SultanSuit.Luxury:
                if(Grade==SultanGrade.Stone)await CreatureCmd.GainBlock(Owner.Creature,10,ValueProp.Move,play);
                if(Grade==SultanGrade.Bronze&&sacrificed is SultanCard chosen)
                {
                    // Re-enter through game's autoplay pipeline so replay hooks and costs execute.
                    chosen.ExhaustOnNextPlay=!waived;
                    using(SultanPayments.AddReplay(chosen))await CardCmd.AutoPlay(ctx,chosen,chosen.TargetType==TargetType.AnyEnemy?Util.RandomEnemy(Owner):null);
                }
                if(Grade==SultanGrade.Silver)await AddPower<FreeCardsPower>(ctx,3);
                if(Grade==SultanGrade.Gold)
                {
                    var power=Owner.Creature.GetPower<GoldenLuxuryPower>()??await PowerCmd.Apply<GoldenLuxuryPower>(ctx,Owner.Creature,1,Owner.Creature,this);
                    power?.Refresh();
                }break;
            case SultanSuit.Lust:
                if(Grade>=SultanGrade.Silver)await AddPower<IntangiblePower>(ctx,Grade==SultanGrade.Silver?1:2);
                await AddPower<RegenPower>(ctx,new[]{2,5,6,10}[(int)Grade]);break;
        }
        if(actualUse&&Util.Tyranny(Owner)&&!Owner.Creature.IsDead)await CardPileCmd.Draw(ctx,1,Owner);
    }
}
public sealed class StoneSlaughter():SultanCard(SultanSuit.Slaughter,SultanGrade.Stone);
public sealed class BronzeSlaughter():SultanCard(SultanSuit.Slaughter,SultanGrade.Bronze);
public sealed class SilverSlaughter():SultanCard(SultanSuit.Slaughter,SultanGrade.Silver);
public sealed class GoldSlaughter():SultanCard(SultanSuit.Slaughter,SultanGrade.Gold);
public sealed class StoneConquest():SultanCard(SultanSuit.Conquest,SultanGrade.Stone);
public sealed class BronzeConquest():SultanCard(SultanSuit.Conquest,SultanGrade.Bronze);
public sealed class SilverConquest():SultanCard(SultanSuit.Conquest,SultanGrade.Silver);
public sealed class GoldConquest():SultanCard(SultanSuit.Conquest,SultanGrade.Gold);
public sealed class StoneLuxury():SultanCard(SultanSuit.Luxury,SultanGrade.Stone);
public sealed class BronzeLuxury():SultanCard(SultanSuit.Luxury,SultanGrade.Bronze);
public sealed class SilverLuxury():SultanCard(SultanSuit.Luxury,SultanGrade.Silver);
public sealed class GoldLuxury():SultanCard(SultanSuit.Luxury,SultanGrade.Gold);
public sealed class StoneLust():SultanCard(SultanSuit.Lust,SultanGrade.Stone);
public sealed class BronzeLust():SultanCard(SultanSuit.Lust,SultanGrade.Bronze);
public sealed class SilverLust():SultanCard(SultanSuit.Lust,SultanGrade.Silver);
public sealed class GoldLust():SultanCard(SultanSuit.Lust,SultanGrade.Gold);
