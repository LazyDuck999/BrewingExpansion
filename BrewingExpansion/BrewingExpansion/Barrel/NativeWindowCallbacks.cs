using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using XIAOWOBrewingBarrel;

namespace BrewingExpansion;

internal static class NativeWindowCallbacks
{
    // Pointer stamps only: do not keep old shop stock and windows alive through a global cache.
    private static readonly Dictionary<long, long> Bound = new();

    internal static void Bind(GameItem item)
    {
        var window = item?.contentWindow;
        if (window == null) return;
        var key = window.Pointer.ToInt64();
        var original = window.openCallback;
        if (original != null && Bound.TryGetValue(key, out var stamp) && stamp == original.Pointer.ToInt64()) return;
        var callback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() =>
        {
            try { original?.Invoke(); }
            finally
            {
                try
                {
                    BarrelItem.CenterWindowControls(window);
                    ConcentratorItem.AlignWindowControls(window);
                    LiunianAger.RefreshWindowLayout(window);
                    IncubatorLayout.Refresh(window);
                    // Resolve owners at open time: native cloning/save restoration can reparent a window.
                    var owners = window.parentItems;
                    if (owners != null)
                        for (var i = 0; i < owners.Count; i++) BarrelMaintenanceEvents.QueueItem(owners[i]);
                }
                catch (Exception ex) { Core.Log?.Warning("Native window refresh failed: " + ex); }
            }
        }));
        window.openCallback = callback;
        Bound[key] = callback.Pointer.ToInt64();
    }

    internal static void ClearStamps() => Bound.Clear();
}
