using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Factories;
namespace ArtuMod;
public static class EventText
{
    static readonly Dictionary<string,string> Text=[];
    public static LocString Loc(string id,string text)
    {
        Text[id]=text;
        Inject();
        return new LocString("events",id);
    }
    public static void Inject()
    {
        var table=LocManager.Instance.GetTable("events");
        if(AccessTools.Field(typeof(LocTable),"_translations").GetValue(table) is Dictionary<string,string> dict)
            foreach(var entry in Text)dict[entry.Key]=entry.Value;
    }
    public static EventOption Option(EventModel model,string id,string title,string text,Func<Task> action)
        =>new(model,action,Loc(id+".title",title),Loc(id+".description",text),id,[]);
    public static void Finish(EventModel model,string id,string text)
        =>AccessTools.Method(typeof(EventModel),"SetEventFinished").Invoke(model,[Loc(id,text)]);
}
[HarmonyPatch(typeof(EventModel),"GenerateInitialOptionsWrapper")]
public static class SultanEventOptions
{
    public static void Postfix(EventModel __instance,ref IReadOnlyList<EventOption> __result)
    {
        if(__instance.Owner is not {} p||p.Character is not Artu)return;
        var list=__result.ToList();
        bool Has(SultanSuit? suit=null)=>p.Deck.Cards.OfType<SultanCard>().Any(c=>suit==null||c.Suit==suit);
        void Add(string id,string title,string text,Func<Task> action)=>list.Add(EventText.Option(__instance,"ARTU_"+id,title,text,action));
        if(__instance is TrashHeap&&Has())Add("TRASH","无人取证","永久移除1张苏丹牌。",async ()=>{
            if(await SelectSultan(p) is {} card){await Util.RemoveSultan(card);EventText.Finish(__instance,"ARTU_TRASH_DONE","垃圾堆吞没了皇命。你会编好故事，至于证据——谁能在这里找到它？");}});
        if(__instance is Wellspring&&Has(SultanSuit.Luxury))Add("WELL","一掷千金","移除奢靡牌，支付30/60/90/120金币；除基础药水外再获得随品级增加的药水。",async ()=>{
            var card=await SelectSultan(p,SultanSuit.Luxury,c=>p.Gold>=30*((int)c.Grade+1));if(card==null)return;
            await PlayerCmd.LoseGold(30*((int)card.Grade+1),p);await Util.RemoveSultan(card);
            await PotionRewards(p,false);
            if(card.Grade==SultanGrade.Stone)await PotionRewards(p,false);
            if(card.Grade>=SultanGrade.Bronze)await PotionRewards(p,true);
            if(card.Grade==SultanGrade.Silver)await PotionRewards(p,false);
            if(card.Grade==SultanGrade.Gold)await PotionRewards(p,true);
            EventText.Finish(__instance,"ARTU_WELL_DONE","你让商贩把最昂贵的东西摆出来。钱袋越来越轻，皇命却终于满足了。");});
        if(__instance is SunkenStatue)
        {
            if(Has(SultanSuit.Lust))Add("DEFILE","亵渎","移除纵欲牌。获得愧疚，升级1张牌。",async ()=>{
                var card=await SelectSultan(p,SultanSuit.Lust);if(card==null)return;await Util.RemoveSultan(card);await CardPileCmd.AddCurseToDeck<Guilty>(p);
                CardCmd.Upgrade(await CardSelectCmd.FromDeckForUpgrade(p,new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt,1)),MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.HorizontalLayout);
                EventText.Finish(__instance,"ARTU_DEFILE_DONE","雕像始终沉默。神是否看见了，你不知道；你却记得自己做过什么。");});
            if(Has(SultanSuit.Conquest))Add("REQUISITION","奉旨征收","移除征服牌。获得玉之剑。",async ()=>{
                var card=await SelectSultan(p,SultanSuit.Conquest);if(card==null)return;await Util.RemoveSultan(card);await RelicCmd.Obtain<SwordOfJade>(p);
                EventText.Finish(__instance,"ARTU_REQUISITION_DONE","石质的表面在掌中剥落，露出温润的玉色。你没有接受试炼，你只是拿走了它。");});
        }
        if(__instance is Trial&&(Has(SultanSuit.Slaughter)||Has(SultanSuit.Conquest)))Add("TRIAL","执掌审判","移除杀戮或征服牌。获得审判庭。",async ()=>{
            var card=await SelectSultan(p,null,c=>c.Suit is SultanSuit.Slaughter or SultanSuit.Conquest);if(card==null)return;
            await Util.RemoveSultan(card);await Util.AddToDeck(p,ModelDb.Card<Tribunal>());
            EventText.Finish(__instance,"ARTU_TRIAL_DONE","你坐上高处。几种悲惨的命运被摆在桌上，现在轮到别人选择。");});
        if(__instance is TabletOfTruth&&Has(SultanSuit.Luxury))Add("TABLET","以金换命","移除奢靡牌。本次解读改为支付原生命代价10倍的金币。",async ()=>{
            var card=await SelectSultan(p,SultanSuit.Luxury);if(card==null)return;await Util.RemoveSultan(card);
            GoldenDecipher.Enabled.Add(__instance,new Marker());
            await GoldenDecipher.Decipher(__instance);});
        if(__instance is SunkenTreasury)
        {
            if(Has(SultanSuit.Luxury)&&p.Gold>=100)Add("STORE","我看不上这点钱","移除奢靡牌，放入100金币和1张无附魔牌，留给未来的自己。新箱覆盖旧箱。",async ()=>{
                var card=await SelectSultan(p,SultanSuit.Luxury);if(card==null)return;
                var stored=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,1),c=>c.Enchantment==null&&c!=card)).FirstOrDefault();
                if(stored==null)return;
                await PlayerCmd.LoseGold(100,p);await Util.RemoveSultan(card);await CardPileCmd.RemoveFromDeck(stored);
                ChestStore.Save(p,stored);
                EventText.Finish(__instance,"ARTU_STORE_DONE","一百枚金币落入箱中，随后是你亲手挑选的宝物。你将它沉回水底，这样才像话。");});
            if(ChestStore.Has(p))Add("TAKE","谁把这些留在这里的？","获得此前留下的100金币和1张牌。",async ()=>{
                var card=ChestStore.Peek(p);if(card==null)return;
                await PlayerCmd.GainGold(100,p);var created=p.RunState.CreateCard(card,p);ChestStore.ApplyUpgrade(p,created);CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(created,PileType.Deck));ChestStore.Clear(p);
                EventText.Finish(__instance,"ARTU_TAKE_DONE","金币摆得整整齐齐，还有一样熟悉的东西。既然没人认领，现在就是你的了。");});
        }
        __result=list;
    }
    public static async Task<SultanCard?> SelectSultan(Player p,SultanSuit? suit=null,Func<SultanCard,bool>? predicate=null)
        =>(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,1),c=>c is SultanCard s&&(suit==null||s.Suit==suit)&&(predicate==null||predicate(s)))).FirstOrDefault() as SultanCard;
    static async Task PotionRewards(Player p,bool rare)
    {
        var pool=p.Character.PotionPool.GetUnlockedPotions(p.UnlockState).Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(p.UnlockState)).Where(x=>!rare||x.Rarity==PotionRarity.Rare).ToList();
        var potion=p.PlayerRng.Rewards.NextItem(pool);if(potion!=null)await RewardsCmd.OfferCustom(p,[new PotionReward(potion.ToMutable(),p)]);
    }
}
public sealed class Marker;
[HarmonyPatch(typeof(TabletOfTruth),"Decipher")]
public static class GoldenDecipher
{
    public static ConditionalWeakTable<EventModel,Marker> Enabled {get;}=new();
    static bool Prefix(TabletOfTruth __instance,ref Task __result)
    {if(!Enabled.TryGetValue(__instance,out _))return true;__result=Decipher(__instance);return false;}
    public static async Task Decipher(EventModel model)
    {
        var p=model.Owner!;int step=(int)AccessTools.Property(typeof(TabletOfTruth),"DecipherCount").GetValue(model)!;
        int cost=10*(step==4?p.Creature.MaxHp-1:new[]{3,6,12,24}[step]);
        if(p.Gold>=cost)
        {
            await PlayerCmd.LoseGold(cost,p);var cards=p.Deck.Cards.Where(c=>c.IsUpgradable).ToList();
            if(step==4)CardCmd.Upgrade(cards,MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.MessyLayout);
            else if(cards.Count>0)CardCmd.Upgrade(model.Rng.NextItem(cards)!);
            AccessTools.Property(typeof(TabletOfTruth),"DecipherCount").SetValue(model,++step);
        }
        if(step>=5){EventText.Finish(model,"ARTU_DECIPHER_DONE","你把最后一枚金币放在石板前。真理收下它，放过了你的生命。");return;}
        int next=10*(step==4?p.Creature.MaxHp-1:new[]{3,6,12,24}[step]);
        var options=new List<EventOption>();
        if(p.Gold>=next)options.Add(EventText.Option(model,"ARTU_DECIPHER_CONT","继续解读",$"支付{next}金币。",()=>Decipher(model)));
        options.Add(EventText.Option(model,"ARTU_DECIPHER_STOP","合上钱袋","结束本次解读。",()=>{EventText.Finish(model,"ARTU_DECIPHER_STOPPED","真理仍在等待报价，而你决定到此为止。");return Task.CompletedTask;}));
        AccessTools.Method(typeof(EventModel),"SetEventState").Invoke(model,[EventText.Loc("ARTU_DECIPHER_TEXT","生命也许无价，石板却愿意谈谈金币。"),options]);
    }
}
public sealed class Leaderless : CustomEventModel
{
    public override string CustomInitialPortraitPath=>ArtPreview.Existing("leaderless")??"res://images/events/trial.png";
    public override bool IsAllowed(MegaCrit.Sts2.Core.Runs.IRunState state)=>state.Players.Any(p=>p.Character is Artu);
    public override List<(string,string)> Localization=>new EventLoc("群龙无首",new EventPageLoc("INITIAL","长桌的主位空着。有人怀念过去的秩序，有人谈论尚未到来的世界。你可以带走一件旧物，或留下一种新的想法。"));
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var p=Owner!;
        var options=new List<EventOption>();
        if(p.Relics.Any(r=>r.Rarity!=RelicRarity.Starter))options.Add(EventText.Option(this,"ARTU_LEADER_RELIC","易物","失去一件非初始遗物，随机获得一件遗物。",async ()=>{
            var relic=await RelicSelection.Choose(p,p.Relics.Where(r=>r.Rarity!=RelicRarity.Starter).ToList());
            if(relic==null)return;await RelicCmd.Remove(relic);await RelicCmd.Obtain(RelicFactory.PullNextRelicForRewardFromFront(p).ToMutable(),p);EventText.Finish(this,"ARTU_LEADER_DONE","一件旧物被带走，另一件留下。桌首仍然空着。");}));
        options.Add(EventText.Option(this,"ARTU_LEADER_THOUGHT","聆听众声","随机获得一种普通思潮。",async ()=>{
            await Util.AddToDeck(p,Rng.NextItem(new CardModel[]{ModelDb.Card<PureThought>(),ModelDb.Card<UncleanThought>(),ModelDb.Card<FearOfSultan>()})!);
            EventText.Finish(this,"ARTU_LEADER_THOUGHT_DONE","声音汇在一起，终于成为一种你无法忽视的想法。");}));
        var groups=p.Deck.Cards.Where(c=>c is PureThought or UncleanThought or FearOfSultan or BowToSultan or TouchPurity or GlimpseDarkness or FreedomFromFear or ThirdThought).GroupBy(c=>c.Id).Where(g=>g.Count()>=3).ToList();
        if(groups.Count>0)options.Add(EventText.Option(this,"ARTU_LEADER_MERGE","汇聚思潮","选择三张同名思潮，立即触发三层成果。",async ()=>{
            var chosen=await Util.Choose(p,groups.Select(g=>g.First()));var state=Util.State(p);if(chosen==null||state==null)return;
            if(chosen is ThirdThought)
            {
                if(chosen is AtonementPrayer){state.Devotion=3;await V04.Evolve<AtonementPrayer,PerfectWorld>(p);}
                if(chosen is AncientFaith){state.Fanaticism=3;await V04.Evolve<AncientFaith,NewWorld>(p);}
                if(chosen is RebellionIntent){state.Treason=3;await V04.Evolve<RebellionIntent,RegicidePlan>(p);}
                EventText.Finish(this,"ARTU_LEADER_MERGE_DONE","长桌旁，新的思想终于成为共同的计划。");return;
            }
            state.Pure=chosen is PureThought?3:0;state.Unclean=chosen is UncleanThought?3:0;state.Fear=chosen is FearOfSultan?3:0;state.Submission=chosen is BowToSultan?3:0;state.CostsPaid=4;
            state.PureSecond=chosen is TouchPurity?3:0;state.DarkSecond=chosen is GlimpseDarkness?3:0;state.AwakeSecond=chosen is FreedomFromFear?3:0;
            await Thoughts.Resolve(p,state,new ThrowingPlayerChoiceContext());state.Pure=state.Unclean=state.Fear=state.Submission=0;
            EventText.Finish(this,"ARTU_LEADER_MERGE_DONE","一个念头被许多声音反复讲述，直到它长成了现实。");}));
        if(p.RunState.Players.Count>1)options.Add(EventText.Option(this,"ARTU_LEADER_SWAP","互通有无","向队友提议交换一件非初始遗物。双方确认后交换。",()=>RelicTrade.Propose(p)));
        options.Add(EventText.Option(this,"ARTU_LEADER_LEAVE","离席","离开。",()=>{EventText.Finish(this,"ARTU_LEADER_LEAVE_DONE","你没有决定由谁坐上主位，仍有一段路要走。");return Task.CompletedTask;}));
        return options;
    }
}
public static class RelicSelection
{
    public static async Task<RelicModel?> Choose(Player p,List<RelicModel> relics)
    {
        if(relics.Count==0)return null;
        var choices=relics.Select(r=>(CardModel)RelicChoice.Create(r)).ToList();
        var selected=await Util.Choose(p,choices);
        return selected is RelicChoice choice?choice.Relic:null;
    }
}
public sealed class RelicChoice:OptionCard
{
    public RelicModel Relic {get;private set;}
    public override string Title=>Relic?.Title.GetFormattedText()??"选择遗物";
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Name"),new StringVar("Text")];
    public override List<(string,string)> Localization=>new CardLoc("选择遗物","{Name}\n{Text}");
    public RelicChoice():base("遗物","选择遗物。"){Relic=null!;}
    public static RelicChoice Create(RelicModel relic)
    {var choice=(RelicChoice)ModelDb.Card<RelicChoice>().ToMutable();choice.Relic=relic;((StringVar)choice.DynamicVars["Name"]).StringValue=relic.Title.GetFormattedText();((StringVar)choice.DynamicVars["Text"]).StringValue=relic.DynamicDescription.GetFormattedText();return choice;}
}
