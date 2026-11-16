namespace ArtuMod;
public sealed class PurityInsightPower():OncePerTurnPower("净悟","当前{Amount}层。\n"+ArtuTerms.Rules["净悟"])
{
    public override Task AfterCardDrawnEarly(PlayerChoiceContext ctx,CardModel card,bool fromHandDraw)
    {
        if(card.Owner!=Owner.Player||!First("draw"))return Task.CompletedTask;
        if(!card.EnergyCost.CostsX){card.EnergyCost.AddThisTurn(-1);card.InvokeEnergyCostChanged();}
        return Task.CompletedTask;
    }
}
public sealed class SecretInsightPower():OncePerTurnPower("秘悟","当前{Amount}层。\n"+ArtuTerms.Rules["秘悟"])
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
public sealed class AwakeningPower():OncePerTurnPower("觉醒","当前{Amount}层。\n"+ArtuTerms.Rules["觉醒"])
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
