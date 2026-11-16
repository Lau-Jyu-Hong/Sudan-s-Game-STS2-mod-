using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Context;
namespace ArtuMod;

[HarmonyPatch(typeof(NRun),nameof(NRun._Ready))]
public static class PersistentStateUi
{
    static void Postfix(NRun __instance)
    {
        try
        {
            if(__instance.HasNode("ArtuPersistentState"))return;
            var run=(RunState)AccessTools.Field(typeof(NRun),"_state").GetValue(__instance)!;
            var player=run.Players.FirstOrDefault(p=>p.NetId==LocalContext.NetId&&Util.State(p)!=null)
                ??(run.Players.Count==1?run.Players[0]:null);
            if(player==null||Util.State(player)==null)return;
            var layer=new CanvasLayer{Name="ArtuPersistentState",Layer=15};
            var row=new HBoxContainer{Name="StatusRow",Position=new Vector2(18,145)};
            layer.AddChild(row);__instance.AddChild(layer);
            var names=new[]{"苏丹之怒","苏丹的嘲弄","虔信","狂信","谋逆"};
            var buttons=names.Select(_=>new Button{CustomMinimumSize=new Vector2(65,28),FocusMode=Control.FocusModeEnum.None}).ToArray();
            foreach(var b in buttons){b.AddThemeFontSizeOverride("font_size",15);row.AddChild(b);}
            int[] previous={-1,-1,-1};long[] flashUntil={0,0,0};
            void Refresh()
            {
                var s=Util.State(player);if(s==null){row.Visible=false;return;}
                // Native card/power tooltips must remain above this compact custom strip.
                var tips=AccessTools.Field(typeof(MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet),"_activeHoverTips")?.GetValue(null) as System.Collections.IDictionary;
                row.Visible=tips==null||tips.Count==0;
                var texts=new[]{$"怒 {s.Anger}",$"嘲 {s.Mockery}",$"虔信 {Math.Min(s.Devotion,3)}/3",$"狂信 {Math.Min(s.Fanaticism,3)}/3",$"谋逆 {Math.Min(s.Treason,3)}/3"};
                int[] progress={s.Devotion,s.Fanaticism,s.Treason};
                for(int i=0;i<3;i++){if(previous[i]>=0&&progress[i]>previous[i])flashUntil[i]=(long)Time.GetTicksMsec()+3000;previous[i]=progress[i];if((long)Time.GetTicksMsec()<flashUntil[i])texts[i+2]+=" +1";}
                for(int i=0;i<buttons.Length;i++){buttons[i].Text=texts[i];buttons[i].TooltipText=ArtuTerms.Detail(names[i],player);}
            }
            Refresh();var timer=new Godot.Timer{WaitTime=0.25,Autostart=true};timer.Timeout+=Refresh;layer.AddChild(timer);
        }
        catch(Exception e){GD.PrintErr("[Artu state UI] skipped without blocking gameplay: "+e.Message);}
    }
}
