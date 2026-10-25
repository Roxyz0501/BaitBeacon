namespace BaitBeacon;

internal static class InventoryPages
{
    public static bool IsValid(int typeBase, int page) => page >= 0 && page < (typeBase switch
    {
        0 => 4,
        10000 => 7,
        4000 or 4100 => 2,
        _ => 0,
    });
}
