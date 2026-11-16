using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
            // A new CanvasLayer draws over the native hover-tip canvas even at layer 0.
            // Use an ordinary top-level Control in NRun, beneath the global hover tips.
            var layer=new Control{Name="ArtuPersistentState",TopLevel=true,MouseFilter=Control.MouseFilterEnum.Ignore,Size=__instance.GetViewportRect().Size};
            var row=new HBoxContainer{Name="StatusRow",Position=new Vector2(18,145)};
            row.AddThemeConstantOverride("separation",5);
            layer.AddChild(row);__instance.AddChild(layer);
            var names=new[]{"罪证","苏丹之怒","苏丹的嘲弄","虔信","狂信","谋逆","商店禁购"};
            var icons=new[]{"crimepower","stateanger","statemockery","statedevotion","statefanaticism","statetreason","supplyblockadepower"}.Select(n=>"res://ArtuMod/images/status/"+n+".tres").ToArray();
            var buttons=names.Select((n,i)=>new Button{Name="State"+i,CustomMinimumSize=new Vector2(56,30),FocusMode=Control.FocusModeEnum.None,Icon=ResourceLoader.Load<Texture2D>(icons[i]),ExpandIcon=true}).ToArray();
            for(int i=0;i<buttons.Length;i++)
            {
                var b=buttons[i];var name=names[i];b.AddThemeFontSizeOverride("font_size",14);b.AddThemeConstantOverride("icon_max_width",20);row.AddChild(b);
                b.MouseEntered+=()=>{
                    NHoverTipSet.Remove(b);
                    var tip=NHoverTipSet.CreateAndShow(b,[new HoverTip(new LocString("powers","ARTU_TERM_"+name),ArtuTerms.Detail(name,player)){Id="ArtuState:"+name}],HoverTipAlignment.Right);
                    if(tip!=null)
                    {
                        tip.SetAlignment(row,HoverTipAlignment.Right);
                        var text=tip.GetNode<Control>("textHoverTipContainer");
                        // Keep the whole explanation above the row, not behind adjacent icons.
                        tip.GlobalPosition+=new Vector2(24,row.GlobalPosition.Y-10-text.GetGlobalRect().End.Y);
                    }
                };
                b.MouseExited+=()=>NHoverTipSet.Remove(b);
            }
            var agenda=new Button{Name="Agenda",Text="议程",FocusMode=Control.FocusModeEnum.None};row.AddChild(agenda);agenda.Pressed+=()=>AgendaDetails.Show(player);
            var banBanner=new Label{Text="白银奢靡逾期：本店禁止购买商品与移除服务，离开后解除。",Position=new Vector2(380,110),MouseFilter=Control.MouseFilterEnum.Ignore};banBanner.AddThemeFontSizeOverride("font_size",24);banBanner.AddThemeColorOverride("font_color",new Color("ff8b80"));layer.AddChild(banBanner);
            var previewCache=new Dictionary<ulong,string>();
            void Refresh()
            {
                var s=Util.State(player);if(s==null){row.Visible=false;return;}
                int[] values={V04.Crime(player),s.Anger,s.Mockery,s.Devotion,s.Fanaticism,s.Treason,s.ActiveShopBan?1:s.NoBuyingShops};
                var shortNames=new[]{"罪","怒","嘲","虔","狂","逆","禁购"};
                for(int i=0;i<buttons.Length;i++)
                {if(values[i]<=0&&buttons[i].Visible)NHoverTipSet.Remove(buttons[i]);buttons[i].Visible=values[i]>0;buttons[i].Text=shortNames[i]+(i<3?values[i].ToString():i<6?Math.Min(values[i],3)+"/3":"");}
                agenda.Visible=RevolutionAgenda.Active(player);row.Visible=values.Any(v=>v>0)||agenda.Visible;banBanner.Visible=s.ActiveShopBan&&player.RunState.CurrentRoom is MerchantRoom;
                var node=NCombatRoom.Instance?.GetCreatureNode(player.Creature);
                var view=__instance.GetViewportRect().Size;
                var anchor=node==null?new Vector2(18,view.Y-105):node.Hitbox.GetGlobalTransformWithCanvas()*new Vector2(node.Hitbox.Size.X/2,node.Hitbox.Size.Y)+new Vector2(-row.Size.X/2,65);
                row.Position=new Vector2(Math.Clamp(anchor.X,8,Math.Max(8,view.X-row.Size.X-8)),Math.Clamp(anchor.Y,85,Math.Max(85,view.Y-row.Size.Y-85)));
                var activeTips=AccessTools.Field(typeof(NHoverTipSet),"_activeHoverTips")?.GetValue(null) as System.Collections.IDictionary;
                bool overlaps=false;
                if(activeTips!=null)foreach(System.Collections.DictionaryEntry entry in activeTips)
                {
                    if(entry.Key is Button own&&buttons.Contains(own))continue;
                    if(entry.Value is NHoverTipSet tip&&GodotObject.IsInstanceValid(tip)&&tip.GetNodeOrNull<Control>("textHoverTipContainer") is {} text&&row.GetGlobalRect().Intersects(text.GetGlobalRect())){overlaps=true;break;}
                }
                // Preserve layout while yielding to a native card/relic popup covering this row.
                row.Modulate=overlaps?new Color(1,1,1,0):Colors.White;
                foreach(var button in buttons)button.MouseFilter=overlaps?Control.MouseFilterEnum.Ignore:Control.MouseFilterEnum.Stop;
                // LocString deadline args are live values, not DynamicVars that emit events.
                // Refresh only the affected hand cards, and never override selection/upgrade UI.
                if(NPlayerHand.Instance is {CurrentMode:NPlayerHand.Mode.Play} hand)
                {
                    var active=new HashSet<ulong>();
                    string stats=string.Join(',',player.Creature.Powers.Select(p=>$"{p.Id}:{p.Amount}"))+string.Join(',',Util.Enemies(player).SelectMany(e=>e.Powers).Select(p=>$"{p.Id}:{p.Amount}"));
                    foreach(var holder in hand.ActiveHolders)
                    {
                        var card=holder.CardNode;if(card?.Model is not ArtuCard model)continue;
                        if(model is not SultanCard and not BurnTheBoats and not UnderKingsOrder)continue;
                        ulong id=card.GetInstanceId();active.Add(id);
                        string key=model is SultanCard sc?$"{sc.Original.BattlesRemaining}:{sc.Original.TurnsSinceUse}:{sc.Original.UsedThisBattle}:{V04.Data(player).Grace}":stats+model.CurrentUpgradeLevel+PaymentLedger.ThisTurn(player);
                        if(previewCache.GetValueOrDefault(id)!=key){card.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);previewCache[id]=key;}
                    }
                    foreach(var id in previewCache.Keys.Except(active).ToArray())previewCache.Remove(id);
                }
            }
            Refresh();var timer=new Godot.Timer{WaitTime=0.25,Autostart=true};timer.Timeout+=Refresh;layer.AddChild(timer);
        }
        catch(Exception e){GD.PrintErr("[Artu state UI] skipped without blocking gameplay: "+e.Message);}
    }
}
