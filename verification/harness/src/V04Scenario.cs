using Godot;
using HarmonyLib;
using System.Text.Json;
using ArtuMod;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Events;

using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuAutoplayLab;
public static class V04Scenario
{
    static readonly List<CardPlay> Plays=[];
    public static NRewardsScreen? RewardScreen;
    public static NCardRewardSelectionScreen? CardRewardScreen;
    public static void Observe(CardPlay play)=>Plays.Add(play);
    public static async Task Run(string directory,string mode)
    {
        var checks=new List<string>();var json=new JsonSerializerOptions{WriteIndented=true};
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,json));
        async Task Until(Func<bool> predicate,string label,int seconds=45){var end=DateTime.UtcNow.AddSeconds(seconds);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(label);await Task.Delay(50);}}
        try
        {
            Engine.TimeScale=6;
            int act=mode=="full"?1:int.Parse(mode);
            Save("conditions.json",new{profile=OS.GetUserDataDir(),mode,act=act+1,balanceTest=false,fixture="actual saved run, normal PlayCardAction; artificial 2000HP/10000 gold/500 energy; enemies HP50000; 6x animation speed"});
            using var selector=CardSelectCmd.UseSelector(new SelectFirst());
            var run=await NGame.Instance!.StartNewSingleplayerRun(ModelDb.Character<Artu>(),true,ActModel.GetDefaultList(),[],"ARTU_V04_"+mode,GameMode.Standard,0);
            run.CurrentActIndex=act;await RunManager.Instance.GenerateMap();var player=run.Players[0];
            await CreatureCmd.SetMaxHp(player.Creature,2000);await CreatureCmd.SetCurrentHp(player.Creature,1800);await PlayerCmd.GainGold(10000,player);
            await RunManager.Instance.EnterMapPointInternal(1,MapPointType.Monster,null,true);
            await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"normal first turn");
            var combat=((CombatRoom)run.CurrentRoom!).CombatState;
            foreach(var enemy in combat.HittableEnemies.ToArray()){await CreatureCmd.SetMaxHp(enemy,50000);await CreatureCmd.SetCurrentHp(enemy,50000);}
            async Task Clear(){foreach(var c in player.PlayerCombatState!.AllCards.Where(c=>c.Pile?.Type is PileType.Hand or PileType.Draw or PileType.Discard).ToArray())await CardPileCmd.RemoveFromCombat(c);foreach(var power in player.Creature.Powers.ToArray())await PowerCmd.Remove(power);V04.Reset(player);SultanUsage.Reset(player);await PlayerCmd.GainEnergy(500,player);}
            async Task<CardModel> Add(Type type,PileType pile=PileType.Hand){var c=combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)),player);await CardPileCmd.Add(c,pile);return c;}
            async Task Play(CardModel card){int before=Plays.Count;var target=card.TargetType==TargetType.AnyEnemy?combat.HittableEnemies.FirstOrDefault():null;Check("manual accepted "+card.Id,card.TryManualPlay(target));await Until(()=>Plays.Skip(before).Any(p=>p.Card==card&&p.IsLastInSeries)&&card.Pile?.Type is not PileType.Play and not PileType.Hand,"card finish "+card.Id);}
            async Task Use<T>()where T:CardModel=>await Play(await Add(typeof(T)));
            async Task RewardOption(EventOption option,Func<CardModel,bool> legal,string label)
            {
                RewardScreen=null;int size=player.Deck.Cards.Count;var chosen=option.Chosen();
                await Until(()=>RewardScreen!=null,label+" reward screen");var screen=RewardScreen!;
                var set=(RewardsSet)AccessTools.Field(typeof(NRewardsScreen),"_rewardsSet").GetValue(screen)!;
                var reward=set.Rewards.OfType<RevolutionCardReward>().Single();Check(label+" native reward candidates legal",reward.Cards.Any()&&reward.Cards.All(legal));
                var buttons=(List<Control>)AccessTools.Field(typeof(NRewardsScreen),"_rewardButtons").GetValue(screen)!;
                var button=buttons.OfType<NRewardButton>().Single(b=>b.Reward==reward);
                CardRewardScreen=null;var taking=(Task)AccessTools.Method(typeof(NRewardButton),"GetReward").Invoke(button,null)!;
                await Until(()=>CardRewardScreen!=null&&AccessTools.Field(typeof(NCardRewardSelectionScreen),"_completionSource").GetValue(CardRewardScreen)!=null,label+" card choices ready");await Task.Delay(400);
                AccessTools.Method(typeof(NCardRewardSelectionScreen),"SelectCard").Invoke(CardRewardScreen,[CardRewardScreen!.GetCardHolder(reward.Cards.First())]);
                await Until(()=>taking.IsCompleted,label+" card selection completes");await taking;await chosen;
                Check(label+" selected card permanently added",player.Deck.Cards.Count==size+1);
            }
            if(mode=="full")
            {
                var types=new[]{typeof(KnowYourEnemy),typeof(RobPeterToPayPaul),typeof(CriminalDefense),typeof(SecretMessenger),typeof(MarchingDrum),typeof(CollectiveWisdom),typeof(ScribesManuscript),typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent),typeof(ArmyRegularization),typeof(RevolutionaryAcademy),typeof(AllByKingsGrace),typeof(SpiralWaltz),typeof(OpenThought),typeof(LifeScepter),typeof(ProfitFromCrime),typeof(BurnTheBoats),typeof(UndergroundPress),typeof(MoneyOpensDoors),typeof(RoyalConscription)};
                foreach(var type in types)
                {
                    await Clear();await ArmyPower.Gain(new ThrowingPlayerChoiceContext(),player,0,5);await ArmyPower.Gain(new ThrowingPlayerChoiceContext(),player,1,2);
                    await Add(typeof(Ironhead));await Add(typeof(StoneSlaughter),PileType.Discard);for(int i=0;i<6;i++)await Add(typeof(DispatchTroops),PileType.Draw);
                    var card=await Add(type);await Play(card);Check("native effect completes "+card.Id,Plays.Any(p=>p.Card==card));
                }
                await Clear();await Use<AncientFaith>();int hp=player.Creature.CurrentHp;int before=Plays.Count;await Use<StoneSlaughter>();var child=Plays.Skip(before).Where(p=>p.Card is StoneSlaughter).ToArray();Check("native faith adds one replay with only first HP cost plus 2HP",child.Length==2&&hp-player.Creature.CurrentHp==3);
                await Clear();await Use<NewWorld>();hp=player.Creature.CurrentHp;before=Plays.Count;await Use<StoneSlaughter>();Check("native new world two appended free replays",Plays.Skip(before).Count(p=>p.Card is StoneSlaughter)==3&&hp-player.Creature.CurrentHp==4);
                await Clear();await Use<SpiralWaltz>();var cat=await Add(typeof(SuperBecky));var names=new Dictionary<CardModel,char>();foreach(char c in "ABCDEFG")names[await Add(typeof(LoyalHound))]=c;
                int turn=player.PlayerCombatState!.TurnNumber;before=Plays.Count;Check("waltz cat starts",cat.TryManualPlay(null));await Until(()=>player.PlayerCombatState?.TurnNumber==turn+1&&player.PlayerCombatState.Phase==PlayerTurnPhase.Play,"copied cat finishes once");
                Check("waltz copies effect only, second scan of remaining hand",string.Concat(Plays.Skip(before).Where(p=>names.ContainsKey(p.Card)).Select(p=>names[p.Card]))=="ADGCBF");Check("waltz cat has only one actual card use",Plays.Skip(before).Count(p=>p.Card==cat)==1);
                await Clear();var original=player.RunState.CreateCard<GoldSlaughter>(player);original.TurnsSinceUse=2;await CardPileCmd.Add(original,PileType.Deck);await Use<AllByKingsGrace>();var gold=combat.CloneCard(original);gold.DeckVersion=original;await CardPileCmd.Add(gold,PileType.Hand);hp=player.Creature.CurrentHp;await Play(gold);Check("native grace keeps original gold deadline and HP",original.TurnsSinceUse==2&&!original.UsedThisBattle&&hp==player.Creature.CurrentHp);await Util.State(player)!.TickGold(new ThrowingPlayerChoiceContext());Check("native grace freezes turn timer",original.TurnsSinceUse==2);
                await Clear();await Use<LifeScepter>();hp=player.Creature.CurrentHp;await Use<StoneSlaughter>();Check("native optional scepter payment replaces HP",hp==player.Creature.CurrentHp&&combat.HittableEnemies.Any(e=>e.GetPowerAmount<RegenPower>()==7));
                await Clear();await PowerCmd.Apply<CrimePower>(new ThrowingPlayerChoiceContext(),player.Creature,8,player.Creature,null);await Use<Quickfoot>();Check("native crime eight creates exactly one native Dazed",player.PlayerCombatState!.DrawPile.Cards.Count(c=>c is MegaCrit.Sts2.Core.Models.Cards.Dazed)==1);
                await Clear();await Util.AddToDeck(player,ModelDb.Card<NoteInBook>());
                await RunManager.Instance.EnterRoomDebug(RoomType.Event,model:ModelDb.Event<ClearStreamsMeeting>());var meeting=((EventRoom)run.CurrentRoom!).LocalMutableEvent;Check("new streams event displays normal options",meeting.CurrentOptions.Count>=2&&Util.State(player)!.ClearStreamsSeen);int money=player.Gold;await RewardOption(meeting.CurrentOptions.Single(o=>o.TextKey.EndsWith("_INVITE")),c=>c is ArtuCard {Thought:true}&&c.Rarity is CardRarity.Common or CardRarity.Uncommon,"streams invite");Check("streams invite loses exactly 35 gold",money-player.Gold==35);
                await RunManager.Instance.EnterRoomDebug(RoomType.Event,model:ModelDb.Event<NurseryClassroom>());var nursery=((EventRoom)run.CurrentRoom!).LocalMutableEvent;Check("new nursery event displays four normal options",nursery.CurrentOptions.Count==4&&Util.State(player)!.NurserySeen);money=player.Gold;await RewardOption(nursery.CurrentOptions.Single(o=>o.TextKey.EndsWith("_KNOWLEDGE")),c=>c is ArtuCard {Reading:true}&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare,"nursery reading");Check("nursery reading loses exactly 30 gold",money-player.Gold==30);
                foreach(var eventType in new[]{typeof(TabletOfTruth),typeof(Leaderless),typeof(Trial)})
                {
                    await RunManager.Instance.EnterRoomDebug(RoomType.Event,model:ModelDb.GetById<EventModel>(ModelDb.GetId(eventType)));var e=((EventRoom)run.CurrentRoom!).LocalMutableEvent;string key=eventType==typeof(TabletOfTruth)?"ARTU_TRANSCRIBE":eventType==typeof(Leaderless)?"ARTU_SECRET_COUNCIL":"ARTU_PROTECT_WITNESS";Check(eventType.Name+" preserves old choices plus hidden option",e.CurrentOptions.Any(o=>o.TextKey==key)&&e.CurrentOptions.Any(o=>o.TextKey!=key));var option=e.CurrentOptions.Single(o=>o.TextKey==key);
                    if(eventType==typeof(TabletOfTruth)){hp=player.Creature.CurrentHp;await RewardOption(option,c=>c is ArtuCard {Reading:true}&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare,"tablet reading");Check("tablet loses exactly 8HP",hp-player.Creature.CurrentHp==8);}
                    else if(eventType==typeof(Leaderless)){int size=player.Deck.Cards.Count;money=player.Gold;await option.Chosen();Check("secret council copies legal thought for 30 gold",player.Deck.Cards.Count==size+1&&money-player.Gold==30);}
                    else {Util.State(player)!.Crime=2;money=player.Gold;await option.Chosen();Check("protect witness permanently removes crime for 30 gold",Util.State(player)!.Crime==0&&money-player.Gold==30);}
                }
                await RunManager.Instance.EnterMapPointInternal(2,MapPointType.Monster,null,true);await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"ending new normal combat");combat=((CombatRoom)run.CurrentRoom!).CombatState;
                await Clear();await Use<NoteInBook>();Check("native note Power and agenda view complete",player.Creature.HasPower<RevolutionNotePower>());
                await Use<RegicidePlan>();await Use<RevolutionConcept>();Check("reverse order same-combat generates unique plan",player.PlayerCombatState!.Hand.Cards.Count(c=>c is RevolutionPlan)==1&&Util.State(player)!.PlanGranted);
            }
            if(mode!="full")await Clear();var plan=player.PlayerCombatState!.Hand.Cards.FirstOrDefault(c=>c is RevolutionPlan)??await Add(typeof(RevolutionPlan));Check("revolution plan manual accepted",plan.TryManualPlay(null));
            await Until(()=>Util.State(player)!.RevolutionWon&&RunManager.Instance.History?.Win==true,"native victory history");await Task.Delay(300);
            Check("revolution ends run without killing player",run.IsGameOver&&player.Creature.IsAlive&&!CombatManager.Instance.IsInProgress);
            Check("native history persists revolution reason badge",RunManager.Instance.History!.Players.All(p=>p.Badges.Any(b=>b.Id=="ARTUMOD_REVOLUTION_SUCCESS")));
            Save("history.json",RunManager.Instance.History);Save("plays.json",Plays.Select(p=>new{card=p.Card.Id.ToString(),p.IsAutoPlay,p.PlayIndex,p.PlayCount,p.Resources.EnergySpent}));
            var shot=NGame.Instance.GetViewport().GetTexture().GetImage();shot.SavePng(Path.Combine(directory,"revolution-success.png"));
            Save("result.json",new{passed=checks.Count,failed=0,checks,act=act+1,runWin=true,reason="革命成功"});NGame.Instance.GetTree().Quit(0);
        }
        catch(Exception e){Save("result.json",new{passed=checks.Count,failed=1,checks,error=e.ToString()});GD.PrintErr(e.ToString());NGame.Instance?.GetTree().Quit(1);}
    }
    sealed class SelectFirst:MegaCrit.Sts2.Core.TestSupport.ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards,int min,int max)=>Task.FromResult(cards.OrderByDescending(c=>c is ScepterReplaceOption).Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)=>new(){card=cards.FirstOrDefault()?.Card};
    }
}
[HarmonyPatch(typeof(NRewardsScreen),nameof(NRewardsScreen.ShowScreen))]
static class V04ObserveRewards {static void Postfix(NRewardsScreen __result)=>V04Scenario.RewardScreen=__result;}
[HarmonyPatch(typeof(NCardRewardSelectionScreen),nameof(NCardRewardSelectionScreen.ShowScreen))]
static class V04ObserveCardRewards {static void Postfix(NCardRewardSelectionScreen? __result)=>V04Scenario.CardRewardScreen=__result;}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterCardPlayed))]
static class V04ObserveActualPlays
{
    static void Postfix(CardPlay cardPlay,ref Task __result)=>__result=After(__result,cardPlay);
    static async Task After(Task original,CardPlay play){await original;V04Scenario.Observe(play);RepairScenario.Observe(play);}
}
