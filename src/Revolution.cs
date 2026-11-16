using MegaCrit.Sts2.Core.Saves;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Models.Badges;
namespace ArtuMod;
public static class RevolutionAgenda
{
    static readonly HashSet<Player> Checking=[];
    public static bool Active(Player p)=>p.Deck.Cards.Any(c=>c is NoteInBook);
    public static int Calculate(Player p)
    {
        var s=Util.State(p)!;int flags=s.AgendaFlags;
        int n=p.Deck.Cards.OfType<SultanCard>().Count();if(n<=4)flags|=1;if(n==0)flags|=2;
        if(V04.Crime(p)==0&&p.Creature.GetPowerAmount<LevyPower>()>=10)flags|=4;
        if(p.Creature.GetPowerAmount<SoldierPower>()>=5&&p.Creature.GetPowerAmount<VeteranPower>()>=5&&p.Creature.GetPowerAmount<ElitePower>()>=5)flags|=8;
        if(s.ReadingUses>=10&&s.ReadingNames.Split(';',StringSplitOptions.RemoveEmptyEntries).Distinct().Count()>=3)flags|=16;
        if(V04.Data(p).Thoughts.Count>=3)flags|=32;if(s.LeviesConverted>=15)flags|=64;
        return flags;
    }
    public static async Task Refresh(Player p,PlayerChoiceContext ctx)
    {
        var s=Util.State(p);if(s==null||!Active(p)||!Checking.Add(p))return;
        try
        {
            UpdateFlags(p);
            if((s.AgendaFlags&127)==127&&!s.ConceptGranted)
            {s.ConceptGranted=true;if(!p.Deck.Cards.Any(c=>c is RevolutionConcept))await Util.AddToDeck(p,ModelDb.Card<RevolutionConcept>());}
            var d=V04.Data(p);if(!d.NoteActive||p.PlayerCombatState==null||p.Creature.IsDead)return;
            // A task reward is claimed at most once per combat even if the power is replayed/copied.
            for(int bit=2;bit<=16;bit<<=1)
            {
                if((s.AgendaFlags&bit)==0||(d.NoteClaimed&bit)!=0)continue;d.NoteClaimed|=bit;
                if(bit==2)CardCmd.Upgrade(p.PlayerCombatState.AllCards.Where(c=>c.IsUpgradable).ToArray(),MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.MessyLayout);
                if(bit==4)await ArmyPower.Gain(ctx,p,0,10);
                if(bit==8)for(int kind=1;kind<=3;kind++)await ArmyPower.Gain(ctx,p,kind,2);
                if(bit==16)await V04.Fetch(ctx,p,c=>c is ArtuCard {Reading:true});
            }
            UpdateFlags(p);
            if((s.AgendaFlags&127)==127&&!s.ConceptGranted)
            {s.ConceptGranted=true;if(!p.Deck.Cards.Any(c=>c is RevolutionConcept))await Util.AddToDeck(p,ModelDb.Card<RevolutionConcept>());}
        }
        finally{Checking.Remove(p);}
    }
    public static Task Check(Player p,PlayerChoiceContext ctx)=>Refresh(p,ctx);
    static void UpdateFlags(Player p)
    {
        var s=Util.State(p)!;int before=s.AgendaFlags;s.AgendaFlags=Calculate(p);
        for(int i=0;i<7;i++)if((before&(1<<i))==0&&(s.AgendaFlags&(1<<i))!=0)
            ArtuFeedback.Show(p,$"革命议程完成：{AgendaDetails.Names[i]}。{AgendaDetails.Rewards[i]}");
    }
}
public static class AgendaUi
{
    public static Task Show(Player p,int flags,PlayerChoiceContext ctx)=>Util.Choose(p,[AgendaStatus.Create(p,flags)],false,ctx);
}
public sealed class AgendaStatus:OptionCard
{
    public AgendaStatus():base("革命议程","查看进度。"){}
    public static AgendaStatus Create(Player p,int flags)
    {
        var result=(AgendaStatus)ModelDb.Card<AgendaStatus>().ToMutable();
        var s=Util.State(p)!;var labels=new[]{"摆脱皇命 I：苏丹牌≤4","摆脱皇命 II：苏丹牌=0","保持清白：罪证0、杂兵≥10","现实力量：三高级兵种各≥5","理论储备：读物≥10次、≥3种","清流交汇：同战其他思潮≥3种","建立苗圃：真实转化杂兵≥15"};
        ((StringVar)result.DynamicVars["Status"]).StringValue=string.Join('\n',labels.Select((label,i)=>$"{((flags&(1<<i))!=0?"✓":"○")} {label}"))+$"\n读物：{s.ReadingUses}次，{s.ReadingNames.Split(';',StringSplitOptions.RemoveEmptyEntries).Distinct().Count()}种；转化：{s.LeviesConverted}层。";
        return result;
    }
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new StringVar("Status","")];
    public override List<(string,string)> Localization=>new CardLoc("革命议程","{Status}");
}
public static class RevolutionRewards
{
    public static CardModel[] Pick(Player p,IEnumerable<ArtuCard> candidates,int count=3)
    {
        var pool=candidates.DistinctBy(c=>c.Id).ToList();var chosen=new List<CardModel>();
        while(pool.Count>0&&chosen.Count<count){var card=p.PlayerRng.Rewards.NextItem(pool)!;pool.Remove(card);chosen.Add(p.RunState.CreateCard(card,p));}return chosen.ToArray();
    }
    public static async Task Offer(Player p,IEnumerable<ArtuCard> candidates,string source)
    {
        var cards=Pick(p,candidates);if(cards.Length==0)return;
        var options=new CardCreationOptions([],CardCreationSource.Other,CardRarityOddsType.Uniform);
        var reward=new RevolutionCardReward(cards,p,options,source);
        GD.Print($"[Artu reading reward] source={source} candidates=[{string.Join(',',cards.Select(c=>c.Id))}]");
        await RewardsCmd.OfferCustom(p,[reward]);
    }
}
public sealed class RevolutionCardReward(IEnumerable<CardModel> cards,Player p,CardCreationOptions options,string source):CardReward(cards,CardCreationSource.Other,p,options)
{
    public override LocString Description=>EventText.Loc("ARTU_REWARD_"+source,source+"：选择一张牌。");
    public override void Populate(){} // Keep the explicit legal choices; don't let generic pool modifiers inject story cards.
}
public static class RevolutionEnding
{
    sealed class EndState{public bool Finalized;}
    static readonly ConditionalWeakTable<RunState,EndState> Ended=new();
    public static bool Finished(RunState state)=>Ended.TryGetValue(state,out var end)&&end.Finalized;
    public static bool IsWon(IRunState state)=>state.Players.Any(p=>Util.State(p)?.RevolutionWon==true);
    public static Task Win(Player player)
    {
        if(player.RunState is not RunState state||Ended.TryGetValue(state,out _))return Task.CompletedTask;
        var end=new EndState();Ended.Add(state,end);foreach(var p in state.Players)if(Util.State(p) is {} s)s.RevolutionWon=true;
        var manager=RunManager.Instance;
        manager.OnEnded(true);end.Finalized=true; // Native victory statistics, persisted run history, and deletion of the active run.
        var turn=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance);
        if(turn!=null)AccessTools.Method(turn.GetType(),"Cancel").Invoke(turn,null);
        Show();GD.Print("[Artu revolution] victory=true reason=革命成功 allPeersExecuteSynchronizedCard=true");
        return Task.CompletedTask;
    }
    static void Show()
    {
        if(NGame.Instance==null)return;
        var layer=new CanvasLayer{Layer=100};var back=new ColorRect{Color=new Color("111323")};back.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);layer.AddChild(back);
        var column=new VBoxContainer{OffsetLeft=100,OffsetTop=140,OffsetRight=1180,OffsetBottom=650};back.AddChild(column);
        var title=new Label{Text="革命成功 / 胜利！",HorizontalAlignment=HorizontalAlignment.Center};title.AddThemeFontSizeOverride("font_size",44);column.AddChild(title);
        var body=new Label{Text="皇命的锁链终于断裂。\n圆桌旁不再只有一个声音。\n你们带着知识与军队，走向自己决定的世界。\n\n本局已以“革命成功”记录为胜利。",HorizontalAlignment=HorizontalAlignment.Center};body.AddThemeFontSizeOverride("font_size",25);column.AddChild(body);
        var button=new Button{Text="返回主菜单",CustomMinimumSize=new Vector2(320,60)};column.AddChild(button);
        button.Pressed+=()=>MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(NGame.Instance.ReturnToMainMenuAfterRun());
        NGame.Instance.AddChild(layer);button.Pressed+=layer.QueueFree;
    }
}
[HarmonyPatch(typeof(RunState),nameof(RunState.IsGameOver),MethodType.Getter)]
static class RevolutionGameOver {static void Postfix(RunState __instance,ref bool __result){if(RevolutionEnding.Finished(__instance))__result=true;}}
// A native serializable badge makes the victory reason visible in run history, including teammates.
public sealed class RevolutionSuccessBadge():CustomBadge(true,false)
{
    public override string Id=>"ARTUMOD_REVOLUTION_SUCCESS";
    public override string CustomBadgeIconPath=>"res://ArtuMod/images/witchcardbox.tres";
    public override BadgeRarity Rarity(SerializableRun run,SerializablePlayer player)=>BadgeRarity.Gold;
    public override bool IsObtained(SerializableRun run,SerializablePlayer player)=>run.Players.Any(p=>p.Relics.Any(r=>RelicModel.FromSerializable(r) is WitchCardBox {RevolutionWon:true}));
}
