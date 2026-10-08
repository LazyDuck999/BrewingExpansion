using System;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

// Resolve old IDs at creation/load boundaries without publishing duplicate
// factories in the item catalogue. Internal inventory IDs remain unchanged.
internal static class LegacyItemMigration
{
    internal const string BarrelId = "xiaowo_large_brewing_barrel";
    internal const string PulpId = "xiaowo_brewing_fruit_pulp";
    internal const string ConcentratorId = "xiaowo_fruit_pulp_concentrator";
    internal const string LiunianId = "xiaowo_liunian_ager";

    internal static bool IsLegacy(string id) => id == BarrelId || id == PulpId
        || id == ConcentratorId || id == LiunianId;

    internal static string CurrentId(string id) => id switch
    {
        BarrelId => BarrelItem.Id,
        PulpId => PulpItem.Id,
        ConcentratorId => ConcentratorItem.Id,
        LiunianId => LiunianAger.Id,
        _ => id,
    };

    internal static int MigrateSavedNodes(Il2CppSystem.Collections.Generic.List<SaveItemNode> nodes)
    {
        var migrated = 0;
        for (var i = 0; nodes != null && i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null || !IsLegacy(node.identifier)) continue;
            // UUIDs, contents, tags, shapes and inventory links belong to the
            // saved object. Only its factory key changes before native decode.
            node.identifier = CurrentId(node.identifier);
            migrated++;
        }
        return migrated;
    }

    internal static bool Migrate(GameItem item)
    {
        if (item == null) return false;
        if (!IsLegacy(item.identifier)) return false;
        item.identifier = CurrentId(item.identifier);
        return true;
    }

    internal static void MigrateOwnedItems()
    {
        try
        {
            var items = PlayerStore.instance?.FindAllItem(true);
            if (items == null) return;
            var migrated = 0;
            for (var i = 0; i < items.Count; i++)
            {
                try { if (Migrate(items[i])) migrated++; }
                catch (Exception ex) { Core.Log?.Warning("Could not migrate legacy item " + i + ": " + ex.Message); }
            }
            if (migrated > 0) Core.Log?.Msg("Migrated " + migrated + " legacy brewing item IDs to be_.");
        }
        catch (Exception ex) { Core.Log?.Warning("Legacy brewing item migration failed: " + ex); }
    }
}

// Direct requests using an old item ID also create the current item. This
// concrete native entry point is used by SaveManager.DecodeNodes as well.
[HarmonyLib.HarmonyPatch(typeof(DirectoryMaster), nameof(DirectoryMaster.Item))]
internal static class LegacyItemCreationPatch
{
    private static void Prefix(ref string identifier)
    {
        identifier = LegacyItemMigration.CurrentId(identifier);
    }
}
