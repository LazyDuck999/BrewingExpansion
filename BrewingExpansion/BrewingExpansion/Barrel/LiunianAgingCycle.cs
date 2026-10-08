using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace XIAOWOBrewingBarrel;

// Shared batch settlement for our cabinet and reviewed native rack callbacks.
// Quality and power decisions still use the original game APIs.
internal static class LiunianAgingCycle
{
    private const string CounterfeitTag = "WINE_COUNTERFEIT_TAG";
    private static readonly HashSet<long> ActiveCycles = new();
    private static readonly Il2CppSystem.Func<GameItem, bool> AgeableFilter =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem, bool>>((Func<GameItem, bool>)MachineWineRack.CanAgeWine);

    internal static void Bind(GameItem machine)
    {
        if (machine.onCycleEndSlotItemFunc == null)
            throw new InvalidOperationException("Liunian's native aging callback is missing.");
        // This cabinet always owns its settlement, even when daily aging APIs
        // have foreign patches. Do not invoke the native rack's aging loop.
        machine.onCycleEndSlotItemFunc = DelegateSupport.ConvertDelegate<
            Il2CppSystem.Action<GameItem, GameInventory, SlotMarker>>(
            (Action<GameItem, GameInventory, SlotMarker>)((_, _, _) => Run(machine)));
    }

    private static void Run(GameItem machine) => RunRack(machine, LiunianAger.Storage(machine),
        LiunianAger.Modules(machine), LiunianAger.Battery(machine), "BE-LIUNIAN-PERF");

    internal static void RunRack(GameItem machine, GameGridInventory storage, GameGridInventory modules,
        GameSlotInventory battery, string logLabel = "BE-RACK-PERF")
    {
        var key = machine.Pointer.ToInt64();
        if (!ActiveCycles.Add(key))
        {
            Core.Log?.Warning("Skipped reentrant Liunian aging cycle: " + machine.uniqueId);
            return;
        }
        try
        {
            var watch = Stopwatch.StartNew();
            if (storage == null || modules == null || battery == null)
                throw new InvalidOperationException("Liunian's native machine inventories are incomplete.");
            // Match the native rack's recursive filter and take its snapshot before drawing power.
            var wines = GraphUtils.FindAllChildrenType<GameItem>(storage.Cast<GraphNodeStorage>(), AgeableFilter);
            if (wines == null || wines.Count == 0) return;
            if (!MachineHelper.TryDrawCyclePower(machine, battery)) return;

            // Native API includes the base day and observes other mods' quality calculations.
            var days = MachineWineRack.GetAgingDays(machine);
            var preparedTicks = watch.ElapsedTicks;
            var aged = 0;
            var limited = 0;
            var failed = 0;
            var validation = LiunianValidationBatch.BeginRack(machine);
            using (validation)
            for (var i = 0; i < wines.Count; i++)
            {
                var wine = wines[i];
                if (wine == null || wine.IsTag(CounterfeitTag)) continue;
                try
                {
                    if (!ExtendedWineAge.TryAdvance(wine, days, out var plan))
                    {
                        failed++;
                        continue;
                    }
                    if (plan.AgeLimited) limited++;
                    if (plan.Days == 0) continue;
                    aged++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Core.Log?.Warning("Liunian skipped a failed wine update: " + ex.Message);
                }
            }
            var agingTicks = watch.ElapsedTicks - preparedTicks;
            // Modules and machine-action listeners observe the final age/value, once per cycle.
            var moduleBatch = LiunianModuleBatch.Begin(machine, modules.Cast<GameInventory>());
            using (moduleBatch) MachineHelper.OnMachineActioned(machine, modules.Cast<GameInventory>());
            watch.Stop();
            Core.Log?.Msg("[" + logLabel + "] cycle=" + machine.uniqueId + " wines=" + aged + "/" + wines.Count
                + " days=" + days + " prepareMs=" + Milliseconds(preparedTicks)
                + " ageMs=" + Milliseconds(agingTicks) + " parentMs=" + Milliseconds(validation.FlushTicks)
                + " parentDeferred=" + validation.Deferred + " moduleMs=" + Milliseconds(moduleBatch.ModuleTicks)
                + " modulesBatched=" + moduleBatch.Batched + " moduleMode=" + moduleBatch.Mode
                + " totalMs=" + Milliseconds(watch.ElapsedTicks) + " ageLimit=" + limited + " skipped=" + failed);
        }
        catch (Exception ex)
        {
            // A cycle may already have drawn power or updated wines. Never replay it here.
            Core.Log?.Error("Liunian bulk aging cycle failed: " + ex);
        }
        finally { ActiveCycles.Remove(key); }
    }

    internal static string Milliseconds(long ticks) => (ticks * 1000d / Stopwatch.Frequency)
        .ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

}
