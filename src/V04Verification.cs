using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
namespace ArtuMod;
public static class V04Verification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var checks=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("v04 "+name);}
        using var selector=CardSelectCmd.UseSelector(new Selector());
        try
        {
            var expected=new Type[]{typeof(KnowYourEnemy),typeof(RobPeterToPayPaul),typeof(CriminalDefense),typeof(SecretMessenger),typeof(MarchingDrum),typeof(CollectiveWisdom),typeof(ScribesManuscript),typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent),typeof(ArmyRegularization),typeof(RevolutionaryAcademy),typeof(AllByKingsGrace),typeof(SpiralWaltz),typeof(OpenThought),typeof(LifeScepter),typeof(ProfitFromCrime),typeof(BurnTheBoats),typeof(UndergroundPress),typeof(MoneyOpensDoors),typeof(RoyalConscription)};
            using(var f=new Fixture())
            {
                var normal=V04.RewardCards(f.Player).ToArray();Check("ordinary rewards register 22/35/25=82",normal.Length==82&&normal.Count(c=>c.Rarity==CardRarity.Common)==22&&normal.Count(c=>c.Rarity==CardRarity.Uncommon)==35&&normal.Count(c=>c.Rarity==CardRarity.Rare)==25);
                foreach(var type in expected)
                {var card=normal.Single(c=>c.GetType()==type);Check($"{card.Title} is a real registered reward",card.CanBeGeneratedInCombat&&card.CanBeGeneratedByModifiers&&card.Localization!.All(t=>!t.Item2.Contains("未确定")));
                var mutable=f.Combat.CreateCard(card,f.Player);Check($"{type.Name} upgrade rule",mutable.IsUpgradable==(!card.Thought&&!card.Reading));if(mutable.IsUpgradable){CardCmd.Upgrade(mutable);Check($"{type.Name} upgrade applies",mutable.IsUpgraded);}}
                Check("final/story cards excluded from reward and generic generation",normal.All(c=>c is not FinalThought and not NoteInBook and not RevolutionPlan)&&ModelDb.All.OfType<FinalThought>().All(c=>!c.CanBeGeneratedInCombat&&!c.CanBeGeneratedByModifiers&&c.Rarity==CardRarity.Ancient));
                var note=f.Card<NoteInBook>();Check("note is Power, dual tags, Retain, no Exhaust",note.Type==CardType.Power&&note.Reading&&note.Thought&&note.Keywords.Contains(CardKeyword.Retain)&&!note.Keywords.Contains(CardKeyword.Exhaust)&&!note.IsUpgradable);
            }
            using(var f=new Fixture())
            {
                int hp=f.Enemy.CurrentHp;f.FillDraw(4);f.Play<KnowYourEnemy>();Check("know enemy base deals 12 and draws at least one",hp-f.Enemy.CurrentHp==12&&f.Player.PlayerCombatState!.Hand.Cards.Count>=1);
                f.ClearHand();f.AddHand<Quickfoot>();int energy=f.Player.PlayerCombatState!.Energy;f.Play<RobPeterToPayPaul>();Check("rob peter discards another card for 2 energy",f.Player.PlayerCombatState.Energy==energy+2&&f.Player.PlayerCombatState.DiscardPile.Cards.Any(c=>c is Quickfoot));
                f.ClearHand();energy=f.Player.PlayerCombatState.Energy;f.Play<RobPeterToPayPaul>();Check("forced empty rob peter has no benefit",f.Player.PlayerCombatState.Energy==energy);
                f.Apply<CrimePower>(1);f.FillDraw(4);int before=f.Player.PlayerCombatState.Hand.Cards.Count;f.Play<CriminalDefense>();Check("criminal defense draws two at crime one",f.Player.PlayerCombatState.Hand.Cards.Count==before+2);
                var s=f.Card<StoneSlaughter>();f.Await(CardPileCmd.Add(s,PileType.Discard));f.Play<SecretMessenger>();Check("messenger retrieves Sultan from discard",s.Pile?.Type==PileType.Hand);
            }
            using(var f=new Fixture())
            {
                f.Gain(1,2);int hp=f.Enemy.CurrentHp;f.Play<MarchingDrum>();Check("drum resolves old layers before adding new",hp-f.Enemy.CurrentHp==4&&f.Player.Creature.Block==2&&f.Player.Creature.GetPowerAmount<SoldierPower>()==3);
                f.FillDraw(6);f.Play<PureThought>();int energy=f.Player.PlayerCombatState!.Energy;f.Play<CollectiveWisdom>();Check("wisdom prior thought gives energy",f.Player.PlayerCombatState.Energy==energy+1&&!ModelDb.Card<CollectiveWisdom>().Thought);
                f.ClearHand();int draw=f.Player.PlayerCombatState.DrawPile.Cards.Count;f.Play<ScribesManuscript>();Check("manuscript views actual up-to-five, optional selector discards remainder",f.Player.PlayerCombatState.DrawPile.Cards.Count==Math.Max(0,draw-5)&&f.Player.PlayerCombatState.Hand.Cards.Count==0);
            }
            using(var f=new Fixture())
            {
                f.Gain(0,7);f.Apply<LivingOffWarPower>(1);f.Play<ArmyRegularization>();Check("regularization converts every levy to selected soldiers",f.Player.Creature.GetPowerAmount<LevyPower>()==0&&f.Player.Creature.GetPowerAmount<SoldierPower>()==7&&f.State.LeviesConverted==7&&f.Player.Creature.Block==7);
                Check("conversion does not activate living off war",ArmyPower.All(f.Player).Sum(a=>a.Amount)==7);
                f.Gain(0,8);f.Play<RevolutionaryAcademy>();f.Await(f.Player.Creature.GetPower<AcademyPower>()!.AfterPlayerTurnStart(f.Context,f.Player));Check("academy converts at most four into fixed selected kind",f.Player.Creature.GetPowerAmount<LevyPower>()==4&&f.Player.Creature.GetPowerAmount<SoldierPower>()==11&&f.State.LeviesConverted==11);
                f.Play<TrainArmy>();f.Await(f.Player.Creature.GetPower<TrainingPower>()!.AfterPlayerTurnStart(f.Context,f.Player));Check("existing training contributes true conversions",f.State.LeviesConverted>11);
            }
            using(var f=new Fixture())
            {
                f.Gain(1,2);f.Play<OpenThought>();int block=f.Player.Creature.Block;f.Play<DispatchTroops>();Check("open thought blocks direct card block",f.Player.Creature.Block==block&&f.Player.Creature.GetPowerAmount<StrengthPower>()==8);
                f.Await(f.Player.Creature.GetPower<SoldierPower>()!.Trigger(f.Context));Check("open thought allows army block",f.Player.Creature.Block==block+2);
            }
            using(var f=new Fixture())
            {
                f.Play<SpiralWaltz>();f.Play<ShareKingsBurden>();Check("waltz repeats next power effect without duplicate use event",f.Player.Creature.GetPowerAmount<KingsBurdenPower>()==6&&SultanUsage.NonSultanThisTurn(f.Player)==2&&!f.Player.Creature.HasPower<SpiralWaltzPower>());
                f.Play<ProfitFromCrime>();f.FillDraw(6);int energy=f.Player.PlayerCombatState!.Energy;f.Apply<CrimePower>(3);f.Apply<CrimePower>(2);Check("crime profit once for bulk gain and saved crime agrees",f.Player.PlayerCombatState.Hand.Cards.Count==2&&f.Player.PlayerCombatState.Energy==energy+1&&f.State.Crime==5);
                f.Await(V04.RemoveCrime(f.Context,f.Player,2));Check("permanent removal updates the same saved crime",f.State.Crime==3&&f.Player.Creature.GetPowerAmount<CrimePower>()==3);
            }
            using(var f=new Fixture())
            {
                f.Gain(0,1);f.Gain(1,1);f.Gain(2,1);f.Gain(3,1);int hp=f.Enemy.CurrentHp;f.Apply<LivingOffWarPower>(1);f.Play<BurnTheBoats>();Check("burn boats 4 layers produce 24 damage and 8 block",hp-f.Enemy.CurrentHp==24&&f.Player.Creature.Block==8);Check("burn boats consumption batch replenishes four once",ArmyPower.All(f.Player).Sum(a=>a.Amount)==4);
                f.Play<UndergroundPress>();foreach(var existing in f.Player.PlayerCombatState!.DrawPile.Cards.ToArray())f.Await(CardPileCmd.Add(existing,PileType.Discard));var item=f.Card<Quickfoot>();f.Await(CardPileCmd.Add(item,PileType.Draw));f.Play<WeakOverStrong>();Check("underground press uses legal non-Sultan draw selection",item.Pile?.Type==PileType.Hand);
            }
            using(var f=new Fixture())
            {
                var item=f.Card<Ironhead>();f.Await(CardPileCmd.Add(item,PileType.Draw));int gold=f.Player.Gold;f.Play<MoneyOpensDoors>();Check("money opens doors costs 20, free combat and retain",f.Player.Gold==gold-20&&item.Pile?.Type==PileType.Hand&&item.EnergyCost.GetWithModifiers(CostModifiers.All)==0&&item.Keywords.Contains(CardKeyword.Retain));f.NextTurn();item.EnergyCost.EndOfTurnCleanup();Check("money free persists next turn",item.EnergyCost.GetWithModifiers(CostModifiers.All)==0);
                f.Play<RoyalConscription>();f.Play<StoneSlaughter>();f.Play<StoneLuxury>();Check("royal conscription once despite two Sultan uses",f.Player.Creature.GetPowerAmount<SoldierPower>()==3);
            }
            foreach(int stacks in new[]{3,6,7,8,11})using(var f=new Fixture())
            {
                f.Apply<CrimePower>(stacks);int hp=f.Player.Creature.CurrentHp;var crime=f.Player.Creature.GetPower<CrimePower>()!;
                Check($"crime {stacks} opening draw -1 and natural energy condition",crime.ModifyHandDraw(f.Player,5)==4&&crime.ModifyEnergyGain(f.Player,3)==(stacks>=4?2:3));
                f.Await(crime.AfterPlayerTurnStart(f.Context,f.Player));Check($"crime {stacks} never start-turn HP loss",f.Player.Creature.CurrentHp==hp);
                var first=f.Card<Quickfoot>();f.Await(CardPileCmd.Add(first,PileType.Hand));Check($"crime {stacks} first non-Sultan surcharge",first.EnergyCost.GetWithModifiers(CostModifiers.All)==first.EnergyCost.Canonical+(stacks>=5?1:0));
                int before=f.Player.PlayerCombatState!.DrawPile.Cards.Count(c=>c is Dazed);f.Play(first);int after=f.Player.PlayerCombatState.DrawPile.Cards.Count(c=>c is Dazed);Check($"crime {stacks} first creates exact dazed count",after-before==(stacks>=6?1:0));
                f.Play<Quickfoot>();Check($"crime {stacks} second replaces sixth-tier dazed rather than doubling",f.Player.PlayerCombatState.DrawPile.Cards.Count(c=>c is Dazed)-after==(stacks>=8?1:0));
                Check($"crime {stacks} dazed uses native Ethereal unplayable status",f.Player.PlayerCombatState.DrawPile.Cards.OfType<Dazed>().All(c=>c.Keywords.Contains(CardKeyword.Ethereal)&&c.Keywords.Contains(CardKeyword.Unplayable)));
                f.Play<Quickfoot>();f.AddHand<Ironhead>();f.AddHand<Ironhead>();int hand=f.Player.PlayerCombatState.Hand.Cards.Count;f.Play<Quickfoot>();Check($"crime {stacks} fourth discards at tier seven",hand-f.Player.PlayerCombatState.Hand.Cards.Count==(stacks>=7?1:0));
            }
            using(var f=new Fixture())
            {
                var original=f.AddDeck<GoldSlaughter>();original.BattlesRemaining=1;original.TurnsSinceUse=2;var card=(SultanCard)f.Combat.CloneCard(original);card.DeckVersion=original;f.Await(CardPileCmd.Add(card,PileType.Hand));
                f.Play<AllByKingsGrace>();int hp=f.Player.Creature.CurrentHp;f.Play(card);Check("grace no HP cost and no fulfilled-state reset",hp==f.Player.Creature.CurrentHp&&!original.UsedThisBattle&&original.TurnsSinceUse==2&&original.BattlesRemaining==1);
                f.Await(f.State.TickGold(f.Context));f.NextTurn();f.Await(f.State.TickGold(f.Context));Check("grace freezes gold across successive own turns",original.TurnsSinceUse==2&&f.State.Anger==0);
                Check("grace one extra native replay and zero energy",MegaCrit.Sts2.Core.Hooks.Hook.ModifyCardPlayCount(f.Combat,card,1,f.Enemy,out _)==2&&card.EnergyCost.GetWithModifiers(CostModifiers.All)==0);
                f.Await(V04.Victory(f.Player,f.Context));Check("normal grace battle penalty expires instance once",f.State.Anger==1&&original.BattlesRemaining==1&&original.TurnsSinceUse==0);
                V04.Reset(f.Player);f.NextTurn();f.Await(f.State.TickGold(f.Context));Check("grace gold expiry restarts original three-turn clock",f.State.Anger==1&&original.TurnsSinceUse==1);
            }
            using(var f=new Fixture())
            {
                var original=f.AddDeck<GoldLuxury>();original.TurnsSinceUse=2;var grace=f.Card<AllByKingsGrace>();CardCmd.Upgrade(grace);f.Play(grace);var card=(SultanCard)f.Combat.CloneCard(original);card.DeckVersion=original;f.Play(card);f.Await(V04.Victory(f.Player,f.Context));Check("upgraded grace no postbattle deadline penalty",f.State.Anger==0&&original.TurnsSinceUse==2&&original.BattlesRemaining==1);
            }
            using(var f=new Fixture())
            {
                f.Play<AncientFaith>();f.Apply<ReplaySultanPower>(1);var c=f.Card<StoneSlaughter>();V04Replay.Begin(c,false);int count=Hook.ModifyCardPlayCount(f.Combat,c,1,f.Enemy,out _);Check("faith appends one to existing replay series",count==3&&V04Replay.SeriesFor(c).FreeFrom==2);int hp=f.Player.Creature.CurrentHp;
                for(int i=0;i<count;i++)f.Play(c,i,count);Check("faith costs two ordinary slots plus once 2HP, appended free",hp-f.Player.Creature.CurrentHp==4);
                var again=f.Card<StoneSlaughter>();V04Replay.Begin(again,false);Check("faith once per own turn",Hook.ModifyCardPlayCount(f.Combat,again,1,f.Enemy,out _)==1);
            }
            using(var f=new Fixture())
            {
                f.Play<NewWorld>();f.FillDraw(5);var c=f.Card<StoneSlaughter>();V04Replay.Begin(c,false);int n=Hook.ModifyCardPlayCount(f.Combat,c,1,f.Enemy,out _);int hp=f.Player.Creature.CurrentHp,energy=f.Player.PlayerCombatState!.Energy;
                for(int i=0;i<n;i++)f.Play(c,i,n);Check("new world adds two free replays, once 3HP and tail draw/energy",n==3&&hp-f.Player.Creature.CurrentHp==4&&f.Player.PlayerCombatState.Hand.Cards.Count==2&&f.Player.PlayerCombatState.Energy==energy+1);
            }
            using(var f=new Fixture())
            {
                f.Play<LifeScepter>();using(var chooseReplacement=CardSelectCmd.PushSelector(new Selector(true)))
                {var scepter=f.Player.Creature.GetPower<LifeScepterPower>()!;Check("optional scepter accepted once",scepter.TryReplace(f.Context).GetAwaiter().GetResult()&&f.Enemy.GetPowerAmount<RegenPower>()==7&&!scepter.Available);f.NextTurn();Check("scepter refreshes only next own turn",scepter.Available);}
            }
            using(var f=new Fixture())
            {
                f.Play<PerfectWorld>();var card=f.Card<Ironhead>();f.Await(CardPileCmd.Add(card,PileType.Hand));Check("perfect world non-Sultan free",card.EnergyCost.GetWithModifiers(CostModifiers.All)==0);var power=f.Player.Creature.GetPower<PerfectWorldPower>()!;var loc=power.ModifyCardPlayResultLocation(card,false,new(){EnergySpent=0,EnergyValue=0,StarsSpent=0,StarValue=0},new(f.Player,PileType.Discard,CardPilePosition.Bottom));Check("perfect world uses combat exhaust pile",loc.pileType==PileType.Exhaust);int hp=f.Enemy.CurrentHp;f.Await(power.BeforeCardPlayed(f.MakePlay(card)));f.Await(CardCmd.Exhaust(f.Context,card));Check("perfect world one exhaust creates six AOE damage",hp-f.Enemy.CurrentHp==6&&card.Pile?.Type==PileType.Exhaust);
            }
            using(var f=new Fixture())
            {
                f.AddDeck<NoteInBook>();f.Await(Thoughts.RemoveSame(f.Player,ModelDb.Card<StoneSlaughter>()));f.Await(Thoughts.RemoveSame(f.Player,ModelDb.Card<StoneConquest>()));f.Await(Thoughts.RemoveSame(f.Player,ModelDb.Card<StoneLuxury>()));f.Await(Thoughts.RemoveSame(f.Player,ModelDb.Card<StoneLust>()));
                f.Gain(0,10);f.Gain(1,5);f.Gain(2,5);f.Gain(3,5);f.State.ReadingUses=10;f.State.ReadingNames="a;b;c";f.State.LeviesConverted=15;
                f.Play<PureThought>();f.Play<UncleanThought>();f.Play<FearOfSultan>();f.Await(RevolutionAgenda.Refresh(f.Player,f.Context));
                Check("all seven stages of six agendas accumulate to 127",f.State.AgendaFlags==127&&f.State.ConceptGranted&&f.Player.Deck.Cards.Count(c=>c is RevolutionConcept)==1);
                f.Await(RevolutionAgenda.Refresh(f.Player,f.Context));Check("concept awarded only once",f.Player.Deck.Cards.Count(c=>c is RevolutionConcept)==1);
                f.Apply<CrimePower>(2);Check("completed clean task not revoked",(f.State.AgendaFlags&4)!=0);
                f.Play<NoteInBook>();int troops=f.Player.Creature.GetPowerAmount<LevyPower>();f.Play<NoteInBook>();Check("note immediate task rewards don't double on repeated copies",troops==20&&f.Player.Creature.GetPowerAmount<LevyPower>()==20);
                f.Play<RevolutionConcept>();f.Play<RegicidePlan>();Check("same-combat actual concept + regicide generates plan in hand",f.State.PlanGranted&&f.Player.PlayerCombatState!.Hand.Cards.Count(c=>c is RevolutionPlan)==1);f.Play<RevolutionConcept>();Check("plan issued once without deleting prerequisites",f.Player.PlayerCombatState!.Hand.Cards.Count(c=>c is RevolutionPlan)==1&&f.Player.Deck.Cards.Any(c=>c is RevolutionConcept));
                var saved=(WitchCardBox)RelicModel.FromSerializable(f.State.ToSerializable());Check("permanent progress serializes counts and awards",saved.ReadingUses==f.State.ReadingUses&&saved.LeviesConverted==15&&saved.AgendaFlags==127&&saved.PlanGranted);
            }
            foreach(var path in new[]{0,1,2})using(var f=new Fixture())
            {
                for(int battle=0;battle<3;battle++)
                {
                    foreach(var power in f.Player.Creature.Powers.ToArray())f.Await(PowerCmd.Remove(power));V04.Reset(f.Player);SultanUsage.Reset(f.Player);f.State.Pure=f.State.Unclean=f.State.Fear=f.State.Submission=0;
                    if(path==0)f.Play<AtonementPrayer>();
                    if(path==1){f.Play<AncientFaith>();f.Play<StoneSlaughter>();f.Play<StoneLuxury>();}
                    if(path==2){V04.Data(f.Player).RebellionTriggers=0;f.Play<RebellionIntent>();f.Play<Quickfoot>();f.NextTurn();f.Play<Quickfoot>();}
                    f.Await(V04.Victory(f.Player,f.Context));
                }
                Check($"third route {path} evolves after three qualifying combats",path==0?f.State.Devotion==3&&f.Player.Deck.Cards.Count(c=>c is PerfectWorld)==1:path==1?f.State.Fanaticism==3&&f.Player.Deck.Cards.Count(c=>c is NewWorld)==1:f.State.Treason==3&&f.Player.Deck.Cards.Count(c=>c is RegicidePlan)==1);
            }
            using(var f=new Fixture())
            {
                var a=RelicChoice.Create(f.State);var b=RelicChoice.Create(f.State);Check("legacy relic choice uses separate mutable models",a.IsMutable&&b.IsMutable&&!ReferenceEquals(a.DynamicVars,b.DynamicVars)&&a.Relic==f.State);Check("legacy relic selection command still returns original relic",RelicSelection.Choose(f.Player,[f.State]).GetAwaiter().GetResult()==f.State);
                var e=ModelDb.Event<ClearStreamsMeeting>();var n=ModelDb.Event<NurseryClassroom>();var run=(RunState)f.Player.RunState;
                run.CurrentActIndex=1;Check("revolution events exclude players without note",!e.IsAllowed(run)&&!n.IsAllowed(run));f.AddDeck<NoteInBook>();run.CurrentActIndex=0;Check("revolution events never act one",!e.IsAllowed(run)&&!n.IsAllowed(run));run.CurrentActIndex=1;Check("revolution events allowed act two with note",e.IsAllowed(run)&&n.IsAllowed(run));run.CurrentActIndex=2;Check("revolution events allowed act three",e.IsAllowed(run)&&n.IsAllowed(run));f.State.ClearStreamsSeen=f.State.NurserySeen=true;Check("revolution events once per run",!e.IsAllowed(run)&&!n.IsAllowed(run));
                var choices=RevolutionRewards.Pick(f.Player,V04.RewardCards(f.Player).Where(c=>c.Reading));Check("reading reward legal unique ordinary names only",choices.Length==3&&choices.DistinctBy(c=>c.Id).Count()==3&&choices.All(c=>c is ArtuCard {Reading:true}&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare));
            }
            return checks;
        }
        finally{Fixture.ResetCombat();TestMode.IsOn=false;}
    }
    public sealed class Fixture:IDisposable
    {
        public Player Player {get;}=Player.CreateForNewRun<Artu>(UnlockState.all,1);
        public CombatState Combat {get;}public Creature Enemy {get;}
        public WitchCardBox State=>Util.State(Player)!;
        public PlayerChoiceContext Context {get;}=new ThrowingPlayerChoiceContext();
        public Fixture(){ResetCombat();var run=RunState.CreateForTest([Player],seed:"ARTU_V04_VERIFY");Combat=new CombatState(runState:run);Combat.AddPlayer(Player);Enemy=Combat.CreateCreature(ModelDb.Monster<BigDummy>().ToMutable(),CombatSide.Enemy,null);Combat.AddCreature(Enemy);CombatManager.Instance.SetUpCombat(Combat);var turn=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;AccessTools.Property(turn.GetType(),"IsInProgress").SetValue(turn,true);Await(CreatureCmd.SetMaxHp(Player.Creature,2000));Await(CreatureCmd.SetCurrentHp(Player.Creature,1500));V04.Reset(Player);SultanUsage.Reset(Player);PaymentLedger.Reset(Player);foreach(var c in Player.PlayerCombatState!.AllCards.ToArray())Await(CardPileCmd.RemoveFromCombat(c));}
        public static void ResetCombat(){var field=AccessTools.Field(typeof(CombatManager),"_turnState");if(field.GetValue(CombatManager.Instance) is {} turn)AccessTools.Method(turn.GetType(),"Cancel").Invoke(turn,null);field.SetValue(CombatManager.Instance,null);CombatManager.Instance.History.Clear();}
        public void Await(Task t)=>t.GetAwaiter().GetResult();public T Card<T>()where T:CardModel=>Combat.CreateCard<T>(Player);
        public void Apply<T>(int amount)where T:PowerModel=>Await(PowerCmd.Apply<T>(Context,Player.Creature,amount,Player.Creature,null));
        public void Gain(int kind,int amount)=>Await(ArmyPower.Gain(Context,Player,kind,amount));public void NextTurn(){Player.PlayerCombatState!.IncrementTurnNumber();Combat.RoundNumber++;}
        public CardPlay MakePlay(CardModel card,int index=0,int count=1)=>new(){Card=card,Player=Player,Target=card.TargetType==TargetType.AnyEnemy?Enemy:null,ResultPile=PileType.Discard,Resources=new(){EnergySpent=0,EnergyValue=1,StarsSpent=0,StarValue=0},IsAutoPlay=false,PlayIndex=index,PlayCount=count};
        public CardPlay Play<T>()where T:ArtuCard=>Play(Card<T>());
        public CardPlay Play(ArtuCard card,int index=0,int count=1){var play=MakePlay(card,index,count);Await(Hook.BeforeCardPlayed(Combat,play));Await(card.ExecuteCopiedEffect(Context,play));Await(Hook.AfterCardPlayed(Combat,Context,play));return play;}
        public T AddDeck<T>()where T:CardModel{var c=Player.RunState.CreateCard<T>(Player);Await(CardPileCmd.Add(c,PileType.Deck));return c;}
        public T AddHand<T>()where T:CardModel{var c=Card<T>();Await(CardPileCmd.Add(c,PileType.Hand));return c;}
        public void FillDraw(int count){for(int i=0;i<count;i++)Await(CardPileCmd.Add(Card<DispatchTroops>(),PileType.Draw));}
        public void ClearHand(){foreach(var c in Player.PlayerCombatState!.Hand.Cards.ToArray())Await(CardPileCmd.Add(c,PileType.Discard));}
        public void Dispose()=>ResetCombat();
    }
    sealed class Selector(bool replace=false):ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options,int min,int max)=>Task.FromResult(options.OrderByDescending(c=>replace&&c is ScepterReplaceOption).Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
