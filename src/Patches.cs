using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
namespace ArtuMod;
[HarmonyPatch(typeof(PlayerCmd),nameof(PlayerCmd.EndTurn))]
public static class ForcedOrdersEndTurn
{
    static bool Prefix(Player player,bool canBackOut)=>!canBackOut||player.Creature.IsDead||(!BeckyController.TakeoverRunning(player)&&(!Util.Tyranny(player)||player.PlayerCombatState?.Hand.Cards.Any(c=>c is SultanCard)!=true));
}
[HarmonyPatch(typeof(PowerCmd),nameof(PowerCmd.TickDownDuration))]
public static class FreezeDebuffs
{
    static bool Prefix(PowerModel power)=>power.Type!=PowerType.Debuff||!power.Owner.HasPower<SubjugatedPower>();
}
[HarmonyPatch(typeof(PowerCmd),nameof(PowerCmd.Decrement))]
public static class FreezeNaturalDebuffCounter
{
    static bool Prefix(PowerModel power,ref Task __result)
    {if(power.Type==PowerType.Debuff&&power.Owner.HasPower<SubjugatedPower>()){__result=Task.CompletedTask;return false;}return true;}
}
[HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.Heal))]
public static class ClampHealing
{
    static bool Prefix(Creature creature,ref decimal amount,ref Task __result)
    {
        if(creature.Player is not {} p||Util.State(p) is not {} state)return true;
        if(state.Anger>=10||state.Mockery>=10){__result=state.EnforceHp();return false;}
        if(state.HealingLocked){__result=Redirect(p,amount);return false;}
        amount=Math.Max(0,Math.Min(amount,Rules.HpCeiling(creature.MaxHp,state.Mockery)-creature.CurrentHp));
        return true;
    }
    static async Task Redirect(Player p,decimal amount){foreach(var target in Util.Enemies(p).ToArray())await CreatureCmd.Heal(target,amount);}
}
[HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.SetCurrentHp))]
public static class ClampRevival
{
    static void Prefix(Creature creature,ref decimal amount)
    {if(creature.Player is {} p&&Util.State(p) is {} state)amount=state.Anger>=10?0:Math.Min(amount,Rules.HpCeiling(creature.MaxHp,state.Mockery));}
}
[HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.GainMaxHp))]
public static class LockMaxHpGrowth
{
    static bool Prefix(Creature creature,ref Task __result)
    {if(creature.Player is {} p&&Util.State(p)?.GrowthLocked==true){__result=Task.CompletedTask;return false;}return true;}
}
[HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.SetMaxHp))]
public static class ClampMaxHpGrowth
{
    static void Prefix(Creature creature,ref decimal amount)
    {if(creature.Player is {} p&&Util.State(p) is {GrowthLocked:true})amount=Math.Min(amount,creature.MaxHp);}
}
[HarmonyPatch(typeof(RelicCmd),nameof(RelicCmd.Replace))]
public static class PreserveImperialState
{
    static void Prefix(RelicModel original,RelicModel replace)
    {
        if(original is not WitchCardBox||replace is not WitchCardBox)return;
        foreach(var prop in typeof(WitchCardBox).GetProperties().Where(p=>p.GetCustomAttribute<SavedPropertyAttribute>()!=null&&p.CanWrite))
            prop.SetValue(replace,prop.GetValue(original));
    }
}
[HarmonyPatch(typeof(ArchaicTooth), "GetTranscendenceStarterCard")]
public static class ArtuTranscendenceBase
{
    static bool Prefix(Player player,ref CardModel? __result)
    {if(player.Character is not Artu)return true;__result=player.Deck.Cards.FirstOrDefault(c=>c is RoyalPass);return false;}
}
[HarmonyPatch(typeof(ArchaicTooth), "GetTranscendenceTransformedCard")]
public static class ArtuTranscendenceResult
{
    static bool Prefix(CardModel starterCard,ref CardModel __result)
    {if(starterCard is not RoyalPass)return true;__result=starterCard.Owner.RunState.CreateCard(ModelDb.Card<NoteInBook>(),starterCard.Owner);return false;}
}
[HarmonyPatch(typeof(DustyTome),nameof(DustyTome.SetupForPlayer))]
public static class DarvArtuSetup
{
    static bool Prefix(DustyTome __instance,Player player)
    {if(player.Character is not Artu)return true;__instance.AncientCard=ModelDb.Card<GoldSlaughter>().Id;return false;}
}
[HarmonyPatch(typeof(DustyTome),nameof(DustyTome.AfterObtained))]
public static class DarvArtuReward
{
    static bool Prefix(DustyTome __instance,ref Task __result)
    {if(__instance.Owner.Character is not Artu)return true;__result=Obtain(__instance.Owner);return false;}
    static async Task Obtain(Player p){var chosen=await Util.Choose(p,Enum.GetValues<SultanSuit>().Select(s=>SultanCard.Get(s,SultanGrade.Gold)));if(chosen!=null)await Util.AddToDeck(p,chosen);}
}
[HarmonyPatch(typeof(RelicModel),nameof(RelicModel.DynamicDescription),MethodType.Getter)]
public static class ImperialStateTooltip
{
    static void Postfix(RelicModel __instance,ref LocString __result)
    {if(__instance is WitchCardBox state){__result.Add("Anger",state.Anger);__result.Add("Mockery",state.Mockery);}}
}
public sealed class MultiArmorPower():ArtuPower("多层护甲","回合结束获得{Amount}格挡；受到未格挡的攻击伤害后减少1层。")
{
    public override Task BeforeSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)=>participants.Contains(Owner)?CreatureCmd.GainBlock(Owner,Amount,ValueProp.Unpowered,null):Task.CompletedTask;
    public override async Task AfterDamageReceived(PlayerChoiceContext ctx,Creature target,DamageResult result,ValueProp props,Creature? dealer,CardModel? source)
    {if(target==Owner&&result.UnblockedDamage>0&&!props.HasFlag(ValueProp.Unpowered))await PowerCmd.Decrement(this);}
}
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry),nameof(MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry.EnoughGold),MethodType.Getter)]
public static class BanSilverLuxuryShop
{
    static void Postfix(Player ____player,ref bool __result)
    {if(Util.State(____player)?.ActiveShopBan==true)__result=false;}
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.AnimShake))]
public static class CreatureAnimShakeHurt
{
    static void Postfix(NCreature __instance)
    {
        try
        {
            var p = __instance.Visuals?.GetNodeOrNull<AnimationPlayer>("%AnimationPlayer") ??
                    __instance.Visuals?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (p != null && p.HasAnimation("hit"))
            {
                p.Play("hit");
                if (p.HasAnimation("idle")) p.Queue("idle");
            }
        }
        catch {}
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
public static class ArtuAttackAnimationTrigger
{
    static void Postfix(CardModel __instance)
    {
        try
        {
            if (__instance.Owner?.Character is not Artu) return;
            var animName = __instance.Type == CardType.Attack ? "attack"
                : __instance.Type is CardType.Skill or CardType.Power ? "cast" : null;
            if (animName == null) return;
            var room = MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance;
            var node = room?.GetCreatureNode(__instance.Owner.Creature);
            var p = node?.Visuals?.GetNodeOrNull<AnimationPlayer>("%AnimationPlayer") ??
                    node?.Visuals?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
            if (p != null && p.HasAnimation(animName))
            {
                p.Play(animName);
                if (p.HasAnimation("idle")) p.Queue("idle");
            }
        }
        catch {}
    }
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready))]
public static class ArtuMerchantIdle
{
    // The stock shop character plays a Spine clip on its first child. A painted
    // pose has an AnimationPlayer instead, and the stock call would also draw a
    // chaotic random number. Skip that path only when this scene brought its own.
    static bool Prefix(NMerchantCharacter __instance)
    {
        var player = Animation(__instance);
        if (player == null) return true;
        if (player.HasAnimation("idle")) player.Play("idle");
        return false;
    }

    internal static AnimationPlayer? Animation(Node node) =>
        node.GetNodeOrNull<AnimationPlayer>("%AnimationPlayer") ??
        node.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
public static class ArtuMerchantPlay
{
    static bool Prefix(NMerchantCharacter __instance, string anim)
    {
        var player = ArtuMerchantIdle.Animation(__instance);
        if (player == null) return true;
        var name = anim is "relaxed_loop" or "Idle" ? "idle" : anim is "Die" or "Dead" or "dead" ? "die" : anim;
        if (!player.HasAnimation(name)) name = player.HasAnimation("idle") ? "idle" : "";
        if (name.Length > 0) player.Play(name);
        return false;
    }
}
