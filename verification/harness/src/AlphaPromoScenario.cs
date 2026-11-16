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
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuAutoplayLab;

// Test-only showcase. Raw captures are actual game renderer output, not a normal-playthrough claim.
public static class AlphaPromoScenario
{
    public static async Task Run(string dir)
    {
        Directory.CreateDirectory(dir);Directory.CreateDirectory(Path.Combine(dir,"screenshots"));
        var checks=new List<string>();var errors=new List<string>();
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(dir,name),JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
        void Check(string label,bool ok){if(!ok)throw new Exception(label);checks.Add(label);}
        async Task Until(Func<bool> test,string label){var limit=DateTime.UtcNow.AddSeconds(45);while(!test()){if(DateTime.UtcNow>limit)throw new TimeoutException(label);await Task.Delay(60);}}
        async Task Shot(string name){await Task.Delay(500);MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet.Clear();await Task.Delay(180);using var image=NGame.Instance!.GetViewport().GetTexture().GetImage();image.SavePng(Path.Combine(dir,"screenshots",name+".png"));image.SaveJpg(Path.Combine(dir,"screenshots",name+".jpg"),0.88f);}
        try
        {
            var mods=MegaCrit.Sts2.Core.Modding.ModManager.GetLoadedMods().Select(m=>m.manifest!.id).ToArray();
            Save("conditions.json",new{mods,profile=OS.GetUserDataDir(),artificialResources=true,cloudSaveStore="local-only mock",steamAuthentication="enabled"});
            Check("only intended isolated mods loaded",mods.All(id=>id is "BaseLib" or "ArtuMod" or "ArtuAutoplayLab"));
            SmokeTests.Run(Path.Combine(dir,"runtime.json"),false);
            using(var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"runtime.json"))))
                if(result.RootElement.GetProperty("failed").GetInt32()!=0)errors.Add("Mechanism regression has failures; inspect runtime.json.");
            TestMode.IsOn=false;Engine.TimeScale=3;
            var run=await NGame.Instance!.StartNewSingleplayerRun(MegaCrit.Sts2.Core.Entities.Players.Player.CreateForNewRun<Artu>(MegaCrit.Sts2.Core.Unlocks.UnlockState.all,1),true,ActModel.GetDefaultList(),[],"ARTU_ALPHA_SHOWCASE",GameMode.Standard,false,0);
            var p=run.Players[0];var state=Util.State(p)!;var ctx=new ThrowingPlayerChoiceContext();int floor=0;CombatState combat=null!;
            await CardPileCmd.RemoveFromDeck(p.Deck.Cards.OfType<SultanCard>().ToArray());
            async Task Battle(bool controlled)
            {
                await RunManager.Instance.EnterMapPointInternal(++floor,MapPointType.Monster,null,true);
                await Until(()=>CombatManager.Instance.IsInProgress&&p.PlayerCombatState?.Phase==PlayerTurnPhase.Play,"combat ready");
                combat=((CombatRoom)run.CurrentRoom!).CombatState;
                foreach(var c in p.PlayerCombatState!.AllCards.ToArray())await CardPileCmd.RemoveFromCombat(c);
                if(controlled){foreach(var e in combat.HittableEnemies){foreach(var power in e.Powers.ToArray())await PowerCmd.Remove(power);await CreatureCmd.SetMaxHp(e,50000);await CreatureCmd.SetCurrentHp(e,50000);}}
            }
            async Task ClearHand(){foreach(var c in p.PlayerCombatState!.Hand.Cards.ToArray())await CardPileCmd.RemoveFromCombat(c);}
            async Task<CardModel> Add(CardModel model){var card=combat.CreateCard(model,p);await CardPileCmd.Add(card,PileType.Hand);return card;}
            async Task Finish()
            {
                foreach(var e in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(e);
                var card=(DestructiveStrike)await Add(ModelDb.Card<DestructiveStrike>());card.DynamicVars.Damage.BaseValue=999999;
                await PlayerCmd.GainEnergy(50,p);
                var enemy=combat.HittableEnemies.First();card.UpdateDynamicVarPreview(CardPreviewMode.Normal,enemy,card.DynamicVars);
                await Task.Delay(250);Check("finisher native manual accepted",card.TryManualPlay(enemy));
                await Task.Delay(700);Save("finisher-state.json",new{baseDamage=card.DynamicVars.Damage.BaseValue,damage=card.DynamicVars.Damage.IntValue,hp=enemy.CurrentHp,isDead=enemy.IsDead,pile=card.Pile?.Type.ToString()});
                await Until(()=>!CombatManager.Instance.IsInProgress,"actual victory");await Task.Delay(500);
            }
            async Task Gallery(string file,string title,string subtitle,CardModel[] cards)
            {
                var size=NRun.Instance!.GetViewportRect().Size;
                var layer=new CanvasLayer{Name="ArtuShowcaseGallery",Layer=35};NRun.Instance.AddChild(layer);
                var background=new ColorRect{Color=new Color(0.035f,0.045f,0.065f,1f),Size=size,MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(background);
                var heading=new Label{Text=title,Position=new Vector2(54,26)};heading.AddThemeFontSizeOverride("font_size",38);heading.AddThemeColorOverride("font_color",new Color("edc66f"));layer.AddChild(heading);
                var note=new Label{Text=subtitle+"  |  Alpha·演示图库（人工构筑）",Position=new Vector2(56,82)};note.AddThemeFontSizeOverride("font_size",20);layer.AddChild(note);
                int columns=cards.Length<=4?cards.Length:4;int rows=(cards.Length+columns-1)/columns;
                float scale=Math.Min(0.9f,Math.Min((size.X-120)/(columns*326),(size.Y-150)/(rows*440)));
                float left=(size.X-columns*326*scale)/2;
                float top=130+(size.Y-130-rows*440*scale)/2;
                for(int i=0;i<cards.Length;i++)
                {
                    var model=combat.CreateCard(cards[i],p);var node=NCard.Create(model)!;layer.AddChild(node);
                    node.Position=new Vector2(left+((i%columns)*326+150)*scale,top+((i/columns)*440+211)*scale);node.Scale=Vector2.One*scale;
                    node.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
                }
                await Shot(file);layer.QueueFree();await Task.Delay(200);
            }
            await Battle(true);
            await PowerCmd.Apply<StrengthPower>(ctx,p.Creature,20,p.Creature,null);await PowerCmd.Apply<DexterityPower>(ctx,p.Creature,20,p.Creature,null);
            await PowerCmd.Apply<WeakPower>(ctx,p.Creature,1,p.Creature,null);await PowerCmd.Apply<FrailPower>(ctx,p.Creature,1,p.Creature,null);await PowerCmd.Apply<VigorPower>(ctx,p.Creature,10,p.Creature,null);
            var target=combat.HittableEnemies.First();foreach(var e in combat.HittableEnemies.Skip(1).ToArray())await CreatureCmd.Kill(e);
            await PowerCmd.Apply<VulnerablePower>(ctx,target,1,p.Creature,null);await ArmyPower.Gain(ctx,p,1,2);
            int hp=target.CurrentHp,block=p.Creature.Block;
            await p.Creature.GetPower<SoldierPower>()!.Trigger(ctx);
            Check("native troop ignores positive stats and weak/vulnerable",hp-target.CurrentHp==4);
            Check("native troop block ignores dexterity/frail",p.Creature.Block-block==2);
            Check("native troop does not consume vigor",p.Creature.GetPowerAmount<VigorPower>()==10);
            foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);
            await PowerCmd.Apply<CrimePower>(ctx,p.Creature,2,p.Creature,null);await Finish();
            Check("native victory preserves two crimes",state.Crime==2);
            await RunManager.Instance.EnterMapPointInternal(++floor,MapPointType.RestSite,null,true);await Task.Delay(650);
            var rest=NRestSiteRoom.Instance!;var option=rest.Options.OfType<ClearCrimeOption>().Single();Check("native rest has crime removal",option.IsEnabled);
            rest.SetText(option.Description.GetFormattedText());await Shot("05-rest-crime");
            int n=rest.Options.ToList().IndexOf(option);Check("native synchronized rest succeeds",await RunManager.Instance.RestSiteSynchronizer.ChooseLocalOption(n));
            rest.AfterSelectingOption(option);await Task.Delay(700);Check("native rest removes exactly one crime",state.Crime==1);
            Check("native rest consumes its action",rest.Options.Count==0);
            var saved=(WitchCardBox)RelicModel.FromSerializable(state.ToSerializable());Check("removed crime persists in save",saved.Crime==1);

            await Battle(false);await CreatureCmd.SetMaxHp(p.Creature,70);await CreatureCmd.SetCurrentHp(p.Creature,70);
            foreach(var power in p.Creature.Powers.ToArray())await PowerCmd.Remove(power);state.Crime=0;
            await Gallery("01-sultan-orders","皇命与代价","四类皇命 × 四种品级；期限与代价由每张牌独立计算",[
                ModelDb.Card<StoneSlaughter>(),ModelDb.Card<StoneConquest>(),ModelDb.Card<StoneLuxury>(),ModelDb.Card<StoneLust>(),
                ModelDb.Card<GoldSlaughter>(),ModelDb.Card<GoldConquest>(),ModelDb.Card<GoldLuxury>(),ModelDb.Card<GoldLust>()]);
            await ArmyPower.Gain(ctx,p,0,5);await ArmyPower.Gain(ctx,p,1,4);await ArmyPower.Gain(ctx,p,2,6);await ArmyPower.Gain(ctx,p,3,2);
            foreach(var model in new CardModel[]{ModelDb.Card<RecruitTroops>(),ModelDb.Card<TrainArmy>(),ModelDb.Card<ArmyRegularization>(),ModelDb.Card<RevolutionaryAcademy>()})await Add(model);
            await Shot("02-army-battle");await ClearHand();
            await Gallery("03-thought-evolution","思想可以演化，结局可以改写","从洁念、邪念、畏权走向不同的思想路线",[
                ModelDb.Card<PureThought>(),ModelDb.Card<TouchPurity>(),ModelDb.Card<AtonementPrayer>(),ModelDb.Card<PerfectWorld>(),
                ModelDb.Card<UncleanThought>(),ModelDb.Card<GlimpseDarkness>(),ModelDb.Card<AncientFaith>(),ModelDb.Card<NewWorld>(),
                ModelDb.Card<FearOfSultan>(),ModelDb.Card<FreedomFromFear>(),ModelDb.Card<RebellionIntent>(),ModelDb.Card<RegicidePlan>()]);
            await Gallery("04-revolution-agenda","从俯首执行，到组织反抗","读物、军校与跨战斗议程；特殊结局需满足实际解锁条件",[
                ModelDb.Card<NoteInBook>(),ModelDb.Card<RevolutionaryAcademy>(),ModelDb.Card<RevolutionConcept>(),ModelDb.Card<RegicidePlan>()]);
            var cat=await Add(ModelDb.Card<SuperBecky>());var handCard=await Add(ModelDb.Card<LoyalHound>());
            Engine.TimeScale=1;var effect=BeckyPresentation.BeforeCard(p,handCard);await Task.Delay(500);
            await Shot("06-becky-cat");await effect;BeckyPresentation.Clear(p);
            Save("result.json",new{passed=checks.Count,failed=errors.Count,checks,errors,artificialSetup=true,normalPlaythrough=false,cloudSaveDisabledByLocalTestStore=true});
        }
        catch(Exception e){errors.Add(e.ToString());Save("result.json",new{passed=checks.Count,failed=errors.Count,checks,errors});GD.PrintErr(e.ToString());}
        NGame.Instance?.GetTree().Quit(errors.Count==0?0:1);
    }
}
