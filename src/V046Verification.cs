using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

// Regression checks for the incremental v0.4.3 design update. These are
// mechanism tests; the native reward screen still requires a live UI pass.
public static class V046Verification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;
        var checks=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("v046 "+name);}
        try
        {
            using(var f=new V04Verification.Fixture())
            {
                f.State.BattleWins=4;f.State.Anger=2;
                EvolutionRewards.Qualify(f.State,6);EvolutionRewards.Qualify(f.State,7);
                var rewards=new List<Reward>();
                Check("evolution and forgiveness add two independent reward slots",EvolutionRewards.Add(f.Player,rewards)&&rewards.Count==2&&rewards.Any(r=>r is ThoughtEvolutionSet)&&rewards.Any(r=>r is ForgivenessReward));
                Check("repeat reward generation does not duplicate slots",!EvolutionRewards.Add(f.Player,rewards)&&rewards.Count==2);
                var saved=rewards.Select(r=>r.ToSerializable()).ToArray();
                Check("custom reward types survive native serialization",saved.Select(s=>Reward.FromSerializable(s,f.Player)).Any(r=>r is ThoughtEvolutionSet)&&saved.Select(s=>Reward.FromSerializable(s,f.Player)).Any(r=>r is ForgivenessReward));
                var state=(WitchCardBox)RelicModel.FromSerializable(f.State.ToSerializable());
                Check("pending evolution and battle wins survive relic save",EvolutionRewards.Qualified(state,6)&&EvolutionRewards.Qualified(state,7)&&state.BattleWins==4&&state.Anger==2);
                Check("forgiveness removes one anger only once",EvolutionRewards.Forgive(f.Player,4).GetAwaiter().GetResult()&&f.State.Anger==1&&!EvolutionRewards.Forgive(f.Player,4).GetAwaiter().GetResult());
                Check("retaining leaves permanent qualification",EvolutionRewards.Decide(f.Player,6,4,false).GetAwaiter().GetResult()&&EvolutionRewards.Qualified(f.State,6));
                f.State.BattleWins=5;
                Check("other qualified route gets its turn after retaining",EvolutionRewards.Next(f.State)==7);
                Check("retained thought remains eligible on later victories",EvolutionRewards.Add(f.Player,new List<Reward>())&&EvolutionRewards.Qualified(f.State,6));
                Check("second route can also be retained",EvolutionRewards.Decide(f.Player,7,5,false).GetAwaiter().GetResult()&&EvolutionRewards.Qualified(f.State,7));
                f.State.BattleWins=6;
                Check("rotation returns to first qualified route",EvolutionRewards.Next(f.State)==6);
                f.AddDeck<AtonementPrayer>();
                Check("later evolution replaces the old thought once",EvolutionRewards.Decide(f.Player,6,6,true).GetAwaiter().GetResult()&&!EvolutionRewards.Qualified(f.State,6)&&!f.Player.Deck.Cards.Any(c=>c is AtonementPrayer)&&f.Player.Deck.Cards.Count(c=>c is PerfectWorld)==1);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Apply<PerfectWorldPower>(1);
                var power=f.Player.Creature.GetPower<PerfectWorldPower>()!;
                var s=f.Card<StoneSlaughter>();var normal=f.Card<Quickfoot>();
                Check("auto use alone does not lock perfect-world route",SultanUsage.FirstManualIsSultan(f.Player)==null);
                SultanUsage.RecordManualStart(s);
                Check("first manual Sultan waives later Sultan cost",PerfectWorldPower.SultanRoute(f.Player)&&V04Replay.Free(s)&&!power.TryModifyEnergyCostInCombat(normal,1,out _));
                f.NextTurn();SultanUsage.RecordManualStart(normal);
                Check("next turn non-Sultan route is exclusive",!PerfectWorldPower.SultanRoute(f.Player)&&!V04Replay.Free(s)&&power.TryModifyEnergyCostInCombat(normal,1,out var free)&&free==0);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Apply<CrimePower>(5);var power=f.Player.Creature.GetPower<CrimePower>()!;
                var first=f.Card<Quickfoot>();var second=f.Card<Quickfoot>();
                f.Await(power.AfterCardDrawnEarly(f.Context,first,true));f.Await(power.AfterCardDrawnEarly(f.Context,second,true));
                Check("crime tier five taxes only first drawn non-Sultan",first.EnergyCost.GetWithModifiers(CostModifiers.All)==first.EnergyCost.Canonical+1&&second.EnergyCost.GetWithModifiers(CostModifiers.All)==second.EnergyCost.Canonical);
                f.NextTurn();var third=f.Card<Quickfoot>();f.Await(power.AfterCardDrawnEarly(f.Context,third,true));
                Check("crime draw tax renews next turn",third.EnergyCost.GetWithModifiers(CostModifiers.All)==third.EnergyCost.Canonical+1);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<SilverLust>();
                Check("silver lust enemy 3/3 and player intangible 1 regen 6",f.Enemy.GetPowerAmount<RegenPower>()==3&&f.Enemy.GetPowerAmount<PlatingPower>()==3&&f.Player.Creature.GetPowerAmount<IntangiblePower>()==1&&f.Player.Creature.GetPowerAmount<RegenPower>()==6);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Await(CreatureCmd.GainBlock(f.Player.Creature,4,ValueProp.Unpowered,null));f.Apply<PassPower>(1);f.Play<StoneConquest>();
                Check("waived stone conquest preserves block and uses four-block ratio",f.Player.Creature.Block==4&&f.Enemy.GetPowerAmount<WeakPower>()==1&&f.Enemy.GetPowerAmount<VulnerablePower>()==1&&PaymentLedger.ThisTurn(f.Player)==0);
            }
            return checks;
        }
        finally{V04Verification.Fixture.ResetCombat();TestMode.IsOn=false;}
    }
}
