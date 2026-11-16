using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

public static class ArmyInfo
{
    public static string Name(int kind)=>new[]{"杂兵","士兵","精兵","锐士"}[Math.Clamp(kind,0,3)];
}
[HarmonyPatch(typeof(CardPileCmd),nameof(CardPileCmd.RemoveFromDeck),[typeof(IReadOnlyList<CardModel>),typeof(bool)])]
static class AgendaAfterDeckRemoval
{
    static void Prefix(IReadOnlyList<CardModel> cards,out Player[] __state)=>__state=cards.Select(c=>c.Owner).Where(p=>Util.State(p)!=null).Distinct().ToArray();
    static void Postfix(Player[] __state,ref Task __result)=>__result=RefreshAfter(__result,__state);
    static async Task RefreshAfter(Task task,Player[] players)
    {await task;foreach(var p in players)await RevolutionAgenda.Refresh(p,new BlockingPlayerChoiceContext());}
}
public sealed class ArmyMight():RewardCard(3,CardType.Power,CardRarity.Rare,TargetType.Self,"军威如山","本场战斗，每回合前{Hits}次兵种造成的伤害，受到你的力量及目标的易伤影响。所有兵种共享额度，多张能力的额度相加。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Hits",8)];
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play)=>AddPower<ArmyMightPower>(ctx,DynamicVars["Hits"].IntValue);
    protected override void OnUpgrade()=>DynamicVars["Hits"].UpgradeValueBy(4);
}
public sealed class ArmyMightPower():ArtuPower("军威如山","每回合前{Amount}次兵种伤害受力量和目标易伤影响。所有兵种、额外结算共享额度；多张额度相加。其余属性与兵种格挡规则不变。")
{
    int turn=-1,used;
    protected override void DeepCloneFields(){base.DeepCloneFields();turn=-1;used=0;}
    public int Remaining=>Math.Max(0,Amount-(turn==Owner.Player?.PlayerCombatState?.TurnNumber?used:0));
    public bool ConsumeHit()
    {
        int now=Owner.Player?.PlayerCombatState?.TurnNumber??0;
        if(turn!=now){turn=now;used=0;}if(used>=Amount)return false;used++;return true;
    }
    protected override IEnumerable<IHoverTip> ExtraHoverTips=>base.ExtraHoverTips.Concat(IsMutable?[new HoverTip(EventText.Loc("ARTU_MIGHT_REMAINING","军威额度"),$"本回合剩余 {Remaining}/{Amount} 次。每个受击敌人计一次，含被格挡或降为0的命中。")]:[]);
    public static async Task Deal(PlayerChoiceContext ctx,Player p,Creature target,int damage)
    {
        decimal value=damage;
        if(p.Creature.GetPower<ArmyMightPower>() is {} might&&might.ConsumeHit())
        {
            value=Math.Max(0,value+p.Creature.GetPowerAmount<StrengthPower>());
            if(target.GetPower<VulnerablePower>() is {} vulnerable)
                value*=vulnerable.ModifyDamageMultiplicative(target,value,ValueProp.Move,p.Creature,null,null);
        }
        // Keep block and native unpowered defenses, without re-applying Strength, Weak or Vigor.
        await CreatureCmd.Damage(ctx,target,value,ValueProp.Unpowered,p.Creature);
    }
}

public static class AgendaDetails
{
    public static readonly string[] Names=["摆脱皇命 I","摆脱皇命 II","保持清白","建立现实力量","理论储备","清流交汇","建立苗圃"];
    public static readonly string[] Conditions=["永久卡组苏丹牌≤4","永久卡组苏丹牌=0","罪证=0且当前杂兵≥10","士兵、精兵、锐士同时各≥5","本局其他读物使用≥10次，且≥3种名称","同场使用≥3种其他思潮，无需获胜","本局累计真实转化15层杂兵为高级兵种"];
    public static readonly string[] Rewards=["字条生效的战斗胜利后，可永久移除1张苏丹牌（可拒绝）。","字条生效后，本场升级全部可升级牌。","字条生效后，获得10杂兵。","字条生效后，三种高级兵种各+2。","字条生效后，从抽/弃牌堆选1读物入手；战后额外读物奖励。","字条生效后，每回合首次其他思潮获得1士兵、抽1张。","字条生效后，每回合开始转化至多2杂兵为随机高级兵种。"];
    public static string Entry(Player? p,int i)
    {
        var s=p==null?null:Util.State(p);bool done=s!=null&&(s.AgendaFlags&(1<<i))!=0;
        string progress="尚未开始";
        if(p!=null&&s!=null)
        {
            bool battle=p.PlayerCombatState!=null&&CombatManager.Instance.IsInProgress;
            progress=i switch{
                0 or 1=>$"苏丹牌 {p.Deck.Cards.OfType<SultanCard>().Count()} 张",
                2=>$"罪证 {V04.Crime(p)}；杂兵 {(battle?p.Creature.GetPowerAmount<LevyPower>():0)}/10",
                3=>$"士兵 {(battle?p.Creature.GetPowerAmount<SoldierPower>():0)}/5，精兵 {(battle?p.Creature.GetPowerAmount<VeteranPower>():0)}/5，锐士 {(battle?p.Creature.GetPowerAmount<ElitePower>():0)}/5",
                4=>$"使用 {s.ReadingUses}/10 次；{s.ReadingNames.Split(';',StringSplitOptions.RemoveEmptyEntries).Distinct().Count()}/3 种",
                5=>$"本场 {(battle?V04.Data(p).Thoughts.Count:0)}/3 种",
                _=>$"真实转化 {s.LeviesConverted}/15 层"};
        }
        return $"{(done?"✓ 已完成":"○ 未完成")} {Names[i]}\n条件：{Conditions[i]}\n进度：{progress}\n强化：{Rewards[i]}";
    }
    public static IEnumerable<IHoverTip> Tips(Player? p)
    {
        for(int group=0;group<3;group++)
        {int start=group*3;yield return new HoverTip(EventText.Loc("ARTU_AGENDA_"+group,$"革命议程 {group+1}/3"),string.Join("\n\n",Enumerable.Range(start,Math.Min(3,7-start)).Select(i=>Entry(p,i))));}
    }
    public static void Show(Player p)
    {
        if(NRun.Instance is not {} run||run.HasNode("ArtuAgendaDetails"))return;
        var overlay=new Panel{Name="ArtuAgendaDetails",ZIndex=100,MouseFilter=Control.MouseFilterEnum.Stop};
        run.AddChild(overlay);var size=run.GetViewportRect().Size;
        overlay.Position=new Vector2(size.X*.18f,size.Y*.12f);overlay.Size=new Vector2(size.X*.64f,size.Y*.76f);
        var column=new VBoxContainer();overlay.AddChild(column);column.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("18202bf5"),BorderColor=new Color("b69b62"),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2});
        var heading=new Label{Text="革命议程 · 已完成任务永久保留；全部完成后获得一次革命的构思",AutowrapMode=TextServer.AutowrapMode.WordSmart};heading.AddThemeFontSizeOverride("font_size",22);column.AddChild(heading);
        var scroll=new ScrollContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};column.AddChild(scroll);
        var body=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};body.AddThemeFontSizeOverride("font_size",21);scroll.AddChild(body);
        void Refresh()=>body.Text=string.Join("\n\n",Enumerable.Range(0,7).Select(i=>Entry(p,i)));
        Refresh();var timer=new Godot.Timer{WaitTime=.3,Autostart=true};timer.Timeout+=Refresh;overlay.AddChild(timer);
        var close=new Button{Text="关闭",CustomMinimumSize=new Vector2(0,45)};close.Pressed+=overlay.QueueFree;column.AddChild(close);
    }
}

public static class ArtuFeedback
{
    public static void Show(Player p,string text)
    {
        GD.Print($"[Artu feedback] player={p.NetId} {text}");
        if(TestMode.IsOn||NRun.Instance is not {} run||!LocalContext.IsMe(p))return;
        try
        {
            var column=run.GetNodeOrNull<VBoxContainer>("ArtuNotices");
            if(column==null){column=new VBoxContainer{Name="ArtuNotices",ZIndex=50,MouseFilter=Control.MouseFilterEnum.Ignore,Position=new Vector2(420,145),Size=new Vector2(850,0)};run.AddChild(column);}
            var label=new Label{Text=text,AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=Control.MouseFilterEnum.Ignore};
            label.AddThemeFontSizeOverride("font_size",23);label.AddThemeColorOverride("font_color",new Color("ffd18a"));label.AddThemeColorOverride("font_outline_color",Colors.Black);label.AddThemeConstantOverride("outline_size",5);column.AddChild(label);
            var timer=new Godot.Timer{WaitTime=8,OneShot=true,Autostart=true};timer.Timeout+=label.QueueFree;label.AddChild(timer);
        }catch(Exception e){GD.PrintErr("[Artu feedback UI] "+e.Message);}
    }
}

public static class DeadlineWarning
{
    public static bool Urgent(SultanCard card)
    {
        if(!card.IsMutable||card.RunState==null||V04.Data(card.Owner).Grace)return false;
        var original=card.Original;bool battle=CombatManager.Instance.IsInProgress&&card.Owner.PlayerCombatState!=null;
        return original.BattlesRemaining<=1&&(!battle||!original.UsedThisBattle)
            ||card.Grade==SultanGrade.Gold&&Rules.GoldTurnDeadline-original.TurnsSinceUse<=1&&(!battle||!original.UsedThisTurn);
    }
}
[HarmonyPatch(typeof(NCard),nameof(NCard.UpdateVisuals))]
static class DeadlineBorderPatch
{
    static void Postfix(NCard __instance)
    {
        if(__instance.Body==null)return;
        var border=__instance.Body.GetNodeOrNull<Panel>("ArtuDeadlineBorder");
        bool urgent=__instance.Model is SultanCard s&&DeadlineWarning.Urgent(s);
        if(!urgent){if(border!=null)border.Visible=false;return;}
        if(border==null)
        {
            border=new Panel{Name="ArtuDeadlineBorder",MouseFilter=Control.MouseFilterEnum.Ignore};
            var style=new StyleBoxFlat{BgColor=new Color(0,0,0,0),BorderColor=new Color("ff3030"),BorderWidthLeft=5,BorderWidthRight=5,BorderWidthTop=5,BorderWidthBottom=5,CornerRadiusTopLeft=16,CornerRadiusTopRight=16,CornerRadiusBottomLeft=16,CornerRadiusBottomRight=16};
            border.AddThemeStyleboxOverride("panel",style);__instance.Body.AddChild(border);
        }
        // CardContainer is a zero-size transform origin; anchoring to it creates an invisible 0x0 border.
        var frame=__instance.GetNodeOrNull<Control>("%Frame");
        border.Position=(frame?.Position??-NCard.defaultSize/2)-new Vector2(3,3);
        border.Size=(frame?.Size??NCard.defaultSize)+new Vector2(6,6);
        border.Visible=true;
    }
}
