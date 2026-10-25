namespace BaitBeacon;

internal sealed class GlyphCoverage
{
    private readonly HashSet<char> characters = [];
    public string Text => new(characters.Order().ToArray());

    public bool Include(IEnumerable<string> text)
    {
        var changed = false;
        foreach (var value in text)
            foreach (var character in value)
                if (!char.IsControl(character) && !char.IsSurrogate(character))
                    changed |= characters.Add(character);
        return changed;
    }
}
