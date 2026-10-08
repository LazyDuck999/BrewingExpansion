using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class ConcentratorItem
{
    internal const string Id = "be_fruit_pulp_concentrator";
    private const string BatteryId = "xiaowo_concentrator_battery";
    private const string FruitId = "xiaowo_concentrator_fruits";
    private const string PulpId = "xiaowo_concentrator_pulp";
    private const int EnergyPerPulp = 8;
    private static readonly HashSet<long> ConfiguredBatterySlots = new();
    private static readonly HashSet<long> PendingOutputResizeWarnings = new();
    private static readonly HashSet<long> MissingBatteryWarnings = new();
    private static readonly Dictionary<long, TagElement> Statuses = new();

    internal static GameItem Create()
    {
        // A native item supplies the usual item selection, drag and save behavior.
        var item = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.VanillaAdvFilterId, false)
            ?? throw new InvalidOperationException("Could not create a native item for the pulp concentrator.");
        item.identifier = Id;
        item.SetName(BarrelItem.Text("原浆浓缩器", "Pulp Concentrator"));
        item.SetValue(500L);
        item.shortDescription = BarrelItem.Text(
    "蒸汽上升的时候，一切轻盈的东西先离开。\n\n" +
    "留在底部的，是最重、最沉、最不肯走的那些——那就是原浆。\n\n" +
    "浓缩不是加法，是减法。\n\n" +
    "——装入电池与四枚相同果实，耗电 8 点，得一份原浆。",
    "When steam rises, everything light leaves first.\n\n" +
    "What stays at the bottom is the heaviest, the densest, the part that refuses to go — that is the pulp.\n\n" +
    "Concentration is not addition. It is subtraction.\n\n" +
    "— Insert a battery and four matching fruits. Spends 8 energy to yield one pulp.");
        item.longDescription = item.shortDescription;
        item.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetGameItemType("MACHINE");
        RemoveFilterMetadata(item);
        item.SetSprite(BarrelItem.SpriteAtlas, "concentrator");
        BuildWindow(item);
        item.ValidateShapeState();
        return item;
    }

    private static void RemoveFilterMetadata(GameItem item)
    {
        if (item == null) return;
        // The filter template restores durability each night. This machine has no
        // durability state, so its inherited cycle callback must not run.
        item.onCycleEndSlotItemFunc = null;
        item.flavorText = string.Empty;
        if (item.itemTypes == null || item.itemTypes.Count != 1 || item.itemTypes[0] != "MACHINE")
        {
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            types.Add("MACHINE");
            item.itemTypes = types;
        }
        foreach (var tag in new[]
        {
            "HOUSEHOLD_GOOD", "FILTER", "WATER_PURIFICATION_SUPPLY", "LUXURY_ITEM",
            "FILTER_SIZE_TAG", "WATER_FILTER", "DURABILITY_TAG", "DURABILITY_FAKE_TAG",
            "MAX_DURABILITY_TAG", "DEFAULT_DURABILITY_TAG",
        })
            if (item.IsTag(tag)) item.DisableTag(tag, false);
        if (item.itemFeatures != null)
            for (var i = item.itemFeatures.Count - 1; i >= 0; i--)
                if (item.itemFeatures[i]?.identifier == "DURABILITY_TAG")
                    item.itemFeatures.RemoveAt(i);
    }

    private static void BuildWindow(GameItem item)
    {
        var battery = new GameSlotInventory(1, 2, false, false) { identifier = BatteryId };
        var fruits = new GameGridInventory(8, 2) { identifier = FruitId };
        var pulp = new GameGridInventory(2, 2) { identifier = PulpId };
        var status = new TagElement(-1).SetText(BarrelItem.Text("等待果实与电池", "Waiting for fruit and battery"));
        var root = new GridPixelElement(3, 3, true).SetSpacing(3, 3);
        root.Attach(new TagElement(-1).SetText(BarrelItem.Text("电池区", "Battery Area")).Cast<PixelElement>(), 0, 0);
        root.Attach(new TagElement(-1).SetText(BarrelItem.Text("果实区", "Fruit Area")).Cast<PixelElement>(), 1, 0);
        root.Attach(new TagElement(-1).SetText(BarrelItem.Text("原浆区", "Pulp Area")).Cast<PixelElement>(), 2, 0);
        root.Attach(battery.Cast<PixelElement>(), 0, 1);
        root.Attach(fruits.Cast<PixelElement>(), 1, 1);
        root.Attach(pulp.Cast<PixelElement>(), 2, 1);
        root.Attach(new ButtonElement(BarrelItem.Text("开始浓缩", "Concentrate"), 120, -1,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => Concentrate(item)))).Cast<PixelElement>(), 1, 2);
        root.Attach(status.Cast<PixelElement>(), 2, 2);
        var window = new PixelWindow(true);
        window.SetTitle(BarrelItem.Text("原浆浓缩器", "Pulp Concentrator"));
        window.Attach(root.Cast<PixelElement>());
        item.SetContentWindow(window);
        BrewingExpansion.NativeWindowCallbacks.Bind(item);
        MachineHelper.SetupBatterySlot(battery, item);
        ConfiguredBatterySlots.Add(item.Pointer.ToInt64());
        Statuses[item.Pointer.ToInt64()] = status;
    }

    private static GameGridInventory FindGrid(GameItem item, string identifier)
    {
        if (item?.contentWindow?.child == null) return null;
        var grids = GraphUtils.FindAllChildrenType<GameGridInventory>(item.contentWindow.child,
            (Il2CppSystem.Func<GameGridInventory, bool>)(grid => grid?.identifier == identifier));
        return grids != null && grids.Count > 0 ? grids[0] : null;
    }

    internal static GameGridInventory Fruits(GameItem item) => FindGrid(item, FruitId);
    internal static GameGridInventory Pulps(GameItem item) => FindGrid(item, PulpId);

    internal static GameSlotInventory Battery(GameItem item)
    {
        if (item?.contentWindow?.child == null) return null;
        var slots = GraphUtils.FindAllChildrenType<GameSlotInventory>(item.contentWindow.child,
            (Il2CppSystem.Func<GameSlotInventory, bool>)(slot => slot?.identifier == BatteryId));
        return slots != null && slots.Count > 0 ? slots[0] : null;
    }

    internal static void EnsureLoaded(GameItem item)
    {
        if (item?.identifier == Id) BrewingExpansion.NativeWindowCallbacks.Bind(item);
        if (item?.identifier != Id) return;
        RemoveFilterMetadata(item);
        if (item.spriteAtlasPath != BarrelItem.SpriteAtlas || item.spritePath != "concentrator")
            item.SetSprite(BarrelItem.SpriteAtlas, "concentrator");
        var key = item.Pointer.ToInt64();
        AlignWindowControls(item.contentWindow);
        var output = Pulps(item);
        foreach (var produced in InventoryOps.Snapshot(output)) PulpItem.EnsureProduced(produced);
        if (output != null && (output.inventoryShape.width != 2 || output.inventoryShape.height != 2))
        {
            if ((output.childItems?.Count ?? 0) == 0 && (output.items?.Count ?? 0) == 0)
            {
                output.SetShape(2, 2);
                PendingOutputResizeWarnings.Remove(key);
            }
            else if (PendingOutputResizeWarnings.Add(key))
                Core.Log?.Warning("Concentrator output resize is pending until the grid is empty: " + item.uniqueId);
        }
        if (ConfiguredBatterySlots.Contains(key)) return;
        var slot = Battery(item);
        if (slot == null)
        {
            if (MissingBatteryWarnings.Add(key))
                Core.Log?.Warning("Concentrator battery slot is missing: " + item.uniqueId);
            return;
        }
        MachineHelper.SetupBatterySlot(slot, item);
        ConfiguredBatterySlots.Add(key);
        MissingBatteryWarnings.Remove(key);
    }

    internal static void AlignWindowControls(PixelWindow window)
    {
        if (window == null ||
            (window.titleString != "原浆浓缩器" && window.titleString != "Pulp Concentrator")) return;
        // IsVisible() can throw while a saved item's window has not been built yet.
        // The layout only needs a live, visible background to position controls.
        RectTransform bounds;
        try
        {
            bounds = window.background?.rectTransform;
            if (bounds == null || !bounds.gameObject.activeInHierarchy) return;
        }
        catch (Exception) { return; }
        var root = window.childElement?.TryCast<GridPixelElement>();
        if (root == null || window.widthPixels <= 0) return;
        AlignIngredientColumns(window, root, bounds);

        ButtonElement button = null;
        TagElement status = null;
        for (var x = 0; x < 3; x++)
        {
            var element = root.GetElement(x, 2);
            button ??= element?.TryCast<ButtonElement>();
            status ??= element?.TryCast<TagElement>();
        }
        if (button?.handler?.rectTransform == null || status?.handler?.rectTransform == null) return;

        var corners = new Il2CppStructArray<Vector3>(4);
        bounds.GetWorldCorners(corners);
        var scale = (corners[2].x - corners[0].x) / window.widthPixels;
        var buttonWidth = (button.forceWidth > 0 ? button.forceWidth : button.widthPixels) * scale;
        var statusWidth = status.widthPixels * scale;
        var gap = 6f * scale;
        var buttonLeft = (corners[0].x + corners[2].x - buttonWidth - gap - statusWidth) * .5f;
        MoveLeftEdgeTo(button.handler.rectTransform, buttonLeft);
        MoveLeftEdgeTo(status.handler.rectTransform, buttonLeft + buttonWidth + gap);
        BarrelItem.CenterHorizontally(button.textNode?.rectTransform, button.buttonNode?.rectTransform);
    }

    private static void AlignIngredientColumns(PixelWindow window, GridPixelElement root, RectTransform bounds)
    {
        var battery = root.GetElement(0, 1)?.TryCast<GameSlotInventory>();
        var fruits = root.GetElement(1, 1)?.TryCast<GameGridInventory>();
        var pulp = root.GetElement(2, 1)?.TryCast<GameGridInventory>();
        if (battery?.handler?.rectTransform == null || fruits?.handler?.rectTransform == null ||
            pulp?.handler?.rectTransform == null) return;

        var corners = new Il2CppStructArray<Vector3>(4);
        bounds.GetWorldCorners(corners);
        var leftEdge = corners[0].x;
        var rightEdge = corners[2].x;
        var scale = (rightEdge - leftEdge) / window.widthPixels;
        battery.handler.rectTransform.GetWorldCorners(corners);
        var leftMargin = corners[0].x - leftEdge;
        pulp.handler.rectTransform.GetWorldCorners(corners);
        var rightMargin = rightEdge - (corners[0].x + pulp.widthPixels * scale);
        var delta = rightMargin - leftMargin;
        if (Math.Abs(delta) < .1f) return;

        BarrelItem.ShiftHorizontally(root.GetElement(1, 0)?.handler?.rectTransform, delta);
        BarrelItem.ShiftHorizontally(fruits.handler.rectTransform, delta);
        BarrelItem.ShiftHorizontally(root.GetElement(2, 0)?.handler?.rectTransform, delta);
        BarrelItem.ShiftHorizontally(pulp.handler.rectTransform, delta);
    }

    private static void MoveLeftEdgeTo(RectTransform target, float worldX)
    {
        var corners = new Il2CppStructArray<Vector3>(4);
        target.GetWorldCorners(corners);
        BarrelItem.ShiftHorizontally(target, worldX - corners[0].x);
    }

    private static void SetStatus(GameItem item, string zh, string en)
    {
        if (!Statuses.TryGetValue(item.Pointer.ToInt64(), out var status))
        {
            var tags = GraphUtils.FindAllChildrenType<TagElement>(item.contentWindow.child,
                (Il2CppSystem.Func<TagElement, bool>)(_ => true));
            if (tags == null || tags.Count < 4) return;
            status = tags[tags.Count - 1];
            Statuses[item.Pointer.ToInt64()] = status;
        }
        status.SetText(BarrelItem.Text(zh, en));
    }

    internal static bool Concentrate(GameItem item)
    {
        var battery = Battery(item)?.currentItem;
        var fruits = Fruits(item);
        var output = Pulps(item);
        if (battery == null || fruits == null || output == null || !PowerHelper.CanDrawPowerSource(battery, EnergyPerPulp))
        {
            SetStatus(item, "电池电量不足（需要8）", "Battery needs 8 energy");
            return false;
        }

        var groups = new Dictionary<string, List<GameItem>>();
        foreach (var fruit in InventoryOps.Snapshot(fruits))
        {
            if (fruit == null || !fruit.IsTag("WINE_INGREDIENT_TAG")) continue;
            if (!groups.TryGetValue(fruit.identifier, out var group))
                groups[fruit.identifier] = group = new List<GameItem>();
            group.Add(fruit);
        }
        List<GameItem> selected = null;
        foreach (var group in groups.Values)
            if (group.Count >= 4) { selected = group; break; }
        if (selected == null)
        {
            SetStatus(item, "需要四个相同果实", "Needs four matching fruits");
            return false;
        }

        var pulp = PulpItem.Create();
        if (!output.MayHaveValidInventorySlot(pulp))
        {
            pulp.Destroy();
            SetStatus(item, "原浆区已满", "Pulp area is full");
            return false;
        }
        var pulpFruits = PulpItem.Fruits(pulp);
        var moved = new List<GameItem>();
        for (var i = 0; i < 4; i++)
        {
            if (!PulpItem.MoveFruitIntoPulp(fruits, pulpFruits, selected[i]))
            {
                RollBack(fruits, pulpFruits, moved, pulp);
                SetStatus(item, "果实转移失败", "Could not move fruit");
                return false;
            }
            moved.Add(selected[i]);
        }

        var value = 0L;
        foreach (var fruit in moved) value += fruit.unitValue;
        pulp.SetName(BarrelItem.Text(selected[0].name + "原浆", selected[0].name + " Pulp"));
        PulpItem.SetPulpDescription(pulp, selected[0].identifier);
        PulpItem.EnsureVisual(pulp);
        pulp.SetValue(value);
        if (!PowerHelper.DrawPowerSource(battery, EnergyPerPulp))
        {
            RollBack(fruits, pulpFruits, moved, pulp);
            SetStatus(item, "电池电量不足（需要8）", "Battery needs 8 energy");
            return false;
        }
        if (!InventoryOps.Accept(output, pulp))
        {
            PowerHelper.ChargeBattery(battery, EnergyPerPulp);
            RollBack(fruits, pulpFruits, moved, pulp);
            SetStatus(item, "原浆区已满", "Pulp area is full");
            return false;
        }
        var fruitId = selected[0].identifier;
        PulpItem.EnsureProduced(pulp);
        if (pulp.contentWindow != null || PulpItem.FruitId(pulp) != fruitId)
        {
            output.Expel(pulp);
            PowerHelper.ChargeBattery(battery, EnergyPerPulp);
            RollBack(fruits, pulpFruits, moved, pulp);
            SetStatus(item, "原浆封装失败", "Could not seal fruit pulp");
            return false;
        }
        SetStatus(item, "已产出一份原浆（耗电8）", "Produced one pulp (8 energy)");
        Core.Log?.Msg("Concentrated four " + fruitId + " into pulp for 8 energy.");
        return true;
    }

    private static void RollBack(GameGridInventory fruits, GameGridInventory pulpFruits, List<GameItem> moved, GameItem pulp)
    {
        var restored = true;
        foreach (var fruit in moved) restored &= InventoryOps.Move(pulpFruits, fruits, fruit);
        if (restored) pulp.Destroy();
        else Core.Log?.Error("Could not restore all fruits after a failed concentration; pulp " + pulp.uniqueId + " was preserved.");
    }
}
