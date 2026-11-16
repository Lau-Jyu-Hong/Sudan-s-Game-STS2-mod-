namespace ArtuMod;

public sealed class RoyalProtectionPower():ArtuPower("王权庇护","只要仍有存活队友，每轮全队共同最多使其损失最大生命的10%（向上取整）。最后一名队友死亡时立即解除。")
{
    decimal spent;
    int lastRound=-1;
    public bool Protected => Owner.Monster is TeaCrownKnight king&&king.LivingAllies.Any();
    public int Limit => (int)Math.Ceiling(Owner.MaxHp/10m);
    public decimal Remaining => Protected?Math.Max(0,Limit-spent):decimal.MaxValue;
    public override int DisplayAmount => Protected?(int)Remaining:0;
    protected override void DeepCloneFields(){base.DeepCloneFields();spent=0;lastRound=-1;}
    public override decimal ModifyHpLostBeforeOstyLate(Creature target,decimal amount,ValueProp props,Creature? dealer,CardModel? source)
        =>target==Owner&&Protected?Math.Min(amount,Remaining):amount;
    public override Task AfterCurrentHpChanged(Creature creature,decimal delta)
    {
        if(creature==Owner&&delta<0&&Protected){spent-=delta;InvokeDisplayAmountChanged();}
        return Task.CompletedTask;
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext ctx,CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {
        // Extra individual player turns must not create a new damage allowance for the team.
        if(side==CombatSide.Player&&lastRound!=state.RoundNumber){spent=0;lastRound=state.RoundNumber;InvokeDisplayAmountChanged();}
        return Task.CompletedTask;
    }
    public override async Task AfterDeath(PlayerChoiceContext ctx,Creature creature,bool wasRemovalPrevented,float deathAnimLength)
    {
        if(Owner.IsDead||Protected)return;
        ((TeaCrownKnight)Owner.Monster!).RefreshSoloIntent();
        await PowerCmd.Remove(this);
    }
}

// Also cover effects that directly set current HP instead of using the damage command.
[HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.SetCurrentHp))]
static class RoyalProtectionDirectHp
{
    static void Prefix(Creature creature,ref decimal amount)
    {
        if(amount<creature.CurrentHp&&creature.GetPower<RoyalProtectionPower>() is { Protected:true } protection)
            amount=creature.CurrentHp-Math.Min(creature.CurrentHp-amount,protection.Remaining);
    }
}

public sealed class SupplyBlockadePower():ArtuPower("封锁补给","下回合开始时少抽{Amount}张牌，随后移除。",PowerType.Debuff)
{
    public override decimal ModifyHandDraw(Player player,decimal count)
        =>player==Owner.Player&&AmountOnTurnStart>0?Math.Max(0,count-Amount):count;
    public override async Task AfterSideTurnStart(CombatSide side,IReadOnlyList<Creature> participants,ICombatState state)
    {if(participants.Contains(Owner)&&AmountOnTurnStart>0)await PowerCmd.Remove(this);}
}

public sealed class RequisitionPower():ArtuPower("军需管制","下回合首次抽到的牌耗能+1，仅持续该回合。",PowerType.Debuff)
{
    int targetTurn;
    bool triggered;
    protected override void DeepCloneFields(){base.DeepCloneFields();targetTurn=0;triggered=false;}
    public override Task AfterApplied(Creature? applier,CardModel? cardSource)
    {targetTurn=(Owner.Player?.PlayerCombatState?.TurnNumber??0)+1;return Task.CompletedTask;}
    public override async Task AfterCardDrawnEarly(PlayerChoiceContext ctx,CardModel card,bool fromHandDraw)
    {
        if(card.Owner!=Owner.Player||Owner.Player?.PlayerCombatState?.TurnNumber!=targetTurn||triggered)return;
        triggered=true;
        // Match the existing Hextech first-draw approach using game-native temporary costs.
        // X/unplayable costs still consume the first-draw trigger; they are not converted to fixed costs.
        if(!card.EnergyCost.CostsX)
        {card.EnergyCost.AddThisTurnOrUntilPlayed(Amount);card.InvokeEnergyCostChanged();}
        await PowerCmd.Remove(this);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {
        if(participants.Contains(Owner)&&Owner.Player?.PlayerCombatState?.TurnNumber>=targetTurn)
            await PowerCmd.Remove(this);
    }
}

public abstract class EnemyArmyPower(string name,string description):ArtuPower(name,description)
{
    public abstract int Kind { get; }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(side==CombatSide.Enemy&&participants.Contains(Owner)&&!Owner.IsDead)await Trigger(ctx);}
    public async Task Trigger(PlayerChoiceContext ctx)
    {
        if(Owner.IsDead||Owner.Monster is not TeaMercenaryCaptain captain)return;
        if(Kind==2&&Amount>=3)await PowerCmd.Apply<VigorPower>(ctx,Owner,5,Owner,null);
        // FromMonster targets all players. Counts/damage do not get another multiplayer multiplier.
        await DamageCmd.Attack(Amount*(Kind==3?4:2)).FromMonster(captain)
            .WithAttackerAnim("AttackSword",0.25f).Execute(ctx);
        if(Kind==1)await CreatureCmd.GainBlock(Owner,Amount,ValueProp.Move,null);
        if(Kind==2)
        {
            foreach(var target in Owner.CombatState!.PlayerCreatures.Where(c=>!c.IsDead).ToArray())
            {
                if(Amount>=6)await PowerCmd.Apply<VulnerablePower>(ctx,target,1,Owner,null);
                if(Amount>=9)await PowerCmd.Apply<WeakPower>(ctx,target,1,Owner,null);
            }
        }
    }
}
public sealed class EnemySoldierPower():EnemyArmyPower("敌方士兵","敌方回合结束时，对所有玩家造成2×{Amount}伤害，队长获得{Amount}基础格挡；格挡按人数缩放。"){public override int Kind=>1;}
public sealed class EnemyVeteranPower():EnemyArmyPower("敌方精兵","敌方回合结束时，对所有玩家造成2×{Amount}伤害。3层：攻击前5活力；6层：对所有玩家施加1易伤；9层：再施加1虚弱。"){public override int Kind=>2;}
public sealed class EnemyElitePower():EnemyArmyPower("敌方锐士","敌方回合结束时，对所有玩家造成4×{Amount}伤害。"){public override int Kind=>3;}
