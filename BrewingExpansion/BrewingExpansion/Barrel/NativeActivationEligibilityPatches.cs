using HarmonyLib;
using Il2Cpp;

namespace BrewingExpansion;

// Do not run native reflection on converted Func delegates. Only handle our own
// twelve item IDs; all other items retain the original eligibility pipeline.
[HarmonyPatch(typeof(GameItem), "CanActivateSlotItem")]
internal static class NativeActivationCanUsePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(GameItem __instance, ref bool __result)
    {
        if (__instance == null || !NativeItemActivation.IsSupported(__instance.identifier)) return true;
        __result = NativeItemActivation.CanActivate(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(GameItem), "MayActivateSlotItem")]
internal static class NativeActivationMayUsePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(GameItem __instance, ref bool __result)
    {
        if (__instance == null || !NativeItemActivation.IsSupported(__instance.identifier)) return true;
        __result = NativeItemActivation.CanActivate(__instance);
        return false;
    }
}
