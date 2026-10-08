using System;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

// The native Inventor weighted-item callback adds DiscountBuy15. Scope the
// exclusion to that shop visit, that unowned item and its weighted-area call.
// Keep the native callback chain (including feature exposure) intact.
internal static class NightShopDoctorDiscount
{
    [ThreadStatic] private static IntPtr _item;
    private static string _discountId;

    internal static IntPtr Begin(GameItem item, bool brewingVisit, bool ignoreCallback)
    {
        var previous = _item;
        _item = IntPtr.Zero;
        try
        {
            if (brewingVisit && !ignoreCallback && item != null && !GeneralHelper.IsItemOwned(item))
            {
                _discountId ??= ItemFeatureList.DiscountBuy15()?.identifier;
                if (!string.IsNullOrEmpty(_discountId)) _item = item.Pointer;
            }
        }
        catch (Exception ex) { Core.Log?.Warning("NIGHT_SHOP discount isolation unavailable: " + ex.Message); }
        return previous;
    }

    internal static void End(IntPtr previous) => _item = previous;

    internal static bool MayAdd(GameItem item, ItemFeature feature) => _item == IntPtr.Zero
        || item == null || item.Pointer != _item || feature == null || feature.identifier != _discountId;
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.OnAddedToWeightedArea))]
internal static class NightShopDoctorDiscountScopePatch
{
    private static void Prefix(GameItem __instance, bool ignoreOnItemAddedToWeighted, out IntPtr __state)
        => __state = NightShopDoctorDiscount.Begin(__instance, NightBrewingShop.IsBrewingVisit, ignoreOnItemAddedToWeighted);

    // Finalizer balances the scope even if native callbacks throw or nest.
    [HarmonyFinalizer]
    private static void Finalizer(IntPtr __state) => NightShopDoctorDiscount.End(__state);
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.AddItemFeature))]
internal static class NightShopDoctorDiscountFeaturePatch
{
    private static bool Prefix(GameItem __instance, ItemFeature itemFeature)
        => NightShopDoctorDiscount.MayAdd(__instance, itemFeature);
}
