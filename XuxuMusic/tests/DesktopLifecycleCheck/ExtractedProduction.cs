// Generated from current production methods; native rendering is simulated.
using System.Collections; using Il2Cpp; using MelonLoader; using HarmonyLib; using UnityEngine;
namespace XuxuMusic {
internal static class DesktopMp3Player {
    private static EmporiumEntry _entry;
    private static EmporiumEntry _sceneEntry;
    private static GameObject _fixture;
    private static GameObject _hoverRange;
    private static GameItem _item;
    private static bool _away;
    private static int _scheduleVersion;

    internal static void Schedule(EmporiumEntry entry)
    {
        if (entry == null) return;
        if (_sceneEntry == null || _sceneEntry.Pointer != entry.Pointer)
        {
            _sceneEntry = entry;
            _away = false;
        }
        var version = ++_scheduleVersion;
        if (_away) return;
        MelonCoroutines.Start(InstallWhenReady(entry, version));
    }
    private static IEnumerator InstallWhenReady(EmporiumEntry entry, int version)
    {
        // Only a bounded scene-initialization wait; no permanent update loop.
        for (var n = 0; n < 90; n++)
        {
            yield return null;
            if (version != _scheduleVersion || _away || entry == null || entry != EmporiumEntry.Instance) yield break;
            if (Mp3LoadState.Loading || BrewingExpansion.MusicSupport.Loading || PlayerStore.instance == null
                || RenderHandler.current?.windowUICanvas == null
                || RenderHandler.current.windowRoot?.rectTransform == null) continue;
            Install(entry);
            yield break;
        }
        Core.Log?.Warning("MP3 desktop fixture: native table was not ready.");
    }
    private static bool Hover(bool visible)
    {
        if (_hoverRange != null) _hoverRange.SetActive(visible && !_away);
        // Let the native event dispatcher continue its normal processing.
        return false;
    }
    private static void ClearHover(bool hideOverlay = true)
    {
        Hover(false);
        var tooltip = ItemTooltipHandler.current;
        if (tooltip == null) return;
        // The overlay is shared: never hide a different item's current tooltip.
        if (_item != null && tooltip?.currentTooltipListener?.Pointer == _item.Pointer)
        {
            tooltip.currentTooltipListener = null;
            // During scene destruction the handler can outlive its child UI.
            // Releasing the fixture must not call into an already dead overlay.
            var overlay = OverlayHandler.current;
            if (hideOverlay && overlay != null) overlay.HideTooltip();
        }
    }
    internal static void Leave(EmporiumEntry entry)
    {
        if (entry == null || _sceneEntry == null || entry.Pointer != _sceneEntry.Pointer) return;
        _away = true;
        ++_scheduleVersion; // Cancel initialization even before the fixture exists.
        ClearHover();
        if (_fixture != null) _fixture.SetActive(false);
        if (_item?.contentWindow?.IsVisible() == true) _item.contentWindow.Hide();
    }
    internal static void Arrive(EmporiumEntry entry)
    {
        if (entry == null) return;
        _away = false;
        if (_fixture == null || _entry == null || _entry.Pointer != entry.Pointer) Schedule(entry);
        else { _fixture.SetActive(true); Position(entry); }
    }
    internal static void Release()
    {
        ++_scheduleVersion;
        _sceneEntry = null;
        try { ClearHover(false); }
        catch (Exception ex) { Core.Log?.Warning("MP3 hover cleanup failed: " + ex.Message); }
        var item = _item;
        var fixture = _fixture;
        _item = null;
        _fixture = null;
        _hoverRange = null;
        _entry = null;
        _away = false;
        // Scene shutdown may already have destroyed the Unity UI objects.
        // Dispose each resource separately so a dead window cannot skip audio
        // cancellation or item cleanup.
        if (item != null)
        {
            try { LocalMp3Player.Remove(item); }
            catch (Exception ex) { Core.Log?.Warning("MP3 desktop player cleanup failed: " + ex.Message); }
            // Remove already disposed its controls. Do not invoke native item
            // destruction against that dead UI during scene shutdown. This
            // transient item is detached and not registered in any inventory.
            try { item.contentWindow = null; }
            catch (Exception ex) { Core.Log?.Warning("MP3 item window detach failed: " + ex.Message); }
        }
        if (fixture != null) UnityEngine.Object.Destroy(fixture);
    }
    internal static void DestroyScene(EmporiumEntry entry)
    {
        if (entry == null || _sceneEntry == null || entry.Pointer != _sceneEntry.Pointer) return;
        Release();
    }
internal static int Installed, Positioned;
private static void Install(EmporiumEntry entry) {
    Release(); _sceneEntry = _entry = entry; _fixture = new(); _hoverRange = new();
    _item = new() { identifier = "be_mp3_player", contentWindow = new() }; Installed++;
}
internal static void Position(EmporiumEntry entry) { Positioned++; }
internal static void SetHover(GameObject hover) { _hoverRange = hover; }
internal static GameItem Item => _item;
internal static GameObject Fixture => _fixture;
internal static void ClearForTest() => ClearHover();
}
[HarmonyPatch(typeof(GameItem), nameof(GameItem.CanSelectSlotItem))]
internal static class DesktopMp3SelectPatch
{
    internal static bool Prefix(GameItem __instance, ref bool __result)
    {
        if (!LocalMp3Player.IsPlayer(__instance)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.MaySelectSlotItem))]
internal static class DesktopMp3MaySelectPatch
{
    internal static bool Prefix(GameItem __instance, ref bool __result)
    {
        if (!LocalMp3Player.IsPlayer(__instance)) return true;
        __result = false;
        return false;
    }
}

internal static class Mp3NewRunPatch
{
    private static void Prefix() => LocalMp3Player.Reset();
    private static void Postfix() => DesktopMp3Player.Schedule(EmporiumEntry.Instance);
}
}