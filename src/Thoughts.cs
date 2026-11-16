namespace ArtuMod;
public abstract class ThoughtCard(string title,string text,CardRarity rarity=CardRarity.Common):RewardCard(1,CardType.Power,rarity,TargetType.Self,title,"[gold]思潮[/gold]\n"+text)
{public override bool Thought=>true;}
public sealed class PureThought():ThoughtCard("纯洁的思想","获得1层洁念。战后获得等量同名牌；3层时改为移除全部同名牌，获得触碰洁净并可删除最多3张普通牌。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Pure++;return AddPower<PureThoughtPower>(ctx,1);}
}
public sealed class UncleanThought():ThoughtCard("不洁的思绪","获得1层邪念。战后获得等量同名牌；3层时改为移除全部同名牌，获得窥视黑暗并升级3张牌，或将3张苏丹牌升阶。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Unclean++;return AddPower<UncleanThoughtPower>(ctx,1);}
}
public sealed class FearOfSultan():ThoughtCard("对苏丹的恐惧","获得1层畏权。本场实际支付超过3次代价后，战后获得等量同名牌；3层时改为移除同名牌，获得免于恐惧的自由，并移除或升阶一张苏丹牌。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Fear++;return AddPower<FearThoughtPower>(ctx,1);}
}
public sealed class BowToSultan():ThoughtCard("俯首称臣","获得1层臣服。战后获得等量同名牌；3层时改为移除全部俯首称臣，获得为虎作伥。",CardRarity.Rare)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Submission++;return AddPower<SubmissionPower>(ctx,1);}
}
// The user clarified the discount lasts the current turn during this update.
public sealed class TouchPurity():ThoughtCard("触碰洁净","获得1净悟。每回合首张抽到的牌费用-1，仅持续当前回合；战斗效果不随净悟层数叠加。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.PureSecond++;return AddPower<PurityInsightPower>(ctx,1);}
}
public sealed class GlimpseDarkness():ThoughtCard("窥视黑暗","获得1秘悟。每回合首次使用苏丹牌时额外失去2生命并抽2张；战斗效果不随秘悟层数叠加。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.DarkSecond++;return AddPower<SecretInsightPower>(ctx,1);}
}
public sealed class FreedomFromFear():ThoughtCard("免于恐惧的自由","获得1觉醒。每回合首次使用非苏丹牌获得1士兵；若此前本回合未使用苏丹牌，再抽1张。战斗效果不随觉醒层数叠加。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.AwakeSecond++;return AddPower<AwakeningPower>(ctx,1);}
}
public static class Thoughts
{
    public static async Task Resolve(Player p,WitchCardBox state,PlayerChoiceContext ctx)
    {
        await ResolveOne(p,state.Pure,ModelDb.Card<PureThought>(),ModelDb.Card<TouchPurity>(),async ()=>{
            var cards=(await CardSelectCmd.FromDeckForRemoval(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,3),c=>c is not SultanCard)).ToList();
            await CardPileCmd.RemoveFromDeck(cards);
        });
        await ResolveOne(p,state.Unclean,ModelDb.Card<UncleanThought>(),ModelDb.Card<GlimpseDarkness>(),async ()=>{
            var choice=await Util.Choose(p,[ModelDb.Card<UpgradeOrdinaryOption>(),ModelDb.Card<PromoteSultanOption>()]);
            if(choice is UpgradeOrdinaryOption){var cards=await CardSelectCmd.FromDeckForUpgrade(p,new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt,0,3));CardCmd.Upgrade(cards,MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.HorizontalLayout);}
            else await PromoteSelected(p,3);
        });
        if(state.CostsPaid>3)await ResolveOne(p,state.Fear,ModelDb.Card<FearOfSultan>(),ModelDb.Card<FreedomFromFear>(),async ()=>{
            var selected=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,1),c=>c is SultanCard)).FirstOrDefault() as SultanCard;
            if(selected==null)return;
            var choice=await Util.Choose(p,selected.Grade==SultanGrade.Gold?[ModelDb.Card<RemoveOption>()]:[ModelDb.Card<RemoveOption>(),ModelDb.Card<PromoteOption>()]);
            if(choice is PromoteOption)await Promote(p,selected);else await Util.RemoveSultan(selected);
        });
        if(state.Submission>=3)
        {
            await RemoveSame(p,ModelDb.Card<BowToSultan>());
            if(!Util.Tyranny(p))await RelicCmd.Obtain<TyrantsAccomplice>(p);
        }
        else for(int i=0;i<state.Submission;i++)await Util.AddToDeck(p,ModelDb.Card<BowToSultan>());
        await ResolveSecond(p,state,ctx);
        state.Pure=state.Unclean=state.Fear=state.Submission=0;
    }
    public static async Task ResolveSecond(Player p,WitchCardBox state,PlayerChoiceContext ctx)
    {
        if(state.DarkSecond==0)await ResolveOne(p,state.PureSecond,ModelDb.Card<TouchPurity>(),ModelDb.Card<AtonementPrayer>(),()=>RemoveOptionalSultan(p));
        if(state.PureSecond==0)await ResolveOne(p,state.DarkSecond,ModelDb.Card<GlimpseDarkness>(),ModelDb.Card<AncientFaith>(),()=>ChooseReward(p,
            ModelDb.All.OfType<ArtuCard>().Where(c=>c.Reading&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare),ctx));
        if(state.Submission==0)await ResolveOne(p,state.AwakeSecond,ModelDb.Card<FreedomFromFear>(),ModelDb.Card<RebellionIntent>(),async ()=>{
            await RemoveOptionalSultan(p);await ChooseReward(p,ArmyRewards(),ctx);
        });
        state.PureSecond=state.DarkSecond=state.AwakeSecond=0;
    }
    static async Task RemoveOptionalSultan(Player p)
    {var card=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,1),c=>c is SultanCard)).FirstOrDefault();if(card!=null)await Util.RemoveSultan(card);}
    static IEnumerable<CardModel> ArmyRewards()=>new CardModel[]{
        ModelDb.Card<RecruitTroops>(),ModelDb.Card<CriminalRecruits>(),ModelDb.Card<ImperialExpedition>(),ModelDb.Card<BackroomDeal>(),
        ModelDb.Card<Drill>(),ModelDb.Card<Muster>(),ModelDb.Card<TrainArmy>(),ModelDb.Card<EliteTroops>(),ModelDb.Card<Chivalry>(),
        ModelDb.Card<AllArmyAttack>(),ModelDb.Card<MercenaryContract>(),ModelDb.Card<BannerResonance>(),ModelDb.Card<FieldReorganization>(),
        ModelDb.Card<SecretAssembly>(),ModelDb.Card<SoldierCharge>(),ModelDb.Card<ArmyInterchange>(),ModelDb.Card<ArmySpecialization>(),
        ModelDb.Card<SupportFromAllSides>(),ModelDb.Card<WarSong>(),ModelDb.Card<PastGlory>(),ModelDb.Card<CavalryAdvance>(),ModelDb.Card<LivingOffWar>()};
    static async Task ChooseReward(Player p,IEnumerable<CardModel> candidates,PlayerChoiceContext ctx)
    {
        var pool=candidates.DistinctBy(c=>c.Id).ToList();var choices=new List<CardModel>();
        while(pool.Count>0&&choices.Count<3){var card=p.PlayerRng.Rewards.NextItem(pool)!;choices.Add(card);pool.Remove(card);}
        var selected=await Util.Choose(p,choices,false,ctx);if(selected!=null)await Util.AddToDeck(p,selected.CanonicalInstance);
    }
    static async Task ResolveOne(Player p,int amount,CardModel source,CardModel result,Func<Task> reward)
    {
        if(amount<3){for(int i=0;i<amount;i++)await Util.AddToDeck(p,source);return;}
        await RemoveSame(p,source);await Util.AddToDeck(p,result);await reward();
    }
    public static async Task RemoveSame(Player p,CardModel source)=>await CardPileCmd.RemoveFromDeck(p.Deck.Cards.Where(c=>c.Id==source.Id).ToList());
    public static async Task PromoteSelected(Player p,int count)
    {
        var selected=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt,0,count),c=>c is SultanCard s&&s.Grade<SultanGrade.Gold)).OfType<SultanCard>().ToArray();
        foreach(var card in selected)await Promote(p,card);
    }
    public static async Task Promote(Player p,SultanCard card)
    {
        if(card.Grade==SultanGrade.Gold)return;
        var newCard=(SultanCard)p.RunState.CreateCard(SultanCard.Get(card.Suit,card.Grade+1),p);
        newCard.BattlesRemaining=card.BattlesRemaining;newCard.TurnsSinceUse=card.TurnsSinceUse;
        await Util.RemoveSultan(card);CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(newCard,PileType.Deck));
    }
}
public sealed class PureThoughtPower():ArtuPower("洁念","战后获得{Amount}张纯洁的思想；达到3层则形成触碰洁净。");
public sealed class UncleanThoughtPower():ArtuPower("邪念","战后获得{Amount}张不洁的思绪；达到3层则形成窥视黑暗。");
public sealed class FearThoughtPower():ArtuPower("畏权","本场实际支付超过3次代价后，战后根据层数形成免于恐惧的自由。");
public sealed class SubmissionPower():ArtuPower("臣服","战后获得{Amount}张俯首称臣；达到3层则获得为虎作伥。");
