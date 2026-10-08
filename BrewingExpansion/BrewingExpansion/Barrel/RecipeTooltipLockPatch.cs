using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace BrewingExpansion;

// Native GetTooltipBasic appends more descriptions AFTER OnCreateTooltipLate.
// Apply the lock to the final RichText instead. GetTooltipAdvanced shares this
// same native implementation/ABI; patch the address once through GetTooltip.
[HarmonyPatch(typeof(GameItemElement), "GetTooltip")]
internal static class RecipeTooltipLockPatch
{
    [HarmonyPostfix]
    internal static void Postfix(GameItemElement __instance, ref RichText __result)
    {
        if (__instance == null || !BrewingExpansionMod.IsOurBrewingDocument(__instance.identifier)) return;
        bool owned = false;
        try { owned = PlayerStore.Instance?.IsPlayerOwnThisItem(__instance.identifier) == true; }
        catch (Exception ex) { MelonLogger.Warning("[BE-RECIPE] Ownership check failed; keep locked: " + ex.Message); }
        if (owned) return;
        var builder = new RichTextBuilder();
        builder.AddLine(BrewingExpansionMod.RecipeLockedHint);
        __result = builder.Build();
    }
}
