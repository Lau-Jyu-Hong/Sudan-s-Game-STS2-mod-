namespace ArtuMod;
public abstract class ThoughtCard(string title,string text,CardRarity rarity=CardRarity.Common):RewardCard(1,CardType.Power,rarity,TargetType.Self,title,"[gold]思潮[/gold]\n"+text)
{public override bool Thought=>true;}
public sealed class PureThought():ThoughtCard("纯洁的思想","获得1层洁念。战后获得等量同名牌；3层时取得进化资格。战斗奖励可选择保留，或移除全部同名牌、获得触碰洁净并可删除最多3张普通牌。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Pure++;return AddPower<PureThoughtPower>(ctx,1);}
}
public sealed class UncleanThought():ThoughtCard("不洁的思绪","获得1层邪念。战后获得等量同名牌；3层时取得进化资格。战斗奖励可选择保留，或移除全部同名牌、获得窥视黑暗并升级3张牌，或将3张苏丹牌升阶。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Unclean++;return AddPower<UncleanThoughtPower>(ctx,1);}
}
public sealed class FearOfSultan():ThoughtCard("对苏丹的恐惧","获得1层畏权。本场实际支付超过3次代价后，战后获得等量同名牌；3层时取得进化资格。奖励可选择保留，或移除同名牌、获得免于恐惧的自由，并移除或升阶一张苏丹牌。")
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Fear++;return AddPower<FearThoughtPower>(ctx,1);}
}
public sealed class BowToSultan():ThoughtCard("俯首称臣","获得1层臣服。战后获得等量同名牌；3层时改为移除全部俯首称臣，获得为虎作伥。",CardRarity.Rare)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.Submission++;return AddPower<SubmissionPower>(ctx,1);}
}
// The user clarified the discount lasts the current turn during this update.
public sealed class TouchPurity():ThoughtCard("触碰洁净","获得1净悟。每回合首张抽到的牌费用-1，仅持续当前回合；战斗效果不叠加。胜利时若无秘悟：1～2层复制同名牌，3层取得永久进化资格，之后可在奖励中选择保留或进化。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.PureSecond++;return AddPower<PurityInsightPower>(ctx,1);}
}
public sealed class GlimpseDarkness():ThoughtCard("窥视黑暗","获得1秘悟。每回合首次使用苏丹牌时额外失去2生命并抽2张；战斗效果不叠加。胜利时若无净悟：1～2层复制同名牌，3层取得永久进化资格，之后可在奖励中选择保留或进化。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.DarkSecond++;return AddPower<SecretInsightPower>(ctx,1);}
}
public sealed class FreedomFromFear():ThoughtCard("免于恐惧的自由","获得1觉醒。每回合首次使用非苏丹牌获得1士兵；此前本回合未用苏丹牌则再抽1张。战斗效果不叠加。胜利时若无臣服：1～2层复制同名牌，3层取得永久进化资格，之后可在奖励中选择保留或进化。",CardRarity.Uncommon)
{
    protected override Task OnPlay(PlayerChoiceContext ctx,CardPlay play){if(Util.State(Owner) is {} s)s.AwakeSecond++;return AddPower<AwakeningPower>(ctx,1);}
}
public static class Thoughts
{
    public static async Task Resolve(Player p,WitchCardBox state,PlayerChoiceContext ctx)
    {
        await ResolveOne(p,state,0,state.Pure,ModelDb.Card<PureThought>());
        await ResolveOne(p,state,1,state.Unclean,ModelDb.Card<UncleanThought>());
        if(state.CostsPaid>3)await ResolveOne(p,state,2,state.Fear,ModelDb.Card<FearOfSultan>());
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
        if(state.DarkSecond==0)await ResolveOne(p,state,3,state.PureSecond,ModelDb.Card<TouchPurity>());
        if(state.PureSecond==0)await ResolveOne(p,state,4,state.DarkSecond,ModelDb.Card<GlimpseDarkness>());
        if(state.Submission==0)await ResolveOne(p,state,5,state.AwakeSecond,ModelDb.Card<FreedomFromFear>());
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
    static async Task ResolveOne(Player p,WitchCardBox state,int stage,int amount,CardModel source)
    {
        if(EvolutionRewards.Qualified(state,stage))return; // A retained qualification cannot farm old copy rewards.
        if(amount<3){for(int i=0;i<amount;i++)await Util.AddToDeck(p,source);return;}
        EvolutionRewards.Qualify(state,stage);
    }
    public static async Task EvolutionBonus(Player p,int stage,PlayerChoiceContext ctx)
    {
        switch(stage)
        {
            case 0:
                var cards=await CardSelectCmd.FromDeckForRemoval(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,3),c=>c is not SultanCard);
                await CardPileCmd.RemoveFromDeck(cards.ToList());break;
            case 1:
                var choice=await Util.Choose(p,[ModelDb.Card<UpgradeOrdinaryOption>(),ModelDb.Card<PromoteSultanOption>()],false,ctx);
                if(choice is UpgradeOrdinaryOption){var upgrades=await CardSelectCmd.FromDeckForUpgrade(p,new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt,0,3));CardCmd.Upgrade(upgrades,MegaCrit.Sts2.Core.Nodes.CommonUi.CardPreviewStyle.HorizontalLayout);}
                else await PromoteSelected(p,3);break;
            case 2:
                var selected=(await CardSelectCmd.FromDeckGeneric(p,new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt,0,1),c=>c is SultanCard)).FirstOrDefault() as SultanCard;
                if(selected==null)break;
                var action=await Util.Choose(p,selected.Grade==SultanGrade.Gold?[ModelDb.Card<RemoveOption>()]:[ModelDb.Card<RemoveOption>(),ModelDb.Card<PromoteOption>()],false,ctx);
                if(action is PromoteOption)await Promote(p,selected);else await Util.RemoveSultan(selected);break;
            case 3: await RemoveOptionalSultan(p);break;
            case 4: await ChooseReward(p,ModelDb.All.OfType<ArtuCard>().Where(c=>c.Reading&&c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare),ctx);break;
            case 5: await RemoveOptionalSultan(p);await ChooseReward(p,ArmyRewards(),ctx);break;
        }
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
public sealed class PureThoughtPower():ArtuPower("洁念","胜利时：1～2层复制等量纯洁的思想；达到3层取得永久进化资格，可在战斗奖励选择保留或进化。");
public sealed class UncleanThoughtPower():ArtuPower("邪念","胜利时：1～2层复制等量不洁的思绪；达到3层取得永久进化资格，可在战斗奖励选择保留或进化。");
public sealed class FearThoughtPower():ArtuPower("畏权","本场实际支付超过3次代价且获胜：1～2层复制同名牌；达到3层取得永久进化资格，可在战斗奖励选择保留或进化。");
public sealed class SubmissionPower():ArtuPower("臣服","战后获得{Amount}张俯首称臣；达到3层则获得为虎作伥。");
