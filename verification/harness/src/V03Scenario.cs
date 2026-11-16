using Godot;
using HarmonyLib;
using System.Text.Json;
using ArtuMod;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
namespace ArtuAutoplayLab;

public static class V03Scenario
{
    static Player? InjectForPlayer;
    static readonly List<CardPlay> Plays=[];
    public static void Observe(CardPlay play)=>Plays.Add(play);
    public static async Task InjectAfterBefore(Task original,CardPlay play)
    {
        await original;
        if(play.Card is not SuperBecky||play.Player!=InjectForPlayer)return;
        InjectForPlayer=null;
        await CardPileCmd.Add(play.Player.Creature.CombatState!.CreateCard<StoneConquest>(play.Player),PileType.Hand);
    }
    public static async Task Run(string directory)
    {
        var checks=new List<string>();var json=new JsonSerializerOptions{WriteIndented=true};
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,json));
        async Task Until(Func<bool> ready,string reason,int seconds=45)
        {var end=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>end)throw new TimeoutException(reason);await Task.Delay(100);}}
        try
        {
            Engine.TimeScale=6;
            Save("conditions.json",new{mode="v0.3 actual manual-action and forced-play pipeline",profile=OS.GetUserDataDir(),seed="ARTU_V03_SCENARIO",
                testChanges="1000 max/900 current player HP; artificial combat cards; force enemy deaths at end",balanceTest=false});
            await Task.Delay(1000);
            using var selector=CardSelectCmd.UseSelector(new AutoSlayCardSelector(new Rng(331)));
            var run=await NGame.Instance!.StartNewSingleplayerRun(ModelDb.Character<Artu>(),false,ActModel.GetDefaultList(),[],"ARTU_V03_SCENARIO",GameMode.Standard,0);
            run.CurrentActIndex=2;await RunManager.Instance.GenerateMap();var player=run.Players[0];
            await CreatureCmd.SetMaxHp(player.Creature,1000);await CreatureCmd.SetCurrentHp(player.Creature,900);
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster,model:ModelDb.Encounter<TeaPartyEncounter>().ToMutable());
            await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"first player turn");
            var room=(CombatRoom)run.CurrentRoom!;var combat=room.CombatState;
            async Task ClearHand(){foreach(var card in player.PlayerCombatState!.Hand.Cards.ToArray())await CardPileCmd.Add(card,PileType.Discard);}
            async Task<T> Add<T>()where T:CardModel{var card=combat.CreateCard<T>(player);await CardPileCmd.Add(card,PileType.Hand);return card;}
            await ClearHand();var becky=await Add<SuperBecky>();var letters=new Dictionary<CardModel,string>();
            foreach(char letter in "ABCDEFG")letters[await Add<LoyalHound>()]=letter.ToString();
            Check("Actual manual Becky card accepted with ordinary 3 energy",becky.TryManualPlay(null));
            await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play&&player.PlayerCombatState.TurnNumber==2,"Becky completes and ends the first turn");
            var forced=Plays.Where(p=>letters.ContainsKey(p.Card)).ToArray();
            Check("Actual native play pipeline uses realtime ADGC order",string.Join("",forced.Select(p=>letters[p.Card]))=="ADGC");
            Check("Actual forced cards spend zero energy",forced.All(p=>p.IsAutoPlay&&p.Resources.EnergySpent==0));
            Check("Actual takeover leaves no active session",!BeckyController.TakeoverRunning(player));
            await ClearHand();await PlayerCmd.GainEnergy(10,player);
            for(int i=0;i<3;i++)
            {
                var thought=await Add<TouchPurity>();int before=Plays.Count;
                Check($"Actual purity play {i+1} accepted",thought.TryManualPlay(null));
                await Until(()=>Plays.Count>before&&Plays.Any(p=>p.Card==thought),"purity card finishes");
                await Task.Delay(300);
            }
            Check("Actual three purity cards stack the independent counter",player.Creature.GetPowerAmount<PurityInsightPower>()==3&&Util.State(player)!.PureSecond==3);
            PlayerCmd.EndTurn(player,false);
            await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play&&player.PlayerCombatState.TurnNumber==3,"purity next-turn draw");
            var drawn=player.PlayerCombatState!.Hand.Cards.ToArray();
            Check("Actual opening draw discounts only one card at three stacks",drawn.Count(c=>c.EnergyCost.HasLocalModifiers)==1);
            Save("purity-draw.json",drawn.Select(c=>new{id=c.Id.ToString(),canonical=c.EnergyCost.Canonical,cost=c.EnergyCost.GetWithModifiers(CostModifiers.All),local=c.EnergyCost.HasLocalModifiers}).ToArray());
            await ClearHand();await PlayerCmd.GainEnergy(10,player);await RelicCmd.Obtain<TyrantsAccomplice>(player);
            var cat2=await Add<SuperBecky>();var zeroCharge=await Add<SoldierCharge>();await Add<LoyalHound>();var affairs=await Add<StateAffairs>();await Add<Ironhead>();await Add<MoonDogBook>();await Add<Quickfoot>();
            InjectForPlayer=player;int start=Plays.Count;int mockery=Util.State(player)!.Mockery;
            Check("Actual second takeover starts before Sultan injection",cat2.TryManualPlay(null));
            await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play&&player.PlayerCombatState.TurnNumber==4,"forced choices complete and second takeover ends turn",60);
            var second=Plays.Skip(start).ToArray();
            Check("Actual cat forces zero-soldier charge despite its normal play restriction",second.Any(p=>p.Card==zeroCharge));
            Check("Actual failed soldier condition gives no soldiers",player.Creature.GetPowerAmount<SoldierPower>()==0);
            Check("Actual cat resolves State Affairs selection without a UI wait",second.Any(p=>p.Card==affairs));
            Check("Actual cat uses injected Sultan without mockery",second.Any(p=>p.Card is StoneConquest)&&Util.State(player)!.Mockery==mockery);
            Check("Actual Sultan exemption leaves its deadline fulfilled",second.Where(p=>p.Card is StoneConquest).All(p=>((SultanCard)p.Card).Original.UsedThisBattle));
            Check("Actual all child cards remain free despite ordinary costs and tyranny",second.Where(p=>p.Card!=cat2).All(p=>p.Resources.EnergySpent==0));
            await RelicCmd.Remove(player.Relics.Single(r=>r is TyrantsAccomplice));await ClearHand();
            await PowerCmd.Apply<ReplaySultanPower>(new ThrowingPlayerChoiceContext(),player.Creature,2,player.Creature,null);
            var luxury=await Add<BronzeLuxury>();var slaughter=await Add<StoneSlaughter>();int hp=player.Creature.CurrentHp;int beforeChild=Plays.Count;
            Check("Actual bronze luxury replay test starts",luxury.TryManualPlay(null));
            await Until(()=>Plays.Skip(beforeChild).Count(p=>p.Card==luxury)==2,"outer luxury replay resolves");
            await Task.Delay(300);
            Check("Actual bronze luxury adds its replay to external replay without multiplying",Plays.Skip(beforeChild).Count(p=>p.Card==slaughter)==3&&hp-player.Creature.CurrentHp==3);
            await ClearHand();var outer=await Add<BronzeLuxury>();var inner=await Add<BronzeLuxury>();var money=await Add<StoneLuxury>();int gold=player.Gold;beforeChild=Plays.Count;
            using(var preferBronze=CardSelectCmd.PushSelector(new PreferBronzeSelector()))
            {
                Check("Actual double bronze luxury recursive test starts",outer.TryManualPlay(null));
                await Until(()=>Plays.Skip(beforeChild).Any(p=>p.Card==outer),"nested bronze luxury resolves");
                await Task.Delay(300);
            }
            Check("Actual nested luxury executes two full gold costs and terminates",gold-player.Gold==10&&Plays.Skip(beforeChild).Count(p=>p.Card==money)==2&&Plays.Skip(beforeChild).Count(p=>p.Card==inner)==2);
            Save("plays.json",Plays.Select(p=>new{id=p.Card.Id.ToString(),label=letters.GetValueOrDefault(p.Card),p.IsAutoPlay,p.PlayIndex,p.PlayCount,p.Resources.EnergySpent}).ToArray());
            int relics=player.Relics.Count;foreach(var enemy in room.Enemies.ToArray())await CreatureCmd.Kill(enemy,true);
            await CombatManager.Instance.CheckWinCondition();
            await Until(()=>!CombatManager.Instance.IsInProgress,"actual victory");
            Check("Updated tea-party victory still grants two relics",player.Relics.Count==relics+2);
            Check("Actual second-tier purity evolution removes current combat counter and grants pending next-tier receipt",player.Deck.Cards.Any(c=>c is AtonementPrayer));
            Save("result.json",new{passed=checks.Count,failed=0,checks,finalHp=player.Creature.CurrentHp});NGame.Instance.GetTree().Quit(0);
        }
        catch(Exception exception)
        {Save("result.json",new{passed=checks.Count,failed=1,checks,error=exception.ToString(),plays=Plays.Select(p=>p.Card.Id.ToString()).ToArray()});GD.PrintErr(exception.ToString());NGame.Instance?.GetTree().Quit(1);}
    }
    sealed class PreferBronzeSelector:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)
            =>Task.FromResult(options.OrderByDescending(c=>c is BronzeLuxury).Take(Math.Min(minSelect,maxSelect)));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeCardPlayed))]
static class V03InjectTestCard
{
    static void Postfix(CardPlay cardPlay,ref Task __result)=>__result=V03Scenario.InjectAfterBefore(__result,cardPlay);
}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterCardPlayed))]
static class V03ObserveActualPlays
{
    static void Postfix(CardPlay cardPlay,ref Task __result)=>__result=After(__result,cardPlay);
    static async Task After(Task original,CardPlay play){await original;V03Scenario.Observe(play);}
}
