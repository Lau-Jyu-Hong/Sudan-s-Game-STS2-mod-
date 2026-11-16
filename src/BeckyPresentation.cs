using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;
// Opt-in visual rhythm. No sound is fabricated: supply licensed short/long meows in a later asset update.
public static class BeckyPresentation
{
    static readonly Dictionary<Player,CanvasLayer> Layers=[];
    static bool Enabled(Player p)=>!TestMode.IsOn&&p.RunState.Players.Count==1&&System.Environment.GetEnvironmentVariable("ARTU_BECKY_PRESENTATION")=="1";
    public static async Task BeforeCard(Player p)
    {
        if(!Enabled(p)||NRun.Instance==null)return;
        if(!Layers.ContainsKey(p))
        {
            var layer=new CanvasLayer{Layer=16,Name="ArtuBeckyPresentation"};
            var label=new Label{Text="贝姬夫人正在挑选下一张牌…",Position=new Vector2(20,178),MouseFilter=Control.MouseFilterEnum.Ignore};
            label.AddThemeFontSizeOverride("font_size",18);layer.AddChild(label);NRun.Instance.AddChild(layer);Layers[p]=layer;
        }
        await Cmd.CustomScaledWait(1f,1f);
    }
    public static async Task AfterScans(Player p)
    {
        if(!Enabled(p)||p.Creature.IsDead||CombatManager.Instance.IsOverOrEnding)return;
        if(Layers.TryGetValue(p,out var layer)&&GodotObject.IsInstanceValid(layer))layer.GetChild<Label>(0).Text="贝姬夫人：歇一会儿。";
        await Cmd.CustomScaledWait(0.5f,0.5f);
    }
    public static void Clear(Player p){if(Layers.Remove(p,out var layer)&&GodotObject.IsInstanceValid(layer))layer.QueueFree();}
}
