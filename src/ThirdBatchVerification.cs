using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;
public static class ThirdBatchVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var checks=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("third-batch "+name);}
        try
        {
            using(var f=new V04Verification.Fixture())
            {
                Check("might is rare reward",V04.RewardCards(f.Player).Any(c=>c is ArmyMight&&c.Rarity==CardRarity.Rare));
                var c=f.Card<ArmyMight>();Check("base 3 energy 8 events",c.EnergyCost.Canonical==3&&c.DynamicVars["Hits"].IntValue==8);
                CardCmd.Upgrade(c);Check("upgraded 3 energy 12 events",c.EnergyCost.GetWithModifiers(CostModifiers.All)==3&&c.DynamicVars["Hits"].IntValue==12);
                f.Play(c);f.Play<ArmyMight>();Check("counter stacking adds finite caps",f.Player.Creature.GetPower<ArmyMightPower>()!.Amount==20);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Apply<StrengthPower>(3);f.Apply<WeakPower>(2);f.Apply<VigorPower>(10);f.Apply<DexterityPower>(8);f.Apply<FrailPower>(2);
                f.Await(PowerCmd.Apply<VulnerablePower>(f.Context,f.Enemy,2,f.Player.Creature,null));
                f.Play<ArmyMight>();f.Gain(1,9);int hp=f.Enemy.CurrentHp;
                f.Await(f.Player.Creature.GetPower<SoldierPower>()!.Trigger(f.Context));
                Check("first 8 damage events use strength and native vulnerable only",hp-f.Enemy.CurrentHp==58);
                Check("soldier block remains fixed and vigor not spent",f.Player.Creature.Block==9&&f.Player.Creature.GetPowerAmount<VigorPower>()==10);
                hp=f.Enemy.CurrentHp;f.Gain(3,1);f.Await(f.Player.Creature.GetPower<ElitePower>()!.Trigger(f.Context));
                Check("other formation and extra settlement share exhausted cap",hp-f.Enemy.CurrentHp==4);
                f.NextTurn();hp=f.Enemy.CurrentHp;f.Await(f.Player.Creature.GetPower<ElitePower>()!.Trigger(f.Context));
                Check("next player turn renews cap",hp-f.Enemy.CurrentHp==10&&f.Player.Creature.GetPower<ArmyMightPower>()!.Remaining==7);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<ArmyMight>();f.Apply<StrengthPower>(-4);f.Gain(0,1);int hp=f.Enemy.CurrentHp;f.Await(f.Player.Creature.GetPower<LevyPower>()!.Trigger(f.Context));
                Check("negative strength clamps to zero and consumes event",hp==f.Enemy.CurrentHp&&f.Player.Creature.GetPower<ArmyMightPower>()!.Remaining==7);
                f.Await(PowerCmd.Remove(f.Player.Creature.GetPower<StrengthPower>()!));f.Apply<StrengthPower>(3);
                f.Await(CreatureCmd.GainBlock(f.Enemy,20,ValueProp.Unpowered,null));f.Await(f.Player.Creature.GetPower<LevyPower>()!.Trigger(f.Context));
                Check("empowered troop damage still hits enemy block",f.Enemy.Block==15&&hp==f.Enemy.CurrentHp);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.AddDeck<NoteInBook>();f.State.LeviesConverted=14;f.Gain(0,1);f.Gain(1,1);
                Check("direct troop gains are not conversions",f.State.LeviesConverted==14&&(f.State.AgendaFlags&64)==0);
                f.Await(V04.Convert(f.Context,f.Player,1,2));Check("actual 14 to 15 conversion unlocks nursery",f.State.LeviesConverted==15&&(f.State.AgendaFlags&64)!=0);
                f.Gain(1,3);f.Gain(2,3);f.Gain(3,4);Check("three advanced at four do not unlock strength",(f.State.AgendaFlags&8)==0);
                f.Gain(1,1);f.Gain(2,1);f.Gain(3,1);Check("all advanced simultaneous five unlocks strength",(f.State.AgendaFlags&8)!=0);
                f.Apply<CrimePower>(1);f.Gain(0,10);Check("crime blocks clean unlock",(f.State.AgendaFlags&4)==0);
                f.Await(V04.RemoveCrime(f.Context,f.Player,1));Check("zero crime and ten levies unlock",(f.State.AgendaFlags&4)!=0);
                f.Play<NoteInBook>();Check("note grants unlocked troops",f.Player.Creature.GetPowerAmount<LevyPower>()==20&&f.Player.Creature.GetPowerAmount<SoldierPower>()==7);
                f.Play<NoteInBook>();Check("copy does not grant immediate troops twice",f.Player.Creature.GetPowerAmount<LevyPower>()==20);
                var saved=(WitchCardBox)RelicModel.FromSerializable(f.State.ToSerializable());Check("unlocks and real conversion counts persist",saved.AgendaFlags==f.State.AgendaFlags&&saved.LeviesConverted==15);
                Check("details use actual permanent conversion count and reward",AgendaDetails.Entry(f.Player,6).Contains("15/15")&&AgendaDetails.Entry(f.Player,6).Contains("至多2"));
            }
            using(var f=new V04Verification.Fixture())
            {
                f.AddDeck<NoteInBook>();f.State.ReadingUses=9;f.State.ReadingNames="a;b";f.Await(RevolutionAgenda.Refresh(f.Player,f.Context));Check("reading requires both thresholds",(f.State.AgendaFlags&16)==0);
                f.Play<NoteInBook>();Check("note excluded from reading and thought counts",f.State.ReadingUses==9&&V04.Data(f.Player).Thoughts.Count==0);
                f.State.ReadingUses=10;f.Await(RevolutionAgenda.Refresh(f.Player,f.Context));Check("ten readings of two names insufficient",(f.State.AgendaFlags&16)==0);
                f.State.ReadingNames="a;b;c";f.Await(RevolutionAgenda.Refresh(f.Player,f.Context));Check("ten readings of three names unlock",(f.State.AgendaFlags&16)!=0);
                f.Play<PureThought>();f.Play<UncleanThought>();Check("two thought names insufficient",(f.State.AgendaFlags&32)==0);
                f.Play<FearOfSultan>();Check("third name unlocks without victory",(f.State.AgendaFlags&32)!=0);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.AddDeck<NoteInBook>();var cards=f.Player.Deck.Cards.OfType<SultanCard>().ToArray();
                f.Await(CardPileCmd.RemoveFromDeck(cards.Take(3).Cast<CardModel>().ToArray()));Check("five Sultan cards not freedom",(f.State.AgendaFlags&1)==0);
                f.Await(CardPileCmd.RemoveFromDeck(cards[3]));Check("four Sultan cards unlock first freedom",(f.State.AgendaFlags&1)!=0&&(f.State.AgendaFlags&2)==0);
                f.Await(CardPileCmd.RemoveFromDeck(cards.Skip(4).Cast<CardModel>().ToArray()));Check("zero Sultan unlock second freedom",(f.State.AgendaFlags&2)!=0);
                var original=f.AddDeck<Quickfoot>();var battle=(ArtuCard)f.Combat.CloneCard(original);battle.DeckVersion=original;f.Await(CardPileCmd.Add(battle,PileType.Hand));f.Play<NoteInBook>();
                Check("note upgrades combat card only",battle.IsUpgraded&&!original.IsUpgraded);
            }
            using(var f=new V04Verification.Fixture())
            {
                for(int k=1;k<=3;k++){var c=ArmyChoice.Create(k);Check("choice title and current rule "+k,c.Title==ArmyInfo.Name(k)&&c.GetDescriptionForPile(PileType.None).Contains(ArtuTerms.Rules[c.Title])&&!c.Title.Contains('{'));}
                var a=f.Player.Creature;var x=PowerCmd.Apply<AcademyPower>(f.Context,a,4,a,null).GetAwaiter().GetResult()!;x.Kind=1;
                var y=PowerCmd.Apply<AcademyPower>(f.Context,a,6,a,null).GetAwaiter().GetResult()!;y.Kind=3;
                Check("academy instances keep targets and caps independent",!ReferenceEquals(x,y)&&x.Kind==1&&y.Kind==3&&x.Amount==4&&y.Amount==6);
                Check("academy target dynamic strings are isolated",((StringVar)x.DynamicVars["Army"]).StringValue=="士兵"&&((StringVar)y.DynamicVars["Army"]).StringValue=="锐士");
                f.Gain(0,1);f.State.LeviesConverted=15;f.State.AgendaFlags=64;f.Apply<RevolutionNotePower>(1);
                f.Await(x.AfterPlayerTurnStart(f.Context,f.Player));f.Await(y.AfterPlayerTurnStart(f.Context,f.Player));f.Await(a.GetPower<RevolutionNotePower>()!.AfterPlayerTurnStart(f.Context,f.Player));
                Check("academy and nursery consume only available levies",f.State.LeviesConverted==20&&a.GetPowerAmount<LevyPower>()==0);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Apply<CrimePower>(4);f.Player.PlayerCombatState!.ResetEnergy();int energy=f.Player.PlayerCombatState.Energy;
                Check("crime reduces natural max energy by one",energy==f.Player.MaxEnergy-1);
                f.Await(PlayerCmd.GainEnergy(2,f.Player));Check("crime no longer taxes card energy gains",f.Player.PlayerCombatState.Energy==energy+2);
                f.State.Anger=7;while(f.Player.PlayerCombatState.TurnNumber<1)f.NextTurn();f.Await(f.State.AfterEnergyReset(f.Player));Check("anger first turn deduction is additional",f.Player.PlayerCombatState.Energy==energy+1);
                var c=f.AddDeck<StoneLuxury>();c.BattlesRemaining=2;Check("two battles no border",!DeadlineWarning.Urgent(c));c.BattlesRemaining=1;Check("last battle warns",DeadlineWarning.Urgent(c));c.UsedThisBattle=true;Check("fulfilled removes border",!DeadlineWarning.Urgent(c));c.UsedThisBattle=false;V04.Data(f.Player).Grace=true;Check("frozen deadline has no border",!DeadlineWarning.Urgent(c));
                V04.Data(f.Player).Grace=false;var g=f.AddDeck<GoldLuxury>();g.BattlesRemaining=2;g.TurnsSinceUse=4;Check("gold last turn independently warns",DeadlineWarning.Urgent(g));
                f.State.NoBuyingShops=1;f.Await(f.State.AfterRoomEntered(new MerchantRoom()));Check("next shop activates ban",f.State.NoBuyingShops==0&&f.State.ActiveShopBan);
                f.Await(f.State.AfterRoomEntered(new MerchantRoom()));Check("same shop restoration retains ban",f.State.ActiveShopBan);
                var saved=(WitchCardBox)RelicModel.FromSerializable(f.State.ToSerializable());Check("ban scope persists in save",saved.ActiveShopBan&&saved.ActiveShopFloor==f.State.ActiveShopFloor);
            }
            return checks;
        }
        finally{V04Verification.Fixture.ResetCombat();TestMode.IsOn=false;}
    }
}
