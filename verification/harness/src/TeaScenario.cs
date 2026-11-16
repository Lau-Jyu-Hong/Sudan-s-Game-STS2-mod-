using Godot;
using HarmonyLib;
using System.Text.Json;
using ArtuMod;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Encounters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace ArtuAutoplayLab;

// Finite integration scenario, deliberately separate from normal gameplay/balance testing.
public static class TeaScenario
{
    static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true};
    public static async Task Run(string directory)
    {
        var checks=new List<string>();var turns=new List<object>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(directory,name),JsonSerializer.Serialize(value,JsonOptions));
        async Task Until(Func<bool> ready,string reason,int seconds=30)
        {var deadline=DateTime.UtcNow.AddSeconds(seconds);while(!ready()){if(DateTime.UtcNow>deadline)throw new TimeoutException(reason);await Task.Delay(100);}}
        try
        {
            Save("conditions.json",new{mode="Finite tea-party integration scenario",profile=OS.GetUserDataDir(),
                seed="ARTU_TEA_SCENARIO",testChanges="Player max HP 1000/current HP 900, one added bronze slaughter card; end turns without playing; force enemy deaths after eight enemy turns",balanceTest=false});
            await Task.Delay(1000);
            var run=await NGame.Instance!.StartNewSingleplayerRun(MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<Artu>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1),false,
                ActModel.GetDefaultList(),[],"ARTU_TEA_SCENARIO",GameMode.Standard,false,0);
            run.CurrentActIndex=2;
            await RunManager.Instance.GenerateMap();
            var player=run.Players[0];
            await CreatureCmd.SetMaxHp(player.Creature,1000);
            await CreatureCmd.SetCurrentHp(player.Creature,900);
            await Util.AddToDeck(player,ModelDb.Card<BronzeSlaughter>());
            using var selector=MegaCrit.Sts2.Core.CardSelection.CardSelectorStack.SetTestSelector(new AutoSlayCardSelector(new Rng(1234)));
            await RunManager.Instance.EnterRoomDebug(RoomType.Event,model:ModelDb.Event<RoundTeaParty>());
            await Until(()=>RunManager.Instance.EventSynchronizer.GetLocalEvent().CurrentOptions.Count>0,"Tea event options");
            var model=RunManager.Instance.EventSynchronizer.GetLocalEvent();
            Check("Real tea event is shared and declares the custom encounter",model.IsShared&&model.CanonicalEncounter is TeaPartyEncounter);
            var option=model.CurrentOptions.Select((o,i)=>(o,i)).FirstOrDefault(x=>x.o.TextKey.Contains("ARTU_TEA_CHALLENGE"));
            Check("Real tea event exposes the bronze-slaughter challenge",option.o!=null);
            int costCards=player.Deck.Cards.Count(TeaPartyChallenge.Eligible);
            RunManager.Instance.EventSynchronizer.ChooseLocalOption(option.i);
            await Until(()=>run.CurrentRoom is CombatRoom { Encounter:TeaPartyEncounter }&&CombatManager.Instance.IsInProgress,"Tea combat begins");
            var room=(CombatRoom)run.CurrentRoom!;
            var king=room.Enemies.Single(c=>c.Monster is TeaCrownKnight);
            var captain=room.Enemies.Single(c=>c.Monster is TeaMercenaryCaptain);
            var warlord=room.Enemies.Single(c=>c.Monster is TeaWarlord);
            await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"First player turn");
            Check("Real challenge removes exactly one eligible deck card",player.Deck.Cards.Count(TeaPartyChallenge.Eligible)==costCards-1);
            Check("Real encounter spawns only the three designed enemies",room.Enemies.Count()==3&&king.MaxHp==140&&warlord.MaxHp==85&&captain.MaxHp==65);
            Check("Real knight placeholders instantiate",room.Enemies.All(c=>c.GetCreatureNode()?.Visuals!=null));
            if(DisplayServer.GetName()!="headless")
            {
                await Task.Delay(1000);
                await NGame.Instance.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                NGame.Instance.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory,"tea-party.png"));
            }
            var ctx=new BlockingPlayerChoiceContext();
            await CreatureCmd.Damage(ctx,king,100,ValueProp.Unpowered|ValueProp.Unblockable,player.Creature);
            Check("Real crown absorbs burst above the 14-HP budget",king.CurrentHp==126);
            var expectedSoldiers=new[]{2,2,2,2,5,5,5,5};
            var expectedVeterans=new[]{0,2,2,2,2,5,5,5};
            var expectedElites=new[]{0,0,2,2,2,2,5,5};
            var expectedKing=new[]{"ROYAL_STRIKE","HEAVY_SLASH","ROYAL_GUARD"};
            for(int turn=1;turn<=8;turn++)
            {
                await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play&&player.PlayerCombatState.TurnNumber==turn,$"Player turn {turn}");
                int beforeHp=player.Creature.CurrentHp;
                var intent=room.Enemies.Select(c=>new{enemy=c.Monster!.Title.GetFormattedText(),move=c.Monster.NextMove.Id}).ToArray();
                Check($"Real turn {turn} crown follows the three-action cycle",king.Monster!.NextMove.Id==expectedKing[(turn-1)%3]);
                var expectedWarlord=turn==1?"SUPPLY_BLOCKADE":turn==2?"REQUISITION":turn%2==1?"BREAK_FORMATION":"FATIGUE";
                Check($"Real turn {turn} warlord follows opening debuffs then alternating pressure",warlord.Monster!.NextMove.Id==expectedWarlord);
                PlayerCmd.EndTurn(player,canBackOut:false);
                await Until(()=>player.PlayerCombatState?.Phase==PlayerTurnPhase.Play&&player.PlayerCombatState.TurnNumber==turn+1,$"Enemy turn {turn} resolves");
                int soldiers=captain.GetPowerAmount<EnemySoldierPower>();
                int veterans=captain.GetPowerAmount<EnemyVeteranPower>();
                int elites=captain.GetPowerAmount<EnemyElitePower>();
                Check($"Real turn {turn} recruitment follows the four-action growth loop",soldiers==expectedSoldiers[turn-1]&&veterans==expectedVeterans[turn-1]&&elites==expectedElites[turn-1]);
                turns.Add(new{turn,intent,beforeHp,afterHp=player.Creature.CurrentHp,soldiers,veterans,elites,
                    recruitCount=((TeaMercenaryCaptain)captain.Monster!).RecruitCount,captainBlock=captain.Block,captainPlating=captain.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.PlatingPower>(),
                    hand=player.PlayerCombatState!.Hand.Cards.Select(c=>new{id=c.Id.ToString(),cost=c.EnergyCost.GetWithModifiers(CostModifiers.All)}).ToArray()});
                Save("turns.json",turns);
            }
            Check("Real captain expands twice after eight turns",((TeaMercenaryCaptain)captain.Monster!).RecruitCount==4);
            int beforeRelics=player.Relics.Count;
            await CreatureCmd.Kill(captain,true);await CreatureCmd.Kill(warlord,true);
            Check("Real last-ally death removes protection immediately",!king.HasPower<RoyalProtectionPower>());
            int beforeHeal=player.Creature.CurrentHp;
            await CreatureCmd.Kill(king,true);
            // This test issues Kill outside a card action; normal actions check victory afterwards.
            await CombatManager.Instance.CheckWinCondition();
            await Until(()=>!CombatManager.Instance.IsInProgress&&player.Relics.Count>=beforeRelics+2,"Tea victory rewards",45);
            Check("Real victory grants two relics",player.Relics.Count==beforeRelics+2);
            Check("Real victory heals at least the promised 20 HP",player.Creature.CurrentHp>=Math.Min(player.Creature.MaxHp,beforeHeal+20));
            Save("result.json",new{passed=checks.Count,failed=0,checks,turns,finalHp=player.Creature.CurrentHp,
                relics=player.Relics.Select(r=>new{id=r.Id.ToString(),name=r.Title.GetFormattedText()}).ToArray()});
            NGame.Instance.GetTree().Quit(0);
        }
        catch(Exception exception)
        {
            Save("result.json",new{passed=checks.Count,failed=1,checks,turns,error=exception.ToString()});
            GD.PrintErr(exception.ToString());NGame.Instance?.GetTree().Quit(1);
        }
    }
}
