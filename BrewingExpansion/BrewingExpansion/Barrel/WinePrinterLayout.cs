using System;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class WinePrinterLayout
{
    internal static bool Ensure(GameItem item)
    {
        if (item?.identifier != BrewingExpansion.BrewingExpansionMod.WinePrinterId) return false;
        var shape = item.shape;
        if (shape == null || shape.width != 3 || shape.height != 3)
        {
            // SetShape is a concrete item operation; do not detour the shared
            // native getter/setter address to force a shape on every access.
            item.SetShape(new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build());
            return true;
        }
        var effective = item.modifiedShape;
        if (effective == null || effective.width != 3 || effective.height != 3)
        {
            item.ValidateShapeState();
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
internal static class WinePrinterLoadLayoutPatch
{
    [HarmonyPostfix]
    internal static void Postfix(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems)
    {
        // Native save restoration overwrites both base and effective shapes.
        // Repair this machine once after those writes have finished.
        for (var i = 0; savedItems != null && i < savedItems.Count; i++)
        {
            var node = savedItems[i];
            if (node?.identifier != BrewingExpansion.BrewingExpansionMod.WinePrinterId) continue;
            try { WinePrinterLayout.Ensure(node.tempLink); }
            catch (Exception ex) { Core.Log?.Warning("Wine printer saved shape repair: " + ex.Message); }
        }
    }
}
