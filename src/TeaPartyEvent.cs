using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Events;
using System.Runtime.CompilerServices;
namespace ArtuMod;

public static class TeaPartyChallenge
{
    static readonly ConditionalWeakTable<IRunState,TeaChallengeConsent> Consents=new();
    static readonly ConditionalWeakTable<CombatRoom,RewardMarker> Rewarded=new();
    sealed class RewardMarker { public bool Granted; }
    public static void ResetConsent(IRunState run)=>Consents.Remove(run);
    public static bool HasArtu(EventModel model) => (model.Owner?.RunState??RunManager.Instance.DebugOnlyGetState())?.Players.Any(p=>p.Character is Artu)==true;
    public static bool Eligible(CardModel card) => card is SultanCard { Suit:SultanSuit.Slaughter, Grade:>=SultanGrade.Bronze };
    public static IReadOnlyList<EventOption> AddOption(EventModel model,IReadOnlyList<EventOption> original)
    {
        if(model is not RoundTeaParty||!HasArtu(model)||model.Owner is not {} owner)return original;
        var payer=owner.RunState.Players.Where(p=>p.Character is Artu&&p.Deck.Cards.Any(Eligible)).OrderBy(p=>p.NetId).FirstOrDefault();
        if(payer==null)return original;
        var consent=Consents.GetValue(owner.RunState,r=>new TeaChallengeConsent(r.Players.Select(p=>p.NetId)));
        // Capture the same payer for every player's event before any instance removes the cost.
        var option=EventText.Option(model,"ARTU_TEA_CHALLENGE","你们也配强迫我选择？",
            "由阿尔图永久移除1张青铜或以上杀戮苏丹牌，全队共同挑战王冠骑士、军阀与佣兵队长。胜利后每人获得2件随机遗物并回复20生命。",
            async ()=>{
                if(owner.RunState.Players.Count>1)
                {
                    var answer=await Util.Choose(owner,[ModelDb.Card<TeaJoinOption>(),ModelDb.Card<TeaDeclineOption>()]);
                    if(!await consent.Vote(owner.NetId,answer is TeaJoinOption))
                    {
                        EventText.Finish(model,"ARTU_TEA_DECLINED","有人不愿为这张皇命冒险。你收回手，圆桌上的茶依然温热。" );
                        return;
                    }
                }
                if(owner==payer)
                {
                    var card=await SultanEventOptions.SelectSultan(payer,SultanSuit.Slaughter,c=>c.Grade>=SultanGrade.Bronze);
                    if(card==null)throw new InvalidOperationException("Tea party challenge cost selection was not completed.");
                    await Util.RemoveSultan(card);
                }
                var enter=AccessTools.Method(typeof(EventModel),"EnterCombatWithoutExitingEvent",[typeof(EncounterModel),typeof(IReadOnlyList<MegaCrit.Sts2.Core.Rewards.Reward>),typeof(bool)]);
                enter.Invoke(model,[ModelDb.Encounter<TeaPartyEncounter>(),Array.Empty<MegaCrit.Sts2.Core.Rewards.Reward>(),false]);
            });
        return [..original,option];
    }
    public static async Task GrantVictoryRewards(CombatRoom room)
    {
        if(room.Encounter is not TeaPartyEncounter)return;
        var marker=Rewarded.GetOrCreateValue(room);
        if(marker.Granted)return;
        marker.Granted=true;
        foreach(var player in room.CombatState.Players)
        {
            for(int i=0;i<2;i++)await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(player).ToMutable(),player);
            await CreatureCmd.Heal(player.Creature,20);
        }
    }
}

public sealed class TeaChallengeConsent
{
    readonly HashSet<ulong> participants;
    readonly Dictionary<ulong,bool> votes=[];
    readonly TaskCompletionSource<bool> result=new();
    public TeaChallengeConsent(IEnumerable<ulong> players)=>participants=players.ToHashSet();
    public Task<bool> Vote(ulong player,bool agree)
    {
        if(!participants.Contains(player))throw new InvalidOperationException("Unknown tea-party participant.");
        votes[player]=agree;
        if(votes.Count==participants.Count)result.TrySetResult(votes.Values.All(v=>v));
        return result.Task;
    }
}
public sealed class TeaJoinOption():OptionCard("同意参战","全队共同挑战三名敌人；胜利后每人获得2件遗物并回复20生命。");
public sealed class TeaDeclineOption():OptionCard("不参战","全队离席，不移除任何苏丹牌，不获得战斗奖励。");

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer),"BeginEvent")]
static class ResetTeaPartyConsent
{
    static void Prefix(EventModel canonicalEvent)
    {if(canonicalEvent is RoundTeaParty&&RunManager.Instance.DebugOnlyGetState() is {} run)TeaPartyChallenge.ResetConsent(run);}
}

[HarmonyPatch(typeof(EventModel),nameof(EventModel.IsShared),MethodType.Getter)]
static class SharedTeaParty
{
    static void Postfix(EventModel __instance,ref bool __result)
    {if(__instance is RoundTeaParty&&TeaPartyChallenge.HasArtu(__instance))__result=true;}
}
[HarmonyPatch(typeof(EventModel),nameof(EventModel.CanonicalEncounter),MethodType.Getter)]
static class TeaPartyEncounterDeclaration
{
    static void Postfix(EventModel __instance,ref EncounterModel? __result)
    {if(__instance is RoundTeaParty&&TeaPartyChallenge.HasArtu(__instance))__result=ModelDb.Encounter<TeaPartyEncounter>();}
}
[HarmonyPatch(typeof(EventModel),"GenerateInitialOptionsWrapper")]
static class AddTeaPartyChallenge
{
    [HarmonyPostfix,HarmonyPriority(Priority.Last)]
    static void Postfix(EventModel __instance,ref IReadOnlyList<EventOption> __result)
        =>__result=TeaPartyChallenge.AddOption(__instance,__result);
}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterCombatVictory))]
static class TeaPartyVictoryRewards
{
    static void Postfix(CombatRoom room,ref Task __result)=>__result=After(__result,room);
    static async Task After(Task original,CombatRoom room){await original;await TeaPartyChallenge.GrantVictoryRewards(room);}
}
