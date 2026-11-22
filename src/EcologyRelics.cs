using System.Reflection;
namespace ArtuMod;
public static class CrimeRecord
{
    public static async Task Add(Player p,int n)
    {
        if(n<=0)return;
        if(p.PlayerCombatState!=null&&CombatManager.Instance.IsInProgress)
        {
            int baseAmount=p.Creature.GetPower<CrimePower>()==null?Util.State(p)?.Crime??0:0;
            await PowerCmd.Apply<CrimePower>(new ThrowingPlayerChoiceContext(),p.Creature,baseAmount+n,p.Creature,null);
        }
        else if(Util.State(p) is {} state)state.Crime+=n;
    }
}
public sealed class UsurperCrownPower():ArtuPower("篡位者的王冠","每回合第一张打出的苏丹牌不消耗能量，并免除代价。")
{
    int spentTurn=-1;
    protected override void DeepCloneFields(){base.DeepCloneFields();spentTurn=-1;}
    public static bool Waives(CardModel card)=>card.Owner?.Creature.GetPower<UsurperCrownPower>()?.Available(card)==true;
    public bool Available(CardModel card)
    {
        var player=Owner?.Player;
        if(player==null||card is not SultanCard||card.Owner!=player)return false;
        return spentTurn!=(player.PlayerCombatState?.TurnNumber??-1);
    }
    public bool TryConsume(CardModel card)
    {
        if(!Available(card))return false;
        spentTurn=Owner.Player?.PlayerCombatState?.TurnNumber??-1;
        return true;
    }
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {modified=original;if(!Available(card))return false;modified=0;return true;}
}
public sealed class ScalesWaiverPower():ArtuPower("黑天秤","本场战斗中，罪证第1至第3档的伤害、格挡与抽牌惩罚不生效。能量及更高档仍生效。")
{
}
public sealed class PardonDecreePower():ArtuPower("免死密诏","下一张打出的苏丹牌免除代价。不改变其能量，打出后仍按原规则重置期限。")
{
}
public sealed class HextechCrownPool:CustomRelicPoolModel{}
[Pool(typeof(HextechCrownPool))]
public sealed class UsurpersCrown:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Starter;
    public override string PackedIconPath=>"res://ArtuMod/images/usurperscrown.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("篡位者的王冠","每回合第一张打出的苏丹牌不消耗能量，并免除代价。","棱彩海克斯。只在海克斯大乱斗的选海克斯时出现。");
    public override async Task BeforeCombatStart()
    {
        var previous=Owner.Creature.GetPower<UsurperCrownPower>();
        if(previous!=null)await PowerCmd.Remove(previous);
        await PowerCmd.Apply<UsurperCrownPower>(new ThrowingPlayerChoiceContext(),Owner.Creature,1,Owner.Creature,null);
    }
}
[Pool(typeof(ArtuRelicPool))]
public sealed class OldBattleFlag:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Common;
    public override string PackedIconPath=>"res://ArtuMod/images/status/levypower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("老旧军旗","每场战斗开始时，获得2杂兵。","旗帜已经褪色，号召力还在。");
    public override Task BeforeCombatStart()=>ArmyPower.Gain(new ThrowingPlayerChoiceContext(),Owner,0,2);
}
[Pool(typeof(ArtuRelicPool))]
public sealed class SultanBronzeRing:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Rare;
    public override string PackedIconPath=>"res://ArtuMod/images/status/goldenluxurypower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("苏丹铜戒","每打出一张苏丹牌，抽1张牌。","铜戒换得宫廷里的一点通融。");
    public override Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)=>play.Player==Owner&&play.Card is SultanCard&&play.IsFirstInSeries?CardPileCmd.Draw(ctx,1,Owner):Task.CompletedTask;
}
[Pool(typeof(ArtuRelicPool))]
public sealed class GuardsOrder:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Uncommon;
    public override string PackedIconPath=>"res://ArtuMod/images/status/soldierpower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("近卫军令状","每当杂兵被真实转化为高级兵种时，每转化1层获得4点格挡。","盖着红印的调度文书。");
}
[Pool(typeof(ArtuRelicPool))]
public sealed class DarkScales:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Uncommon;
    public override string PackedIconPath=>"res://ArtuMod/images/status/crimepower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("黑天秤","每场战斗开始时，若金币足够，失去等同于罪证层数×10的金币，最多30。支付后，本场免除罪证第1至第3档。第4档及更高档仍生效。金币不足则本场不免除，也不把金币扣成负数。","天平往暗处偏了一寸。");
    public override async Task BeforeCombatStart()
    {
        var leftover=Owner.Creature.GetPower<ScalesWaiverPower>();
        if(leftover!=null)await PowerCmd.Remove(leftover);
        int crime=Math.Max(Owner.Creature.GetPowerAmount<CrimePower>(),Util.State(Owner)?.Crime??0);
        int cost=Math.Min(3,crime)*10;
        if(cost<=0||Owner.Gold<cost)return;
        await PlayerCmd.LoseGold(cost,Owner);
        await PowerCmd.Apply<ScalesWaiverPower>(new ThrowingPlayerChoiceContext(),Owner.Creature,1,Owner.Creature,null);
    }
}
[Pool(typeof(ArtuRelicPool))]
public sealed class BloodDrum:WitchCardBox
{
    public override RelicRarity Rarity=>RelicRarity.Ancient;
    public override string PackedIconPath=>"res://ArtuMod/images/status/warsongpower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("血染战鼓","替换女术士的牌盒。每回合开始时获得1士兵。休息处可以销毁一张苏丹牌，该选择占用本次休息。不再提供换牌。","鼓声留下的只有队列。");
    public override RelicModel? GetUpgradeReplacement()=>null;
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)=>player==Owner&&!Owner.Creature.IsDead?ArmyPower.Gain(ctx,Owner,1,1):Task.CompletedTask;
    public override bool TryModifyRestSiteOptions(Player player,ICollection<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption> options)
    {if(player!=Owner)return false;options.Add(new RemoveSultanOption(player));if(Crime>0)options.Add(new ClearCrimeOption(player));return true;}
}
[Pool(typeof(ArtuRelicPool))]
public sealed class UsuryContract:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Shop;
    public override string PackedIconPath=>"res://ArtuMod/images/status/spendingrefundpower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("高利贷契约","拾取时获得250金币，并获得2层罪证。","字据上的利息以后再算，黄金现在就在手里。");
    public override async Task AfterObtained(){await PlayerCmd.GainGold(250,Owner);await CrimeRecord.Add(Owner,2);}
}
[Pool(typeof(ArtuRelicPool))]
public sealed class GarrisonEnlistment:CustomRelicModel
{
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override string PackedIconPath=>"res://ArtuMod/images/status/recruitmentnoticepower.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override List<(string,string)> Localization=>new RelicLoc("闯关降卒","每场战斗开始时，获得2士兵。","哨卡上留下来的两个人，此后每战都到。");
    public override Task BeforeCombatStart()=>ArmyPower.Gain(new ThrowingPlayerChoiceContext(),Owner,1,2);
}
public static class HextechBridge
{
    public static bool Attempted{get;private set;}
    public static bool Registered{get;private set;}
    public static void TryRegister()
    {
        if(Attempted)return;
        Attempted=true;
        try
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HextechRunes.HextechRunesInterop")).FirstOrDefault(t=>t!=null);
            if(type==null)return;
            if(type.GetProperty("ApiVersion")?.GetValue(null) is not int version||version<1)return;
            var available=(Func<Player,bool>)(p=>p.Character is Artu);
            type.GetMethod("RegisterPlayerRune")!.Invoke(null,[typeof(UsurpersCrown),"Prismatic",null,null,0,"COMPREHENSIVE","ArtuMod",available]);
            type.GetMethod("SetPlayerRunePoolLabel")?.Invoke(null,[typeof(UsurpersCrown),"ARTU"]);
            type.GetMethod("SetPlayerRuneCharacter")?.Invoke(null,[typeof(UsurpersCrown),typeof(Artu)]);
            Registered=true;
        }
        catch(Exception ex){GD.Print($"[Artu hex] registration skipped: {ex.GetBaseException().Message}");}
    }
}
