using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
namespace ArtuMod;

public static class V04
{
    public sealed class Battle
    {
        public HashSet<ModelId> Thoughts=[];public HashSet<SultanSuit> Suits=[];
        public HashSet<Type> ThirdUsed=[];public HashSet<SultanCard> GraceUsed=[];
        public bool Grace,GracePenalty,NoteActive,RegicideUsed,ConceptUsed,NoteShown,VictoryProcessed,SettlementProcessed;
        public int AngerAtStart,RebellionTriggers,NoteClaimed;public int BeckyEndQueuedTurn=-1;
    }
    static readonly ConditionalWeakTable<Player,Battle> Battles=new();
    public static Battle Data(Player p)=>Battles.GetOrCreateValue(p);
    public static void Reset(Player p){V04Replay.Purified.RemoveWhere(c=>c.Owner==p);Battles.Remove(p);Data(p).AngerAtStart=Util.State(p)?.Anger??0;}
    public static int Crime(Player p)=>p.PlayerCombatState!=null&&CombatManager.Instance.IsInProgress?p.Creature.GetPowerAmount<CrimePower>():Util.State(p)?.Crime??0;
    public static async Task RemoveCrime(PlayerChoiceContext ctx,Player p,int n)
    {var power=p.Creature.GetPower<CrimePower>();if(power!=null)await PowerCmd.ModifyAmount(ctx,power,-Math.Min(n,power.Amount),p.Creature,null);else if(Util.State(p) is {} s)s.Crime=Math.Max(0,s.Crime-n);}
    public static async Task<CardModel?> Fetch(PlayerChoiceContext ctx,Player p,Func<CardModel,bool> filter,bool drawOnly=false)
    {var pcs=p.PlayerCombatState!;var cards=drawOnly?pcs.DrawPile.Cards:pcs.DrawPile.Cards.Concat(pcs.DiscardPile.Cards);var card=await Util.Choose(p,cards.Where(filter),false,ctx);if(card!=null)await CardPileCmd.Add(card,PileType.Hand);return card;}
    public static async Task<int> ChooseArmy(PlayerChoiceContext ctx,Player p,IEnumerable<int> kinds)
    {var options=kinds.Distinct().Select(k=>ArmyChoice.Create(k)).Cast<CardModel>().ToArray();return (await Util.Choose(p,options,false,ctx) as ArmyChoice)?.Kind??kinds.First();}
    public static async Task<int> Convert(PlayerChoiceContext ctx,Player p,int limit,int? kind,CardModel? source=null)
    {
        var levy=p.Creature.GetPower<LevyPower>();if(levy==null)return 0;int n=Math.Min(limit,levy.Amount);
        await ArmyPower.Consume(ctx,levy,n,source,false);
        if(kind.HasValue)await ArmyPower.Gain(ctx,p,kind.Value,n,source);
        else for(int i=0;i<n;i++)await ArmyPower.Gain(ctx,p,ArmyPower.RandomAdvanced(p),1,source);
        if(Util.State(p) is {} s)s.LeviesConverted+=n;
        foreach(var recruitment in p.Creature.Powers.OfType<TemporaryRecruitmentPower>().ToArray())await recruitment.OnConverted(ctx,n,source);
        await RevolutionAgenda.Refresh(p,ctx);return n;
    }
    public static async Task TriggerAll(PlayerChoiceContext ctx,Player p,int times=1)
    {for(int i=0;i<times&&!p.Creature.IsDead;i++)foreach(var (_,_,source) in ArmyPower.Formations(p).ToArray())await source.Trigger(ctx);}
    public static IEnumerable<ArtuCard> RewardCards(Player p)=>p.Character.CardPool.GetUnlockedCards(p.UnlockState,p.RunState.Players.Count>1?CardMultiplayerConstraint.MultiplayerOnly:CardMultiplayerConstraint.SingleplayerOnly).OfType<ArtuCard>().Where(c=>c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
    public static async Task Victory(Player p,PlayerChoiceContext ctx)
    {
        var s=Util.State(p);if(s==null||p.Creature.IsDead)return;var d=Data(p);
        if(d.VictoryProcessed)return;d.VictoryProcessed=true;
        if(d.Grace&&d.GracePenalty)
        foreach(var card in d.GraceUsed.Where(c=>p.Deck.Cards.Contains(c)).ToArray())
        {
            card.BattlesRemaining--;if(card.Grade==SultanGrade.Gold)card.TurnsSinceUse++;
            GD.Print($"[Artu grace deadline] {card.Id} battles={card.BattlesRemaining} goldTurns={card.TurnsSinceUse} instance={p.Deck.Cards.ToList().IndexOf(card)}");
            bool turnExpired=card.Grade==SultanGrade.Gold&&card.TurnsSinceUse>=3;
            if(card.BattlesRemaining<=0||turnExpired)await s.Expire(ctx,card);
            if(turnExpired)card.TurnsSinceUse=0; // Same reset as the original gold three-turn expiry path.
            if(p.Creature.IsDead)return;
        }
        if(d.ThirdUsed.Contains(typeof(AtonementPrayer))&&s.Crime==0&&s.Anger==d.AngerAtStart)
        {s.Devotion++;GD.Print($"[Artu evolution] 虔信 {s.Devotion}/3");if(s.Devotion==3)await Evolve<AtonementPrayer,PerfectWorld>(p);}
        if(d.ThirdUsed.Contains(typeof(AncientFaith))&&d.Suits.Count>=2)
        {s.Fanaticism++;GD.Print($"[Artu evolution] 狂信 {s.Fanaticism}/3");if(s.Fanaticism==3)await Evolve<AncientFaith,NewWorld>(p);}
        if(d.ThirdUsed.Contains(typeof(RebellionIntent))&&d.RebellionTriggers>=2)
        {s.Treason++;GD.Print($"[Artu evolution] 谋逆 {s.Treason}/3");if(s.Treason==3)await Evolve<RebellionIntent,RegicidePlan>(p);}
        int removals=(d.NoteActive&&(s.AgendaFlags&1)!=0?1:0)+(d.RegicideUsed&&SultanUsage.UsedKinds(p).Count==0?1:0);
        for(int i=0;i<removals&&p.Deck.Cards.Any(c=>c is SultanCard);i++)
        {
            var selected=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,1),c=>c is SultanCard)).FirstOrDefault();
            if(selected!=null)await Util.RemoveSultan(selected);
        }
        if(d.NoteActive&&(s.AgendaFlags&16)!=0)await RevolutionRewards.Offer(p,RewardCards(p).Where(c=>c.Reading),"理论储备");
        await RevolutionAgenda.Refresh(p,ctx);
        // Crime persists until an explicit removal effect or the rest-site action.
    }
    public static async Task Evolve<T,F>(Player p)where T:CardModel where F:CardModel
    {await Thoughts.RemoveSame(p,ModelDb.Card<T>());await Util.AddToDeck(p,ModelDb.Card<F>());}
}
public sealed class ArmyChoice:OptionCard
{
    public int Kind {get;private set;}=1;
    public ArmyChoice():base("兵种","选择兵种。"){}
    public static ArmyChoice Create(int kind){var c=(ArmyChoice)ModelDb.Card<ArmyChoice>().ToMutable();c.Kind=kind;((StringVar)c.DynamicVars["Choice"]).StringValue=new[]{"杂兵","士兵","精兵","锐士"}[kind];return c;}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Choice","士兵")];
    public override List<(string,string)> Localization=>new CardLoc("{Choice}","选择{Choice}。");
}
public sealed class AcademyPower():ArtuPower("革命军校","每回合开始先获得2杂兵，再转化至多{Amount}杂兵为选定高级兵种。")
{
    public override PowerInstanceType InstanceType=>PowerInstanceType.Instanced;
    public int Kind {get;set;}=1;
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p){if(p!=Owner.Player)return;await ArmyPower.Gain(ctx,p,0,2);await V04.Convert(ctx,p,Amount,Kind);}
}
public sealed class KingsGracePower():ArtuPower("全凭君恩","本场苏丹牌0费、免代价、额外重放1次，期限与履行状态冻结。")
{
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified){modified=original;if(card.Owner!=Owner.Player||card is not SultanCard)return false;modified=0;return true;}
    public override int ModifyCardPlayCount(CardModel card,Creature? target,int count)=>card.Owner==Owner.Player&&card is SultanCard?count+1:count;
}
public sealed class OpenThoughtPower():ArtuPower("洞开的思想","本场无法从卡牌直接效果获得格挡。")
{public override decimal ModifyBlockMultiplicative(Creature target,decimal amount,ValueProp props,CardModel? card,CardPlay? play)=>target==Owner&&card!=null?0:1;}
public sealed class SpiralWaltzPower():ArtuPower("螺旋圆舞曲","下一张能力牌的效果额外结算{Amount}次。")
{
    HashSet<CardPlay> pending=[];
    protected override void DeepCloneFields(){base.DeepCloneFields();pending=[];}
    public override Task BeforeCardPlayed(CardPlay play){if(play.Player==Owner.Player&&play.Card.Type==CardType.Power&&play.IsFirstInSeries)pending.Add(play);return Task.CompletedTask;}
    public override async Task AfterCardPlayedLate(PlayerChoiceContext ctx,CardPlay play)
    {
        if(!pending.Remove(play))return;int repeats=Amount;await PowerCmd.Remove(this);
        for(int i=0;i<repeats&&!Owner.IsDead&&CombatManager.Instance.IsInProgress;i++)
        {if(play.Card is ArtuCard card)await card.ExecuteCopiedEffect(ctx,play);else if(AccessTools.Method(play.Card.GetType(),"OnPlay").Invoke(play.Card,[ctx,play]) is Task task)await task;}
    }
}
public sealed class LifeScepterPower():OncePerTurnPower("生命权杖","每回合可替换一次苏丹代价，使一名敌人获得{Amount}再生。")
{
    int usedTurn=-1;
    protected override void DeepCloneFields(){base.DeepCloneFields();usedTurn=-1;}
    public bool Available=>usedTurn!=Owner.Player!.PlayerCombatState!.TurnNumber&&Util.Enemies(Owner.Player).Any();
    public async Task<bool> TryReplace(PlayerChoiceContext ctx)
    {
        if(!Available)return false;var player=Owner.Player!;
        var choice=await Util.Choose(player,[ModelDb.Card<ScepterDeclineOption>(),ModelDb.Card<ScepterReplaceOption>()],false,ctx);
        if(choice is not ScepterReplaceOption)return false;
        var enemies=Util.Enemies(player).ToArray();if(enemies.Length==0)return false;
        // Native card choice synchronization supplies the enemy index on all peers.
        var tokens=enemies.Select((e,i)=>EnemyChoice.Create(i)).Cast<CardModel>().ToArray();
        int index=(await Util.Choose(player,tokens,false,ctx) as EnemyChoice)?.Index??0;
        usedTurn=player.PlayerCombatState!.TurnNumber;
        await PowerCmd.Apply<RegenPower>(ctx,enemies[index],Amount,Owner,null);return true;
    }
}
public sealed class EnemyChoice:OptionCard
{
    public int Index {get;private set;}
    public EnemyChoice():base("敌人","选择代价替换的目标。"){}
    public static EnemyChoice Create(int index){var c=(EnemyChoice)ModelDb.Card<EnemyChoice>().ToMutable();c.Index=index;((StringVar)c.DynamicVars["Enemy"]).StringValue=$"敌人 {index+1}";return c;}
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Enemy","敌人")];
    public override List<(string,string)> Localization=>new CardLoc("{Enemy}","让该敌人获得再生。");
}
public sealed class CrimeProfitPower():OncePerTurnPower("以罪谋利","每回合首次获得罪证，抽2张牌并获得1能量。")
{
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext ctx,PowerModel power,decimal amount,Creature? applier,CardModel? source)
    {if(power.Owner!=Owner||power is not CrimePower||amount<=0||!First("crime"))return;await CardPileCmd.Draw(ctx,2,Owner.Player!);await PlayerCmd.GainEnergy(1,Owner.Player!);}
}
public sealed class UndergroundPressPower():OncePerTurnPower("地下印坊","每回合首次读物，从抽牌堆选1张非苏丹牌入手。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player==Owner.Player&&play.Card is ArtuCard {Reading:true}&&First("reading"))await V04.Fetch(ctx,play.Player,c=>c is not SultanCard,true);}
}
public sealed class RoyalConscriptionPower():OncePerTurnPower("王命征召","每回合首次苏丹牌，选择并获得{Amount}层高级兵种。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player!=Owner.Player||play.Card is not SultanCard||!First("sultan"))return;int kind=await V04.ChooseArmy(ctx,play.Player,[1,2,3]);await ArmyPower.Gain(ctx,play.Player,kind,Amount,play.Card);}
}
public sealed class AtonementPower():OncePerTurnPower("赎罪祈愿","每回合首张抽到的非苏丹牌，仅本回合0费。")
{
    public override Task AfterCardDrawn(PlayerChoiceContext ctx,CardModel card,bool fromHandDraw)
    {if(card.Owner==Owner.Player&&card is not SultanCard&&First("draw"))card.EnergyCost.SetThisTurn(0);return Task.CompletedTask;}
}
public sealed class AncientFaithPower():ArtuPower("古老的信仰","每回合首次主动苏丹牌：额外失去2HP，免代价重放1次。");
public sealed class NewWorldPower():ArtuPower("新世界","每回合首次主动苏丹牌：额外失去3HP，免代价重放2次，再抽2并获得1能量。");
public sealed class RebellionIntentPower():OncePerTurnPower("谋反的意图","每回合首次其他非苏丹牌获得1锐士；之前未用苏丹牌时结算军队一次。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner.Player||play.Card is SultanCard or RebellionIntent||!First("non"))return;
        await ArmyPower.Gain(ctx,play.Player,3,1,play.Card);
        if(!SultanUsage.SultanBefore(play)){V04.Data(play.Player).RebellionTriggers++;await V04.TriggerAll(ctx,play.Player);}
    }
}
public sealed class PerfectWorldPower():ArtuPower("完美世界","苏丹免代价；非苏丹牌0费并在使用后消耗，每张因此消耗时全体6伤害。")
{
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified){modified=original;if(card.Owner!=Owner.Player||card is SultanCard)return false;modified=0;return true;}
    public override CardLocation ModifyCardPlayResultLocation(CardModel card,bool auto,ResourceInfo resources,CardLocation location)
        =>card.Owner==Owner.Player&&card is not SultanCard?new CardLocation(location.player,PileType.Exhaust,location.position):location;
    public override async Task AfterCardExhausted(PlayerChoiceContext ctx,CardModel card,bool ethereal)
    {if(card.Owner!=Owner.Player||card is SultanCard||!V04Replay.Purified.Remove(card))return;await CreatureCmd.Damage(ctx,Util.Enemies(card.Owner).ToArray(),6,ValueProp.Unpowered,Owner);}
    public override Task BeforeCardPlayed(CardPlay play){if(play.Player==Owner.Player&&play.Card is not SultanCard)V04Replay.Purified.Add(play.Card);return Task.CompletedTask;}
}
public sealed class RegicidePower():ArtuPower("弑君的计划","每回合结束额外结算所有兵种2次。")
{
    public override Task BeforeSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)=>participants.Contains(Owner)?V04.TriggerAll(ctx,Owner.Player!,2):Task.CompletedTask;
}
public sealed class RevolutionConceptPower():OncePerTurnPower("革命的构思","每回合首次其他读物获得1能量；首次其他思潮获得各1高级兵种。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner.Player||play.Card is not ArtuCard c||c is RevolutionConcept)return;
        if(c.Reading&&First("reading"))await PlayerCmd.GainEnergy(1,play.Player);
        if(c.Thought&&First("thought"))for(int kind=1;kind<=3;kind++)await ArmyPower.Gain(ctx,play.Player,kind,1,play.Card);
    }
}
public sealed class RevolutionNotePower():OncePerTurnPower("革命议程","字条拥有已解锁的所有议程强化。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {if(play.Player==Owner.Player&&(Util.State(play.Player)!.AgendaFlags&32)!=0&&play.Card is ArtuCard {Thought:true} and not NoteInBook&&First("thought")){await ArmyPower.Gain(ctx,play.Player,1,1,play.Card);await CardPileCmd.Draw(ctx,1,play.Player);}}
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player p)
    {if(p==Owner.Player&&(Util.State(p)!.AgendaFlags&64)!=0)await V04.Convert(ctx,p,2,null);}
}
// Add new replays to ONE native play series. Only the appended faith/world slots are exempt;
// external replay slots retain their ordinary costs. No nested AutoPlay multiplication.
public static class V04Replay
{
    public sealed class Series {public bool Auto;public int FreeFrom=int.MaxValue,Bonus;public bool Charged;}
    static readonly ConditionalWeakTable<CardModel,Series> SeriesByCard=new();
    public static readonly HashSet<CardModel> Purified=[];
    public static Series SeriesFor(CardModel c)=>SeriesByCard.GetOrCreateValue(c);
    public static void Begin(CardModel c,bool auto){SeriesByCard.Remove(c);SeriesFor(c).Auto=auto;}
    public static bool Exempt(CardPlay play)=>SeriesFor(play.Card).Bonus>0&&play.PlayIndex>=SeriesFor(play.Card).FreeFrom;
    public static bool Free(SultanCard card,CardPlay? play=null)=>BeckyController.Active(card.Owner)||V04.Data(card.Owner).Grace||card.Owner.Creature.HasPower<PerfectWorldPower>()||play!=null&&Exempt(play);
}
[HarmonyPatch(typeof(CardModel),nameof(CardModel.OnPlayWrapper))]
static class V04SeriesStart {static void Prefix(CardModel __instance,bool isAutoPlay)=>V04Replay.Begin(__instance,isAutoPlay);}
[HarmonyPatch(typeof(Hook),nameof(Hook.ModifyCardPlayCount))]
static class V04FaithReplays
{
    [HarmonyPriority(Priority.Last)]
    static void Postfix(CardModel card,ref int __result)
    {
        if(card is not SultanCard||V04Replay.SeriesFor(card).Auto||SultanUsage.ManualSultanUsedThisTurn(card.Owner))return;
        var s=V04Replay.SeriesFor(card);int bonus=(card.Owner.Creature.HasPower<AncientFaithPower>()?1:0)+(card.Owner.Creature.HasPower<NewWorldPower>()?2:0);
        if(bonus==0)return;s.FreeFrom=__result;s.Bonus=bonus;__result+=bonus;
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterCardPlayed))]
static class V04FaithAfter
{
    static void Postfix(PlayerChoiceContext choiceContext,CardPlay cardPlay,ref Task __result)=>__result=After(__result,choiceContext,cardPlay);
    static async Task After(Task original,PlayerChoiceContext ctx,CardPlay play)
    {
        await original;if(play.Card is not SultanCard||play.IsAutoPlay)return;
        var s=V04Replay.SeriesFor(play.Card);if(s.Bonus==0)return;
        if(!s.Charged){s.Charged=true;int hp=(play.Player.Creature.HasPower<AncientFaithPower>()?2:0)+(play.Player.Creature.HasPower<NewWorldPower>()?3:0);await Util.LoseHp(ctx,play.Player,hp);}
        if(play.IsLastInSeries&&play.Player.Creature.HasPower<NewWorldPower>()&&!play.Player.Creature.IsDead){await CardPileCmd.Draw(ctx,2,play.Player);await PlayerCmd.GainEnergy(1,play.Player);}
    }
}
