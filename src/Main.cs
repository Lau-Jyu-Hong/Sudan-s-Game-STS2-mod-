using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
namespace ArtuMod;
[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static void Initialize() => new Harmony("ArtuMod").PatchAll(Assembly.GetExecutingAssembly());
}
public sealed class Artu : PlaceholderCharacterModel
{
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override Color NameColor => new("d6ae63");
    public override int StartingHp => 70;
    public override CardPoolModel CardPool => ModelDb.CardPool<ArtuCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<ArtuRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<MegaCrit.Sts2.Core.Models.PotionPools.IroncladPotionPool>();
    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<StoneSlaughter>(),ModelDb.Card<StoneSlaughter>(),
        ModelDb.Card<StoneConquest>(),ModelDb.Card<StoneConquest>(),
        ModelDb.Card<StoneLuxury>(),ModelDb.Card<StoneLuxury>(),
        ModelDb.Card<StoneLust>(),ModelDb.Card<StoneLust>(),
        ModelDb.Card<DispatchTroops>(),ModelDb.Card<RoyalPass>()];
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<WitchCardBox>()];
    public override List<(string,string)> Localization => new CharacterLoc("阿尔图","阿尔图",
        "背负苏丹皇命的异乡人。执行、堕落，或组织一支不再俯首的军队。","他","他","他的","他的",
        "他身上有墨水与旧金币的气味。","先让我处理完皇命。","苏丹终于满意了吗？","苏丹尚未准许我死去。","这些钱可还不够。",
        "皇命与自由","苏丹牌拥有独立期限与代价。");
}
public sealed class ArtuCardPool : CustomCardPoolModel
{
    public override string Title => "Artu";
    public override string BigEnergyIconPath => ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.IroncladCardPool>().EnergyIconPath;
    public override string TextEnergyIconPath => ModelDb.CardPool<MegaCrit.Sts2.Core.Models.CardPools.IroncladCardPool>().EnergyIconPath;
    public override float H => 0.12f; public override float S => 0.55f; public override float V => 0.9f;
    public override Color DeckEntryCardColor => new("d6ae63");
    public override bool IsColorless => false;
}
public sealed class ArtuRelicPool : CustomRelicPoolModel { }
[Pool(typeof(ArtuCardPool))]
public abstract class ArtuCard(int cost, CardType type, CardRarity rarity, TargetType target) : CustomCardModel(cost,type,rarity,target)
{
    public virtual bool Reading => false;
    public virtual bool Thought => false;
    public override int MaxUpgradeLevel => Reading || Thought ? 0 : 1;
    // Beta art uses installed game assets until an Artu art set is supplied.
    public override string PortraitPath {
        get {
            var path="res://ArtuMod/images/"+GetType().Name.ToLowerInvariant()+".tres";
            return ResourceLoader.Exists(path)?path:"res://ArtuMod/images/flowingthoughts.tres";
        }
    }
    public override string BetaPortraitPath => PortraitPath;
    public override string CustomPortraitPath=>PortraitPath;
    protected static CardSelectorPrefs Choose(string text,int count=1,bool skip=false)
        => new(new LocString("cards","ARTU_CHOOSE"){ },count){ };
    protected async Task<CardModel?> Select(PlayerChoiceContext context,IEnumerable<CardModel> cards,bool skip=false)
    {
        var list=cards.ToList();
        if(list.Count==0)return null;
        return (await CardSelectCmd.FromSimpleGrid(context,list,Owner,new CardSelectorPrefs(SelectionScreenPrompt,skip?0:1,1))).FirstOrDefault();
    }
    protected Task AddPower<T>(PlayerChoiceContext ctx,int amount,Creature? target=null) where T:PowerModel
        => PowerCmd.Apply<T>(ctx,target??Owner.Creature,amount,Owner.Creature,this);
    public Task ExecuteCopiedEffect(PlayerChoiceContext ctx,CardPlay play)=>OnPlay(ctx,play);
}
public static class Util
{
    public static WitchCardBox? State(Player p)=>p.Relics.OfType<WitchCardBox>().FirstOrDefault();
    public static bool Tyranny(Player p)=>p.Relics.Any(r=>r is TyrantsAccomplice);
    public static IEnumerable<Creature> Enemies(Player p)=>p.Creature.CombatState?.HittableEnemies??[];
    public static Creature? RandomEnemy(Player p)=>p.RunState.Rng.CombatTargets.NextItem(Enemies(p));
    public static Task LoseHp(PlayerChoiceContext ctx,Player p,int amount,CardModel? card=null)
        =>CreatureCmd.SetCurrentHp(p.Creature,Math.Max(0,p.Creature.CurrentHp-amount));
    public static async Task AddToDeck(Player p,CardModel canonical)
    {
        var card=p.RunState.CreateCard(canonical,p);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card,PileType.Deck));
    }
    public static async Task<CardModel?> Choose(Player p,IEnumerable<CardModel> cards,bool skip=false,PlayerChoiceContext? ctx=null,LocString? prompt=null)
    {
        var list=cards.ToList();if(list.Count==0)return null;
        var mutable=list.Select(c=>c.IsMutable?c:p.RunState.CreateCard(c,p)).ToList();
        return (await CardSelectCmd.FromSimpleGrid(ctx is null or ThrowingPlayerChoiceContext?new BlockingPlayerChoiceContext():ctx,mutable,p,
            new CardSelectorPrefs(prompt??new LocString("card_selection","ARTU_CHOOSE"),skip?0:1,1))).FirstOrDefault();
    }
    public static async Task RemoveSultan(CardModel card)
    {
        // Explicit Sultan operations use command APIs; Eternal excludes generic UI removal.
        await CardPileCmd.RemoveFromDeck(card);
    }
}
