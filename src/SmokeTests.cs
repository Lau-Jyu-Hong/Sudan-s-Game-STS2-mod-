using System.Reflection;
using System.Text.Json;
namespace ArtuMod;
public static class SmokeTests
{
    public static void Run(string output)=>Run(output,true);
    public static void Run(string output,bool quitAfterChecks)
    {
        var checks=new List<string>();var errors=new List<string>();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
        void Progress(string text)=>File.AppendAllText(output+".progress.log",$"{DateTime.UtcNow:O} {text}\n");
        Progress("BEGIN smoke tests");
        void Check(string name,Func<bool> test){Progress("START "+name);try{if(!test())throw new Exception("Assertion failed");checks.Add(name);Progress("PASS "+name);}catch(Exception e){errors.Add(name+": "+e);Progress("FAIL "+name+": "+e.Message);}}
        Check("Artu character registered",()=>ModelDb.AllCharacters.Any(c=>c is Artu));
        Check("10-card starter deck, two of each suit",()=>ModelDb.Character<Artu>().StartingDeck.Count()==10&&ModelDb.Character<Artu>().StartingDeck.OfType<SultanCard>().GroupBy(c=>c.Suit).All(g=>g.Count()==2));
        Check("16 Sultan classes registered",()=>ModelDb.All.OfType<SultanCard>().Count()==16);
        Check("Sultan independent mutable deadlines",()=>{var a=(SultanCard)ModelDb.Card<StoneSlaughter>().ToMutable();var b=(SultanCard)ModelDb.Card<StoneSlaughter>().ToMutable();a.BattlesRemaining=1;return b.BattlesRemaining==5;});
        Check("All Sultan non-upgradable and protected",()=>ModelDb.All.OfType<SultanCard>().All(c=>c.MaxUpgradeLevel==0&&c.CanonicalKeywords.Contains(CardKeyword.Eternal)));
        Check("Readings/thoughts never upgrade",()=>ModelDb.All.OfType<ArtuCard>().Where(c=>c.Reading||c.Thought).All(c=>c.MaxUpgradeLevel==0));
        Check("FlowingThoughts is not thought",()=>!ModelDb.Card<FlowingThoughts>().Thought);
        Check("Latest gold cards save counters",()=>typeof(SultanCard).GetProperty(nameof(SultanCard.TurnsSinceUse))!.GetCustomAttribute<SavedPropertyAttribute>()!=null);
        Check("Mockery floor at 9 stacks",()=>Rules.HpCeiling(73,9)==7);
        Check("Mockery 10 stacks always zero",()=>Enumerable.Range(1,1000).All(hp=>Rules.HpCeiling(hp,10)==0));
        Check("Sultan deck and turn deadlines",()=>Rules.BattleDeadline(SultanGrade.Stone)==5&&Rules.BattleDeadline(SultanGrade.Bronze)==3&&Rules.BattleDeadline(SultanGrade.Silver)==1&&Rules.BattleDeadline(SultanGrade.Gold)==1&&Rules.GoldTurnDeadline==5);
        Check("Forced Sultan priority releases ordinary cards",()=>!Rules.PrioritizeSultan(true,true,false)&&Rules.PrioritizeSultan(true,false,false));
        Check("Chivalry counts actual non-overkill life loss",()=>Rules.AdvancedTroopsFromDamage(4,0)==0&&Rules.AdvancedTroopsFromDamage(30,12)==3);
        Check("Character localization present",()=>ModelDb.Character<Artu>().Title.GetFormattedText().Contains("阿尔图"));
        Check("All custom cards localized",()=>ModelDb.All.OfType<ArtuCard>().All(c=>LocString.Exists("cards",c.Id.Entry+".title")&&LocString.Exists("cards",c.Id.Entry+".description")));
        Check("Rest exchange label present",()=>new LocString("rest_site_ui","OPTION_ARTU_EXCHANGE.name").GetFormattedText()=="换牌");
        Check("Relic state survives reflection copy",()=>{var a=(WitchCardBox)ModelDb.Relic<WitchCardBox>().ToMutable();var b=(WitchTreasureBox)ModelDb.Relic<WitchTreasureBox>().ToMutable();a.Anger=9;a.Mockery=4;foreach(var prop in typeof(WitchCardBox).GetProperties().Where(p=>p.GetCustomAttribute<SavedPropertyAttribute>()!=null&&p.CanWrite))prop.SetValue(b,prop.GetValue(a));return b.Anger==9&&b.Mockery==4;});
        Check("All portrait resources present",()=>ModelDb.All.OfType<ArtuCard>().All(c=>ResourceLoader.Exists(c.PortraitPath)));
        Check("Original portrait textures load",()=>ModelDb.Card<GoldSlaughter>().Portrait?.GetWidth()==250);
        Check("All custom status icons load at combat and hover size",()=>{foreach(var p in ModelDb.All.OfType<ArtuPower>().Where(p=>p.GetType().Name.EndsWith("Power",StringComparison.Ordinal))){if(!ResourceLoader.Exists(p.CustomPackedIconPath)||p.Icon.GetWidth()!=128||p.BigIcon.GetWidth()!=128)throw new Exception($"{p.GetType().Name}: icon={p.Icon?.GetWidth()}, big={p.BigIcon?.GetWidth()}, packed={p.PackedIconPath}, resolved={p.ResolvedBigIconPath}");}return true;});
        Check("Every playable custom power has a distinct icon resource path",()=>{var powers=ModelDb.All.OfType<ArtuPower>().Where(p=>p.GetType().Name.EndsWith("Power",StringComparison.Ordinal)).ToArray();return powers.Select(p=>p.CustomPackedIconPath).Distinct().Count()==powers.Length;});
        Check("Saved card properties round trip",()=>{var a=(SultanCard)ModelDb.Card<GoldSlaughter>().ToMutable();a.BattlesRemaining=1;a.TurnsSinceUse=2;var b=(SultanCard)CardModel.FromSerializable(a.ToSerializable());return b.TurnsSinceUse==2&&b.BattlesRemaining==1;});
        Check("Saved relic properties round trip",()=>{var a=(WitchCardBox)ModelDb.Relic<WitchCardBox>().ToMutable();a.Mockery=9;a.GoldLocked=true;var b=(WitchCardBox)RelicModel.FromSerializable(a.ToSerializable());return b.Mockery==9&&b.GoldLocked;});
        Check("Conquest target lists isolated by instance",()=>!ReferenceEquals(((ConquestChallengePower)ModelDb.Power<ConquestChallengePower>().ToMutable()).Targets,((ConquestChallengePower)ModelDb.Power<ConquestChallengePower>().ToMutable()).Targets));
        Check("Card pool energy icon present",()=>ResourceLoader.Exists(ModelDb.CardPool<ArtuCardPool>().BigEnergyIconPath));
        Check("Combat command integration",()=>{checks.AddRange(CombatVerification.Run());return true;});
        Check("Tea party mechanics and multiplayer command integration",()=>{checks.AddRange(TeaPartyVerification.Run());return true;});
        Check("v03 specification gameplay integration",()=>{checks.AddRange(V03Verification.Run());return true;});
        Check("v04 full incremental gameplay integration",()=>{checks.AddRange(V04Verification.Run());return true;});
        Check("Defense supplement gameplay integration",()=>{checks.AddRange(DefenseSupplementVerification.Run());return true;});
        Check("Repair verification",()=>{checks.AddRange(RepairVerification.Run());return true;});
        Check("Second batch verification",()=>{checks.AddRange(SecondBatchVerification.Run());return true;});
        Check("v0.4.3 incremental verification",()=>{checks.AddRange(V046Verification.Run());return true;});
        Check("third batch gameplay verification",()=>{checks.AddRange(ThirdBatchVerification.Run());return true;});
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output)!);
        var pool=ModelDb.Character<Artu>().CardPool.GetUnlockedCards(MegaCrit.Sts2.Core.Unlocks.UnlockState.all,CardMultiplayerConstraint.SingleplayerOnly).OfType<ArtuCard>().Where(c=>c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare).OrderBy(c=>c.Rarity).ThenBy(c=>c.Id.ToString()).Select(c=>new{id=c.Id.ToString(),name=c.Title,rarity=c.Rarity.ToString(),type=c.Type.ToString(),energy=c.EnergyCost.Canonical,upgradable=c.MaxUpgradeLevel>0,c.Reading,c.Thought,c.CanBeGeneratedInCombat,c.CanBeGeneratedByModifiers,description=c.Description.GetFormattedText()});
        File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(output)!,"registered-pool.json"),JsonSerializer.Serialize(pool,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(output,JsonSerializer.Serialize(new{passed=checks.Count,failed=errors.Count,checks,errors},new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("ARTU_SMOKE_RESULT "+checks.Count+" passed, "+errors.Count+" failed");
        if(quitAfterChecks)((SceneTree)Engine.GetMainLoop()).Quit(errors.Count==0?0:1);
    }
}
