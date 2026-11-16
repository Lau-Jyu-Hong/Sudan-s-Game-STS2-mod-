using MegaCrit.Sts2.Core.HoverTips;
using System.Text.RegularExpressions;
namespace ArtuMod;

public static class ArtuTerms
{
    public static readonly Dictionary<string,string> Rules=new()
    {
        ["罪证"]="由罪犯、代价替换等效果获得，跨战斗保存，不随胜利自然减少。有罪证时可在休息处选择销毁罪证，消耗一次休息行动移除1层；其他明确消罪效果仍有效。\n1：卡牌伤害−1。\n2：卡牌格挡−1。\n3：每回合开始少抽1张。\n4：自然能量−1。\n5：首张非苏丹牌+1费。\n6：首张非苏丹牌后加入1晕眩。\n7：第4张起非苏丹牌随机弃1张。\n8：每张非苏丹牌后加入1晕眩，替代第6档。",
        ["苏丹之怒"]="苏丹牌到期获得，跨战斗保存。各档惩罚累计：\n1：开场1虚弱。\n3：额外开场1脆弱。\n5：开场洗入1张临时伤口。\n7：首回合自然能量−1。\n9：开场失去10生命。\n10：立即死亡，不可复活。",
        ["苏丹的嘲弄"]="无法支付苏丹代价却强行履行等行为获得，跨战斗保存。每层使可保有生命上限减少最大生命的10%，向下取整；不永久减少MaxHP。10层立即死亡。",
        ["代价"]="每次正常使用或重放苏丹牌均需支付其指定代价。免除与替换不是支付原代价；失败不会计入王命之下。",
        ["苏丹牌"]="杀戮、征服、奢靡、纵欲四类皇命，具有独立期限与代价。成功使用具体原牌可重置它的期限，同名不同牌独立计算。",
        ["读物"]="阿尔图牌的类别标签。读物不能升级，可以触发读物相关卡牌与革命议程。",
        ["思潮"]="阿尔图思想路线的类别标签。思潮不能升级，按各自条件在胜利后演化；不同进度使用不同计数。",
        ["杂兵"]="每层在玩家回合结束时对随机敌人造成2固定伤害，可被格挡。不受力量、虚弱、易伤、活力影响。不跨战斗保存；全军出击可将其改为群体伤害。",
        ["士兵"]="每层在玩家回合结束时对随机敌人造成2固定伤害，结算后每层获得1格挡；不受力量、敏捷、虚弱、易伤、脆弱、活力影响，可被格挡。不跨战斗保存。",
        ["精兵"]="每层回合结束造成2固定伤害，不受力量、虚弱、易伤、活力影响，可被格挡。\n≥3层：获得5活力，供角色后续攻击使用，不增强兵种。\n≥6层：结算附加1易伤。\n≥9层：结算附加1虚弱。\n每档每次结算一次，不跨战斗保存。",
        ["锐士"]="每层在玩家回合结束时对随机敌人造成4固定伤害，可被格挡。不受力量、虚弱、易伤、活力影响。不跨战斗保存。",
        ["洁念"]="纯洁的思想产生。本场未使用邪念、畏权且获胜时，按该路线获得奖励；累计达到既有进化条件时移除同名思想并获得触碰洁净。战斗计数不跨战斗保存。",
        ["邪念"]="不洁的思绪产生，与洁念的战后奖励互斥；按该路线条件获得奖励或进化为窥视黑暗。战斗计数不跨战斗保存。",
        ["畏权"]="对苏丹的恐惧产生。按使用次数在胜利后获得该路线奖励或进化为免于恐惧的自由。战斗计数不跨战斗保存。",
        ["净悟"]="由触碰洁净获得，仅本场有效。每回合第一张抽到的牌费用−1，仅当前回合，减费不随层数叠加。\n胜利时若本场没有秘悟：\n1～2层：向永久牌组加入等于层数的触碰洁净。\n≥3层：从永久牌组移除全部触碰洁净，加入1张赎罪祈愿；可永久移除至多1张苏丹牌。\n净悟与秘悟同场存在时，各自战斗效果正常，双方战后复制与进化奖励均不结算。",
        ["秘悟"]="由窥视黑暗获得，仅本场有效。每回合首次使用苏丹牌时额外失去2生命并抽2张牌，此战斗效果不随层数叠加。\n胜利时若本场没有净悟：\n1～2层：向永久牌组加入等于层数的窥视黑暗。\n≥3层：永久移除全部窥视黑暗，加入1张古老的信仰；另从随机提供的3张普通/罕见/稀有读物牌中选1张永久加入牌组。\n净悟与秘悟同场存在时，战斗效果正常，双方战后复制与进化奖励均不结算。",
        ["觉醒"]="由免于恐惧的自由获得，仅本场有效。每回合首次使用非苏丹牌获得1士兵；此前本回合没有用苏丹牌则抽1张，不随层数叠加。\n胜利时若本场没有臣服：\n1～2层：向永久牌组加入等于层数的免于恐惧的自由。\n≥3层：永久移除全部免于恐惧的自由，加入1张谋反的意图；可永久移除至多1张苏丹牌，并从随机提供的3张军队相关牌中选1张永久入组。",
        ["臣服"]="向苏丹俯首产生；会阻止觉醒路线的战后奖励/进化。按既定累计条件可获得为虎作伥；当场计数在结算后重置。",
        ["虔信"]="跨战斗进度，累计3次进化。\n本场使用赎罪祈愿并获胜；胜利当下罪证为0，且本战未新增苏丹之怒。\n满足时+1，每战至多1；罪证不会随胜利自然减少。3次移除全部同名三阶牌并获得完美世界。",
        ["狂信"]="跨战斗进度，累计3次进化。\n本场使用古老的信仰，并使用至少两种不同类型（不是品级）的苏丹牌而获胜。\n满足时+1，每战至多1；3次移除同名三阶牌并获得新世界。",
        ["谋逆"]="跨战斗进度，累计3次进化。\n本场使用谋反的意图，其额外兵种结算至少触发2次，并获胜。\n满足时+1，每战至多1；3次移除同名三阶牌并获得弑君的计划。",
        ["再生"]="原生能力，在持有者回合结束恢复生命后层数减少。玩家与敌人的再生各自在自己的回合结算。",
        ["多重护甲"]="回合结束获得对应层数格挡；受到未格挡的攻击伤害后减少1层。不跨战斗保存。",
        ["高级兵种"]="士兵、精兵、锐士三类，不包含杂兵。兵种层数仅本场有效。"
    };
    static readonly string Pattern=string.Join("|",Rules.Keys.OrderByDescending(s=>s.Length).Select(Regex.Escape));
    public static string Highlight(string text)
    {
        bool gold=false;var parts=Regex.Split(text,@"(\[[^\]]+\])");
        for(int i=0;i<parts.Length;i++)
        {if(parts[i]=="[gold]")gold=true;else if(parts[i]=="[/gold]")gold=false;else if(!gold&&!parts[i].StartsWith('['))parts[i]=Regex.Replace(parts[i],Pattern,m=>$"[gold]{m.Value}[/gold]");}
        return string.Concat(parts);
    }
    public static void Register()
    {
        var table=(Dictionary<string,string>)AccessTools.Field(typeof(LocTable),"_translations").GetValue(LocManager.Instance.GetTable("powers"))!;
        foreach(var name in Rules.Keys)table["ARTU_TERM_"+name]=name;
        table["ARTU_TERM_期限"]="履行期限与逾期";table["ARTU_TERM_兵种总计"]="破釜沉舟总计";
    }
    public static Player? OwnerOf(AbstractModel model)
    {try{return model switch{CardModel c when c.RunState!=null=>c.Owner,RelicModel r when r.IsMutable=>r.Owner,PowerModel p when p.IsMutable=>p.Owner.Player,_=>null};}catch{return null;}}
    public static string Detail(string name,Player? player)
    {
        string text=Rules[name];var s=player==null?null:Util.State(player);if(s==null)return text;
        return name switch{
            "罪证"=>$"当前：{V04.Crime(player!)}层。\n"+text,
            "苏丹之怒"=>$"当前：{s.Anger}层。\n"+text,
            "苏丹的嘲弄"=>$"当前：{s.Mockery}层；目前可保有生命上限：{RulesHp(player!,s)} / 最大生命{player!.Creature.MaxHp}。\n"+text,
            "虔信"=>$"当前进度：{Math.Min(3,s.Devotion)}/3。\n"+text,
            "狂信"=>$"当前进度：{Math.Min(3,s.Fanaticism)}/3。\n"+text,
            "谋逆"=>$"当前进度：{Math.Min(3,s.Treason)}/3。\n"+text,_=>text};
    }
    static int RulesHp(Player p,WitchCardBox s)=>ArtuMod.Rules.HpCeiling(p.Creature.MaxHp,s.Mockery);
    public static IEnumerable<IHoverTip> Tips(string text,Player? player=null)
    {
        foreach(var name in Rules.Keys.Where(text.Contains))
            yield return new HoverTip(new LocString("powers","ARTU_TERM_"+name),Detail(name,player)){Id="ArtuTerm:"+name};
    }
}
