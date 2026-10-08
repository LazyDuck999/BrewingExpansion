using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace XIAOWOBrewingBarrel;

internal static class NativeItemDirectoryRegistration
{
    private static Il2CppSystem.Func<GameItem> _barrelFactory;
    private static Il2CppSystem.Func<GameItem> _pulpFactory;
    private static Il2CppSystem.Func<GameItem> _concentratorFactory;
    private static Il2CppSystem.Func<GameItem> _liunianFactory;
    private static Il2CppSystem.Func<GameItem> _basicPurifierFactory;
    private static Il2CppSystem.Func<GameItem> _middlePurifierFactory;
    private static Il2CppSystem.Func<GameItem> _advancedPurifierFactory;

    internal static void Register(ModItemDirectory __instance)
    {
        if (__instance == null) return;
        _barrelFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)BarrelItem.Create);
        _pulpFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)PulpItem.Create);
        _concentratorFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)ConcentratorItem.Create);
        _liunianFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)LiunianAger.Create);
        _basicPurifierFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)XuxuWaterPurifier.CreateBasic);
        _middlePurifierFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)XuxuWaterPurifier.CreateMiddle);
        _advancedPurifierFactory ??= DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((Func<GameItem>)XuxuWaterPurifier.CreateAdvanced);
        if (!__instance.factoryDictionary.ContainsKey(BarrelItem.Id)) __instance.Add(BarrelItem.Id, _barrelFactory);
        if (!__instance.factoryDictionary.ContainsKey(PulpItem.Id)) __instance.Add(PulpItem.Id, _pulpFactory);
        if (!__instance.factoryDictionary.ContainsKey(ConcentratorItem.Id)) __instance.Add(ConcentratorItem.Id, _concentratorFactory);
        if (!__instance.factoryDictionary.ContainsKey(LiunianAger.Id)) __instance.Add(LiunianAger.Id, _liunianFactory);
        // Old IDs are resolved by the load/creation patches, not advertised.
        if (!__instance.factoryDictionary.ContainsKey(XuxuWaterPurifier.BasicId)) __instance.Add(XuxuWaterPurifier.BasicId, _basicPurifierFactory);
        if (!__instance.factoryDictionary.ContainsKey(XuxuWaterPurifier.MiddleId)) __instance.Add(XuxuWaterPurifier.MiddleId, _middlePurifierFactory);
        if (!__instance.factoryDictionary.ContainsKey(XuxuWaterPurifier.AdvancedId)) __instance.Add(XuxuWaterPurifier.AdvancedId, _advancedPurifierFactory);
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.LoadGame))]
internal static class BarrelLoadPatch
{
    internal static bool Loading;
    private static void Prefix()
    {
        Loading = true;
        LiunianAger.ResetRuntimeCache();
    }
    private static void Postfix()
    {
        LegacyItemMigration.MigrateOwnedItems();
        Loading = false;
        BarrelItem.SealSavedPulps();
        BarrelMaintenanceEvents.ScheduleFullSweep();
    }
}

internal static class BarrelItem
{
    internal static void SealSavedPulps()
    {
        try
        {
            var items = PlayerStore.instance?.FindAllItem(true);
            if (items == null) return;
            for (var i = 0; i < items.Count; i++)
                if (items[i]?.identifier == PulpItem.Id) PulpItem.EnsureProduced(items[i]);
        }
        catch (Exception ex) { Core.Log?.Warning("Could not seal saved pulps: " + ex); }
    }

    internal const string Id = "be_large_brewing_barrel";
    internal const string SpriteAtlas = "XIAOWO/BrewingBarrel";
    private const string InputId = "xiaowo_barrel_pulp";
    private const string BottlesId = "xiaowo_barrel_bottles";
    private const string YeastId = "xiaowo_barrel_yeast";
    private const string OutputId = "xiaowo_barrel_products";
    private static readonly Dictionary<long, TagElement> Displays = new();
    private static readonly Dictionary<long, TagElement> WaterDisplays = new();
    private static readonly HashSet<long> PendingLayoutWarnings = new();
    private static readonly HashSet<long> TickItemWarnings = new();
    private static readonly HashSet<long> UpdatedSavedControls = new();
    private static readonly HashSet<long> ClearedWaterSpriteModifiers = new();
    private const string BarrelNameZh = "xuxu酿造桶";
    private const string BarrelNameEn = "xuxu Brewing Barrel";
    private const string BarrelDescriptionZh =
    "一个有耐心的地方。果实进去，时间替你把世界关掉。\n\n" +
    "果实醒着的时候是果实，睡着的时候是酒。\n\n" +
    "酿酒桶不做什么——它只是替你把世界关掉一夜。\n\n" +
    "醒来时，它多了一点你没有的东西：时间。";
    private const string BarrelDescriptionEn =
    "A place with patience. Fruit goes in, and time shuts the world away for you.\n\n" +
    "Fruit awake is fruit. Fruit asleep is wine.\n\n" +
    "The barrel does nothing — it merely shuts the world away for a single night.\n\n" +
    "When it wakes, it holds something you do not: time.";

    internal static string Text(string zh, string en)
    {
        return BrewingExpansion.Lang.T(zh, en);
    }

    internal static GameItem Create()
    {
        // Keep the game's normal item construction and inventory interaction chain.
        var item = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.VanillaWaterPitcherId, false)
            ?? throw new InvalidOperationException("Could not create a vanilla water pitcher for the brewing barrel.");
        item.identifier = Id;
        item.SetName(Text(BarrelNameZh, BarrelNameEn));
        item.shortDescription = Text(BarrelDescriptionZh, BarrelDescriptionEn);
        item.longDescription = item.shortDescription;
        item.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetGameItemType("MACHINE");
        WaterHelper.InitLiquidContainerItem(item, 3000, 0, false, true, false, false, 3);
        item.SetValue(1400L);
        BrewingExpansion.BrewingExpansionMod.ClearSpriteModifierCallbacks(item);
        item.SetSprite(SpriteAtlas, "barrel_idle");
        BuildWindow(item);
        item.ValidateShapeState();
        return item;
    }

    private static void BuildWindow(GameItem item)
    {
        var input = new GameGridInventory(4, 2) { identifier = InputId };
        var bottles = new GameGridInventory(4, 4) { identifier = BottlesId };
        var yeast = new GameGridInventory(4, 2) { identifier = YeastId };
        var output = new GameGridInventory(4, 4) { identifier = OutputId };
        var status = new TagElement(-1).SetText(Text("等待原浆、空瓶与酵母", "Waiting for pulp, bottles and yeast"));
        var waterStatus = new TagElement(-1).SetText(Text("储水 0/3000 ml", "Water 0/3000 ml"));
        var root = new GridPixelElement(2, 6, true).SetSpacing(3, 3);
        root.Attach(new TagElement(-1).SetText(Text("原浆区", "Pulp Area")).Cast<PixelElement>(), 0, 0);
        root.Attach(new TagElement(-1).SetText(Text("空瓶区", "Empty Bottle Area")).Cast<PixelElement>(), 1, 0);
        root.Attach(input.Cast<PixelElement>(), 0, 1);
        root.Attach(bottles.Cast<PixelElement>(), 1, 1);
        root.Attach(new TagElement(-1).SetText(Text("成品", "Finished wine")).Cast<PixelElement>(), 0, 2);
        root.Attach(new TagElement(-1).SetText(Text("酵母区", "Yeast Area")).Cast<PixelElement>(), 1, 2);
        root.Attach(output.Cast<PixelElement>(), 0, 3);
        root.Attach(yeast.Cast<PixelElement>(), 1, 3);
        root.Attach(status.Cast<PixelElement>(), 0, 4);
        root.Attach(waterStatus.Cast<PixelElement>(), 1, 4);
        root.Attach(new ButtonElement(Text("开始发酵", "Start Fermentation"), 120, -1,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => BrewingBatch.Start(item)))).Cast<PixelElement>(), 0, 5);
        var window = new PixelWindow(true);
        window.SetTitle(Text(BarrelNameZh, BarrelNameEn));
        window.Attach(root.Cast<PixelElement>());
        item.SetContentWindow(window);
        BrewingExpansion.NativeWindowCallbacks.Bind(item);
        Displays[item.Pointer.ToInt64()] = status;
        WaterDisplays[item.Pointer.ToInt64()] = waterStatus;
    }

    internal static GameGridInventory Input(GameItem item) => Find(item, InputId);
    internal static GameGridInventory Bottles(GameItem item) => Find(item, BottlesId);
    internal static GameGridInventory Yeast(GameItem item) => Find(item, YeastId);
    internal static GameGridInventory Output(GameItem item) => Find(item, OutputId);

    private static GameGridInventory Find(GameItem item, string id)
    {
        if (item?.contentWindow?.child == null) return null;
        var matches = GraphUtils.FindAllChildrenType<GameGridInventory>(item.contentWindow.child,
            (Il2CppSystem.Func<GameGridInventory, bool>)(grid => grid?.identifier == id));
        return matches != null && matches.Count > 0 ? matches[0] : null;
    }

    private static bool ResizeEmptyGrid(GameGridInventory grid, int width, int height)
    {
        if (grid.inventoryShape.width == width && grid.inventoryShape.height == height) return true;
        // A saved barrel may contain items outside the new bounds. Wait until that grid is empty.
        if ((grid.childItems?.Count ?? 0) > 0 || (grid.items?.Count ?? 0) > 0) return false;
        grid.SetShape(width, height);
        return true;
    }

    private static void UpdateSavedLabels(GameItem item)
    {
        var labels = GraphUtils.FindAllChildrenType<TagElement>(item.contentWindow.child,
            (Il2CppSystem.Func<TagElement, bool>)(_ => true));
        if (labels == null) return;
        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            switch (label.GetText())
            {
                case "原浆 · 放入四个同种果实可自动压榨":
                case "Pulp · four matching fruits press automatically":
                    label.SetText(Text("原浆区", "Pulp Area"));
                    break;
                case "空瓶 / 发酵中":
                case "Empty bottles / fermenting":
                    label.SetText(Text("空瓶区", "Empty Bottle Area"));
                    break;
                case "酵母":
                case "Yeast":
                    label.SetText(Text("酵母区", "Yeast Area"));
                    break;
            }
        }
    }

    private static void UpdateSavedButton(GameItem item)
    {
        var button = item.contentWindow.childElement?.TryCast<GridPixelElement>()?.GetElement(0, 5)?.TryCast<ButtonElement>();
        if (button != null) button.forceWidth = 120;
    }

    private static void UpdateSavedIdentity(GameItem item)
    {
        item.SetName(Text(BarrelNameZh, BarrelNameEn));
        item.shortDescription = Text(BarrelDescriptionZh, BarrelDescriptionEn);
        item.longDescription = item.shortDescription;
        item.contentWindow.SetTitle(Text(BarrelNameZh, BarrelNameEn));
    }

    internal static void CenterHorizontally(RectTransform target, RectTransform bounds,
        float logicalTargetWidth = 0f, float logicalBoundsWidth = 0f)
    {
        if (target == null || bounds == null) return;
        var targetCorners = new Il2CppStructArray<Vector3>(4);
        var boundCorners = new Il2CppStructArray<Vector3>(4);
        target.GetWorldCorners(targetCorners);
        bounds.GetWorldCorners(boundCorners);
        var targetCenter = logicalTargetWidth > 0f && logicalBoundsWidth > 0f
            ? targetCorners[0].x + logicalTargetWidth * (boundCorners[2].x - boundCorners[0].x) / logicalBoundsWidth * .5f
            : (targetCorners[0].x + targetCorners[2].x) * .5f;
        var delta = (boundCorners[0].x + boundCorners[2].x) * .5f - targetCenter;
        if (Math.Abs(delta) > .1f) target.position += new Vector3(delta, 0f, 0f);
    }

    internal static void CenterWindowControls(PixelWindow window)
    {
        if ((window?.titleString != BarrelNameZh && window?.titleString != BarrelNameEn) || !window.IsVisible()) return;
        CenterHorizontally(window.titleText?.rectTransform, window.background?.rectTransform);
        var root = window.childElement?.TryCast<GridPixelElement>();
        var button = root?.GetElement(0, 5)?.TryCast<ButtonElement>();
        CenterHorizontally(button?.handler?.rectTransform, window.background?.rectTransform,
            button?.forceWidth ?? 0, window.widthPixels);
        CenterHorizontally(button?.textNode?.rectTransform, button?.buttonNode?.rectTransform);
        AlignRightColumn(window, root);
    }

    private static void AlignRightColumn(PixelWindow window, GridPixelElement root)
    {
        var left = root?.GetElement(0, 1)?.TryCast<GameGridInventory>();
        var right = root?.GetElement(1, 1)?.TryCast<GameGridInventory>();
        var bounds = window.background?.rectTransform;
        if (left?.handler?.rectTransform == null || right?.handler?.rectTransform == null
            || bounds == null || window.widthPixels <= 0) return;

        var corners = new Il2CppStructArray<Vector3>(4);
        bounds.GetWorldCorners(corners);
        var leftEdge = corners[0].x;
        var rightEdge = corners[2].x;
        var scale = (rightEdge - leftEdge) / window.widthPixels;
        left.handler.rectTransform.GetWorldCorners(corners);
        var leftMargin = corners[0].x - leftEdge;
        right.handler.rectTransform.GetWorldCorners(corners);
        var rightMargin = rightEdge - (corners[0].x + right.widthPixels * scale);
        var delta = rightMargin - leftMargin;
        if (Math.Abs(delta) < .1f) return;
        ShiftHorizontally(root.GetElement(1, 0)?.handler?.rectTransform, delta);
        ShiftHorizontally(right.handler.rectTransform, delta);
        ShiftHorizontally(root.GetElement(1, 2)?.handler?.rectTransform, delta);
        ShiftHorizontally(root.GetElement(1, 3)?.handler?.rectTransform, delta);
    }

    internal static void ShiftHorizontally(RectTransform target, float delta)
    {
        if (target != null) target.position += new Vector3(delta, 0f, 0f);
    }

    private static void UpdateSavedLayout(GameItem item, GameGridInventory input, GameGridInventory bottles,
        GameGridInventory yeast, GameGridInventory output)
    {
        var complete = ResizeEmptyGrid(input, 4, 2)
            & ResizeEmptyGrid(bottles, 4, 4)
            & ResizeEmptyGrid(yeast, 4, 2)
            & ResizeEmptyGrid(output, 4, 4);
        var key = item.Pointer.ToInt64();
        if (UpdatedSavedControls.Add(key))
        {
            UpdateSavedLabels(item);
            UpdateSavedButton(item);
            UpdateSavedIdentity(item);
        }
        if (complete) PendingLayoutWarnings.Remove(key);
        else if (PendingLayoutWarnings.Add(key))
            Core.Log?.Warning("Barrel grid resize is pending until occupied grids are emptied: " + item.uniqueId);
    }

    internal static void SetStatus(GameItem item, string zh, string en)
    {
        if (item == null || !Displays.TryGetValue(item.Pointer.ToInt64(), out var display)) return;
        try { display.SetText(Text(zh, en)); }
        catch { Displays.Remove(item.Pointer.ToInt64()); }
    }

    internal static void SetVisual(GameItem item, bool active)
    {
        if (item == null) return;
        var tag = active ? "XIAOWO_BARREL_ACTIVE" : "XIAOWO_BARREL_IDLE";
        if (item.IsTag(tag)) return;
        item.SetSprite(SpriteAtlas, active ? "barrel_active" : "barrel_idle");
        item.DisableTag(active ? "XIAOWO_BARREL_IDLE" : "XIAOWO_BARREL_ACTIVE");
        item.EnableTag(tag);
    }

    private static void UpdateWaterStatus(GameItem item)
    {
        if (!WaterDisplays.TryGetValue(item.Pointer.ToInt64(), out var display)) return;
        try
        {
            var amount = WaterHelper.GetCurrentCapacityML(item);
            display.SetText(Text($"储水 {amount}/3000 ml", $"Water {amount}/3000 ml"));
        }
        catch { WaterDisplays.Remove(item.Pointer.ToInt64()); }
    }

    internal static void MaintainOwnedItems()
    {
        if (BarrelLoadPatch.Loading || PlayerStore.instance == null) return;
        try
        {
            var items = OwnedInventorySnapshot.Items();
            if (items == null) return;
            for (var i = 0; i < items.Count; i++)
                MaintainItem(items[i]);
        }
        catch (Exception ex) { Core.Log?.Error("Barrel maintenance sweep failed: " + ex); }
    }

    internal static bool IsModItem(GameItem item)
    {
        var id = item?.identifier;
        return LegacyItemMigration.IsLegacy(id)
            || id == Id || id == PulpItem.Id || id == ConcentratorItem.Id
            || id == LiunianAger.Id || id == BrewingExpansion.BrewingExpansionMod.IncubatorId
            || id == BrewingExpansion.BrewingExpansionMod.BrewerySignId
            || id == BrewingExpansion.BrewingExpansionMod.XuxuBatteryId
            || id == BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId
            || id == BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId
            || (id != null && id.StartsWith("be_music_cassette_", StringComparison.Ordinal))
            || (id != null && BrewingExpansion.BrewingExpansionMod.IsOurFruit(id))
            || (id != null && BrewingExpansion.BrewingExpansionMod.IsOurSeed(id))
            || Array.IndexOf(XuxuWaterPurifier.Ids, id) >= 0;
    }

    internal static void MaintainItem(GameItem item)
    {
        if (BarrelLoadPatch.Loading || item == null) return;
        try
        {
            LegacyItemMigration.Migrate(item);
            var id = item.identifier;
            var recharge = id == BrewingExpansion.BrewingExpansionMod.XuxuBatteryId ? 6
                : id == BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId ? 8
                : id == BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId ? 12 : 0;
            if (recharge != 0)
            {
                var tag = item.GetTagReadonly("power_source_item_recharge");
                if (tag == null || tag.valueInt != recharge)
                    BrewingExpansion.BrewingExpansionMod.SetTagInt(item, "power_source_item_recharge", recharge);
                return;
            }
            if (id == PulpItem.Id) { PulpItem.EnsureProduced(item); return; }
            if (CustomCassettes.EnsureLoaded(item)) return;
            if (BrewingExpansion.BrewingExpansionMod.IsOurSeed(id))
            {
                BrewingExpansion.BrewingExpansionMod.EnsureSeedFoodTag(item);
                return;
            }
            if (BrewingExpansion.BrewingExpansionMod.IsOurFruit(id))
            {
                if (!item.IsTag("FOOD")) item.EnableTag("FOOD", false);
                return;
            }
            if (id == ConcentratorItem.Id) { ConcentratorItem.EnsureLoaded(item); return; }
            if (id == LiunianAger.Id) { LiunianAger.EnsureLoaded(item); return; }
            if (id == BrewingExpansion.BrewingExpansionMod.IncubatorId)
            { BrewingExpansion.IncubatorLayout.Configure(item); return; }
            if (Array.IndexOf(XuxuWaterPurifier.Ids, id) >= 0)
            { XuxuWaterPurifier.EnsureLoaded(item); return; }
            if (id == BrewingExpansion.BrewingExpansionMod.BrewerySignId)
            {
                if (item.itemTypes != null && item.itemTypes.Count > 0) item.itemTypes.Clear();
                return;
            }
            if (id != Id) return;
            BrewingExpansion.NativeWindowCallbacks.Bind(item);
            if (ClearedWaterSpriteModifiers.Add(item.Pointer.ToInt64()))
                BrewingExpansion.BrewingExpansionMod.ClearSpriteModifierCallbacks(item);
            if (item.spriteAtlasPath != SpriteAtlas ||
                (item.spritePath != "barrel_idle" && item.spritePath != "barrel_active"))
                item.SetSprite(SpriteAtlas, item.IsTag("XIAOWO_BARREL_ACTIVE") ? "barrel_active" : "barrel_idle");
            var input = Input(item);
            var bottles = Bottles(item);
            var yeast = Yeast(item);
            var output = Output(item);
            if (input == null || bottles == null || yeast == null || output == null)
            {
                Core.Log?.Warning("Barrel layout is incomplete; preserving contained items: " + item.uniqueId);
                return;
            }
            UpdateSavedLayout(item, input, bottles, yeast, output);
            foreach (var pulp in InventoryOps.Snapshot(input)) PulpItem.EnsureProduced(pulp);
            CenterWindowControls(item.contentWindow);
            BrewingBatch.Tick(item);
            UpdateWaterStatus(item);
        }
        catch (Exception ex)
        {
            long pointer = 0;
            try { pointer = item.Pointer.ToInt64(); } catch { }
            if (TickItemWarnings.Add(pointer))
                Core.Log?.Warning("Skipping invalid item during barrel maintenance: " + ex);
        }
    }
}



[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.UpdateRectTransform))]
internal static class BarrelPointerPatch
{
#if BREWING_TEST
    private static bool _logged;
    private static bool _loggedAfter;
    private static readonly Dictionary<long, string> Layers = new();
    internal static void LogPointer(TreeNodeRender render, string action)
    {
        if (render != null && Layers.TryGetValue(render.Pointer.ToInt64(), out var layer))
            Core.Log?.Msg("BREWING_TEST POINTER_EVENT " + action + " on " + layer);
    }
    private static void Register(TreeNodeRender render, string layer)
    {
        if (render != null) Layers[render.Pointer.ToInt64()] = layer;
    }
#endif
    private static void Postfix(GameItemElement __instance)
    {
        if (__instance == null || !IsOurMachine(__instance.identifier)) return;
        var size = __instance.rectTransform != null ? __instance.rectTransform.rect.size : Vector2.zero;
        if (size.x <= 0 || size.y <= 0)
            size = new Vector2(__instance.widthPixels, __instance.heightPixels);
#if BREWING_TEST
        if (!_logged)
        {
            _logged = true;
            Core.Log?.Msg("BREWING_TEST POINTER before: item=" + size + " handler=" + Describe(__instance.handler)
                + " background=" + Describe(__instance.background) + " fake=" + Describe(__instance.fakeBackground));
        }
#endif
        PrepareHitLayer(__instance.handler, size);
        PrepareHitLayer(__instance.background, size);
#if BREWING_TEST
        Register(__instance.handler, "handler");
        Register(__instance.background, "background");
        Register(__instance.fakeBackground, "fakeBackground");
#endif
        DisableFakeRaycast(__instance);
#if BREWING_TEST
        if (!_loggedAfter)
        {
            _loggedAfter = true;
            Core.Log?.Msg("BREWING_TEST POINTER after: handler=" + Describe(__instance.handler)
                + " background=" + Describe(__instance.background) + " fake=" + Describe(__instance.fakeBackground));
        }
#endif
    }

    private static void PrepareHitLayer(TreeNodeRender layer, Vector2 size)
    {
        if (layer == null) return;
        if (layer.rectTransform != null && size.x > 0 && size.y > 0)
            layer.rectTransform.sizeDelta = size;
        Image image = layer.image;
        if (image == null) return;
        image.raycastTarget = true;
        image.alphaHitTestMinimumThreshold = 0f;
    }

    internal static void DisableFakeRaycast(GameItemElement item)
    {
        if (item == null || !IsOurMachine(item.identifier) || item.fakeBackground?.image == null) return;
        item.fakeBackground.image.raycastTarget = false;
        item.fakeBackground.image.alphaHitTestMinimumThreshold = 0f;
    }

    private static bool IsOurMachine(string identifier) =>
        identifier == BarrelItem.Id || identifier == ConcentratorItem.Id || identifier == LiunianAger.Id
        || Array.IndexOf(XuxuWaterPurifier.Ids, identifier) >= 0;

#if BREWING_TEST
    private static string Describe(TreeNodeRender layer) => layer == null ? "null"
        : "size=" + (layer.rectTransform == null ? "null" : layer.rectTransform.rect.size.ToString())
            + ",raycast=" + (layer.image == null ? "null" : layer.image.raycastTarget.ToString())
            + ",alpha=" + (layer.image == null ? "null" : layer.image.alphaHitTestMinimumThreshold.ToString());
#endif
}

[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ToggleFakeBackground), typeof(bool), typeof(Vector4))]
internal static class BarrelFakeBackgroundVectorPatch
{
    private static void Postfix(GameItemElement __instance) => BarrelPointerPatch.DisableFakeRaycast(__instance);
}

[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ToggleFakeBackground), typeof(bool), typeof(float), typeof(float))]
internal static class BarrelFakeBackgroundSizePatch
{
    private static void Postfix(GameItemElement __instance) => BarrelPointerPatch.DisableFakeRaycast(__instance);
}

#if BREWING_TEST
[HarmonyPatch(typeof(TreeNodeRender), nameof(TreeNodeRender.OnPointerDown))]
internal static class BarrelPointerDownTestPatch
{
    private static int _count;
    private static void Postfix(TreeNodeRender __instance)
    {
        if (_count++ < 20)
            Core.Log?.Msg("BREWING_TEST ANY_POINTER_DOWN name=" + __instance?.gameObject?.name
                + ",element=" + __instance?.element?.GetType().Name
                + ",parent=" + __instance?.parent?.gameObject?.name
                + ",parentElement=" + __instance?.parent?.element?.GetType().Name
                + ",image=" + __instance?.image?.sprite?.name
                + ",size=" + __instance?.rectTransform?.rect.size);
        BarrelPointerPatch.LogPointer(__instance, "down");
    }
}
[HarmonyPatch(typeof(TreeNodeRender), nameof(TreeNodeRender.OnBeginDrag))]
internal static class BarrelBeginDragTestPatch
{
    private static int _count;
    private static void Postfix(TreeNodeRender __instance)
    {
        if (_count++ < 20)
            Core.Log?.Msg("BREWING_TEST ANY_BEGIN_DRAG name=" + __instance?.gameObject?.name
                + ",parent=" + __instance?.parent?.gameObject?.name);
        BarrelPointerPatch.LogPointer(__instance, "begin drag");
    }
}
[HarmonyPatch(typeof(TreeNodeRender), nameof(TreeNodeRender.OnEndDrag))]
internal static class BarrelEndDragTestPatch
{
    private static void Postfix(TreeNodeRender __instance) => BarrelPointerPatch.LogPointer(__instance, "end drag");
}
#endif

[HarmonyPatch(typeof(WaterHelper), nameof(WaterHelper.TransferLiquid))]
internal static class WaterOnlyBarrelPatch
{
    private static bool Prefix(GameItem __0, GameItem __1)
    {
        if (__1?.identifier != BarrelItem.Id) return true;
        if (__0 == null || __0.identifier == "wine_bottle" || __0.IsTag("ALCOHOL")) return false;
        try { return WaterFeatureHelper.GetWaterFeature(__0) != null; }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(WineHelper), nameof(WineHelper.CanBottleSourceTarget))]
internal static class NoWineIntoBarrelPatch
{
    private static void Postfix(GameItem __1, ref bool __result)
    {
        if (__1?.identifier == BarrelItem.Id) __result = false;
    }
}

[HarmonyPatch(typeof(WineHelper), nameof(WineHelper.PourWineIntoContainer))]
internal static class NoWinePourPatch
{
    private static bool Prefix(GameItem __1) => __1?.identifier != BarrelItem.Id;
}
