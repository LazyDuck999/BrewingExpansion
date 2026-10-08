using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

// Keep the original OnMachineActioned traversal and final calculations. Only
// replace a verified quality module's inner action + redundant group recompute.
internal sealed class LiunianModuleBatch : IDisposable
{
    [ThreadStatic] private static LiunianModuleBatch _current;
    private readonly LiunianModuleBatch _previous;
    private readonly GameItem _machine;
    private readonly GameInventory _grid;
    private readonly HashSet<long> _members = new();
    private readonly long _started = Stopwatch.GetTimestamp();
    private bool _enabled, _disposed, _marked, _touched;
    private static readonly string[] EffectTags = { "MODULE_EFFECT1_TAG", "MODULE_EFFECT2_TAG" };
    internal static bool Enabled { get; set; } = true;
    internal int Batched { get; private set; }
    internal string Mode { get; private set; } = "native";
    internal long ModuleTicks { get; private set; }

    private static readonly MethodInfo[] NativeMethods = {
        AccessTools.Method(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned)),
        AccessTools.Method(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ActivateOnUseAction)),
        AccessTools.Method(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ComputeAllModuleTempStat)),
        AccessTools.Method(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ResetAllTempStat)),
        AccessTools.Method(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.HandleModuleEffectAction)),
        AccessTools.Method(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.Degrade)),
        AccessTools.Method(typeof(ModuleHelper), nameof(ModuleHelper.ComputeModuleEffect),
            new[] { typeof(GameInventory), typeof(GameItem), typeof(GameItem) }),
        AccessTools.Method(typeof(ModuleHelper), nameof(ModuleHelper.ComputeModuleEffect),
            new[] { typeof(Il2CppSystem.Collections.Generic.List<GameItem>), typeof(GameItem) }),
        AccessTools.Method(typeof(ModuleHelper), nameof(ModuleHelper.ApplyBasicModuleEffect))
    };

    private LiunianModuleBatch(GameItem machine, GameInventory grid)
    {
        _machine = machine; _grid = grid;
        _previous = _current;
        _current = this;
        try
        {
            if (machine == null || grid == null) return;
            if (!Enabled) { Mode = "native-disabled"; return; }
            var overlap = ForeignPatch();
            if (overlap != null) { Mode = "native-overlap:" + overlap; return; }
            var items = grid.childItems;
            if (items == null || items.Count < 2) { Mode = "native-small"; return; }
            for (var i = 0; i < items.Count; i++)
            {
                if (!IsSafeQualityModule(items[i])) { Mode = "native-special-module"; return; }
                _members.Add(items[i].Pointer.ToInt64());
            }
            _enabled = true;
            Mode = "quality-batch";
        }
        catch (Exception ex)
        {
            Mode = "native-check-failed";
            Core.Log?.Warning("Liunian retained native module settlement: " + ex.Message);
        }
    }

    internal static LiunianModuleBatch Begin(GameItem machine, GameInventory grid) => new(machine, grid);

    // Liunian already owns a scope for profiling. A native completion must not
    // open a second scope for that same machine; nested other machines do.
    internal static LiunianModuleBatch EnterNative(GameItem machine, GameInventory grid) =>
        machine == null || grid == null || (_current?._machine?.Pointer == machine.Pointer
            && _current?._grid?.Pointer == grid.Pointer) ? null : Begin(machine, grid);

    private static string ForeignPatch()
    {
        var ownAssembly = typeof(LiunianModuleBatch).Assembly;
        foreach (var method in NativeMethods)
        {
            if (method == null) return "missing-method";
            var patches = HarmonyLib.Harmony.GetPatchInfo(method);
            if (patches == null) continue;
            foreach (var patch in patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers).Concat(patches.ILManipulators))
                if (patch.PatchMethod?.DeclaringType?.Assembly != ownAssembly) return patch.owner ?? "unknown";
        }
        return null;
    }

    private static bool SafeEffect(GameItem item, string tag)
    {
        if (!item.IsTag(tag)) return true;
        var id = item.GetTagReadonly(tag)?.valueString;
        if (id is not ("MODULE_EFFECT_PREMIUM" or "MODULE_EFFECT_INFERIOR" or "MODULE_EFFECT_DEGRADING")) return false;
        var effects = ModuleEffectHelper.moduleEffects;
        if (effects == null || !effects.TryGetValue(id, out var effect) || effect == null) return false;
        // Premium/inferior any-actions are native no-ops (same reviewed ret
        // implementation); degrading changes only this module's permanent stats.
        if (effect.onAnyAction != null && !NativeActionIdentity.IsSingleNative(effect.onAnyAction, 0x2FF7B0)) return false;
        if (id == "MODULE_EFFECT_DEGRADING")
            return NativeActionIdentity.IsSingleNative(effect.onUsedAction, 0x4E4780);
        return effect.onUsedAction == null;
    }

    private static bool IsSafeQualityModule(GameItem item) => item != null
        && item.identifier is "system_module_quality" or "random_quality_module"
            or "system_capped_neural_core" or "system_module_performance" or "system_module_efficiency"
        && SafeEffect(item, "MODULE_EFFECT1_TAG") && SafeEffect(item, "MODULE_EFFECT2_TAG");

    internal static bool TryActivate(GameItem item, GameInventory grid, GameItem machine)
    {
        var scope = _current;
        if (scope == null || !scope._enabled || item == null || grid == null || machine == null
            || machine.Pointer != scope._machine.Pointer || grid.Pointer != scope._grid.Pointer
            || !scope._members.Contains(item.Pointer.ToInt64())) return false;
        // Recheck before any side effect. A changed/unknown module resumes the
        // normal pipeline; never replay an action after it has already run.
        if (!IsSafeQualityModule(item))
        {
            // A callback can change later modules after preflight. Catch up
            // derived stats before that module reads the machine, then leave
            // the rest of this cycle on the native pipeline.
            scope.FlushDerived();
            scope._enabled = false;
            scope.Mode = "native-module-changed";
            return false;
        }
        scope._touched = true;
        var resumedNative = false;
        foreach (var tag in EffectTags)
            if (item.IsTag(tag))
            {
                // A first-slot action can change the second slot through a
                // native validation callback. Catch up before the new action
                // reads derived stats; finish this item without replaying slot 1.
                if (!resumedNative && !SafeEffect(item, tag))
                {
                    scope.FlushDerived();
                    scope._enabled = false;
                    scope.Mode = "native-module-changed";
                    resumedNative = true;
                }
                var id = item.GetTagReadonly(tag).valueString;
                ModuleEffectHelper.moduleEffects[id].onUsedAction?.Invoke(item);
            }
        if (resumedNative) scope.FlushDerived();
        else scope.Batched++;
        return true;
    }

    internal static void MarkModuleCompleted(GameItem machine)
    {
        var scope = _current;
        if (scope == null || machine?.Pointer != scope._machine?.Pointer || scope._marked) return;
        scope._marked = true;
        scope.ModuleTicks = Stopwatch.GetTimestamp() - scope._started;
    }

    private void FlushDerived()
    {
        ModuleEffectHelper.ComputeAllModuleTempStat(_grid.childItems);
        ModuleHelper.ComputeModuleEffect(_grid, null, _machine);
        ModuleHelper.ApplyBasicModuleEffect(_grid, null, _machine);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current = _previous;
        if (!_marked)
        {
            // Failure may have changed permanent module values. Restore their
            // derived stats, without replaying any use/loss/random action.
            if (_enabled && _touched)
                try
                {
                    FlushDerived();
                }
                catch (Exception ex) { Core.Log?.Warning("Liunian module stat recovery failed: " + ex.Message); }
            ModuleTicks = Stopwatch.GetTimestamp() - _started;
        }
    }
}

[HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned))]
internal static class MachineModuleCycleBatchPatch
{
    [HarmonyPrefix]
    internal static void Prefix(GameItem machine, GameInventory moduleGrid, out LiunianModuleBatch __state) =>
        __state = LiunianModuleBatch.EnterNative(machine, moduleGrid);

    [HarmonyPostfix, HarmonyPriority(Priority.First)]
    internal static void Postfix(GameItem machine) => LiunianModuleBatch.MarkModuleCompleted(machine);

    [HarmonyFinalizer]
    internal static Exception Finalizer(Exception __exception, LiunianModuleBatch __state)
    {
        if (__state == null) return __exception;
        try
        {
            __state.Dispose();
            Core.Log?.Msg("[BE-MODULE-PERF] mode=" + __state.Mode + " batched=" + __state.Batched
                + " moduleMs=" + LiunianAgingCycle.Milliseconds(__state.ModuleTicks)
                + " failed=" + (__exception != null));
        }
        catch (Exception ex) { Core.Log?.Warning("Module batch cleanup failed: " + ex.Message); }
        return __exception;
    }
}

[HarmonyPatch(typeof(ModuleEffectHelper), nameof(ModuleEffectHelper.ActivateOnUseAction))]
internal static class LiunianModuleUseBatchPatch
{
    internal static bool Prefix(GameItem item, GameInventory moduleGrid, GameItem machine) =>
        !LiunianModuleBatch.TryActivate(item, moduleGrid, machine);
}
