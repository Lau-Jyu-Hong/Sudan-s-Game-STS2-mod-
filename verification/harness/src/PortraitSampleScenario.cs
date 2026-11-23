using ArtuMod;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using System.Text.Json;

namespace ArtuAutoplayLab;

public static class PortraitSampleScenario
{
    static readonly Type[] Sample =
    [
        typeof(ArrayThrust), typeof(SoldierCharge), typeof(SuperBecky), typeof(CrocodilePool),
        typeof(BurnTheBoats), typeof(NewWorld), typeof(AncientFaith), typeof(ChooseSoldiers),
        typeof(LifeScepter), typeof(KnowYourEnemy)
    ];

    public static async Task Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var checks = new List<string>();
        var rows = new List<object>();
        string? error = null;
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        async Task Until(Func<bool> test, string label)
        {
            var deadline = DateTime.UtcNow.AddSeconds(40);
            while (!test())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException(label);
                await Task.Delay(80);
            }
        }
        try
        {
            var sourceDir = System.Environment.GetEnvironmentVariable("ARTU_PORTRAIT_DIR")
                ?? throw new InvalidOperationException("ARTU_PORTRAIT_DIR is required.");
            TestMode.IsOn = false;
            using var selector = CardSelectorStack.SetTestSelector(new TakeFirst());
            var run = await NGame.Instance!.StartNewSingleplayerRun(
                Player.CreateForNewRun<Artu>(UnlockState.all, 1),
                true, ActModel.GetDefaultList(), [], "ARTU_PORTRAIT_SAMPLE", GameMode.Standard, false, 0);
            var player = run.Players[0];
            await RunManager.Instance.EnterMapPointInternal(1, MapPointType.Monster, null, true);
            await Until(() => CombatManager.Instance.IsInProgress && player.PlayerCombatState?.Phase == PlayerTurnPhase.Play,
                "Native combat did not reach the play phase.");
            var combat = ((CombatRoom)run.CurrentRoom!).CombatState;
            var layer = new CanvasLayer { Layer = 30 };
            NGame.Instance.AddChild(layer);
            var size = NGame.Instance.GetViewportRect().Size;
            layer.AddChild(new ColorRect { Size = size, Color = new Color("14120f") });
            var pages = Sample.Chunk(5).Select((group, index) => (group, index)).ToArray();
            foreach (var (group, page) in pages)
            {
                var shown = new List<NCard>();
                for (var i = 0; i < group.Length; i++)
                {
                    var model = (ArtuCard)combat.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(group[i])), player);
                    var name = model.GetType().Name.ToLowerInvariant();
                    var expected = "res://ArtuMod/images/" + name + ".tres";
                    Check(model.PortraitPath == expected, name + " uses its own portrait resource");
                    var live = model.Portrait.GetImage();
                    Check(live != null && live.GetWidth() == 250 && live.GetHeight() == 190, name + " portrait is 250x190");
                    var source = Image.LoadFromFile(Path.Combine(sourceDir, name + ".png"));
                    Check(source != null, name + " source png loads");
                    var delta = MaxChannelDelta(live!, source!);
                    rows.Add(new { name, title = model.Title, delta, path = model.PortraitPath });
                    Check(delta <= 2, name + " in-game portrait differs from the packed source by " + delta);
                    var node = NCard.Create(model)!;
                    layer.AddChild(node);
                    node.Scale = Vector2.One * 0.78f;
                    node.Position = new Vector2(size.X * (0.12f + i * 0.19f), size.Y * 0.58f);
                    var readyUntil = DateTime.UtcNow.AddSeconds(3);
                    while (!node.IsNodeReady() && DateTime.UtcNow < readyUntil) await Task.Delay(30);
                    node.UpdateVisuals(PileType.Hand, CardPreviewMode.Normal);
                    var portraitVisible = node.GetNode<TextureRect>("%Portrait").Visible;
                    if (model.Rarity == CardRarity.Ancient)
                        Check(!portraitVisible, name + " keeps the game ancient card layout");
                    else
                        Check(portraitVisible, name + " shows the normal portrait window");
                    shown.Add(node);
                }
                await Task.Delay(700);
                using var shot = NGame.Instance.GetViewport().GetTexture().GetImage();
                Check(shot.SavePng(Path.Combine(dir, "portrait-sample-" + (page + 1) + ".png")) == Error.Ok,
                    "Screenshot portrait-sample-" + (page + 1));
                foreach (var node in shown) node.QueueFree();
            }
            var sultan = ModelDb.Card<GoldSlaughter>();
            Check(sultan.Portrait.GetWidth() == 384, "Gold Slaughter full-card art remains 384 wide");
            var thoughts = new ArtuCard[] { ModelDb.Card<TouchPurity>(), ModelDb.Card<GlimpseDarkness>(), ModelDb.Card<FreedomFromFear>() };
            var shared = ModelDb.Card<FlowingThoughts>().Portrait.GetImage();
            foreach (var thought in thoughts)
            {
                var own = thought.GetType().Name.ToLowerInvariant();
                Check(thought.PortraitPath.EndsWith(own + ".tres", StringComparison.Ordinal), own + " keeps its existing portrait path");
                Check(MaxChannelDelta(thought.Portrait.GetImage(), shared) > 20, own + " is not the shared flowing-thoughts portrait");
                rows.Add(new { name = own, title = thought.Title, keptExisting = true, path = thought.PortraitPath });
            }
        }
        catch (Exception e)
        {
            error = e.ToString();
            GD.PrintErr(error);
        }
        File.WriteAllText(Path.Combine(dir, "result.json"), JsonSerializer.Serialize(new
        {
            passed = error == null,
            checks,
            rows,
            error,
            userDir = OS.GetUserDataDir()
        }, new JsonSerializerOptions { WriteIndented = true }));
        NGame.Instance?.GetTree().Quit(error == null ? 0 : 1);
    }

    static int MaxChannelDelta(Image live, Image source)
    {
        live.Convert(Image.Format.Rgb8);
        source.Convert(Image.Format.Rgb8);
        if (live.GetWidth() != source.GetWidth() || live.GetHeight() != source.GetHeight()) return 255;
        var max = 0;
        for (var y = 0; y < live.GetHeight(); y += 2)
        {
            for (var x = 0; x < live.GetWidth(); x += 2)
            {
                var a = live.GetPixel(x, y);
                var b = source.GetPixel(x, y);
                max = Math.Max(max, Channel(a.R, b.R));
                max = Math.Max(max, Channel(a.G, b.G));
                max = Math.Max(max, Channel(a.B, b.B));
            }
        }
        return max;
    }

    static int Channel(float left, float right) => (int)Math.Round(Math.Abs(left - right) * 255f);

    sealed class TakeFirst : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> cards, int min, int max)
            => Task.FromResult<IEnumerable<CardModel>>(cards.Take(Math.Max(min, 0)));
        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> cards, IReadOnlyList<MegaCrit.Sts2.Core.Entities.CardRewardAlternatives.CardRewardAlternative> alternatives)
            => new() { card = cards.FirstOrDefault()?.Card };
    }
}
