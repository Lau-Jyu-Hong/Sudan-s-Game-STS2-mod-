namespace ArtuMod;
public static class RelicTrade
{
    public static async Task Propose(Player p)
    {
        var players=p.RunState.Players.Where(other=>other!=p&&!other.Creature.IsDead).ToArray();
        if(players.Length==0)return;
        var choice=await Util.Choose(p,players.Select(other=>(CardModel)new PlayerChoiceCard(other)));
        if(choice is not PlayerChoiceCard picked)return;
        var partner=p.RunState.Players.FirstOrDefault(other=>other.NetId==picked.PlayerId);if(partner==null)return;
        var ours=await RelicSelection.Choose(p,p.Relics.Where(CanTrade).ToList());if(ours==null)return;
        var theirs=await RelicSelection.Choose(partner,partner.Relics.Where(CanTrade).ToList());if(theirs==null)return;
        var agree=await Util.Choose(partner,[new TradeConfirm(ours,theirs),ModelDb.Card<StopOption>()]);
        if(agree is not TradeConfirm)return;
        var confirm=await Util.Choose(p,[new TradeConfirm(ours,theirs),ModelDb.Card<StopOption>()]);
        if(confirm is not TradeConfirm||!p.Relics.Contains(ours)||!partner.Relics.Contains(theirs))return;
        // RelicCmd.Remove/Obtain maintain owner hooks; mutable instances keep their saved counters.
        var theirCopy=RelicModel.FromSerializable(theirs.ToSerializable());
        var ourCopy=RelicModel.FromSerializable(ours.ToSerializable());
        await RelicCmd.Remove(ours);await RelicCmd.Remove(theirs);
        await RelicCmd.Obtain(theirCopy,p);await RelicCmd.Obtain(ourCopy,partner);
    }
    static bool CanTrade(RelicModel r)=>r.Rarity!=RelicRarity.Starter&&r is not WitchCardBox and not TyrantsAccomplice&&!r.HasUponPickupEffect;
}
public sealed class PlayerChoiceCard:OptionCard
{
    public ulong PlayerId {get;set;}
    public string PlayerName {get;set;}="队友";
    public PlayerChoiceCard():base("队友","选择交易的队友。"){}
    public PlayerChoiceCard(Player p):this(){PlayerId=p.NetId;PlayerName=p.Character.Title.GetFormattedText()+" ("+p.NetId+")";}
    public override string Title=>PlayerName;
}
public sealed class TradeConfirm:OptionCard
{
    public string Terms {get;set;}="同意交换双方已选定的遗物。";
    public TradeConfirm():base("同意交换","双方确认后交换。"){}
    public TradeConfirm(RelicModel ours,RelicModel theirs):this(){Terms=ours.Title.GetFormattedText()+" ⇄ "+theirs.Title.GetFormattedText();}
    public override string Title=>"确认："+Terms;
}
