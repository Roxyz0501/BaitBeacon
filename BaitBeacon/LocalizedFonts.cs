using Dalamud;
using Dalamud.Interface.ManagedFontAtlas;

namespace BaitBeacon;

internal sealed class LocalizedFonts : IDisposable
{
    private IFontHandle[] fonts = [];
    private readonly IFontAtlas atlas;
    private readonly GlyphCoverage coverage = new();
    private string? builtText;

    public LocalizedFonts(IFontAtlas atlas, IEnumerable<string> itemNames)
    {
        this.atlas = atlas;
        Ensure(itemNames.Prepend(Loc.FontText + new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray())));
    }

    // Called before Push, only after list changes. Existing handles must not be disposed while pushed.
    public void Ensure(IEnumerable<string> text)
    {
        coverage.Include(text);
        var fontText = coverage.Text;
        if (builtText == fontText) return;
        var glyphs = fontText.ToGlyphRange();
        var replacement = new List<IFontHandle>();
        try
        {
            for (var face = 0; face < 4; face++)
            {
                var fontFace = face;
                replacement.Add(atlas.NewDelegateFontHandle(step => step.OnPreBuild(tk =>
                    tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                        new SafeFontConfig { SizePx = 17, FontNo = fontFace, GlyphRanges = glyphs }))));
            }
        }
        catch { foreach (var handle in replacement) handle.Dispose(); throw; }
        var previous = fonts;
        fonts = replacement.ToArray();
        builtText = fontText;
        foreach (var handle in previous) handle.Dispose();
    }

    private static int Face(string? code) => code switch { "zh-Hant" => 1, "zh-Hans" => 2, "ko" => 3, _ => 0 };
    public IDisposable Push(string? code) => fonts[Face(code)].Push();
    public void Dispose() { foreach (var font in fonts) font.Dispose(); }
}
