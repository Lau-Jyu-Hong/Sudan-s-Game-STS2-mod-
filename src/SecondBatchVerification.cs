using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
namespace ArtuMod;
public static class SecondBatchVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var checks=new List<string>();
        void Check(string n,bool ok){if(!ok)throw new Exception(n);checks.Add("second batch "+n);}
        using var selector=CardSelectorStack.SetTestSelector(new First());
        try
        {
            foreach(var type in new[]{typeof(AtonementPrayer),typeof(AncientFaith),typeof(RebellionIntent),typeof(PerfectWorld),typeof(NewWorld),typeof(RegicidePlan)})
                Check("six thoughts cost one "+type.Name,ModelDb.GetById<CardModel>(ModelDb.GetId(type)).EnergyCost.Canonical==1);
            foreach(var s in ModelDb.All.OfType<SultanCard>())
            {
                bool targeted=s.Suit!=SultanSuit.Luxury&&!(s.Suit==SultanSuit.Conquest&&s.Grade==SultanGrade.Bronze)&&!(s.Suit==SultanSuit.Lust&&s.Grade==SultanGrade.Gold);
                Check("Sultan target audit "+s.Id,s.TargetType==(targeted?TargetType.AnyEnemy:TargetType.Self));
                Check("Sultan individual expiry text "+s.Id,s.ExpiryText().Length>5);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<BronzeConquest>();Check("bronze conquest no target works",f.Player.Creature.GetPowerAmount<StrengthPower>()==2&&f.Player.Creature.GetPowerAmount<DexterityPower>()==-1);
                var a=f.AddDeck<StoneLust>();var b=f.AddDeck<StoneLust>();a.BattlesRemaining=1;b.BattlesRemaining=2;
                Check("same names retain independent UI deadlines",a.DeadlineText().Contains("剩余1场")&&b.DeadlineText().Contains("剩余2场"));
                var copy=(SultanCard)f.Combat.CloneCard(a);copy.DeckVersion=a;
                Check("combat clone UI uses original deadline",copy.GetDescriptionForPile(PileType.Hand).Contains("剩余1场"));
                f.Play<AllByKingsGrace>();Check("frozen deadline UI",copy.DeadlineText().Contains("冻结")&&copy.GetDescriptionForPile(PileType.Hand).Contains("冻结中"));
                Check("box no global progress hover",f.State.HoverTips.OfType<HoverTip>().All(t=>t.Title is not "虔信" and not "苏丹之怒" and not "谋逆"));
            }
            foreach(bool upgrade in new[]{false,true})using(var f=new V04Verification.Fixture())
            {
                var criminals=f.Card<CriminalRecruits>();if(upgrade)CardCmd.Upgrade(criminals);f.Play(criminals);
                Check("criminal recruits 7/10 and one crime "+upgrade,f.Player.Creature.GetPowerAmount<LevyPower>()==(upgrade?10:7)&&f.State.Crime==1);
            }
            foreach(bool upgrade in new[]{false,true})using(var f=new V04Verification.Fixture())
            {
                var c=f.Card<TemporaryRecruitment>();if(upgrade)CardCmd.Upgrade(c);f.Play(c);int initial=upgrade?2:1,limit=upgrade?5:3;
                Check("recruitment initial "+upgrade,f.Player.Creature.GetPowerAmount<LevyPower>()==initial);
                int converted=0;for(int i=0;i<12;i++)converted+=V04.Convert(f.Context,f.Player,1,3).GetAwaiter().GetResult();
                Check("refill finite and cap exact "+upgrade,converted==initial+limit&&f.State.LeviesConverted==converted);
                Check("gain cannot recursively count conversions",f.Player.Creature.GetPowerAmount<ElitePower>()==converted&&f.Player.Creature.GetPowerAmount<LevyPower>()==0);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<TemporaryRecruitment>();var up=f.Card<TemporaryRecruitment>();CardCmd.Upgrade(up);f.Play(up);
                Check("two independent temporary caps",f.Player.Creature.Powers.OfType<TemporaryRecruitmentPower>().Count()==2);
                int converted=0;for(int i=0;i<25;i++)converted+=V04.Convert(f.Context,f.Player,1,1).GetAwaiter().GetResult();
                Check("two caps replenish 3+5 not infinite",converted==11&&f.State.LeviesConverted==11);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<TemporaryRecruitment>();var power=f.Player.Creature.GetPower<TemporaryRecruitmentPower>()!;
                f.Await(power.AfterSideTurnEnd(f.Context,CombatSide.Player,[f.Player.Creature]));f.Await(V04.Convert(f.Context,f.Player,1,1));
                Check("temporary end removes cap not initial levy",f.Player.Creature.GetPowerAmount<LevyPower>()==0&&f.State.LeviesConverted==1);
                f.Play<RecruitmentNotice>();f.Await(f.Player.Creature.GetPower<RecruitmentNoticePower>()!.AfterPlayerTurnStart(f.Context,f.Player));
                Check("notice grants two next turn",f.Player.Creature.GetPowerAmount<LevyPower>()==2);
                var notice=f.Card<RecruitmentNotice>();CardCmd.Upgrade(notice);Check("notice upgraded costs1 only",notice.EnergyCost.GetWithModifiers(CostModifiers.All)==1);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Gain(0,3);f.Apply<CrimePower>(2);var c=f.AddHand<BurnTheBoats>();CardCmd.Upgrade(c);c.UpdateDynamicVarPreview(CardPreviewMode.Normal,null,c.DynamicVars);
                Check("boats base stays 8/2, crime shows7/1",c.DynamicVars.Damage.BaseValue==8&&c.DynamicVars.Block.BaseValue==2&&c.DynamicVars.Damage.PreviewValue==7&&c.DynamicVars.Block.PreviewValue==1);
                Check("boats total shows three and21/3",c.GetDescriptionForPile(PileType.Hand).Contains("可消耗3层")&&c.DamageEstimate(3)=="21");
                int hp=f.Enemy.CurrentHp;f.Play(c);Check("boats damage matches preview total",hp-f.Enemy.CurrentHp==21&&f.Player.Creature.Block==3);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Gain(0,3);f.Apply<VigorPower>(5);var c=f.AddHand<BurnTheBoats>();c.UpdateDynamicVarPreview(CardPreviewMode.Normal,null,c.DynamicVars);
                string preview=c.DamageEstimate(3);int hp=f.Enemy.CurrentHp;f.Play(c);
                Check("boats vigor applies to only first independent hit",preview==(hp-f.Enemy.CurrentHp).ToString());
            }
            Check("secret insight text explicit permanent choose one",ArtuTerms.Rules["秘悟"].Contains("3张")&&ArtuTerms.Rules["秘悟"].Contains("选1张永久"));
        }
        finally{TestMode.IsOn=false;}return checks;
    }
    sealed class First:ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards,int min,int max)=>Task.FromResult(cards.Take(min));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards,IReadOnlyList<CardRewardAlternative> alternatives)=>default;
    }
}
