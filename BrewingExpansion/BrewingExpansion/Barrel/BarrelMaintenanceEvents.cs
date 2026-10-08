using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace XIAOWOBrewingBarrel;

// Item maintenance runs after actual game mutations. A one-frame delay lets
// the native inventory finish validating before we inspect or repair it.
internal static class BarrelMaintenanceEvents
{
    private static readonly Dictionary<long, GameItem> Pending = new();
    private static bool _scheduled;
    private static bool _fullSweep;
    private static bool _processing;

    internal static void ScheduleFullSweep()
    {
        _fullSweep = true;
        Schedule();
    }

    internal static void QueueItem(GameItem item)
    {
        if (_processing || BarrelLoadPatch.Loading || !BarrelItem.IsModItem(item)) return;
        try { Pending[item.Pointer.ToInt64()] = item; Schedule(); }
        catch { }
    }

    internal static void QueueInventory(GameInventory inventory, GameItem item)
    {
        if (_processing || BarrelLoadPatch.Loading) return;
        QueueItem(item);
        try { QueueItem(inventory?.GetParentItem()); }
        catch { }
    }

    private static void Schedule()
    {
        if (_scheduled) return;
        _scheduled = true;
        MelonCoroutines.Start(Drain());
    }

    private static IEnumerator Drain()
    {
        yield return null;
        _scheduled = false;
        if (BarrelLoadPatch.Loading)
        {
            yield break;
        }
        var sweep = _fullSweep;
        _fullSweep = false;
        var batch = new List<GameItem>(Pending.Values);
        Pending.Clear();
        _processing = true;
        try
        {
            if (sweep) BarrelItem.MaintainOwnedItems();
            else foreach (var item in batch) BarrelItem.MaintainItem(item);
        }
        catch (Exception ex) { Core.Log?.Warning("Event-driven item maintenance failed: " + ex); }
        finally { _processing = false; }
    }
}

[HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.UncheckedAccept), typeof(GameItem))]
internal static class BarrelGridAcceptEventPatch
{
    private static void Postfix(GameGridInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.Expel), typeof(GameItem))]
internal static class BarrelGridExpelEventPatch
{
    private static void Postfix(GameGridInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(GameGridScrollableInventory), nameof(GameGridScrollableInventory.UncheckedAccept), typeof(GameItem))]
internal static class BarrelScrollableAcceptEventPatch
{
    private static void Postfix(GameGridScrollableInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(GameGridScrollableInventory), nameof(GameGridScrollableInventory.Expel), typeof(GameItem))]
internal static class BarrelScrollableExpelEventPatch
{
    private static void Postfix(GameGridScrollableInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(GameSlotInventory), nameof(GameSlotInventory.UncheckedAccept), typeof(GameItem))]
internal static class BarrelSlotAcceptEventPatch
{
    private static void Postfix(GameSlotInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(GameSlotInventory), nameof(GameSlotInventory.Expel), typeof(GameItem))]
internal static class BarrelSlotExpelEventPatch
{
    private static void Postfix(GameSlotInventory __instance, GameItem item, bool __result)
    { if (__result) BarrelMaintenanceEvents.QueueInventory(__instance, item); }
}

[HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.TransferLiquid), typeof(GameItem), typeof(GameItem))]
internal static class BarrelWaterTransferEventPatch
{
    private static void Postfix(GameItem sourceContainer, GameItem targetContainer)
    {
        BarrelMaintenanceEvents.QueueItem(sourceContainer);
        BarrelMaintenanceEvents.QueueItem(targetContainer);
    }
}

[HarmonyPatch(typeof(WineHelper), nameof(WineHelper.FinishFermentation), typeof(GameItem))]
internal static class BarrelWineFinishEventPatch
{
    private static void Postfix(GameItem item)
    {
        try { BarrelMaintenanceEvents.QueueItem(item?.parentInventory?.GetParentItem()); }
        catch { }
    }
}
