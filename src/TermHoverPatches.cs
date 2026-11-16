using MegaCrit.Sts2.Core.HoverTips;
namespace ArtuMod;
public static class TermHoverPatches
{
    public static IEnumerable<IHoverTip> Merge(AbstractModel model,IEnumerable<IHoverTip> tips,string text)
    {
        if(model.GetType().Namespace!="ArtuMod")return tips;
        var list=tips.ToList();var ids=list.OfType<HoverTip>().Select(t=>t.Id).ToHashSet();
        list.AddRange(ArtuTerms.Tips(text,ArtuTerms.OwnerOf(model)).Where(t=>t is HoverTip h&&!ids.Contains(h.Id)));return list;
    }
}
[HarmonyPatch(typeof(RelicModel),nameof(RelicModel.HoverTips),MethodType.Getter)]
static class ExtraArtuRelicTerms
{
    static void Postfix(RelicModel __instance,ref IEnumerable<IHoverTip> __result)
        =>__result=TermHoverPatches.Merge(__instance,__result,string.Join("\n",(__instance is CustomRelicModel custom?custom.Localization:null)?.Select(t=>t.Item2)??[]));
}
[HarmonyPatch(typeof(PotionModel),nameof(PotionModel.HoverTips),MethodType.Getter)]
static class ExtraArtuPotionTerms
{
    static void Postfix(PotionModel __instance,ref IEnumerable<IHoverTip> __result)
        =>__result=TermHoverPatches.Merge(__instance,__result,__instance.DynamicDescription.GetFormattedText());
}
