using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace BrewingExpansion;

// These empty-item factories own their activation callbacks; no template effect is retained.
internal static class NativeItemActivation
{
    private static readonly Il2CppSystem.Action<GameItem, GameInventory, SlotMarker> Use =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameItem, GameInventory, SlotMarker>>(
            (Action<GameItem, GameInventory, SlotMarker>)Activate);

    internal static void Bind(GameItem item)
    {
        if (item == null || !IsSupported(item.identifier)) return;
        // Native eligibility uses InvokeAllReduce -> Delegate.DynamicInvoke. Converted
        // managed Func targets fail native reflection with TargetException on this build.
        // These empty-item factories have no native eligibility callback to preserve.
        item.canActivateSlotItemFunc = null;
        item.mayActivateSlotItemFunc = null;
        item.onActivateSlotItemFunc = Use;
    }

    internal static bool IsSupported(string id) => id is
        BrewingExpansionMod.MyPerfumeId or BrewingExpansionMod.FreedomPerfumeId or
        BrewingExpansionMod.PremiumBlindboxId or BrewingExpansionMod.MidautumnBlindboxId or
        BrewingExpansionMod.MidautumnMooncakeBoxId or BrewingExpansionMod.LowBlindboxId or
        BrewingExpansionMod.MidBlindboxId or BrewingExpansionMod.AfterglowBlindboxId or
        BrewingExpansionMod.WineBlindboxId or BrewingExpansionMod.ContainerBlindboxId or
        BrewingExpansionMod.FigurineBlindboxId or BrewingExpansionMod.CassetteBundleId;

    internal static bool CanActivate(GameItem item)
    {
        try
        {
            return item != null && IsSupported(item.identifier)
                && BrewingExpansionMod.IsOwnedByPlayerPublic(item)
                && (!BrewingExpansionMod.IsPerfume(item) || BrewingExpansionMod.GetPerfumeUses(item) > 0);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[BE-NATIVE] Activation check failed: " + ex.Message);
            return false;
        }
    }

    private static void Activate(GameItem item, GameInventory inventory, SlotMarker slot)
    {
        if (!CanActivate(item)) return;
        try
        {
            // Existing operations keep their cooldown, capacity, consumption and reward rules.
            switch (item.identifier)
            {
                case BrewingExpansionMod.MyPerfumeId:
                case BrewingExpansionMod.FreedomPerfumeId: BrewingExpansionMod.UseMyPerfume(item); break;
                case BrewingExpansionMod.PremiumBlindboxId: BrewingExpansionMod.OpenPremiumBlindbox(item); break;
                case BrewingExpansionMod.MidautumnBlindboxId: BrewingExpansionMod.OpenMidautumnBlindbox(item); break;
                case BrewingExpansionMod.MidautumnMooncakeBoxId: BrewingExpansionMod.OpenMidautumnMooncakeBox(item); break;
                case BrewingExpansionMod.LowBlindboxId: BrewingExpansionMod.OpenLowBlindbox(item); break;
                case BrewingExpansionMod.MidBlindboxId: BrewingExpansionMod.OpenMidBlindbox(item); break;
                case BrewingExpansionMod.AfterglowBlindboxId: BrewingExpansionMod.OpenAfterglowBlindbox(item); break;
                case BrewingExpansionMod.WineBlindboxId: BrewingExpansionMod.OpenWineBlindbox(item); break;
                case BrewingExpansionMod.ContainerBlindboxId: BrewingExpansionMod.OpenContainerBlindbox(item); break;
                case BrewingExpansionMod.FigurineBlindboxId: BrewingExpansionMod.OpenFigurineBlindbox(item); break;
                case BrewingExpansionMod.CassetteBundleId: BrewingExpansionMod.OpenCassetteBundle(item); break;
            }
        }
        catch (Exception ex) { MelonLogger.Warning("[BE-NATIVE] Item use failed: " + ex); }
    }
}
