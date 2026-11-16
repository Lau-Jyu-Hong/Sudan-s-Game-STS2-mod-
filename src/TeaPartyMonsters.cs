using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace ArtuMod;

// This encounter is event-only: registering it must not add it to ordinary map encounters.
public sealed class TeaPartyEncounter : CustomEncounterModel, ILocalizationProvider
{
    public TeaPartyEncounter() : base(RoomType.Monster, autoAdd:false)
        => CustomContentDictionary.RegisterType(GetType());
    public override bool IsValidForAct(ActModel act) => false;
    public override bool ShouldGiveRewards => false;
    public override string CustomScenePath => "res://scenes/encounters/knights_elite.tscn";
    public override IEnumerable<MonsterModel> AllPossibleMonsters =>
        [ModelDb.Monster<TeaCrownKnight>(), ModelDb.Monster<TeaMercenaryCaptain>(), ModelDb.Monster<TeaWarlord>()];
    protected override IReadOnlyList<(MonsterModel,string?)> GenerateMonsters() =>
        [(ModelDb.Monster<TeaCrownKnight>().ToMutable(),"first"),
         (ModelDb.Monster<TeaMercenaryCaptain>().ToMutable(),"second"),
         (ModelDb.Monster<TeaWarlord>().ToMutable(),"third")];
    public override float GetCameraScaling() => 0.87f;
    public override Vector2 GetCameraOffset() => Vector2.Down*50;
    public List<(string,string)> Localization => [("title","圆桌茶会：掀桌")];
}

public abstract class TeaMonster : CustomMonsterModel, ILocalizationProvider
{
    protected abstract MonsterModel VisualTemplate { get; }
    protected abstract string AttackAnimation { get; }
    public abstract List<(string,string)> Localization { get; }
    public override DamageSfxType TakeDamageSfxType => DamageSfxType.Armor;
    public override string CustomDeathSfx => VisualTemplate.DeathSfx;
    public override CreatureAnimator GenerateAnimator(MegaSprite controller) => VisualTemplate.GenerateAnimator(controller);
    protected Task Cast() => CreatureCmd.TriggerAnim(Creature,"Cast",0.25f);
    protected Task Attack(int amount) => DamageCmd.Attack(amount).FromMonster(this)
        .WithAttackerAnim(AttackAnimation,0.25f).WithHitFx("vfx/vfx_attack_slash").Execute(null);
}

public sealed class TeaCrownKnight : TeaMonster
{
    public override int MinInitialHp => 140;
    public override int MaxInitialHp => 140;
    protected override MonsterModel VisualTemplate => ModelDb.Monster<FlailKnight>();
    protected override string AttackAnimation => "RamAttack";
    public override string CustomVisualPath => "res://scenes/creature_visuals/flail_knight.tscn";
    public IEnumerable<Creature> LivingAllies => CombatState.Enemies.Where(c=>c!=Creature&&!c.IsDead);
    public override List<(string,string)> Localization => new MonsterLoc("王冠骑士",
        [("ROYAL_STRIKE","王室剑击"),("HEAVY_SLASH","重斩"),("ROYAL_GUARD","王权庇护"),("LAST_STAND","孤军重斩")]);
    public override Task AfterAddedToRoom() => PowerCmd.Apply<RoyalProtectionPower>(new ThrowingPlayerChoiceContext(),Creature,1,Creature,null);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var first=new MoveState("ROYAL_STRIKE",_=>Attack(13),new SingleAttackIntent(13));
        var second=new MoveState("HEAVY_SLASH",_=>Attack(16),new SingleAttackIntent(16));
        var guard=new MoveState("ROYAL_GUARD",_=>ProtectAlly(),new DefendIntent());
        var solo=new MoveState("LAST_STAND",_=>Attack(16),new SingleAttackIntent(16));
        var branch=new ConditionalBranchState("ALLY_CHECK");
        branch.AddState(guard,()=>LivingAllies.Any());
        branch.AddState(solo,()=>true);
        first.FollowUpState=second;second.FollowUpState=branch;
        guard.FollowUpState=first;solo.FollowUpState=first;
        return new MonsterMoveStateMachine([first,second,guard,solo,branch],first);
    }
    public async Task ProtectAlly()
    {
        var ally=LivingAllies.OrderBy(c=>c.CurrentHp).ThenBy(c=>c.CombatId).FirstOrDefault();
        if(ally==null){await Attack(16);return;}
        await Cast();
        // Powered monster block invokes the game's multiplayer multiplier exactly once.
        await CreatureCmd.GainBlock(ally,20,ValueProp.Move,null);
    }
    public void RefreshSoloIntent()
    {
        if(!LivingAllies.Any()&&NextMove?.Id=="ROYAL_GUARD")
            SetMoveImmediate((MoveState)MoveStateMachine!.States["LAST_STAND"],forceTransition:true);
    }
}

public sealed class TeaWarlord : TeaMonster
{
    public override int MinInitialHp => 85;
    public override int MaxInitialHp => 85;
    protected override MonsterModel VisualTemplate => ModelDb.Monster<MagiKnight>();
    protected override string AttackAnimation => "RamAttack";
    public override string CustomVisualPath => "res://scenes/creature_visuals/magi_knight.tscn";
    public override List<(string,string)> Localization => new MonsterLoc("军阀",
        [("SUPPLY_BLOCKADE","封锁补给"),("REQUISITION","军需管制"),("BREAK_FORMATION","破阵突袭"),("FATIGUE","疲敌战术")]);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var blockade=new MoveState("SUPPLY_BLOCKADE",_=>Blockade(),new DebuffIntent());
        var requisition=new MoveState("REQUISITION",_=>Requisition(),new CardDebuffIntent());
        var vulnerable=new MoveState("BREAK_FORMATION",_=>Pressure(true),new SingleAttackIntent(8),new DebuffIntent());
        var weak=new MoveState("FATIGUE",_=>Pressure(false),new SingleAttackIntent(8),new DebuffIntent());
        blockade.FollowUpState=requisition;requisition.FollowUpState=vulnerable;
        vulnerable.FollowUpState=weak;weak.FollowUpState=vulnerable;
        return new MonsterMoveStateMachine([blockade,requisition,vulnerable,weak],blockade);
    }
    public async Task Blockade()
    {
        await Cast();
        foreach(var target in CombatState.PlayerCreatures.Where(c=>!c.IsDead).ToArray())
            await PowerCmd.Apply<SupplyBlockadePower>(new ThrowingPlayerChoiceContext(),target,1,Creature,null);
    }
    public async Task Requisition()
    {
        await Cast();
        foreach(var target in CombatState.PlayerCreatures.Where(c=>!c.IsDead).ToArray())
            await PowerCmd.Apply<RequisitionPower>(new ThrowingPlayerChoiceContext(),target,1,Creature,null);
    }
    public async Task Pressure(bool vulnerable)
    {
        await Attack(8);
        foreach(var target in CombatState.PlayerCreatures.Where(c=>!c.IsDead).ToArray())
            if(vulnerable)await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(),target,2,Creature,null);
            else await PowerCmd.Apply<WeakPower>(new ThrowingPlayerChoiceContext(),target,2,Creature,null);
    }
}

public sealed class TeaMercenaryCaptain : TeaMonster
{
    public const int InitialRecruitCount=2;
    public int RecruitCount { get; private set; }=InitialRecruitCount;
    public override int MinInitialHp => 65;
    public override int MaxInitialHp => 65;
    protected override MonsterModel VisualTemplate => ModelDb.Monster<SpectralKnight>();
    protected override string AttackAnimation => "AttackSword";
    public override string CustomVisualPath => "res://scenes/creature_visuals/spectral_knight.tscn";
    public override List<(string,string)> Localization => new MonsterLoc("佣兵队长",
        [("RECRUIT_SOLDIERS","招募士兵"),("RECRUIT_VETERANS","招募精兵"),("RECRUIT_ELITES","招募锐士"),("EXPAND_ARMY","扩军整备")]);
    protected override void DeepCloneFields(){base.DeepCloneFields();RecruitCount=InitialRecruitCount;}
    public override Task AfterAddedToRoom() => PowerCmd.Apply<PlatingPower>(new ThrowingPlayerChoiceContext(),Creature,10,Creature,null);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // The attack icon forecasts the troops' combined damage; separate troop powers resolve it.
        var soldier=new MoveState("RECRUIT_SOLDIERS",_=>Recruit(1),new BuffIntent(),new TroopAttackIntent(()=>ProjectedDamage(1)));
        var veteran=new MoveState("RECRUIT_VETERANS",_=>Recruit(2),new BuffIntent(),new TroopAttackIntent(()=>ProjectedDamage(2)));
        var elite=new MoveState("RECRUIT_ELITES",_=>Recruit(3),new BuffIntent(),new TroopAttackIntent(()=>ProjectedDamage(3)));
        var expand=new MoveState("EXPAND_ARMY",_=>Expand(),new BuffIntent(),new TroopAttackIntent(()=>ProjectedDamage(0)));
        soldier.FollowUpState=veteran;veteran.FollowUpState=elite;elite.FollowUpState=expand;expand.FollowUpState=soldier;
        return new MonsterMoveStateMachine([soldier,veteran,elite,expand],soldier);
    }
    public decimal ProjectedDamage(int recruitKind)
    {
        var soldiers=Creature.GetPowerAmount<EnemySoldierPower>()+(recruitKind==1?RecruitCount:0);
        var veterans=Creature.GetPowerAmount<EnemyVeteranPower>()+(recruitKind==2?RecruitCount:0);
        var elites=Creature.GetPowerAmount<EnemyElitePower>()+(recruitKind==3?RecruitCount:0);
        return 2*soldiers+2*veterans+4*elites;
    }
    public async Task Recruit(int kind)
    {
        await Cast();var ctx=new ThrowingPlayerChoiceContext();
        if(kind==1)await PowerCmd.Apply<EnemySoldierPower>(ctx,Creature,RecruitCount,Creature,null);
        else if(kind==2)await PowerCmd.Apply<EnemyVeteranPower>(ctx,Creature,RecruitCount,Creature,null);
        else await PowerCmd.Apply<EnemyElitePower>(ctx,Creature,RecruitCount,Creature,null);
    }
    public async Task Expand()
    {
        await Cast();RecruitCount++;
        var ctx=new ThrowingPlayerChoiceContext();
        // Apply scales new powers, but an existing power's increment uses the raw amount.
        if(Creature.GetPower<PlatingPower>() is {} plating)
            await PowerCmd.ModifyAmount(ctx,plating,plating.GetScaledAmountForMultiplayer(CombatState,Creature,2,Creature,null),Creature,null);
        else
            await PowerCmd.Apply<PlatingPower>(ctx,Creature,2,Creature,null);
    }
}
