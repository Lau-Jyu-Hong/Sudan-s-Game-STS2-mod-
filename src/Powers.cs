namespace ArtuMod;
public abstract class ArtuPower(string name,string description,PowerType type=PowerType.Buff):CustomPowerModel
{
    public override PowerType Type=>type;
    public override PowerStackType StackType=>PowerStackType.Counter;
    public override List<(string,string)> Localization=>new PowerLoc(name,description,description);
    public override string CustomPackedIconPath=>ModelDb.Power<StrengthPower>().PackedIconPath;
    public override string CustomBigIconPath=>ModelDb.Power<StrengthPower>().ResolvedBigIconPath;
}
public sealed class PassPower():ArtuPower("御用通行","本回合下一张苏丹牌免除代价。")
{
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class DoubleCostPower():ArtuPower("色厉内茬","苏丹牌0费，但每次效果的代价处理两次。");
public sealed class FreeCardsPower():ArtuPower("白银奢靡","接下来{Amount}张非苏丹牌0费。")
{
    // Confirmed: the three remaining uses persist across turns.
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {modified=original;if(card.Owner!=Owner.Player||card is SultanCard)return false;modified=0;return true;}
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player==Owner.Player&&play.Card is not SultanCard&&play.IsFirstInSeries)await PowerCmd.Decrement(this);}
}
public sealed class GoldenLuxuryPower():ArtuPower("黄金奢靡","每回合前2张非苏丹牌0费，持续3个自己的回合。")
{
    public int TurnsRemaining {get;set;}=3;
    public int CardsLeft {get;set;}=2;
    public override int DisplayAmount=>TurnsRemaining;
    public void Refresh(){TurnsRemaining=3;CardsLeft=2;InvokeDisplayAmountChanged();}
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {modified=original;if(CardsLeft<=0||card.Owner!=Owner.Player||card is SultanCard)return false;modified=0;return true;}
    public override Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player==Owner.Player&&play.Card is not SultanCard&&play.IsFirstInSeries)CardsLeft=Math.Max(0,CardsLeft-1);return Task.CompletedTask;}
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)
    {if(player==Owner.Player)CardsLeft=2;return Task.CompletedTask;}
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner)&&--TurnsRemaining<=0)await PowerCmd.Remove(this);}
}
public sealed class SubjugatedPower():ArtuPower("征服","无法获得格挡；负面状态不自然减少，直到征服者下回合。",PowerType.Debuff)
{
    public ulong CommanderId {get;set;}
    public override decimal ModifyBlockMultiplicative(Creature target,decimal amount,ValueProp props,CardModel? source,CardPlay? play)=>target==Owner?0:1;
    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext ctx,Player player)
    {if(player.NetId==CommanderId)await PowerCmd.Remove(this);}
}
public sealed class ConquestChallengePower():ArtuPower("黄金征服挑战","本回合打破指定目标的全部格挡，获得5力量、5敏捷。")
{
    public List<Creature> Targets {get;private set;}=[];
    protected override void DeepCloneFields(){base.DeepCloneFields();Targets=[];}
    public override async Task AfterBlockBroken(PlayerChoiceContext ctx,Creature target,Creature? breaker)
    {
        int count=Targets.Count(t=>t==target);if(count==0)return;
        Targets.RemoveAll(t=>t==target);
        await PowerCmd.Apply<StrengthPower>(ctx,Owner,5*count,Owner,null);
        await PowerCmd.Apply<DexterityPower>(ctx,Owner,5*count,Owner,null);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class NoDexterityPower():ArtuPower("失去征服","本场战斗无法获得敏捷。",PowerType.Debuff)
{
    public override bool TryModifyPowerAmountReceived(PowerModel power,Creature target,decimal amount,Creature? applier,out decimal modified)
    {modified=amount;if(target==Owner&&power is DexterityPower&&amount>0){modified=0;return true;}return false;}
}
public sealed class MirrorHealingPower():ArtuPower("纵欲的惩罚","每当恢复生命，所有敌人恢复等量生命。",PowerType.Debuff)
{
    public override async Task AfterCurrentHpChanged(Creature creature,decimal delta)
    {if(creature!=Owner||delta<=0)return;foreach(var target in Util.Enemies(Owner.Player!).ToArray())await CreatureCmd.Heal(target,delta);}
}
public sealed class SiegeBoulderPower():ArtuPower("苏丹的滚石","敌方回合开始时，对所有玩家造成{Amount}伤害，再增加5。")
{
    public override async Task BeforeSideTurnStart(PlayerChoiceContext ctx,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {
        if(side!=CombatSide.Enemy||Owner.IsDead)return;
        await CreatureCmd.Damage(ctx,state.GetOpponentsOf(Owner),Amount,ValueProp.Unpowered,Owner);
        await PowerCmd.ModifyAmount(ctx,this,5,null,null);
    }
}
public abstract class ArmyPower(string name,string description):ArtuPower(name,description)
{
    public abstract int Kind {get;}
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {
        if(!participants.Contains(Owner)||Owner.IsDead)return;
        if(Kind>0&&Owner.HasPower<ArmySpecializationPower>()&&this!=All(Owner.Player!).FirstOrDefault(a=>a.Kind>0))return;
        await Trigger(ctx);
    }
    public async Task Trigger(PlayerChoiceContext ctx)
    {
        var p=Owner.Player!;var target=Util.RandomEnemy(p);if(target==null)return;
        int kind=Kind;int amount=Amount;
        if(Kind>0&&Owner.GetPower<ArmySpecializationPower>() is {} specialized)
        {kind=specialized.FormationKind;amount=All(p).Where(a=>a.Kind>0).Sum(a=>a.Amount);}
        var interchange=Owner.GetPower<ArmyInterchangePower>();
        int veteransForThreshold=amount;
        if(kind==2&&interchange?.CountSoldiersForThreshold==true&&CountAs(p,2)>0)veteransForThreshold+=CountAs(p,1);
        if(kind==2&&veteransForThreshold>=3)await PowerCmd.Apply<VigorPower>(ctx,Owner,5,Owner,null);
        int damage=kind==3?4:kind==2&&interchange!=null&&CountAs(p,3)>0?2+interchange.Amount:2;
        for(int i=0;i<amount;i++)
        {
            target=Util.RandomEnemy(p);if(target==null)break;
            if(kind==0&&Owner.HasPower<AllArmyPower>())
                await CreatureCmd.Damage(ctx,Util.Enemies(p).ToArray(),damage,ValueProp.Move,Owner);
            else await CreatureCmd.Damage(ctx,target,damage,ValueProp.Move,Owner);
            // Each layer attacks separately. Vigor belongs to the first attack, not every soldier.
            if(Owner.HasPower<VigorPower>())await PowerCmd.Remove<VigorPower>(Owner);
        }
        if(kind==1)await CreatureCmd.GainBlock(Owner,amount,ValueProp.Unpowered,null);
        if(kind==3&&interchange!=null&&CountAs(p,1)>0)await CreatureCmd.GainBlock(Owner,amount*interchange.Amount,ValueProp.Unpowered,null);
        if(kind==2)
        {
            target=target is {IsDead:false}?target:Util.RandomEnemy(p);
            if(target!=null&&veteransForThreshold>=6)await PowerCmd.Apply<VulnerablePower>(ctx,target,1,Owner,null);
            if(target!=null&&veteransForThreshold>=9)await PowerCmd.Apply<WeakPower>(ctx,target,1,Owner,null);
        }
    }
    public static int RandomAdvanced(Player p)=>p.RunState.Rng.CombatCardGeneration.NextInt(1,4);
    public static int CountAs(Player p,int kind)=>Formations(p).Where(f=>f.kind==kind).Sum(f=>f.amount);
    public static IEnumerable<(int kind,int amount,ArmyPower source)> Formations(Player p)
    {
        var armies=All(p).Where(a=>a.Amount>0).ToArray();
        if(p.Creature.GetPower<ArmySpecializationPower>() is {} specialized)
        {
            foreach(var levy in armies.Where(a=>a.Kind==0))yield return(0,levy.Amount,levy);
            var advanced=armies.Where(a=>a.Kind>0).ToArray();
            if(advanced.Length>0)yield return(specialized.FormationKind,advanced.Sum(a=>a.Amount),advanced[0]);
        }
        else foreach(var army in armies)yield return(army.Kind,army.Amount,army);
    }
    public static async Task Consume(PlayerChoiceContext ctx,ArmyPower army,int amount,CardModel? source=null,bool countAsConsumption=true)
    {
        var player=army.Owner.Player!;int n=Math.Min(amount,army.Amount);if(n<=0)return;
        await PowerCmd.ModifyAmount(ctx,army,-n,player.Creature,source);
        if(countAsConsumption&&player.Creature.GetPower<LivingOffWarPower>() is {} living)await living.OnConsume(ctx,n,source);
    }
    public static async Task ConsumeMany(PlayerChoiceContext ctx,Player player,IEnumerable<(ArmyPower army,int amount)> costs,CardModel? source)
    {
        var all=costs.ToArray();int total=all.Sum(c=>Math.Min(c.amount,c.army.Amount));
        foreach(var (army,amount) in all)await Consume(ctx,army,amount,source,false);
        if(player.Creature.GetPower<LivingOffWarPower>() is {} living)await living.OnConsume(ctx,total,source);
    }
    public static async Task Gain(PlayerChoiceContext ctx,Player p,int kind,int amount,CardModel? card=null,bool allowResonance=true)
    {
        if(amount<=0)return;
        if(kind==0&&p.Creature.HasPower<GloryPower>()){for(int i=0;i<amount;i++)await Gain(ctx,p,p.RunState.Rng.CombatCardGeneration.NextInt(1,4),1,card);return;}
        switch(kind){
            case 0:await PowerCmd.Apply<LevyPower>(ctx,p.Creature,amount,p.Creature,card);break;
            case 1:await PowerCmd.Apply<SoldierPower>(ctx,p.Creature,amount,p.Creature,card);break;
            case 2:await PowerCmd.Apply<VeteranPower>(ctx,p.Creature,amount,p.Creature,card);break;
            case 3:await PowerCmd.Apply<ElitePower>(ctx,p.Creature,amount,p.Creature,card);break;}
        if(allowResonance&&p.Creature.GetPower<BannerResonancePower>() is {} banner)await banner.OnGain(ctx,kind,card);
    }
    public static IEnumerable<ArmyPower> All(Player p)=>p.Creature.Powers.OfType<ArmyPower>();
}
public sealed class LevyPower():ArmyPower("杂兵","回合结束时，每层对随机敌人造成2伤害。"){public override int Kind=>0;}
public sealed class SoldierPower():ArmyPower("士兵","回合结束时，每层对随机敌人造成2伤害，获得{Amount}格挡。"){public override int Kind=>1;}
public sealed class VeteranPower():ArmyPower("精兵","回合结束时，每层对随机敌人造成2伤害。3层：出击前5活力；6层：1易伤；9层：1虚弱，各档每次结算一次。"){public override int Kind=>2;}
public sealed class ElitePower():ArmyPower("锐士","回合结束时，每层对随机敌人造成4伤害。"){public override int Kind=>3;}
public sealed class AllArmyPower():ArtuPower("全军出击","杂兵攻击所有敌人。");
public sealed class GloryPower():ArtuPower("追忆过去的荣光","生成杂兵时，改为等量随机高级兵种。");
public sealed class TrainingPower():ArtuPower("训练兵队","回合开始时，将最多{Amount}杂兵转为随机高级兵。")
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p)
    {if(p!=Owner.Player)return;var levy=Owner.GetPower<LevyPower>();if(levy==null)return;int n=Math.Min(Amount,levy.Amount);
    await V04.Convert(ctx,p,n,null);}
}
public sealed class WarSongPower():ArtuPower("强军战歌","每回合开始时，获得{Amount}精兵。")
{
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p)=>p==Owner.Player?ArmyPower.Gain(ctx,p,2,Amount):Task.CompletedTask;
}
public sealed class CrimePower():ArtuPower("罪证","1：卡牌伤害−1；2：卡牌格挡−1；3：开局抽牌−1；4：自然能量−1；5：首张非苏丹牌+1费；6：首张非苏丹牌后1晕眩；7：第4张及以后非苏丹牌额外随机弃1；8：每张非苏丹牌后1晕眩（替代第6级）。",PowerType.Debuff)
{
    public override decimal ModifyDamageAdditive(Creature? target,decimal amount,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>dealer==Owner&&card!=null&&!props.HasFlag(ValueProp.Unpowered)&&Amount>=1?-1:0;
    public override decimal ModifyBlockAdditive(Creature target,decimal amount,ValueProp props,CardModel? card,CardPlay? play)=>target==Owner&&card!=null&&Amount>=2?-1:0;
    public override decimal ModifyHandDraw(Player p,decimal count)=>p==Owner.Player&&Amount>=3?count-1:count;
    public override decimal ModifyEnergyGain(Player p,decimal amount)=>p==Owner.Player&&Amount>=4?amount-1:amount;
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {modified=original;if(Amount<5||card.Owner!=Owner.Player||card is SultanCard||card.EnergyCost.CostsX||original<0||SultanUsage.NonSultanThisTurn(card.Owner)>0)return false;modified=original+1;return true;}
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner.Player||play.Card is SultanCard)return;
        int count=SultanUsage.NonSultanThisTurn(play.Player);
        if(Amount>=8||Amount>=6&&count==1)await CardPileCmd.Add(play.Player.Creature.CombatState!.CreateCard<Dazed>(play.Player),PileType.Draw,CardPilePosition.Random);
        if(Amount>=7&&count>=4){var hand=play.Player.PlayerCombatState!.Hand.Cards.Where(c=>c!=play.Card).ToArray();var card=play.Player.RunState.Rng.CombatCardGeneration.NextItem(hand);if(card!=null)await CardCmd.Discard(ctx,card);}
    }
    public override Task AfterRemoved(Creature oldOwner){if(oldOwner.Player is {} p&&Util.State(p) is {} s)s.Crime=0;return Task.CompletedTask;}
}
public sealed class TeaPower():ArtuPower("妻子的茶会","本回合每使用一张牌，获得{Amount}格挡。")
{
    public override Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)=>play.Player==Owner.Player?CreatureCmd.GainBlock(Owner,Amount,ValueProp.Unpowered,null):Task.CompletedTask;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class ReplaySultanPower():ArtuPower("犬马声色","本回合接下来{Amount}次使用苏丹牌时，重放1次。")
{
    public override int ModifyCardPlayCount(CardModel card,Creature? target,int count)=>card.Owner==Owner.Player&&card is SultanCard?count+1:count;
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player==Owner.Player&&play.Card is SultanCard&&play.IsLastInSeries)await PowerCmd.Decrement(this);}
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class ObeyPower():ArtuPower("顺应皇命","回合开始时抽1张牌；若抽到苏丹牌，自动使用并正常处理代价。")
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p)
    {
        if(p!=Owner.Player)return;
        for(int i=0;i<Amount;i++)foreach(var card in (await CardPileCmd.Draw(ctx,1,p)).OfType<SultanCard>())
            await CardCmd.AutoPlay(ctx,card,card.TargetType==TargetType.AnyEnemy?Util.RandomEnemy(p):null);
    }
}
public sealed class WhisperPower():ArtuPower("耳语之书","下一回合无法造成或受到伤害；该回合结束时失去2力量。")
{
    public override PowerInstanceType InstanceType=>PowerInstanceType.Instanced;
    public int TurnsToWait {get;set;}=1; public bool Active {get;set;} public bool Penalized {get;set;}
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext ctx,Player p){if(p==Owner.Player&&--TurnsToWait<=0)Active=true;return Task.CompletedTask;}
    public override decimal ModifyDamageMultiplicative(Creature? target,decimal amount,ValueProp props,Creature? dealer,CardModel? card,CardPlay? play)=>Active&&(dealer==Owner||target==Owner)?0:1;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {
        if(!Active)return;
        if(participants.Contains(Owner)&&!Penalized){Penalized=true;await PowerCmd.Apply<StrengthPower>(ctx,Owner,-2,Owner,null);}
        if(side==CombatSide.Enemy)await PowerCmd.Remove(this);
    }
}
public sealed class TribunalSentencePower():ArtuPower("审判刑罚","此后每回合开始受到{Amount}伤害。",PowerType.Debuff)
{
    public override Task BeforeSideTurnStart(PlayerChoiceContext ctx,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
        =>participants.Contains(Owner)?CreatureCmd.Damage(ctx,Owner,Amount,ValueProp.Unpowered,null,null,null):Task.CompletedTask;
}
public sealed class TribunalPower():ArtuPower("审判庭","每回合开始选定一名敌人；它从历史次数最少的三项刑罚中随机选择。")
{
    Dictionary<Creature,int[]> history=[];
    protected override void DeepCloneFields(){base.DeepCloneFields();history=[];}
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p)
    {
        if(p!=Owner.Player)return;var targets=Util.Enemies(p).ToList();if(targets.Count==0)return;
        var selected=await Util.Choose(p,targets.Select(t=>(CardModel)new EnemyChoiceCard(t)),false,ctx);
        var target=(selected as EnemyChoiceCard)?.Target;
        if(target==null||target.IsDead)return;
        if(!history.TryGetValue(target,out var counts))history[target]=counts=new int[3];
        int min=counts.Min();int choice=p.RunState.Rng.CombatCardGeneration.NextItem(Enumerable.Range(0,3).Where(i=>counts[i]==min));counts[choice]++;
        if(choice==0)await PowerCmd.Apply<StrengthPower>(ctx,target,-2,Owner,null);
        if(choice==1){await PowerCmd.Apply<FrailPower>(ctx,target,1,Owner,null);await PowerCmd.Apply<VulnerablePower>(ctx,target,1,Owner,null);}
        if(choice==2)await PowerCmd.Apply<TribunalSentencePower>(ctx,target,20,Owner,null);
    }
}
