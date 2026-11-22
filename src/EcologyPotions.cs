using MegaCrit.Sts2.Core.Entities.Potions;
namespace ArtuMod;
public sealed class ArtuPotionPool:CustomPotionPoolModel{}
[Pool(typeof(ArtuPotionPool))]
public sealed class ConscriptionHornPotion:CustomPotionModel
{
    public override PotionRarity Rarity=>PotionRarity.Uncommon;
    public override TargetType TargetType=>TargetType.Self;
    public override PotionUsage Usage=>PotionUsage.CombatOnly;
    public override List<(string,string)> Localization=>new PotionLoc("征召号角","获得士兵、精兵、锐士各1层。");
    protected override async Task OnUse(PlayerChoiceContext ctx,Creature? target)
    {await ArmyPower.Gain(ctx,Owner,1,1);await ArmyPower.Gain(ctx,Owner,2,1);await ArmyPower.Gain(ctx,Owner,3,1);}
}
[Pool(typeof(ArtuPotionPool))]
public sealed class PardonDecreePotion:CustomPotionModel
{
    public override PotionRarity Rarity=>PotionRarity.Uncommon;
    public override TargetType TargetType=>TargetType.Self;
    public override PotionUsage Usage=>PotionUsage.CombatOnly;
    public override List<(string,string)> Localization=>new PotionLoc("免死密诏","抽2张牌。下一张打出的苏丹牌免除代价。");
    protected override async Task OnUse(PlayerChoiceContext ctx,Creature? target)
    {await CardPileCmd.Draw(ctx,2,Owner);await PowerCmd.Apply<PardonDecreePower>(ctx,Owner.Creature,1,Owner.Creature,null);}
}
[Pool(typeof(ArtuPotionPool))]
public sealed class HolyAmnestyWaterPotion:CustomPotionModel
{
    public override PotionRarity Rarity=>PotionRarity.Rare;
    public override TargetType TargetType=>TargetType.Self;
    public override PotionUsage Usage=>PotionUsage.AnyTime;
    public override List<(string,string)> Localization=>new PotionLoc("赦免圣水","回复10点生命，并永久移除1层罪证。");
    protected override async Task OnUse(PlayerChoiceContext ctx,Creature? target)
    {await CreatureCmd.Heal(Owner.Creature,10);await V04.RemoveCrime(ctx,Owner,1);}
}
