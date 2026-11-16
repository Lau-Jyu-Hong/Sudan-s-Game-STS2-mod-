using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;
public static class RepairVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;var checks=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("repair "+name);}
        try
        {
            for(int kind=0;kind<4;kind++)using(var f=new V04Verification.Fixture())
            {
                f.Gain(kind,1);f.Await(CreatureCmd.GainBlock(f.Enemy,20,ValueProp.Unpowered,null));int hp=f.Enemy.CurrentHp;
                foreach(var army in ArmyPower.All(f.Player).ToArray())f.Await(army.BeforeSideTurnEnd(f.Context,CombatSide.Player,new[]{f.Player.Creature}));
                GD.Print($"[Artu army block diagnostic] kind={kind} HP={hp}->{f.Enemy.CurrentHp} block=20->{f.Enemy.Block} before enemy turn");
                Check($"army {kind} damages enemy block at player end before enemy turn",f.Enemy.CurrentHp==hp&&f.Enemy.Block==20-(kind==3?4:2));
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Await(PlayerCmd.GainGold(100,f.Player));f.Play<ReturnOnSpending>();f.Await(PlayerCmd.LoseGold(21,f.Player));
                Check("all-source gold loss tracks actual 21",f.State.GoldLost==21);
                var power=f.Player.Creature.GetPower<SpendingRefundPower>()!;
                Check("refund power displays spent and remaining",power.DynamicVars["Spent"].IntValue==21&&power.DynamicVars["Needed"].IntValue==0);
            }
            foreach(int loss in new[]{20,21})foreach(bool upgrade in new[]{false,true})using(var f=new V04Verification.Fixture())
            {
                f.Await(PlayerCmd.GainGold(100,f.Player));var card=f.Card<ReturnOnSpending>();if(upgrade)CardCmd.Upgrade(card);f.Play(card);
                f.Await(PlayerCmd.LoseGold(loss,f.Player));int gold=f.Player.Gold;
                f.Await(f.State.AfterCombatVictory(new CombatRoom(f.Combat)));
                Check($"refund threshold loss={loss} upgrade={upgrade}",f.Player.Gold-gold==(loss==21?(upgrade?30:20):0));
                int after=f.Player.Gold;f.Await(f.State.AfterCombatVictory(new CombatRoom(f.Combat)));
                Check("duplicate victory callback cannot refund twice",f.Player.Gold==after);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Await(PlayerCmd.GainGold(100,f.Player));f.Await(PlayerCmd.LoseGold(21,f.Player));int gold=f.Player.Gold;
                f.Await(f.State.AfterCombatVictory(new CombatRoom(f.Combat)));Check("no card means no refund",f.Player.Gold==gold);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<ReturnOnSpending>();f.Play<ReturnOnSpending>();f.Await(PlayerCmd.GainGold(100,f.Player));f.Play<StoneLuxury>();
                Check("manual Sultan spend no longer double-counts",f.State.GoldLost==5&&f.State.Refund==40);
                f.Await(PlayerCmd.LoseGold(16,f.Player));int gold=f.Player.Gold;f.Await(f.State.AfterCombatVictory(new CombatRoom(f.Combat)));
                Check("legacy additive refund stacking preserved",f.Player.Gold-gold==40);
            }
            foreach(bool upgraded in new[]{false,true})using(var f=new V04Verification.Fixture())
            {
                var card=f.AddHand<UnderKingsOrder>();if(upgraded)CardCmd.Upgrade(card);
                for(int count=0;count<3;count++)
                {
                    card.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,card.DynamicVars);
                    int expected=8+count*(upgraded?4:3);
                    Check($"king damage preview base/upgraded count={count}",card.DynamicVars.Damage.PreviewValue==expected);
                    int hp=f.Enemy.CurrentHp;f.Play(card);Check("king damage execution matches preview",hp-f.Enemy.CurrentHp==expected);
                    f.Await(PaymentLedger.Record(f.Context,f.Player));
                }
                f.NextTurn();card.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,card.DynamicVars);
                Check("king turn reset returns to eight",card.DynamicVars.Damage.PreviewValue==8);
                f.Await(PaymentLedger.Record(f.Context,f.Player,false));Check("replacement excluded from original-cost damage",PaymentLedger.ThisTurn(f.Player)==0);
            }
            using(var f=new V04Verification.Fixture())
            {
                var card=f.Card<Quickfoot>();int hp=f.Enemy.CurrentHp;f.Play(card);Check("quickfoot base 3 damage and block",hp-f.Enemy.CurrentHp==3&&f.Player.Creature.Block==3);
                CardCmd.Upgrade(card);hp=f.Enemy.CurrentHp;f.Play(card);Check("quickfoot upgraded 5 damage and block",hp-f.Enemy.CurrentHp==5&&f.Player.Creature.Block==8);
                f.Apply<CrimePower>(1);f.Play<AtonementPrayer>();f.Apply<CrimePower>(1);
                f.Await(V04.Victory(f.Player,f.Context));Check("devotion checked before natural crime decay",f.State.Devotion==0&&f.State.Crime==0);
                f.Await(V04.Victory(f.Player,f.Context));Check("victory progression not repeated",f.State.Devotion==0);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Apply<CrimePower>(3);f.Await(V04.Victory(f.Player,f.Context));Check("victory removes exactly one saved crime",f.State.Crime==2&&f.Player.Creature.GetPowerAmount<CrimePower>()==2);
                var saved=(WitchCardBox)RelicModel.FromSerializable(f.State.ToSerializable());Check("crime decay roundtrips save",saved.Crime==2);
            }
            foreach(var model in ModelDb.All.OfType<ArtuCard>())
            {
                string text=string.Join("",(model.Localization??[]).Select(x=>x.Item2));
                Check("no design upgrade text "+model.Id,!text.Contains("升级：")&&!text.Contains("升级后")&&!text.Contains("（升级"));
                if(model.CanonicalKeywords.Contains(CardKeyword.Exhaust))Check("no duplicate self-exhaust prose "+model.Id,!text.EndsWith("消耗。"));
            }
            using(var f=new V04Verification.Fixture())
            {
                var card=f.Card<ArmySpecialization>();string key=card.Id.Entry+".description";
                Check("specialization base does not promise extra army",!card.GetDescriptionForPile(PileType.Hand).Contains("另获得"));
                CardCmd.Upgrade(card);Check("specialization upgrade promises actual extra army",card.GetDescriptionForPile(PileType.Hand).Contains("另获得1层"));
                Check("purity supplies native named hover tip",f.Card<TouchPurity>().HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().Any(t=>t.Title=="净悟"));
                Check("evolution supplies named progress hover tip",f.Card<AtonementPrayer>().HoverTips.OfType<MegaCrit.Sts2.Core.HoverTips.HoverTip>().Any(t=>t.Title=="虔信"&&t.Description.Contains("0/3")));
                f.State.Mockery=2;Check("mockery tooltip uses actual HP ceiling",ArtuTerms.Detail("苏丹的嘲弄",f.Player).Contains("1600"));
                Check("native text-sized energy resource exists",ResourceLoader.Exists(ModelDb.CardPool<ArtuCardPool>().TextEnergyIconPath));
            }
            var hand="ABCDEFGH".Select(c=>c.ToString()).ToList();var order=new List<string>();
            BeckyController.Scan(()=>hand.Count,i=>{order.Add(hand[i]);hand.RemoveAt(i);return Task.CompletedTask;}).GetAwaiter().GetResult();
            Check("eight-card Becky scan is ADGCH",string.Concat(order)=="ADGCH");
        }
        finally{TestMode.IsOn=false;}
        return checks;
    }
}
