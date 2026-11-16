using ArtuMod;
using Godot;
using HarmonyLib;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuAutoplayLab;

public static class SecondBatchScenario
{
    public static bool Active,Tracing;
    static readonly List<CardPlay> Plays=[];
    static readonly List<object> Trace=[];
    public static void Observe(CardPlay play){if(Active)Plays.Add(play);}
    public static void Mark(string phase,ICombatState state)
    {if(Tracing)Trace.Add(new{phase,side=state.CurrentSide.ToString(),enemies=state.HittableEnemies.Select(e=>new{id=e.Monster?.Id.ToString(),block=e.Block,hp=e.CurrentHp}).ToArray()});}
    public static async Task Run(string dir)
    {
        Active=true;var checks=new List<string>();
        void Save(string name,object data)=>File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Until(Func<bool> test,string label){var deadline=DateTime.UtcNow.AddSeconds(50);while(!test()){if(DateTime.UtcNow>deadline)throw new TimeoutException(label);await Task.Delay(50);}}
        async Task Shot(string name){await Task.Delay(350);NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,name+".png"));}
        try
        {
            Engine.TimeScale=6;using var select=CardSelectCmd.UseSelector(new First());
            var run=await NGame.Instance!.StartNewSingleplayerRun(ModelDb.Character<Artu>(),true,ActModel.GetDefaultList(),[],"ARTU_SECOND_BATCH",GameMode.Standard,0);
            var p=run.Players[0];var s=Util.State(p)!;var ctx=new ThrowingPlayerChoiceContext();
            await CreatureCmd.SetMaxHp(p.Creature,2000);await CreatureCmd.SetCurrentHp(p.Creature,1800);await PlayerCmd.GainGold(10000,p);
            await CardPileCmd.RemoveFromDeck(p.Deck.Cards.OfType<SultanCard>().ToArray());CombatState combat=null!;int floor=0;
            async Task Battle()
            {
                await RunManager.Instance.EnterMapPointInternal(++floor,MapPointType.Monster,null,true);
                await Until(()=>CombatManager.Instance.IsInProgress&&p.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"battle start");
                combat=((CombatRoom)run.CurrentRoom!).CombatState;
                foreach(var e in combat.HittableEnemies){await CreatureCmd.SetMaxHp(e,50000);await CreatureCmd.SetCurrentHp(e,50000);}
                foreach(var c in p.PlayerCombatState!.AllCards.ToArray())await CardPileCmd.RemoveFromCombat(c);
                await PlayerCmd.GainEnergy(500,p);
            }
            async Task<CardModel> Add(Type type,bool upgrade=false){var c=combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)),p);if(upgrade)CardCmd.Upgrade(c);await CardPileCmd.Add(c,PileType.Hand);return c;}
            async Task Play(CardModel c)
            {int before=Plays.Count;Check("manual accepted "+c.Id,c.TryManualPlay(c.TargetType==TargetType.AnyEnemy?combat.HittableEnemies.First():null));await Until(()=>Plays.Skip(before).Any(x=>x.Card==c&&x.IsLastInSeries)&&c.Pile?.Type is not PileType.Hand and not PileType.Play,"finish "+c.Id);}
            async Task Use<T>(bool up=false)where T:CardModel=>await Play(await Add(typeof(T),up));
            async Task Finish()
            {
                foreach(var e in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(e);
                var c=await Add(typeof(DestructiveStrike));c.DynamicVars.Damage.BaseValue=999999;await PlayerCmd.GainEnergy(500,p);await Play(c);
                await Until(()=>!CombatManager.Instance.IsInProgress,"victory");await Task.Delay(300);await SaveManager.Instance.SaveRun(null);
            }
            await Battle();await Task.Delay(400);
            var row=NRun.Instance!.GetNode<HBoxContainer>("ArtuPersistentState/StatusRow");
            Check("all zero states hidden",!row.Visible&&row.GetChildren().OfType<Button>().All(b=>!b.Visible));
            if(System.Environment.GetEnvironmentVariable("ARTU_BATCH_UI_ONLY")=="1")
            {
                foreach(var power in ModelDb.All.OfType<ArtuPower>().Where(power=>power.GetType().Name.EndsWith("Power",StringComparison.Ordinal)))
                    Check("native combat and hover icon "+power.Id,power.Icon.GetWidth()==128&&power.BigIcon.GetWidth()==128&&power.IconPath.StartsWith("res://ArtuMod/images/status/"));
                foreach(var b in row.GetChildren().OfType<Button>())Check("persistent state icon "+b.Name,b.Icon!=null&&b.Icon.GetWidth()==128);
                await PowerCmd.Apply<CrimePower>(ctx,p.Creature,1,p.Creature,null);s.Anger=1;s.Mockery=2;s.Devotion=s.Fanaticism=s.Treason=1;await Task.Delay(400);
                for(int kind=0;kind<4;kind++)await ArmyPower.Gain(ctx,p,kind,kind+1);
                await PowerCmd.Apply<PurityInsightPower>(ctx,p.Creature,1,p.Creature,null);
                await PowerCmd.Apply<SecretInsightPower>(ctx,p.Creature,1,p.Creature,null);
                await PowerCmd.Apply<AwakeningPower>(ctx,p.Creature,1,p.Creature,null);
                await PowerCmd.Apply<SubjugatedPower>(ctx,combat.HittableEnemies.First(),1,p.Creature,null);
                row.GetNode<Button>("State3").EmitSignal(Control.SignalName.MouseEntered);await Shot("devotion-under-character");
                row.GetNode<Button>("State3").EmitSignal(Control.SignalName.MouseExited);
                Save("result.json",new{passed=checks.Count,failed=0,uiScreenshotOnly=true});NGame.Instance.GetTree().Quit(0);return;
            }
            await Use<BronzeConquest>();Check("bronze conquest native no enemy target",p.Creature.GetPowerAmount<StrengthPower>()==2&&p.Creature.GetPowerAmount<DexterityPower>()==-1);
            await Use<CriminalRecruits>();Check("native criminals base7",p.Creature.GetPowerAmount<LevyPower>()==7&&s.Crime==1);
            await Use<CriminalRecruits>(true);Check("native criminals upgrade10",p.Creature.GetPowerAmount<LevyPower>()==17&&s.Crime==2);
            foreach(var type in new[]{typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent),typeof(PerfectWorld),typeof(NewWorld),typeof(RegicidePlan)})
            {
                foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);
                int delta=3-p.PlayerCombatState!.Energy;if(delta>0)await PlayerCmd.GainEnergy(delta,p);else if(delta<0)await PlayerCmd.LoseEnergy(-delta,p);
                await Play(await Add(type));Check("native thought spends one energy "+type.Name,p.PlayerCombatState.Energy==2);
            }
            foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);await Finish();

            for(int kind=0;kind<4;kind++)
            {
                await Battle();foreach(var e in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(e);
                var enemy=combat.HittableEnemies.First();await CreatureCmd.LoseBlock(ctx,enemy,enemy.Block,p.Creature);
                Trace.Clear();Tracing=true;await Use<BronzeLust>();Mark("after bronze lust gives block",combat);
                await ArmyPower.Gain(ctx,p,kind,1);Mark("end turn requested",combat);int hp=enemy.CurrentHp,turn=p.PlayerCombatState!.TurnNumber;
                PlayerCmd.EndTurn(p,false);await Until(()=>p.PlayerCombatState!.TurnNumber>turn&&p.PlayerCombatState.Phase==PlayerTurnPhase.Play,"normal full turn");
                Tracing=false;Save($"block-trace-{kind}.json",Trace.ToArray());
                Check("army trace phase recorded "+kind,Trace.Count>=4);Check("army hit remained blocked "+kind,enemy.CurrentHp==hp);
                await Finish();
            }
            await Battle();await Use<TemporaryRecruitment>();await Use<TemporaryRecruitment>(true);
            Check("native independent recruitment effects",p.Creature.Powers.OfType<TemporaryRecruitmentPower>().Count()==2);
            int beforeConversion=s.LeviesConverted;await Use<ArmyRegularization>();
            Check("native regularization converts five, refills eight",s.LeviesConverted-beforeConversion==5&&p.Creature.GetPowerAmount<LevyPower>()==8);
            Check("refill does not count agenda conversions",s.LeviesConverted-beforeConversion==5);
            await Use<FieldReorganization>(true);Check("native conversion cap5 and no refill left",p.Creature.GetPowerAmount<LevyPower>()==5);
            await Use<RecruitmentNotice>(true);await Use<TrainArmy>(true);await Use<RevolutionaryAcademy>(true);int oldConverted=s.LeviesConverted,turnStart=p.PlayerCombatState!.TurnNumber;
            PlayerCmd.EndTurn(p,false);await Until(()=>p.PlayerCombatState!.TurnNumber>turnStart&&p.PlayerCombatState.Phase==PlayerTurnPhase.Play,"training and academy turn");
            Check("native start-turn supply converts2+6",s.LeviesConverted-oldConverted==8);await Finish();

            await Battle();var boats=(BurnTheBoats)await Add(typeof(BurnTheBoats),true);
            await PowerCmd.Apply<CrimePower>(ctx,p.Creature,2,p.Creature,null);await ArmyPower.Gain(ctx,p,0,3);await Task.Delay(400);
            var cardNode=MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand.Instance!.GetCard(boats)!;
            var descNode=(GodotObject)AccessTools.Field(cardNode.GetType(),"_descriptionLabel").GetValue(cardNode)!;
            Check("live visible boats total updates after army gained",descNode.Get("text").AsString().Contains("可消耗3层"));
            boats.UpdateDynamicVarPreview(CardPreviewMode.Normal,null,boats.DynamicVars);
            Check("native boats crime preview7/1 and21 total",boats.DynamicVars.Damage.PreviewValue==7&&boats.DynamicVars.Block.PreviewValue==1&&boats.DamageEstimate(3)=="21");
            var owner=new Control{Position=new Vector2(720,180),Size=new Vector2(20,20)};NRun.Instance.AddChild(owner);NHoverTipSet.CreateAndShow(owner,boats.HoverTips);await Shot("boats-totals");NHoverTipSet.Remove(owner);owner.QueueFree();
            await Play(boats);await Finish();

            await Battle();foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);
            await PlayerCmd.LoseEnergy(p.PlayerCombatState!.Energy-3,p);
            await Use<SilverLust>();await Use<SilverLust>();await Use<EliteTroops>(true);var fourth=await Add(typeof(EliteTroops),true);
            Check("four one-cost combo cards cannot all play on3 energy",p.PlayerCombatState.Energy==0&&!fourth.TryManualPlay(null));
            Save("lust-combo-observation.json",new{energy=0,eliteLayers=p.Creature.GetPowerAmount<ElitePower>(),playerIntangible=p.Creature.GetPowerAmount<IntangiblePower>(),playerRegen=p.Creature.GetPowerAmount<RegenPower>(),enemy=combat.HittableEnemies.Select(e=>new{regen=e.GetPowerAmount<RegenPower>(),plating=e.GetPowerAmount<PlatingPower>()}),artificialSetup=true,notUniversalBalanceProof=true});
            await Finish();

            await Battle();await PowerCmd.Apply<CrimePower>(ctx,p.Creature,1,p.Creature,null);s.Anger=1;s.Mockery=2;s.Devotion=1;s.Fanaticism=1;s.Treason=1;await Task.Delay(400);
            Check("six nonzero independent icons",row.GetChildren().OfType<Button>().Count(b=>b.Visible)==6);
            var button=row.GetNode<Button>("State3");button.EmitSignal(Control.SignalName.MouseEntered);await Shot("devotion-under-character");button.EmitSignal(Control.SignalName.MouseExited);
            var boxOwner=new Control{Position=new Vector2(720,180),Size=new Vector2(20,20)};NRun.Instance.AddChild(boxOwner);NHoverTipSet.CreateAndShow(boxOwner,s.HoverTips);await Shot("box-without-progress");NHoverTipSet.Remove(boxOwner);boxOwner.QueueFree();
            var deadline=(SultanCard)await Add(typeof(GoldSlaughter));deadline.TurnsSinceUse=2;NHoverTipSet.CreateAndShow(button,deadline.HoverTips);await Shot("gold-deadline-expiry");NHoverTipSet.Remove(button);await CardPileCmd.RemoveFromCombat(deadline);
            var dark=await Add(typeof(GlimpseDarkness));NHoverTipSet.CreateAndShow(button,dark.HoverTips);await Shot("secret-insight-explicit-reward");NHoverTipSet.Remove(button);await CardPileCmd.RemoveFromCombat(dark);
            await Finish();Check("map states still visible",row.Visible);await Shot("map-nonzero-states");
            await RunManager.Instance.EnterMapPointInternal(++floor,MapPointType.RestSite,null,true);await Task.Delay(500);
            Check("outside combat state UI uses fallback",row.Visible&&row.Position.Y>NRun.Instance.GetViewportRect().Size.Y*0.75f);await Shot("rest-site-nonzero-states");await SaveManager.Instance.SaveRun(null);
            var reloaded=(WitchCardBox)RelicModel.FromSerializable(s.ToSerializable());Check("same persistent source serializes",reloaded.Devotion==1&&reloaded.Mockery==2);
            await NGame.Instance.ReturnToMainMenuAfterRun();Save("result.json",new{passed=checks.Count,failed=0,checks,artificialResources=true,profile=OS.GetUserDataDir(),actualVictories=9,armyMechanicsNotChanged=true});NGame.Instance.GetTree().Quit(0);
        }
        catch(Exception e){Save("result.json",new{passed=checks.Count,failed=1,checks,error=e.ToString()});GD.PrintErr(e.ToString());NGame.Instance?.GetTree().Quit(1);}
        finally{Active=false;Tracing=false;}
    }
    sealed class First:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards,int min,int max)=>Task.FromResult(cards.Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.AfterCardPlayed))]
static class ObserveSecondBatchPlay
{static void Prefix(CardPlay cardPlay)=>SecondBatchScenario.Observe(cardPlay);}
[HarmonyPatch(typeof(ArmyPower),nameof(ArmyPower.Trigger))]
static class ObserveSecondBatchArmy
{
    static void Prefix(ArmyPower __instance)=>SecondBatchScenario.Mark("before army "+__instance.Kind,__instance.Owner.CombatState!);
    static void Postfix(ArmyPower __instance,ref Task __result){if(SecondBatchScenario.Tracing)__result=After(__result,__instance);}
    static async Task After(Task task,ArmyPower a){await task;SecondBatchScenario.Mark("after army "+a.Kind,a.Owner.CombatState!);}
}
[HarmonyPatch(typeof(Creature),nameof(Creature.AfterTurnStart))]
static class ObserveSecondBatchClear
{
    static void Prefix(Creature __instance,CombatSide side){if(side==CombatSide.Enemy&&__instance.Monster!=null&&__instance.CombatState!=null)SecondBatchScenario.Mark("before enemy turn clear",__instance.CombatState);}
    static void Postfix(Creature __instance,CombatSide side,ref Task __result){if(SecondBatchScenario.Tracing&&side==CombatSide.Enemy&&__instance.Monster!=null)__result=After(__result,__instance);}
    static async Task After(Task task,Creature e){await task;SecondBatchScenario.Mark("after enemy turn clear",e.CombatState!);}
}
