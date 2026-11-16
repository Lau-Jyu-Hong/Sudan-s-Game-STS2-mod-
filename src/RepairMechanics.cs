namespace ArtuMod;

// The game command changes Gold synchronously. Count actual loss once for every source,
// including foreign card effects, but never shops/events outside an active combat.
[HarmonyPatch(typeof(PlayerCmd),nameof(PlayerCmd.LoseGold))]
public static class TrackCombatGoldLoss
{
    static void Prefix(Player player,out int __state)=>__state=player.Gold;
    static void Postfix(Player player,int __state)
    {
        if(!CombatManager.Instance.IsInProgress||Util.State(player) is not {} state)return;
        int lost=Math.Max(0,__state-player.Gold);if(lost==0)return;
        state.GoldLost+=lost;player.Creature.GetPower<SpendingRefundPower>()?.Refresh();
        GD.Print($"[Artu gold loss] player={player.NetId} lost={lost} accumulated={state.GoldLost} refund={state.Refund}");
    }
}
public sealed class SpendingRefundPower():ArtuPower("有去有回",
    "本场已损失{Spent}金币；距严格超过20还需{Needed}金币。\n{Status}\n胜利后直接返还{Amount}金币。多张能力的返还金额相加。")
{
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Spent",0),new DynamicVar("Needed",21),new StringVar("Status","未达成")];
    public void Refresh()
    {
        int spent=Util.State(Owner.Player!)?.GoldLost??0;
        DynamicVars["Spent"].BaseValue=spent;DynamicVars["Needed"].BaseValue=Math.Max(0,21-spent);
        ((StringVar)DynamicVars["Status"]).StringValue=spent>20?"已达成":"未达成";
        InvokeDisplayAmountChanged();
    }
}
public sealed class PaymentDamageVar():DamageVar(8,ValueProp.Move)
{
    public override void UpdateCardPreview(CardModel card,CardPreviewMode mode,Creature? target,bool runGlobalHooks)
    {
        BaseValue=8+(card.RunState==null?0:PaymentLedger.ThisTurn(card.Owner))*card.DynamicVars["Bonus"].IntValue;
        base.UpdateCardPreview(card,mode,target,runGlobalHooks);
    }
}
