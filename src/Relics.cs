namespace ArtuMod;
[Pool(typeof(ArtuRelicPool))]
public class WitchCardBox : CustomRelicModel
{
    protected override IEnumerable<MegaCrit.Sts2.Core.HoverTips.IHoverTip> ExtraHoverTips=>ArtuTerms.Tips("苏丹牌",ArtuTerms.OwnerOf(this));
    public override RelicRarity Rarity=>RelicRarity.Starter;
    public override string PackedIconPath=>"res://ArtuMod/images/witchcardbox.tres";
    protected override string BigIconPath=>PackedIconPath;
    [SavedProperty] public int Anger {get;set;}
    [SavedProperty] public int Mockery {get;set;}
    [SavedProperty] public int WeakBattles {get;set;}
    [SavedProperty] public int VulnerableBattles {get;set;}
    [SavedProperty] public int BoulderBattles {get;set;}
    [SavedProperty] public int MirrorHealBattles {get;set;}
    [SavedProperty] public int StrengthPenalty {get;set;}
    [SavedProperty] public int ShopPurchases {get;set;}
    [SavedProperty] public int ShopVisits {get;set;}
    [SavedProperty] public int NoBuyingShops {get;set;}
    [SavedProperty] public bool ActiveShopTax {get;set;}
    [SavedProperty] public bool ActiveShopBan {get;set;}
    [SavedProperty] public int StrengthLockBattles {get;set;}
    [SavedProperty] public bool GrowthLocked {get;set;}
    [SavedProperty] public int MaxHpLimit {get;set;}
    [SavedProperty] public bool StatsLocked {get;set;}
    [SavedProperty] public bool GoldLocked {get;set;}
    [SavedProperty] public bool HealingLocked {get;set;}
    [SavedProperty] public int AgendaStage {get;set;}
    [SavedProperty] public int CostsPaid {get;set;}
    [SavedProperty] public int GoldLost {get;set;}
    [SavedProperty] public int Refund {get;set;}
    [SavedProperty] public int Fear {get;set;}
    [SavedProperty] public int Pure {get;set;}
    [SavedProperty] public int Unclean {get;set;}
    [SavedProperty] public int Submission {get;set;}
    [SavedProperty] public int PureSecond {get;set;}
    [SavedProperty] public int DarkSecond {get;set;}
    [SavedProperty] public int AwakeSecond {get;set;}
    [SavedProperty] public int TreasuryRate {get;set;}
    [SavedProperty] public int Crime {get;set;}
    [SavedProperty] public int AgendaFlags {get;set;}
    [SavedProperty] public int ReadingUses {get;set;}
    [SavedProperty] public string ReadingNames {get;set;}="";
    [SavedProperty] public int LeviesConverted {get;set;}
    [SavedProperty] public int Devotion {get;set;}
    [SavedProperty] public int Fanaticism {get;set;}
    [SavedProperty] public int Treason {get;set;}
    [SavedProperty] public bool ConceptGranted {get;set;}
    [SavedProperty] public bool PlanGranted {get;set;}
    [SavedProperty] public bool RevolutionWon {get;set;}
    [SavedProperty] public bool ClearStreamsSeen {get;set;}
    [SavedProperty] public bool NurserySeen {get;set;}
    [SavedProperty] public int PendingEvolutions {get;set;}
    [SavedProperty] public int LastOfferedEvolutionStage {get;set;}=-1;
    [SavedProperty] public int BattleWins {get;set;}
    [SavedProperty] public int EvolutionResolvedVictory {get;set;}
    [SavedProperty] public int ForgivenessClaimedVictory {get;set;}
    private bool enforcing;
    public override List<(string,string)> Localization=>new RelicLoc("女术士的牌盒",
        "每场战斗开始时，从抽牌堆中选择一张苏丹牌加入手牌。休息处可将非黄金苏丹牌换成随机高一级苏丹牌。","皇命不曾远去。");
    protected override IEnumerable<DynamicVar> CanonicalVars=>[new DynamicVar("Anger",0),new DynamicVar("Mockery",0)];
    public override RelicModel? GetUpgradeReplacement()=>ModelDb.Relic<WitchTreasureBox>();
    public async Task EnforceHp()
    {
        if(enforcing)return;
        enforcing=true;
        try{
            if(Anger>=Rules.Execution||Mockery>=Rules.Execution){await CreatureCmd.Kill(Owner.Creature,true);return;}
            int cap=Rules.HpCeiling(Owner.Creature.MaxHp,Mockery);
            if(Owner.Creature.CurrentHp>cap)await CreatureCmd.SetCurrentHp(Owner.Creature,cap);
        }finally{enforcing=false;}
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext ctx,CardPlay play)
    {
        if(play.Player!=Owner)return;var d=V04.Data(Owner);
        if(play.Card is ArtuCard card)
        {
            if(card.Reading&&card is not NoteInBook)
            {ReadingUses++;var names=ReadingNames.Split(';',StringSplitOptions.RemoveEmptyEntries).ToHashSet();names.Add(card.Id.ToString());ReadingNames=string.Join(';',names.Order());}
            if(card.Thought&&card is not NoteInBook)d.Thoughts.Add(card.Id);
            if(card is ThirdThought)d.ThirdUsed.Add(card.GetType());
            if(card is RegicidePlan)d.RegicideUsed=true;
            if(card is RevolutionConcept)d.ConceptUsed=true;
        }
        if(play.Card is SultanCard s)d.Suits.Add(s.Suit);
        await RevolutionAgenda.Refresh(Owner,ctx);
        if(RevolutionAgenda.Active(Owner)&&d.ConceptUsed&&d.RegicideUsed&&!PlanGranted)
        {PlanGranted=true;await CardPileCmd.Add(Owner.Creature.CombatState!.CreateCard<RevolutionPlan>(Owner),PileType.Hand);}
    }
    public override Task AfterPowerAmountChanged(PlayerChoiceContext ctx,PowerModel power,decimal amount,Creature? applier,CardModel? card)
    {
        if(power.Owner!=Owner.Creature)return Task.CompletedTask;
        if(power is CrimePower)Crime=Math.Max(0,power.Amount);
        return RevolutionAgenda.Refresh(Owner,ctx);
    }
    public override Task AfterCardChangedPiles(CardModel card,PileType oldPile,AbstractModel? by)
        =>card.Owner==Owner&&(oldPile==PileType.Deck||card.Pile?.Type==PileType.Deck)?RevolutionAgenda.Refresh(Owner,new BlockingPlayerChoiceContext()):Task.CompletedTask;
    public override Task AfterCombatEnd(CombatRoom room)
    {Crime=Owner.Creature.GetPowerAmount<CrimePower>();return Task.CompletedTask;}
    public override Task AfterCurrentHpChanged(Creature creature,decimal delta)=>creature==Owner.Creature?EnforceHp():Task.CompletedTask;
    public override async Task BeforeCombatStart()
    {
        CostsPaid=0;GoldLost=0;Refund=0;Fear=0;Pure=0;Unclean=0;Submission=0;
        PureSecond=0;DarkSecond=0;AwakeSecond=0;TreasuryRate=0;
        PaymentLedger.Reset(Owner);SultanUsage.Reset(Owner);V04.Reset(Owner);
        if(Crime>0)await PowerCmd.Apply<CrimePower>(new ThrowingPlayerChoiceContext(),Owner.Creature,Crime,Owner.Creature,null);
        foreach(var card in Owner.Deck.Cards.OfType<SultanCard>())
        {card.UsedThisBattle=false;card.UsedThisTurn=false;card.FailedThisBattle=false;card.LastCountedTurn=0;}
        await EnforceHp();if(Owner.Creature.IsDead)return;
        var ctx=new ThrowingPlayerChoiceContext();
        if(Anger>=1)await PowerCmd.Apply<WeakPower>(ctx,Owner.Creature,1,null,null);
        if(Anger>=3)await PowerCmd.Apply<FrailPower>(ctx,Owner.Creature,1,null,null);
        if(Anger>=9)await Util.LoseHp(ctx,Owner,10);
        if(WeakBattles>0){await PowerCmd.Apply<WeakPower>(ctx,Owner.Creature,5,null,null);WeakBattles=0;}
        if(VulnerableBattles>0){await PowerCmd.Apply<VulnerablePower>(ctx,Owner.Creature,5,null,null);VulnerableBattles=0;}
        if(StrengthPenalty>0)await PowerCmd.Apply<StrengthPower>(ctx,Owner.Creature,-StrengthPenalty,null,null);
        foreach(var enemy in Util.Enemies(Owner).ToArray())
        {
            if(StatsLocked)await PowerCmd.Apply<IntangiblePower>(ctx,enemy,1,null,null);
            if(BoulderBattles>0)await PowerCmd.Apply<SiegeBoulderPower>(ctx,enemy,5,null,null);
        }
        if(BoulderBattles>0)BoulderBattles--;
        if(MirrorHealBattles>0){await PowerCmd.Apply<MirrorHealingPower>(ctx,Owner.Creature,1,null,null);MirrorHealBattles--;}
        if(StrengthLockBattles>0){await PowerCmd.Apply<NoDexterityPower>(ctx,Owner.Creature,1,null,null);StrengthLockBattles--;}
        if(Anger>=5)await CardPileCmd.AddToCombatAndPreview<Wound>(Owner.Creature,PileType.Draw,1,Owner,CardPilePosition.Random);
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx,Player player)
    {
        if(player!=Owner||player.PlayerCombatState?.TurnNumber!=1||Owner.Creature.IsDead)return;
        var choices=PileType.Draw.GetPile(Owner).Cards.OfType<SultanCard>().Cast<CardModel>().ToList();
        if(choices.Count>0)
        {
            var selected=await Util.Choose(Owner,choices,false,ctx,new LocString("card_selection","ARTU_SULTAN_TO_HAND"));
            if(selected!=null){await CardPileCmd.Add(selected,PileType.Hand);if(this is WitchTreasureBox)selected.SetToFreeThisTurn();}
        }
    }
    public override async Task AfterEnergyReset(Player player)
    {
        if(player==Owner&&Anger>=7&&player.PlayerCombatState?.TurnNumber==1)await PlayerCmd.LoseEnergy(1,Owner);
    }
    public override bool ShouldPlay(CardModel card,AutoPlayType type)
    {
        if(card.Owner!=Owner)return true;
        return BeckyController.Active(Owner)||Rules.PrioritizeSultan(Util.Tyranny(Owner),Owner.PlayerCombatState?.Hand.Cards.Any(c=>c is SultanCard)==true,card is SultanCard);
    }
    public override bool TryModifyEnergyCostInCombat(CardModel card,decimal original,out decimal modified)
    {
        modified=original;
        if(card.Owner!=Owner||card is not SultanCard||(!Util.Tyranny(Owner)&&!Owner.Creature.HasPower<DoubleCostPower>()))return false;
        modified=0;return true;
    }
    public override bool TryModifyPowerAmountReceived(PowerModel canonical,Creature target,decimal amount,Creature? applier,out decimal modified)
    {
        modified=amount;
        if(target==Owner.Creature&&StatsLocked&&amount>0&&(canonical is StrengthPower||canonical is DexterityPower)){modified=0;return true;}
        return false;
    }
    public override decimal ModifyGoldGained(Player player,decimal amount)=>player==Owner&&GoldLocked?0:amount;
    public override decimal ModifyMerchantPrice(Player player,MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry entry,decimal cost)
        =>player==Owner&&(ShopPurchases>0||ActiveShopTax)?cost*2:cost;
    public override Task AfterItemPurchased(Player player,MegaCrit.Sts2.Core.Entities.Merchant.MerchantEntry item,int spent)
    {if(player==Owner&&ShopPurchases>0)ShopPurchases--;return Task.CompletedTask;}
    public override Task AfterRoomEntered(AbstractRoom room)
    {if(room is MerchantRoom){ActiveShopTax=ShopVisits>0;ActiveShopBan=NoBuyingShops>0;if(ShopVisits>0)ShopVisits--;if(NoBuyingShops>0)NoBuyingShops--;}else{ActiveShopTax=false;ActiveShopBan=false;}return EnforceHp();}
    public override Task BeforeRoomEntered(AbstractRoom room)
    {
        return Task.CompletedTask;
    }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext ctx,CombatSide side,IEnumerable<Creature> participants)
    {
        if(side!=CombatSide.Player||!participants.Contains(Owner.Creature)||Owner.Creature.IsDead)return;
        await TickGold(ctx);
    }
    public async Task TickGold(PlayerChoiceContext ctx)
    {
        int turn=Owner.PlayerCombatState?.TurnNumber??1;
        foreach(var card in Owner.Deck.Cards.OfType<SultanCard>().Where(c=>c.Grade==SultanGrade.Gold).ToArray())
        {
            if(V04.Data(Owner).Grace||card.LastCountedTurn==turn)continue;
            card.LastCountedTurn=turn;
            if(card.UsedThisTurn){card.TurnsSinceUse=0;card.UsedThisTurn=false;continue;}
            card.TurnsSinceUse++;
            if(card.TurnsSinceUse>=Rules.GoldTurnDeadline){await Expire(ctx,card);card.TurnsSinceUse=0;card.FailedThisBattle=true;}
        }
    }
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if(Owner.Creature.IsDead)return;
        if(V04.Data(Owner).SettlementProcessed)return;
        V04.Data(Owner).SettlementProcessed=true;
        BattleWins++;
        var ctx=new ThrowingPlayerChoiceContext();
        await TickGold(ctx);
        foreach(var card in Owner.Deck.Cards.OfType<SultanCard>().ToArray())
        {
            if(V04.Data(Owner).Grace)continue;
            if(card.UsedThisBattle)card.BattlesRemaining=Rules.BattleDeadline(card.Grade);
            else if(!card.FailedThisBattle&&--card.BattlesRemaining<=0)await Expire(ctx,card);
            if(Owner.Creature.IsDead)return;
        }
        if(GoldLost>20&&Refund>0){int before=Owner.Gold;await PlayerCmd.GainGold(Refund,Owner);GD.Print($"[Artu refund] spent={GoldLost} refund={Refund} balance={before}->{Owner.Gold}");}
        if(TreasuryRate>0)await PlayerCmd.GainGold(TreasuryRate*SultanUsage.UsedKinds(Owner).Count,Owner);
        await V04.Victory(Owner,ctx);
        await Thoughts.Resolve(Owner,this,ctx);
    }
    public async Task Expire(PlayerChoiceContext ctx,SultanCard card)
    {
        Anger++;card.BattlesRemaining=Rules.BattleDeadline(card.Grade);
        await EnforceHp();if(Owner.Creature.IsDead)return;
        switch(card.Suit)
        {
            case SultanSuit.Slaughter:
                if(card.Grade==SultanGrade.Gold){MaxHpLimit=Math.Max(1,Owner.Creature.MaxHp/2);await CreatureCmd.SetMaxHp(Owner.Creature,MaxHpLimit);GrowthLocked=true;}
                else await Util.LoseHp(ctx,Owner,new[]{10,20,30}[(int)card.Grade]);break;
            case SultanSuit.Conquest:
                if(card.Grade==SultanGrade.Stone)WeakBattles=1;
                if(card.Grade==SultanGrade.Bronze)StrengthPenalty++;
                // Confirmed silver expiry: three subsequent battles deny Dexterity.
                if(card.Grade==SultanGrade.Silver)StrengthLockBattles=Math.Max(StrengthLockBattles,3);
                if(card.Grade==SultanGrade.Gold)StatsLocked=true;break;
            case SultanSuit.Luxury:
                if(card.Grade==SultanGrade.Stone)ShopPurchases++;
                if(card.Grade==SultanGrade.Bronze)ShopVisits=Math.Max(ShopVisits,1);
                // Confirmed silver luxury expiry: next shop forbids purchases.
                if(card.Grade==SultanGrade.Silver)NoBuyingShops=Math.Max(NoBuyingShops,1);
                if(card.Grade==SultanGrade.Gold)GoldLocked=true;break;
            case SultanSuit.Lust:
                if(card.Grade==SultanGrade.Stone)VulnerableBattles=1;
                if(card.Grade==SultanGrade.Bronze)BoulderBattles=Math.Max(BoulderBattles,3);
                // Confirmed silver lust expiry: three subsequent battles mirror healing.
                if(card.Grade==SultanGrade.Silver)MirrorHealBattles=Math.Max(MirrorHealBattles,3);
                if(card.Grade==SultanGrade.Gold)HealingLocked=true;break;
        }
        await EnforceHp();
    }
    public override bool TryModifyRestSiteOptions(Player player,ICollection<MegaCrit.Sts2.Core.Entities.RestSite.RestSiteOption> options)
    {if(player!=Owner)return false;options.Add(new ExchangeSultanOption(player));if(Crime>0)options.Add(new ClearCrimeOption(player));return true;}
    public override bool TryModifyRewards(Player player,List<MegaCrit.Sts2.Core.Rewards.Reward> rewards,AbstractRoom? room)
        =>player==Owner&&room is CombatRoom&&EvolutionRewards.Add(player,rewards);
}
public sealed class WitchTreasureBox:WitchCardBox
{
    public override RelicRarity Rarity=>RelicRarity.Ancient;
    public override List<(string,string)> Localization=>new RelicLoc("女术士的宝盒","每场战斗开始时选择一张苏丹牌加入手牌，本回合0费。休息处换牌时，从三张随机高一级苏丹牌中选一张。","皇命不曾远去。");
    public override RelicModel? GetUpgradeReplacement()=>null;
}
[Pool(typeof(ArtuRelicPool))]
public sealed class TyrantsAccomplice:CustomRelicModel
{
    public override string PackedIconPath=>"res://ArtuMod/images/tyrantsaccomplice.tres";
    protected override string BigIconPath=>PackedIconPath;
    public override RelicRarity Rarity=>RelicRarity.Event;
    public override List<(string,string)> Localization=>new RelicLoc("为虎作伥","苏丹牌0费，使用后抽1张。有苏丹牌在手时，必须先使用苏丹牌，且不能主动结束回合。无法支付的代价由1层苏丹的嘲弄替代。嘲弄每层锁住10%可存活生命；10层永久死亡。","替苏丹挥刀的人，也被握在掌中。");
}
public sealed class ClearCrimeOption(Player owner):CustomRestSiteOption(owner)
{
    public override string OptionId=>"ARTU_CLEAR_CRIME";
    public override string CustomIconPath=>"res://ArtuMod/images/status/crimepower.tres";
    public override bool IsEnabled=>(Util.State(Owner)?.Crime??0)>0;
    public override async Task<bool> OnSelect()
    {
        var state=Util.State(Owner);if(state==null||state.Crime<=0)return false;
        int before=state.Crime;
        await V04.RemoveCrime(new ThrowingPlayerChoiceContext(),Owner,1);
        state.Crime=before-1;
        GD.Print($"[Artu rest crime] {before}->{state.Crime}");
        return true; // Native rest UI spends the normal rest-site action.
    }
}
public sealed class ExchangeSultanOption(Player owner):CustomRestSiteOption(owner)
{
    public override string OptionId=>"ARTU_EXCHANGE";
    public override string CustomIconPath=>"res://images/ui/rest_site/option_smith.png";
    public override bool IsEnabled=>Owner.Deck.Cards.OfType<SultanCard>().Any(c=>c.Grade<SultanGrade.Gold);
    public override async Task<bool> OnSelect()
    {
        var selected=await CardSelectCmd.FromDeckGeneric(Owner,new CardSelectorPrefs(new LocString("rest_site_ui","OPTION_ARTU_EXCHANGE.description"),1),c=>c is SultanCard s&&s.Grade<SultanGrade.Gold);
        if(selected.FirstOrDefault() is not SultanCard original)return false;
        var all=Enum.GetValues<SultanSuit>().Select(s=>SultanCard.Get(s,original.Grade+1)).ToList();
        SultanCard chosen;
        if(Util.State(Owner) is WitchTreasureBox)
        {
            var choices=new List<CardModel>();
            for(int i=0;i<3;i++){var item=Owner.PlayerRng.Rewards.NextItem(all)!;all.Remove(item);choices.Add(item);}
            chosen=(SultanCard)(await Util.Choose(Owner,choices))!;
        }
        else chosen=Owner.PlayerRng.Rewards.NextItem(all)!;
        var card=(SultanCard)Owner.RunState.CreateCard(chosen,Owner);
        card.BattlesRemaining=original.BattlesRemaining;card.TurnsSinceUse=original.TurnsSinceUse;
        await Util.RemoveSultan(original);CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card,PileType.Deck));return true;
    }
}
