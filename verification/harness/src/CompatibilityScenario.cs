using System.Net.Http.Json;
using System.Text.Json;
using ArtuMod;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace ArtuAutoplayLab;

internal static class CompatibilityScenario
{
    internal static async Task Run(string output,string mode)
    {
        var checks=new List<string>();
        void Check(string label,bool condition)
        {if(!condition)throw new InvalidOperationException(label);checks.Add(label);}
        async Task Until(Func<bool> ready,string label,int seconds=40)
        {
            var end=DateTime.UtcNow.AddSeconds(seconds);
            while(!ready()){if(DateTime.UtcNow>=end)throw new TimeoutException(label);await Task.Delay(70);}
        }
        var exit=0;
        try
        {
            Engine.TimeScale=4;
            using var selector=CardSelectorStack.SetTestSelector(
                new MegaCrit.Sts2.Core.AutoSlay.Helpers.AutoSlayCardSelector(new MegaCrit.Sts2.Core.Random.Rng(731)));
            if(mode=="compat-recovery")
            {
                var read=SaveManager.Instance.LoadRunSave();
                var save=read.SaveData??throw new InvalidOperationException("No isolated copy to load");
                var run=RunState.FromSerializable(save);
                Check("original Artu models load from the copied save",run.Players.Count==1&&run.Players[0].Character is Artu&&run.Players[0].Deck.Cards.Any(c=>c is SultanCard));
                var player=run.Players[0];var startingGold=player.Gold;var startingDeck=player.Deck.Cards.Select(c=>c.Id).ToArray();
                var box=Util.State(player)!;var wins=box.BattleWins;
                await RunManager.Instance.SetUpSavedSingleplayer(run,save);
                await NGame.Instance!.LoadRun(run,save.PreFinishedRoom);
                await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"copied combat accepts input");
                Check("load does not change copied gold or deck",player.Gold==startingGold&&startingDeck.SequenceEqual(player.Deck.Cards.Select(c=>c.Id)));
                var combat=((CombatRoom)run.CurrentRoom!).CombatState;
                foreach(var enemy in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(enemy);
                var target=combat.HittableEnemies.First();
                var finisher=combat.CreateCard<DestructiveStrike>(player);finisher.DynamicVars.Damage.BaseValue=999999;
                await CardPileCmd.Add(finisher,PileType.Hand);await PlayerCmd.GainEnergy(10,player);
                Check("copied combat accepts a normal finishing card",finisher.TryManualPlay(target));
                await Until(()=>!CombatManager.Instance.IsInProgress&&V04Scenario.RewardScreen!=null,"copied combat finishes and shows rewards");
                Check("copied combat victory counted exactly once",box.BattleWins==wins+1);
                if(SaveManager.Instance.CurrentRunSaveTask is {} writing)await writing;
                var persisted=SaveManager.Instance.LoadRunSave().SaveData;
                Check("isolated post-victory save is readable",persisted?.PreFinishedRoom!=null);
            }
            else
            {
                var player=Player.CreateForNewRun<Artu>(UnlockState.all,1);
                var run=await NGame.Instance!.StartNewSingleplayerRun(player,true,ActModel.GetDefaultList(),[],"ARTU_COMPAT_SHOP",GameMode.Standard,false);
                await PlayerCmd.GainGold(700,player);
                await RunManager.Instance.EnterMapPointInternal(1,MapPointType.Shop,null,true);
                await Until(()=>NRun.Instance?.MerchantRoom!=null,"first merchant room");
                using var browser=new System.Net.Http.HttpClient(new HttpClientHandler{UseProxy=false}){BaseAddress=new Uri("http://localhost:8770"),Timeout=TimeSpan.FromSeconds(10)};
                async Task<JsonElement> State()=>await browser.GetFromJsonAsync<JsonElement>("state");
                async Task<bool> Action(string mode,string? type=null,int index=0)
                {using var res=await browser.PostAsJsonAsync("action",new{action="shop_action",shop_action=mode,choice_type=type,item_index=index});return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("Success").GetBoolean();}
                var state=await State();
                Check("bridge reads current shop inventory",state.GetProperty("Shop").GetProperty("CharacterCards").GetArrayLength()>0);
                Check("bridge opens shop inventory",await Action("open_inventory"));
                var gold=player.Gold;var deck=player.Deck.Cards.Count;
                Check("bridge dispatches native synchronized purchase",await Action("buy","character_card"));
                await Until(()=>player.Deck.Cards.Count==deck+1,"native card purchase completes");
                Check("purchase spends gold",player.Gold<gold);
                Check("bridge dispatches native card removal",await Action("remove_card"));
                await Until(()=>player.Deck.Cards.Count==deck,"native card removal completes");
                var box=Util.State(player)!;box.NoBuyingShops=1;
                await RunManager.Instance.EnterMapPointInternal(2,MapPointType.Shop,null,true);
                await Until(()=>box.ActiveShopBan&&NRun.Instance?.MerchantRoom!=null,"shop ban applied");
                var inv=RunManager.Instance.MerchantSynchronizer.GetLocalInventory();
                gold=player.Gold;deck=player.Deck.Cards.Count;
                Check("active ban rejects native and bridge purchase",!await RunManager.Instance.MerchantSynchronizer.PurchaseLocalEntry(inv.CharacterCardEntries.First())&&!await Action("buy","character_card"));
                Check("active ban rejects card removal and preserves resources",!await Action("remove_card")&&player.Gold==gold&&player.Deck.Cards.Count==deck);
            }
        }
        catch(Exception error)
        {
            exit=1;GD.PrintErr(error.ToString());
            File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());
        }
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=exit,checks},new JsonSerializerOptions{WriteIndented=true}));
        NGame.Instance?.GetTree().Quit(exit);
    }
}
