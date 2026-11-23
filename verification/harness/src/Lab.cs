using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.AutoSlay;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;
using Environment = System.Environment;

namespace ArtuAutoplayLab;

// Test-only adapter. Never install this DLL in the player's usual game directory.
[ModInitializer(nameof(Initialize))]
public static class Lab
{
    static string? localScenario,localReport;
    public static bool Enabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_AUTOPLAY_SEED"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_NORMAL_LABEL"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_TEA_SCENARIO"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_V03_SCENARIO"))
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO"))||localScenario!=null;
    static bool started;
    static string reportDirectory = "";
    static object? lastSnapshot;
    static int maxFloor;
    public static void Initialize()
    {
        var path=Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath())!,"ArtuPromoTest.json");
        if(File.Exists(path)&&string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_SMOKE_OUTPUT")))
        {
            using var config=JsonDocument.Parse(File.ReadAllText(path));
            localScenario=config.RootElement.GetProperty("scenario").GetString();
            localReport=config.RootElement.GetProperty("reportDirectory").GetString();
        }
        bool isolated=Path.GetFileName(OS.GetUserDataDir().TrimEnd('/','\\'))=="ArtuModVerification";
        if(localScenario!=null&&!isolated)throw new InvalidOperationException("Local promo config is allowed only in ArtuModVerification.");
        // The offline test launch already uses only isolated local storage.
        // Do not reinitialize the singleton while the engine is loading settings.
        var commandLine=OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToArray();
        if(isolated&&!commandLine.Zip(commandLine.Skip(1)).Any(a=>a.First=="--force-steam"&&a.Second=="off"))
        {
            // Keep Steam authentication, but never write the demonstration to the player's cloud saves.
            var previousSettings=MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SettingsSave;
            var acknowledged=previousSettings.ModSettings?.PlayerAgreedToModLoading??false;
            var local=new MegaCrit.Sts2.Core.Saves.SaveManager(new MegaCrit.Sts2.Core.Saves.GodotFileIo("user://default/1"));
            MegaCrit.Sts2.Core.Saves.SaveManager.MockInstanceForTesting(local);
            local.InitSettingsData();
            // Switching the storage backend must not discard the existing acknowledgement.
            local.SettingsSave.ModSettings??=new MegaCrit.Sts2.Core.Modding.ModSettings();
            local.SettingsSave.ModSettings.PlayerAgreedToModLoading=acknowledged;
            local.SettingsSave.SeenEaDisclaimer=previousSettings.SeenEaDisclaimer;
        }
        if (Enabled) new Harmony("ArtuAutoplayLab").PatchAll(Assembly.GetExecutingAssembly());
    }
    public static async Task StartAfterMenu(Task original)
    {
        await original;
        if (started || !Enabled) return;
        started = true;
        // AutoSlay can abandon a run and modifies unlocks. Enforce the dedicated test profile.
        if (Path.GetFileName(OS.GetUserDataDir().TrimEnd('/', '\\')) != "ArtuModVerification")
            throw new InvalidOperationException("ArtuAutoplayLab requires the isolated ArtuModVerification profile.");
        reportDirectory = Environment.GetEnvironmentVariable("ARTU_AUTOPLAY_REPORT")
            ?? localReport ?? throw new InvalidOperationException("ARTU_AUTOPLAY_REPORT is required.");
        Directory.CreateDirectory(reportDirectory);
        await Task.Delay(1000);
        // Debug room jumps don't interact with tutorial popups; keep them
        // disabled only in the already-validated isolated profile.
        MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SetFtuesEnabled(false);
        if(localScenario=="third-batch")
        {
            _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(ThirdBatchScenario.Run(reportDirectory));return;
        }
        if(localScenario=="alpha-promo")
        {
            _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(AlphaPromoScenario.Run(reportDirectory));return;
        }
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO"))) {
            var compat=Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")!;
            if(compat=="art-preview"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(ArtPreviewScenario.Run(reportDirectory));return;
            }
            if(compat=="portrait-sample"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(PortraitSampleScenario.Run(reportDirectory));return;
            }
            if(compat is "compat-recovery" or "compat-shop"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(CompatibilityScenario.Run(reportDirectory,compat));return;
            }
            if(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")=="third-batch"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(ThirdBatchScenario.Run(reportDirectory));return;
            }
            if(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")=="evolution"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(EvolutionScenario.Run(reportDirectory));return;
            }
            if(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")=="second-batch"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(SecondBatchScenario.Run(reportDirectory));return;
            }
            if(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")=="repair"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(RepairScenario.Run(reportDirectory));return;
            }
            if(Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")=="repair-reload"){
                _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(RepairScenario.Reload(reportDirectory));return;
            }
            _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(V04Scenario.Run(reportDirectory,Environment.GetEnvironmentVariable("ARTU_V04_SCENARIO")!));
            return;
        }
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_V03_SCENARIO"))) {
            _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(V03Scenario.Run(reportDirectory));
            return;
        }
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ARTU_TEA_SCENARIO"))) {
            _ = MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(TeaScenario.Run(reportDirectory));
            return;
        }
        var normalLabel = Environment.GetEnvironmentVariable("ARTU_NORMAL_LABEL");
        if (!string.IsNullOrWhiteSpace(normalLabel)) {
            File.WriteAllText(Path.Combine(reportDirectory,"conditions.json"), JsonSerializer.Serialize(new {
                runLabel = normalLabel, seedPolicy = "Game-generated standard-mode seed; record the save/log for replay", profile = OS.GetUserDataDir(), mode = "normal rules via local HTTP bridge",
                defensiveBuffs = "none", strengthRamp = "none", cardExecution = "PlayCardAction; ordinary costs",
                unfinishedThoughtCards = "v0.4 playable higher thoughts; not a balance test"
            }, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }
        File.WriteAllText(Path.Combine(reportDirectory,"conditions.json"), JsonSerializer.Serialize(new {
            seed = Environment.GetEnvironmentVariable("ARTU_AUTOPLAY_SEED"),
            profile = OS.GetUserDataDir(), character = "Artu", mode = "built-in AutoSlay smoke test",
            defensiveBuffs = "999 Plating and 999 Regen", strengthRamp = "200 Strength per turn starting turn 3",
            cardExecution = "AutoPlay; does not validate ordinary manual play/energy costs",
            unfinishedThoughtCards = "v0.4 playable higher thoughts; not a balance test"
        }, new JsonSerializerOptions { WriteIndented = true }));
        new AutoSlayer().Start(Environment.GetEnvironmentVariable("ARTU_AUTOPLAY_SEED")!, Path.Combine(reportDirectory,"autoslay.log"));
        _ = Monitor();
    }
    static async Task Monitor()
    {
        try {
            while (AutoSlayer.IsActive) {
                Capture();
                await Task.Delay(1000);
            }
        } catch (Exception e) { File.AppendAllText(Path.Combine(reportDirectory,"monitor-errors.log"),e.ToString()); }
    }
    static void Capture()
    {
        var run = RunManager.Instance.DebugOnlyGetState();
        if (run == null) return;
        maxFloor = Math.Max(maxFloor,run.TotalFloor);
        lastSnapshot = new { utc = DateTime.UtcNow, floor = run.TotalFloor,
            players = run.Players.Select(p => new { character = p.Character.Id.ToString(), hp = p.Creature.CurrentHp,
                maxHp = p.Creature.MaxHp, dead = p.Creature.IsDead, gold = p.Gold,
                anger = ArtuMod.Util.State(p)?.Anger, mockery = ArtuMod.Util.State(p)?.Mockery,
                deck = p.Deck.Cards.Select(c => new { id = c.Id.ToString(),
                    deadline = (c as ArtuMod.SultanCard)?.BattlesRemaining }).ToArray() }).ToArray() };
        File.AppendAllText(Path.Combine(reportDirectory,"states.jsonl"), JsonSerializer.Serialize(lastSnapshot)+"\n");
    }
    public static void Finish(int exitCode)
    {
        if (string.IsNullOrEmpty(reportDirectory)) return;
        Capture();
        File.WriteAllText(Path.Combine(reportDirectory,"result.json"), JsonSerializer.Serialize(new {
            exitCode, maxFloor, lastSnapshot, limitations = "Smoke run uses buffs and AutoPlay; not a balance or manual-UI test."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

[HarmonyPatch(typeof(NGame),"LaunchMainMenu")]
static class Bootstrap
{
    static void Postfix(ref Task __result) => __result = Lab.StartAfterMenu(__result);
}

[HarmonyPatch(typeof(NCharacterSelectButton),nameof(NCharacterSelectButton.Select))]
static class SelectArtu
{
    static bool redirecting;
    static bool Prefix(NCharacterSelectButton __instance)
    {
        if (!Lab.Enabled || !AutoSlayer.IsActive || redirecting || __instance.Character is ArtuMod.Artu) return true;
        var button = __instance.GetParent().GetChildren().OfType<NCharacterSelectButton>()
            .FirstOrDefault(b => b.Character is ArtuMod.Artu)
            ?? throw new InvalidOperationException("Artu character button was not found; refusing to test a different character.");
        try { redirecting = true; button.UnlockIfPossible(); button.Select(); AutoSlayLog.Info("ArtuAutoplayLab selected actual character: "+button.Character.Id); }
        finally { redirecting = false; }
        return false;
    }
}

[HarmonyPatch(typeof(AutoSlayer),"QuitGame")]
static class WriteResult
{
    static void Prefix(int exitCode) => Lab.Finish(exitCode);
}
