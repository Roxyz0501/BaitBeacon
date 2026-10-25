using BaitBeacon;
using Roxyz0501.Dalamud.CommonUi;

PersistenceChecks.Run();

static void Check(bool result, string name)
{
    if (!result) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

var root = Path.Combine(Path.GetTempPath(), "BaitBeaconTests-" + Guid.NewGuid().ToString("N"));
var own = Path.Combine(root, "BaitBeacon");
var reborn = Path.Combine(root, "GatherbuddyReborn");
Directory.CreateDirectory(own);
Directory.CreateDirectory(reborn);
try
{
    var path = Path.Combine(reborn, "auto_gather_lists.json");
    File.WriteAllText(path, """
      [
        {"Name":"Fishing","FolderPath":"Weekend","ItemIds":[36679,41299,42],"EnabledItems":{"42":false}},
        {"Name":"Legacy","ItemIds":[36679]}
      ]
      """);
    var lists = new SavedLists(own);
    Check(lists.Lists.Count == 2, "saved lists load");
    Check(lists.Lists[0].Key == "Weekend/Fishing", "folder path identity");
    Check(lists.Lists[0].ItemIds.SequenceEqual([36679u, 41299u]), "disabled items excluded");
    Check(lists.Lists[1].ItemIds.SequenceEqual([36679u]), "older list without enabled map");
    File.WriteAllText(path, "{ broken");
    lists.Reload();
    Check(lists.Error == "read" && lists.Lists.Count == 2, "partial rewrite keeps last valid list");

    var catalog = new BaitCatalog();
    var coverage = new GlyphCoverage();
    Check(coverage.Include(["Fishing"]) && coverage.Include(["麒麟", "龍"])
        && "麒麟龍".All(coverage.Text.Contains), "new list glyphs included after startup");
    Check(!coverage.Include(["龍", "Fishing", "麒麟"]), "unchanged glyphs require no font rebuild");
    foreach (var malformed in new[] { "[null]", "[{}]", "[{\"Name\":\"Bad\",\"ItemIds\":null}]",
                 "[{\"Name\":\"Bad\",\"ItemIds\":[\"36679\"]}]",
                 "[{\"Name\":\"Bad\",\"ItemIds\":[36679],\"EnabledItems\":{\"36679\":\"false\"}}]" })
    {
        var previous = lists.Lists;
        File.WriteAllText(path, malformed);
        Check(!lists.Reload(retry: true) && lists.Error == "read" && ReferenceEquals(previous, lists.Lists),
            "malformed schema retains last snapshot: " + malformed);
    }
    File.WriteAllText(path, "[]");
    Check(lists.Reload(retry: true) && lists.Lists.Count == 0, "valid empty list clears snapshot");
    Check(catalog.Resolve([8760], id => id == 2603).UnknownFishIds.Contains(8760),
        "known predator bait does not hide missing target bait");
    Check(catalog.Resolve([8760], id => id == 2625).UnknownFishIds.Contains(8760),
        "known target bait does not hide missing prerequisite bait");
    var complete = catalog.Resolve([8760], id => id is 2625 or 2603);
    Check(complete.UnknownFishIds.Count == 0 && complete.BaitIds.SetEquals([2625u, 2603u]),
        "complete target and predator bait retained");
    foreach (var (typeBase, count) in new[] { (0, 4), (10000, 7), (4000, 2), (4100, 2) })
        Check(Enumerable.Range(0, count).All(page => InventoryPages.IsValid(typeBase, page))
            && !InventoryPages.IsValid(typeBase, -1) && !InventoryPages.IsValid(typeBase, count),
            "inventory page bounds " + typeBase);
    var baits = catalog.Resolve([36679, 41299], id => id is 36593 or 38808);
    Check(baits.BaitIds.ToHashSet().SetEquals([36593u, 38808u]), "direct and mooch bait resolution");
    Check(baits.UnknownFishIds.Count == 0, "known fish resolved");
    var mooched = catalog.Resolve([36683], id => id is 36589 or 36591);
    Check(mooched.BaitIds.ToHashSet().SetEquals([36589u])
          && mooched.UnknownFishIds.Count == 0, "mooch fish uses explicit initial bait");
    var sourceFish = catalog.Resolve([36458], id => id == 36591);
    Check(sourceFish.BaitIds.ToHashSet().SetEquals([36591u])
          && sourceFish.UnknownFishIds.Count == 0, "previously missing mooch source resolved");
    Check(catalog.Resolve([999999], _ => false).UnknownFishIds.SetEquals([999999u]),
        "unresolved selected fish is reported");
    Loc.CheckCompleteness();
    Check(Loc.Languages.Select(x => (x.Code, x.Native))
          .SequenceEqual(StandardPluginTabs.Languages.Select(x => (x.Code, x.SelfName))),
        "shared tab language order and native names");
    static string ResourceText(string name)
    {
        using var stream = typeof(StandardPluginTabs).Assembly.GetManifestResourceStream(name)
            ?? throw new Exception("Missing resource " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    var pluginSource = ResourceText("BaitBeacon.Plugin.cs");
    var tabsSource = ResourceText("BaitBeacon.StandardPluginTabs.cs");
    var listsTab = pluginSource.IndexOf("ImGui.BeginTabItem(T(\"Lists\")", StringComparison.Ordinal);
    var sharedTabs = pluginSource.IndexOf("StandardPluginTabs.DrawStandardTrailingTabs(", StringComparison.Ordinal);
    Check(listsTab >= 0 && sharedTabs > listsTab
          && pluginSource.LastIndexOf("StandardPluginTabs.DrawStandardTrailingTabs(", StringComparison.Ordinal) == sharedTabs
          && pluginSource.IndexOf("ImGui.BeginTabItem(", listsTab + 1, StringComparison.Ordinal) < 0,
        "one shared trailing tab group after Lists");
    var settingsTab = tabsSource.IndexOf("BeginTabItem($\"{settingsLabel}###{settingsStableId}\")", StringComparison.Ordinal);
    var languageTab = tabsSource.IndexOf("BeginTabItem($\"{languageSettingsLabel}###LanguageSettings\")", StringComparison.Ordinal);
    var supportTab = tabsSource.IndexOf("BeginTabItem($\"{supportLabel}###Support\")", StringComparison.Ordinal);
    Check(settingsTab >= 0 && languageTab > settingsTab && supportTab > languageTab,
        "shared settings language support tab order");
    Check(Loc.Get("ja", "LanguageSettings") == "言語設定"
          && Loc.Get("ja", "Support") == "支援"
          && Loc.Get("en", "LanguageHelp").Length > 0
          && Loc.Get("en", "SupportRecipient").Contains("Roxyz0501"),
        "language and support tab localization");
    Check(Loc.FontText.Contains("한국어") && Loc.FontText.Contains("简体中文")
          && Loc.FontText.Contains("繁體中文") && Loc.FontText.Contains('—'),
        "font glyph list covers language names and item separator");
    Check(Loc.Normalize("zh_TW") == "zh-Hant" && Loc.Normalize("zh") is null, "Chinese script normalization");
    Check(Loc.FromGame(Dalamud.Game.ClientLanguage.Japanese) == "ja", "game language precedence input");
    var configuration = new Configuration();
    Check(configuration.HighlightEnabled, "highlight enabled by default");
    configuration.HighlightEnabled = false;
    var savedConfiguration = System.Text.Json.JsonSerializer.Deserialize<Configuration>(
        System.Text.Json.JsonSerializer.Serialize(configuration));
    Check(savedConfiguration is { HighlightEnabled: false }, "highlight toggle persists");
    Check(configuration.InitializeLanguage(Dalamud.Game.ClientLanguage.Japanese, "de") && configuration.Language == "ja",
        "first launch prefers game language");
    configuration.Language = "ko";
    Check(!configuration.InitializeLanguage(Dalamud.Game.ClientLanguage.English, "de") && configuration.Language == "ko",
        "saved choice remains fixed");
    configuration.Language = null;
    Check(configuration.InitializeLanguage(null, "zh_CN") && configuration.Language == "zh-Hans",
        "Dalamud language fallback");
    configuration.Language = "Auto";
    Check(configuration.InitializeLanguage(null, "zh") && configuration.Language == "en",
        "ambiguous language resolves to English");
    foreach (var savedCode in new[] { "ja", "en" })
    {
        var migrated = System.Text.Json.JsonSerializer.Deserialize<Configuration>(
            "{\"Language\":\"" + savedCode + "\",\"SelectedList\":\"Weekend/Fishing\",\"HighlightEnabled\":false}")!;
        Check(!migrated.InitializeLanguage(Dalamud.Game.ClientLanguage.French, "de")
              && migrated.Language == savedCode && migrated.SelectedList == "Weekend/Fishing"
              && !migrated.HighlightEnabled, "saved " + savedCode + " config preserved");
    }
}
finally
{
    Directory.Delete(root, true);
}
