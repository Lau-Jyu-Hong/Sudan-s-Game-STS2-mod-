using MegaCrit.Sts2.Core.MonsterMoves.Intents;
namespace ArtuMod;

// Native intent math assumes powered attacks. Troop intent must not preview Strength/Weak/Vulnerable.
public sealed class TroopAttackIntent(Func<decimal> damage):SingleAttackIntent(damage)
{
    public override int GetTotalDamage(IEnumerable<Creature> targets,Creature owner)=>Math.Max(0,(int)DamageCalc!());
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets,Creature owner)
    {
        var text=base.GetIntentDescription(targets,owner);
        text.Add("Damage",GetTotalDamage(targets,owner));return text;
    }
}
