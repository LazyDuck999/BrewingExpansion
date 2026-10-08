using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace XIAOWOBrewingBarrel;

// Bind the factory's native cycle callback, rather than detouring a compiler
// closure or rewriting another mod's generated native trampoline at runtime.
internal static class WineRackAgingOptimization
{
    internal static bool Enabled { get; set; } = true;
    internal static int BatchedCycles { get; private set; }
    private static readonly MethodInfo NativeCycle = FindNativeCycle();
    private static readonly MethodInfo[] DailyMethods = {
        AccessTools.Method(typeof(WineHelper), nameof(WineHelper.OnAgeWine)),
        AccessTools.Method(typeof(AgableHelper), nameof(AgableHelper.OnAge)),
        NativeCycle
    };

    private static MethodInfo FindNativeCycle()
    {
        var signature = new[] { typeof(GameItem), typeof(GameInventory), typeof(SlotMarker) };
        var candidates = typeof(MachineWineRack).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.Name.Contains("AgeWell") && m.Name.EndsWith("b__5") && m.ReturnType == typeof(void)
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(signature)).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    internal static string Overlap()
    {
        foreach (var method in DailyMethods)
        {
            if (method == null) return "missing-native-signature";
            var info = HarmonyLib.Harmony.GetPatchInfo(method);
            if (info == null) continue;
            foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers)
                .Concat(info.Finalizers).Concat(info.ILManipulators))
                if (patch.PatchMethod?.DeclaringType?.Assembly != typeof(WineRackAgingOptimization).Assembly)
                    return patch.owner ?? "unknown";
        }
        return null;
    }

    internal static void Bind(GameItem machine)
    {
        var original = machine?.onCycleEndSlotItemFunc;
        if (original == null || !NativeActionIdentity.IsSingleNative(original, 0x578810)) return;
        // The native wine rack's fixed grid roles are retained by its factory.
        // Reading them again each cycle also handles decoding and resized grids.
        var warned = false;
        machine.onCycleEndSlotItemFunc = DelegateSupport.ConvertDelegate<
            Il2CppSystem.Action<GameItem, GameInventory, SlotMarker>>(
            (Action<GameItem, GameInventory, SlotMarker>)((item, parent, slot) =>
            {
                var overlap = Enabled ? Overlap() : "disabled";
                var storage = LiunianAger.Storage(machine);
                var modules = LiunianAger.Modules(machine);
                var battery = LiunianAger.Battery(machine);
                if (overlap != null || storage == null || modules == null || battery == null)
                {
                    if (!warned)
                    {
                        warned = true;
                        Core.Log?.Msg("[BE-RACK-COMPAT] " + machine.identifier + " native fallback: "
                            + (overlap ?? "unrecognized-inventories"));
                    }
                    original.Invoke(item, parent, slot);
                    return;
                }
                BatchedCycles++;
                LiunianAgingCycle.RunRack(machine, storage, modules, battery);
            }));
    }
}

[HarmonyPatch(typeof(MachineWineRack), nameof(MachineWineRack.AgeWell))]
internal static class NativeWineRackBatchFactoryPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameItem __result)
    {
        try { WineRackAgingOptimization.Bind(__result); }
        catch (Exception ex) { Core.Log?.Warning("Wine rack retained its native callback: " + ex.Message); }
    }
}
