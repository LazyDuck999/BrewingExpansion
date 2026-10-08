using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.EndNight))]
internal static class XuxuBatteryNightPatch
{
    private static readonly List<GameItem> Candidates = new();
    private static readonly HashSet<long> NativeCharged = new();
    internal static bool InNight;

    [HarmonyPrefix]
    private static void Prefix()
    {
        Candidates.Clear();
        NativeCharged.Clear();
        InNight = false;
        try
        {
            foreach (var item in OwnedInventorySnapshot.Items())
            {
                try
                {
                    var rate = RechargeRate(item);
                    if (rate == 0) continue;
                    BrewingExpansion.BrewingExpansionMod.SetTagInt(item, "power_source_item_recharge", rate);
                    Candidates.Add(item);
                }
                catch (Exception ex) { Core.Log?.Warning("Skipping invalid xuxu battery: " + ex.Message); }
            }
            InNight = true;
        }
        catch (Exception ex)
        {
            Candidates.Clear();
            Core.Log?.Error("Xuxu battery pre-night scan failed: " + ex);
        }
    }

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        try
        {
            foreach (var item in Candidates)
            {
                try
                {
                    if (item == null || !item.IsTag(BrewingExpansion.BrewingExpansionMod.OwnedTag)
                        || NativeCharged.Contains(item.Pointer.ToInt64())) continue;
                    var rate = RechargeRate(item);
                    if (rate == 0) continue;
                    PowerHelper.ChargeBattery(item, rate);
                }
                catch (Exception ex) { Core.Log?.Warning("Skipping xuxu battery recharge: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.Log?.Error("Xuxu battery overnight recharge failed: " + ex); }
        finally
        {
            InNight = false;
            Candidates.Clear();
            NativeCharged.Clear();
        }
    }

    private static int RechargeRate(GameItem item) => item?.identifier switch
    {
        BrewingExpansion.BrewingExpansionMod.XuxuBatteryId => 6,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId => 8,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId => 12,
        _ => 0
    };

    internal static void MarkNativeCharge(GameItem item)
    {
        if (InNight && item != null) NativeCharged.Add(item.Pointer.ToInt64());
    }
}

[HarmonyPatch(typeof(PowerHelper), nameof(PowerHelper.ChargeBattery), new Type[] { typeof(GameItem) })]
internal static class XuxuNativeBatteryChargePatch
{
    [HarmonyPrefix]
    private static void Prefix(GameItem gameItem) => XuxuBatteryNightPatch.MarkNativeCharge(gameItem);
}
