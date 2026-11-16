using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

public static class DefenseSupplementVerification
{
    public static List<string> Run()
    {
        TestMode.IsOn=true;
        var checks=new List<string>();
        void Check(string name,bool ok){if(!ok)throw new Exception(name);checks.Add("defense supplement "+name);}
        try
        {
            using(var f=new V04Verification.Fixture())
            {
                var pool=V04.RewardCards(f.Player).ToArray();
                Check("both cards are common ordinary rewards",pool.Any(c=>c is BlockReprisal&&c.Rarity==CardRarity.Common)&&pool.Any(c=>c is DestructiveStrike&&c.Rarity==CardRarity.Common));
                Check("reprisal costs 1, strike costs 2",f.Card<BlockReprisal>().EnergyCost.Canonical==1&&f.Card<DestructiveStrike>().EnergyCost.Canonical==2);
                var second=f.Combat.CreateCreature(ModelDb.Monster<BigDummy>().ToMutable(),CombatSide.Enemy,null);f.Combat.AddCreature(second);
                f.Play<BlockReprisal>();
                Check("base reprisal gives 9 block and arms once",f.Player.Creature.Block==9&&f.Player.Creature.GetPowerAmount<BlockReprisalPower>()==1);
                f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,4,null));
                Check("partial loss does not trigger",f.Player.Creature.Block==5&&f.Enemy.GetPowerAmount<WeakPower>()==0&&f.Player.Creature.HasPower<BlockReprisalPower>());
                f.Await(CreatureCmd.GainBlock(f.Player.Creature,4,ValueProp.Unpowered,null));
                f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,5,null));
                Check("tracks total block, not the original nine",f.Player.Creature.Block==4&&f.Enemy.GetPowerAmount<WeakPower>()==0);
                f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,4,null));
                Check("active removal to zero weakens all enemies once",f.Enemy.GetPowerAmount<WeakPower>()==1&&second.GetPowerAmount<WeakPower>()==1&&!f.Player.Creature.HasPower<BlockReprisalPower>());
                f.Await(CreatureCmd.GainBlock(f.Player.Creature,5,ValueProp.Unpowered,null));f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,5,null));
                Check("later zero does not repeat",f.Enemy.GetPowerAmount<WeakPower>()==1&&second.GetPowerAmount<WeakPower>()==1);
            }
            using(var f=new V04Verification.Fixture())
            {
                var c=f.Card<BlockReprisal>();CardCmd.Upgrade(c);f.Play(c);
                Check("upgraded reprisal gives 12 block",f.Player.Creature.Block==12);
                f.Await(CreatureCmd.Damage(f.Context,f.Player.Creature,12,ValueProp.Unpowered,f.Enemy));
                Check("damage breaking block triggers",f.Player.Creature.Block==0&&f.Enemy.GetPowerAmount<WeakPower>()==1&&!f.Player.Creature.HasPower<BlockReprisalPower>());
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<BlockReprisal>();f.NextTurn();
                f.Await(f.Player.Creature.AfterTurnStart(CombatSide.Player));
                f.Await(Hook.AfterBlockCleared(f.Combat,f.Player.Creature));
                Check("natural turn-start clearing triggers",f.Player.Creature.Block==0&&f.Enemy.GetPowerAmount<WeakPower>()==1&&!f.Player.Creature.HasPower<BlockReprisalPower>());
                f.Await(Hook.AfterBlockCleared(f.Combat,f.Player.Creature));
                Check("repeated clear hook does not repeat",f.Enemy.GetPowerAmount<WeakPower>()==1);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<BlockReprisal>();f.Apply<BarricadePower>(1);f.NextTurn();
                f.Await(f.Player.Creature.AfterTurnStart(CombatSide.Player));f.Await(Hook.AfterBlockCleared(f.Combat,f.Player.Creature));
                Check("retained positive block does not trigger",f.Player.Creature.Block==9&&f.Enemy.GetPowerAmount<WeakPower>()==0&&f.Player.Creature.HasPower<BlockReprisalPower>());
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<BlockReprisal>();f.Play<StoneConquest>();
                Check("rock conquest removal adds reprisal to normal weak",f.Player.Creature.Block==0&&f.Enemy.GetPowerAmount<WeakPower>()==2&&f.Enemy.GetPowerAmount<VulnerablePower>()==1);
            }
            using(var f=new V04Verification.Fixture())
            {
                f.Play<BlockReprisal>();f.Play<BlockReprisal>();
                Check("two uses stack two pending reprisals",f.Player.Creature.GetPowerAmount<BlockReprisalPower>()==2);
                f.Await(CreatureCmd.LoseBlock(f.Context,f.Player.Creature,f.Player.Creature.Block,null));
                Check("two pending uses each trigger once",f.Enemy.GetPowerAmount<WeakPower>()==2&&!f.Player.Creature.HasPower<BlockReprisalPower>());
            }
            using(var f=new V04Verification.Fixture())
            {
                int hp=f.Enemy.CurrentHp;f.Play<DestructiveStrike>();
                Check("base strike deals 10 before applying one vulnerable",hp-f.Enemy.CurrentHp==10&&f.Enemy.GetPowerAmount<VulnerablePower>()==1);
                f.Await(PowerCmd.Remove<VulnerablePower>(f.Enemy));var c=f.Card<DestructiveStrike>();CardCmd.Upgrade(c);hp=f.Enemy.CurrentHp;f.Play(c);
                Check("upgrade keeps 10 damage and applies two vulnerable",hp-f.Enemy.CurrentHp==10&&f.Enemy.GetPowerAmount<VulnerablePower>()==2&&c.EnergyCost.GetWithModifiers(CostModifiers.All)==2);
                hp=f.Enemy.CurrentHp;f.Play<DestructiveStrike>();
                Check("strike actual damage benefits from existing vulnerable",hp-f.Enemy.CurrentHp==15);
                var preview=f.AddHand<DestructiveStrike>();preview.UpdateDynamicVarPreview(CardPreviewMode.Normal,f.Enemy,preview.DynamicVars);
                Check("targeted damage preview benefits from vulnerable",preview.DynamicVars.Damage.PreviewValue==15);
            }
        }
        finally{TestMode.IsOn=false;}
        return checks;
    }
}
