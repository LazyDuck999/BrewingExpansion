using System;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace BrewingExpansion;

internal static class IncubatorLayout
{
    internal const int BottomMarginPixels = 36;
    internal const int EnergyPerNight = 12;

    internal static void EnsureEnergyUsage(GameItem item)
    {
        if (item?.identifier != BrewingExpansionMod.IncubatorId) return;
        if (item.GetTagReadonly("machinery_drawn_power")?.valueInt != EnergyPerNight)
            BrewingExpansionMod.SetTagInt(item, "machinery_drawn_power", EnergyPerNight);
        if (item.GetTagReadonly("MACHINERY_DRAWN_POWER_BASE")?.valueInt != EnergyPerNight)
            BrewingExpansionMod.SetTagInt(item, "MACHINERY_DRAWN_POWER_BASE", EnergyPerNight);
    }

    internal static void Configure(GameItem item)
    {
        if (item?.identifier != BrewingExpansionMod.IncubatorId) return;
        EnsureEnergyUsage(item);
        // Old saves retain the furnace's native processing callback.
        item.onCycleEndSlotItemFunc = null;
        var window = item.contentWindow;
        NativeWindowCallbacks.Bind(item);
        var root = NativeLayout(window);
        if (root == null) return;
        if (root.gridWidth == 4 && root.gridHeight == 2)
        {
            var resized = Resize(root);
            var addedMargin = EnsureBottomMargin(window);
            if (resized || addedMargin) RefreshWindowLayout(window);
            return;
        }
        if (root.gridWidth != 5 || root.gridHeight != 2) return;
        var module = root.GetElement(1, 1)?.TryCast<GameGridInventory>();
        if (module == null || module.childItems.Count != 0)
        {
            // Keep pre-existing module items accessible until the player removes them.
            XIAOWOBrewingBarrel.Core.Log?.Warning("Incubator module contains items; remove them before hiding its area.");
            return;
        }

        var kept = new PixelElement[4, 2];
        var oldColumns = new[] { 0, 2, 3, 4 };
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 2; y++)
                kept[x, y] = root.GetElement(oldColumns[x], y);
        if (kept[1, 1]?.TryCast<GameGridInventory>() == null
            || kept[2, 1]?.TryCast<GameGridInventory>() == null) return;

        if (!window.Detach(root.Cast<PixelElement>())) return;
        root.DetachAll();
        var compact = new GridPixelElement(4, 2, true);
        var attached = true;
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 2; y++)
                attached &= kept[x, y] != null && compact.AttachPos(kept[x, y], x, y);
        if (attached && window.Attach(compact.Cast<PixelElement>()))
        {
            Resize(compact);
            EnsureBottomMargin(window);
            RefreshWindowLayout(window);
            return;
        }

        compact.DetachAll();
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 2; y++)
                root.AttachPos(kept[x, y], oldColumns[x], y);
        window.Attach(root.Cast<PixelElement>());
        XIAOWOBrewingBarrel.Core.Log?.Error("Could not compact the incubator window; restored its original layout.");
    }

    private static bool Resize(GridPixelElement root)
    {
        var input = root.GetElement(1, 1)?.TryCast<GameGridInventory>();
        var output = root.GetElement(2, 1)?.TryCast<GameGridInventory>();
        if (input == null || output == null) return false;
        var changed = false;
        if (input.inventoryShape.width != 12 || input.inventoryShape.height != 12)
        {
            input.SetShape(12, 12);
            changed = true;
        }
        if (output.inventoryShape.width != 12 || output.inventoryShape.height != 12)
        {
            output.SetShape(12, 12);
            changed = true;
        }
        if (changed)
        {
            input.Validate();
            output.Validate();
            root.Validate();
        }
        return changed;
    }

    internal static GridPixelElement NativeLayout(PixelWindow window)
    {
        var root = window?.childElement?.TryCast<GridPixelElement>();
        if (root?.gridWidth == 1 && root.gridHeight == 2
            && root.GetElement(0, 1)?.TryCast<EmptyPixelElement>() != null)
            return root.GetElement(0, 0)?.TryCast<GridPixelElement>();
        return root;
    }

    private static bool EnsureBottomMargin(PixelWindow window)
    {
        var native = window?.childElement?.TryCast<GridPixelElement>();
        if (native?.gridWidth != 4 || native.gridHeight != 2) return false;
        var padded = new GridPixelElement(1, 2, true).SetSpacing(0, 0);
        var spacer = new EmptyPixelElement(1, BottomMarginPixels);
        if (!window.Detach(native.Cast<PixelElement>())) return false;
        if (padded.AttachPos(native.Cast<PixelElement>(), 0, 0)
            && padded.AttachPos(spacer.Cast<PixelElement>(), 0, 1)
            && window.Attach(padded.Cast<PixelElement>())) return true;
        padded.DetachAll();
        window.Attach(native.Cast<PixelElement>());
        XIAOWOBrewingBarrel.Core.Log?.Warning("Could not add incubator bottom margin; original layout restored.");
        return false;
    }

    private static void RefreshWindowLayout(PixelWindow window)
    {
        var native = NativeLayout(window);
        native?.Validate();
        var root = window?.childElement?.TryCast<GridPixelElement>();
        if (root != null && native != null && root.Pointer != native.Pointer) root.Validate();
        window?.Validate();
        if (window != null && window.IsVisible()) window.FixBounds();
    }

    internal static void Refresh(PixelWindow window)
    {
        if (window?.titleString != BrewingExpansionMod.IncubatorName) return;
        var root = NativeLayout(window);
        if (root?.gridWidth != 4) return;
        Resize(root);
        EnsureBottomMargin(window);
        RefreshWindowLayout(window);
    }

    internal static GameGridInventory Input(GameItem item) => Region(item, false);
    internal static GameGridInventory Output(GameItem item) => Region(item, true);

    private static GameGridInventory Region(GameItem item, bool output)
    {
        var root = NativeLayout(item?.contentWindow);
        if (root == null || root.gridHeight != 2) return null;
        if (root.gridWidth == 4) return root.GetElement(output ? 2 : 1, 1)?.TryCast<GameGridInventory>();
        if (root.gridWidth == 5) return root.GetElement(output ? 3 : 2, 1)?.TryCast<GameGridInventory>();
        return null;
    }
}
