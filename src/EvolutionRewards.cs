using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

// Use the native linked reward widget: two vertically stacked, mutually exclusive buttons.
// Qualification lives on the relic, not on a transient combat power or a reward instance.
public static class EvolutionRewards
{
    [CustomEnum] public static RewardType EvolutionSetType;
    [CustomEnum] public static RewardType EvolutionChoiceType;
    [CustomEnum] public static RewardType ForgivenessType;
    static readonly Type[] Sources=[typeof(PureThought),typeof(UncleanThought),typeof(FearOfSultan),typeof(TouchPurity),typeof(GlimpseDarkness),typeof(FreedomFromFear),typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent)];
    static readonly Type[] Results=[typeof(TouchPurity),typeof(GlimpseDarkness),typeof(FreedomFromFear),typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent),typeof(PerfectWorld),typeof(NewWorld),typeof(RegicidePlan)];
    public static CardModel Source(int stage)=>ModelDb.GetById<CardModel>(ModelDb.GetId(Sources[stage]));
    public static CardModel Result(int stage)=>ModelDb.GetById<CardModel>(ModelDb.GetId(Results[stage]));
    public static bool Qualified(WitchCardBox s,int stage)=>(s.PendingEvolutions&(1<<stage))!=0;
    public static void Qualify(WitchCardBox s,int stage)=>s.PendingEvolutions|=1<<stage;
    public static int Next(WitchCardBox s)=>Enumerable.Range(1,9).Select(step=>(s.LastOfferedEvolutionStage+step+9)%9).FirstOrDefault(i=>Qualified(s,i),-1);
    public static bool CanForgive(WitchCardBox s)=>s.BattleWins>0&&s.BattleWins%4==0&&s.Anger>0&&s.ForgivenessClaimedVictory!=s.BattleWins;
    public static bool Add(Player p,List<Reward> rewards)
    {
        var s=Util.State(p);if(s==null||s.BattleWins<=0||p.Creature.IsDead)return false;bool changed=false;
        int stage=Next(s);
        if(stage>=0&&s.EvolutionResolvedVictory!=s.BattleWins&&!rewards.Any(r=>r is ThoughtEvolutionSet))
        {rewards.Add(new ThoughtEvolutionSet(p,stage,s.BattleWins));changed=true;}
        if(CanForgive(s)&&!rewards.Any(r=>r is ForgivenessReward))
        {rewards.Add(new ForgivenessReward(p,s.BattleWins));changed=true;}
        return changed;
    }
    public static async Task<bool> Decide(Player p,int stage,int victory,bool evolve)
    {
        var s=Util.State(p);
        if(s==null||s.BattleWins!=victory||s.EvolutionResolvedVictory==victory||!Qualified(s,stage))return false;
        s.EvolutionResolvedVictory=victory;
        s.LastOfferedEvolutionStage=stage;
        if(!evolve){GD.Print($"[Artu evolution retained] stage={stage} victory={victory}");return true;}
        // Consume qualification before any selectable bonus, so replay/stale reward cannot pay it twice.
        s.PendingEvolutions&=~(1<<stage);
        await Thoughts.RemoveSame(p,Source(stage));await Util.AddToDeck(p,Result(stage));
        await Thoughts.EvolutionBonus(p,stage,new BlockingPlayerChoiceContext());
        GD.Print($"[Artu evolution accepted] stage={stage} victory={victory}");return true;
    }
    public static Task<bool> Forgive(Player p,int victory)
    {
        var s=Util.State(p);if(s==null||s.BattleWins!=victory||!CanForgive(s))return Task.FromResult(false);
        s.ForgivenessClaimedVictory=victory;s.Anger--;GD.Print($"[Artu forgiveness] victory={victory} anger={s.Anger}");return Task.FromResult(true);
    }
    public static Control? Icon(string name)
    {if(TestMode.IsOn)return null;return new TextureRect{Texture=ResourceLoader.Load<Texture2D>("res://ArtuMod/images/status/"+name+".tres"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,CustomMinimumSize=new Vector2(48,48)};}
}
public sealed class ThoughtEvolutionSet(Player player,int stage,int victory):LinkedRewardSet(
    [new ThoughtEvolutionChoice(player,stage,victory,false),new ThoughtEvolutionChoice(player,stage,victory,true)],player)
{
    public int Stage {get;}=stage;public int Victory {get;}=victory;
    protected override RewardType RewardType=>EvolutionRewards.EvolutionSetType;
    public override SerializableReward ToSerializable()=>new(){RewardType=RewardType,GoldAmount=Stage,OptionCount=Victory};
}

// v0.113.1 child reward signals now pass the claimed button. The native
// linked widget still wires a zero-argument callback and emits its own
// signal without the expected argument. Adapt only Artu's linked rewards.
[HarmonyPatch(typeof(NLinkedRewardSet),"Reload")]
static class EvolutionLinkedRewardSignal
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var fromAction=AccessTools.Method(typeof(Callable),nameof(Callable.From),[typeof(Action)]);
        var adapted=AccessTools.Method(typeof(EvolutionLinkedRewardSignal),nameof(Adapt));
        foreach(var instruction in instructions)
        {
            if(instruction.Calls(fromAction))instruction.operand=adapted;
            yield return instruction;
        }
    }

    static Callable Adapt(Action callback)
        =>callback.Target is NLinkedRewardSet {LinkedRewardSet:ThoughtEvolutionSet}
            ?Callable.From<NRewardButton>(_=>callback())
            :Callable.From(callback);
}

[HarmonyPatch(typeof(NLinkedRewardSet),"GetReward")]
static class EvolutionLinkedRewardCollected
{
    static bool Prefix(NLinkedRewardSet __instance,NRewardsScreen ____rewardsScreen)
    {
        if(__instance.LinkedRewardSet is not ThoughtEvolutionSet rewards)return true;
        ____rewardsScreen.RewardCollectedFrom(__instance);
        rewards.OnSkipped();
        __instance.QueueFree();
        return false;
    }
}
public sealed class ThoughtEvolutionChoice(Player player,int stage,int victory,bool evolve):Reward(player)
{
    protected override RewardType RewardType=>EvolutionRewards.EvolutionChoiceType;
    public override int RewardsSetIndex=>8;
    public override bool IsPopulated=>true;
    public override LocString Description
    {get{var loc=new LocString("gameplay_ui",evolve?"ARTU_EVOLVE":"ARTU_RETAIN");loc.Add("Thought",EvolutionRewards.Source(stage).Title);loc.Add("Next",EvolutionRewards.Result(stage).Title);return loc;}}
    public override void Populate(){}
    public override void MarkContentAsSeen(){}
    public override Control? CreateIcon()=>EvolutionRewards.Icon(stage%3==0?"purethoughtpower":stage%3==1?"uncleanthoughtpower":"fearthoughtpower");
    protected override Task<bool> OnSelect()=>EvolutionRewards.Decide(Player,stage,victory,evolve);
    public override SerializableReward ToSerializable()=>new(){RewardType=RewardType,GoldAmount=stage,OptionCount=victory,WasGoldStolenBack=evolve};
}
public sealed class ForgivenessReward(Player player,int victory):Reward(player)
{
    protected override RewardType RewardType=>EvolutionRewards.ForgivenessType;
    public override int RewardsSetIndex=>9;
    public override bool IsPopulated=>true;
    public override LocString Description=>new("gameplay_ui","ARTU_FORGIVE");
    public override void Populate(){}
    public override void MarkContentAsSeen(){}
    public override Control? CreateIcon()=>EvolutionRewards.Icon("stateanger");
    protected override Task<bool> OnSelect()=>EvolutionRewards.Forgive(Player,victory);
    public override SerializableReward ToSerializable()=>new(){RewardType=RewardType,OptionCount=victory};
}
[HarmonyPatch(typeof(Reward),nameof(Reward.FromSerializable))]
static class RestoreArtuRewards
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(SerializableReward save,Player player,ref Reward __result)
    {
        if(save.RewardType==EvolutionRewards.EvolutionSetType&&save.GoldAmount is >=0 and <9)__result=new ThoughtEvolutionSet(player,save.GoldAmount,save.OptionCount);
        else if(save.RewardType==EvolutionRewards.EvolutionChoiceType&&save.GoldAmount is >=0 and <9)__result=new ThoughtEvolutionChoice(player,save.GoldAmount,save.OptionCount,save.WasGoldStolenBack);
        else if(save.RewardType==EvolutionRewards.ForgivenessType)__result=new ForgivenessReward(player,save.OptionCount);
        else return true;
        return false;
    }
}
