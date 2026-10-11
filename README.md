# Bait Beacon

An independent Dalamud plugin by Roxyz0501. Select a saved GatherBuddy Reborn Auto-Gather list, see the bait needed for its fish, and highlight matching bait in open inventory windows.

## Status

Public preview `1.0.0.0`. In-game overlay placement and font coverage still require verification.

## Use

Open `/baitbeacon`, select a saved list, and open your inventory. The plugin reads GatherBuddy Reborn's `auto_gather_lists.json` without changing it. Use the **Enable highlighting** checkbox on the list or Settings tab to turn all markers on or off. Settings also has separate switches for inventory, saddlebags and retainers. It checks whether GatherBuddy Reborn is installed and loaded in Config. A saved list can be viewed even while GatherBuddy Reborn is not running.

Original GatherBuddy has gathering lists but no compatible Auto-Gather list file, so it is shown as informational and is not used as a source.

The bundled fish-to-bait data is a snapshot of all available upstream patch files on 2026-10-11. Mooch routes use the target fish's explicit initial bait when specified; otherwise they trace to the mooch source fish's bait. Predator fish bait is also included. Spearfishing items are skipped because they need no bait. Only selected rod fish with no resolvable bait route are listed as unknown. GatherBuddy Reborn user-defined fish overrides are not read.

## Languages

The separate **Language settings** tab offers Japanese, English, German, French, Korean, Simplified Chinese, and Traditional Chinese. At first run, game language is preferred, then a supported Dalamud UI language, then English. The chosen language is saved and can be changed at any time without restarting. Game item names use the language data provided by the game. The separate **Support** tab uses the shared Roxyz0501 design and opens Ko-fi only when its button is clicked.

## Credits and data

The plugin refers to the saved-list JSON structure and fish bait data from [GatherBuddy Reborn](https://github.com/FFXIV-CombatReborn/GatherBuddyReborn), maintained by The Combat Reborn Team, licensed under Apache-2.0. The original [GatherBuddy](https://github.com/Ottermandias/GatherBuddy) is by Ottermandias and contributors, also Apache-2.0. `Data/fish-bait.json` is derived from GatherBuddy Reborn's fish data and is covered by the included Apache-2.0 license and NOTICE. No upstream plugin source code is included.

Inventory addon names and slot ordering were cross-checked against [Inventory Search Bar](https://github.com/Tischel/InventorySearchBar), by Tischel, licensed under AGPL-3.0. Its implementation code is not included.

Optional support for Roxyz0501: [Ko-fi](https://ko-fi.com/roxyz0501).
