using System.Reflection;
namespace ArtuMod;
[HarmonyPatch(typeof(ModelDb),nameof(ModelDb.InitIds))]
public static class RegisterArtuUi
{
    [HarmonyPostfix,HarmonyPriority(Priority.Last)]
    static void Postfix()
    {
        ArtuTerms.Register();
        var dict=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("rest_site_ui"))!;
        dict["OPTION_ARTU_EXCHANGE.name"]="换牌";
        dict["OPTION_ARTU_EXCHANGE.description"]="选择一张非黄金苏丹牌，换为随机的高一级苏丹牌；继承剩余期限。";
        dict["OPTION_ARTU_CLEAR_CRIME.name"]="销毁罪证";
        dict["OPTION_ARTU_CLEAR_CRIME.description"]="移除1层罪证。消耗本次休息处行动，不回复生命。";
        dict["OPTION_ARTU_REMOVE_SULTAN.name"]="销毁苏丹牌";
        dict["OPTION_ARTU_REMOVE_SULTAN.description"]="永久移除1张苏丹牌。消耗本次休息处行动，不回复生命。";
        HextechBridge.TryRegister();
        var selection=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("card_selection"))!;
        selection["ARTU_CHOOSE"]="选择一张牌。";
        selection["ARTU_SULTAN_TO_HAND"]="选择一张苏丹牌加入手牌。";
        var gameplay=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("gameplay_ui"))!;
        gameplay["ARTU_RETAIN"]="保留当前思潮：{Thought}（保留进化资格）";
        gameplay["ARTU_EVOLVE"]="完成思潮进化：{Thought} → {Next}";
        gameplay["ARTU_FORGIVE"]="请求宽恕：免费永久清除1层苏丹之怒";
        var cards=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("cards"))!;
        foreach(var model in ModelDb.All.OfType<ArtuCard>())cards[model.Id.Entry+".selectionScreenPrompt"]="选择卡牌。";
        var badges=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("badges"))!;
        badges["ARTUMOD_REVOLUTION_SUCCESS.title"]="革命成功";
        badges["ARTUMOD_REVOLUTION_SUCCESS.description"]="通过革命的计划赢得本局游戏。";
        EventText.Inject();
        var testOutput=System.Environment.GetEnvironmentVariable("ARTU_SMOKE_OUTPUT");
        if(!string.IsNullOrWhiteSpace(testOutput))SmokeTests.Run(testOutput);
    }
}
[HarmonyPatch(typeof(EventModel),"BeforeEventStarted")]
public static class SyncChestBeforeEvent
{
    static void Postfix(EventModel __instance,ref Task __result)
    {
        if(__instance is not MegaCrit.Sts2.Core.Models.Events.SunkenTreasury||__instance.Owner is not {} p||p.Character is not Artu)return;
        __result=Wait(__result,p);
    }
    static async Task Wait(Task original,Player p)
    {await original;ChestStore.LoadAndSync(p);await ChestStore.Ready(p);}
}
