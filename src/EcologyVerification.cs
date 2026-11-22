using System.Reflection;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
namespace ArtuMod;
public static class EcologyVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var checks=new List<string>();
        using var selector=CardSelectorStack.SetTestSelector(new TakeFirst());
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("ecology "+name);}
        try
        {
        using(var f=new V04Verification.Fixture())
        {
            f.Play<TemporaryRecruitment>();Check("temporary recruitment gives 4 block and 1 levy",f.Player.Creature.Block==4&&f.Player.Creature.GetPowerAmount<LevyPower>()==1);
            var upgraded=f.Card<TemporaryRecruitment>();CardCmd.Upgrade(upgraded);f.Play(upgraded);Check("temporary recruitment upgrade is 6 block and 2 levies",f.Player.Creature.Block==10&&f.Player.Creature.GetPowerAmount<LevyPower>()==3);
            int hp=f.Enemy.CurrentHp;f.Play<ArrayThrust>();Check("array thrust deals 7 and recruits 1",hp-f.Enemy.CurrentHp==7&&f.Player.Creature.GetPowerAmount<LevyPower>()==4);
            var thrust=f.Card<ArrayThrust>();CardCmd.Upgrade(thrust);hp=f.Enemy.CurrentHp;f.Play(thrust);Check("array thrust upgrade deals 10",hp-f.Enemy.CurrentHp==10);
            f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,f.Player.Creature.Block,f.Player.Creature));hp=f.Enemy.CurrentHp;f.Play<SoldierCharge>();Check("soldier charge floor is 4 damage and 2 block",hp-f.Enemy.CurrentHp==4&&f.Player.Creature.Block==2&&f.Enemy.GetPowerAmount<VulnerablePower>()==0);
            f.Gain(1,3);f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,f.Player.Creature.Block,f.Player.Creature));hp=f.Enemy.CurrentHp;f.Play<SoldierCharge>();Check("three soldiers are all spent for 18 extra damage",hp-f.Enemy.CurrentHp==22&&f.Player.Creature.Block==2&&f.Enemy.GetPowerAmount<VulnerablePower>()==0&&f.Player.Creature.GetPowerAmount<SoldierPower>()==0);
            f.Gain(1,1);var charge=f.Card<SoldierCharge>();CardCmd.Upgrade(charge);f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,f.Player.Creature.Block,f.Player.Creature));hp=f.Enemy.CurrentHp;f.Play(charge);Check("soldier charge upgrade spends its soldier for 5, 3 block, bonus 9",hp-f.Enemy.CurrentHp==14&&f.Player.Creature.Block==3&&f.Enemy.GetPowerAmount<VulnerablePower>()==0&&f.Player.Creature.GetPowerAmount<SoldierPower>()==0);
            f.FillDraw(4);int hand=f.Player.PlayerCombatState!.Hand.Cards.Count;f.Play<SacrificeForDuty>();Check("sacrifice spends 1 levy and draws 2",f.Player.Creature.GetPowerAmount<LevyPower>()==4&&f.Player.PlayerCombatState.Hand.Cards.Count==hand+2);
            if(f.Enemy.GetPower<VulnerablePower>() is {} mark)f.Await(PowerCmd.Remove(mark));
            f.Await(PlayerCmd.GainGold(10,f.Player));int gold=f.Player.Gold;hp=f.Enemy.CurrentHp;f.Play<PrivateeringPay>();Check("privateer with 10 gold buys an elite",f.Player.Gold==gold-10&&f.Player.Creature.GetPowerAmount<ElitePower>()==1&&hp-f.Enemy.CurrentHp==9);
            f.Await(PlayerCmd.LoseGold(f.Player.Gold,f.Player));int crime=f.Player.Creature.GetPowerAmount<CrimePower>();hp=f.Enemy.CurrentHp;f.Play<PrivateeringPay>();Check("broke privateer adds crime and no elite",f.Player.Gold==0&&f.Player.Creature.GetPowerAmount<ElitePower>()==1&&f.Player.Creature.GetPowerAmount<CrimePower>()==crime+1&&hp-f.Enemy.CurrentHp==9);
            Check("recruitment notice stays at 2 energy",ModelDb.Card<RecruitmentNotice>().EnergyCost.Canonical==2);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(PlayerCmd.GainGold(30,f.Player));f.Apply<CrimePower>(3);int gold=f.Player.Gold;f.Await(RelicCmd.Obtain<DarkScales>(f.Player));f.Await(f.Player.Relics.OfType<DarkScales>().Single().BeforeCombatStart());
            Check("dark scales pays 30 at 3 crime",f.Player.Gold==gold-30&&f.Player.Creature.HasPower<ScalesWaiverPower>());
            int hp=f.Enemy.CurrentHp;f.Play<Quickfoot>();Check("paid scales restores card damage and block",hp-f.Enemy.CurrentHp==3&&f.Player.Creature.Block==3);
            var crime=f.Player.Creature.GetPower<CrimePower>()!;Check("paid scales restores draw",crime.ModifyHandDraw(f.Player,5)==5);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(PlayerCmd.GainGold(40,f.Player));f.Apply<CrimePower>(4);int gold=f.Player.Gold;f.Await(RelicCmd.Obtain<DarkScales>(f.Player));f.Await(f.Player.Relics.OfType<DarkScales>().Single().BeforeCombatStart());
            var crime=f.Player.Creature.GetPower<CrimePower>()!;
            Check("crime 4 still costs 30 and keeps the energy penalty",f.Player.Gold==gold-30&&f.Player.Creature.HasPower<ScalesWaiverPower>()&&crime.ModifyMaxEnergy(f.Player,3)==2&&crime.ModifyDamageAdditive(f.Enemy,7,ValueProp.Move,f.Player.Creature,f.Card<Quickfoot>(),null)==0);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(PlayerCmd.LoseGold(f.Player.Gold,f.Player));f.Await(PlayerCmd.GainGold(10,f.Player));f.Apply<CrimePower>(3);f.Await(RelicCmd.Obtain<DarkScales>(f.Player));f.Await(f.Player.Relics.OfType<DarkScales>().Single().BeforeCombatStart());
            int hp=f.Enemy.CurrentHp;f.Play<Quickfoot>();Check("unpaid scales leaves the damage penalty",f.Player.Gold==10&&!f.Player.Creature.HasPower<ScalesWaiverPower>()&&hp-f.Enemy.CurrentHp==2);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(RelicCmd.Obtain<UsurpersCrown>(f.Player));f.Await(f.Player.Relics.OfType<UsurpersCrown>().Single().BeforeCombatStart());
            var first=Hand<StoneSlaughter>(f);var second=Hand<StoneSlaughter>(f);
            Check("first sultan is free to preview",first.EnergyCost.GetWithModifiers(CostModifiers.All)==0&&second.EnergyCost.GetWithModifiers(CostModifiers.All)==0);
            int hp=f.Player.Creature.CurrentHp;f.Play(first);Check("crown waives the first slaughter price",f.Player.Creature.CurrentHp==hp);
            Check("second sultan returns to normal cost",second.EnergyCost.GetWithModifiers(CostModifiers.All)==1);
            f.Play(second);Check("second slaughter still pays life",hp-f.Player.Creature.CurrentHp==1);
            f.Await(PlayerCmd.LoseGold(f.Player.Gold,f.Player));f.NextTurn();var luxury=Hand<StoneLuxury>(f);Check("next turn the first sultan is free again",luxury.EnergyCost.GetWithModifiers(CostModifiers.All)==0);
            int gold=f.Player.Gold;f.Play(luxury);Check("crown waives stone luxury gold",f.Player.Gold==gold&&f.Player.Creature.Block>=10);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Apply<PardonDecreePower>(1);var card=f.Card<StoneSlaughter>();Check("pardon does not zero energy",card.EnergyCost.GetWithModifiers(CostModifiers.All)==1);
            int hp=f.Player.Creature.CurrentHp;var original=f.AddDeck<StoneSlaughter>();original.BattlesRemaining=1;var played=(SultanCard)f.Combat.CloneCard(original);played.DeckVersion=original;f.Play(played);
            Check("pardon waives price and the play still resets the deadline",f.Player.Creature.CurrentHp==hp&&!f.Player.Creature.HasPower<PardonDecreePower>()&&original.BattlesRemaining==Rules.BattleDeadline(SultanGrade.Stone));
            f.Play<StoneSlaughter>();Check("the following sultan pays again",hp-f.Player.Creature.CurrentHp==1);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(RelicCmd.Obtain<BloodDrum>(f.Player));var drum=f.Player.Relics.OfType<BloodDrum>().Single();
            var sultan=f.Card<StoneSlaughter>();f.Await(CardPileCmd.Add(sultan,PileType.Draw));
            f.Await(drum.AfterPlayerTurnStart(f.Context,f.Player));f.NextTurn();f.Await(drum.AfterPlayerTurnStart(f.Context,f.Player));
            Check("blood drum adds one soldier per turn and does not tutor",f.Player.Creature.GetPowerAmount<SoldierPower>()==2&&f.Player.Creature.GetPowerAmount<ElitePower>()==0&&sultan.Pile?.Type==PileType.Draw&&drum.GetUpgradeReplacement()==null);
            var options=new List<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption>();drum.TryModifyRestSiteOptions(f.Player,options);
            Check("blood drum rest options drop the exchange",options.OfType<RemoveSultanOption>().Count()==1&&!options.OfType<ExchangeSultanOption>().Any());
            f.AddDeck<StoneLuxury>();var remove=options.OfType<RemoveSultanOption>().Single();int sultans=f.Player.Deck.Cards.OfType<SultanCard>().Count();Check("removing a sultan spends the selection",remove.OnSelect().GetAwaiter().GetResult()&&f.Player.Deck.Cards.OfType<SultanCard>().Count()==sultans-1);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(RelicCmd.Obtain<OldBattleFlag>(f.Player));f.Await(f.Player.Relics.OfType<OldBattleFlag>().Single().BeforeCombatStart());Check("old flag starts with 2 levies",f.Player.Creature.GetPowerAmount<LevyPower>()==2);
            f.Await(RelicCmd.Obtain<GarrisonEnlistment>(f.Player));f.Await(f.Player.Relics.OfType<GarrisonEnlistment>().Single().BeforeCombatStart());Check("checkpoint relic starts with 2 soldiers",f.Player.Creature.GetPowerAmount<SoldierPower>()==2);
            f.Gain(0,2);f.Await(RelicCmd.Obtain<GuardsOrder>(f.Player));f.Await(V04.Convert(f.Context,f.Player,2,1));Check("guards order blocks 4 per real promotion",f.Player.Creature.Block==8&&f.Player.Creature.GetPowerAmount<SoldierPower>()==4);
            f.FillDraw(2);int hand=f.Player.PlayerCombatState!.Hand.Cards.Count;f.Await(RelicCmd.Obtain<SultanBronzeRing>(f.Player));f.Play<StoneSlaughter>();Check("bronze ring draws after a sultan",f.Player.PlayerCombatState.Hand.Cards.Count==hand+1&&ModelDb.Relic<SultanBronzeRing>().Rarity==RelicRarity.Rare);
            int gold=f.Player.Gold;int crime=f.State.Crime;f.Await(RelicCmd.Obtain<UsuryContract>(f.Player));Check("usury pays 250 and records 2 crime",f.Player.Gold==gold+250&&f.State.Crime==crime+2);
        }
        using(var f=new V04Verification.Fixture())
        {
            f.Await(PlayerCmd.LoseGold(f.Player.Gold,f.Player));
            var border=ModelDb.Event<BorderTaxCollectorEvent>().ToMutable();
            Check("border event is offered to Artu",border.IsAllowed(f.Player.RunState));
            var poor=Options(border,f.Player);Check("50 gold stays available without a sultan or a purse",poor.Count==2);
            f.Await(PlayerCmd.GainGold(100,f.Player));Check("bribe appears once the purse can pay",Options(border,f.Player).Count==3);
            f.Await(RelicCmd.Obtain<GarrisonEnlistment>(f.Player));Check("checkpoint option closes after the relic is owned",Options(border,f.Player).Count==2);
            var mutiny=ModelDb.Event<PalaceGuardMutinyEvent>().ToMutable();
            Check("mutiny offers pay, fight, and leave",Options(mutiny,f.Player).Count==3);
            f.Await(RelicCmd.Obtain<GuardsOrder>(f.Player));Check("mutiny pay closes once the order is owned",Options(mutiny,f.Player).Count==2);
        }
        Check("horn is uncommon and holy water can be used outside combat",ModelDb.Potion<ConscriptionHornPotion>().Rarity==PotionRarity.Uncommon&&ModelDb.Potion<HolyAmnestyWaterPotion>().Usage==PotionUsage.AnyTime&&ModelDb.Potion<PardonDecreePotion>().Rarity==PotionRarity.Uncommon);
        var potions=ModelDb.Character<Artu>().PotionPool.GetUnlockedPotions(UnlockState.all).ToArray();
        Check("character potion pool is the three Artu potions",potions.Length==3&&potions.Any(p=>p is ConscriptionHornPotion)&&potions.Any(p=>p is PardonDecreePotion)&&potions.Any(p=>p is HolyAmnestyWaterPotion));
        Check("hex registration was attempted",HextechBridge.Attempted);
        Check("crown relic icon is packed",ResourceLoader.Exists("res://ArtuMod/images/usurperscrown.tres"));
        return checks;
        }
        finally{TestMode.IsOn=false;}
    }
    static T Hand<T>(V04Verification.Fixture f)where T:CardModel
    {
        var original=f.AddDeck<T>();var card=(T)f.Combat.CloneCard(original);
        if(card is SultanCard sultan&&original is SultanCard deck)sultan.DeckVersion=deck;
        f.Await(CardPileCmd.Add(card,PileType.Hand));return card;
    }
    static IReadOnlyList<EventOption> Options(EventModel model,Player player)
    {
        AccessTools.Property(typeof(EventModel),"Owner").SetValue(model,player);
        return (IReadOnlyList<EventOption>)AccessTools.Method(model.GetType(),"GenerateInitialOptions").Invoke(model,null)!;
    }
    sealed class TakeFirst:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int min,int max)=>Task.FromResult<IEnumerable<CardModel>>(options.Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
