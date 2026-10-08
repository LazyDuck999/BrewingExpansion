using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class PulpItem
{
    internal const string Id = "be_brewing_fruit_pulp";
    private const string FruitInventoryId = "xiaowo_pulp_fruits";
    private const string FruitTagPrefix = "XIAOWO_PULP_FRUIT_";
    private static readonly HashSet<long> PendingShapeWarnings = new();
    private static readonly HashSet<long> RefreshedSprites = new();
    private static readonly HashSet<long> FillingFruitGrids = new();
    private static readonly HashSet<long> PendingRecoveryWarnings = new();
    private static readonly string[] PulpTextureNames =
    {
        "辣椒", "水稻", "小麦", "苦艾草", "花椒", "柠檬", "黄葡萄", "甘蔗",
        "黄瓜", "荔枝", "茉莉花", "青提", "桂花", "杨梅", "虚空李", "星尘莓",
    };

    private static readonly Dictionary<string, string> PulpDescZh = new()
{
    { "chili_fruit",          "火留在了这里，比火更久。" },
    { "rice_fruit",           "一碗饭，熬成了半勺甜。" },
    { "wheat_fruit",          "麦香不再飘散，它沉住了。" },
    { "wormwood_fruit",       "苦从草里走出来，藏进水里。" },
    { "sichuan_pepper_fruit", "麻不再刺痛，只是不肯散。" },
    { "lemon_fruit",          "酸得比昨天更安静，也更利。" },
    { "yellow_grape_fruit",   "阳光被收成了琥珀。" },
    { "sugarcane_fruit",      "甜从硬壳里出来，学会了低声。" },
    { "cucumber_fruit",       "清凉从地里走进瓶里。" },
    { "lychee_fruit",         "远方的甜，被攥紧了一寸。" },
    { "jasmine_fruit",        "花香不再飘，沉到水里去了。" },
    { "green_grape_fruit",    "霜意还在，但已经不是霜。" },
    { "osmanthus_fruit",      "香不再撒出去，聚成一小团。" },
    { "bayberry_fruit",       "等过一整个夏天，酸终于肯安静。" },
    { "void_plum_fruit",      "沉下去的东西，这里装得下。" },
    { "stardust_berry_fruit", "光被拧紧，成了一粒。" },
};

    private static readonly Dictionary<string, string> PulpDescEn = new()
{
    { "chili_fruit",          "Fire stays here, longer than fire." },
    { "rice_fruit",           "A whole bowl of rice, boiled down to half a spoon of sweetness." },
    { "wheat_fruit",          "The scent of wheat no longer drifts — it has settled." },
    { "wormwood_fruit",       "Bitterness steps out of the herb, and hides in the water." },
    { "sichuan_pepper_fruit", "The numbness no longer stings — it simply refuses to leave." },
    { "lemon_fruit",          "Sour — quieter than yesterday, and keener." },
    { "yellow_grape_fruit",   "Sunlight, gathered into amber." },
    { "sugarcane_fruit",      "Sweetness steps out of its hard shell, and learns to speak softly." },
    { "cucumber_fruit",       "Coolness walks out of the soil and into the bottle." },
    { "lychee_fruit",         "A sweetness from afar, clenched one inch tighter." },
    { "jasmine_fruit",        "The fragrance no longer drifts — it has sunk into the water." },
    { "green_grape_fruit",    "The frost is still here, but it is no longer frost." },
    { "osmanthus_fruit",      "The fragrance no longer scatters — it gathers into one small knot." },
    { "bayberry_fruit",       "After a whole summer of waiting, the sourness finally agrees to be quiet." },
    { "void_plum_fruit",      "What sinks, this can hold." },
    { "stardust_berry_fruit", "Light, wrung tight into a single grain." },
};

    private const string PulpFallbackZh = "由四个相同果实浓缩；两份原浆可酿四瓶酒。";
    private const string PulpFallbackEn = "Concentrated from four matching fruits; two pulps brew four bottles.";

    internal static void SetPulpDescription(GameItem pulp, string fruitId)
    {
        if (pulp == null) return;
        string zh, en;
        if (fruitId == null
            || !PulpDescZh.TryGetValue(fruitId, out zh)
            || !PulpDescEn.TryGetValue(fruitId, out en))
        {
            zh = PulpFallbackZh;
            en = PulpFallbackEn;
        }
        pulp.shortDescription = BarrelItem.Text(zh, en);
        pulp.longDescription = pulp.shortDescription;
    }


    internal static GameItem Create()
    {
        var item = ItemDirectory.CreateEmptyItem(Id) ?? throw new InvalidOperationException("Could not allocate fruit pulp.");
        item.SetName(BarrelItem.Text("果实原浆", "Fruit Pulp"));
        item.SetValue(0L);
        item.shortDescription = BarrelItem.Text("由四个相同果实浓缩；两份原浆可酿四瓶酒。",
            "Concentrated from four matching fruits; two pulps brew four bottles.");
        item.longDescription = item.shortDescription;
        item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetGameItemType("FOOD");
        var template = DirectoryMaster.Item("wine_bottle", false);
        if (template != null) item.SetSprite(template.spriteAtlasPath, template.spritePath);
        var fruitGrid = new GameGridInventory(4, 4) { identifier = FruitInventoryId };
        var window = new PixelWindow(true);
        window.SetTitle(BarrelItem.Text("原浆中的果实", "Fruit in pulp"));
        window.Attach(fruitGrid.Cast<PixelElement>());
        item.SetContentWindow(window);
        item.ValidateShapeState();
        return item;
    }

    internal static void EnsureProduced(GameItem item)
    {
        if (item?.identifier != Id) return;
        if (!GeneralHelper.IsItemOwned(item)) GeneralHelper.SetItemOwned(item, true);
        // Old test builds could persist this flag. It disables the whole item,
        // including normal inventory interactions, so only block activation below.
        item.forceDisableActivate = false;
        if (item.contentWindow?.IsVisible() == true) item.contentWindow.Hide();
        RescueForeignContents(item);
        SealContents(item);
        EnsureShape(item);
        EnsureVisual(item);
    }

    private static void SealContents(GameItem pulp)
    {
        var grid = Fruits(pulp);
        if (grid == null) return;
        var contents = InventoryOps.Snapshot(grid);
        var fruitId = contents.Count == 0 ? null : LegacyFruitId(grid);
        if (contents.Count != 0 && fruitId == null) return;
        if (fruitId != null && !pulp.IsTag(FruitTagPrefix + fruitId)
            && !pulp.EnableTag(FruitTagPrefix + fruitId, true)) return;

        var detached = new List<GameItem>();
        foreach (var fruit in contents)
        {
            if (!grid.Expel(fruit))
            {
                foreach (var removed in detached) RestoreRejectedMove(grid, removed);
                Core.Log?.Warning("Could not seal pulp fruit data: " + pulp.uniqueId);
                return;
            }
            detached.Add(fruit);
        }
        try { pulp.SetContentWindow(null); }
        catch (Exception ex)
        {
            foreach (var removed in detached) RestoreRejectedMove(grid, removed);
            Core.Log?.Warning("Could not remove pulp container window: " + ex.Message);
            return;
        }
        foreach (var fruit in detached) fruit.Destroy();
        Core.Log?.Msg("Sealed pulp " + pulp.uniqueId + " as " + (fruitId ?? "empty") + "; no container window remains.");
    }

    internal static bool IsFillingFruitGrid(GameInventory grid) =>
        grid != null && grid.identifier == FruitInventoryId
        && FillingFruitGrids.Contains(grid.Pointer.ToInt64());

    internal static bool MoveFruitIntoPulp(GameGridInventory source, GameGridInventory target, GameItem fruit)
    {
        if (source == null || target?.identifier != FruitInventoryId || fruit == null
            || !fruit.IsTag("WINE_INGREDIENT_TAG")) return false;
        var current = InventoryOps.Snapshot(target);
        if (current.Count >= 4) return false;
        foreach (var existing in current)
            if (existing.identifier != fruit.identifier) return false;
        var key = target.Pointer.ToInt64();
        FillingFruitGrids.Add(key);
        try { return InventoryOps.Move(source, target, fruit); }
        finally { FillingFruitGrids.Remove(key); }
    }

    internal static bool RestoreRejectedMove(GameGridInventory target, GameItem item)
    {
        if (target?.identifier != FruitInventoryId || item == null) return false;
        var key = target.Pointer.ToInt64();
        FillingFruitGrids.Add(key);
        try { return target.UncheckedAccept(item); }
        finally { FillingFruitGrids.Remove(key); }
    }

    private static void RescueForeignContents(GameItem pulp)
    {
        var grid = Fruits(pulp);
        if (grid == null) return;
        var contents = InventoryOps.Snapshot(grid);
        if (contents.Count == 0) return;
        var counts = new Dictionary<string, int>();
        string keepId = null;
        var keepCount = 0;
        foreach (var content in contents)
        {
            if (!content.IsTag("WINE_INGREDIENT_TAG")) continue;
            var count = counts.TryGetValue(content.identifier, out var prior) ? prior + 1 : 1;
            counts[content.identifier] = count;
            if (count > keepCount) { keepId = content.identifier; keepCount = count; }
        }
        if (keepCount < 4) keepId = null;
        var retained = 0;
        foreach (var content in contents)
        {
            if (content.identifier == keepId && content.IsTag("WINE_INGREDIENT_TAG") && retained < 4)
            {
                retained++;
                continue;
            }
            var store = PlayerStore.instance;
            var emporium = EmporiumEntry.Instance;
            var backpack = store?.gridInv;
            var counter = emporium?.backInvinvElementCounter;
            var stockroom = emporium?.backInvinvElement;
            var recovered = (backpack != null && backpack.MayHaveValidInventorySlot(content)
                    && InventoryOps.Move(grid, backpack, content))
                || (counter != null && counter.MayHaveValidInventorySlot(content)
                    && InventoryOps.Move(grid, counter, content))
                || (stockroom != null && stockroom.MayHaveValidInventorySlot(content)
                    && InventoryOps.Move(grid, stockroom, content));
            if (recovered)
            {
                PendingRecoveryWarnings.Remove(pulp.Pointer.ToInt64());
                Core.Log?.Msg("Recovered foreign item " + content.identifier + " from pulp " + pulp.uniqueId);
            }
            else if (PendingRecoveryWarnings.Add(pulp.Pointer.ToInt64()))
                Core.Log?.Warning("Free backpack or counter space to recover an item trapped in pulp " + pulp.uniqueId);
        }
    }

    internal static bool IsInternalWindow(PixelWindow window)
    {
        if (window == null) return false;
        var parents = window.parentItems;
        if (parents != null)
            for (var i = 0; i < parents.Count; i++)
                if (parents[i]?.identifier == Id) return true;
        return window.titleString == "原浆中的果实" || window.titleString == "Fruit in pulp";
    }

    internal static void EnsureShape(GameItem item)
    {
        if (item?.identifier != Id) return;
        var shape = item.shape;
        if (shape?.globalWidth == 2 && shape.globalHeight == 2) return;
        var grid = item.parentInventory?.TryCast<GameGridInventory>();
        if (grid == null) return;
        if (grid.identifier == "xiaowo_concentrator_pulp" && (grid.childItems?.Count ?? 0) > 1)
        {
            if (PendingShapeWarnings.Add(item.Pointer.ToInt64()))
                Core.Log?.Warning("Saved pulp needs the concentrator output cleared before resizing: " + item.uniqueId);
            return;
        }

        // Reinsert only when the larger item actually fits. An old 2x2 output
        // can hold two 1x2 pulps, so forcing both to 2x2 would corrupt its layout.
        var oldShape = shape;
        var oldModifiedShape = item.modifiedShape;
        if (!grid.Expel(item)) return;
        item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.ValidateShapeState();
        if (grid.MayHaveValidInventorySlot(item) && grid.UncheckedAccept(item))
        {
            PendingShapeWarnings.Remove(item.Pointer.ToInt64());
            Core.Log?.Msg("Resized saved pulp to 2x2: " + item.uniqueId);
            return;
        }

        item.shape = oldShape;
        item.modifiedShape = oldModifiedShape;
        item.ValidateShapeState();
        if (!grid.UncheckedAccept(item))
            Core.Log?.Error("Could not restore saved pulp after shape migration: " + item.uniqueId);
        else if (PendingShapeWarnings.Add(item.Pointer.ToInt64()))
            Core.Log?.Warning("Saved pulp needs 2x2 free space before it can be resized: " + item.uniqueId);
    }

    internal static void EnsureVisual(GameItem item)
    {
        if (item?.identifier != Id) return;
        var fruitId = FruitId(item);
        if (fruitId == null) return;
        var ids = BrewingExpansion.BrewingExpansionMod.FruitIds;
        for (var i = 0; i < ids.Length && i < PulpTextureNames.Length; i++)
        {
            if (ids[i] != fruitId) continue;
            var key = "pulp_" + PulpTextureNames[i];
            var pointer = item.Pointer.ToInt64();
            if (item.spriteAtlasPath != BarrelItem.SpriteAtlas || item.spritePath != key
                || !RefreshedSprites.Contains(pointer))
            {
                item.SetSprite(BarrelItem.SpriteAtlas, key);
                item.SetSpriteQuick(BarrelItem.SpriteAtlas, key);
                item.TryCast<GameItemElement>()?.ValidatePartialSprite();
                RefreshedSprites.Add(pointer);
            }
            return;
        }
    }

    internal static GameGridInventory Fruits(GameItem pulp)
    {
        if (pulp?.identifier != Id || pulp.contentWindow?.child == null) return null;
        var root = pulp.contentWindow.child.TryCast<GameGridInventory>();
        if (root?.identifier == FruitInventoryId) return root;
        var matches = GraphUtils.FindAllChildrenType<GameGridInventory>(pulp.contentWindow.child,
            (Il2CppSystem.Func<GameGridInventory, bool>)(grid => grid?.identifier == FruitInventoryId));
        return matches != null && matches.Count > 0 ? matches[0] : null;
    }

    internal static string FruitId(GameItem pulp)
    {
        if (pulp?.identifier != Id) return null;
        foreach (var id in BrewingExpansion.BrewingExpansionMod.FruitIds)
            if (pulp.IsTag(FruitTagPrefix + id)) return id;
        return LegacyFruitId(Fruits(pulp));
    }

    private static string LegacyFruitId(GameGridInventory grid)
    {
        var fruits = InventoryOps.Snapshot(grid);
        if (fruits.Count != 4 || !fruits[0].IsTag("WINE_INGREDIENT_TAG")) return null;
        var id = fruits[0].identifier;
        for (var i = 1; i < fruits.Count; i++)
            if (fruits[i].identifier != id || !fruits[i].IsTag("WINE_INGREDIENT_TAG")) return null;
        return id;
    }

    internal static void PressAvailable(GameItem barrel)
    {
        var input = BarrelItem.Input(barrel);
        if (input == null) return;
        var groups = new Dictionary<string, List<GameItem>>();
        foreach (var fruit in InventoryOps.Snapshot(input))
        {
            if (fruit?.identifier == Id || fruit == null || !fruit.IsTag("WINE_INGREDIENT_TAG")) continue;
            if (!groups.TryGetValue(fruit.identifier, out var group))
                groups[fruit.identifier] = group = new List<GameItem>();
            group.Add(fruit);
        }
        foreach (var group in groups.Values)
        {
            Core.Log?.Msg("Press candidate: " + group[0].identifier + " x" + group.Count);
            while (group.Count >= 4)
            {
                var pulp = Create();
                var target = Fruits(pulp);
                var moved = new List<GameItem>();
                for (var i = 0; i < 4; i++)
                {
                    if (!MoveFruitIntoPulp(input, target, group[i]))
                    {
                        Core.Log?.Warning("Could not move fruit " + i + " into pulp inventory.");
                        break;
                    }
                    moved.Add(group[i]);
                }
                if (moved.Count != 4 || !InventoryOps.Accept(input, pulp))
                {
                    Core.Log?.Warning("Could not complete fruit pressing; moved=" + moved.Count);
                    foreach (var fruit in moved) InventoryOps.Move(target, input, fruit);
                    pulp.Destroy();
                    return;
                }
                var fruitId = group[0].identifier;
                var fruitName = group[0].name;
                var value = 0L;
                foreach (var fruit in moved) value += fruit.unitValue;
                pulp.SetName(BarrelItem.Text(fruitName + "原浆", fruitName + " Pulp"));
                SetPulpDescription(pulp, fruitId);
                pulp.SetValue(value);
                EnsureProduced(pulp);
                if (pulp.contentWindow != null || FruitId(pulp) != fruitId)
                {
                    Core.Log?.Error("Pressed pulp did not seal correctly: " + pulp.uniqueId);
                    input.Expel(pulp);
                    foreach (var fruit in moved)
                        if (fruit.parentInventory?.Pointer == target.Pointer) InventoryOps.Move(target, input, fruit);
                    pulp.Destroy();
                    return;
                }
                group.RemoveRange(0, 4);
                Core.Log?.Msg("Pressed four " + fruitId + " into fruit pulp.");
            }
        }
    }
}

[HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.UncheckedAccept), typeof(GameItem))]
internal static class PulpFruitGridAcceptPatch
{
    private static bool Prefix(GameGridInventory __instance, GameItem item, ref bool __result)
    {
        if (__instance?.identifier != "xiaowo_pulp_fruits" || BarrelLoadPatch.Loading
            || PulpItem.IsFillingFruitGrid(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.MayActivateSlotItem))]
internal static class PulpMayActivatePatch
{
    private static void Postfix(GameItem __instance, ref bool __result)
    {
        if (__instance?.identifier == PulpItem.Id) __result = false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.MayHaveValidInventorySlot), typeof(GameItem))]
internal static class PulpContainerTargetPatch
{
    private static void Postfix(GameItem __instance, ref bool __result)
    {
        if (__instance?.identifier == PulpItem.Id) __result = false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.TryFindOneValidInventorySlot), typeof(GameItem))]
internal static class PulpContainerSlotPatch
{
    private static void Postfix(GameItem __instance, ref SlotMarker __result)
    {
        if (__instance?.identifier == PulpItem.Id) __result = null;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.CanActivateSlotItem))]
internal static class PulpCanActivatePatch
{
    private static void Postfix(GameItem __instance, ref bool __result)
    {
        if (__instance?.identifier == PulpItem.Id) __result = false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.ActivateSlotItem))]
internal static class PulpActivatePatch
{
    private static bool Prefix(GameItem __instance) => __instance?.identifier != PulpItem.Id;
}

[HarmonyPatch(typeof(PixelWindow), nameof(PixelWindow.Show), new Type[] { })]
internal static class PulpWindowShowPatch
{
    private static bool Prefix(PixelWindow __instance, ref PixelWindow __result)
    {
        if (!PulpItem.IsInternalWindow(__instance)) return true;
        __result = __instance;
        return false;
    }
}

[HarmonyPatch(typeof(PixelWindow), nameof(PixelWindow.Show), typeof(Vector2), typeof(Vector2), typeof(bool))]
internal static class PulpWindowShowPositionedPatch
{
    private static bool Prefix(PixelWindow __instance, ref PixelWindow __result)
    {
        if (!PulpItem.IsInternalWindow(__instance)) return true;
        __result = __instance;
        return false;
    }
}
