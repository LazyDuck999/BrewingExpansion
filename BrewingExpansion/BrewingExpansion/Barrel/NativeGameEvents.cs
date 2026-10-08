using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;

namespace BrewingExpansion;

// Subscribe to the game's extension points without detouring their callers.
internal static class NativeGameEvents
{
    private static readonly Il2CppSystem.Action<ModItemDirectory> DirectoryReady =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ModItemDirectory>>(
            (Action<ModItemDirectory>)(directory => Safe(() =>
                XIAOWOBrewingBarrel.NativeItemDirectoryRegistration.Register(directory))));
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        ModHook.add_OnModItemDirectoryInit(DirectoryReady);
        _registered = true;
        MelonLogger.Msg("[BE-NATIVE] Directory event subscribed (3.5).");
    }

    internal static void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        ModHook.remove_OnModItemDirectoryInit(DirectoryReady);
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { MelonLogger.Warning("[BE-NATIVE] Event failed: " + ex); }
    }
}
