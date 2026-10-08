using System;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace XuxuMusic;

internal static class NativeMusicEvents
{
    private static readonly Il2CppSystem.Action<ModItemDirectory> DirectoryReady =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ModItemDirectory>>(
            (Action<ModItemDirectory>)(directory => Safe(() => LocalMp3Player.Register(directory))));
    private static readonly Il2CppSystem.Action Leaving =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => Safe(() =>
            DesktopMp3Player.Leave(EmporiumEntry.Instance))));
    private static readonly Il2CppSystem.Action Returning =
        DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => Safe(() =>
            DesktopMp3Player.Arrive(EmporiumEntry.Instance))));
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) return;
        ModHook.add_OnModItemDirectoryInit(DirectoryReady);
        try
        {
            ModHook.add_OnLeavingStoreLate(Leaving);
            try { ModHook.add_OnReturningStoreLate(Returning); }
            catch { ModHook.remove_OnLeavingStoreLate(Leaving); throw; }
        }
        catch { ModHook.remove_OnModItemDirectoryInit(DirectoryReady); throw; }
        _registered = true;
    }

    internal static void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        try { ModHook.remove_OnModItemDirectoryInit(DirectoryReady); }
        finally
        {
            try { ModHook.remove_OnLeavingStoreLate(Leaving); }
            finally { ModHook.remove_OnReturningStoreLate(Returning); }
        }
    }

    private static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { Core.Log?.Warning("Native music event failed: " + ex); }
    }
}
