using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
namespace ArtuMod;

public static class V03Verification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var passed=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);passed.Add(name);}
        using var selector=CardSelectCmd.UseSelector(new FirstSelector());
        try
        {
            var pool=ModelDb.CardPool<ArtuCardPool>().AllCards.ToArray();
            Check("v03 normal pool: 20 common, 28 uncommon, 11 rare",pool.Count(c=>c.Rarity==CardRarity.Common)==20&&pool.Count(c=>c.Rarity==CardRarity.Uncommon)==28&&pool.Count(c=>c.Rarity==CardRarity.Rare)==11);
            Check("v03 unfinished tier-three/final thoughts excluded from normal rewards",pool.OfType<UnfinishedThought>().All(c=>c.Rarity is CardRarity.Event or CardRarity.Ancient&&!c.CanBeGeneratedInCombat&&!c.CanBeGeneratedByModifiers));
            Check("v03 three second thoughts are 1-energy uncommon powers without upgrades",new CardModel[]{ModelDb.Card<TouchPurity>(),ModelDb.Card<GlimpseDarkness>(),ModelDb.Card<FreedomFromFear>()}.All(c=>c.Type==CardType.Power&&c.Rarity==CardRarity.Uncommon&&c.EnergyCost.Canonical==1&&c.MaxUpgradeLevel==0));
            Check("v03 all Sultan cards remain Curse outside use predicates",pool.OfType<SultanCard>().All(c=>c.Type==CardType.Curse&&!c.IsRemovable&&!c.IsTransformable&&!c.IsUpgradable)&&!pool.Where(c=>c.Type==CardType.Attack||c.Type==CardType.Skill).OfType<SultanCard>().Any());
            Check("v03 native usage/history comparison patches found",NativeSultanUsageTypes.PatchedMethods>=20);
            Check("v03 Becky upgrades only add Innate",UpgradeCheck<SuperBecky>(c=>c.Keywords.Contains(CardKeyword.Innate)&&c.EnergyCost.GetWithModifiers(CostModifiers.All)==3));
            Check("v03 support upgrade removes Exhaust",UpgradeCheck<SupportFromAllSides>(c=>!c.Keywords.Contains(CardKeyword.Exhaust)));
            Check("v03 crime replacement card does not exhaust",!ModelDb.Card<CrimeOfDeceiving>().CanonicalKeywords.Contains(CardKeyword.Exhaust));
            Check("v031 card box uses a hand-selection prompt",new MegaCrit.Sts2.Core.Localization.LocString("card_selection","ARTU_SULTAN_TO_HAND").GetFormattedText()=="选择一张苏丹牌加入手牌。");
            Check("v031 all four luxury cards target self and keep Curse identity",pool.OfType<SultanCard>().Where(c=>c.Suit==SultanSuit.Luxury).All(c=>c.TargetType==TargetType.Self&&c.Type==CardType.Curse));
            foreach(var grade in Enum.GetValues<SultanGrade>())
            using(var f=new Fixture())
            {
                f.Await(PowerCmd.Apply<VulnerablePower>(f.Context,f.Enemy,1,f.Player.Creature,null));
                var card=(SultanCard)f.Combat.CreateCard(SultanCard.Get(SultanSuit.Slaughter,grade),f.Player);
                f.Await(CardPileCmd.Add(card,PileType.Hand));
                card.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,card.DynamicVars);
                int expected=(int)Math.Floor(Rules.SlaughterDamage(grade)*1.5m);
                Check($"v031 {grade} slaughter previews vulnerable damage",(int)Math.Floor(card.DynamicVars.Damage.PreviewValue)==expected&&card.Localization.Any(x=>x.Item2.Contains("{Damage:diff()}")));
                if(grade>=SultanGrade.Silver)f.Await(CreatureCmd.GainBlock(f.Enemy,20,ValueProp.Unpowered,null));
                int hp=f.Enemy.CurrentHp;f.Play(card);
                Check($"v031 {grade} slaughter actually applies vulnerable once",hp-f.Enemy.CurrentHp==expected);
                if(grade>=SultanGrade.Silver)Check($"v031 {grade} slaughter still ignores block",f.Enemy.Block==20);
            }
            using(var f=new Fixture())
            {
                f.Await(PowerCmd.Apply<VulnerablePower>(f.Context,f.Enemy,1,f.Player.Creature,null));
                var card=f.Card<DispatchTroops>();f.Await(CardPileCmd.Add(card,PileType.Hand));
                card.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,card.DynamicVars);
                Check("v031 starter skill previews vulnerable damage",card.Type==CardType.Skill&&card.DynamicVars.Damage.PreviewValue==9&&card.Localization.Any(x=>x.Item2.Contains("{Damage:diff()}")));
                int hp=f.Enemy.CurrentHp;f.Play(card);
                Check("v031 starter skill actually applies vulnerable once",hp-f.Enemy.CurrentHp==9);
            }
            using(var f=new Fixture())
            {
                f.Await(PowerCmd.Apply<VulnerablePower>(f.Context,f.Enemy,1,f.Player.Creature,null));
                f.Await(PowerCmd.Apply<ShrinkPower>(f.Context,f.Player.Creature,-1,f.Enemy,null));
                var slaughter=f.Card<StoneSlaughter>();f.Await(CardPileCmd.Add(slaughter,PileType.Hand));
                slaughter.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,slaughter.DynamicVars);
                int hp=f.Enemy.CurrentHp;f.Play(slaughter);
                Check("v031 shrink plus vulnerable gives stone slaughter 11 preview and actual damage",Math.Floor(slaughter.DynamicVars.Damage.PreviewValue)==11&&hp-f.Enemy.CurrentHp==11);
                var skill=f.Card<DispatchTroops>();f.Await(CardPileCmd.Add(skill,PileType.Hand));
                skill.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,skill.DynamicVars);
                hp=f.Enemy.CurrentHp;f.Play(skill);
                Check("v031 shrink plus vulnerable gives starter skill 6 preview and actual damage",Math.Floor(skill.DynamicVars.Damage.PreviewValue)==6&&hp-f.Enemy.CurrentHp==6);
            }
            Check("v03 tribunal is colorless rare but event-only",ModelDb.Card<Tribunal>().Rarity==CardRarity.Rare&&ModelDb.Card<Tribunal>().Pool.IsColorless&&!pool.Any(c=>c is Tribunal)&&!ModelDb.Card<Tribunal>().CanBeGeneratedInCombat);

            using(var f=new Fixture())
            {
                var kunai=ModelDb.Relic<Kunai>().ToMutable();f.Player.AddRelicInternal(kunai);
                f.Apply<UseIdentityProbe>(1);
                for(int i=0;i<3;i++)f.Play<StoneSlaughter>();
                Check("v03 native Kunai counts actual Sultan slaughter plays",f.Player.Creature.GetPowerAmount<DexterityPower>()==1);
                Check("v03 Curse-use listener still sees the same three Curse plays",f.Player.Creature.GetPower<UseIdentityProbe>()!.CurseUses==3);
                var opener=ModelDb.Relic<LetterOpener>().ToMutable();f.Player.AddRelicInternal(opener);int hp=f.Enemy.CurrentHp;
                for(int i=0;i<3;i++)f.Play<BronzeConquest>();
                Check("v03 native Letter Opener counts actual Sultan skill plays",hp-f.Enemy.CurrentHp==5);
                Check("v03 Curse identity survives virtual skill hooks",f.Player.Creature.GetPower<UseIdentityProbe>()!.CurseUses==6);
                Check("v03 distinct used Sultan kinds ignore duplicate copies",SultanUsage.UsedKinds(f.Player).Count==2);
            }
            using(var f=new Fixture())
            {
                var attack=f.Play<StoneSlaughter>();var skill=f.Play<StoneLuxury>();
                var entries=(List<CombatHistoryEntry>)AccessTools.Field(typeof(CombatHistory),"_entries").GetValue(CombatManager.Instance.History)!;
                entries.Add(new CardPlayFinishedEntry(attack,f.Combat.RoundNumber,f.Combat.CurrentSide,CombatManager.Instance.History,f.Combat.Players));
                entries.Add(new CardPlayFinishedEntry(skill,f.Combat.RoundNumber,f.Combat.CurrentSide,CombatManager.Instance.History,f.Combat.Players));
                var finisher=f.Card<MegaCrit.Sts2.Core.Models.Cards.Finisher>();
                f.Await(CardPileCmd.Add(finisher,PileType.Hand));
                Check("v03 native history attack predicate counts slaughter but not luxury",((CalculatedVar)finisher.DynamicVars["CalculatedHits"]).Calculate(f.Enemy)==1);
            }
            using(var f=new Fixture())
            {
                f.Apply<UseIdentityProbe>(1);
                var original=f.Play<StoneSlaughter>().Card as SultanCard;
                original!.BattlesRemaining=1;original.TurnsSinceUse=2;original.UsedThisBattle=false;
                int hp=f.Enemy.CurrentHp,own=f.Player.Creature.CurrentHp;
                f.Play<FollowFootsteps>();f.Play<FollowFootsteps>();
                Check("v03 effect copies execute damage and cost twice without recursion",hp-f.Enemy.CurrentHp==22&&own-f.Player.Creature.CurrentHp==2);
                Check("v03 copying Sultan does not reset its deadline",original.BattlesRemaining==1&&original.TurnsSinceUse==2&&!original.UsedThisBattle);
                Check("v03 effect copies do not create extra Sultan-use records",SultanUsage.UsedKinds(f.Player).Count==1&&f.Player.Creature.GetPower<UseIdentityProbe>()!.CurseUses==1);
            }
            using(var f=new Fixture())
            {
                f.Apply<KingsBurdenPower>(3);f.Play<StoneSlaughter>();
                Check("v03 successful cost gives burden block and one turn payment",f.Player.Creature.Block==3&&PaymentLedger.ThisTurn(f.Player)==1);
                int hp=f.Enemy.CurrentHp;f.Play<UnderKingsOrder>();Check("v03 king order damage is 8 plus 3 per payment",hp-f.Enemy.CurrentHp==11);
                f.NextTurn();Check("v03 payment count resets by player turn",PaymentLedger.ThisTurn(f.Player)==0);
                f.Play<MercenaryContract>();Check("v03 mercenary contract spends 15 gold and recruits 2 soldiers 1 elite",f.Player.Gold==84&&f.Player.Creature.GetPowerAmount<SoldierPower>()==2&&f.Player.Creature.GetPowerAmount<ElitePower>()==1);
            }
            foreach(int n in new[]{1,3,10})foreach(bool upgraded in new[]{false,true})
            using(var f=new Fixture())
            {
                f.Apply<SoldierPower>(n);int hp=f.Enemy.CurrentHp;
                var card=f.Card<SoldierCharge>();if(upgraded)CardCmd.Upgrade(card);
                f.Play(card);
                Check($"v03 charge consumes all {n} soldiers upgraded={upgraded}",f.Player.Creature.GetPowerAmount<SoldierPower>()==0&&hp-f.Enemy.CurrentHp==8+n*(upgraded?5:4)&&f.Player.Creature.Block==5);
            }
            using(var f=new Fixture())
            {
                int hp=f.Enemy.CurrentHp;var card=f.Card<SoldierCharge>();
                Check("v03 zero-soldier charge is not normally playable",!(bool)AccessTools.Property(typeof(CardModel),"IsPlayable").GetValue(card)!);
                f.Play(card);Check("v03 forced zero-soldier charge has no damage/block benefit",f.Enemy.CurrentHp==hp&&f.Player.Creature.Block==0);
            }
            using(var f=new Fixture())
            {
                f.Apply<BannerResonancePower>(1);f.Gain(0,1);f.Gain(0,1);f.Gain(1,1);
                Check("v03 banner tracks first gain separately for each type",f.Player.Creature.GetPowerAmount<LevyPower>()==3&&f.Player.Creature.GetPowerAmount<SoldierPower>()==2);
                f.NextTurn();f.Gain(0,1);Check("v03 banner resets next player turn without recursion",f.Player.Creature.GetPowerAmount<LevyPower>()==5);
            }
            using(var f=new Fixture())
            {
                f.Apply<SoldierPower>(2);f.Apply<VeteranPower>(2);f.Apply<ElitePower>(2);f.Apply<ArmySpecializationPower>(1);
                var special=f.Player.Creature.GetPower<ArmySpecializationPower>()!;special.FormationKind=2;
                int hp=f.Enemy.CurrentHp;
                foreach(var army in ArmyPower.All(f.Player).ToArray())f.Await(army.BeforeSideTurnEnd(f.Context,CombatSide.Player,[f.Player.Creature]));
                Check("v03 specialization combines advanced troops and resolves only once",hp-f.Enemy.CurrentHp==17&&f.Enemy.GetPowerAmount<VulnerablePower>()==1);
                Check("v03 specialization preserves real stacks",f.Player.Creature.GetPowerAmount<SoldierPower>()==2&&f.Player.Creature.GetPowerAmount<VeteranPower>()==2&&f.Player.Creature.GetPowerAmount<ElitePower>()==2);
                f.Await(special.AfterSideTurnEnd(f.Context,CombatSide.Player,[f.Player.Creature]));
                Check("v03 specialization expires after the player side",ArmyPower.Formations(f.Player).Count()==3&&!f.Player.Creature.HasPower<ArmySpecializationPower>());
            }
            using(var f=new Fixture())
            {
                f.Apply<SoldierPower>(2);f.Apply<VeteranPower>(1);f.Apply<ElitePower>(1);f.Apply<ArmyInterchangePower>(1);
                f.Player.Creature.GetPower<ArmyInterchangePower>()!.CountSoldiersForThreshold=true;int hp=f.Enemy.CurrentHp;
                f.Await(f.Player.Creature.GetPower<VeteranPower>()!.Trigger(f.Context));
                Check("v03 interchange adds elite synergy and counts soldiers only for threshold",hp-f.Enemy.CurrentHp==8&&!f.Enemy.HasPower<VulnerablePower>());
                f.Await(f.Player.Creature.GetPower<ElitePower>()!.Trigger(f.Context));Check("v03 interchange elite block requires soldiers",f.Player.Creature.Block==1);
            }
            using(var f=new Fixture())
            {
                f.Apply<LivingOffWarPower>(1);for(int i=0;i<4;i++)f.Gain(i,1);f.FillDraw(10);int energy=f.Player.PlayerCombatState!.Energy;
                f.Play<Drill>();Check("v03 simultaneous troop consumption rewards its full quantity once",ArmyPower.All(f.Player).Sum(p=>p.Amount)==4&&f.Player.PlayerCombatState.Energy==energy+4);
                f.Play<Drill>();Check("v03 living off war does not trigger twice in one turn",ArmyPower.All(f.Player).Sum(p=>p.Amount)==3);
            }
            using(var f=new Fixture())
            {f.Apply<LivingOffWarPower>(1);f.Gain(0,3);f.Play<FieldReorganization>();Check("v03 conversion is not falsely treated as consuming troops",ArmyPower.All(f.Player).Sum(a=>a.Amount)==3&&f.Player.Creature.Block==9);}
            using(var f=new Fixture())
            {
                f.Apply<PurityInsightPower>(3);var first=f.Card<TrainArmy>();var second=f.Card<TrainArmy>();
                f.DrawHook(first);f.DrawHook(second);Check("v03 three purity stacks discount only one card",first.EnergyCost.GetWithModifiers(CostModifiers.All)==1&&second.EnergyCost.GetWithModifiers(CostModifiers.All)==2);
                first.EnergyCost.EndOfTurnCleanup();Check("v03 purity discount expires this turn",first.EnergyCost.GetWithModifiers(CostModifiers.All)==2);
                f.NextTurn();f.Apply<RequisitionPower>(1);f.NextTurn();var combined=f.Card<TrainArmy>();f.DrawHook(combined);
                Check("v03 purity and warlord tax combine once",combined.EnergyCost.GetWithModifiers(CostModifiers.All)==2);
                f.NextTurn();var x=f.Combat.CreateCard(ModelDb.AllCards.First(c=>c.EnergyCost.CostsX),f.Player);var afterX=f.Card<TrainArmy>();f.DrawHook(x);f.DrawHook(afterX);
                Check("v03 X draw consumes purity opportunity without changing X",x.EnergyCost.CostsX&&!x.EnergyCost.HasLocalModifiers&&afterX.EnergyCost.GetWithModifiers(CostModifiers.All)==2);
            }
            using(var f=new Fixture())
            {
                f.Apply<SecretInsightPower>(3);f.FillDraw(10);int hp=f.Player.Creature.CurrentHp,hand=f.Player.PlayerCombatState!.Hand.Cards.Count;
                f.Play<StoneSlaughter>();f.Play<StoneSlaughter>();
                Check("v03 three secret insight stacks lose extra 2 HP and draw 2 only once",hp-f.Player.Creature.CurrentHp==4&&f.Player.PlayerCombatState.Hand.Cards.Count==hand+2);
            }
            using(var f=new Fixture())
            {
                f.Apply<AwakeningPower>(3);f.FillDraw(10);int hand=f.Player.PlayerCombatState!.Hand.Cards.Count;
                f.Play<Quickfoot>();f.Play<Quickfoot>();
                Check("v03 three awakening stacks grant one soldier/draw once",f.Player.Creature.GetPowerAmount<SoldierPower>()==1&&f.Player.PlayerCombatState.Hand.Cards.Count==hand+1);
            }
            using(var f=new Fixture())
            {
                f.Apply<AwakeningPower>(3);f.FillDraw(10);int hand=f.Player.PlayerCombatState!.Hand.Cards.Count;
                f.Play<StoneSlaughter>();f.Play<Quickfoot>();
                Check("v03 earlier Sultan blocks awakening draw but not soldier",f.Player.Creature.GetPowerAmount<SoldierPower>()==1&&f.Player.PlayerCombatState.Hand.Cards.Count==hand);
            }
            using(var f=new Fixture())
            {
                f.State.PureSecond=3;f.State.DarkSecond=3;f.AddDeck<TouchPurity>();f.AddDeck<GlimpseDarkness>();int count=f.Player.Deck.Cards.Count;
                f.Await(Thoughts.ResolveSecond(f.Player,f.State,f.Context));
                Check("v03 purity/secret insight mutually block end-of-combat generation",f.Player.Deck.Cards.Count==count&&!f.Player.Deck.Cards.Any(c=>c is AtonementPrayer or AncientFaith));
                Check("v03 resolved second counters cannot duplicate future event rewards",f.State.PureSecond==0&&f.State.DarkSecond==0);
            }
            using(var f=new Fixture())
            {
                f.State.PureSecond=3;f.AddDeck<TouchPurity>();f.AddDeck<TouchPurity>();
                f.Await(Thoughts.ResolveSecond(f.Player,f.State,f.Context));
                Check("v03 purity evolution removes all originals and grants explicit unfinished next tier",!f.Player.Deck.Cards.Any(c=>c is TouchPurity)&&f.Player.Deck.Cards.Count(c=>c is AtonementPrayer)==1);
            }
            using(var f=new Fixture())
            {
                f.State.AwakeSecond=3;f.State.Submission=1;f.AddDeck<FreedomFromFear>();int count=f.Player.Deck.Cards.Count;
                f.Await(Thoughts.ResolveSecond(f.Player,f.State,f.Context));
                Check("v03 submission blocks awakening progression only",f.Player.Deck.Cards.Count==count&&!f.Player.Deck.Cards.Any(c=>c is RebellionIntent));
            }
            using(var f=new Fixture())
            {
                f.FillDraw(10);f.Apply<SecretAssemblyPower>(1);f.Player.Creature.GetPower<SecretAssemblyPower>()!.DrawCount=1;
                var note=f.Card<NoteInBook>();var play=f.MakePlay(note);f.Await(Hook.BeforeCardPlayed(f.Combat,play));f.Await(Hook.AfterCardPlayed(f.Combat,f.Context,play));
                Check("v03 note triggers Reading and Thought independently",f.Player.Creature.GetPowerAmount<SoldierPower>()==1&&f.Player.PlayerCombatState!.Hand.Cards.Count==1);
            }
            using(var f=new Fixture())
            {
                var gold=f.Card<GoldConquest>();f.Play<SilverConquest>();
                Check("v03 silver first prevents full gold block cost",!gold.CanPayTarget(f.Enemy));
                f.Play(gold);Check("v03 unaffordable gold does not reset deadline or pay a partial cost",!gold.UsedThisBattle&&f.State.CostsPaid==1);
            }
            using(var f=new Fixture())
            {
                f.Play<GoldConquest>();f.Play<SilverConquest>();Check("v03 gold then silver leaves 25 block",f.Enemy.Block==25);
                f.Await(CreatureCmd.Damage(f.Context,f.Enemy,25,ValueProp.Unpowered,f.Player.Creature));
                Check("v03 actually breaking remaining challenge block grants 5 strength/dexterity",f.Player.Creature.GetPowerAmount<StrengthPower>()==5&&f.Player.Creature.GetPowerAmount<DexterityPower>()==3);
            }
            using(var f=new Fixture())
            {
                f.Apply<BurstPower>(1);var skill=f.Card<StoneLuxury>();
                Check("v03 native skill replay hook recognizes Sultan usage type",f.Player.Creature.GetPower<BurstPower>()!.ModifyCardPlayCount(skill,f.Enemy,1)==2&&skill.Type==CardType.Curse);
                f.Apply<ReplaySultanPower>(2);
                using(SultanPayments.AddReplay(skill))Check("v03 explicit bronze replay adds to native replay once",Hook.ModifyCardPlayCount(f.Combat,skill,1,f.Enemy,out _)==4);
                Check("v03 explicit replay scope cannot leak to later plays",!SultanPayments.HasExtraReplay(skill));
            }
            using(var f=new Fixture())
            {
                f.Apply<DoubleCostPower>(1);var card=f.Card<BronzeSlaughter>();SultanPayments.ReplaceNextCost(card);int hp=f.Player.Creature.CurrentHp;
                f.Play(card);Check("v03 replacement pays one crime instead of double original cost",f.Player.Creature.CurrentHp==hp&&f.Player.Creature.GetPowerAmount<CrimePower>()==1&&f.State.CostsPaid==1);
            }
            using(var f=new Fixture())
            {
                f.Await(PlayerCmd.GainGold(100,f.Player));f.Play<GoldLuxury>();var power=f.Player.Creature.GetPower<GoldenLuxuryPower>()!;
                power.TurnsRemaining=1;power.CardsLeft=0;f.Play<GoldLuxury>();
                Check("v03 gold luxury refreshes duration and quota without stacking",power.Amount==1&&power.TurnsRemaining==3&&power.CardsLeft==2&&power.DisplayAmount==3);
            }
            var handList="ABCDEFG".Select(c=>c.ToString()).ToList();var order=new List<string>();
            BeckyController.Scan(()=>handList.Count,index=>{order.Add(handList[index]);handList.RemoveAt(index);return Task.CompletedTask;}).GetAwaiter().GetResult();
            Check("v03 Becky realtime positions implement ADGC",string.Join("",order)=="ADGC"&&string.Join("",handList)=="BEF");
            handList="ABCDE".Select(c=>c.ToString()).ToList();order=[];
            BeckyController.Scan(()=>handList.Count,index=>{var card=handList[index];order.Add(card);handList.RemoveAt(index);if(card=="A")handList.AddRange(["F","G"]);return Task.CompletedTask;}).GetAwaiter().GetResult();
            Check("v03 Becky rereads hand after generated cards",string.Join("",order)=="ADGC");
            return passed;
        }
        finally{Reset();TestMode.IsOn=false;}
    }
    static bool UpgradeCheck<T>(Func<CardModel,bool> test)where T:CardModel
    {var card=ModelDb.Card<T>().ToMutable();CardCmd.Upgrade(card);return test(card);}
    static void Reset()
    {var field=AccessTools.Field(typeof(CombatManager),"_turnState");if(field.GetValue(CombatManager.Instance) is {} turn)AccessTools.Method(turn.GetType(),"Cancel").Invoke(turn,null);field.SetValue(CombatManager.Instance,null);CombatManager.Instance.History.Clear();}
    sealed class Fixture:IDisposable
    {
        public Player Player {get;}=Player.CreateForNewRun<Artu>(UnlockState.all,1);
        public CombatState Combat {get;}public Creature Enemy {get;}
        public WitchCardBox State=>Util.State(Player)!;
        public PlayerChoiceContext Context {get;}=new ThrowingPlayerChoiceContext();
        public Fixture()
        {
            Reset();var run=RunState.CreateForTest([Player],seed:"ARTU_V03_VERIFY");Combat=new CombatState(runState:run);Combat.AddPlayer(Player);
            Enemy=Combat.CreateCreature(ModelDb.Monster<BigDummy>().ToMutable(),CombatSide.Enemy,null);Combat.AddCreature(Enemy);CombatManager.Instance.SetUpCombat(Combat);
            var turn=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;AccessTools.Property(turn.GetType(),"IsInProgress").SetValue(turn,true);
            Await(CreatureCmd.SetMaxHp(Player.Creature,2000));Await(CreatureCmd.SetCurrentHp(Player.Creature,1500));SultanUsage.Reset(Player);PaymentLedger.Reset(Player);
        }
        public void Await(Task task)=>task.GetAwaiter().GetResult();
        public T Card<T>()where T:CardModel=>Combat.CreateCard<T>(Player);
        public void Apply<T>(int amount)where T:PowerModel=>Await(PowerCmd.Apply<T>(Context,Player.Creature,amount,Player.Creature,null));
        public void Gain(int kind,int amount)=>Await(ArmyPower.Gain(Context,Player,kind,amount));
        public void NextTurn(){Player.PlayerCombatState!.IncrementTurnNumber();Combat.RoundNumber++;}
        public CardPlay MakePlay(CardModel card)=>new(){Card=card,Player=Player,Target=Enemy,ResultPile=PileType.Discard,Resources=new(){EnergySpent=0,EnergyValue=1,StarsSpent=0,StarValue=0},IsAutoPlay=false,PlayIndex=0,PlayCount=1};
        public CardPlay Play<T>()where T:ArtuCard=>Play(Card<T>());
        public CardPlay Play(ArtuCard card){var play=MakePlay(card);Await(Hook.BeforeCardPlayed(Combat,play));Await(card.ExecuteCopiedEffect(Context,play));Await(Hook.AfterCardPlayed(Combat,Context,play));return play;}
        public void DrawHook(CardModel card)=>Await(Hook.AfterCardDrawn(Combat,Context,card,true));
        public void FillDraw(int count){for(int i=0;i<count;i++)Await(CardPileCmd.Add(Card<DispatchTroops>(),PileType.Draw));}
        public void AddDeck<T>()where T:CardModel=>Await(Util.AddToDeck(Player,ModelDb.Card<T>()));
        public void Dispose()=>Reset();
    }
    sealed class FirstSelector:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int minSelect,int maxSelect)=>Task.FromResult(options.Take(minSelect));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
public sealed class UseIdentityProbe():ArtuPower("测试出牌身份","隔离验证用，不加入牌池。")
{
    public int CurseUses {get;private set;}
    public override Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play){if(play.Player==Owner.Player&&play.Card.Type==CardType.Curse)CurseUses++;return Task.CompletedTask;}
}
