using System.Runtime.CompilerServices;
namespace ArtuMod;

public abstract class OncePerTurnPower(string name,string description):ArtuPower(name,description)
{
    Dictionary<string,int> lastTurns=[];
    protected override void DeepCloneFields(){base.DeepCloneFields();lastTurns=[];}
    protected bool First(string key)
    {
        int turn=Owner.Player!.PlayerCombatState!.TurnNumber;
        if(lastTurns.TryGetValue(key,out int last)&&last==turn)return false;
        lastTurns[key]=turn;return true;
    }
}
public static class PaymentLedger
{
    sealed class Data { public int Turn=-1;public int Count; }
    static readonly ConditionalWeakTable<Player,Data> DataByPlayer=new();
    public static int ThisTurn(Player player)
    {var data=DataByPlayer.GetOrCreateValue(player);return data.Turn==player.PlayerCombatState?.TurnNumber?data.Count:0;}
    public static void Reset(Player player)=>DataByPlayer.Remove(player);
    public static async Task Record(PlayerChoiceContext ctx,Player player,bool originalCost=true)
    {
        var data=DataByPlayer.GetOrCreateValue(player);int turn=player.PlayerCombatState?.TurnNumber??0;
        if(data.Turn!=turn){data.Turn=turn;data.Count=0;}if(originalCost)data.Count++;
        if(Util.State(player) is {} state)state.CostsPaid++;
        if(player.Creature.GetPower<KingsBurdenPower>() is {} burden)
            await CreatureCmd.GainBlock(player.Creature,burden.Amount,ValueProp.Unpowered,null);
        player.PlayerCombatState?.RecalculateCardValues();
    }
}
public sealed class KingsBurdenPower():ArtuPower("为君分忧","每成功支付一次苏丹牌代价获得{Amount}格挡。");
public sealed class BannerResonancePower():OncePerTurnPower("军旗共鸣","每回合首次获得某一种兵种，额外获得{Amount}层；四种分别计算。")
{
    public async Task OnGain(PlayerChoiceContext ctx,int kind,CardModel? source)
    {if(First(kind.ToString()))await ArmyPower.Gain(ctx,Owner.Player!,kind,Amount,source,allowResonance:false);}
}
public sealed class SecretAssemblyPower():OncePerTurnPower("秘密集会","每回合首次使用思潮获得{Amount}士兵；首次使用读物抽牌。")
{
    public int DrawCount {get;set;}
    protected override void DeepCloneFields(){base.DeepCloneFields();DrawCount=0;}
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner.Player||play.Card is not ArtuCard card)return;
        if(card.Thought&&First("thought"))await ArmyPower.Gain(ctx,play.Player,1,Amount,play.Card);
        if(card.Reading&&First("reading"))await CardPileCmd.Draw(ctx,DrawCount,play.Player);
    }
}
public sealed class KingsTreasuryPower():ArtuPower("王之宝库","战斗结束每种实际使用过的不同苏丹牌获得{Amount}金币。");
public sealed class ArmyInterchangePower():ArtuPower("互通有无","有士兵时锐士结算获得格挡；有锐士时精兵每层伤害+1。")
{
    public bool CountSoldiersForThreshold {get;set;}
    protected override void DeepCloneFields(){base.DeepCloneFields();CountSoldiersForThreshold=false;}
}
public sealed class ArmySpecializationPower():ArtuPower("特化部队","本回合高级兵种视为指定类型，真实层数保持不变。")
{
    public int FormationKind {get;set;}=1;
    protected override void DeepCloneFields(){base.DeepCloneFields();FormationKind=1;}
    public override async Task AfterSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {if(participants.Contains(Owner))await PowerCmd.Remove(this);}
}
public sealed class LivingOffWarPower():OncePerTurnPower("以战养战","每回合首次消耗兵种，获得同等数量的一种随机高级兵种。")
{
    public async Task OnConsume(PlayerChoiceContext ctx,int amount,CardModel? source)
    {if(amount>0&&First("consume"))await ArmyPower.Gain(ctx,Owner.Player!,ArmyPower.RandomAdvanced(Owner.Player!),amount*Amount,source);}
}
public sealed class LibraryPower():OncePerTurnPower("藏书阁","每回合首次使用读物抽2张，将其中1张本回合变为0费。")
{
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner.Player||play.Card is not ArtuCard {Reading:true}||!First("reading"))return;
        var drawn=(await CardPileCmd.Draw(ctx,2*Amount,play.Player)).Where(c=>c.Pile?.Type==PileType.Hand).ToArray();
        var card=await Util.Choose(play.Player,drawn,false,ctx);card?.SetToFreeThisTurn();
    }
}
public sealed class NotoriousPower():ArtuPower("恶名昭彰","每回合开始按罪证层数获得力量，最多{Amount}。")
{
    public override Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)
        =>player==Owner.Player?PowerCmd.Apply<StrengthPower>(ctx,Owner,Math.Min(Amount,Owner.GetPowerAmount<CrimePower>()),Owner,null):Task.CompletedTask;
}
