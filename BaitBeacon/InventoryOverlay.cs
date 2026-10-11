using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Bounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BaitBeacon;

public sealed unsafe class InventoryOverlay(IGameGui gui)
{
    private const uint Border = 0xFF00BFFF;
    private const uint Fill = 0x3300BFFF;

    public void Draw(IReadOnlySet<uint> bait, Configuration config)
    {
        if (!config.HighlightEnabled || bait.Count == 0) return;
        var order = ItemOrderModule.Instance();
        var inventory = InventoryManager.Instance();
        if (order == null || inventory == null) return;

        var playerSorter = order->InventorySorter;
        if (config.HighlightInventory && playerSorter != null)
        {
            var normal = gui.GetAddonByName<AtkUnitBase>("Inventory");
            if (Visible(normal))
            {
                var tab = ((AddonInventory*)normal)->TabIndex;
                DrawGrid("InventoryGrid", playerSorter, inventory, bait, tab, 0);
            }

            var large = gui.GetAddonByName<AtkUnitBase>("InventoryLarge");
            if (Visible(large))
            {
                var tab = ((AddonInventoryLarge*)large)->TabIndex;
                for (var grid = 0; grid < 2; grid++)
                    DrawGrid("InventoryGrid" + grid, playerSorter, inventory, bait, tab * 2 + grid, 0);
            }

            var expanded = gui.GetAddonByName<AtkUnitBase>("InventoryExpansion");
            if (Visible(expanded))
                for (var grid = 0; grid < 4; grid++)
                    DrawGrid("InventoryGrid" + grid + "E", playerSorter, inventory, bait, grid, 0);
        }

        if (config.HighlightRetainer)
        {
            var retainer = gui.GetAddonByName<AtkUnitBase>("InventoryRetainer");
            if (Visible(retainer))
            {
                var sorter = order->GetActiveRetainerSorter();
                if (sorter != null)
                    DrawGrid("RetainerGrid", sorter, inventory, bait, ((AddonInventoryRetainer*)retainer)->TabIndex, 10000);
            }
        }

        if (config.HighlightSaddlebag)
        {
            DrawSaddlebag("InventoryBuddy", order, inventory, bait);
        }
    }

    private void DrawSaddlebag(string name, ItemOrderModule* order, InventoryManager* inventory, IReadOnlySet<uint> bait)
    {
        var addon = gui.GetAddonByName<AtkUnitBase>(name);
        if (!Visible(addon)) return;
        var tab = ((AddonInventoryBuddy*)addon)->TabIndex;
        if (tab > 1) return;
        var sorter = tab == 0 ? order->SaddleBagSorter : order->PremiumSaddleBagSorter;
        var typeBase = tab == 0 ? 4000 : 4100;
        if (sorter == null) return;
        for (var pane = 0; pane < 2; pane++)
        {
            for (var cell = 0; cell < 35; cell++)
            {
                if (!Matches(sorter, inventory, bait, pane * 35 + cell, typeBase)) continue;
                var nodeIndex = (pane == 0 ? 44 : 8) + (34 - cell);
                DrawNode(addon, nodeIndex);
            }
        }
    }

    private static bool Visible(AtkUnitBase* addon) => addon != null && addon->IsVisible;

    private void DrawGrid(string name, ItemOrderModuleSorter* sorter, InventoryManager* inventory,
        IReadOnlySet<uint> bait, int page, int typeBase)
    {
        if (page is < 0 or > 4) return;
        var grid = gui.GetAddonByName<AtkUnitBase>(name);
        if (!Visible(grid)) return;
        for (var cell = 0; cell < 35; cell++)
        {
            if (Matches(sorter, inventory, bait, page * 35 + cell, typeBase))
                DrawNode(grid, 3 + 34 - cell);
        }
    }

    private static bool Matches(ItemOrderModuleSorter* sorter, InventoryManager* inventory,
        IReadOnlySet<uint> bait, int sortedIndex, int typeBase)
    {
        if (sorter == null || sorter->Items.First == null || sortedIndex >= sorter->Items.Count) return false;
        var entry = sorter->Items[sortedIndex].Value;
        if (entry == null || entry->Slot >= 35 || entry->Page >= 5) return false;
        var bag = inventory->GetInventoryContainer((InventoryType)(typeBase + entry->Page));
        if (bag == null || !bag->IsLoaded || entry->Slot >= bag->Size) return false;
        var item = bag->GetInventorySlot(entry->Slot);
        return item != null && bait.Contains(item->ItemId);
    }

    private static void DrawNode(AtkUnitBase* addon, int index)
    {
        if (index < 0 || index >= addon->UldManager.NodeListCount) return;
        var node = addon->UldManager.NodeList[index];
        if (node == null || !node->IsVisible()) return;
        Bounds bounds = default;
        node->GetBounds(&bounds);
        if (bounds.Width is < 8 or > 100 || bounds.Height is < 8 or > 100) return;
        var min = new Vector2(bounds.Pos1.X, bounds.Pos1.Y);
        var max = new Vector2(bounds.Pos2.X, bounds.Pos2.Y);
        var draw = ImGui.GetForegroundDrawList();
        draw.AddRectFilled(min, max, Fill, 3);
        draw.AddRect(min, max, Border, 3, ImDrawFlags.None, 2.5f);
    }
}
