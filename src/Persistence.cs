using System.Text.Json;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
namespace ArtuMod;
public sealed record ChestData(string Category,string Entry,int UpgradeLevel);
public static class ChestStore
{
    static readonly Dictionary<ulong,ChestData?> Shared=[];
    static readonly Dictionary<ulong,TaskCompletionSource> Pending=[];
    public static Task Ready(Player p)
    {if(Shared.ContainsKey(p.NetId))return Task.CompletedTask;if(!Pending.TryGetValue(p.NetId,out var tcs))Pending[p.NetId]=tcs=new();return tcs.Task;}
    static string PathFor(Player p)
    {
        var folder=ProjectSettings.GlobalizePath("user://artu_mod/profile_"+SaveManager.Instance.CurrentProfileId);
        Directory.CreateDirectory(folder);
        return System.IO.Path.Combine(folder,p.NetId+".json");
    }
    public static bool Has(Player p)=>Shared.TryGetValue(p.NetId,out var data)&&data!=null&&ModelDb.GetByIdOrNull<CardModel>(new ModelId(data.Category,data.Entry))!=null;
    public static void LoadAndSync(Player p)
    {
        if(!LocalContext.IsMe(p))return;
        ChestData? data=null;var path=PathFor(p);
        if(File.Exists(path))try{data=JsonSerializer.Deserialize<ChestData>(File.ReadAllText(path));}catch(JsonException){}
        Shared[p.NetId]=data;CustomMessageWrapper.Send(new ChestMessage{PlayerId=p.NetId,Json=JsonSerializer.Serialize(data)});
    }
    public static void Receive(ulong id,ChestData? data){Shared[id]=data;if(Pending.TryGetValue(id,out var tcs))tcs.TrySetResult();}
    public static CardModel? Peek(Player p)
    {
        if(!Shared.TryGetValue(p.NetId,out var data)||data==null)return null;
        var model=ModelDb.GetByIdOrNull<CardModel>(new ModelId(data.Category,data.Entry));if(model==null)return null;
        return model;
    }
    public static void ApplyUpgrade(Player p,CardModel card)
    {if(Shared.TryGetValue(p.NetId,out var data)&&data!=null)for(int i=0;i<Math.Min(data.UpgradeLevel,card.MaxUpgradeLevel);i++)CardCmd.Upgrade(card);}
    public static void Save(Player p,CardModel card)
    {var data=new ChestData(card.Id.Category,card.Id.Entry,card.CurrentUpgradeLevel);Shared[p.NetId]=data;if(LocalContext.IsMe(p))AtomicWrite(p,data);}
    public static void Clear(Player p){Shared[p.NetId]=null;if(LocalContext.IsMe(p))AtomicWrite(p,null);}
    static void AtomicWrite(Player p,ChestData? data)
    {var path=PathFor(p);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(data));File.Move(path+".tmp",path,true);}
}
public sealed class ChestMessage:ICustomMessage
{
    public ulong PlayerId {get;set;}public string Json {get;set;}="null";
    public bool ShouldBroadcast=>true;
    public void Serialize(PacketWriter writer){writer.WriteULong(PlayerId);writer.WriteString(Json);}
    public void Deserialize(PacketReader reader){PlayerId=reader.ReadULong();Json=reader.ReadString();}
    public void HandleMessage(ulong senderId)
    {if(senderId!=PlayerId)return;try{ChestStore.Receive(PlayerId,JsonSerializer.Deserialize<ChestData>(Json));}catch(JsonException){}}
}
