using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Models.Monsters;
namespace ArtuMod;
public static class CombatVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;
        var passed=new List<string>();
        var p=Player.CreateForNewRun<Artu>(UnlockState.all,1);
        var run=RunState.CreateForTest([p],seed:"ARTUVERIFY");
        var combat=new CombatState(runState:run);
        combat.AddPlayer(p);
        var enemy=combat.CreateCreature(ModelDb.Monster<BigDummy>().ToMutable(),CombatSide.Enemy,null);
        combat.AddCreature(enemy);
        CombatManager.Instance.SetUpCombat(combat);
        var turnState=AccessTools.Field(typeof(CombatManager),"_turnState").GetValue(CombatManager.Instance)!;
        AccessTools.Property(turnState.GetType(),"IsInProgress").SetValue(turnState,true);
        var ctx=new ThrowingPlayerChoiceContext();
        var state=Util.State(p)!;
        void Check(string name,bool ok){if(!ok)throw new Exception(name);passed.Add(name);}
        void Apply<T>(int count,Creature? target=null)where T:PowerModel=>PowerCmd.Apply<T>(ctx,target??p.Creature,count,p.Creature,null).GetAwaiter().GetResult();
        void Play<T>()where T:CardModel
        {
            var card=combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId<T>()),p);
            var play=new CardPlay{Card=card,Player=p,Target=enemy,ResultPile=PileType.Discard,Resources=new ResourceInfo{EnergySpent=0,EnergyValue=1,StarsSpent=0,StarValue=0},IsAutoPlay=false,PlayIndex=0,PlayCount=1};
            ((Task)AccessTools.Method(typeof(T),"OnPlay").Invoke(card,[ctx,play])!).GetAwaiter().GetResult();
        }
        try
        {
            int hp=enemy.CurrentHp;
            Play<StoneLuxury>();Check("Stone luxury spends 5 gold and creates 10 block",p.Gold==94&&p.Creature.Block==10);
            Play<StoneConquest>();Check("Stone conquest pays all block and applies 2 weak/vulnerable",p.Creature.Block==0&&enemy.GetPowerAmount<WeakPower>()==2&&enemy.GetPowerAmount<VulnerablePower>()==2);
            PowerCmd.Remove<WeakPower>(enemy).GetAwaiter().GetResult();PowerCmd.Remove<VulnerablePower>(enemy).GetAwaiter().GetResult();
            Play<StoneSlaughter>();Check("Stone slaughter loses 1 player HP and deals 11",p.Creature.CurrentHp==69&&enemy.CurrentHp==hp-11);
            Apply<PassPower>(1);Play<StoneSlaughter>();Check("Royal pass skips the HP cost",p.Creature.CurrentHp==69);
            Apply<DoubleCostPower>(1);Play<BronzeSlaughter>();Check("DoubleCost pays both HP costs once per effect",p.Creature.CurrentHp==63);
            Apply<SoldierPower>(3);var soldier=p.Creature.GetPower<SoldierPower>()!;hp=enemy.CurrentHp;soldier.Trigger(ctx).GetAwaiter().GetResult();
            Check("3 soldiers deal 6 damage and give 3 block",enemy.CurrentHp==hp-6&&p.Creature.Block==3);
            Apply<CrimePower>(2);hp=enemy.CurrentHp;soldier.Trigger(ctx).GetAwaiter().GetResult();
            Check("Army damage bypasses card-only crime penalty",enemy.CurrentHp==hp-6);
            Apply<VeteranPower>(9);var veterans=p.Creature.GetPower<VeteranPower>()!;hp=enemy.CurrentHp;veterans.Trigger(ctx).GetAwaiter().GetResult();
            Check("9 veterans deal fixed 18 and grant unused vigor plus debuffs",enemy.CurrentHp==hp-18&&p.Creature.GetPowerAmount<VigorPower>()==5&&enemy.GetPowerAmount<WeakPower>()==1&&enemy.GetPowerAmount<VulnerablePower>()==1);
            state.Mockery=5;state.EnforceHp().GetAwaiter().GetResult();Check("Mockery clamps existing life to 50 percent",p.Creature.CurrentHp==35);
            CreatureCmd.Heal(p.Creature,100,false).GetAwaiter().GetResult();Check("Healing cannot exceed the mockery ceiling",p.Creature.CurrentHp==35);
            state.StatsLocked=true;Apply<StrengthPower>(5);Check("Gold conquest expiry denies new strength",p.Creature.GetPowerAmount<StrengthPower>()==0);
            state.GoldLocked=true;int gold=p.Gold;PlayerCmd.GainGold(50,p).GetAwaiter().GetResult();Check("Gold luxury expiry denies income",p.Gold==gold);
            Play<PureThought>();Play<UncleanThought>();Play<FearOfSultan>();Play<BowToSultan>();
            Check("Thought cards maintain separate visible stacks",state.Pure==1&&state.Unclean==1&&state.Fear==1&&state.Submission==1&&p.Creature.GetPowerAmount<PureThoughtPower>()==1&&p.Creature.GetPowerAmount<SubmissionPower>()==1);
            var a=(SultanCard)p.Deck.Cards.OfType<StoneSlaughter>().First();var b=p.Deck.Cards.OfType<StoneSlaughter>().Last();
            a.BattlesRemaining=2;b.BattlesRemaining=1;Check("Duplicate deck Sultan deadlines remain independent",a.BattlesRemaining!=b.BattlesRemaining);
            var goldCard=run.CreateCard<GoldLuxury>(p);CardPileCmd.Add(goldCard,PileType.Deck).GetAwaiter().GetResult();
            state.TickGold(ctx).GetAwaiter().GetResult();state.TickGold(ctx).GetAwaiter().GetResult();
            Check("Gold timer ticks once per own turn",goldCard.TurnsSinceUse==1);
            AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(p.PlayerCombatState,2);
            state.TickGold(ctx).GetAwaiter().GetResult();
            Check("Gold timer carries accumulated own turns",goldCard.TurnsSinceUse==2);
            goldCard.UsedThisTurn=true;AccessTools.Property(typeof(PlayerCombatState),"TurnNumber").SetValue(p.PlayerCombatState,3);
            state.TickGold(ctx).GetAwaiter().GetResult();Check("Gold successful use resets three-turn counter",goldCard.TurnsSinceUse==0);
            var blocked=(SultanCard)combat.CreateCard(ModelDb.Card<StoneConquest>(),p);
            CreatureCmd.LoseBlock(ctx,p.Creature,p.Creature.Block,p.Creature).GetAwaiter().GetResult();
            Check("Zero block cannot pay stone conquest",!blocked.CanPayAny());
            var tyrant=ModelDb.Relic<TyrantsAccomplice>().ToMutable();p.AddRelicInternal(tyrant);
            CardPileCmd.Add(blocked,PileType.Hand).GetAwaiter().GetResult();
            var ordinary=combat.CreateCard<Quickfoot>(p);
            Check("Tyranny blocks normal cards while Sultan remains",!state.ShouldPlay(ordinary,AutoPlayType.Default));
            Check("Tyranny prevents voluntarily ending turn",!ForcedEndAllowed(p));
            ((Task)AccessTools.Method(typeof(SultanCard),"OnPlay").Invoke(blocked,[ctx,new CardPlay{Card=blocked,Player=p,Target=enemy,ResultPile=PileType.Discard,Resources=new ResourceInfo{EnergySpent=0,EnergyValue=0,StarsSpent=0,StarValue=0},IsAutoPlay=false,PlayIndex=0,PlayCount=1}])!).GetAwaiter().GetResult();
            Check("Unpayable Sultan cost is replaced by one mockery",state.Mockery==6);
            state.Mockery=10;state.EnforceHp().GetAwaiter().GetResult();
            Check("Mockery ten executes immediately",p.Creature.IsDead);
            CreatureCmd.SetCurrentHp(p.Creature,1).GetAwaiter().GetResult();
            Check("Revival cannot bypass mockery ten",p.Creature.IsDead);
            return passed;
        }
        finally {TestMode.IsOn=false;}
    }
    static bool ForcedEndAllowed(Player p)=>(bool)AccessTools.Method(typeof(ForcedOrdersEndTurn),"Prefix").Invoke(null,[p,true])!;
}
