using ArtuMod;
using Godot;
using HarmonyLib;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuAutoplayLab;

// Native controls and ordinary manual-play actions; artificial test deck and energy, not a balance run.
public static class ThirdBatchScenario
{
    static IEnumerable<Node> Walk(Node root)
    {foreach(var n in root.GetChildren()){yield return n;foreach(var child in Walk(n))yield return child;}}
    public static async Task Run(string dir)
    {
        var checks=new List<string>();string? error=null;
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add(name);}
        async Task Until(Func<bool> ready,string name){var end=DateTime.UtcNow.AddSeconds(35);while(!ready()){if(DateTime.UtcNow>end)throw new TimeoutException(name);await Task.Delay(75);}}
        async Task Shot(string name){await Task.Delay(500);using var image=NGame.Instance!.GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(dir,name+".png"));}
        try
        {
            TestMode.IsOn=false;Engine.TimeScale=3;
            var run=await NGame.Instance!.StartNewSingleplayerRun(MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<Artu>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1),true,ActModel.GetDefaultList(),[],"ARTU_THIRD_BATCH_UI",GameMode.Standard,false,0);
            var p=run.Players[0];
            await CardPileCmd.RemoveFromDeck(p.Deck.Cards.OfType<SultanCard>().ToArray());
            var note=run.CreateCard<NoteInBook>(p);await CardPileCmd.Add(note,PileType.Deck);
            await RunManager.Instance.EnterMapPointInternal(1,MapPointType.Monster,null,true);
            await Until(()=>CombatManager.Instance.IsInProgress&&p.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"normal combat opens");
            Check("normal combat entered",true);
            var combat=((CombatRoom)run.CurrentRoom!).CombatState;
            await PlayerCmd.GainEnergy(20,p);
            foreach(var c in p.PlayerCombatState!.Hand.Cards.ToArray())await CardPileCmd.Add(c,PileType.Discard);
            var might=combat.CreateCard<ArmyMight>(p);await CardPileCmd.Add(might,PileType.Hand);
            Check("might accepts ordinary manual play",might.TryManualPlay(null));
            await Until(()=>p.Creature.HasPower<ArmyMightPower>(),"might power applied");
            Check("might uses base eight event cap",p.Creature.GetPower<ArmyMightPower>()!.Amount==8);
            var academy=combat.CreateCard<RevolutionaryAcademy>(p);await CardPileCmd.Add(academy,PileType.Hand);
            Check("academy accepts ordinary manual play",academy.TryManualPlay(null));
            await Until(()=>Walk(NGame.Instance).OfType<NSimpleCardSelectScreen>().Any(),"native troop choice");
            var screen=Walk(NGame.Instance).OfType<NSimpleCardSelectScreen>().Last();await Task.Delay(400);
            var choices=Walk(screen).OfType<NCard>().Select(n=>n.Model).OfType<ArmyChoice>().Distinct().ToArray();
            Check("native selection renders three named owned options",choices.Length==3&&choices.All(c=>c.Owner==p&&!c.Title.Contains('{')));
            await Shot("01-army-choice");
            var selected=(HashSet<CardModel>)AccessTools.Field(typeof(NSimpleCardSelectScreen),"_selectedCards").GetValue(screen)!;
            selected.Add(choices.Single(c=>c.Kind==3));AccessTools.Method(typeof(NSimpleCardSelectScreen),"CompleteSelection").Invoke(screen,null);
            await Until(()=>p.Creature.GetPower<AcademyPower>()?.Kind==3,"academy choice and apply animation resolve");
            Check("actual academy target is elite",p.Creature.GetPower<AcademyPower>()!.Kind==3);
            AgendaDetails.Show(p);await Shot("02-agenda-details");
            var panel=NRun.Instance!.GetNode<Panel>("ArtuAgendaDetails");
            Check("agenda has real scroll container",Walk(panel).OfType<ScrollContainer>().Any());
            panel.QueueFree();await Task.Delay(250);
            var urgent=combat.CreateCard<StoneLuxury>(p);urgent.BattlesRemaining=1;await CardPileCmd.Add(urgent,PileType.Hand);
            var normal=combat.CreateCard<StoneLuxury>(p);normal.BattlesRemaining=2;await CardPileCmd.Add(normal,PileType.Hand);
            await Until(()=>Walk(NGame.Instance).OfType<NCard>().Any(n=>ReferenceEquals(n.Model,urgent)),"deadline card visible");
            await Shot("03-deadline-border");
            var cardNode=Walk(NGame.Instance).OfType<NCard>().First(n=>ReferenceEquals(n.Model,urgent));
            File.WriteAllText(Path.Combine(dir,"card-geometry.json"),JsonSerializer.Serialize(Walk(cardNode).OfType<Control>().Select(n=>new{name=n.Name.ToString(),size=n.Size.ToString(),position=n.Position.ToString(),z=n.ZIndex,visible=n.Visible}),new JsonSerializerOptions{WriteIndented=true}));
            Check("urgent instance has visible nonzero red border",cardNode.Body.GetNodeOrNull<Panel>("ArtuDeadlineBorder") is {Visible:true} b&&b.Size.X>=300&&b.Size.Y>=422);
            V04.Data(p).Grace=true;await Task.Delay(700);
            Check("freeze refresh clears red border",cardNode.Body.GetNode<Panel>("ArtuDeadlineBorder").Visible==false);
        }
        catch(Exception e){error=e.ToString();GD.PrintErr(error);}
        File.WriteAllText(Path.Combine(dir,"native-result.json"),JsonSerializer.Serialize(new{passed=checks.Count,failed=error==null?0:1,checks,error,limitations="Artificial test deck/energy. Not a full run, balance test or multiplayer test."},new JsonSerializerOptions{WriteIndented=true}));
        NGame.Instance?.GetTree().Quit(error==null?0:1);
    }
}
