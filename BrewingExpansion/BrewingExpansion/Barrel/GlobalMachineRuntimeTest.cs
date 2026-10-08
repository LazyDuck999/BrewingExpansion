#if GLOBAL_MACHINE_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

// Local test build only. Creates a temporary run, blocks its disk saves, and
// verifies detached native fixtures plus actual turbo and EndNight entry points.
internal static class GlobalMachineRuntimeTest
{
    // This assembly is deliberately a local test build. A normal Steam launch
    // runs the harness too; the production build excludes this entire file.
    internal static readonly bool Enabled = true;
    private static bool _scheduled;
    private static int _checks;
    private static GameItem _nightNative, _nightLiunian;
    internal static void Schedule()
    {
        if (!Enabled || _scheduled) return;
        _scheduled = true;
        MelonCoroutines.Start(Verify());
    }
    private static IEnumerator Verify()
    {
        for (var n = 0; n < 200 && MainMenuUIController.Instance == null; n++)
            yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(1f);
        try
        {
            Check(MainMenuUIController.Instance != null && NewGameData.Instance != null, "main menu ready");
            var data = NewGameData.Instance;
            data.startType = NewGameData.StartType.MOONSHINER;
            data.skipIntro = true; data.disableTutorialHint = true; data.startingPerks.Clear();
            MainMenuUIController.Instance.StartNewGameClick();
        }
        catch (Exception ex) { Fail(ex); yield break; }
        for (var n = 0; n < 200 && (PlayerStore.instance == null || EmporiumEntry.Instance == null); n++)
            yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(2f);
        try
        {
            Check(PlayerStore.instance != null && EmporiumEntry.Instance != null, "temporary run ready");
            // InitialSave normally initializes this feature table. Save calls
            // are blocked in this test build, but EncodeNode still needs it.
            PlayerStore.instance.savedItemFeatureList ??= new Il2CppSystem.Collections.Generic.List<ItemFeature>();
            Run();
        }
        catch (Exception ex) { Fail(ex); yield break; }
        for (var night = 0; night < 2; night++)
        {
            yield return new WaitForSeconds(5.2f);
            try { NightCheck(night); }
            catch (Exception ex) { Fail(ex); yield break; }
        }
        _nightNative.Destroy(); _nightLiunian.Destroy();
        MelonLogger.Msg("BE_GLOBAL_TEST ALL PASS checks=" + _checks);
        Application.Quit(0);
    }
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        _checks++; MelonLogger.Msg("BE_GLOBAL_TEST PASS " + name);
    }
    private static void Fail(Exception ex)
    {
        MelonLogger.Error("BE_GLOBAL_TEST FAIL " + ex);
        Application.Quit(2);
    }
    private static GameItem Wine()
    {
        var wine = DirectoryMaster.Item("wine_bottle", true);
        wine.ModifyTag("WINE_CONTAINER_STATE", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 2), true);
        AgableHelper.InitAgable(wine, 3); wine.SetValue(100L);
        Check(MachineWineRack.CanAgeWine(wine), "finished wine fixture");
        return wine;
    }
    private static GameItem Rack(bool liunian)
    {
        var rack = DirectoryMaster.Item(liunian ? LiunianAger.Id : "wine_rack", true);
        Check(!string.IsNullOrEmpty(rack?.identifier), "serializable rack fixture");
        var battery = DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId, true);
        battery.ModifyTag("power_source_item_energy", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = 10000; }), true);
        Check(InventoryOps.Accept(LiunianAger.Battery(rack), battery), "battery inserted " + rack.identifier);
        var grid = LiunianAger.Modules(rack);
        grid.SetShape(6, 20);
        for (var i = 0; i < 12; i++)
        {
            var module = DirectoryMaster.Item("system_module_quality", true);
            module.ModifyTag("BONUS_PERCENTAGE_QUALITY_INT", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = 3000; }), true);
            if (i == 0) ModuleEffectHelper.AddEffectToModule(module, "MODULE_EFFECT_DEGRADING");
            Check(InventoryOps.Accept(grid, module), "quality module inserted");
        }
        ModuleEffectHelper.ComputeAllModuleTempStat(grid.childItems);
        ModuleHelper.ComputeModuleEffect(grid, null, rack);
        ModuleHelper.ApplyBasicModuleEffect(grid, null, rack);
        for (var i = 0; i < 8; i++) Check(InventoryOps.Accept(LiunianAger.Storage(rack), Wine()), "wine inserted");
        return rack;
    }
    private static string Snapshot(GameItem item)
    {
        var fields = new SortedDictionary<string, string>();
        foreach (var pair in item.state.dict)
            if (pair.Key.Contains("MODULE") || pair.Key.Contains("STAT") || pair.Key.Contains("BONUS"))
                fields[pair.Key] = pair.Value.valueEnabled + ":" + pair.Value.valueInt + ":" + pair.Value.valueFloat;
        return string.Join("|", fields.Select(p => p.Key + "=" + p.Value)) + ";value=" + item.unitValue;
    }
    private static void Turbo(GameItem rack)
    {
        var turbo = new ToolDirectory().CreateTurboBooster();
        Check(turbo.onThisTargetItemFunc != null, "native turbo callback");
        turbo.onThisTargetItemFunc.Invoke(turbo, rack);
        turbo.Destroy();
    }
    private static void CycleCheck(GameItem rack, bool turbo)
    {
        rack.ModifyTag("TOTAL_PERCENTAGE_QUALITY_BONUS_INT", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = 30000; }), true);
        var wines = InventoryOps.Snapshot(LiunianAger.Storage(rack));
        var before = wines.Select(w => (Wine: w, Age: AgableHelper.GetAge(w), Value: w.unitValue)).ToArray();
        var days = MachineWineRack.GetAgingDays(rack);
        var battery = LiunianAger.Battery(rack).currentItem;
        var energy = battery.GetTagReadonly("power_source_item_energy").valueInt;
        var watch = Stopwatch.StartNew();
        if (turbo) Turbo(rack); else rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
        watch.Stop();
        foreach (var sample in before)
            Check(AgableHelper.GetAge(sample.Wine) == sample.Age + days
                && sample.Wine.unitValue == sample.Value + 3L * days, "exact age/value " + rack.identifier);
        Check(battery.GetTagReadonly("power_source_item_energy").valueInt < energy, "cycle power " + rack.identifier);
        MelonLogger.Msg("BE_GLOBAL_TEST TIMING " + rack.identifier + " wines=" + wines.Count + " days=" + days
            + " elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3"));
    }
    private static bool Cold(GameItem wine)
    {
        var id = MachineFridge.CreateChilledFeature().identifier;
        for (var i = 0; wine.itemFeatures != null && i < wine.itemFeatures.Count; i++)
            if (wine.itemFeatures[i].identifier == id && !wine.itemFeatures[i].isDisabled) return true;
        return false;
    }
    private static void Modules(GameItem machine, GameGridInventory grid)
    {
        // Clone with the game's own serializer, run the same actions with our
        // batch disabled, then compare all saved base/module/machine stats.
        var control = SaveManager.DecodeNodes(SaveManager.EncodeNodes(machine.Cast<GraphNodeStorage>()));
        var controlGrid = control.identifier == LiunianAger.Id ? LiunianAger.Modules(control) : MachineHelper.GetModuleInv(control);
        var oldEnabled = LiunianModuleBatch.Enabled;
        var firstModule = grid.childItems[0];
        var qualityBefore = firstModule.GetTagReadonly("BONUS_PERCENTAGE_QUALITY_INT").valueInt;
        try
        {
            LiunianModuleBatch.Enabled = false;
            MachineHelper.OnMachineActioned(control, controlGrid);
            LiunianModuleBatch.Enabled = true;
            MachineHelper.OnMachineActioned(machine, grid);
            Check(Snapshot(machine) == Snapshot(control), "module machine stats match " + machine.identifier);
            Check(InventoryOps.Snapshot(grid).Select(Snapshot).SequenceEqual(InventoryOps.Snapshot(controlGrid).Select(Snapshot)),
                "module loss/stats match " + machine.identifier);
            if (firstModule.IsTag("MODULE_EFFECT1_TAG")
                && firstModule.GetTagReadonly("MODULE_EFFECT1_TAG").valueString == "MODULE_EFFECT_DEGRADING")
                Check(firstModule.GetTagReadonly("BONUS_PERCENTAGE_QUALITY_INT").valueInt < qualityBefore, "nonzero module degradation " + machine.identifier);
        }
        finally { LiunianModuleBatch.Enabled = oldEnabled; control.Destroy(); }
    }
    private static void Run()
    {
        BrewingExpansion.BrewingExpansionMod.EnsureSpriteAssetsReady();
        MelonLogger.Msg("BE_GLOBAL_TEST agingOverlap=" + (WineRackAgingOptimization.Overlap() ?? "none"));
        var batchBefore = WineRackAgingOptimization.BatchedCycles;
        var native = Rack(false); var liunian = Rack(true);
        foreach (var rack in new[] { native, liunian })
        {
            CycleCheck(rack, true); CycleCheck(rack, true);
            var restored = SaveManager.DecodeNodes(SaveManager.EncodeNodes(rack.Cast<GraphNodeStorage>()));
            CycleCheck(restored, false); restored.Destroy();
            Modules(rack, LiunianAger.Modules(rack));
            var battery = LiunianAger.Battery(rack).currentItem;
            var wine = LiunianAger.Storage(rack).childItems[0]; var age = AgableHelper.GetAge(wine);
            battery.ModifyTag("power_source_item_energy", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 0), true);
            rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
            Check(AgableHelper.GetAge(wine) == age, "unpowered rack unchanged " + rack.identifier);
            battery.ModifyTag("power_source_item_energy", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 10000), true);
        }
        if (WineRackAgingOptimization.Overlap() == null)
            Check(WineRackAgingOptimization.BatchedCycles >= batchBefore + 3, "native racks actually used batch callback");
        foreach (var machine in new[] { DirectoryMaster.Item("fridge", true), DirectoryMaster.Item("furnace", true) })
        {
            var grid = MachineHelper.GetModuleInv(machine);
            Check(grid != null, "native module grid " + machine.identifier);
            grid.SetShape(6, 20);
            for (var i = 0; i < 6; i++)
                Check(InventoryOps.Accept(grid, DirectoryMaster.Item("system_module_quality", true)), "other machine quality fixture");
            Modules(machine, grid);
            machine.Destroy();
        }
        Check(InventoryOps.Accept(EmporiumEntry.Instance.invElement, native), "native rack placed for overnight");
        Check(InventoryOps.Accept(EmporiumEntry.Instance.invElement, liunian), "liunian placed for overnight");
        Check(PlayerStore.instance.FindAllItem().Contains(native)
            && PlayerStore.instance.FindAllItem().Contains(liunian), "night traversal includes both racks");
        _nightNative = native; _nightLiunian = liunian;
        Stress();
    }
    private static void NightCheck(int night)
    {
        var calls = new int[2];
        var exact = new bool[2];
        var racks = new[] { _nightNative, _nightLiunian };
        var originals = racks.Select(r => r.onCycleEndSlotItemFunc).ToArray();
        try
        {
            for (var i = 0; i < racks.Length; i++)
            {
                var index = i; var rack = racks[i]; var original = originals[i];
                rack.onCycleEndSlotItemFunc = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<
                    Il2CppSystem.Action<GameItem, GameInventory, SlotMarker>>(
                    (Action<GameItem, GameInventory, SlotMarker>)((item, parent, slot) =>
                    {
                        // Native nightly preparation updates module stats
                        // before entering a machine's cycle. Observe here.
                        var wine = LiunianAger.Storage(rack).childItems[0];
                        var age = AgableHelper.GetAge(wine); var value = wine.unitValue;
                        var days = MachineWineRack.GetAgingDays(rack);
                        original.Invoke(item, parent, slot); calls[index]++;
                        MelonLogger.Msg("BE_GLOBAL_TEST NIGHT " + rack.identifier + " expected=" + days
                            + " actual=" + (AgableHelper.GetAge(wine) - age));
                        exact[index] = AgableHelper.GetAge(wine) == age + days && wine.unitValue == value + 3L * days;
                    }));
            }
            var dayBefore = BrewingExpansion.BrewingExpansionMod.TotalGameDays;
            PlayerStore.instance.EndNight();
            Check(BrewingExpansion.BrewingExpansionMod.TotalGameDays == dayBefore + 1, "night advances mod day counter " + night);
            Check(calls[0] == 1 && calls[1] == 1, "native EndNight runs each rack exactly once " + night);
            Check(exact[0] && exact[1], "native EndNight exact ages/values both racks " + night);
            Check(Cold(LiunianAger.Storage(_nightLiunian).childItems[0]), "liunian chilled after full night " + night);
            Check(!Cold(LiunianAger.Storage(_nightNative).childItems[0]), "ordinary rack does not acquire Liunian chilling " + night);
        }
        finally { for (var i = 0; i < racks.Length; i++) racks[i].onCycleEndSlotItemFunc = originals[i]; }
    }
    private static void Stress()
    {
        // Both modes verify Liunian's independent 64-bit path. Solo mode also
        // verifies that the newly generalized native-rack callback uses it.
        var kinds = WineRackAgingOptimization.Overlap() == null ? new[] { false, true } : new[] { true };
        foreach (var liunian in kinds)
        {
            var rack = Rack(liunian); var storage = LiunianAger.Storage(rack);
            storage.SetShape(20, 20);
            for (var i = storage.childItems.Count; i < 100; i++) Check(InventoryOps.Accept(storage, Wine()), "stress wine inserted");
            rack.ModifyTag("TOTAL_PERCENTAGE_QUALITY_BONUS_INT", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = 799999875; }), true);
            var days = MachineWineRack.GetAgingDays(rack);
            Check(days == 6400000, "stress quality gives 6.4 million days");
            foreach (var wine in InventoryOps.Snapshot(storage))
                wine.ModifyTag("AGE_INT", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = int.MaxValue - 2; }), true);
            var watch = Stopwatch.StartNew(); Turbo(rack); watch.Stop();
            foreach (var wine in InventoryOps.Snapshot(storage))
            {
                Check(ExtendedWineAge.TryRead(wine, out var age) && age == (long)int.MaxValue - 2 + days
                    && wine.unitValue == 100L + 3L * days, "stress exact 64-bit age/value");
                var tooltip = new RichTextBuilder(); AgableHelper.AddAgeTooltipLine(tooltip, wine);
                Check(System.Text.RegularExpressions.Regex.Replace(tooltip.Build().nakedText, "\\D", "")
                    .Contains(age.ToString(System.Globalization.CultureInfo.InvariantCulture)), "stress full age tooltip");
            }
            var restored = SaveManager.DecodeNodes(SaveManager.EncodeNodes(rack.Cast<GraphNodeStorage>()));
            Check(ExtendedWineAge.TryRead(LiunianAger.Storage(restored).childItems[0], out var restoredAge)
                && restoredAge == (long)int.MaxValue - 2 + days, "stress 64-bit age survives native serialization");
            MelonLogger.Msg("BE_GLOBAL_TEST STRESS " + rack.identifier + " wines=100 days=" + days
                + " elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("F3"));
            restored.Destroy(); rack.Destroy();
        }
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
internal static class GlobalTestNoSave { private static bool Prefix() => !GlobalMachineRuntimeTest.Enabled; }
[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.InitialSave))]
internal static class GlobalTestNoInitialSave { private static bool Prefix() => !GlobalMachineRuntimeTest.Enabled; }
[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile")]
internal static class GlobalTestNoSlotWrite { private static bool Prefix() => !GlobalMachineRuntimeTest.Enabled; }
#endif
