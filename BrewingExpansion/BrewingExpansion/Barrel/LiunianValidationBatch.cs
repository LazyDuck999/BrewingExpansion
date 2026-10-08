using System;
using System.Diagnostics;
using HarmonyLib;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

// Only the shared parent cabinet is deferred. Every changed bottle still runs
// its complete native validation and updates its modified-state snapshot.
internal sealed class LiunianValidationBatch : IDisposable
{
    [ThreadStatic] private static LiunianValidationBatch _current;
    private readonly LiunianValidationBatch _previous;
    private readonly GameItem _machine;
    private bool _pending, _disposed;
    internal int Deferred { get; private set; }
    internal long FlushTicks { get; private set; }

    private LiunianValidationBatch(GameItem machine)
    {
        _machine = machine;
        _previous = _current;
        _current = this;
    }

    internal static LiunianValidationBatch Begin(GameItem machine)
    {
        if (machine?.identifier != LiunianAger.Id)
            throw new ArgumentException("A Liunian cabinet is required.", nameof(machine));
        return new LiunianValidationBatch(machine);
    }

    internal static LiunianValidationBatch BeginRack(GameItem machine) => machine == null
        ? throw new ArgumentNullException(nameof(machine)) : new LiunianValidationBatch(machine);

    internal static bool TryDefer(GameItem item)
    {
        var scope = _current;
        if (scope == null || item == null || item.Pointer != scope._machine.Pointer) return false;
        scope._pending = true;
        scope.Deferred++;
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Restore first, even if native validation or another mod throws.
        _current = _previous;
        if (!_pending) return;
        var start = Stopwatch.GetTimestamp();
        try { if (_machine.TryCast<GameItemElement>()?.IsDestroyed() != true) _machine.ValidateShapeState(); }
        catch (Exception ex) { Core.Log?.Warning("Liunian final parent validation failed: " + ex.Message); }
        finally { FlushTicks = Stopwatch.GetTimestamp() - start; }
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.ValidateShapeState))]
internal static class LiunianParentValidationPatch
{
    internal static bool Prefix(GameItem __instance) => !LiunianValidationBatch.TryDefer(__instance);
}
