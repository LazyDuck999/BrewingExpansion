using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class BrewingBatch
{
    private const int BatchSize = 4;
    private const int WaterRequiredMl = 3000;

    internal static void Start(GameItem barrel)
    {
        try
        {
            var input = BarrelItem.Input(barrel);
            var bottleGrid = BarrelItem.Bottles(barrel);
            var yeastGrid = BarrelItem.Yeast(barrel);
            var output = BarrelItem.Output(barrel);
            if (input == null || bottleGrid == null || yeastGrid == null || output == null) return;
            if (HasFermentingBottles(bottleGrid))
            {
                BarrelItem.SetStatus(barrel, "本批正在发酵，次日可领取", "Fermenting; collect wine tomorrow");
                return;
            }
            // The player may have taken every fermenting bottle out since the
            // last scan. The barrel's saved visual tag must not lock a new batch.
            if (barrel.IsTag("XIAOWO_BARREL_ACTIVE")) BarrelItem.SetVisual(barrel, false);
            var pulps = new List<GameItem>();
            foreach (var item in InventoryOps.Snapshot(input)) if (item.identifier == PulpItem.Id) pulps.Add(item);
            if (pulps.Count < 2 || PulpItem.FruitId(pulps[0]) == null || PulpItem.FruitId(pulps[1]) == null)
            {
                BarrelItem.SetStatus(barrel, "需要两瓶各含四个同种果实的原浆", "Two pulps, each holding four matching fruits, are required");
                return;
            }
            var bottles = new List<GameItem>();
            foreach (var item in InventoryOps.Snapshot(bottleGrid))
                if (item.identifier == "wine_bottle" && !WineHelper.IsFermentingOrFinished(item)
                    && WaterHelper.GetCurrentCapacityML(item) == 0 && item.FindAllChildItems()?.Count == 0)
                    bottles.Add(item);
            if (bottles.Count < BatchSize)
            {
                BarrelItem.SetStatus(barrel, "需要四个空酒瓶", "Four empty wine bottles are required");
                return;
            }
            var yeasts = new List<GameItem>();
            GameItem infiniteYeast = null;
            foreach (var item in InventoryOps.Snapshot(yeastGrid))
            {
                if (IsInfiniteYeast(item)) infiniteYeast ??= item;
                else if (item.identifier?.StartsWith("wine_yeast_", StringComparison.Ordinal) == true) yeasts.Add(item);
            }
            if (infiniteYeast == null && yeasts.Count < BatchSize)
            {
                BarrelItem.SetStatus(barrel, "需要一个无限酵母或四个普通酵母", "Add one infinite yeast or four ordinary yeasts");
                return;
            }
            if (WaterHelper.GetCurrentCapacityML(barrel) < WaterRequiredMl - 1)
            {
                BarrelItem.SetStatus(barrel, "请先灌满3000 ml水", "Fill the barrel with 3000 ml water first");
                return;
            }
            if (InventoryOps.Snapshot(output).Count > 0)
            {
                BarrelItem.SetStatus(barrel, "先取走上批成品", "Collect the previous batch first");
                return;
            }

            var fruitAId = PulpItem.FruitId(pulps[0]);
            var fruitBId = PulpItem.FruitId(pulps[1]);
            var started = 0;
            for (var i = 0; i < BatchSize; i++)
            {
                var bottle = bottles[i];
                var bottleIngredients = IngredientGrid(bottle);
                if (bottleIngredients == null) break;
                var fruitA = DirectoryMaster.Item(fruitAId, true);
                var fruitB = DirectoryMaster.Item(fruitBId, true);
                if (fruitA == null || fruitB == null)
                {
                    fruitA?.Destroy();
                    fruitB?.Destroy();
                    break;
                }
                if (!InventoryOps.Accept(bottleIngredients, fruitA))
                {
                    fruitA.Destroy();
                    fruitB.Destroy();
                    break;
                }
                if (!InventoryOps.Accept(bottleIngredients, fruitB))
                {
                    fruitA.Destroy();
                    fruitB.Destroy();
                    break;
                }
                var yeast = infiniteYeast == null ? yeasts[i] : CreateTemporaryYeast(infiniteYeast);
                if (yeast == null) break;
                if (infiniteYeast == null)
                {
                    if (!InventoryOps.Move(yeastGrid, bottleIngredients, yeast)) break;
                }
                else if (!InventoryOps.Accept(bottleIngredients, yeast))
                {
                    yeast.Destroy();
                    break;
                }
                var fermentationStarted = false;
                try
                {
                    WaterHelper.TransferLiquid(barrel, bottle);
                    fermentationStarted = WaterHelper.GetCurrentCapacityML(bottle) >= 749
                        && WineHelper.TryStartFermentation(bottle);
                }
                finally
                {
                    // Native fermentation returns reusable yeast to the player. These four
                    // copies only stand in for the one reactor in the barrel, so discard them.
                    if (infiniteYeast != null) DestroyTemporaryYeast(yeast);
                }
                if (!fermentationStarted) break;
                started++;
            }
            if (started != BatchSize)
            {
                BarrelItem.SetStatus(barrel, $"只启动了 {started}/4 瓶；未消耗的材料保留在桶内",
                    $"Only {started}/4 bottles started; unused ingredients remain in the barrel");
                Core.Log?.Warning("Barrel partial start: " + started + "/4 bottles, barrel=" + barrel.uniqueId);
                if (started > 0) BarrelItem.SetVisual(barrel, true);
                return;
            }
            foreach (var pulp in pulps.GetRange(0, 2)) pulp.Destroy();
            // A reusable reactor is the barrel's catalyst. It already remains in
            // the yeast slot; moving it to the backpack can run native grid UI
            // validation while the backpack has no live background (for example
            // when brewing from a loaded save), and can detach the reactor.
            BarrelItem.SetVisual(barrel, true);
            BarrelItem.SetStatus(barrel, "四瓶正在发酵，次日可领取", "Four bottles fermenting; collect tomorrow");
            Core.Log?.Msg("Four-bottle barrel fermentation started: " + barrel.uniqueId);
        }
        catch (Exception ex) { Core.Log?.Error("Starting barrel fermentation failed: " + ex); }
    }

    private static bool IsInfiniteYeast(GameItem item)
    {
        var id = item?.identifier;
        return id == "be_golden_yeast_reactor" || id == "be_advanced_yeast_reactor"
            || id == "be_forbidden_yeast_reactor" || id == "wine_yeast_infinite"
            || item?.IsTag("BE_FORBIDDEN_YEAST_REACTOR") == true
            || item?.IsTag("WINE_YEAST_REUSABLE_TAG") == true;
    }

    private static GameItem CreateTemporaryYeast(GameItem reactor)
    {
        // The forbidden reactor keeps the vanilla red yeast identifier; its custom tag
        // and power must be recreated through the mod factory, not the red yeast factory.
        var id = reactor.IsTag("BE_FORBIDDEN_YEAST_REACTOR")
            ? "be_forbidden_yeast_reactor" : reactor.identifier;
        return DirectoryMaster.Item(id, true);
    }

    private static void DestroyTemporaryYeast(GameItem yeast)
    {
        try
        {
            if (yeast?.parentInventory != null) yeast.Destroy();
        }
        catch (Exception ex) { Core.Log?.Warning("Could not discard temporary barrel yeast: " + ex); }
    }

    private static GameGridInventory IngredientGrid(GameItem bottle)
    {
        if (bottle?.contentWindow?.child == null) return null;
        var grids = GraphUtils.FindAllChildrenType<GameGridInventory>(bottle.contentWindow.child,
            (Il2CppSystem.Func<GameGridInventory, bool>)(grid => grid != null));
        return grids != null && grids.Count > 0 ? grids[0] : null;
    }

    private static bool HasFermentingBottles(GameGridInventory bottles)
    {
        foreach (var bottle in InventoryOps.Snapshot(bottles))
            if (bottle.identifier == "wine_bottle" && WineHelper.IsFermentingOrFinished(bottle)
                && !WineHelper.IsFinishedWine(bottle)) return true;
        return false;
    }

    internal static void Tick(GameItem barrel)
    {
        var bottles = BarrelItem.Bottles(barrel);
        if (bottles == null) return;
        var running = HasFermentingBottles(bottles);
        var active = barrel.IsTag("XIAOWO_BARREL_ACTIVE");
        if (running && !active) BarrelItem.SetVisual(barrel, true);
        else if (!running && active)
        {
            BarrelItem.SetVisual(barrel, false);
            var finishedInside = false;
            foreach (var bottle in InventoryOps.Snapshot(bottles))
                if (bottle.identifier == "wine_bottle" && WineHelper.IsFinishedWine(bottle))
                { finishedInside = true; break; }
            if (finishedInside)
                BarrelItem.SetStatus(barrel, "酒已酿成，过夜后将移入成品区",
                    "Wine finished; it will move to output overnight");
            else
                BarrelItem.SetStatus(barrel, "发酵酒瓶已取出，可再次装料酿造",
                    "Fermenting bottles removed; ready for another batch");
        }
    }

    internal static void FinishAtNight(GameItem barrel)
    {
        var source = BarrelItem.Bottles(barrel);
        var output = BarrelItem.Output(barrel);
        if (source == null || output == null) return;
        var completed = 0;
        var stillFermenting = 0;
        var pendingOutput = 0;
        var foundWine = false;
        foreach (var bottle in InventoryOps.Snapshot(source))
        {
            if (bottle.identifier != "wine_bottle" || !WineHelper.IsFermentingOrFinished(bottle)) continue;
            foundWine = true;
            if (!WineHelper.IsFinishedWine(bottle)
                && BrewingExpansion.BrewingExpansionMod.ReadBottleMark(bottle)
                    == BrewingExpansion.BrewingExpansionMod.NianHeshiWineId
                && (bottle.GetTagReadonly("FERMENTATION_TIME_REMAINING_INT")?.valueInt ?? 0) > 0)
            {
                stillFermenting++;
                continue;
            }
            if (!WineHelper.IsFinishedWine(bottle)) WineHelper.FinishFermentation(bottle);
            if (!WineHelper.IsFinishedWine(bottle)) { stillFermenting++; continue; }
            RefreshExpansionWine(bottle);
            if (InventoryOps.Move(source, output, bottle)) completed++;
            else pendingOutput++;
        }
        if (!foundWine)
        {
            if (barrel.IsTag("XIAOWO_BARREL_ACTIVE"))
            {
                BarrelItem.SetVisual(barrel, false);
                BarrelItem.SetStatus(barrel, "发酵酒瓶已取出，可再次装料酿造",
                    "Fermenting bottles removed; ready for another batch");
            }
            return;
        }
        if (completed > 0) Core.Log?.Msg("Barrel finished " + completed + " bottle(s): " + barrel.uniqueId);
        if (stillFermenting > 0)
        {
            BarrelItem.SetVisual(barrel, true);
            BarrelItem.SetStatus(barrel, $"仍有 {stillFermenting} 瓶发酵中", $"{stillFermenting} bottles are still fermenting");
            return;
        }
        BarrelItem.SetVisual(barrel, false);
        if (pendingOutput > 0)
        {
            BarrelItem.SetStatus(barrel, $"{pendingOutput} 瓶已酿成，成品区空间不足；取走成品后会继续转移",
                $"{pendingOutput} finished bottles await space in the output");
            return;
        }
        BarrelItem.SetStatus(barrel, $"已完成 {completed} 瓶；请从成品区取酒",
            $"Finished {completed} bottles; collect them from the output");
    }

    private static void RefreshExpansionWine(GameItem bottle)
    {
        try
        {
            var type = AccessTools.TypeByName("BrewingExpansion.BrewingExpansionMod");
            var read = AccessTools.Method(type, "ReadBottleMark", new[] { typeof(GameItem) });
            var replace = AccessTools.Method(type, "ApplyWineReplacement",
                new[] { typeof(GameItem), typeof(string), typeof(string) });
            var id = read?.Invoke(null, new object[] { bottle }) as string;
#if BREWING_TEST
            Core.Log?.Msg("BREWING_TEST wine before replacement: id=" + bottle.identifier
                + " canAge=" + MachineWineRack.CanAgeWine(bottle)
                + " age=" + AgableHelper.GetAge(bottle)
                + " finished=" + WineHelper.IsFinishedWine(bottle));
#endif
            if (!string.IsNullOrEmpty(id)) replace?.Invoke(null, new object[] { bottle, id, "BrewingBarrel" });
#if BREWING_TEST
            Core.Log?.Msg("BREWING_TEST wine after replacement: id=" + bottle.identifier
                + " canAge=" + MachineWineRack.CanAgeWine(bottle)
                + " age=" + AgableHelper.GetAge(bottle)
                + " finished=" + WineHelper.IsFinishedWine(bottle));
#endif
        }
        catch (Exception ex) { Core.Log?.Warning("Expansion wine conversion deferred: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.EndNight))]
internal static class FinishBarrelAtNightPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        try
        {
            var items = PlayerStore.instance?.FindAllItem(true);
            if (items == null) return;
            for (var i = 0; i < items.Count; i++)
                if (items[i]?.identifier == BarrelItem.Id) BrewingBatch.FinishAtNight(items[i]);
        }
        catch (Exception ex) { Core.Log?.Error("Barrel night completion failed: " + ex); }
    }
}
