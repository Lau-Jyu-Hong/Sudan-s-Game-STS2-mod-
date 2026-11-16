using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
namespace ArtuMod;

public static class SultanUsage
{
    sealed class Data {public readonly HashSet<ModelId> Used=[];public readonly List<(int turn,CardPlay play)> Plays=[];}
    static readonly ConditionalWeakTable<Player,Data> DataByPlayer=new();
    public static bool ManualSultanUsedThisTurn(Player p)=>DataByPlayer.GetOrCreateValue(p).Plays.Any(x=>x.turn==p.PlayerCombatState?.TurnNumber&&x.play.Card is SultanCard&&!x.play.IsAutoPlay);
    public static int NonSultanThisTurn(Player p)=>DataByPlayer.GetOrCreateValue(p).Plays.Count(x=>x.turn==p.PlayerCombatState?.TurnNumber&&x.play.Card is not SultanCard);
    public static void Reset(Player player)=>DataByPlayer.Remove(player);
    public static IReadOnlySet<ModelId> UsedKinds(Player player)=>DataByPlayer.GetOrCreateValue(player).Used;
    public static bool SultanUsedThisTurn(Player player)=>DataByPlayer.GetOrCreateValue(player).Plays.Any(p=>p.turn==player.PlayerCombatState?.TurnNumber&&p.play.Card is SultanCard);
    public static bool FirstThisTurn(CardPlay play,Func<CardModel,bool> filter)
        =>DataByPlayer.GetOrCreateValue(play.Player).Plays.FirstOrDefault(p=>p.turn==play.Player.PlayerCombatState?.TurnNumber&&filter(p.play.Card)).play==play;
    public static bool SultanBefore(CardPlay play)
        =>DataByPlayer.GetOrCreateValue(play.Player).Plays.TakeWhile(p=>p.play!=play).Any(p=>p.turn==play.Player.PlayerCombatState?.TurnNumber&&p.play.Card is SultanCard);
    public static void Record(CardPlay play)
    {
        var data=DataByPlayer.GetOrCreateValue(play.Player);
        data.Plays.Add((play.Player.PlayerCombatState?.TurnNumber??0,play));
        if(play.Card is SultanCard)data.Used.Add(play.Card.Id);
    }
    public static CardModel? PreviousAttack(Player player,CardModel current)
        =>DataByPlayer.GetOrCreateValue(player).Plays.AsEnumerable().Reverse()
            .Where(p=>p.turn==player.PlayerCombatState?.TurnNumber&&p.play.Card!=current&&UsageType(p.play.Card)==CardType.Attack)
            .Select(p=>p.play.Card).FirstOrDefault();
    public static CardType UsageType(CardModel card)=>card is SultanCard s?(s.Suit==SultanSuit.Slaughter?CardType.Attack:CardType.Skill):card.Type;
}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeCardPlayed))]
static class RecordSultanUses
{
    static void Prefix(CardPlay cardPlay)=>SultanUsage.Record(cardPlay);
}

// Only replace Attack/Skill comparisons made through a CardPlay (including native history
// predicates), or in play-count hooks. The CardModel.Type getter remains untouched.
// Curse comparisons and all card searches, cost rules, upgrades and transformations stay native.
[HarmonyPatch]
static class NativeSultanUsageTypes
{
    static readonly MethodInfo CardTypeGetter=AccessTools.PropertyGetter(typeof(CardModel),nameof(CardModel.Type));
    static readonly MethodInfo GetPlayedCard=AccessTools.PropertyGetter(typeof(CardPlay),nameof(CardPlay.Card));
    static readonly MethodInfo VirtualType=AccessTools.Method(typeof(SultanUsage),nameof(SultanUsage.UsageType));
    public static int PatchedMethods {get;private set;}
    static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(var type in typeof(CardModel).Assembly.GetTypes().Where(t=>t.Namespace?.StartsWith("MegaCrit.Sts2.Core.Models")==true))
        foreach(var method in type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
        {
            if(method.IsAbstract||method.ContainsGenericParameters||method.GetMethodBody()==null)continue;
            List<CodeInstruction> instructions;
            try{instructions=PatchProcessor.GetOriginalInstructions(method);}catch{continue;}
            if(instructions.Select((i,n)=>(i,n)).Any(x=>ReplaceHere(instructions,x.n,method)))
            {PatchedMethods++;yield return method;}
        }
    }
    static bool ReplaceHere(List<CodeInstruction> code,int i,MethodBase method)
    {
        if(!code[i].Calls(CardTypeGetter)||i==0||i+1>=code.Count)return false;
        var next=code[i+1];
        bool attackOrSkill=next.opcode==OpCodes.Ldc_I4_1||next.opcode==OpCodes.Ldc_I4_2;
        if(!attackOrSkill)return false;
        bool playCount=method.Name=="ModifyCardPlayCount"||method.DeclaringType?.Name.StartsWith("<ModifyCardPlayCount>")==true;
        return code[i-1].Calls(GetPlayedCard)||playCount;
    }
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> original,MethodBase __originalMethod)
    {
        var code=original.ToList();
        for(int i=0;i<code.Count;i++)if(ReplaceHere(code,i,__originalMethod)){code[i].opcode=OpCodes.Call;code[i].operand=VirtualType;}
        return code;
    }
}
[HarmonyPatch(typeof(CardModel),nameof(CardModel.CanPlayTargeting))]
static class SultanTargetPayment
{
    static void Postfix(CardModel __instance,Creature? target,ref bool __result)
    {
        if(!__result||__instance is not SultanCard card||BeckyController.Active(card.Owner)||Util.Tyranny(card.Owner)||
            card.Owner.Creature.HasPower<PassPower>()||SultanPayments.HasReplacement(card)||V04Replay.Free(card)||card.Owner.Creature.GetPower<LifeScepterPower>()?.Available==true)return;
        __result=card.CanPayTarget(target);
    }
}
public static class SultanPayments
{
    sealed class Replacement;
    static readonly AsyncLocal<CardModel?> ExtraReplay=new();
    sealed class ReplayScope(CardModel card):IDisposable
    {
        readonly CardModel? previous=ExtraReplay.Value;
        public void Enter()=>ExtraReplay.Value=card;
        public void Dispose()=>ExtraReplay.Value=previous;
    }
    public static IDisposable AddReplay(CardModel card){var scope=new ReplayScope(card);scope.Enter();return scope;}
    public static bool HasExtraReplay(CardModel card)=>ExtraReplay.Value==card;
    static readonly ConditionalWeakTable<CardModel,Replacement> Replacements=new();
    public static void ReplaceNextCost(SultanCard card){Replacements.Remove(card);Replacements.Add(card,new());}
    public static bool HasReplacement(SultanCard card)=>Replacements.TryGetValue(card,out _);
    public static bool ConsumeReplacement(SultanCard card)=>Replacements.Remove(card);
    public static void LogFailure(PlayerChoiceContext ctx,CardPlay play,SultanCard source,int before,int after)
    {
        var hand=play.Player.PlayerCombatState?.Hand.Cards;
        var reason=source.Suit switch{SultanSuit.Slaughter=>"insufficient HP",SultanSuit.Conquest when source.Grade==SultanGrade.Stone=>"no block",SultanSuit.Conquest when source.Grade==SultanGrade.Gold=>"target cannot gain full 50 block",SultanSuit.Luxury when source.Grade==SultanGrade.Bronze=>"no other Sultan card in hand",SultanSuit.Luxury=>"insufficient gold",_=>"no valid living target"};
        GD.Print($"[Artu cost failure] source={source.Id} played={play.Card.Id} reason={reason} replay={play.PlayIndex}/{play.PlayCount} mockery={before}->{after} hp={play.Player.Creature.CurrentHp} gold={play.Player.Gold} block={play.Player.Creature.Block} hand=[{string.Join(",",hand?.Select(c=>c.Id.ToString())??[])}] parent=[{string.Join(",",ctx.ModelStack?.Select(m=>m.Id.ToString())??[])}]");
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.ModifyCardPlayCount))]
static class ExplicitSultanReplay
{
    static void Postfix(CardModel card,ref int __result){if(SultanPayments.HasExtraReplay(card))__result++;}
}
[HarmonyPatch(typeof(CardModel),nameof(CardModel.IsTransformable),MethodType.Getter)]
static class ProtectSultanTransformations
{
    static void Postfix(CardModel __instance,ref bool __result)
    {if(__instance is SultanCard)__result=false;}
}
public static class EffectCopy
{
    sealed class Source { public required CardModel Card; }
    static readonly ConditionalWeakTable<CardModel,Source> CopiedFrom=new();
    static CardModel? ResolveSource(Player player,CardModel current)
    {
        var source=SultanUsage.PreviousAttack(player,current);
        var visited=new HashSet<CardModel>{current};
        while(source is FollowFootsteps)
        {
            if(!visited.Add(source)||!CopiedFrom.TryGetValue(source,out var data))return null;
            source=data.Card;
        }
        return source;
    }
    public static bool CanExecute(Player player,CardModel current)
    {var source=ResolveSource(player,current);return source!=null&&(source is not SultanCard s||s.CanPayAny());}
    public static async Task Execute(PlayerChoiceContext ctx,CardPlay play)
    {
        var source=ResolveSource(play.Player,play.Card);
        if(source==null)return;
        CopiedFrom.Remove(play.Card);CopiedFrom.Add(play.Card,new(){Card=source});
        if(source is ArtuCard artu)await artu.ExecuteCopiedEffect(ctx,play);
        else if(AccessTools.Method(source.GetType(),"OnPlay").Invoke(source,[ctx,play]) is Task task)await task;
    }
}
