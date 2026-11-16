namespace ArtuMod;
public sealed class EnemyChoiceCard:OptionCard
{
    public Creature? Target {get;}
    public EnemyChoiceCard():base("审判对象","选择本回合接受审判的敌人。"){}
    public EnemyChoiceCard(Creature target):this(){Target=target;}
    public override string Title=>Target?.Monster?.Title.GetFormattedText()??"审判对象";
}
