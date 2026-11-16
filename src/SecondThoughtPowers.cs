namespace ArtuMod;
public sealed class PurityInsightPower():OncePerTurnPower("净悟","每回合首张抽牌费用-1，仅持续当前回合，不随层数叠加。若无秘悟，战后复制或进化。")
{
    public override Task AfterCardDrawnEarly(PlayerChoiceContext ctx,CardModel card,bool fromHandDraw)
    {
        if(card.Owner!=Owner.Player||!First("draw"))return Task.CompletedTask;
        if(!card.EnergyCost.CostsX){card.EnergyCost.AddThisTurn(-1);card.InvokeEnergyCostChanged();}
        return Task.CompletedTask;
    }
}
public sealed class SecretInsightPower():OncePerTurnPower("秘悟","每回合首次使用苏丹牌，额外失去2生命并抽2张；战斗收益不叠加。若无净悟，战后复制或进化。")
{
    CardPlay? pending;
    protected override void DeepCloneFields(){base.DeepCloneFields();pending=null;}
    public override Task BeforeCardPlayed(CardPlay play)
    {if(play.Player==Owner.Player&&play.Card is SultanCard&&SultanUsage.FirstThisTurn(play,c=>c is SultanCard)&&First("sultan"))pending=play;return Task.CompletedTask;}
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(pending!=play)return;pending=null;
        await Util.LoseHp(ctx,play.Player,2);if(!play.Player.Creature.IsDead)await CardPileCmd.Draw(ctx,2,play.Player);
    }
}
public sealed class AwakeningPower():OncePerTurnPower("觉醒","每回合首次使用非苏丹牌获得1士兵；此前没有用苏丹牌时额外抽1。若无臣服，战后复制或进化。")
{
    CardPlay? pending;bool draw;
    protected override void DeepCloneFields(){base.DeepCloneFields();pending=null;draw=false;}
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if(play.Player==Owner.Player&&play.Card is not SultanCard&&SultanUsage.FirstThisTurn(play,c=>c is not SultanCard)&&First("ordinary"))
        {pending=play;draw=!SultanUsage.SultanBefore(play);}
        return Task.CompletedTask;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        // A newly played Awakening can observe its own first non-Sultan play in the after hook.
        if(pending==null&&play.Player==Owner.Player&&play.Card is not SultanCard&&SultanUsage.FirstThisTurn(play,c=>c is not SultanCard)&&First("ordinary"))
        {pending=play;draw=!SultanUsage.SultanBefore(play);}
        if(pending!=play)return;pending=null;await ArmyPower.Gain(ctx,play.Player,1,1,play.Card);if(draw)await CardPileCmd.Draw(ctx,1,play.Player);
    }
}
