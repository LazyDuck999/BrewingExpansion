#if LIUNIAN_AGING_TEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

// Only compiled into the local verification DLL, never the distributable build.
internal static class LiunianAgingRuntimeTest
{
    private static readonly bool TestAge64 = Array.IndexOf(Environment.GetCommandLineArgs(), "--be-test-age64") >= 0;
    internal static readonly bool Enabled = TestAge64 || Array.IndexOf(Environment.GetCommandLineArgs(), "--be-test-bulk-aging") >= 0;
    private static bool _scheduled;
    internal static void Schedule()
    {
        if (!Enabled || _scheduled) return;
        _scheduled = true;
        MelonCoroutines.Start(TestAge64 ? VerifyAge64() : Verify());
    }
    private static IEnumerator VerifyAge64()
    {
        // These checks need no new game or player save. All fixtures are detached.
        for (var n = 0; n < 150 && MainMenuUIController.Instance == null; n++)
            yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(1f);
        try { RunAge64(); MelonLogger.Msg("BE_AGE64_TEST ALL PASS"); Application.Quit(0); }
        catch (Exception ex) { Fail(ex); }
    }
    private static void RunAge64()
    {
        var fresh = Wine();
        Check(ExtendedWineAge.TryAdvance(fresh, 241, out _)
            && fresh.IsTag("AGE_INT") && AgableHelper.HasAged(fresh) && AgableHelper.GetAge(fresh) == 241,
            "first turbo-style batch enables age without an overnight update");
        var freshTooltip = new RichTextBuilder();
        WineHelper.CreateTooltip(freshTooltip, fresh);
        Check(freshTooltip.Build().nakedText.Contains("241"), "first batch is visible in native wine tooltip");
        fresh.Destroy();
        var wine = Wine(7);
        wine.ModifyTag("AGE_INT", (Il2CppSystem.Action<TagState>)(s => s.valueInt = int.MaxValue - 2), true);
        Check(ExtendedWineAge.TryAdvance(wine, 5, out var crossing)
            && crossing.Age == (long)int.MaxValue + 3 && wine.unitValue == 135,
            "cross Int32 limit with exact age/value");
        Check(AgableHelper.GetAge(wine) == int.MaxValue
            && wine.IsTag(ExtendedWineAge.ExtendedTag)
            && ExtendedWineAge.TryRead(wine, out var fullAge) && fullAge == crossing.Age,
            "native compatibility projection and extended age");
        const long preciseAge = 9_007_199_254_740_997L;
        wine.ModifyTag(ExtendedWineAge.ExtendedTag, (Il2CppSystem.Action<TagState>)(s => s.valueLong = preciseAge), true);
        Check(wine.GetTagReadonly(ExtendedWineAge.ExtendedTag).Clone().valueLong == preciseAge,
            "native tag clone keeps all 64 bits");
        var encoded = SaveManager.EncodeNodes(wine.Cast<GraphNodeStorage>());
        var restored = SaveManager.DecodeNodes(encoded);
        Check(restored != null && ExtendedWineAge.TryRead(restored, out var restoredAge)
            && restoredAge == preciseAge && restored.unitValue == 135,
            "native item encode/decode keeps exact age above double precision");
        var tooltip = new RichTextBuilder();
        AgableHelper.AddAgeTooltipLine(tooltip, restored);
        Check(System.Text.RegularExpressions.Regex.Replace(tooltip.Build().nakedText, "\\D", "")
            .Contains(preciseAge.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "native tooltip displays full 64-bit age");
        WineHelper.OnAgeWine(restored);
        Check(ExtendedWineAge.TryRead(restored, out restoredAge) && restoredAge == preciseAge + 1
            && restored.unitValue == 142 && AgableHelper.GetAge(restored) == int.MaxValue,
            "native daily aging continues beyond Int32 without double value gain");
        AgableHelper.ResetAge(restored);
        Check(!restored.IsTag(ExtendedWineAge.ExtendedTag)
            && ExtendedWineAge.TryRead(restored, out restoredAge) && restoredAge == 0,
            "native reset clears the extension");
        WineHelper.OnAgeWine(restored);
        Check(AgableHelper.GetAge(restored) == 1 && !restored.IsTag(ExtendedWineAge.ExtendedTag),
            "reset wine resumes ordinary native aging");
        var legacy = Wine();
        legacy.ModifyTag("AGE_INT", (Il2CppSystem.Action<TagState>)(s => s.valueInt = int.MaxValue - 1), true);
        WineHelper.OnAgeWine(legacy);
        WineHelper.OnAgeWine(legacy);
        Check(ExtendedWineAge.TryRead(legacy, out var legacyAge) && legacyAge == (long)int.MaxValue + 1
            && legacy.unitValue == 106, "native rack boundary acquires extension on demand");
        var legacyDisabled = Wine();
        legacyDisabled.ModifyTag("AGE_INT", (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = int.MaxValue; }), true);
        legacyDisabled.ModifyTag(ExtendedWineAge.ExtendedTag,
            (Il2CppSystem.Action<TagState>)(s => s.valueLong = (long)int.MaxValue + 10000));
        Check(!legacyDisabled.IsTag(ExtendedWineAge.ExtendedTag)
            && ExtendedWineAge.TryRead(legacyDisabled, out var recoveredAge) && recoveredAge == (long)int.MaxValue + 10000,
            "legacy disabled extension can be read");
        Check(ExtendedWineAge.TryAdvance(legacyDisabled, 5, out var recoveredPlan)
            && recoveredPlan.Age == (long)int.MaxValue + 10005 && legacyDisabled.IsTag(ExtendedWineAge.ExtendedTag),
            "legacy disabled extension is enabled on next cycle");
        AgableHelper.ResetAge(legacyDisabled);
        Check(ExtendedWineAge.TryRead(legacyDisabled, out var resetAge) && resetAge == 0,
            "reset does not resurrect recovered extension");
        legacyDisabled.Destroy();
        legacy.Destroy();
        restored.Destroy();
        wine.Destroy();
    }
    private static IEnumerator Verify()
    {
        for (var n = 0; n < 150 && MainMenuUIController.Instance == null; n++)
            yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(1f);
        try
        {
            var game = NewGameData.Instance;
            if (game == null || MainMenuUIController.Instance == null) throw new Exception("Menu not ready");
            game.startType = NewGameData.StartType.MOONSHINER;
            game.skipIntro = true;
            game.disableTutorialHint = true;
            game.startingPerks.Clear();
            MainMenuUIController.Instance.StartNewGameClick();
        }
        catch (Exception ex) { Fail(ex); yield break; }
        for (var n = 0; n < 150 && (PlayerStore.instance == null || EmporiumEntry.Instance == null); n++)
            yield return new WaitForSeconds(0.2f);
        yield return new WaitForSeconds(2f);
        try { Run(); MelonLogger.Msg("BE_AGING_TEST ALL PASS"); Application.Quit(0); }
        catch (Exception ex) { Fail(ex); }
    }
    private static void Fail(Exception ex) { MelonLogger.Error("BE_AGING_TEST FAIL: " + ex); Application.Quit(2); }
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        MelonLogger.Msg("BE_AGING_TEST PASS: " + label);
    }
    private const string ProbeOwner = "be.test.foreign-daily-aging";
    private static int _foreignDailyCalls;
    private static void CountForeignDailyCall() => _foreignDailyCalls++;
    private static GameItem Wine(int rate = 3)
    {
        var wine = DirectoryMaster.Item("wine_bottle", true);
        wine.ModifyTag("WINE_CONTAINER_STATE", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 2), true);
        AgableHelper.InitAgable(wine, rate);
        wine.SetValue(100L);
        Check(MachineWineRack.CanAgeWine(wine), "native finished wine fixture");
        return wine;
    }
    private static void SetQuality(GameItem machine, int quality)
    {
        machine.ModifyTag("TOTAL_PERCENTAGE_QUALITY_BONUS_INT", (Il2CppSystem.Action<TagState>)(s => s.valueInt = quality), true);
        Check(MachineryHelper.GetCurrentQualityBonus(machine) == quality, "quality fixture " + quality);
    }
    private static GameItem Powered(GameItem machine)
    {
        Check(InventoryOps.Accept(LiunianAger.Battery(machine),
            DirectoryMaster.Item(BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId, true)), "battery inserted");
        return machine;
    }
    private static void Run()
    {
        BrewingExpansion.BrewingExpansionMod.EnsureSpriteAssetsReady();
        var rack = Powered(DirectoryMaster.Item(LiunianAger.Id, true));
        var wine = Wine(7);
        var storage = LiunianAger.Storage(rack);
        var battery = LiunianAger.Battery(rack).currentItem;
        int Energy() => battery.GetTagReadonly("power_source_item_energy").valueInt;
        var beforeEmpty = Energy();
        rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
        Check(Energy() == beforeEmpty, "empty cabinet spends no power");
        Check(InventoryOps.Accept(storage, wine), "wine inserted");
        SetQuality(rack, 30000);
        var age = AgableHelper.GetAge(wine);
        var value = wine.unitValue;
        var days = MachineWineRack.GetAgingDays(rack);
        var energy = Energy();
        var probe = new HarmonyLib.Harmony(ProbeOwner);
        var wineAgeMethod = AccessTools.Method(typeof(WineHelper), nameof(WineHelper.OnAgeWine));
        var ageMethod = AccessTools.Method(typeof(AgableHelper), nameof(AgableHelper.OnAge));
        var observer = new HarmonyMethod(AccessTools.Method(typeof(LiunianAgingRuntimeTest), nameof(CountForeignDailyCall)));
        _foreignDailyCalls = 0;
        try
        {
            probe.Patch(wineAgeMethod, prefix: observer);
            probe.Patch(ageMethod, prefix: observer);
            rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
            Check(_foreignDailyCalls == 0, "foreign daily aging patches do not replace Liunian batch settlement");
        }
        finally
        {
            probe.Unpatch(wineAgeMethod, HarmonyPatchType.Prefix, ProbeOwner);
            probe.Unpatch(ageMethod, HarmonyPatchType.Prefix, ProbeOwner);
        }
        Check(days == 241 && AgableHelper.GetAge(wine) == age + days
            && wine.unitValue == value + 7L * days, "exact native days/value including base day");
        var vanilla = Powered(MachineWineRack.AgeWell());
        var controlWine = Wine(7);
        Check(InventoryOps.Accept(LiunianAger.Storage(vanilla), controlWine), "native control wine inserted");
        SetQuality(vanilla, 30000);
        vanilla.onCycleEndSlotItemFunc.Invoke(vanilla, null, null);
        Check(controlWine.unitValue == wine.unitValue && AgableHelper.GetAge(controlWine) == AgableHelper.GetAge(wine),
            "batch matches unmodified native rack");
        Check(vanilla.contentWindow != null, "native rack factory remains available");
        vanilla.Destroy();
        Check(Energy() < energy, "powered cycle spends energy");
        var chilledId = MachineFridge.CreateChilledFeature().identifier;
        bool Chilled(GameItem bottle)
        {
            for (var i = 0; i < bottle.itemFeatures.Count; i++)
                if (bottle.itemFeatures[i].identifier == chilledId && !bottle.itemFeatures[i].isDisabled) return true;
            return false;
        }
        Check(Chilled(wine), "cycle completion chills wine");
        battery.ModifyTag("power_source_item_energy", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 0), true);
        age = AgableHelper.GetAge(wine);
        rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
        Check(AgableHelper.GetAge(wine) == age && Energy() == 0, "no power means no aging");
        battery.ModifyTag("power_source_item_energy", (Il2CppSystem.Action<TagState>)(s => s.valueInt = 2000), true);
        var saved = SaveManager.DecodeNodes(SaveManager.EncodeNodes(rack.Cast<GraphNodeStorage>()));
        LiunianAger.EnsureLoaded(saved);
        var restoredWine = LiunianAger.Storage(saved).childItems[0];
        SetQuality(saved, 30000);
        age = AgableHelper.GetAge(restoredWine);
        saved.onCycleEndSlotItemFunc.Invoke(saved, null, null);
        Check(AgableHelper.GetAge(restoredWine) == age + 241, "bulk callback survives serialization");
        saved.Destroy();

        for (var i = storage.childItems.Count; i < 100; i++) Check(InventoryOps.Accept(storage, Wine()), "full-cabinet wine inserted");
        SetQuality(rack, 799999875);
        var snapshots = new List<(GameItem Wine, int Age, long Value, int Rate)>();
        foreach (var item in InventoryOps.Snapshot(storage))
            snapshots.Add((item, AgableHelper.GetAge(item), item.unitValue,
                item.GetTagReadonly("AGE_VALUE_MOD_PER_DAY_INT")?.valueInt ?? 3));
        days = MachineWineRack.GetAgingDays(rack);
        var watch = Stopwatch.StartNew();
        rack.onCycleEndSlotItemFunc.Invoke(rack, null, null);
        watch.Stop();
        foreach (var sample in snapshots)
            Check(AgableHelper.GetAge(sample.Wine) == sample.Age + days
                && sample.Wine.unitValue == sample.Value + (long)sample.Rate * days, "large-batch bottle result");
        MelonLogger.Msg("BE_AGING_TEST full cabinet: " + snapshots.Count + " bottles x " + days + " days, " + watch.ElapsedMilliseconds + " ms");
        SetQuality(rack, 0);
        Check(InventoryOps.Accept(PlayerStore.instance.gridInv, rack), "night fixture placed in store");
        age = AgableHelper.GetAge(wine);
        PlayerStore.instance.EndNight();
        Check(AgableHelper.GetAge(wine) > age && Chilled(wine), "first native EndNight ages and chills");
        age = AgableHelper.GetAge(wine);
        PlayerStore.instance.EndNight();
        Check(AgableHelper.GetAge(wine) > age && Chilled(wine), "second native EndNight keeps chilling");
        rack.Destroy();
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
internal static class LiunianTestNoSave { private static bool Prefix() => !LiunianAgingRuntimeTest.Enabled; }
[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.InitialSave))]
internal static class LiunianTestNoInitialSave { private static bool Prefix() => !LiunianAgingRuntimeTest.Enabled; }
[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile")]
internal static class LiunianTestNoSlotWrite { private static bool Prefix() => !LiunianAgingRuntimeTest.Enabled; }
#endif
