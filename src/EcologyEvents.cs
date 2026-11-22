using MegaCrit.Sts2.Core.Events;
namespace ArtuMod;
public sealed class BorderTaxCollectorEvent:CustomEventModel
{
    public override string CustomInitialPortraitPath=>"res://images/events/trial.png";
    public override bool IsAllowed(MegaCrit.Sts2.Core.Runs.IRunState state)=>state.Players.Any(p=>p.Character is Artu);
    public override List<(string,string)> Localization=>new EventLoc("边防哨卡的税吏",new EventPageLoc("INITIAL","关隘前，税吏带着披甲卫兵拦住去路。通关文牒，或者按这儿的规矩办事。"));
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var p=Owner!;var options=new List<EventOption>();
        if(p.Gold>=35)options.Add(EventText.Option(this,"ARTU_TAX_BRIBE","行贿买路","失去35金币，获得1张已升级的阵列突刺。",async ()=>{
            await PlayerCmd.LoseGold(35,p);var card=p.RunState.CreateCard<ArrayThrust>(p);CardCmd.Upgrade(card);CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card,PileType.Deck));
            EventText.Finish(this,"ARTU_TAX_BRIBE_DONE","税吏收下钱袋，把一份布防图塞了过来。");}));
        if(!p.Relics.Any(r=>r is GarrisonEnlistment))options.Add(EventText.Option(this,"ARTU_TAX_FORCE","强行闯关","获得遗物闯关降卒，并获得2层罪证。此后每场战斗开始时获得2士兵。",async ()=>{
            await RelicCmd.Obtain<GarrisonEnlistment>(p);await CrimeRecord.Add(p,2);
            EventText.Finish(this,"ARTU_TAX_FORCE_DONE","两名守军留下了。哨卡的记录也留下了。");}));
        options.Add(EventText.Option(this,"ARTU_TAX_DECREE","亮出皇命","获得50金币。",async ()=>{
            await PlayerCmd.GainGold(50,p);EventText.Finish(this,"ARTU_TAX_DECREE_DONE","税吏让开道路，并奉上盘缠。");}));
        return options;
    }
}
public sealed class PalaceGuardMutinyEvent:CustomEventModel
{
    public override string CustomInitialPortraitPath=>"res://images/events/trial.png";
    public override bool IsAllowed(MegaCrit.Sts2.Core.Runs.IRunState state)=>state.Players.Any(p=>p.Character is Artu);
    public override List<(string,string)> Localization=>new EventLoc("禁卫军的哗变",new EventPageLoc("INITIAL","缺饷的禁卫军围坐磨刀。留下军饷，或者留下别的东西。"));
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var p=Owner!;var options=new List<EventOption>();
        if(p.Gold>=50&&!p.Relics.Any(r=>r is GuardsOrder))options.Add(EventText.Option(this,"ARTU_MUTINY_PAY","发放军饷","失去50金币，获得近卫军令状。",async ()=>{
            await PlayerCmd.LoseGold(50,p);await RelicCmd.Obtain<GuardsOrder>(p);
            EventText.Finish(this,"ARTU_MUTINY_PAY_DONE","禁卫队长交出调度令状。");}));
        options.Add(EventText.Option(this,"ARTU_MUTINY_FIGHT","雷霆镇压","失去12点生命，获得破釜沉舟。",async ()=>{
            await Util.LoseHp(new BlockingPlayerChoiceContext(),p,12);if(p.Creature.IsDead)return;
            await Util.AddToDeck(p,ModelDb.Card<BurnTheBoats>());
            EventText.Finish(this,"ARTU_MUTINY_FIGHT_DONE","营地安静下来。");}));
        options.Add(EventText.Option(this,"ARTU_MUTINY_LEAVE","抽身退避","离开。",()=>{EventText.Finish(this,"ARTU_MUTINY_LEAVE_DONE","你没有留下。");return Task.CompletedTask;}));
        return options;
    }
}
