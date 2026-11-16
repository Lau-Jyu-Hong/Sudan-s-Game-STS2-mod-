using System.Text.Json;
using ArtuMod;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;

namespace ArtuAutoplayLab;

// Isolated native UI check, never shipped with the production mod.
public static class EvolutionScenario
{
    public static async Task Run(string dir)
    {
        var checks=new List<string>();var error="";
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Until(Func<bool> predicate,string name){var end=DateTime.UtcNow.AddSeconds(45);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException(name);await Task.Delay(60);}}
        try
        {
            TestMode.IsOn=false;Engine.TimeScale=3;
            bool evolve=System.Environment.GetEnvironmentVariable("ARTU_EVOLUTION_CHOICE")=="evolve";
            V04Scenario.RewardScreen=null;
            var run=await NGame.Instance!.StartNewSingleplayerRun(ModelDb.Character<Artu>(),true,ActModel.GetDefaultList(),[],"ARTU_EVOLUTION_UI",GameMode.Standard,0);
            var player=run.Players[0];var state=Util.State(player)!;
            state.BattleWins=3;state.Anger=1;EvolutionRewards.Qualify(state,6);
            // Avoid the starter relic's interactive opening Sultan selection in this UI fixture.
            await CardPileCmd.RemoveFromDeck(player.Deck.Cards.OfType<SultanCard>().ToArray());
            await RunManager.Instance.EnterMapPointInternal(1,MapPointType.Monster,null,true);
            await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"combat ready");
            var combat=((CombatRoom)run.CurrentRoom!).CombatState;
            foreach(var enemy in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(enemy);
            var target=combat.HittableEnemies.First();
            var finisher=combat.CreateCard<DestructiveStrike>(player);finisher.DynamicVars.Damage.BaseValue=999999;
            await CardPileCmd.Add(finisher,PileType.Hand);await PlayerCmd.GainEnergy(10,player);
            Check("native finishing card accepted",finisher.TryManualPlay(target));
            await Until(()=>!CombatManager.Instance.IsInProgress,"combat victory");
            await Until(()=>V04Scenario.RewardScreen!=null,"native reward screen");
            var screen=V04Scenario.RewardScreen!;
            var rewards=(RewardsSet)AccessTools.Field(typeof(NRewardsScreen),"_rewardsSet").GetValue(screen)!;
            Check("native reward has ordinary items and both independent custom slots",rewards.Rewards.Any(r=>r is ThoughtEvolutionSet)&&rewards.Rewards.Any(r=>r is ForgivenessReward)&&rewards.Rewards.Any(r=>r is not ThoughtEvolutionSet and not ForgivenessReward));
            var controls=(List<Control>)AccessTools.Field(typeof(NRewardsScreen),"_rewardButtons").GetValue(screen)!;
            var buttons=controls.OfType<NRewardButton>().ToArray();
            static IEnumerable<Control> Descendants(Control root)
            {foreach(var child in root.GetChildren().OfType<Control>()){yield return child;foreach(var deeper in Descendants(child))yield return deeper;}}
            await Task.Delay(700);
            var linked=controls.OfType<NLinkedRewardSet>().Single();
            var choices=Descendants(linked).OfType<NRewardButton>().Where(b=>b.Reward is ThoughtEvolutionChoice).ToArray();
            Check("native linked set renders two evolution buttons",choices.Length==2);
            var info=new{battleWins=state.BattleWins,pending=state.PendingEvolutions,anger=state.Anger,rewards=rewards.Rewards.Select(r=>r.GetType().Name).ToArray(),controls=controls.Select(c=>c.GetType().Name).ToArray(),buttons=buttons.Select(b=>b.Reward?.GetType().Name).ToArray(),linkedButtons=choices.Select(b=>b.Reward?.Description.GetFormattedText()).ToArray()};
            File.WriteAllText(Path.Combine(dir,"reward-ui.json"),JsonSerializer.Serialize(info,new JsonSerializerOptions{WriteIndented=true}));
            using(var image=NGame.Instance.GetViewport().GetTexture().GetImage())image.SavePng(Path.Combine(dir,"reward-ui.png"));
            var forgiving=buttons.Single(b=>b.Reward is ForgivenessReward);
            var take=(Task)AccessTools.Method(typeof(NRewardButton),"GetReward").Invoke(forgiving,null)!;
            await take;
            Check("native forgiveness button removes exactly one anger",state.Anger==0&&state.ForgivenessClaimedVictory==4);
            Check("native reward leaves thought qualification pending",EvolutionRewards.Qualified(state,6));
            var decision=(Task)AccessTools.Method(typeof(NRewardButton),"GetReward").Invoke(choices[evolve?1:0],null)!;
            await decision;
            if(evolve)Check("native evolve button consumes qualification and adds next thought",!EvolutionRewards.Qualified(state,6)&&state.EvolutionResolvedVictory==4&&player.Deck.Cards.Count(c=>c is PerfectWorld)==1);
            else Check("native retain button leaves qualification and current thought unchanged",EvolutionRewards.Qualified(state,6)&&state.EvolutionResolvedVictory==4&&!player.Deck.Cards.Any(c=>c is PerfectWorld));
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=0,checks},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){error=e.ToString();File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=1,checks,error},new JsonSerializerOptions{WriteIndented=true}));GD.PrintErr(error);}
        NGame.Instance?.GetTree().Quit(error.Length==0?0:1);
    }
}
