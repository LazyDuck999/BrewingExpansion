using System;
using System.Collections.Generic;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class LiunianInventoryRegions
{
    internal static bool IsBattery(GameItem item)
    {
        if (item == null) return false;
        // GetTagReadonly returns a default object even for absent tags.
        // Only enabled power tags or the explicit item type identify a cell.
        if (item.IsTag("power_source_item") || item.IsTag("power_source_item_max_energy")) return true;
        var types = item.itemTypes;
        for (var i = 0; types != null && i < types.Count; i++)
            if (types[i] == "ENERGY_CELL") return true;
        return false;
    }

    internal static int RegionFor(SaveItemNode child, int savedNode)
    {
        var types = child.itemTypes;
        var battery = false;
        var wine = false;
        var module = false;
        for (var i = 0; types != null && i < types.Count; i++)
        {
            var type = types[i];
            if (string.IsNullOrEmpty(type)) continue;
            battery |= type == "ENERGY_CELL";
            wine |= type == "ALCOHOL";
            module |= type.IndexOf("MODULE", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        try
        {
            battery |= child.itemState?.IsTag("power_source_item") == true
                || child.itemState?.IsTag("power_source_item_max_energy") == true;
            wine |= child.itemState?.IsTag("WINE_CONTAINER_TAG") == true;
            module |= child.itemState?.IsTag("MODULE") == true
                || child.itemState?.IsTag("MODULE_TAG") == true;
        }
        catch { }
        var id = child.identifier ?? string.Empty;
        battery |= id.IndexOf("battery", StringComparison.OrdinalIgnoreCase) >= 0;
        wine |= id == "wine_bottle" || id.EndsWith("_wine", StringComparison.OrdinalIgnoreCase);
        module |= id.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0;
        if (battery) return LiunianAger.BatterySaveNode;
        if (wine) return LiunianAger.StorageSaveNode;
        if (module) return LiunianAger.ModuleSaveNode;

        return -1; // Preserve manuals and any unrecognized item in its original slot.
    }
}

internal static class LiunianInventoryRepair
{
    internal static void Repair(GridPixelElement layout, GameGridInventory modules,
        GameGridInventory storage, int cabinetId)
    {
        // Older builds could bind inventories by traversal order. Keep every
        // item attached unless its type and target region are both certain.
        var misplacedWines = InventoryOps.Snapshot(modules);
        var movedWine = 0;
        var movedModule = 0;
        for (var i = 0; i < misplacedWines.Count; i++)
        {
            try
            {
                var wine = misplacedWines[i];
                if (wine != null && MachineWineRack.CanAgeWine(wine)
                    && storage.MayHaveValidInventorySlot(wine)
                    && InventoryOps.Move(modules, storage, wine)) movedWine++;
            }
            catch (Exception ex) { Core.Log?.Warning("Liunian wine repair skipped an item: " + ex); }
        }
        var misplacedModules = InventoryOps.Snapshot(storage);
        for (var i = 0; i < misplacedModules.Count; i++)
        {
            try
            {
                var module = misplacedModules[i];
                if (module != null && !MachineWineRack.CanAgeWine(module)
                    && !LiunianInventoryRegions.IsBattery(module)
                    && modules.MayHaveValidInventorySlot(module)
                    && InventoryOps.Move(storage, modules, module)) movedModule++;
            }
            catch (Exception ex) { Core.Log?.Warning("Liunian module repair skipped an item: " + ex); }
        }
        var battery = layout.GetElement(0, 1)?.TryCast<GameSlotInventory>();
        var batteryItems = battery?.childItems;
        var misplacedBatteryItems = new List<GameItem>();
        for (var i = 0; batteryItems != null && i < batteryItems.Count; i++)
            if (batteryItems[i] != null) misplacedBatteryItems.Add(batteryItems[i]);
        for (var i = 0; i < misplacedBatteryItems.Count; i++)
        {
            try
            {
                var module = misplacedBatteryItems[i];
                if (!LiunianInventoryRegions.IsBattery(module)
                    && !MachineWineRack.CanAgeWine(module)
                    && modules.MayHaveValidInventorySlot(module)
                    && InventoryOps.Move(battery, modules, module)) movedModule++;
            }
            catch (Exception ex) { Core.Log?.Warning("Liunian battery area repair skipped an item: " + ex); }
        }
        if (movedWine > 0 || movedModule > 0)
            Core.Log?.Msg("Liunian " + cabinetId + ": restored " + movedWine
                + " wine(s) and " + movedModule + " module(s) to their areas.");
    }
}


[HarmonyLib.HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
internal static class LiunianSavedInventoryPatch
{
    [HarmonyLib.HarmonyPrefix]
    internal static void Prefix(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems)
    {
        if (savedItems == null || savedItems.Count == 0) return;
        try
        {
            // Normalize before cabinet detection and inventory-link repair,
            // including saves that still use the 3.0 xiaowo_ factory keys.
            var migrated = LegacyItemMigration.MigrateSavedNodes(savedItems);
            if (migrated > 0)
                Core.Log?.Msg("Resolved " + migrated + " legacy brewing item IDs before native loading.");
            var containsCabinet = false;
            for (var i = 0; i < savedItems.Count; i++)
                if (savedItems[i]?.identifier == LiunianAger.Id)
                {
                    containsCabinet = true;
                    break;
                }
            if (!containsCabinet) return;
            var byUuid = new Dictionary<long, SaveItemNode>();
            for (var i = 0; i < savedItems.Count; i++)
            {
                var node = savedItems[i];
                if (node != null) byUuid[node.uuid] = node;
            }
            for (var i = 0; i < savedItems.Count; i++)
            {
                var cabinet = savedItems[i];
                if (cabinet?.identifier != LiunianAger.Id) continue;
                var children = cabinet.childItems;
                var regions = cabinet.childItemInventoryNode;
                var corrected = 0;
                for (var j = 0; children != null && regions != null
                    && j < children.Count && j < regions.Count; j++)
                {
                    if (!byUuid.TryGetValue(children[j], out var child)) continue;
                    var target = LiunianInventoryRegions.RegionFor(child, regions[j]);
                    if (target < 0)
                    {
                        if (j < 4 && regions[j] >= 3 && regions[j] <= 6)
                            Core.Log?.Warning("Liunian " + cabinet.uniqueId + ": unknown saved item "
                                + child.identifier + " at inventory node " + regions[j]);
                        continue;
                    }
                    if (regions[j] == target) continue;
                    if (corrected < 4)
                        Core.Log?.Msg("Liunian " + cabinet.uniqueId + ": " + child.identifier
                            + " inventory node " + regions[j] + " -> " + target);
                    regions[j] = target;
                    corrected++;
                }
                if (corrected > 0)
                    Core.Log?.Msg("Liunian " + cabinet.uniqueId + ": corrected " + corrected
                        + " saved inventory link(s) before loading.");
            }
        }
        catch (Exception ex) { Core.Log?.Warning("Liunian saved inventory repair failed: " + ex); }
    }

    [HarmonyLib.HarmonyPostfix]
    internal static void Postfix(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems)
    {
        // Factory validation can cache the empty cabinet before DecodeNodes
        // restores its children. Repair only after the complete native decode.
        for (var i = 0; savedItems != null && i < savedItems.Count; i++)
        {
            var node = savedItems[i];
            // Decode restores saved state over the factory defaults. Repair old
            // seeds here too, including unowned seeds in saved merchant stock.
            if (node?.tempLink != null && BrewingExpansion.BrewingExpansionMod.IsOurSeed(node.identifier))
            {
                try { BrewingExpansion.BrewingExpansionMod.EnsureSeedFoodTag(node.tempLink); }
                catch (Exception ex) { Core.Log?.Warning("Seed post-load food tag repair failed: " + ex); }
            }
            if (node?.identifier != LiunianAger.Id || node.tempLink == null) continue;
            try { LiunianAger.EnsureLoaded(node.tempLink, forceRepair: true); }
            catch (Exception ex) { Core.Log?.Warning("Liunian post-load inventory repair failed: " + ex); }
        }
    }
}
