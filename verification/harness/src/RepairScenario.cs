using ArtuMod;
using Godot;
using HarmonyLib;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
namespace ArtuAutoplayLab;

public static class RepairScenario
{
    public static bool Active;
    static readonly List<CardPlay> Plays=[];
    public static object? ArmyTrace;
    public static void Observe(CardPlay play){if(Active)Plays.Add(play);}
    public static async Task Run(string dir)
    {
        Active=true;var checks=new List<string>();
        void Save(string file,object data)=>File.WriteAllText(Path.Combine(dir,file),JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true}));
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Until(Func<bool> test,string label){var limit=DateTime.UtcNow.AddSeconds(45);while(!test()){if(DateTime.UtcNow>limit)throw new TimeoutException(label);await Task.Delay(50);}}
        async Task Shot(string name){await Task.Delay(250);NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,name+".png"));}
        try
        {
            Engine.TimeScale=6;using var selector=MegaCrit.Sts2.Core.CardSelection.CardSelectorStack.SetTestSelector(new First());
            var run=await NGame.Instance!.StartNewSingleplayerRun(MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<Artu>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1),true,ActModel.GetDefaultList(),[],"ARTU_REPAIR_NATIVE",GameMode.Standard,false,0);
            var p=run.Players[0];var state=Util.State(p)!;var ctx=new ThrowingPlayerChoiceContext();
            await CreatureCmd.SetMaxHp(p.Creature,2000);await CreatureCmd.SetCurrentHp(p.Creature,1800);await PlayerCmd.GainGold(10000,p);
            await CardPileCmd.RemoveFromDeck(p.Deck.Cards.OfType<SultanCard>().ToArray());
            CombatState combat=null!;int floor=0;
            async Task Battle()
            {
                await RunManager.Instance.EnterMapPointInternal(++floor,MapPointType.Monster,null,true);
                await Until(()=>CombatManager.Instance.IsInProgress&&p.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"battle start");
                combat=((CombatRoom)run.CurrentRoom!).CombatState;
                foreach(var e in combat.HittableEnemies.ToArray()){await CreatureCmd.SetMaxHp(e,50000);await CreatureCmd.SetCurrentHp(e,50000);}
                foreach(var c in p.PlayerCombatState!.AllCards.ToArray())await CardPileCmd.RemoveFromCombat(c);
                await PlayerCmd.GainEnergy(500,p);
            }
            async Task<CardModel> Add(Type type,PileType pile=PileType.Hand){var c=combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)),p);await CardPileCmd.Add(c,pile);return c;}
            async Task Play(CardModel card)
            {
                int n=Plays.Count;Check("native manual accepted "+card.Id,card.TryManualPlay(card.TargetType==TargetType.AnyEnemy?combat.HittableEnemies.First():null));
                await Until(()=>Plays.Skip(n).Any(x=>x.Card==card&&x.IsLastInSeries)&&card.Pile?.Type is not PileType.Play and not PileType.Hand,"finish "+card.Id);
            }
            async Task Use<T>()where T:CardModel=>await Play(await Add(typeof(T)));
            async Task Finish()
            {
                var enemies=combat.HittableEnemies.ToArray();foreach(var e in enemies.Skip(1))await CreatureCmd.Kill(e);
                var finisher=await Add(typeof(DestructiveStrike));finisher.DynamicVars.Damage.BaseValue=999999;
                await PlayerCmd.GainEnergy(500,p);await Play(finisher);
                await Until(()=>!CombatManager.Instance.IsInProgress,"victory finishes");await Task.Delay(300);
                await SaveManager.Instance.SaveRun(null);
            }
            await Battle();Check("persistent five-counter strip attached",NRun.Instance!.HasNode("ArtuPersistentState/StatusRow"));
            var tipOwner=new Control{Position=new Vector2(720,180),Size=new Vector2(20,20)};NRun.Instance.AddChild(tipOwner);
            var purity=await Add(typeof(TouchPurity));
            MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.CreateAndShow(tipOwner,purity.HoverTips);
            await Shot("purity-native-hover");MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Remove(tipOwner);tipOwner.QueueFree();await CardPileCmd.RemoveFromCombat(purity);
            await Use<ReturnOnSpending>();await PlayerCmd.LoseGold(20,p);
            var refundOwner=new Control{Position=new Vector2(720,180),Size=new Vector2(20,20)};NRun.Instance.AddChild(refundOwner);
            MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.CreateAndShow(refundOwner,p.Creature.GetPower<SpendingRefundPower>()!.HoverTips);
            await Shot("refund-native-hover");MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Remove(refundOwner);refundOwner.QueueFree();
            Check("native refund tracks 20 not qualified",state.GoldLost==20&&p.Creature.GetPower<SpendingRefundPower>()!.DynamicVars["Needed"].IntValue==1);
            await Use<CoinSlingshot>();Check("coin counts once and reaches 25",state.GoldLost==25);
            for(int payments=0;payments<3;payments++)
            {
                if(payments>0)await Use<StoneSlaughter>();var orderCard=await Add(typeof(UnderKingsOrder));
                var target=combat.HittableEnemies.First();orderCard.UpdateDynamicVarPreview(CardPreviewMode.Normal,target,orderCard.DynamicVars);
                Check("native king preview count "+payments,orderCard.DynamicVars.Damage.PreviewValue==8+payments*3);
                int hp=target.CurrentHp;await Play(orderCard);Check("native king execution count "+payments,hp-target.CurrentHp==8+payments*3);
            }
            var enemy=combat.HittableEnemies.First();await Use<BronzeLust>();
            await CreatureCmd.GainBlock(enemy,20-enemy.Block,MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered,null);
            await ArmyPower.Gain(ctx,p,3,1);ArmyTrace=null;int turn=p.PlayerCombatState!.TurnNumber;
            PlayerCmd.EndTurn(p,false);await Until(()=>p.PlayerCombatState!.TurnNumber>turn&&p.PlayerCombatState.Phase==PlayerTurnPhase.Play,"normal full turn");
            Check("native player-end army trace captured",ArmyTrace!=null);Save("army-trace.json",ArmyTrace!);await Shot("battle-and-state-strip");
            int money=p.Gold;await Finish();Check("refund directly reaches balance",p.Gold==money+20);
            Check("map strip persists",NRun.Instance.HasNode("ArtuPersistentState/StatusRow"));await Shot("post-victory-state-strip");

            await Battle();foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);
            var cat=await Add(typeof(SuperBecky));var letters=new Dictionary<CardModel,char>();foreach(char c in "ABCD")letters[await Add(typeof(LoyalHound))]=c;
            foreach(char c in "EFGH")letters[await Add(typeof(LoyalHound),PileType.Draw)]=c;
            turn=p.PlayerCombatState!.TurnNumber;int before=Plays.Count;
            var fxWatch=WatchBecky(dir,p,()=>p.PlayerCombatState!.TurnNumber>turn);
            Check("Becky manual accepted",cat.TryManualPlay(null));
            await Until(()=>p.PlayerCombatState!.TurnNumber>turn&&p.PlayerCombatState.Phase==PlayerTurnPhase.Play,"Becky scans and one end turn");
            var fx=await fxWatch;Save("becky-presentation.json",new{fx.shortPlaying,fx.longPlaying,fx.frames,fx.sfxBus});
            Check("Becky short and long audio streams play",fx.shortPlaying&&fx.longPlaying);
            Check("Becky frame sequence advances",fx.frames>1);
            Check("Becky sound uses native SFX bus",fx.sfxBus);
            Check("Becky overlay removed before next turn",!NRun.Instance!.HasNode("ArtuBeckyPresentation"));
            string order=string.Concat(Plays.Skip(before).Where(x=>letters.ContainsKey(x.Card)).Select(x=>letters[x.Card]));
            Save("becky-native.json",new{order,played=Plays.Skip(before).Select(x=>x.Card.Id.ToString())});
            Check("Becky draws four before live eight-card ADGCH scan",order=="ADGCH");await Finish();

            await Battle();
            foreach(var e in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(e);
            var earlyCat=await Add(typeof(SuperBecky));var earlyKill=await Add(typeof(DestructiveStrike));earlyKill.DynamicVars.Damage.BaseValue=999999;
            await Add(typeof(LoyalHound));foreach(int i in Enumerable.Range(0,4))await Add(typeof(LoyalHound),PileType.Draw);
            var interruptedFx=BeckyPresentation.BeforeCard(p,earlyKill);await Task.Delay(250);BeckyPresentation.Clear(p);
            Check("Becky mid-wait cancellation returns safely",await Task.WhenAny(interruptedFx,Task.Delay(2000))==interruptedFx);
            Check("Becky mid-wait cancellation leaves no overlay",!NRun.Instance!.HasNode("ArtuBeckyPresentation"));
            before=Plays.Count;turn=p.PlayerCombatState!.TurnNumber;
            Check("Becky lethal native manual accepted",earlyCat.TryManualPlay(null));
            await Until(()=>!CombatManager.Instance.IsInProgress,"Becky lethal stops combat");await Task.Delay(500);
            Check("Becky lethal only plays the killing card",Plays.Skip(before).Count(x=>x.Card!=earlyCat)==1);
            Check("Becky lethal overlay cleaned",!NRun.Instance!.HasNode("ArtuBeckyPresentation"));
            Save("becky-lethal.json",new{extraAutoCards=Plays.Skip(before).Count(x=>x.Card!=earlyCat),overlayRemaining=NRun.Instance.HasNode("ArtuBeckyPresentation"),combatEnded=!CombatManager.Instance.IsInProgress});

            for(int path=0;path<3;path++)
            {
                Type third=path==0?typeof(AtonementPrayer):path==1?typeof(AncientFaith):typeof(RebellionIntent);
                await Util.AddToDeck(p,ModelDb.GetById<CardModel>(ModelDb.GetId(third)));
                for(int i=1;i<=3;i++)
                {
                    await Battle();await Play(await Add(third));
                    if(path==1){await Use<StoneSlaughter>();await Use<StoneLuxury>();}
                    if(path==2){await Use<Quickfoot>();turn=p.PlayerCombatState!.TurnNumber;PlayerCmd.EndTurn(p,false);await Until(()=>p.PlayerCombatState!.TurnNumber>turn&&p.PlayerCombatState.Phase==PlayerTurnPhase.Play,"rebellion second turn");await PlayerCmd.GainEnergy(500,p);await Use<Quickfoot>();}
                    await Finish();int progress=path==0?state.Devotion:path==1?state.Fanaticism:state.Treason;
                    Check($"native route {path} victory {i} progress",progress==i);
                    var saved=(WitchCardBox)RelicModel.FromSerializable(state.ToSerializable());Check("serialized progress matches",saved.Devotion==state.Devotion&&saved.Fanaticism==state.Fanaticism&&saved.Treason==state.Treason);
                }
                Type final=path==0?typeof(PerfectWorld):path==1?typeof(NewWorld):typeof(RegicidePlan);
                Check($"native route {path} removes originals and grants one final",p.Deck.Cards.All(c=>c.GetType()!=third)&&p.Deck.Cards.Count(c=>c.GetType()==final)==1);
            }
            await Shot("all-evolution-progress");Save("saved-progress.json",state.ToSerializable());
            await NGame.Instance.ReturnToMainMenuAfterRun();Check("returns to main menu",NRun.Instance==null);
            Save("result.json",new{passed=checks.Count,failed=0,checks,actualVictories=12,profile=OS.GetUserDataDir(),manualCardActions=true,artificialResources=true,reloadNotTested=true});
            NGame.Instance.GetTree().Quit(0);
        }
        catch(Exception e){Save("result.json",new{passed=checks.Count,failed=1,checks,error=e.ToString()});GD.PrintErr(e.ToString());NGame.Instance?.GetTree().Quit(1);}
        finally{Active=false;}
    }
    sealed class First:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards,int min,int max)=>Task.FromResult(cards.Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
    static async Task<(bool shortPlaying,bool longPlaying,int frames,bool sfxBus)> WatchBecky(string dir,Player player,Func<bool> done)
    {
        bool shortPlaying=false,longPlaying=false,sfxBus=false;var frames=new HashSet<ulong>();bool shortShot=false,longShot=false;
        var deadline=DateTime.UtcNow.AddSeconds(35);
        while(!done()&&DateTime.UtcNow<deadline)
        {
            await Task.Delay(80);
            var layer=NRun.Instance?.GetNodeOrNull<CanvasLayer>("ArtuBeckyPresentation");
            if(layer==null)continue;
            var sprite=layer.GetNode<TextureRect>("Cat");var voice=layer.GetNode<AudioStreamPlayer>("Voice");
            sfxBus|=voice.Bus=="SFX"&&AudioServer.GetBusIndex("SFX")>=0;
            if(sprite.Visible&&sprite.Texture!=null)frames.Add(sprite.Texture.GetInstanceId());
            bool isLong=(voice.Stream?.GetLength()??0)>2;
            if(voice.Playing){if(isLong)longPlaying=true;else shortPlaying=true;}
            if(voice.Playing&&(!isLong&&!shortShot||isLong&&!longShot))
            {
                await Task.Delay(250); // Let the native renderer draw the newly visible cat.
                if(GodotObject.IsInstanceValid(layer))NGame.Instance!.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,isLong?"becky-rest.png":"becky-above-card.png"));
                if(isLong)longShot=true;else shortShot=true;
            }
        }
        return(shortPlaying,longPlaying,frames.Count,sfxBus);
    }
    public static async Task Reload(string dir)
    {
        try
        {
            var input=SaveManager.Instance.LoadRunSave();if(!input.Success||input.SaveData==null)throw new Exception("missing test save");
            var run=RunState.FromSerializable(input.SaveData);await RunManager.Instance.SetUpSavedSingleplayer(run,input.SaveData);
            await NGame.Instance!.LoadRun(run,input.SaveData.PreFinishedRoom);await Task.Delay(1000);
            var s=Util.State(run.Players[0])!;bool ok=s.Devotion==3&&s.Fanaticism==3&&s.Treason==3&&NRun.Instance!.HasNode("ArtuPersistentState/StatusRow");
            NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir,"reload-progress.png"));
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{passed=ok?1:0,failed=ok?0:1,s.Devotion,s.Fanaticism,s.Treason,reloadedFromDisk=true}));NGame.Instance.GetTree().Quit(ok?0:1);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{failed=1,error=e.ToString()}));NGame.Instance?.GetTree().Quit(1);}
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeSideTurnEnd))]
static class RepairObserveArmyBlock
{
    static void Postfix(ICombatState combatState,CombatSide side,ref Task __result)
    {
        if(!RepairScenario.Active||side!=CombatSide.Player)return;
        var before=combatState.HittableEnemies.Select(e=>(e,e.CurrentHp,e.Block)).ToArray();__result=After(__result,before);
    }
    static async Task After(Task original,(Creature e,int hp,int block)[] before)
    {
        await original;
        if(RepairScenario.ArmyTrace==null)RepairScenario.ArmyTrace=before.Select(x=>new{hpBefore=x.hp,hpAfter=x.e.CurrentHp,blockBefore=x.block,blockAfter=x.e.Block,timing="after player-end hooks, before enemy turn"}).ToArray();
    }
}
