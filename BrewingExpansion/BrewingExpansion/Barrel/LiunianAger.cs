using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace XIAOWOBrewingBarrel;

internal static class LiunianAger
{
    internal const string Id = "be_liunian_ager";
    // SaveManager stores the index of each child inventory in the item's
    // pixel-element tree. The value-header wrapper added one node after 3.0.
    internal const int BatterySaveNode = 4;
    internal const int ModuleSaveNode = 5;
    internal const int StorageSaveNode = 6;
    private const string StorageId = "xiaowo_liunian_storage";
    private const string ModuleId = "xiaowo_liunian_modules";
    private const int BottomMarginPixels = 20;
    private const string DescriptionZh =
        "陈化是春天，冰镇是冬天。\n\n" +
        "四季本来是一年一年地走——走过春，才到夏；走过秋，才到冬。没有谁能同时站在两个季节里。\n\n" +
        "流年酒柜却做到了。\n" +
        "它把一整年的光阴，折叠成一扇薄薄的柜门——\n" +
        "推开它，酒就同时拥有了春天和冬天。\n\n" +
        "暖让它在深处长大，冷让它在表面收紧。\n" +
        "等它出来的时候，你尝到的不是某一年，\n" +
        "是所有年份，同时在场。";
    private const string DescriptionEn =
        "Aging is spring. Chilling is winter.\n\n" +
        "The seasons once walked year after year — through spring, then summer; through autumn, then winter. No one could ever stand in two seasons at once.\n\n" +
        "The Cabinet of Passing Years did it anyway.\n" +
        "It folded a whole year into a thin cabinet door —\n" +
        "open it, and the wine holds spring and winter at once.\n\n" +
        "Warmth lets it grow deep. Cold lets it tighten on the surface.\n" +
        "When it comes out, what you taste is not a single year —\n" +
        "it is every year, all present at once.";
    // The native rack draws bottle-count overlays through this callback.
    // Keep its inventory and aging callbacks, but use only Liunian's own art.
    private static readonly Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>>
        FixedArt = _ => new Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>();
    private const float VisibleValueRefreshSeconds = 5f;
    private static readonly Dictionary<long, (long Window, long Child, int UniqueId)> Configured = new();
    private static readonly Dictionary<long, float> NextValueRefresh = new();
    private static readonly HashSet<long> DirtyValues = new();
    private static readonly Dictionary<long, GameItem> NightCycles = new();
    private static bool _inNight;

    internal static void ResetRuntimeCache()
    {
        Configured.Clear();
        NextValueRefresh.Clear();
        DirtyValues.Clear();
        NightCycles.Clear();
        _inNight = false;
    }

    internal static GameItem Create()
    {
        // Keep the native machine's inventories and rules. Only this cabinet's
        // aging callback is replaced with a batch using native cycle APIs.
        var item = MachineWineRack.AgeWell()
            ?? throw new InvalidOperationException("Could not create the native wine aging machine.");
        item.identifier = Id;
        item.SetName(BarrelItem.Text("流年", "Liunian"));
        UpdateDescription(item);
        item.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetValue(6666L);
        item.SetSprite(BarrelItem.SpriteAtlas, "liunian");
        item.getItemSpriteModifierForItemFunc = FixedArt;
        Configure(item);
        LiunianAgingCycle.Bind(item);
        item.ValidateShapeState();
        return item;
    }

    internal static void EnsureLoaded(GameItem item, bool forceRepair = false)
    {
        if (item?.identifier != Id) return;
        UpdateDescription(item);
        if (item.spriteAtlasPath != BarrelItem.SpriteAtlas || item.spritePath != "liunian")
            item.SetSprite(BarrelItem.SpriteAtlas, "liunian");
        item.getItemSpriteModifierForItemFunc = FixedArt;
        var window = item.contentWindow;
        BrewingExpansion.NativeWindowCallbacks.Bind(item);
        var key = item.Pointer.ToInt64();
        var stamp = (Window: window?.Pointer.ToInt64() ?? 0L,
            Child: window?.child?.Pointer.ToInt64() ?? 0L, UniqueId: item.uniqueId);
        if (!forceRepair && stamp.Child != 0 && Configured.TryGetValue(key, out var previous) && previous == stamp)
        {
            RefreshVisibleValueIfNeeded(item);
            return;
        }
        if (Configure(item, repairContents: true))
            Configured[key] = (Window: window?.Pointer.ToInt64() ?? 0L,
                Child: window?.child?.Pointer.ToInt64() ?? 0L, UniqueId: item.uniqueId);
    }

    private static void UpdateDescription(GameItem item)
    {
        var description = BarrelItem.Text(DescriptionZh, DescriptionEn);
        if (item.shortDescription != description) item.shortDescription = description;
        if (item.longDescription != description) item.longDescription = description;
    }

    private static bool Configure(GameItem item, bool repairContents = false)
    {
        var window = item?.contentWindow;
        if (window?.child == null) return false;
        BrewingExpansion.NativeWindowCallbacks.Bind(item);
        var layout = NativeLayout(window);
        // Save restoration can change GraphUtils' traversal order. The native
        // rack's inventory roles are defined by these fixed layout positions.
        var modules = layout?.GetElement(1, 1)?.TryCast<GameGridInventory>();
        var storage = layout?.GetElement(2, 1)?.TryCast<GameGridInventory>();
        if (modules == null || storage == null || modules.Pointer == storage.Pointer)
        {
            Core.Log?.Warning("Liunian native inventory positions are incomplete: " + item?.uniqueId);
            return false;
        }

        storage.identifier = StorageId;
        modules.identifier = ModuleId;
        var resized = false;
        if (storage.inventoryShape.width != 20 || storage.inventoryShape.height != 20)
        {
            storage.SetShape(20, 20);
            resized = true;
        }
        if (modules.inventoryShape.width != 6 || modules.inventoryShape.height != 20)
        {
            modules.SetShape(6, 20);
            resized = true;
        }
        if (repairContents)
        {
            try { LiunianInventoryRepair.Repair(layout, modules, storage, item.uniqueId); }
            catch (Exception ex) { Core.Log?.Warning("Liunian item repair could not finish: " + ex); }
        }
        var addedMargin = EnsureBottomMargin(window);
        var addedHeader = EnsureValueHeader(window);
        layout = NativeLayout(window);
        layout?.GetElement(0, 0)?.TryCast<TagElement>()?.SetText(BarrelItem.Text("电池区", "Battery Area"));
        layout?.GetElement(1, 0)?.TryCast<TagElement>()?.SetText(BarrelItem.Text("模块区", "Module Area"));
        layout?.GetElement(2, 0)?.TryCast<TagElement>()?.SetText(BarrelItem.Text("存放区", "Storage Area"));
        window.SetTitle(BarrelItem.Text("流年", "Liunian"));
        // A full cabinet can hold hundreds of bottles. Revalue it on demand,
        // rather than during every periodic item-maintenance scan.
        MarkValueDirty(storage);
        if (resized || addedMargin || addedHeader || repairContents) RefreshWindowLayout(window);
        return true;
    }

    internal static void MarkValueDirty(GameGridInventory inventory)
    {
        if (inventory?.identifier != StorageId && inventory?.identifier != ModuleId) return;
        var cabinet = inventory.GetParentItem();
        if (cabinet?.identifier == Id)
        {
            DirtyValues.Add(cabinet.Pointer.ToInt64());
            BarrelMaintenanceEvents.QueueItem(cabinet);
        }
    }

    private static void RefreshVisibleValueIfNeeded(GameItem cabinet)
    {
        var window = cabinet?.contentWindow;
        if (window == null) return;
        try { if (!window.IsVisible()) return; }
        catch { return; }
        var key = cabinet.Pointer.ToInt64();
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (!DirtyValues.Contains(key)
            && NextValueRefresh.TryGetValue(key, out var next) && now < next) return;
        UpdateValueHeader(Storage(cabinet));
    }

    internal static void RefreshWindowLayout(PixelWindow window)
    {
        if (window == null || (window.titleString != "流年" && window.titleString != "Liunian")) return;
        // Resizing the two grids does not immediately resize the native rack's
        // parent window. Without bottom-up validation it stays at the old size
        // until a battery or another item change triggers the game's refresh.
        var root = window.childElement?.TryCast<GridPixelElement>();
        var native = NativeLayout(window);
        if (root == null || native == null) return;
        UpdateValueHeader(native.GetElement(2, 1)?.TryCast<GameGridInventory>());
        native.GetElement(1, 1)?.TryCast<GameGridInventory>()?.Validate();
        native.GetElement(2, 1)?.TryCast<GameGridInventory>()?.Validate();
        native.GetElement(0, 1)?.TryCast<GameSlotInventory>()?.Validate();
        native.Validate();
        // The bottom-margin grid is another parent between native and root.
        // Its cached size must also follow a repaired/resized native layout.
        var padded = root.GetElement(0, 1)?.TryCast<GridPixelElement>();
        if (padded != null && padded.Pointer != native.Pointer) padded.Validate();
        if (root.Pointer != native.Pointer) root.Validate();
        window.Validate();
        try { if (window.IsVisible()) window.FixBounds(); }
        catch (Exception ex) { Core.Log?.Warning("Liunian bounds refresh deferred: " + ex.Message); }
    }

    private static GridPixelElement NativeLayout(PixelWindow window)
    {
        var root = window?.childElement?.TryCast<GridPixelElement>();
        if (root?.gridWidth == 1 && root.gridHeight == 2
            && root.GetElement(0, 0)?.TryCast<TagElement>() != null)
            root = root.GetElement(0, 1)?.TryCast<GridPixelElement>();
        if (root?.gridWidth == 1 && root.gridHeight == 2
            && root.GetElement(0, 1)?.TryCast<EmptyPixelElement>() != null)
            return root.GetElement(0, 0)?.TryCast<GridPixelElement>();
        return root;
    }

    private static bool EnsureValueHeader(PixelWindow window)
    {
        var previous = window?.childElement?.TryCast<GridPixelElement>();
        if (previous == null) return false;
        if (previous.gridWidth == 1 && previous.gridHeight == 2
            && previous.GetElement(0, 0)?.TryCast<TagElement>() != null) return false;

        var wrapper = new GridPixelElement(1, 2, true).SetSpacing(0, 0);
        var header = new TagElement(-1).SetText(ValueText(0));
        if (!window.Detach(previous.Cast<PixelElement>())) return false;
        if (wrapper.Attach(header.Cast<PixelElement>(), 0, 0)
            && wrapper.Attach(previous.Cast<PixelElement>(), 0, 1)
            && window.Attach(wrapper.Cast<PixelElement>())) return true;
        wrapper.Detach(previous.Cast<PixelElement>());
        window.Attach(previous.Cast<PixelElement>());
        Core.Log?.Warning("Could not add Liunian's value header; original layout restored.");
        return false;
    }

    internal static void UpdateValueHeader(GameGridInventory storage)
    {
        if (storage?.identifier != StorageId) return;
        var cabinet = storage.GetParentItem();
        if (cabinet?.identifier != Id) return;
        var root = cabinet.contentWindow?.childElement?.TryCast<GridPixelElement>();
        if (root?.gridWidth != 1 || root.gridHeight != 2) return;
        var header = root.GetElement(0, 0)?.TryCast<TagElement>();
        if (header == null) return;

        long total = 0;
        var wines = storage.childItems;
        for (var i = 0; wines != null && i < wines.Count; i++)
        {
            try
            {
                var wine = wines[i];
                if (wine != null)
                {
                    var worth = Math.Max(0L, wine.GetCurrentValue());
                    total = worth > long.MaxValue - total ? long.MaxValue : total + worth;
                }
            }
            catch (Exception ex) { Core.Log?.Warning("Could not value a wine in Liunian: " + ex.Message); }
        }
        var next = ValueText(total);
        if (header.GetText() != next) header.SetText(next);
        var key = cabinet.Pointer.ToInt64();
        DirtyValues.Remove(key);
        NextValueRefresh[key] = UnityEngine.Time.realtimeSinceStartup + VisibleValueRefreshSeconds;
    }

    private static string ValueText(long value)
    {
        var zh = value < 500 ? "还只是春前的一场雨。"
            : value < 1000 ? "一场春雨，刚刚湿润了土。"
            : value < 2000 ? "夏天到了，花已经开了。"
            : value < 5000 ? "秋收在手，酒坛已经满了。"
            : value < 10000 ? "冬天沉下来了，酒也沉了。"
            : "四季都在这一柜里了。";
        var en = value < 500 ? "Only a rain before spring."
            : value < 1000 ? "Spring rain has just softened the earth."
            : value < 2000 ? "Summer is here; the flowers have opened."
            : value < 5000 ? "Autumn's harvest is in; the jars are full."
            : value < 10000 ? "Winter has settled, and so has the wine."
            : "All four seasons rest in this cabinet.";
        return BarrelItem.Text(
            $"酒柜内含有酒酿的总价值为：{value}信用点。——{zh}",
            $"Total value of the wine in this cabinet: {value} credits. — {en}");
    }

    private static bool EnsureBottomMargin(PixelWindow window)
    {
        var native = window?.childElement?.TryCast<GridPixelElement>();
        if (native == null || NativeLayout(window)?.Pointer != native.Pointer) return false;
        var padded = new GridPixelElement(1, 2, true).SetSpacing(0, 0);
        var spacer = new EmptyPixelElement(1, BottomMarginPixels);
        if (!window.Detach(native.Cast<PixelElement>())) return false;
        if (padded.Attach(native.Cast<PixelElement>(), 0, 0)
            && padded.Attach(spacer.Cast<PixelElement>(), 0, 1)
            && window.Attach(padded.Cast<PixelElement>())) return true;
        padded.Detach(native.Cast<PixelElement>());
        window.Attach(native.Cast<PixelElement>());
        Core.Log?.Warning("Could not add Liunian's bottom margin; original layout restored.");
        return false;
    }

    internal static GameGridInventory Storage(GameItem item) =>
        NativeLayout(item?.contentWindow)?.GetElement(2, 1)?.TryCast<GameGridInventory>();
    internal static GameGridInventory Modules(GameItem item) =>
        NativeLayout(item?.contentWindow)?.GetElement(1, 1)?.TryCast<GameGridInventory>();
    internal static GameSlotInventory Battery(GameItem item) =>
        NativeLayout(item?.contentWindow)?.GetElement(0, 1)?.TryCast<GameSlotInventory>();

    internal static bool HasPower(GameItem item)
    {
        if (item?.contentWindow?.child == null) return false;
        var battery = NativeLayout(item.contentWindow)?.GetElement(0, 1)
            ?.TryCast<GameSlotInventory>()?.currentItem;
        return battery != null && PowerHelper.CanDrawPowerSource(battery, 1);
    }

    internal static int ChillStoredWine(GameItem item)
    {
        var storage = Storage(item);
        if (storage == null) return 0;
        var chilledFeatureId = MachineFridge.CreateChilledFeature()?.identifier;
        var count = 0;
        var wines = storage.childItems;
        for (var i = 0; wines != null && i < wines.Count; i++)
        {
            var wine = wines[i];
            if (wine == null || !MachineWineRack.CanAgeWine(wine))
                continue;
            // An active chilled feature already gives the full price bonus.
            // Reapplying it every night needlessly runs the native feature code
            // once per bottle in a full cabinet.
            var features = wine.itemFeatures;
            var alreadyChilled = false;
            for (var j = 0; !string.IsNullOrEmpty(chilledFeatureId)
                && features != null && j < features.Count; j++)
            {
                var feature = features[j];
                if (feature?.identifier == chilledFeatureId && !feature.isDisabled)
                {
                    alreadyChilled = true;
                    break;
                }
            }
            if (alreadyChilled) continue;
            MachineFridge.ChillItem(wine);
            count++;
        }
        return count;
    }

    internal static void CompleteCycle(GameItem item)
    {
        if (item?.identifier != Id) return;
        if (_inNight)
        {
            NightCycles[item.Pointer.ToInt64()] = item;
            return;
        }
        ApplyCycleChill(item);
    }

    internal static void BeginNight()
    {
        NightCycles.Clear();
        _inNight = true;
    }

    internal static void FinishNight()
    {
        _inNight = false;
        try
        {
            // Native night processing can expire yesterday's chilled feature
            // after the rack's aging callback. Chill only racks that completed
            // a powered cycle, once the entire native night has finished.
            foreach (var item in NightCycles.Values)
                try { ApplyCycleChill(item); }
                catch (Exception ex) { Core.Log?.Warning("Liunian night chilling failed: " + ex); }
        }
        finally { NightCycles.Clear(); }
    }

    internal static void AbortNight()
    {
        _inNight = false;
        NightCycles.Clear();
    }

    private static void ApplyCycleChill(GameItem item)
    {
        if (item?.identifier != Id) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int chilled;
        var validation = LiunianValidationBatch.Begin(item);
        using (validation) chilled = ChillStoredWine(item);
        var chillTicks = watch.ElapsedTicks;
        var storage = Storage(item);
        MarkValueDirty(storage);
        RefreshVisibleValueIfNeeded(item);
        watch.Stop();
        Core.Log?.Msg("[BE-LIUNIAN-PERF] chill=" + item.uniqueId + " bottles=" + chilled
            + " chillMs=" + LiunianAgingCycle.Milliseconds(chillTicks)
            + " parentMs=" + LiunianAgingCycle.Milliseconds(validation.FlushTicks)
            + " parentDeferred=" + validation.Deferred
            + " valueMs=" + LiunianAgingCycle.Milliseconds(watch.ElapsedTicks - chillTicks));
    }

}

[HarmonyLib.HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.UncheckedAccept))]
internal static class LiunianValueOnAcceptPatch
{
    [HarmonyLib.HarmonyPostfix]
    private static void Postfix(GameGridInventory __instance, bool __result)
    {
        if (!__result) return;
        try { LiunianAger.MarkValueDirty(__instance); }
        catch (Exception ex) { Core.Log?.Warning("Could not mark Liunian value after adding an item: " + ex.Message); }
    }
}

[HarmonyLib.HarmonyPatch(typeof(StoreReputation), nameof(StoreReputation.InitReputation))]
internal static class LiunianNewRunCachePatch
{
    [HarmonyLib.HarmonyPrefix]
    private static void Prefix() => LiunianAger.ResetRuntimeCache();
}

[HarmonyLib.HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.Expel))]
internal static class LiunianValueOnExpelPatch
{
    [HarmonyLib.HarmonyPostfix]
    private static void Postfix(GameGridInventory __instance, bool __result)
    {
        if (!__result) return;
        try { LiunianAger.MarkValueDirty(__instance); }
        catch (Exception ex) { Core.Log?.Warning("Could not mark Liunian value after removing an item: " + ex.Message); }
    }
}

[HarmonyLib.HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned))]
internal static class LiunianChillOnCyclePatch
{
    [HarmonyLib.HarmonyPostfix]
    private static void Postfix(GameItem machine)
    {
        if (machine?.identifier != LiunianAger.Id) return;
        LiunianModuleBatch.MarkModuleCompleted(machine);
        try { LiunianAger.CompleteCycle(machine); }
        catch (Exception ex) { Core.Log?.Warning("Liunian cycle chilling failed: " + ex); }
    }
}

[HarmonyLib.HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.EndNight))]
internal static class LiunianChillAfterNightPatch
{
    [HarmonyLib.HarmonyPrefix]
    private static void Prefix() => LiunianAger.BeginNight();

    [HarmonyLib.HarmonyPostfix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    private static void Postfix() => LiunianAger.FinishNight();

    [HarmonyLib.HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null) LiunianAger.AbortNight();
        return __exception;
    }
}
