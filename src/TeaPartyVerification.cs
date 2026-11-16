using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Entities.Powers;
namespace ArtuMod;

public static class TeaPartyVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;
        var passed=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);passed.Add(name);}
        void Await(Task task)=>task.GetAwaiter().GetResult();
        try
        {
            ResetTestCombat();
            Check("Tea encounter is registered and event-only",!ModelDb.Encounter<TeaPartyEncounter>().ShouldGiveRewards&&
                !BaseLib.Patches.Content.CustomContentDictionary.CustomEncounters.Any(e=>e is TeaPartyEncounter));
            Check("Tea three models registered and localized",new MonsterModel[]{ModelDb.Monster<TeaCrownKnight>(),ModelDb.Monster<TeaWarlord>(),ModelDb.Monster<TeaMercenaryCaptain>()}.All(m=>m.Title.Exists()));
            Check("Tea knight placeholder scenes and encounter exist",ResourceLoader.Exists(ModelDb.Monster<TeaCrownKnight>().CustomVisualPath)&&
                ResourceLoader.Exists(ModelDb.Monster<TeaWarlord>().CustomVisualPath)&&ResourceLoader.Exists(ModelDb.Monster<TeaMercenaryCaptain>().CustomVisualPath)&&
                ResourceLoader.Exists(ModelDb.Encounter<TeaPartyEncounter>().CustomScenePath));
            Check("Tea cost excludes stone and non-slaughter cards",!TeaPartyChallenge.Eligible(ModelDb.Card<StoneSlaughter>())&&!TeaPartyChallenge.Eligible(ModelDb.Card<GoldLuxury>())&&TeaPartyChallenge.Eligible(ModelDb.Card<BronzeSlaughter>()));
            var consent=new TeaChallengeConsent([1,2]);var waiting=consent.Vote(1,true);
            Check("Tea requires every participant to confirm",!waiting.IsCompleted);
            Check("Tea one decline rejects challenge",!consent.Vote(2,false).GetAwaiter().GetResult());
            consent=new TeaChallengeConsent([1,2]);consent.Vote(1,true);
            Check("Tea all participants accept challenge",consent.Vote(2,true).GetAwaiter().GetResult());
            for(int count=1;count<=4;count++)
            {
                var players=Enumerable.Range(1,count).Select(i=>Player.CreateForNewRun<Artu>(UnlockState.all,(ulong)i)).ToArray();
                var run=RunState.CreateForTest(players,seed:"TEA_VERIFY_"+count);run.CurrentActIndex=2;
                var combat=new CombatState(ModelDb.Encounter<TeaPartyEncounter>().ToMutable(),run,multiplayerScalingModel:run.MultiplayerScalingModel);
                foreach(var player in players)combat.AddPlayer(player);
                var king=combat.CreateCreature(ModelDb.Monster<TeaCrownKnight>().ToMutable(),CombatSide.Enemy,"first");combat.AddCreature(king);
                var captain=combat.CreateCreature(ModelDb.Monster<TeaMercenaryCaptain>().ToMutable(),CombatSide.Enemy,"second");combat.AddCreature(captain);
                var warlord=combat.CreateCreature(ModelDb.Monster<TeaWarlord>().ToMutable(),CombatSide.Enemy,"third");combat.AddCreature(warlord);
                CombatManager.Instance.SetUpCombat(combat);
                var turnState=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;
                AccessTools.Property(turnState.GetType(),"IsInProgress").SetValue(turnState,true);
                foreach(var enemy in combat.Enemies)Await(enemy.Monster!.AfterAddedToRoom());
                var ctx=new ThrowingPlayerChoiceContext();
                Check($"Tea {count}p HP uses standard act-three scaling",king.MaxHp==new[]{140,336,504,672}[count-1]&&warlord.MaxHp==new[]{85,204,306,408}[count-1]&&captain.MaxHp==new[]{65,156,234,312}[count-1]);
                Check($"Tea {count}p initial plating scaled once",captain.GetPowerAmount<PlatingPower>()==new[]{10,30,50,70}[count-1]);
                var protection=king.GetPower<RoyalProtectionPower>()!;
                Check($"Tea {count}p crown limit rounds up",protection.Limit==new[]{14,34,51,68}[count-1]);
                Await(protection.BeforeSideTurnStart(ctx,CombatSide.Player,combat.PlayerCreatures,combat));
                int hp=king.CurrentHp;
                Await(CreatureCmd.Damage(ctx,king,7,ValueProp.Unpowered|ValueProp.Unblockable,players[0].Creature));
                Await(CreatureCmd.Damage(ctx,king,500,ValueProp.Unpowered|ValueProp.Unblockable,players[^1].Creature));
                Check($"Tea {count}p crown shares cumulative cap including non-attack damage",hp-king.CurrentHp==protection.Limit);
                Await(protection.BeforeSideTurnStart(ctx,CombatSide.Player,combat.PlayerCreatures,combat));
                Await(CreatureCmd.SetCurrentHp(king,1));
                Check($"Tea {count}p extra player turn and direct HP setter cannot reset cap",hp-king.CurrentHp==protection.Limit);
                combat.RoundNumber++;
                Await(protection.BeforeSideTurnStart(ctx,CombatSide.Player,combat.PlayerCreatures,combat));
                hp=king.CurrentHp;Await(CreatureCmd.SetCurrentHp(king,1));
                Check($"Tea {count}p direct HP loss obeys next-round allowance",hp-king.CurrentHp==protection.Limit);
                Await(CreatureCmd.LoseBlock(ctx,captain,captain.Block,players[0].Creature));
                Await(((TeaCrownKnight)king.Monster!).ProtectAlly());
                Check($"Tea {count}p crown protects lowest-HP ally with block scaled once",captain.Block==new[]{20,40,72,96}[count-1]&&warlord.Block==0);
                Await(((TeaMercenaryCaptain)captain.Monster!).Recruit(1));
                var soldiers=captain.GetPower<EnemySoldierPower>()!;
                var before=players.Select(p=>p.Creature.CurrentHp).ToArray();
                int beforeBlock=captain.Block;Await(soldiers.Trigger(ctx));
                Check($"Tea {count}p soldiers attack all players without multiplying counts",players.Select((p,i)=>before[i]-p.Creature.CurrentHp).All(n=>n==4)&&soldiers.Amount==2);
                Check($"Tea {count}p soldier block scales once",captain.Block-beforeBlock==new[]{2,4,7,9}[count-1]);
                Await(((TeaMercenaryCaptain)captain.Monster!).Recruit(2));
                Await(((TeaMercenaryCaptain)captain.Monster!).Recruit(3));
                Check($"Tea {count}p first recruitment cycle base damage 16",((TeaMercenaryCaptain)captain.Monster!).ProjectedDamage(0)==16);
                int plate=captain.GetPowerAmount<PlatingPower>();Await(((TeaMercenaryCaptain)captain.Monster!).Expand());
                Check($"Tea {count}p expansion grows recruits and plating once",((TeaMercenaryCaptain)captain.Monster!).RecruitCount==3&&captain.GetPowerAmount<PlatingPower>()-plate==new[]{2,6,10,14}[count-1]);
                Await(PowerCmd.ModifyAmount(ctx,captain.GetPower<EnemyVeteranPower>()!,7,captain,null));
                // No existing vulnerability/weakness: all nine veterans should hit each player for 18+5.
                before=players.Select(p=>p.Creature.CurrentHp).ToArray();
                Await(captain.GetPower<EnemyVeteranPower>()!.Trigger(ctx));
                Check($"Tea {count}p veteran vigor and thresholds affect whole team",players.Select((p,i)=>before[i]-p.Creature.CurrentHp).All(n=>n==23)&&
                    players.All(p=>p.Creature.GetPowerAmount<VulnerablePower>()==1&&p.Creature.GetPowerAmount<WeakPower>()==1)&&!captain.HasPower<VigorPower>());
                foreach(var player in players)
                {
                    Await(PowerCmd.Remove<VulnerablePower>(player.Creature));Await(PowerCmd.Remove<WeakPower>(player.Creature));
                    AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(player.PlayerCombatState,2);
                }
                Await(((TeaWarlord)warlord.Monster!).Blockade());
                foreach(var player in players)AccessTools.Property(typeof(PowerModel),"AmountOnTurnStart").SetValue(player.Creature.GetPower<SupplyBlockadePower>(),1);
                Check($"Tea {count}p blockade affects every player",players.All(p=>p.Creature.GetPower<SupplyBlockadePower>()!.ModifyHandDraw(p,5)==4));
                foreach(var player in players)
                {
                    var blockade=player.Creature.GetPower<SupplyBlockadePower>()!;
                    Await(blockade.AfterSideTurnStart(CombatSide.Player,combat.PlayerCreatures,combat));
                    Check($"Tea {count}p player {player.NetId} blockade expires after start draw",!player.Creature.HasPower<SupplyBlockadePower>());
                }
                Await(((TeaWarlord)warlord.Monster!).Requisition());
                foreach(var player in players)
                {
                    AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(player.PlayerCombatState,3);
                    var first=combat.CreateCard<DispatchTroops>(player);var second=combat.CreateCard<DispatchTroops>(player);
                    var tax=player.Creature.GetPower<RequisitionPower>()!;
                    Await(tax.AfterCardDrawnEarly(ctx,first,true));Await(tax.AfterCardDrawnEarly(ctx,second,true));
                    Check($"Tea {count}p player {player.NetId} requisition taxes only first draw",first.EnergyCost.GetWithModifiers(CostModifiers.All)==2&&second.EnergyCost.GetWithModifiers(CostModifiers.All)==1);
                    first.EnergyCost.EndOfTurnCleanup();
                    Check($"Tea {count}p player {player.NetId} requisition cost expires",first.EnergyCost.GetWithModifiers(CostModifiers.All)==1);
                    foreach(var template in new[]{ModelDb.AllCards.First(c=>c.EnergyCost.CostsX),ModelDb.Card<Wound>()})
                    {
                        AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(player.PlayerCombatState,2);
                        Await(PowerCmd.Apply<RequisitionPower>(ctx,player.Creature,1,warlord,null));
                        AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(player.PlayerCombatState,3);
                        var special=combat.CreateCard(template,player);var normal=combat.CreateCard<DispatchTroops>(player);
                        int originalCost=special.EnergyCost.GetWithModifiers(CostModifiers.All);
                        var specialTax=player.Creature.GetPower<RequisitionPower>()!;
                        Await(specialTax.AfterCardDrawnEarly(ctx,special,true));Await(specialTax.AfterCardDrawnEarly(ctx,normal,true));
                        Check($"Tea {count}p player {player.NetId} first draw {template.Id} follows Hextech X/unplayable semantics",
                            special.EnergyCost.GetWithModifiers(CostModifiers.All)==originalCost&&normal.EnergyCost.GetWithModifiers(CostModifiers.All)==1&&!player.Creature.HasPower<RequisitionPower>());
                    }
                }
                // Killing the final allies must release the crown immediately within the current round.
                // Startup fixtures are not a live RunManager room, so emulate the death callback.
                // The separate real-room scenario validates the actual CreatureCmd.Kill path.
                AccessTools.Method(typeof(Creature),"SetCurrentHpInternal").Invoke(captain,[0m]);
                AccessTools.Method(typeof(Creature),"SetCurrentHpInternal").Invoke(warlord,[0m]);
                Await(protection.AfterDeath(ctx,warlord,false,0));
                hp=king.CurrentHp;Await(CreatureCmd.Damage(ctx,king,20,ValueProp.Unpowered|ValueProp.Unblockable,players[0].Creature));
                Check($"Tea {count}p final ally death immediately releases crown protection",hp-king.CurrentHp==20&&!king.HasPower<RoyalProtectionPower>());
                ResetTestCombat();
            }
            return passed;
        }
        finally{ResetTestCombat();TestMode.IsOn=false;}
    }
    static void ResetTestCombat()
    {
        // These startup tests run before RunManager initializes an action synchronizer.
        // Cancel only the synthetic combat; production Reset also needs that synchronizer.
        var field=AccessTools.Field(typeof(CombatManager),"_turnState");
        if(field.GetValue(CombatManager.Instance) is {} turn)AccessTools.Method(turn.GetType(),"Cancel").Invoke(turn,null);
        field.SetValue(CombatManager.Instance,null);CombatManager.Instance.History.Clear();
    }
}
