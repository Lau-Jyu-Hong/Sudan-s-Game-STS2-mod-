using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
namespace ArtuMod;
public abstract class RevolutionEvent:CustomEventModel
{
    public abstract bool Seen(Player p);
    public override string CustomInitialPortraitPath=>"res://images/events/trial.png";
    public override bool IsAllowed(IRunState state)=>state.CurrentActIndex is 1 or 2&&state.Players.Any(p=>RevolutionAgenda.Active(p)&&!Seen(p));
    protected void Finish(string text)=>EventText.Finish(this,Id.Entry+"_DONE",text);
    protected EventOption Choice(string id,string title,string desc,Func<Task> action)=>EventText.Option(this,Id.Entry+id,title,desc,action);
    protected EventOption Leave()=>Choice("_LEAVE","离开","离开。",()=>{Finish("你记下这里的人和事，继续走向高塔深处。");return Task.CompletedTask;});
}
public sealed class ClearStreamsMeeting:RevolutionEvent
{
    public override bool Seen(Player p)=>Util.State(p)?.ClearStreamsSeen==true;
    public override List<(string,string)> Localization=>new EventLoc("清流交汇",new EventPageLoc("INITIAL","秘密聚会中的人彼此打量。有人谈论皇命，有人收起字条，也有人始终站在灯光之外。"));
    protected override Task BeforeEventStarted(bool pre){if(Owner is {} p&&RevolutionAgenda.Active(p))Util.State(p)!.ClearStreamsSeen=true;return Task.CompletedTask;}
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var p=Owner!;if(!RevolutionAgenda.Active(p))return [Leave()];var options=new List<EventOption>();
        if(V04.Crime(p)>=1)options.Add(Choice("_SPY","识破密探","失去7HP；永久移除1罪证；从3张随机罕见思潮选1。",async ()=>{await Util.LoseHp(new BlockingPlayerChoiceContext(),p,7);if(p.Creature.IsDead)return;await V04.RemoveCrime(new BlockingPlayerChoiceContext(),p,1);await RevolutionRewards.Offer(p,V04.RewardCards(p).Where(c=>c.Thought&&c.Rarity==CardRarity.Uncommon),"识破密探");Finish("密探留下了罪证，而你留下了信任。");}));
        if(p.Gold>=35)options.Add(Choice("_INVITE","广邀同道","失去35金币；从3张不同的普通或罕见思潮选1。",async ()=>{await PlayerCmd.LoseGold(35,p);await RevolutionRewards.Offer(p,V04.RewardCards(p).Where(c=>c.Thought&&c.Rarity is CardRarity.Common or CardRarity.Uncommon),"广邀同道");Finish("越来越多的声音围在长桌旁。");}));
        options.Add(Leave());return options;
    }
}
public sealed class NurseryClassroom:RevolutionEvent
{
    public override bool Seen(Player p)=>Util.State(p)?.NurserySeen==true;
    public override List<(string,string)> Localization=>new EventLoc("苗圃的课堂",new EventPageLoc("INITIAL","奈费勒的苗圃中，孩子们学习识字与技术。院外，成年人练习如何保护彼此。"));
    protected override Task BeforeEventStarted(bool pre){if(Owner is {} p&&RevolutionAgenda.Active(p))Util.State(p)!.NurserySeen=true;return Task.CompletedTask;}
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var p=Owner!;if(!RevolutionAgenda.Active(p))return [Leave()];var list=new List<EventOption>();
        if(p.Gold>=30)list.Add(Choice("_KNOWLEDGE","传播知识","失去30金币；从3张合法读物选1。",async ()=>{await PlayerCmd.LoseGold(30,p);await RevolutionRewards.Offer(p,V04.RewardCards(p).Where(c=>c.Reading),"传播知识");Finish("你留下纸张与墨水，带走一本书。");}));
        if(p.Gold>=35)list.Add(Choice("_TRAIN","组织训练","失去35金币；选择训练兵队或临阵整编。",async ()=>{await PlayerCmd.LoseGold(35,p);await RevolutionRewards.Offer(p,[ModelDb.Card<TrainArmy>(),ModelDb.Card<FieldReorganization>()],"组织训练");Finish("杂乱的队列开始学会共同前进。");}));
        if(p.Gold>=20)list.Add(Choice("_RELIEF","提供救济","失去20金币，回复20HP。",async ()=>{await PlayerCmd.LoseGold(20,p);await CreatureCmd.Heal(p.Creature,20);Finish("热食与干净的床铺让你恢复了力气。");}));
        list.Add(Leave());return list;
    }
}
[HarmonyPatch(typeof(EventModel),"GenerateInitialOptionsWrapper")]
static class RevolutionHiddenOptions
{
    static void Postfix(EventModel __instance,ref IReadOnlyList<EventOption> __result)
    {
        if(__instance.Owner is not {} p||!RevolutionAgenda.Active(p))return;
        var list=__result.ToList();var ctx=new BlockingPlayerChoiceContext();
        void Add(string id,string name,string desc,Func<Task> effect)=>list.Add(EventText.Option(__instance,id,name,desc,async ()=>{await effect();if(!p.Creature.IsDead)EventText.Finish(__instance,id+"_DONE","秘密在纸页间传递，也在人的记忆中留下痕迹。");}));
        if(__instance is TabletOfTruth)Add("ARTU_TRANSCRIBE","誊录碑文","失去8HP；从3张合法读物选1。",async ()=>{await Util.LoseHp(ctx,p,8);if(!p.Creature.IsDead)await RevolutionRewards.Offer(p,V04.RewardCards(p).Where(c=>c.Reading),"誊录碑文");});
        if(__instance is Leaderless&&p.Gold>=30&&p.Deck.Cards.OfType<ArtuCard>().Any(c=>c.Thought&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare))
            Add("ARTU_SECRET_COUNCIL","秘密议会","失去30金币；复制永久牌组中的1张普通、罕见或稀有思潮。",async ()=>{var card=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(new LocString("card_selection","ARTU_CHOOSE"),1),c=>c is ArtuCard {Thought:true}&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)).FirstOrDefault();if(card==null)return;await PlayerCmd.LoseGold(30,p);await Util.AddToDeck(p,card.CanonicalInstance);});
        if(__instance is Trial&&p.Gold>=30)Add("ARTU_PROTECT_WITNESS","保护证人","失去30金币；永久移除至多2罪证。",async ()=>{await PlayerCmd.LoseGold(30,p);await V04.RemoveCrime(ctx,p,2);});
        __result=list;
    }
}
