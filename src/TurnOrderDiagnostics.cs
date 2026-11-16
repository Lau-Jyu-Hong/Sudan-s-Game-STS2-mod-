using MegaCrit.Sts2.Core.Hooks;
namespace ArtuMod;
// Opt-in evidence gathering for the player's full mod list. Never changes block or turn order.
public static class TurnOrderDiagnostics
{
    public static bool Enabled=>System.Environment.GetEnvironmentVariable("ARTU_TRACE_BLOCK_ORDER")=="1";
    public static void Record(string phase,ICombatState? state)
    {
        if(!Enabled||state==null||!state.PlayerCreatures.Any(c=>c.Player?.Character is Artu))return;
        GD.Print($"[Artu block order] tick={Time.GetTicksMsec()} phase={phase} side={state.CurrentSide} enemies=[{string.Join(';',state.HittableEnemies.Select(e=>$"{e.Monster?.Id}:block={e.Block},hp={e.CurrentHp}"))}]");
    }
}
[HarmonyPatch(typeof(Hook),nameof(Hook.BeforeSideTurnEnd))]
static class TracePlayerEnd
{
    static void Prefix(ICombatState combatState,CombatSide side){if(side==CombatSide.Player)TurnOrderDiagnostics.Record("before player end hooks",combatState);}
    static void Postfix(ICombatState combatState,CombatSide side,ref Task __result){if(TurnOrderDiagnostics.Enabled&&side==CombatSide.Player)__result=After(__result,combatState);}
    static async Task After(Task task,ICombatState state){await task;TurnOrderDiagnostics.Record("after player end hooks",state);}
}
[HarmonyPatch(typeof(ArmyPower),nameof(ArmyPower.Trigger))]
static class TraceArmyOrder
{
    static void Prefix(ArmyPower __instance)=>TurnOrderDiagnostics.Record("before army "+__instance.Kind,__instance.Owner.CombatState);
    static void Postfix(ArmyPower __instance,ref Task __result){if(TurnOrderDiagnostics.Enabled)__result=After(__result,__instance);}
    static async Task After(Task task,ArmyPower power){await task;TurnOrderDiagnostics.Record("after army "+power.Kind,power.Owner.CombatState);}
}
[HarmonyPatch(typeof(Creature),nameof(Creature.AfterTurnStart))]
static class TraceEnemyClear
{
    static void Prefix(Creature __instance,CombatSide side){if(side==CombatSide.Enemy&&__instance.Monster!=null)TurnOrderDiagnostics.Record("before enemy turn clear",__instance.CombatState);}
    static void Postfix(Creature __instance,CombatSide side,ref Task __result){if(TurnOrderDiagnostics.Enabled&&side==CombatSide.Enemy&&__instance.Monster!=null)__result=After(__result,__instance);}
    static async Task After(Task task,Creature creature){await task;TurnOrderDiagnostics.Record("after enemy turn clear",creature.CombatState);}
}
