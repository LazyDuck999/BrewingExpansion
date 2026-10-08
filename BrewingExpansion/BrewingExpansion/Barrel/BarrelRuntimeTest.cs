#if BREWING_TEST
using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class BarrelRuntimeTest
{
    private static bool _done;
    private static int _readyFrame;

    internal static void Tick()
    {
        if (_done) return;
        if (PlayerStore.instance == null || EmporiumEntry.Instance == null) return;
        if (_readyFrame == 0) _readyFrame = Time.frameCount + 60;
        if (Time.frameCount < _readyFrame) return;
        _done = true;
        try { Run(); }
        catch (Exception ex) { Core.Log?.Error("BREWING_TEST FAIL: " + ex); }
    }

    private static void Run()
    {
        var incubatorProbe = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.IncubatorId, true);
        var incubatorRoot = BrewingExpansion.IncubatorLayout.NativeLayout(incubatorProbe?.contentWindow);
        var incubatorFrame = incubatorProbe?.contentWindow?.childElement?.TryCast<GridPixelElement>();
        Core.Log?.Msg("BREWING_TEST incubator root=" + incubatorRoot?.gridWidth + "x" + incubatorRoot?.gridHeight
            + " window=" + incubatorProbe?.contentWindow?.widthPixels + "x" + incubatorProbe?.contentWindow?.heightPixels);
        if (incubatorRoot != null)
            for (var y = 0; y < incubatorRoot.gridHeight; y++)
                for (var x = 0; x < incubatorRoot.gridWidth; x++)
                {
                    var node = incubatorRoot.GetElement(x, y);
                    Core.Log?.Msg("BREWING_TEST incubator pixel " + x + "," + y
                        + " grid=" + node?.TryCast<GridPixelElement>()?.gridWidth + "x" + node?.TryCast<GridPixelElement>()?.gridHeight
                        + " slot=" + (node?.TryCast<GameSlotInventory>() != null)
                        + " inv=" + node?.TryCast<GameGridInventory>()?.inventoryShape?.width + "x" + node?.TryCast<GameGridInventory>()?.inventoryShape?.height
                        + " tag=" + (node?.TryCast<TagElement>() != null));
                }
        if (incubatorRoot?.gridWidth != 4 || incubatorRoot.gridHeight != 2
            || incubatorFrame?.gridWidth != 1 || incubatorFrame.gridHeight != 2
            || incubatorFrame.GetElement(0, 1)?.TryCast<EmptyPixelElement>() == null
            || incubatorFrame.heightPixels < incubatorRoot.heightPixels + BrewingExpansion.IncubatorLayout.BottomMarginPixels
            || incubatorRoot.GetElement(1, 1)?.TryCast<GameGridInventory>()?.inventoryShape?.width != 12
            || incubatorRoot.GetElement(1, 1)?.TryCast<GameGridInventory>()?.inventoryShape?.height != 12
            || incubatorRoot.GetElement(2, 1)?.TryCast<GameGridInventory>()?.inventoryShape?.width != 12
            || incubatorRoot.GetElement(2, 1)?.TryCast<GameGridInventory>()?.inventoryShape?.height != 12
            || incubatorProbe.onCycleEndSlotItemFunc != null)
            throw new InvalidOperationException("Incubator did not remove the module area or resize its native inventories.");
        incubatorProbe.contentWindow.Show();
        if (incubatorProbe.contentWindow.widthPixels < incubatorFrame.widthPixels
            || incubatorProbe.contentWindow.heightPixels < incubatorFrame.heightPixels + incubatorProbe.contentWindow.titleHeight)
            throw new InvalidOperationException("Incubator window does not enclose its 12x12 inventories.");
        Core.Log?.Msg("BREWING_TEST PASS: incubator has no module area; native input/output are 12x12 and window encloses both.");
        var savedIncubator = SaveManager.DecodeNodes(SaveManager.EncodeNodes(incubatorProbe.Cast<GraphNodeStorage>()));
        BrewingExpansion.IncubatorLayout.Configure(savedIncubator);
        var savedIncubatorRoot = BrewingExpansion.IncubatorLayout.NativeLayout(savedIncubator?.contentWindow);
        var savedIncubatorFrame = savedIncubator?.contentWindow?.childElement?.TryCast<GridPixelElement>();
        if (savedIncubatorRoot?.gridWidth != 4 || savedIncubatorRoot.gridHeight != 2
            || savedIncubatorFrame?.gridWidth != 1 || savedIncubatorFrame.gridHeight != 2
            || BrewingExpansion.IncubatorLayout.Input(savedIncubator)?.inventoryShape?.width != 12
            || BrewingExpansion.IncubatorLayout.Output(savedIncubator)?.inventoryShape?.height != 12
            || savedIncubator.onCycleEndSlotItemFunc != null)
            throw new InvalidOperationException("Incubator layout or daily-processing rule did not survive save restoration.");
        savedIncubator.Destroy();
        Core.Log?.Msg("BREWING_TEST PASS: incubator layout and daily-processing rule survive serialization.");
        if (incubatorRoot != null)
            for (var y = 0; y < incubatorRoot.gridHeight; y++)
                for (var x = 0; x < incubatorRoot.gridWidth; x++)
                {
                    var node = incubatorRoot.GetElement(x, y);
                    Core.Log?.Msg("BREWING_TEST incubator pixel " + x + "," + y + "=" + node?.GetType().Name
                        + " grid=" + node?.TryCast<GridPixelElement>()?.gridWidth + "x" + node?.TryCast<GridPixelElement>()?.gridHeight
                        + " slot=" + (node?.TryCast<GameSlotInventory>() != null)
                        + " inv=" + node?.TryCast<GameGridInventory>()?.inventoryShape?.width + "x" + node?.TryCast<GameGridInventory>()?.inventoryShape?.height
                        + " tag=" + (node?.TryCast<TagElement>() != null));
                }
        if (incubatorProbe?.contentWindow?.child != null)
        {
            var invs = GraphUtils.FindAllChildrenType<GameInventory>(incubatorProbe.contentWindow.child,
                (Il2CppSystem.Func<GameInventory, bool>)(_ => true));
            for (var i = 0; invs != null && i < invs.Count; i++)
                Core.Log?.Msg("BREWING_TEST incubator inventory " + i + " " + invs[i].GetType().Name
                    + " id=" + invs[i].identifier + " shape=" + invs[i].TryCast<GameGridInventory>()?.inventoryShape?.width + "x" + invs[i].TryCast<GameGridInventory>()?.inventoryShape?.height);
        }
        var basicBattery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuBatteryId, true);
        var midBattery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId, true);
        var highBattery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId, true);
        if (basicBattery?.GetTagReadonly("power_source_item_recharge")?.valueInt != 6
            || midBattery?.GetTagReadonly("power_source_item_recharge")?.valueInt != 8
            || highBattery?.GetTagReadonly("power_source_item_recharge")?.valueInt != 12)
            throw new InvalidOperationException("Battery nightly recharge is not 6/8/12 kJ.");
        Core.Log?.Msg("BREWING_TEST PASS: three xuxu batteries recharge 6/8/12 kJ nightly.");
        var vanillaRack = MachineWineRack.AgeWell();
        Core.Log?.Msg("BREWING_TEST native ager id=" + vanillaRack?.identifier
            + " name=" + vanillaRack?.name + " shape=" + vanillaRack?.shape?.width + "x" + vanillaRack?.shape?.height
            + " value=" + vanillaRack?.unitValue + " sprite=" + vanillaRack?.spriteAtlasPath + "/" + vanillaRack?.spritePath
            + " agingDays=" + (vanillaRack == null ? -1 : MachineWineRack.GetAgingDays(vanillaRack))
            + " cycle=" + (vanillaRack?.onCycleEndSlotItemFunc != null));
        if (vanillaRack?.contentWindow?.child != null)
        {
            var grids = GraphUtils.FindAllChildrenType<GameGridInventory>(vanillaRack.contentWindow.child,
                (Il2CppSystem.Func<GameGridInventory, bool>)(_ => true));
            if (grids != null)
                for (var i = 0; i < grids.Count; i++)
                    Core.Log?.Msg("BREWING_TEST native ager grid " + i + " id=" + grids[i].identifier
                        + " size=" + grids[i].inventoryShape.width + "x" + grids[i].inventoryShape.height);
            var slots = GraphUtils.FindAllChildrenType<GameSlotInventory>(vanillaRack.contentWindow.child,
                (Il2CppSystem.Func<GameSlotInventory, bool>)(_ => true));
            if (slots != null)
                for (var i = 0; i < slots.Count; i++)
                    Core.Log?.Msg("BREWING_TEST native ager slot " + i + " id=" + slots[i].identifier);
        }
        var liunian = DirectoryMaster.Item(LiunianAger.Id, true);
        Core.Log?.Msg("BREWING_TEST Liunian window padding=" + liunian?.contentWindow?.yPadding
            + " titleHeight=" + liunian?.contentWindow?.titleHeight
            + " height=" + liunian?.contentWindow?.heightPixels);
        var shopStock = (string[])typeof(NightBrewingShop)
            .GetField("StockIds", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
        if (shopStock == null || Array.IndexOf(shopStock, LiunianAger.Id) < 0)
            throw new InvalidOperationException("Liunian is missing from the brewing shop stock list.");
        var ageableFound = 0;
        GameItem ageableSample = null;
        var ownedItems = PlayerStore.instance.FindAllItem(true);
        if (ownedItems != null)
            for (var i = 0; i < ownedItems.Count && ageableFound < 3; i++)
                if (ownedItems[i] != null && MachineWineRack.CanAgeWine(ownedItems[i]))
                {
                    ageableSample ??= ownedItems[i];
                    Core.Log?.Msg("BREWING_TEST native ageable item: " + ownedItems[i].identifier
                        + " name=" + ownedItems[i].name);
                    ageableFound++;
                }
        Core.Log?.Msg("BREWING_TEST native ageable examples=" + ageableFound);
        var wineStorage = LiunianAger.Storage(liunian);
        var moduleStorage = LiunianAger.Modules(liunian);
        var machineSlots = liunian?.contentWindow?.child == null ? null
            : GraphUtils.FindAllChildrenType<GameSlotInventory>(liunian.contentWindow.child,
                (Il2CppSystem.Func<GameSlotInventory, bool>)(_ => true));
        if (liunian == null || liunian.unitValue != 6666 || liunian.shape.width != 3 || liunian.shape.height != 3
            || wineStorage?.inventoryShape.width != 20 || wineStorage.inventoryShape.height != 20
            || moduleStorage?.inventoryShape.width != 6 || moduleStorage.inventoryShape.height != 20
            || machineSlots?.Count != 2 || liunian.onCycleEndSlotItemFunc == null
            || MachineWineRack.GetAgingDays(liunian) != MachineWineRack.GetAgingDays(vanillaRack)
            || liunian.spritePath != "liunian" || BarrelSprite.Get("liunian") == null)
            throw new InvalidOperationException("Liunian did not retain the native aging mechanism or requested sizes.");
        liunian.contentWindow.Show(); // Check the newly bought, empty machine before any battery or wine is added.
        var firstOpenRoot = liunian.contentWindow.childElement?.TryCast<GridPixelElement>();
        Core.Log?.Msg("BREWING_TEST Liunian first open: window=" + liunian.contentWindow.widthPixels
            + "x" + liunian.contentWindow.heightPixels + " layout=" + firstOpenRoot?.widthPixels
            + "x" + firstOpenRoot?.heightPixels);
        if (firstOpenRoot == null || liunian.contentWindow.widthPixels < firstOpenRoot.widthPixels
            || liunian.contentWindow.heightPixels < firstOpenRoot.heightPixels + liunian.contentWindow.titleHeight)
            throw new InvalidOperationException("Liunian window does not enclose its grids on first open.");
        Core.Log?.Msg("BREWING_TEST PASS: Liunian first-open border encloses both empty grids without a battery.");
        var rackNodes = SaveManager.EncodeNodes(liunian.Cast<GraphNodeStorage>());
        var loadedRack = SaveManager.DecodeNodes(rackNodes);
        LiunianAger.EnsureLoaded(loadedRack);
        var restoredLayout = loadedRack?.contentWindow?.childElement?.TryCast<GridPixelElement>();
        if (loadedRack == null || LiunianAger.Storage(loadedRack)?.inventoryShape.width != 20
            || LiunianAger.Modules(loadedRack)?.inventoryShape.height != 20
            || loadedRack.onCycleEndSlotItemFunc == null
            || restoredLayout?.gridWidth != 1 || restoredLayout.gridHeight != 2
            || restoredLayout.GetElement(0, 1)?.TryCast<EmptyPixelElement>() == null
            || loadedRack.getItemSpriteModifierForItemFunc?.Invoke(loadedRack)?.Count != 0)
            throw new InvalidOperationException("Liunian lost its inventory layout or aging callback after serialization.");
        loadedRack.Destroy();
        Core.Log?.Msg("BREWING_TEST PASS: Liunian uses the native aging machine, 3x3 art, 20x20 wine storage and 6x20 modules, including after save restoration.");
        var nativeFilter = DirectoryMaster.Item("water_filter", true);
        var nativePurifier = DirectoryMaster.Item("portable_water_purifier", true);
        if (nativeFilter == null || !nativeFilter.IsTag("WATER_FILTER")
            || nativePurifier == null || nativePurifier.onThisTargetItemFunc == null
            || nativePurifier.canThisTargetItemFunc == null)
            throw new InvalidOperationException("The native filter and purifier templates are not distinct.");
        for (var i = 0; i < XuxuWaterPurifier.Ids.Length; i++)
        {
            var purifier = DirectoryMaster.Item(XuxuWaterPurifier.Ids[i], true);
            Core.Log?.Msg("BREWING_TEST purifier " + i + ": exists=" + (purifier != null)
                + " cartridge=" + (purifier != null && purifier.IsTag("WATER_FILTER"))
                + " target=" + (purifier != null && purifier.onThisTargetItemFunc != null)
                + " sprite=" + (purifier == null ? "null" : purifier.spritePath)
                + " durability=" + (purifier == null ? -1 : DurabilityHelper.GetMaxDurability(purifier))
                + " value=" + (purifier == null ? -1 : purifier.unitValue)
                + " base=" + (purifier == null ? -1 : purifier.unitBaseValue));
            if (purifier == null || purifier.IsTag("WATER_FILTER")
                || purifier.onThisTargetItemFunc == null || purifier.canThisTargetItemFunc == null
                || purifier.onCycleEndSlotItemFunc != null
                || purifier.spritePath != XuxuWaterPurifier.SpriteKeys[i]
                || purifier.shape.globalWidth != 2 || purifier.shape.globalHeight != 2
                || DurabilityHelper.GetMaxDurability(purifier) != XuxuWaterPurifier.MaxDurabilities[i]
                || purifier.unitValue != XuxuWaterPurifier.Prices[i])
                throw new InvalidOperationException("Water purifier " + i + " has incorrect filtering, sprite, durability or price.");
            DurabilityHelper.SetCurrentDurability(purifier, XuxuWaterPurifier.MaxDurabilities[i] - 50);
            if (!XuxuWaterPurifier.RestoreAtEndNight(purifier)
                || DurabilityHelper.GetRealDurability(purifier) != Math.Min(
                    XuxuWaterPurifier.MaxDurabilities[i], XuxuWaterPurifier.MaxDurabilities[i] - 50 + XuxuWaterPurifier.DailyRestores[i]))
                throw new InvalidOperationException("Water purifier " + i + " did not restore the expected daily durability.");
        }
        var legacy = DirectoryMaster.Item("water_filter", true);
        legacy.identifier = XuxuWaterPurifier.BasicId;
        XuxuWaterPurifier.EnsureLoaded(legacy);
        if (legacy.IsTag("WATER_FILTER") || legacy.onThisTargetItemFunc == null
            || legacy.onCycleEndSlotItemFunc != null)
            throw new InvalidOperationException("A saved cartridge was not migrated to purifier behavior.");
        Core.Log?.Msg("BREWING_TEST PASS: three native portable purifiers have target actions, requested prices, durability and daily recovery; legacy item migrated.");
        var waterTarget = DirectoryMaster.Item("water_pitcher", true);
        var workingPurifier = DirectoryMaster.Item(XuxuWaterPurifier.BasicId, true);
        WaterHelper.FillWithRustWaterLowEnd(waterTarget);
        var purityBefore = WaterHelper.GetWaterPurity(waterTarget);
        var durabilityBefore = DurabilityHelper.GetRealDurability(workingPurifier);
        var nativeCanPurify = nativePurifier.canThisTargetItemFunc.Invoke(nativePurifier, waterTarget);
        var modCanPurify = workingPurifier.canThisTargetItemFunc.Invoke(workingPurifier, waterTarget);
        Core.Log?.Msg("BREWING_TEST purifier action: nativeCan=" + nativeCanPurify
            + " modCan=" + modCanPurify + " water=" + WaterHelper.GetCurrentCapacityML(waterTarget)
            + " purityBefore=" + purityBefore + " durabilityBefore=" + durabilityBefore);
        if (!nativeCanPurify || !modCanPurify)
            throw new InvalidOperationException("The native or mod purifier cannot target a dirty water pitcher.");
        workingPurifier.onThisTargetItemFunc.Invoke(workingPurifier, waterTarget);
        var purityAfter = WaterHelper.GetWaterPurity(waterTarget);
        var durabilityAfter = DurabilityHelper.GetRealDurability(workingPurifier);
        Core.Log?.Msg("BREWING_TEST purifier action: purityAfter=" + purityAfter
            + " durabilityAfter=" + durabilityAfter);
        if (purityAfter <= purityBefore || durabilityAfter >= durabilityBefore)
            throw new InvalidOperationException("The mod purifier did not clean the water and spend durability.");
        Core.Log?.Msg("BREWING_TEST PASS: native targeting chain purifies dirty water and spends durability.");
        var barrel = DirectoryMaster.Item(BarrelItem.Id, true);
        if (barrel == null) throw new InvalidOperationException("The barrel was not registered in the item directory.");
        Core.Log?.Msg("BREWING_TEST barrel value=" + barrel.unitValue + ", base=" + barrel.unitBaseValue);
        if (barrel.unitValue != 1400) throw new InvalidOperationException("The barrel price is not 1400.");
        if (BarrelSprite.Get("barrel_idle") == null || BarrelSprite.Get("barrel_active") == null)
            throw new InvalidOperationException("A barrel sprite could not be decoded.");
        var sign = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.BrewerySignId, true);
        if (sign == null || sign.itemTypes == null || sign.itemTypes.Count != 0)
            throw new InvalidOperationException("The brewery sign still exposes an untranslated item type.");
        Core.Log?.Msg("BREWING_TEST PASS: brewery sign has no untranslated item type.");
        Core.Log?.Msg("BREWING_TEST PASS: barrel factory and both sprites are available.");
        if (barrel.shape.globalWidth != 3 || barrel.shape.globalHeight != 3)
            throw new InvalidOperationException("Barrel is not 3x3.");
        var input = BarrelItem.Input(barrel);
        var bottles = BarrelItem.Bottles(barrel);
        var yeast = BarrelItem.Yeast(barrel);
        var output = BarrelItem.Output(barrel);
        if (input == null || bottles == null || yeast == null || output == null)
            throw new InvalidOperationException("One or more barrel inventory regions are missing.");
        if (input.inventoryShape.width != 4 || input.inventoryShape.height != 2
            || bottles.inventoryShape.width != 4 || bottles.inventoryShape.height != 4
            || yeast.inventoryShape.width != 4 || yeast.inventoryShape.height != 2
            || output.inventoryShape.width != 4 || output.inventoryShape.height != 4)
            throw new InvalidOperationException("The barrel inventory regions have incorrect dimensions.");
        Core.Log?.Msg("BREWING_TEST PASS: barrel grids are 4x2, 4x4, 4x2 and 4x4.");
        var concentrator = DirectoryMaster.Item(ConcentratorItem.Id, true);
        if (concentrator == null || concentrator.unitValue != 500)
            throw new InvalidOperationException("The concentrator price is not 500.");
        if (concentrator == null || concentrator.shape.globalWidth != 3 || concentrator.shape.globalHeight != 3)
            throw new InvalidOperationException("The 3x3 pulp concentrator was not registered.");
        if (concentrator.itemTypes == null || concentrator.itemTypes.Count != 1
            || concentrator.itemTypes[0] != "MACHINE" || !string.IsNullOrEmpty(concentrator.flavorText)
            || concentrator.IsTag("WATER_FILTER") || concentrator.IsTag("DURABILITY_TAG")
            || concentrator.onCycleEndSlotItemFunc != null)
            throw new InvalidOperationException("Concentrator still exposes filter classifications or flavor text.");
        for (var i = 0; i < concentrator.itemFeatures.Count; i++)
            if (concentrator.itemFeatures[i]?.identifier == "DURABILITY_TAG")
                throw new InvalidOperationException("Concentrator still exposes the filter durability feature.");
        Core.Log?.Msg("BREWING_TEST PASS: concentrator tooltip has only the machine type and no inherited filter details.");
        var batterySlot = ConcentratorItem.Battery(concentrator);
        var fruitRegion = ConcentratorItem.Fruits(concentrator);
        var pulpRegion = ConcentratorItem.Pulps(concentrator);
        if (batterySlot == null || batterySlot.defaultGridWidth != 1 || batterySlot.defaultGridHeight != 2
            || fruitRegion?.inventoryShape.width != 8 || fruitRegion.inventoryShape.height != 2
            || pulpRegion?.inventoryShape.width != 2 || pulpRegion.inventoryShape.height != 2)
            throw new InvalidOperationException("The concentrator inventory regions have incorrect dimensions.");
        var samplePulp = PulpItem.Create();
        if (samplePulp.shape.globalWidth != 2 || samplePulp.shape.globalHeight != 2)
            throw new InvalidOperationException("Fruit pulp is not 2x2.");
        var sampleFruitGrid = PulpItem.Fruits(samplePulp);
        var rejectedBottle = DirectoryMaster.Item("wine_bottle", true);
        var rejectedFruit = DirectoryMaster.Item("green_grape_fruit", true);
        if (sampleFruitGrid.MayHaveValidInventorySlot(rejectedBottle)
            || sampleFruitGrid.MayHaveValidInventorySlot(rejectedFruit)
            || InventoryOps.Accept(sampleFruitGrid, rejectedBottle)
            || InventoryOps.Accept(sampleFruitGrid, rejectedFruit)
            || InventoryOps.Snapshot(sampleFruitGrid).Count != 0)
            throw new InvalidOperationException("The sealed pulp inventory accepted an external item.");
        Core.Log?.Msg("BREWING_TEST PASS: pulp fruit storage rejects foreign items and extra fruit.");
        if (!PulpItem.IsInternalWindow(samplePulp.contentWindow)
            || PulpItem.IsInternalWindow(barrel.contentWindow))
            throw new InvalidOperationException("Pulp-only window blocking is not scoped correctly.");
        PulpItem.EnsureProduced(samplePulp);
        if (samplePulp.contentWindow != null || samplePulp.child != null
            || samplePulp.MayHaveValidInventorySlot(rejectedBottle))
            throw new InvalidOperationException("Produced pulp still has a container target.");
        if (input.MayHaveValidInventorySlot(DirectoryMaster.Item("wine_bottle", true))
            || bottles.MayHaveValidInventorySlot(samplePulp)
            || yeast.MayHaveValidInventorySlot(DirectoryMaster.Item("green_grape_fruit", true))
            || output.MayHaveValidInventorySlot(samplePulp)
            || fruitRegion.MayHaveValidInventorySlot(samplePulp)
            || pulpRegion.MayHaveValidInventorySlot(DirectoryMaster.Item("green_grape_fruit", true)))
            throw new InvalidOperationException("A machine area accepted an item of the wrong type.");
        if (!input.MayHaveValidInventorySlot(samplePulp)
            || !pulpRegion.MayHaveValidInventorySlot(samplePulp)
            || !bottles.MayHaveValidInventorySlot(DirectoryMaster.Item("wine_bottle", true))
            || !yeast.MayHaveValidInventorySlot(DirectoryMaster.Item("wine_yeast_red", true))
            || !fruitRegion.MayHaveValidInventorySlot(DirectoryMaster.Item("green_grape_fruit", true)))
            throw new InvalidOperationException("A machine area rejected a valid item type.");
        Core.Log?.Msg("BREWING_TEST PASS: pulp is 2x2 and machine areas reject wrong item types.");
        samplePulp.Destroy();
        var firstOldPulp = PulpItem.Create();
        var secondOldPulp = PulpItem.Create();
        foreach (var oldPulp in new[] { firstOldPulp, secondOldPulp })
        {
            oldPulp.shape = new GridShapeBuilder(1, 2).SetDataFill(1, 2, 1).Build();
            oldPulp.modifiedShape = oldPulp.shape.Clone();
            oldPulp.ValidateShapeState();
            if (!InventoryOps.Accept(pulpRegion, oldPulp))
                throw new InvalidOperationException("Could not set up legacy pulp layout.");
        }
        if (InventoryOps.Snapshot(pulpRegion).Count != 2)
            throw new InvalidOperationException("The legacy output did not contain two old pulps.");
        ConcentratorItem.EnsureLoaded(concentrator);
        if (!GeneralHelper.IsItemOwned(firstOldPulp) || !GeneralHelper.IsItemOwned(secondOldPulp)
            || firstOldPulp.CanActivateSlotItem() || secondOldPulp.CanActivateSlotItem())
            throw new InvalidOperationException("Saved pulp did not become owned and non-openable.");
        PulpItem.EnsureShape(firstOldPulp);
        if (firstOldPulp.shape.globalWidth != 1)
            throw new InvalidOperationException("Pulp expanded into an occupied legacy output area.");
        secondOldPulp.Destroy();
        PulpItem.EnsureShape(firstOldPulp);
        if (firstOldPulp.shape.globalWidth != 2 || firstOldPulp.shape.globalHeight != 2)
            throw new InvalidOperationException("Legacy pulp did not resize after space became available.");
        firstOldPulp.Destroy();
        Core.Log?.Msg("BREWING_TEST PASS: saved 1x2 pulp waits for free space and then becomes 2x2.");
        var legacyPulp = PulpItem.Create();
        var legacyGrid = PulpItem.Fruits(legacyPulp);
        var foreign = DirectoryMaster.Item("wine_bottle", true);
        BarrelLoadPatch.Loading = true;
        try
        {
            for (var i = 0; i < 4; i++)
                if (!InventoryOps.Accept(legacyGrid, DirectoryMaster.Item("green_grape_fruit", true)))
                    throw new InvalidOperationException("Could not prepare four saved pulp fruits.");
            if (!InventoryOps.Accept(legacyGrid, foreign))
                throw new InvalidOperationException("Could not prepare an old pulp with an extra item.");
        }
        finally { BarrelLoadPatch.Loading = false; }
        PulpItem.EnsureProduced(legacyPulp);
        if (legacyPulp.contentWindow != null || legacyPulp.child != null
            || PulpItem.FruitId(legacyPulp) != "green_grape_fruit"
            || foreign.parentInventory?.Pointer == legacyGrid.Pointer)
            throw new InvalidOperationException("Saved pulp did not recover its extra item and lose its container.");
        legacyPulp.Destroy();
        Core.Log?.Msg("BREWING_TEST PASS: saved pulp retained its recipe, returned the extra item and became a non-container.");
        var batteryItem = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuBatteryId, true);
        if (batteryItem == null || !InventoryOps.Accept(batterySlot, batteryItem))
            throw new InvalidOperationException("The battery slot rejected the mod battery.");
        var energyBefore = batteryItem.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
        if (energyBefore < 8) throw new InvalidOperationException("The test battery has insufficient energy.");
        for (var i = 0; i < 8; i++)
            if (!InventoryOps.Accept(fruitRegion, DirectoryMaster.Item("green_grape_fruit", true)))
                throw new InvalidOperationException("Ingredient region rejected grape " + i);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            if (!ConcentratorItem.Concentrate(concentrator))
                throw new InvalidOperationException("The concentrator did not produce pulp cycle " + cycle);
            var produced = InventoryOps.Snapshot(pulpRegion);
            if (produced.Count == 1)
            {
                var remainingFruits = InventoryOps.Snapshot(fruitRegion);
                var comparisonFruit = remainingFruits.Count > 0 ? remainingFruits[0] : null;
                Core.Log?.Msg("BREWING_TEST produced pulp: owned=" + GeneralHelper.IsItemOwned(produced[0])
                    + " activate=" + produced[0].CanActivateSlotItem()
                    + " mayActivate=" + produced[0].MayActivateSlotItem()
                    + " selectable=" + produced[0].CanSelectSlotItem()
                    + " maySelectFunc=" + (produced[0].maySelectSlotItemFunc != null)
                    + " canSelectFunc=" + (produced[0].canSelectSlotItemFunc != null)
                    + " disableActivate=" + produced[0].forceDisableActivate
                    + " parent=" + produced[0].parentInventory?.identifier
                    + " fruitSelect=" + comparisonFruit?.CanSelectSlotItem()
                    + " fruitMayFunc=" + (comparisonFruit?.maySelectSlotItemFunc != null)
                    + " fruitCanFunc=" + (comparisonFruit?.canSelectSlotItemFunc != null));
            }
            if (produced.Count != 1 || !GeneralHelper.IsItemOwned(produced[0])
                || produced[0].CanActivateSlotItem())
                throw new InvalidOperationException("Concentrated pulp is not owned and non-openable.");
            if (produced[0].spriteAtlasPath != BarrelItem.SpriteAtlas
                || produced[0].spritePath != "pulp_青提"
                || BarrelSprite.Get("pulp_青提") == null)
                throw new InvalidOperationException("Concentrated pulp has the wrong artwork.");
            if (produced.Count != 1 || PulpItem.FruitId(produced[0]) != "green_grape_fruit"
                || produced[0].contentWindow != null || produced[0].child != null)
                throw new InvalidOperationException("Concentrated pulp still has a container or lost its recipe.");
            if (cycle == 0)
            {
                var nodes = SaveManager.EncodeNodes(produced[0].Cast<GraphNodeStorage>());
                var decoded = SaveManager.DecodeNodes(nodes);
                Core.Log?.Msg("BREWING_TEST roundtrip nodes=" + nodes?.Count
                    + " fruit=" + PulpItem.FruitId(decoded)
                    + " window=" + (decoded?.contentWindow != null)
                    + " child=" + (decoded?.child != null));
                PulpItem.EnsureProduced(decoded);
                if (decoded == null || PulpItem.FruitId(decoded) != "green_grape_fruit"
                    || decoded.contentWindow != null || decoded.child != null)
                    throw new InvalidOperationException("Saved pulp restored as a container or lost its fruit recipe.");
                decoded.Destroy();
                Core.Log?.Msg("BREWING_TEST PASS: pulp recipe and non-container state survive item serialization.");
                var backpack = PlayerStore.instance.gridInv;
                if (!InventoryOps.Move(pulpRegion, backpack, produced[0])
                    || !InventoryOps.Move(backpack, input, produced[0]))
                    throw new InvalidOperationException("Could not carry concentrated pulp through the backpack.");
                Core.Log?.Msg("BREWING_TEST PASS: owned concentrated pulp entered and left the backpack.");
            }
            else if (!InventoryOps.Move(pulpRegion, input, produced[0]))
                throw new InvalidOperationException("Could not move concentrated pulp cycle " + cycle + " into the barrel.");
        }
        var energyAfter = batteryItem.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
        if (energyBefore - energyAfter != 16)
            throw new InvalidOperationException("Two concentration cycles did not consume sixteen energy: " + energyBefore + " -> " + energyAfter);
        var pulps = InventoryOps.Snapshot(input);
        if (pulps.Count != 2 || PulpItem.FruitId(pulps[0]) != "green_grape_fruit"
            || PulpItem.FruitId(pulps[1]) != "green_grape_fruit")
            throw new InvalidOperationException("Eight grapes did not become two valid concentrated pulps: " + pulps.Count);
        Core.Log?.Msg("BREWING_TEST PASS: eight grapes produced two pulps for sixteen energy; both fit the barrel.");

        for (var i = 0; i < 4; i++)
        {
            if (!InventoryOps.Accept(bottles, DirectoryMaster.Item("wine_bottle", true)))
                throw new InvalidOperationException("Bottle region rejected empty bottle " + i);
            if (!InventoryOps.Accept(yeast, DirectoryMaster.Item("wine_yeast_red", true)))
                throw new InvalidOperationException("Yeast region rejected ordinary yeast " + i);
        }
        var jug = DirectoryMaster.Item("water_jug", true);
        WaterHelper.FillWithBaseWater(jug);
        WaterHelper.TransferLiquid(jug, barrel);
        if (WaterHelper.GetCurrentCapacityML(barrel) < 2999)
            throw new InvalidOperationException("A native water jug could not fill the barrel.");
        var waterModifiers = barrel.getItemSpriteModifierForItemFunc?.Invoke(barrel);
        if (waterModifiers != null && waterModifiers.Count != 0)
            throw new InvalidOperationException("The filled barrel still has water sprite modifiers.");
        if (barrel.spriteAtlasPath != BarrelItem.SpriteAtlas || barrel.spritePath != "barrel_idle")
            throw new InvalidOperationException("The filled barrel did not retain its barrel sprite.");
        Core.Log?.Msg("BREWING_TEST PASS: filling the barrel retains barrel art without a water icon.");
        Core.Log?.Msg("BREWING_TEST preflight: barrelWater=" + WaterHelper.GetCurrentCapacityML(barrel)
            + ", bottles=" + bottles.childItems.Count + ", yeast=" + yeast.childItems.Count);
        BrewingBatch.Start(barrel);
        var fermenting = 0;
        foreach (var bottle in InventoryOps.Snapshot(bottles))
            if (WineHelper.IsFermentingOrFinished(bottle)) fermenting++;
        Core.Log?.Msg("BREWING_TEST after start: fermenting=" + fermenting
            + ", water=" + WaterHelper.GetCurrentCapacityML(barrel) + ", pulpLeft=" + input.childItems.Count);
        if (fermenting != 4) throw new InvalidOperationException("Four native bottles did not start fermentation.");
        Core.Log?.Msg("BREWING_TEST PASS: four bottles started native fermentation.");

        BrewingBatch.FinishAtNight(barrel);
        if (output.childItems.Count != 4 || bottles.childItems.Count != 0)
            throw new InvalidOperationException("Night completion did not move four wines to output.");
        Core.Log?.Msg("BREWING_TEST first wine: " + output.childItems[0].name
            + ", value=" + output.childItems[0].unitValue);
        Core.Log?.Msg("BREWING_TEST PASS: four finished wines moved to the output region.");
        var finishedWine = output.childItems[0];
        Core.Log?.Msg("BREWING_TEST Liunian wine: canAge=" + MachineWineRack.CanAgeWine(finishedWine)
            + " hasAged=" + AgableHelper.HasAged(finishedWine)
            + " storage=" + wineStorage.MayHaveValidInventorySlot(finishedWine)
            + " modules=" + moduleStorage.MayHaveValidInventorySlot(finishedWine));
        if (ageableSample != null)
        {
            var clone = SaveManager.DecodeNodes(SaveManager.EncodeNodes(ageableSample.Cast<GraphNodeStorage>()));
            if (clone == null || !MachineWineRack.CanAgeWine(clone)
                || !wineStorage.MayHaveValidInventorySlot(clone)
                || moduleStorage.MayHaveValidInventorySlot(clone)
                || !InventoryOps.Accept(wineStorage, clone))
                throw new InvalidOperationException("Liunian storage did not accept a saved ageable wine.");
            Core.Log?.Msg("BREWING_TEST Liunian after bottle: sprite=" + liunian.spriteAtlasPath + "/"
                + liunian.spritePath + " modifierCount="
                + (liunian.getItemSpriteModifierForItemFunc?.Invoke(liunian)?.Count ?? -1));
            if (liunian.spritePath != "liunian"
                || liunian.getItemSpriteModifierForItemFunc?.Invoke(liunian)?.Count != 0)
                throw new InvalidOperationException("Liunian art changed after inserting wine.");
            var battery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuBatteryId, true);
            var batteryPlaced = battery != null && InventoryOps.Accept(machineSlots[0], battery);
            var ageBefore = AgableHelper.GetAge(clone);
            var valueBeforeCycle = clone.GetCurrentValue();
            var energyBeforeAging = battery?.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
            if (batteryPlaced) liunian.onCycleEndSlotItemFunc.Invoke(liunian, null, null);
            var ageAfter = AgableHelper.GetAge(clone);
            var valueAfterCycle = clone.GetCurrentValue();
            var energyAfterAging = battery?.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
            Core.Log?.Msg("BREWING_TEST Liunian cycle: battery=" + batteryPlaced
                + " age=" + ageBefore + "->" + ageAfter
                + " energy=" + energyBeforeAging + "->" + energyAfterAging);
            if (!batteryPlaced || ageAfter <= ageBefore)
                throw new InvalidOperationException("Liunian did not age a native-compatible wine with a battery.");
            Core.Log?.Msg("BREWING_TEST PASS: Liunian accepts ageable wine and advances its age with native battery and cycle logic.");
            var chillable = MachineFridge.IsChillable(clone);
            var duplicateChill = LiunianAger.ChillStoredWine(liunian);
            Core.Log?.Msg("BREWING_TEST Liunian cycle chill: chillable=" + chillable
                + " duplicate=" + duplicateChill + " value=" + valueBeforeCycle + "->" + valueAfterCycle);
            if (chillable && (duplicateChill != 0 || valueAfterCycle <= valueBeforeCycle))
                throw new InvalidOperationException("Liunian cycle did not chill wine exactly once.");
            Core.Log?.Msg("BREWING_TEST PASS: Liunian chills wine during a completed native machine cycle.");
        }
        var probeType = Type.GetType("BrewingDebugToolkit.BrewingProbe, BrewingDebugToolkit", false);
        var probe = probeType?.GetMethod("BuildStatus", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var status = probe?.Invoke(null, null) as string;
        if (string.IsNullOrEmpty(status))
            throw new InvalidOperationException("Brewing Debug Toolkit did not return a status report.");
        Core.Log?.Msg("BREWING_TEST DEBUG_TOOLKIT: " + status);
        TestOvernight(incubatorProbe, liunian, ageableSample);
        liunian.contentWindow.Show();
        Core.Log?.Msg("BREWING_TEST PASS: Liunian inventory window opened for visual inspection.");
    }

    private static void TestOvernight(GameItem incubator, GameItem liunian, GameItem sampleWine)
    {
        if (incubator == null || liunian == null || sampleWine == null)
            throw new InvalidOperationException("Overnight fixtures are unavailable.");
        var player = PlayerStore.instance;
        var input = BrewingExpansion.IncubatorLayout.Input(incubator);
        var output = BrewingExpansion.IncubatorLayout.Output(incubator);
        var powerSlot = BrewingExpansion.IncubatorLayout.NativeLayout(incubator.contentWindow)
            ?.GetElement(0, 1)?.TryCast<GameSlotInventory>();
        var battery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuBatteryId, true);
        var seed = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.JasmineSeedId, true);
        var unchilledWine = SaveManager.DecodeNodes(SaveManager.EncodeNodes(sampleWine.Cast<GraphNodeStorage>()));
        var wineStorage = LiunianAger.Storage(liunian);
        if (input == null || output == null || powerSlot == null || battery == null || seed == null
            || unchilledWine == null || !InventoryOps.Accept(powerSlot, battery)
            || !InventoryOps.Accept(input, seed) || !InventoryOps.Accept(wineStorage, unchilledWine)
            || !InventoryOps.Accept(player.gridInv, incubator) || !InventoryOps.Accept(player.gridInv, liunian))
            throw new InvalidOperationException("Could not place the powered incubator and Liunian fixtures in the store.");
        if (!LiunianAger.HasPower(liunian))
            throw new InvalidOperationException("Liunian overnight fixture was not powered.");
        var chilledId = MachineFridge.CreateChilledFeature().identifier;
        bool HasChilled()
        {
            var features = unchilledWine.itemFeatures;
            for (var i = 0; features != null && i < features.Count; i++)
                if (features[i]?.identifier == chilledId && !features[i].isDisabled) return true;
            return false;
        }
        if (HasChilled()) throw new InvalidOperationException("Overnight wine was already chilled.");
        var listed = OwnedInventorySnapshot.Items();
        var sawIncubator = false;
        var sawLiunian = false;
        for (var i = 0; listed != null && i < listed.Count; i++)
        {
            sawIncubator |= listed[i]?.Pointer == incubator.Pointer;
            sawLiunian |= listed[i]?.Pointer == liunian.Pointer;
        }
        Core.Log?.Msg("BREWING_TEST overnight fixture visibility: found=" + listed?.Count
            + " incubator=" + sawIncubator + " liunian=" + sawLiunian
            + " owned=" + incubator.IsTag(BrewingExpansion.BrewingExpansionMod.OwnedTag)
            + "/" + liunian.IsTag(BrewingExpansion.BrewingExpansionMod.OwnedTag)
            + " incubatorParent=" + incubator.parentInventory?.identifier
            + " liunianParent=" + liunian.parentInventory?.identifier
            + " battery=" + BrewingExpansion.PatchSeedToFruit.Battery(incubator)?.identifier);
        if (!sawIncubator || !sawLiunian)
            throw new InvalidOperationException("Night fixtures in the backpack are missing from the owned inventory snapshot.");
        var rechargeIds = new[]
        {
            BrewingExpansion.BrewingExpansionMod.XuxuBatteryId,
            BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId,
            BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId
        };
        var recharging = new GameItem[rechargeIds.Length];
        var rechargeBefore = new int[rechargeIds.Length];
        for (var i = 0; i < rechargeIds.Length; i++)
        {
            recharging[i] = DirectoryMaster.Item(rechargeIds[i], true);
            if (recharging[i] == null || !PowerHelper.DrawPowerSource(recharging[i], 20)
                || !InventoryOps.Accept(player.gridInv, recharging[i]))
                throw new InvalidOperationException("Could not prepare an owned battery recharge fixture.");
            rechargeBefore[i] = recharging[i].GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
        }
        var beforeEnergy = battery.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
        var beforeValue = unchilledWine.GetCurrentValue();
        player.EndNight();
        var afterEnergy = battery.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
        var afterValue = unchilledWine.GetCurrentValue();
        var expectedRecharge = new[] { 6, 8, 12 };
        for (var i = 0; i < recharging.Length; i++)
        {
            var afterRecharge = recharging[i].GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
            Core.Log?.Msg("BREWING_TEST overnight recharge " + rechargeIds[i] + ": "
                + rechargeBefore[i] + "->" + afterRecharge);
            if (afterRecharge - rechargeBefore[i] != expectedRecharge[i])
                throw new InvalidOperationException("Xuxu battery did not regain its specified nightly energy.");
        }
        Core.Log?.Msg("BREWING_TEST overnight: incubatorEnergy=" + beforeEnergy + "->" + afterEnergy
            + " input=" + input.childItems.Count + " output=" + output.childItems.Count
            + " chilled=" + HasChilled() + " wineValue=" + beforeValue + "->" + afterValue);
        // This battery also gains 6 kJ overnight, so the net drop is 12 - 6.
        if (beforeEnergy - afterEnergy != 6 || input.childItems.Count != 0
            || output.childItems.Count == 0 || !HasChilled() || afterValue <= beforeValue)
            throw new InvalidOperationException("The actual EndNight did not spend incubator energy, hatch a seed, and chill Liunian wine.");
        Core.Log?.Msg("BREWING_TEST PASS: native EndNight spends incubator energy, hatches a seed in output, and chills Liunian wine.");
        var ageBeforeSecondNight = AgableHelper.GetAge(unchilledWine);
        player.EndNight();
        var ageAfterSecondNight = AgableHelper.GetAge(unchilledWine);
        if (ageAfterSecondNight <= ageBeforeSecondNight || !HasChilled())
            throw new InvalidOperationException("Liunian lost its chilled wine feature on the second night.");
        Core.Log?.Msg("BREWING_TEST PASS: Liunian ages and keeps wine chilled across consecutive nights.");
    }

    private static void TestCompositeWithInfiniteYeast()
    {
        var barrel = DirectoryMaster.Item(BarrelItem.Id, true);
        if (barrel == null) throw new InvalidOperationException("The barrel factory failed for a second instance.");
        var input = BarrelItem.Input(barrel);
        var bottles = BarrelItem.Bottles(barrel);
        var yeastSlot = BarrelItem.Yeast(barrel);
        var output = BarrelItem.Output(barrel);
        for (var i = 0; i < 4; i++)
        {
            if (!InventoryOps.Accept(input, DirectoryMaster.Item("lemon_fruit", true))
                || !InventoryOps.Accept(input, DirectoryMaster.Item("sugarcane_fruit", true)))
                throw new InvalidOperationException("Composite ingredients could not be placed.");
            if (!InventoryOps.Accept(bottles, DirectoryMaster.Item("wine_bottle", true)))
                throw new InvalidOperationException("Composite bottles could not be placed.");
        }
        PulpItem.PressAvailable(barrel);
        if (InventoryOps.Snapshot(input).Count != 2)
            throw new InvalidOperationException("Composite pulps were not produced.");
        var infinite = DirectoryMaster.Item("be_golden_yeast_reactor", true);
        if (infinite == null || !InventoryOps.Accept(yeastSlot, infinite))
            throw new InvalidOperationException("Infinite yeast could not be placed.");
        WaterHelper.FillWithBaseWater(barrel);
        BrewingBatch.Start(barrel);
        var running = 0;
        foreach (var bottle in InventoryOps.Snapshot(bottles))
            if (WineHelper.IsFermentingOrFinished(bottle)) running++;
        if (running != 4) throw new InvalidOperationException("Infinite yeast started only " + running + "/4 composite bottles.");
        var parent = infinite.parentInventory;
        if (parent == null || (parent.Pointer != PlayerStore.instance.gridInv.Pointer && parent.Pointer != yeastSlot.Pointer))
            throw new InvalidOperationException("Infinite yeast was not returned to the backpack or kept in its slot.");
        Core.Log?.Msg("BREWING_TEST PASS: one infinite yeast started four composite bottles; returned="
            + (parent.Pointer == PlayerStore.instance.gridInv.Pointer) + ", parent=" + parent.identifier);
        BrewingBatch.FinishAtNight(barrel);
        if (output.childItems.Count != 4)
            throw new InvalidOperationException("Composite wine output count was " + output.childItems.Count);
        Core.Log?.Msg("BREWING_TEST PASS: composite wine finished; name=" + output.childItems[0].name);
    }
}
#endif
