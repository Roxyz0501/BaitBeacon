using Dalamud.Configuration;
using Dalamud.Game;

namespace BaitBeacon;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string? Language { get; set; }
    public string SelectedList { get; set; } = string.Empty;
    public bool HighlightEnabled { get; set; } = true;
    public bool HighlightInventory { get; set; } = true;
    public bool HighlightSaddlebag { get; set; } = true;
    public bool HighlightRetainer { get; set; } = true;

    public bool InitializeLanguage(ClientLanguage? game, string? dalamud)
    {
        if (Loc.IsSupported(Language)) return false;
        Language = Loc.FromGame(game) ?? Loc.Normalize(dalamud) ?? "en";
        return true;
    }
}
