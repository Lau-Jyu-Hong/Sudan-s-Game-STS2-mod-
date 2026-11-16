using ArtuMod;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using System.Text.Json;

namespace ArtuAutoplayLab;

public static class ArtPreviewScenario
{
    public static async Task Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var checks = new List<string>();
        string? error = null;
        void Check(bool ok,string name)
        {
            if(!ok) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Until(Func<bool> test)
        {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while(!test())
            {
                if(DateTime.UtcNow > deadline) throw new TimeoutException("Native combat or hand did not initialize.");
                await Task.Delay(80);
            }
        }
        async Task Shot(string name)
        {
            await Task.Delay(850);
            using var im = NGame.Instance!.GetViewport().GetTexture().GetImage();
            Check(im.SavePng(Path.Combine(dir,name+".png"))==Error.Ok, "Screenshot: "+name);
        }
        try
        {
            TestMode.IsOn=false;
            static IEnumerable<Node> Walk(Node root)
            {
                yield return root;
                foreach(var child in root.GetChildren())
                    foreach(var node in Walk(child)) yield return node;
            }
            var menu=Walk(NGame.Instance!).OfType<NMainMenuSubmenuStack>().First();
            var characterSelect=menu.GetSubmenuType<NCharacterSelectScreen>();
            characterSelect.InitializeSingleplayer();
            menu.PushSubmenuType<NCharacterSelectScreen>();
            var button=Walk(characterSelect).OfType<NCharacterSelectButton>().FirstOrDefault(b=>b.Character is Artu);
            Check(button!=null,"Artu selection button is present");
            characterSelect.SelectCharacter(button!);
            await Shot("00-character-select");
            var run=await NGame.Instance!.StartNewSingleplayerRun(Player.CreateForNewRun<Artu>(UnlockState.all,1),
                true,ActModel.GetDefaultList(),[],"ARTU_ART_PREVIEW",GameMode.Standard,false,0);
            var player=run.Players[0];
            NMapScreen.Instance?.Open(true);
            await Shot("00-map");
            NMapScreen.Instance?.Close(false);
            await Util.AddToDeck(player,ModelDb.Card<GoldSlaughter>());
            await RunManager.Instance.EnterMapPointInternal(1,MapPointType.Monster,null,true);
            await Until(()=>Walk(NGame.Instance!).OfType<NSimpleCardSelectScreen>().Any(n=>n.IsVisibleInTree()));
            var selection=Walk(NGame.Instance!).OfType<NSimpleCardSelectScreen>().Last(n=>n.IsVisibleInTree());
            var goldOption=Walk(selection).OfType<NCard>().FirstOrDefault(c=>c.Model is GoldSlaughter);
            if (goldOption!=null)
                Check(goldOption.GetNode<TextureRect>("%AncientPortrait").Visible,"Witch box selector shows Gold Slaughter full art");
            await Shot("00-witch-box-choice");
            var selected=(HashSet<CardModel>)AccessTools.Field(typeof(NSimpleCardSelectScreen),"_selectedCards").GetValue(selection)!;
            selected.Add(goldOption?.Model??Walk(selection).OfType<NCard>().First(c=>c.Model is SultanCard).Model!);
            AccessTools.Method(typeof(NSimpleCardSelectScreen),"CompleteSelection").Invoke(selection,null);
            await Until(()=>CombatManager.Instance.IsInProgress&&player.PlayerCombatState?.Phase==PlayerTurnPhase.Play);
            var combat=((CombatRoom)run.CurrentRoom!).CombatState;
            var gold=player.PlayerCombatState!.Hand.Cards.OfType<GoldSlaughter>().FirstOrDefault();
            if(gold==null){gold=(GoldSlaughter)combat.CreateCard(ModelDb.Card<GoldSlaughter>(),player);await CardPileCmd.Add(gold,PileType.Hand);}
            await Until(()=>NPlayerHand.Instance!.ActiveHolders.Any(h=>h.CardNode?.Model==gold));
            var native=NPlayerHand.Instance!.ActiveHolders.Single(h=>h.CardNode?.Model==gold).CardNode!;
            Check(native.GetNode<TextureRect>("%AncientPortrait").Visible && !native.GetNode<TextureRect>("%Portrait").Visible,
                "Gold Slaughter is full-art in native combat hand");
            Check(gold.Rarity==CardRarity.Event&&gold.Type==CardType.Curse,"Gold Slaughter still has Event rarity and Curse type");
            Check(NGame.Instance.FindChild("ArtuEnergyEmblem",true,false) is TextureRect,
                "Native energy counter uses Artu emblem");
            var ctx=new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<AllArmyPower>(ctx,player.Creature,1,player.Creature,null);
            await PowerCmd.Apply<ArmyMightPower>(ctx,player.Creature,1,player.Creature,null);
            MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Clear();
            await Shot("01-combat");
            var layer=new CanvasLayer{Layer=30}; NGame.Instance.AddChild(layer);
            var size=NGame.Instance.GetViewportRect().Size;
            layer.AddChild(new ColorRect{Size=size,Color=new Color("292835")});
            var normal=NCard.Create(combat.CreateCard(ModelDb.Card<DispatchTroops>(),player))!;
            var pilot=NCard.Create(gold)!;
            layer.AddChild(normal);layer.AddChild(pilot);
            normal.Position=new Vector2(size.X*.30f,size.Y*.51f);pilot.Position=new Vector2(size.X*.70f,size.Y*.51f);
            normal.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
            pilot.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
            Check(normal.GetNode<TextureRect>("%Portrait").Visible&&!normal.GetNode<TextureRect>("%AncientPortrait").Visible,
                "A non-Sultan card stays on the native normal layout");
            Check(pilot.GetNode<TextureRect>("%AncientPortrait").Visible&&pilot.GetNode<Control>("%DescriptionLabel").Visible,
                "Preview node has full art and native description");
            await Shot("02-card-comparison");
            normal.QueueFree();
            pilot.QueueFree();
            var deck=ModelDb.All.OfType<SultanCard>().OrderBy(c=>c.Suit).ThenBy(c=>c.Grade).ToArray();
            foreach(var (cards,page) in deck.Chunk(8).Select((group,i)=>(group,i)))
            {
                var shown = new List<NCard>();
                var scale = Math.Min((size.X-60)/1350f,(size.Y-48)/900f);
                for(int i=0;i<cards.Length;i++)
                {
                    var model=combat.CreateCard(cards[i],player);
                    var node=NCard.Create(model)!;layer.AddChild(node);
                    node.Scale=Vector2.One*scale;
                    node.Position=new Vector2(size.X/2+(i%4-1.5f)*333*scale,size.Y/2+(i/4-.5f)*440*scale);
                    node.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
                    Check(node.GetNode<TextureRect>("%AncientPortrait").Visible&&model.Rarity==CardRarity.Event,
                        "Full-art layout / unchanged rarity: "+model.Id.Entry);
                    shown.Add(node);
                }
                await Shot("03-full-sultan-set-"+(page+1));
                foreach(var node in shown)node.QueueFree();
            }
        }
        catch(Exception e){error=e.ToString();GD.PrintErr(error);}
        File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{checks,error,userDir=OS.GetUserDataDir()},new JsonSerializerOptions{WriteIndented=true}));
        NGame.Instance?.GetTree().Quit(error==null?0:1);
    }
}
