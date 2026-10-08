using System;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class BarrelSlotRules
{
    // Only these machine inventories are restricted. Existing contents remain removable
    // when an older save contains an item that the new placement rules reject.
    internal static bool? Accepts(GameInventory slot, GameItem item)
    {
        if (slot == null || item == null) return null;
        var id = slot.identifier;
        if (id == null || !id.StartsWith("xiaowo_barrel_", StringComparison.Ordinal)
            && !id.StartsWith("xiaowo_concentrator_", StringComparison.Ordinal)) return null;
        if (item.parentInventory?.Pointer == slot.Pointer) return null;

        switch (id)
        {
            case "xiaowo_pulp_fruits":
                // This grid is stored inside pulp for brewing data, not an inventory
                // the player may put items into. Only the two creation paths may fill it.
                return PulpItem.IsFillingFruitGrid(slot) ? null : false;
            case "xiaowo_barrel_pulp":
            case "xiaowo_concentrator_pulp":
                return item.identifier == PulpItem.Id;
            case "xiaowo_barrel_bottles":
                return IsEmptyBottle(item);
            case "xiaowo_barrel_yeast":
                return IsYeast(item);
            case "xiaowo_barrel_products":
                return IsFinishedWine(item);
            case "xiaowo_concentrator_battery":
                return item.GetTagReadonly("power_source_item_max_energy") != null;
            case "xiaowo_concentrator_fruits":
                return item.identifier != PulpItem.Id && item.IsTag("WINE_INGREDIENT_TAG");
            default:
                return null;
        }
    }

    private static bool IsEmptyBottle(GameItem item)
    {
        if (item.identifier != "wine_bottle") return false;
        return !WineHelper.IsFermentingOrFinished(item)
            && WaterHelper.GetCurrentCapacityML(item) == 0
            && item.FindAllChildItems()?.Count == 0;
    }

    private static bool IsYeast(GameItem item)
    {
        var id = item.identifier;
        return id?.StartsWith("wine_yeast_", StringComparison.Ordinal) == true
            || id == "be_golden_yeast_reactor"
            || id == "be_advanced_yeast_reactor"
            || id == "be_forbidden_yeast_reactor";
    }

    private static bool IsFinishedWine(GameItem item)
    {
        return item.identifier == "wine_bottle" && WineHelper.IsFinishedWine(item);
    }
}
