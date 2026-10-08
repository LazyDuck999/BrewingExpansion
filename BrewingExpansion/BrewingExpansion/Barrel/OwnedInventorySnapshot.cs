using System.Collections.Generic;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class OwnedInventorySnapshot
{
    internal static List<GameItem> Items()
    {
        var result = new List<GameItem>();
        var seen = new HashSet<long>();
        var store = PlayerStore.instance;
        if (store == null) return result;
        try
        {
            var gameItems = store.FindAllItem(true);
            for (var i = 0; gameItems != null && i < gameItems.Count; i++)
                Add(result, seen, gameItems[i]);
        }
        catch (System.Exception ex) { Core.Log?.Warning("Owned item scan failed: " + ex.Message); }
        try
        {
            var backpack = store.gridInv?.childItems;
            for (var i = 0; backpack != null && i < backpack.Count; i++)
            {
                var root = backpack[i];
                Add(result, seen, root);
                var children = root?.FindAllChildItems();
                for (var j = 0; children != null && j < children.Count; j++)
                    Add(result, seen, children[j]);
            }
        }
        catch (System.Exception ex) { Core.Log?.Warning("Backpack item scan failed: " + ex.Message); }
        return result;
    }

    private static void Add(List<GameItem> result, HashSet<long> seen, GameItem item)
    {
        try
        {
            if (item != null && item.IsTag(BrewingExpansion.BrewingExpansionMod.OwnedTag)
                && seen.Add(item.Pointer.ToInt64()))
                result.Add(item);
        }
        catch (System.Exception ex) { Core.Log?.Warning("Skipping invalid owned item: " + ex.Message); }
    }
}
