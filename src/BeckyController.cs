using System.Threading;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

public static class BeckyController
{
    sealed record Session(Player Player,Rng Choices);
    static readonly AsyncLocal<Session?> Current=new();
    static readonly HashSet<Player> Running=[];
    static readonly HashSet<Player> Preparing=[];
    public static bool Active(Player player)=>Current.Value?.Player==player&&Running.Contains(player);
    public static bool TakeoverRunning(Player player)=>Running.Contains(player);
    public static async Task DrawAndRun(PlayerChoiceContext ctx,Player player)
    {
        if(Running.Contains(player)||!Preparing.Add(player))return;
        try{await CardPileCmd.Draw(ctx,4,player);if(!player.Creature.IsDead&&!CombatManager.Instance.IsOverOrEnding)await Run(ctx,player);}
        finally{Preparing.Remove(player);}
    }
    public static async Task Run(PlayerChoiceContext ctx,Player player)
    {
        if(Active(player)){GD.Print("[Artu Becky] Nested takeover ignored; the current two scans continue.");return;}
        var previous=Current.Value;
        Current.Value=new(player,new Rng(player,ModelDb.Card<SuperBecky>().Id,(ulong)(player.PlayerCombatState?.TurnNumber??0)));
        Running.Add(player);
        try
        {
            await Scan(()=>player.PlayerCombatState?.Hand.Cards.Count??0,async index=>{
                if(player.Creature.IsDead||CombatManager.Instance.IsOverOrEnding)return;
                if(index>=player.PlayerCombatState!.Hand.Cards.Count)return;
                await BeckyPresentation.BeforeCard(player,player.PlayerCombatState.Hand.Cards[index]);
                if(player.Creature.IsDead||CombatManager.Instance.IsOverOrEnding||index>=player.PlayerCombatState!.Hand.Cards.Count)return;
                var card=player.PlayerCombatState!.Hand.Cards[index];
                GD.Print($"[Artu Becky] position={index+1} card={card.Id} hand=[{string.Join(",",player.PlayerCombatState.Hand.Cards.Select(c=>c.Id))}]");
                await ForcePlay(ctx,card);
            },()=>!player.Creature.IsDead&&!CombatManager.Instance.IsOverOrEnding);
            await BeckyPresentation.AfterScans(player);
        }
        finally{BeckyPresentation.Clear(player);Current.Value=previous;Running.Remove(player);}
        if(!player.Creature.IsDead&&CombatManager.Instance.IsInProgress&&V04.Data(player).BeckyEndQueuedTurn!=player.PlayerCombatState!.TurnNumber)
        {V04.Data(player).BeckyEndQueuedTurn=player.PlayerCombatState.TurnNumber;PlayerCmd.EndTurn(player,canBackOut:false);}
    }
    public static async Task Scan(Func<int> count,Func<int,Task> playAt,Func<bool>? keepGoing=null)
    {
        // Positions are reread after every card, including all nested effects. Never snapshot or renumber cards.
        int resolved=0;
        for(int start=0;start<=1;start++)for(int index=start;index<count()&&(keepGoing?.Invoke()??true);index+=2)
        {
            if(++resolved>1000)throw new InvalidOperationException("Becky scan stopped: a mod continuously grows the hand beyond the normal hand limit.");
            await playAt(index);
        }
    }
    public static async Task ForcePlay(PlayerChoiceContext ctx,CardModel card)
    {
        var player=card.Owner;Creature? target=null;
        if(card.TargetType==TargetType.AnyEnemy)target=Util.RandomEnemy(player);
        if(card.TargetType==TargetType.AnyAlly)target=player.RunState.Rng.CombatTargets.NextItem(player.Creature.CombatState!.PlayerCreatures.Where(c=>c!=player.Creature&&!c.IsDead));
        if(card.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly&&target==null)
        {await CardPileCmd.Add(card,PileType.Discard);return;}
        if(card.EnergyCost.CostsX)card.EnergyCost.CapturedXValue=player.PlayerCombatState!.Energy;
        card.LastStarsSpent=card.HasStarCostX?player.PlayerCombatState!.Stars:Math.Max(0,card.GetStarCostWithModifiers());
        var resources=new ResourceInfo{EnergySpent=0,EnergyValue=card.EnergyCost.GetAmountToSpend(),StarsSpent=0,StarValue=card.LastStarsSpent};
        await Hook.BeforeCardAutoPlayed(player.Creature.CombatState!,card,target,AutoPlayType.Default);
        // OnPlayWrapper is the normal effect/history/replay pipeline; restrictions and resource spending
        // are bypassed here, while each card's own OnPlay conditions remain authoritative.
        await card.OnPlayWrapper(ctx,target,true,resources);
    }
    public static async Task<IEnumerable<CardModel>> Select(PlayerChoiceContext ctx,Player player,IReadOnlyList<CardModel> cards,int minimum,int maximum)
    {
        if(cards.Count==0)return [];
        var indexes=Enumerable.Range(0,cards.Count).ToList();Current.Value!.Choices.Shuffle(indexes);
        var selected=indexes.Take(Math.Min(cards.Count,Math.Max(minimum,maximum))).ToList();
        if(TestMode.IsOn)return selected.Select(i=>cards[i]).ToArray();
        var sync=RunManager.Instance.PlayerChoiceSynchronizer;uint id=sync.ReserveChoiceId(player);
        await ctx.SignalPlayerChoiceBegun(player,PlayerChoiceOptions.None);
        try
        {
            // Every peer advances the dedicated RNG. Only the owning peer submits its choice;
            // other peers consume the game's synchronized indexes, without replacing the global selector.
            if(LocalContext.IsMe(player))sync.SyncLocalChoice(player,id,PlayerChoiceResult.FromIndexes(selected));
            else selected=(await sync.WaitForRemoteChoice(player,id)).AsIndexes().ToList();
            return selected.Select(i=>cards[i]).ToArray();
        }
        finally{await ctx.SignalPlayerChoiceEnded();}
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.ShouldPlay))]
static class BeckyBypassProhibitions
{
    static bool Prefix(CardModel card,ref AbstractModel? preventer,ref bool __result)
    {if(!BeckyController.Active(card.Owner))return true;preventer=null;__result=true;return false;}
}
[HarmonyPatch(typeof(CardModel),nameof(CardModel.CanPlay),new Type[]{})]
static class BeckyBlocksManualInput
{
    static bool Prefix(CardModel __instance,ref bool __result)
    {if(!BeckyController.TakeoverRunning(__instance.Owner))return true;__result=false;return false;}
}
[HarmonyPatch(typeof(CardSelectCmd),nameof(CardSelectCmd.FromSimpleGrid))]
static class BeckySelectGrid
{
    static bool Prefix(PlayerChoiceContext context,IReadOnlyList<CardModel> cardsIn,Player player,CardSelectorPrefs prefs,ref Task<IEnumerable<CardModel>> __result)
    {if(!BeckyController.Active(player))return true;__result=BeckyController.Select(context,player,cardsIn,prefs.MinSelect,prefs.MaxSelect);return false;}
}
[HarmonyPatch(typeof(CardSelectCmd),nameof(CardSelectCmd.FromHand))]
static class BeckySelectHand
{
    static bool Prefix(PlayerChoiceContext context,Player player,CardSelectorPrefs prefs,Func<CardModel,bool>? filter,ref Task<IEnumerable<CardModel>> __result)
    {if(!BeckyController.Active(player))return true;__result=BeckyController.Select(context,player,player.PlayerCombatState!.Hand.Cards.Where(filter??(_=>true)).ToArray(),prefs.MinSelect,prefs.MaxSelect);return false;}
}
[HarmonyPatch(typeof(CardSelectCmd),nameof(CardSelectCmd.FromCombatPile),[typeof(PlayerChoiceContext),typeof(CardPile),typeof(Player),typeof(CardSelectorPrefs),typeof(Func<CardModel,bool>)])]
static class BeckySelectPile
{
    static bool Prefix(PlayerChoiceContext context,CardPile pile,Player player,CardSelectorPrefs prefs,Func<CardModel,bool>? filter,ref Task<IEnumerable<CardModel>> __result)
    {if(!BeckyController.Active(player))return true;__result=BeckyController.Select(context,player,pile.Cards.Where(filter??(_=>true)).ToArray(),prefs.MinSelect,prefs.MaxSelect);return false;}
}
[HarmonyPatch(typeof(CardSelectCmd),nameof(CardSelectCmd.FromChooseACardScreen))]
static class BeckySelectGenerated
{
    static bool Prefix(PlayerChoiceContext context,IReadOnlyList<CardModel> cards,Player player,ref Task<CardModel?> __result)
    {if(!BeckyController.Active(player))return true;__result=Choose(context,player,cards);return false;}
    static async Task<CardModel?> Choose(PlayerChoiceContext ctx,Player player,IReadOnlyList<CardModel> cards)=>(await BeckyController.Select(ctx,player,cards,0,1)).FirstOrDefault();
}
[HarmonyPatch(typeof(CardSelectCmd),nameof(CardSelectCmd.FromHandForUpgrade))]
static class BeckySelectUpgrade
{
    static bool Prefix(PlayerChoiceContext context,Player player,ref Task<CardModel?> __result)
    {if(!BeckyController.Active(player))return true;__result=Choose(context,player);return false;}
    static async Task<CardModel?> Choose(PlayerChoiceContext ctx,Player player)=>(await BeckyController.Select(ctx,player,player.PlayerCombatState!.Hand.Cards.Where(c=>c.IsUpgradable).ToArray(),0,1)).FirstOrDefault();
}
