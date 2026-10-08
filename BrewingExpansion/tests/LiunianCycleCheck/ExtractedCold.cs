// Generated from current production methods. Native API is simulated.
using Il2Cpp;
namespace XIAOWOBrewingBarrel;
internal static partial class LiunianAger {
private static readonly Dictionary<long, GameItem> NightCycles = new();
private static bool _inNight;
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
[HarmonyLib.HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned))]
internal static class LiunianChillOnCyclePatch
{
    [HarmonyLib.HarmonyPostfix]
    internal static void Postfix(GameItem machine)
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
    internal static void Prefix() => LiunianAger.BeginNight();

    [HarmonyLib.HarmonyPostfix, HarmonyLib.HarmonyPriority(HarmonyLib.Priority.Last)]
    internal static void Postfix() => LiunianAger.FinishNight();

    [HarmonyLib.HarmonyFinalizer]
    internal static Exception Finalizer(Exception __exception)
    {
        if (__exception != null) LiunianAger.AbortNight();
        return __exception;
    }
}
