using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using Roxyz0501.Dalamud.CommonUi;

namespace BaitBeacon;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/baitbeacon";
    private readonly IDalamudPluginInterface pi;
    private readonly ICommandManager commands;
    private readonly CommandInfo commandInfo;
    private readonly IFramework framework;
    private readonly IGameInventory gameInventory;
    private readonly IDataManager data;
    private readonly IPluginLog log;
    private readonly IClientState client;
    private readonly ConfigurationStore settings;
    private readonly FailureCircuit overlayFailures;
    private readonly CancellationTokenRegistration shutdownRegistration;
    private readonly CancellationTokenRegistration unloadRegistration;
    private readonly Configuration config;
    private readonly SavedLists lists;
    private readonly BaitCatalog catalog;
    private readonly InventoryOverlay overlay;
    private readonly LocalizedFonts fonts;
    private readonly HashSet<uint> rodFishIds;
    private readonly HashSet<uint> baitIds;
    private BaitResult current = new(new HashSet<uint>(), new HashSet<uint>());
    private long lastReload = Environment.TickCount64;
    private volatile bool stopping;
    private bool disposed;
    private bool open;

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework,
        IGameGui gui, IGameInventory gameInventory, IDataManager data, IClientState client, IPluginLog log,
        IGameLifecycle lifecycle)
    {
        this.pi = pi;
        this.commands = commands;
        this.framework = framework;
        this.gameInventory = gameInventory;
        this.data = data;
        this.log = log;
        this.client = client;
        settings = new ConfigurationStore(pi.ConfigFile.FullName, ex => ReportStopped(ex, "Configuration saving"),
            onFinalFailure: ex =>
            {
                log.Error(ex, "Final configuration flush failed. Last saved settings remain on disk. Use Retry in /baitbeacon.");
                open = true;
            });
        config = settings.Configuration;
        if (settings.Recovered || settings.LoadWarning is not null) open = true;
        overlayFailures = new FailureCircuit(TimeSpan.FromSeconds(3), ex => ReportStopped(ex, "Inventory highlighting"));
        ClientLanguage? game;
        try { game = client.ClientLanguage; } catch { game = null; }
        Loc.CheckCompleteness();
        if (config.InitializeLanguage(game, pi.UiLanguage)) Save();
        lists = new SavedLists(pi.GetPluginConfigDirectory(), ex => ReportStopped(ex, "Saved list reading"));
        catalog = new BaitCatalog();
        overlay = new InventoryOverlay(gui);
        rodFishIds = data.GetExcelSheet<FishParameter>()
            .Select(x => x.Item.RowId).Where(x => x != 0).ToHashSet();
        baitIds = data.GetExcelSheet<Item>().Where(x => x.ItemSearchCategory.RowId == 30)
            .Select(x => x.RowId).ToHashSet();
        baitIds.UnionWith(data.GetExcelSheet<WKSItemInfo>()
            .Where(x => x.WKSItemSubCategory.RowId == 5)
            .Select(x => x.Item.RowId).Where(x => x != 0));
        fonts = new LocalizedFonts(pi.UiBuilder.FontAtlas,
            FontItemNames().Concat(lists.Lists.SelectMany(x => new[] { x.Name, x.Folder })));
        Recalculate();

        commandInfo = new CommandInfo((_, _) => open = true)
        {
            HelpMessage = T("CommandHelp"),
        };
        commands.AddHandler(Command, commandInfo);
        framework.Update += Update;
        pi.UiBuilder.Draw += Draw;
        pi.UiBuilder.OpenMainUi += Open;
        pi.UiBuilder.OpenConfigUi += Open;
        client.Logout += OnLogout;
        shutdownRegistration = lifecycle.GameShuttingDownToken.Register(StopSaving);
        unloadRegistration = lifecycle.DalamudUnloadingToken.Register(StopSaving);
    }

    private void Open() => open = true;
    private string T(string key) => Loc.Get(config.Language, key);
    private void Save() => settings.Update(config);
    private void OnLogout(int type, int code) => settings.Flush();
    private void StopSaving() { stopping = true; settings.Dispose(); }
    private void ReportStopped(Exception ex, string operation)
    {
        log.Error(ex, "{Operation} stopped after three failures. Open /baitbeacon and use Retry.", operation);
        open = true;
    }

    private void Update(IFramework _)
    {
        if (stopping) return;
        settings.Tick();
        var now = Environment.TickCount64;
        if (now - lastReload < 3000) return;
        lastReload = now;
        if (lists.Reload()) Recalculate();
    }

    private void Recalculate()
    {
        var selected = lists.Lists.FirstOrDefault(x => x.Key == config.SelectedList);
        current = selected is null
            ? new BaitResult(new HashSet<uint>(), new HashSet<uint>())
            : catalog.Resolve(selected.ItemIds.Where(rodFishIds.Contains), baitIds.Contains);
    }

    private void Draw()
    {
        if (stopping) return;
        overlayFailures.Run(() => overlay.Draw(current.BaitIds, config));
        if (!open) return;
        using var font = fonts.Push(config.Language);
        ImGui.SetNextWindowSize(new Vector2(610, 510), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(390, 290), new Vector2(float.MaxValue));
        if (ImGui.Begin(T("Title") + "###BaitBeacon", ref open))
        {
            DrawRecovery();
            if (ImGui.BeginTabBar("BaitBeaconTabs"))
            {
                if (ImGui.BeginTabItem(T("Lists") + "###Lists"))
                {
                    DrawLists();
                    ImGui.EndTabItem();
                }
                StandardPluginTabs.DrawStandardTrailingTabs(
                    T("Settings"), "Settings", DrawSettings,
                    T("LanguageSettings"), DrawLanguageSettings,
                    T("Support"), DrawSupport);
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }

    private void DrawLists()
    {
        if (lists.Error == "read") ImGui.TextWrapped(T("ListError"));
        if (lists.Lists.Count == 0) ImGui.TextWrapped(T("NoLists"));
        else
        {
            ImGui.TextUnformatted(T("Select"));
            var selected = lists.Lists.FirstOrDefault(x => x.Key == config.SelectedList);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("###SavedList", selected is null ? T("NoSelection") : Display(selected)))
            {
                foreach (var list in lists.Lists)
                    if (ImGui.Selectable(Display(list) + "###" + list.Key, list.Key == config.SelectedList))
                    {
                        config.SelectedList = list.Key;
                        Save();
                        Recalculate();
                    }
                ImGui.EndCombo();
            }
        }
        if (ImGui.Button(T("Reload"))) { if (lists.Reload(retry: true)) Recalculate(); }
        var highlight = config.HighlightEnabled;
        if (ImGui.Checkbox(T("HighlightEnabled") + "###HighlightEnabledLists", ref highlight))
        {
            config.HighlightEnabled = highlight;
            Save();
        }
        ImGui.Separator();
        ImGui.TextUnformatted(T("Bait"));
        if (current.BaitIds.Count == 0) ImGui.TextUnformatted(T("None"));
        foreach (var id in current.BaitIds.OrderBy(ItemName))
        {
            var count = CountPlayerItem(id);
            ImGui.BulletText($"{ItemName(id)}  |  {string.Format(T("Count"), count)}");
        }
        if (current.UnknownFishIds.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextUnformatted(T("Unknown"));
            foreach (var id in current.UnknownFishIds.OrderBy(ItemName))
                ImGui.BulletText(ItemName(id));
        }
    }

    private void DrawSettings()
    {
        ImGui.TextUnformatted(T("Dependency"));
        PluginStatus("GatherBuddyReborn");
        PluginStatus("GatherBuddy");
        ImGui.TextWrapped(T("Original"));
        ImGui.Separator();
        var highlight = Toggle("HighlightEnabled", config.HighlightEnabled);
        var player = Toggle("Inventory", config.HighlightInventory);
        var saddle = Toggle("Saddlebag", config.HighlightSaddlebag);
        var retainer = Toggle("Retainer", config.HighlightRetainer);
        if (highlight != config.HighlightEnabled || player != config.HighlightInventory
            || saddle != config.HighlightSaddlebag || retainer != config.HighlightRetainer)
        {
            config.HighlightEnabled = highlight;
            config.HighlightInventory = player;
            config.HighlightSaddlebag = saddle;
            config.HighlightRetainer = retainer;
            Save();
        }
    }

    private void DrawRecovery()
    {
        if (lists.Stopped)
        {
            ImGui.TextWrapped(T("ListStopped"));
            ImGui.TextUnformatted(lists.LastError?.Message ?? "");
        }
        if (overlayFailures.Stopped)
        {
            ImGui.TextWrapped(T("OverlayStopped"));
            ImGui.TextUnformatted(overlayFailures.LastError?.Message ?? "");
            if (ImGui.Button(T("Retry") + "###RetryOverlay")) overlayFailures.Retry();
        }
        if (settings.Recovered) ImGui.TextWrapped(T("ConfigRecovered"));
        if (settings.LoadWarning is not null) ImGui.TextWrapped(T("ConfigLoadWarning"));
        var saveError = settings.LastError;
        if (saveError is not null)
        {
            if (!settings.LoadBlocked) ImGui.TextWrapped(T(settings.Stopped ? "SaveStopped" : "SavePending"));
            ImGui.TextUnformatted(saveError.Message);
            if (ImGui.Button(T("Retry") + "###RetrySave"))
            {
                settings.Retry();
                if (!settings.LoadBlocked)
                {
                    if (config.InitializeLanguage(client.ClientLanguage, pi.UiLanguage)) Save();
                    commandInfo.HelpMessage = T("CommandHelp");
                    Recalculate();
                }
            }
        }
    }

    private void DrawLanguageSettings()
    {
        var changed = StandardPluginTabs.DrawLanguageSelector(
            T("Language"), T("LanguageHelp"), config.Language ?? "en");
        if (changed is null) return;
        config.Language = changed;
        commandInfo.HelpMessage = T("CommandHelp");
        Save();
    }

    private void DrawSupport() => StandardPluginTabs.DrawSupportContent(
        T("SupportHeading"), T("SupportText"), T("SupportRecipient"), T("OpenKoFi"));

    private void PluginStatus(string internalName)
    {
        var installed = pi.InstalledPlugins.FirstOrDefault(x => x.InternalName == internalName);
        var status = installed is null ? T("Missing") : installed.IsLoaded ? T("Loaded") : T("Installed");
        ImGui.BulletText($"{internalName}: {status}");
    }

    private bool Toggle(string label, bool value)
    {
        ImGui.Checkbox(T(label) + "###" + label, ref value);
        return value;
    }

    private static string Display(SavedList list) => string.IsNullOrEmpty(list.Folder) ? list.Name : list.Folder + "/" + list.Name;

    private string ItemName(uint id)
    {
        var row = data.GetExcelSheet<Item>(Loc.GameDataLanguage(config.Language)).GetRowOrDefault(id);
        return row is { } item ? item.Name.ToString() : $"#{id}";
    }

    private IEnumerable<string> FontItemNames()
    {
        var ids = rodFishIds.Concat(baitIds).Distinct().ToArray();
        foreach (var language in new[] { ClientLanguage.Japanese, ClientLanguage.English,
                     ClientLanguage.German, ClientLanguage.French })
        {
            var sheet = data.GetExcelSheet<Item>(language);
            foreach (var id in ids)
                if (sheet.GetRowOrDefault(id) is { } item)
                    yield return item.Name.ToString();
        }
    }

    private int CountPlayerItem(uint id)
    {
        var count = 0;
        foreach (var type in new[] { GameInventoryType.Inventory1, GameInventoryType.Inventory2,
                     GameInventoryType.Inventory3, GameInventoryType.Inventory4 })
            foreach (var item in gameInventory.GetInventoryItems(type))
                if (item.ItemId == id) count += item.Quantity;
        return count;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        stopping = true;
        framework.Update -= Update;
        pi.UiBuilder.Draw -= Draw;
        pi.UiBuilder.OpenMainUi -= Open;
        pi.UiBuilder.OpenConfigUi -= Open;
        client.Logout -= OnLogout;
        commands.RemoveHandler(Command);
        shutdownRegistration.Dispose();
        unloadRegistration.Dispose();
        try { settings.Dispose(); }
        finally { fonts.Dispose(); }
    }
}
