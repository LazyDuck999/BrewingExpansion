using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class InventoryOps
{
    private static readonly MethodInfo ExpelMethod = typeof(GameGridInventory).GetMethod("Expel", new[] { typeof(GameItem) });
    private static readonly MethodInfo AcceptMethod = typeof(GameGridInventory).GetMethod("UncheckedAccept", new[] { typeof(GameItem) });
    private static readonly MethodInfo SlotAcceptMethod = typeof(GameSlotInventory).GetMethod("UncheckedAccept", new[] { typeof(GameItem) });
    private static readonly MethodInfo SlotExpelMethod = typeof(GameSlotInventory).GetMethod("Expel", new[] { typeof(GameItem) });

    internal static List<GameItem> Snapshot(GameGridInventory inventory)
    {
        var result = new List<GameItem>();
        var items = inventory?.childItems;
        if (items == null) return result;
        for (var i = 0; i < items.Count; i++) if (items[i] != null) result.Add(items[i]);
        return result;
    }

    internal static bool Move(GameGridInventory source, GameGridInventory target, GameItem item)
    {
        if (source == null || target == null || item == null || ExpelMethod == null || AcceptMethod == null) return false;
        try
        {
            if (ExpelMethod.Invoke(source, new object[] { item }) is not true)
            {
                Core.Log?.Warning("Inventory expel rejected " + item.identifier + " from " + source.identifier);
                return false;
            }
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("Inventory expel failed for " + item.identifier + ": " + ex);
            return false;
        }
        try
        {
            if (AcceptMethod.Invoke(target, new object[] { item }) is true) return true;
            Core.Log?.Warning("Inventory accept rejected " + item.identifier + " into " + target.identifier);
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("Inventory accept failed for " + item.identifier + " into " + target.identifier + ": " + ex);
        }
        // A native accept can attach the item before throwing while validating
        // its UI. Never put an already attached item into the source again.
        try { if (item.parentInventory?.Pointer == target.Pointer) return true; }
        catch { }
        bool restored = false;
        try
        {
            restored = item.parentInventory?.Pointer == source.Pointer
                || (source.identifier == "xiaowo_pulp_fruits"
                    ? PulpItem.RestoreRejectedMove(source, item)
                    : AcceptMethod.Invoke(source, new object[] { item }) is true);
        }
        catch (Exception ex) { Core.Log?.Error("Inventory restore failed for " + item.identifier + ": " + ex); }
        if (!restored)
            Core.Log?.Error("Could not restore an item after inventory move failed: " + item.uniqueId);
        return false;
    }

    internal static bool Accept(GameGridInventory target, GameItem item) =>
        target != null && item != null && AcceptMethod?.Invoke(target, new object[] { item }) is true;

    internal static bool Accept(GameSlotInventory target, GameItem item) =>
        target != null && item != null && SlotAcceptMethod?.Invoke(target, new object[] { item }) is true;

    internal static bool Move(GameSlotInventory source, GameGridInventory target, GameItem item)
    {
        if (source == null || target == null || item == null || SlotExpelMethod == null || AcceptMethod == null)
            return false;
        try
        {
            if (SlotExpelMethod.Invoke(source, new object[] { item }) is not true) return false;
            try
            {
                if (AcceptMethod.Invoke(target, new object[] { item }) is true) return true;
            }
            catch (Exception ex) { Core.Log?.Warning("Inventory accept failed for " + item.identifier + ": " + ex); }
            if (item.parentInventory?.Pointer == target.Pointer) return true;
            if (item.parentInventory?.Pointer != source.Pointer && !Accept(source, item))
                Core.Log?.Error("Could not restore a misplaced item after inventory move failed: " + item.uniqueId);
        }
        catch (Exception ex) { Core.Log?.Warning("Inventory move failed for " + item.identifier + ": " + ex); }
        return false;
    }
}
