namespace ArtuMod;
public enum SultanSuit { Slaughter, Conquest, Luxury, Lust }
public enum SultanGrade { Stone, Bronze, Silver, Gold }
public static class Rules
{
    public const int Execution = 10;
    public static int HpCeiling(int maxHp, int mockery) => (int)((long)maxHp * Math.Clamp(10 - mockery, 0, 10) / 10);
    public const int GoldTurnDeadline = 5;
    public static int BattleDeadline(SultanGrade grade) => grade == SultanGrade.Stone ? 5 : grade == SultanGrade.Bronze ? 3 : 1;
    public static bool PrioritizeSultan(bool tyranny, bool hasSultan, bool playingSultan) => !tyranny || !hasSultan || playingSultan;
    public static int SlaughterHpCost(SultanGrade grade) => new[] {1,3,5,8}[(int)grade];
    public static int SlaughterDamage(SultanGrade grade) => new[] {11,18,35,66}[(int)grade];
    public static int LuxuryGoldCost(SultanGrade grade) => new[] {5,0,15,25}[(int)grade];
    public static int AdvancedTroopsFromDamage(int beforeHp, int afterHp) => Math.Max(0, beforeHp - afterHp) / 6;
    public static bool HasAngerEffect(int anger, int threshold) => anger >= threshold;
}
