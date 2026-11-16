using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
namespace ArtuMod;

// Presentation only: no card choices, combat RNG, resource payments or scan rules live here.
public static class BeckyPresentation
{
    sealed class Session(CanvasLayer layer,TextureRect cat,AudioStreamPlayer voice,Godot.Timer timer)
    {
        public readonly CanvasLayer Layer=layer;
        public readonly TextureRect Cat=cat;
        public readonly AudioStreamPlayer Voice=voice;
        public readonly Godot.Timer Timer=timer;
        public CardModel? Card;
        public Vector2 LastAnchor;
        public int Calls;
        public ulong Started;
        public bool Resting;
    }
    static readonly Dictionary<Player,Session> Sessions=[];
    static readonly string[] ShortIds=["1472","1473","1479"];
    static AudioStreamMP3[]? Shorts;
    static AudioStreamMP3? Long;
    static Texture2D[]? Jump,Idle;
    static bool Failed;
    static bool Enabled(Player p)=>!TestMode.IsOn&&p.RunState.Players.Count==1
        &&System.Environment.GetEnvironmentVariable("ARTU_BECKY_PRESENTATION")!="0";
    static bool Alive(Player p)=>!p.Creature.IsDead&&CombatManager.Instance.IsInProgress&&!CombatManager.Instance.IsOverOrEnding&&NRun.Instance!=null;
    static byte[] Bytes(string name)
    {
        using var stream=typeof(BeckyPresentation).Assembly.GetManifestResourceStream("ArtuMod.Becky."+name)
            ??throw new InvalidOperationException("Missing Becky asset: "+name);
        using var bytes=new MemoryStream();stream.CopyTo(bytes);return bytes.ToArray();
    }
    static Texture2D Texture(string name)
    {
        using var image=new Image();
        if(image.LoadPngFromBuffer(Bytes(name))!=Error.Ok)throw new InvalidOperationException("Bad Becky PNG: "+name);
        return ImageTexture.CreateFromImage(image);
    }
    static void LoadAssets()
    {
        if(Shorts!=null)return;
        Shorts=ShortIds.Select(id=>new AudioStreamMP3{Data=Bytes(id+".mp3"),Loop=false}).ToArray();
        Long=new AudioStreamMP3{Data=Bytes("1890.mp3"),Loop=false};
        Jump=Enumerable.Range(1,8).Select(n=>Texture($"Jump-{n}.png")).ToArray();
        Idle=Enumerable.Range(1,10).Select(n=>Texture($"Idle-{n}.png")).ToArray();
        GD.Print($"[Artu Becky FX] loaded CC0 assets, lengths={string.Join(",",Shorts.Append(Long).Select(s=>s.GetLength().ToString("F3")))}");
    }
    static Session Get(Player p)
    {
        if(Sessions.TryGetValue(p,out var s)&&GodotObject.IsInstanceValid(s.Layer))return s;
        LoadAssets();
        var layer=new CanvasLayer{Layer=16,Name="ArtuBeckyPresentation"};
        var cat=new TextureRect{Name="Cat",Size=new Vector2(146,130),MouseFilter=Control.MouseFilterEnum.Ignore,
            ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,Texture=Idle![0]};
        // The game initializes and updates SFX -> Master from its audio sliders,
        // including mute-in-background. Do not apply the sliders twice here.
        var voice=new AudioStreamPlayer{Name="Voice",Bus="SFX",VolumeLinear=0.75f};
        var timer=new Godot.Timer{WaitTime=0.05,IgnoreTimeScale=true};
        layer.AddChild(cat);layer.AddChild(voice);layer.AddChild(timer);NRun.Instance!.AddChild(layer);
        s=new Session(layer,cat,voice,timer);Sessions[p]=s;
        var captured=s;timer.Timeout+=()=>{
            try{Tick(p,captured);}
            catch(Exception e){Failed=true;GD.PrintErr("[Artu Becky FX] animation stopped: "+e.Message);Clear(p);}
        };timer.Start();return s;
    }
    static void Tick(Player p,Session s)
    {
        if(!Alive(p)){Clear(p);return;}
        var elapsed=(Time.GetTicksMsec()-s.Started)/1000f;
        var frames=s.Resting?Idle!:elapsed<0.96f?Jump!:Idle!;
        int frame=s.Resting?(int)(elapsed/0.12f)%frames.Length:elapsed<0.96f?Math.Min(7,(int)(elapsed/0.12f)):(int)((elapsed-0.96f)/0.12f)%frames.Length;
        s.Cat.Texture=frames[frame];
        if(!s.Resting&&s.Card!=null)
        {
            var node=NPlayerHand.Instance?.GetCard(s.Card);
            if(node!=null&&GodotObject.IsInstanceValid(node))
                s.LastAnchor=node.GetGlobalTransformWithCanvas()*new Vector2(0,-211);
        }
        // Upright cat above the card; it never reparents or moves the native card.
        var viewport=NRun.Instance!.GetViewportRect().Size;
        var pos=s.LastAnchor-new Vector2(s.Cat.Size.X/2,s.Cat.Size.Y+12);
        s.Cat.Position=new Vector2(Math.Clamp(pos.X,8,Math.Max(8,viewport.X-s.Cat.Size.X-8)),Math.Clamp(pos.Y,80,Math.Max(80,viewport.Y-s.Cat.Size.Y-8)));
    }
    static async Task Wait(Player p,Session s,double duration)
    {
        var deadline=Time.GetTicksMsec()+(ulong)(duration*1000);
        while(Time.GetTicksMsec()<deadline&&Alive(p)&&Sessions.GetValueOrDefault(p)==s&&GodotObject.IsInstanceValid(s.Layer))
        {
            // Bind the awaiter to the persistent tree, not the removable FX layer.
            // Clearing the cat mid-wait must not strand the card action forever.
            var tree=s.Layer.GetTree();
            await tree.ToSignal(tree.CreateTimer(0.05,false,false,true),SceneTreeTimer.SignalName.Timeout);
        }
        if(GodotObject.IsInstanceValid(s.Voice))s.Voice.Stop();
    }
    public static async Task BeforeCard(Player p,CardModel card)
    {
        if(!Enabled(p)||!Alive(p)||Failed)return;
        try
        {
            var s=Get(p);s.Card=card;s.Resting=false;s.Started=Time.GetTicksMsec();s.Cat.Visible=true;
            var node=NPlayerHand.Instance?.GetCard(card);
            if(node==null){Clear(p);return;}
            s.LastAnchor=node.GetGlobalTransformWithCanvas()*new Vector2(0,-211);Tick(p,s);
            int variant=s.Calls++%Shorts!.Length;s.Voice.Stop();s.Voice.Stream=Shorts[variant];s.Voice.Play();
            double duration=Math.Max(1.8,Shorts[variant].GetLength()+0.2);
            GD.Print($"[Artu Becky FX] short={ShortIds[variant]} card={card.Id} seconds={duration:F3}");
            await Wait(p,s,duration);
            if(GodotObject.IsInstanceValid(s.Cat))s.Cat.Visible=false;
        }
        catch(Exception e){Failed=true;GD.PrintErr("[Artu Becky FX] skipped; gameplay continues: "+e.Message);Clear(p);}
    }
    public static async Task AfterScans(Player p)
    {
        if(!Enabled(p)||!Alive(p)||Failed)return;
        try
        {
            var s=Get(p);s.Resting=true;s.Started=Time.GetTicksMsec();s.Cat.Visible=true;
            // Rest above the last played card's location; the card itself may already be gone.
            if(s.LastAnchor==Vector2.Zero)s.LastAnchor=NRun.Instance!.GetViewportRect().Size*new Vector2(0.5f,0.75f);
            Tick(p,s);s.Voice.Stop();s.Voice.Stream=Long;s.Voice.Play();
            double duration=Math.Max(3.2,Long!.GetLength()+0.2);
            GD.Print($"[Artu Becky FX] long=1890 seconds={duration:F3}");await Wait(p,s,duration);
        }
        catch(Exception e){Failed=true;GD.PrintErr("[Artu Becky FX] resting skipped: "+e.Message);Clear(p);}
    }
    public static void Clear(Player p)
    {
        if(!Sessions.Remove(p,out var s))return;
        if(GodotObject.IsInstanceValid(s.Timer))s.Timer.Stop();
        if(GodotObject.IsInstanceValid(s.Voice))s.Voice.Stop();
        if(GodotObject.IsInstanceValid(s.Layer))s.Layer.QueueFree();
    }
}
