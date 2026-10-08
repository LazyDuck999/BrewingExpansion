using System;
using System.Reflection;
using Il2Cpp;
using MelonLoader;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: MelonInfo(typeof(XuxuMusic.MusicMod), "xuxu的MP3", "1.0.0", "xuxu")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]
[assembly: HarmonyDontPatchAll]
[assembly: MelonAdditionalDependencies("BrewingExpansion")]

namespace XuxuMusic;

public sealed class MusicMod : MelonMod
{
    private bool _enabled;

    public override void OnInitializeMelon()
    {
        var prerequisite = typeof(BrewingExpansion.BrewingExpansionMod).Assembly;
        if (prerequisite.GetType("BrewingExpansion.MusicSupport", false) == null)
        {
            Core.Log.Warning("需要配套的酿酒拓展前置。请安装分离播放器后的 BrewingExpansion.dll。");
            return;
        }
        // Older/duplicate BrewingExpansion DLLs still contain a player. Avoid
        // installing a second set of hooks or creating a duplicate fixture.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType("XIAOWOBrewingBarrel.DesktopMp3Player", false) == null) continue;
            Core.Log.Warning("An older BrewingExpansion DLL already includes the MP3 player. "
                + "Update BrewingExpansion to the separated prerequisite build before enabling XuxuMusic.");
            return;
        }
        NativeMusicEvents.Register();
        try { HarmonyInstance.PatchAll(typeof(MusicMod).Assembly); }
        catch
        {
            try { NativeMusicEvents.Unregister(); }
            finally { HarmonyInstance.UnpatchSelf(); }
            throw;
        }
        _enabled = true;
        Core.Log.Msg("MP3 player 1.0.0 ready; prerequisite: BrewingExpansion. Music: Mods\\xuxumusic.");
    }

    public override void OnDeinitializeMelon()
    {
        if (!_enabled) return;
        try { NativeMusicEvents.Unregister(); }
        catch (Exception ex) { Core.Log.Warning("Native events shutdown: " + ex); }
        try { LocalMp3Player.Reset(); }
        catch (Exception ex) { Core.Log.Warning("Player shutdown: " + ex.Message); }
    }
}

internal static class Core
{
    internal static readonly MusicLogger Log = new();
}

internal sealed class MusicLogger
{
    internal void Msg(string text) => MelonLogger.Msg("[XUXU-MUSIC] " + text);
    internal void Warning(string text) => MelonLogger.Warning("[XUXU-MUSIC] " + text);
}

internal static class Mp3LoadState
{
    internal static bool Loading;
}
