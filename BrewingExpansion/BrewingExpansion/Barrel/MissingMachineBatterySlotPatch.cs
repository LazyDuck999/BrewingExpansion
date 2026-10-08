using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace XIAOWOBrewingBarrel;

// The incubator's bottom padding wraps its original grid in another grid.
// The native helper only searches the outer grid at (0, 1), so restore the
// battery slot lookup from the preserved inner layout for our incubator.
[HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.GetBatterySlot))]
internal static class IncubatorBatterySlotPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameItem machine, ref GameSlotInventory __result)
    {
        if (__result != null || machine?.identifier != BrewingExpansion.BrewingExpansionMod.IncubatorId)
            return;
        var root = BrewingExpansion.IncubatorLayout.NativeLayout(machine.contentWindow);
        __result = root?.GetElement(0, 1)?.TryCast<GameSlotInventory>();
    }
}

// The game's RechargeMachineBattery dereferences GetBatterySlot without checking it.
// A saved machine with a content window but no battery slot would otherwise abort
// PlayerStore.EndNight before the incubator and other nightly patches can run.
[HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.RechargeMachineBattery))]
internal static class MissingMachineBatterySlotPatch
{
    private static readonly HashSet<long> ReportedItems = new();

    [HarmonyPrefix]
    private static bool Prefix(GameItem gameItem)
    {
        if (gameItem == null)
        {
            ReportOnce(0, "<null>", "<null>");
            return false;
        }

        if (gameItem.contentWindow == null) return true;

        try
        {
            if (MachineHelper.GetBatterySlot(gameItem) != null) return true;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[BE-NIGHT] 检查机器电池槽失败: " + ex.Message);
        }

        ReportOnce(gameItem.Pointer.ToInt64(), gameItem.identifier, gameItem.name);
        return false;
    }

    private static void ReportOnce(long pointer, string id, string name)
    {
        if (!ReportedItems.Add(pointer)) return;
        MelonLogger.Warning("[BE-NIGHT] 跳过缺少电池槽的机器夜间充电: id="
            + (id ?? "<null>") + " name=" + (name ?? "<null>"));
    }
}
