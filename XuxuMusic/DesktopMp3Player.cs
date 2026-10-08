using System;
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace XuxuMusic;

// A clickable Image beside the native cassette fixture. A detached GameItem
// keeps playlist settings and the existing controls, without a slot inventory
// or invisible item-rendering node on the desktop.
internal static class DesktopMp3Player
{
    private const string SaveFileKey = "BrewingExpansion.DesktopMp3.File.v1";
    private const string SaveVolumeKey = "BrewingExpansion.DesktopMp3.Volume.v1";
    // In the reference shop image: filing cabinet front, x=86.3%, y=44%
    // from the bottom. Native cassette window bounds are NOT the small tape
    // device painted on the desk; using them put our entry off-screen.
    private static readonly Vector2 DeskPoint = new(.863f, .44f);
    private static EmporiumEntry _entry;
    private static EmporiumEntry _sceneEntry;
    private static GameObject _fixture;
    private static GameObject _hoverRange;
    private static GameItem _item;
    private static bool _away;
    private static int _scheduleVersion;

    internal static bool IsFixture(GameItem item) => item != null && _item != null && item.Pointer == _item.Pointer;

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

    private static void Install(EmporiumEntry entry)
    {
        try
        {
            if (_entry != null && _entry.Pointer == entry.Pointer && _fixture != null)
            {
                _fixture.SetActive(!_away);
                Position(entry);
                return;
            }
            Release();
            _sceneEntry = entry;
            BrewingExpansion.MusicSupport.EnsureAssetsReady();
            _entry = entry;
            _away = false;
            _item = LocalMp3Player.CreateFixed();
            // Native ownership reads the validated tag state. skipValidate=true
            // left the detached item looking unowned in the native tooltip.
            GeneralHelper.SetItemOwned(_item, true);
            RestoreSettings();
            var sprite = MusicAssets.GetSprite()
                ?? throw new InvalidOperationException("Embedded mp3.png is unavailable.");
            _fixture = new GameObject("BE_DesktopMP3");
            _fixture.SetActive(false);
            _fixture.AddComponent<RectTransform>();
            var image = _fixture.AddComponent<Image>();
            // A full rectangular hit target, including transparent sprite pixels.
            image.color = Color.clear;
            image.raycastTarget = true;
            CreateHoverRange();
            var icon = new GameObject("MP3Icon");
            icon.layer = _fixture.layer;
            var iconRect = icon.AddComponent<RectTransform>();
            iconRect.SetParent(_fixture.transform, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            var iconImage = icon.AddComponent<Image>();
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            // Native ItemTooltipHandler raycasts TreeNodeRender.element. Merely
            // showing a tooltip on PointerEnter would be cleared by that handler.
            // Connect the detached item as a native TooltipListener, but keep its
            // rendering/layout independent of inventory slots and drag handlers.
            var node = _fixture.AddComponent<TreeNodeRender>();
            node.rectTransform = _fixture.GetComponent<RectTransform>();
            node.image = image;
            node.element = _item.Cast<PixelElement>();
            node.eventHandler = new MultiEventHandler();
            node.eventHandler.mouseEnterFunc.Add((Il2CppSystem.Func<PointerEventData, bool>)(_ => Hover(true)));
            node.eventHandler.mouseMoveFunc.Add((Il2CppSystem.Func<PointerEventData, bool>)(_ => Hover(true)));
            node.eventHandler.mouseExitFunc.Add((Il2CppSystem.Func<PointerEventData, bool>)(_ => Hover(false)));
            var button = _fixture.AddComponent<Button>();
            button.targetGraphic = iconImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (_away || _item == null) return;
                ClearHover();
                try { LocalMp3Player.Open(_item); }
                catch (Exception ex) { Core.Log?.Warning("MP3 desktop click failed: " + ex); }
            }));
            Position(entry);
            _fixture.SetActive(true);
            var rect = _fixture.GetComponent<RectTransform>();
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Core.Log?.Msg("MP3 desktop Image ready: 3x2, mp3.png; parent="
                + rect.parent?.name + "; sibling=" + rect.GetSiblingIndex()
                + "; local=" + rect.anchoredPosition + "; screen="
                + RectTransformUtility.WorldToScreenPoint(camera, rect.position) + "; size=" + rect.sizeDelta
                + "; scale=" + rect.lossyScale + "; canvas=" + canvas?.name
                + "; active=" + rect.gameObject.activeInHierarchy
                + "; owned=" + GeneralHelper.IsItemOwned(_item));
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("MP3 desktop fixture failed: " + ex);
            Release();
        }
    }

    private static void CreateHoverRange()
    {
        _hoverRange = new GameObject("MP3HoverRange_3x2");
        var root = _hoverRange.AddComponent<RectTransform>();
        root.SetParent(_fixture.transform, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
        root.sizeDelta = new Vector2(48f, 32f);
        // One transparent footprint, like native fixed facilities: only the
        // outside border, with neither colored fill nor internal cell lines.
        var edges = new[]
        {
            (new Vector2(0f, 15.5f), new Vector2(48f, 1f)),
            (new Vector2(0f, -15.5f), new Vector2(48f, 1f)),
            (new Vector2(-23.5f, 0f), new Vector2(1f, 32f)),
            (new Vector2(23.5f, 0f), new Vector2(1f, 32f))
        };
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = new GameObject($"Edge_{i}");
            var rect = edge.AddComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = edges[i].Item2;
            rect.anchoredPosition = edges[i].Item1;
            var image = edge.AddComponent<Image>();
            image.sprite = RenderHandler.current.black;
            image.color = Color.white;
            image.raycastTarget = false;
        }
        // The icon is drawn above the border; none of these edges receives input.
        _hoverRange.SetActive(false);
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

    internal static void Position(EmporiumEntry entry)
    {
        if (_fixture == null || _entry == null || entry?.Pointer != _entry.Pointer) return;
        try
        {
            var render = RenderHandler.current;
            var canvas = render?.windowUICanvas;
            var canvasRect = canvas?.GetComponent<RectTransform>();
            var parent = render?.windowRoot?.rectTransform;
            var target = _fixture.GetComponent<RectTransform>();
            if (canvasRect == null || parent == null || target == null) return;
            target.SetParent(parent, false);
            _fixture.layer = canvas.gameObject.layer;
            foreach (var child in _fixture.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = canvas.gameObject.layer;
            _fixture.GetComponent<TreeNodeRender>().parent = render.windowRoot;
            target.anchorMin = target.anchorMax = target.pivot = new Vector2(.5f, .5f);
            target.localRotation = Quaternion.identity;
            target.localScale = new Vector3(canvasRect.lossyScale.x / parent.lossyScale.x,
                canvasRect.lossyScale.y / parent.lossyScale.y, 1f);
            target.sizeDelta = new Vector2(48f, 32f); // 3x2 native 16px cells.
            var area = canvasRect.rect;
            var width = area.width;
            var height = area.height;
            // Follow the centered shop picture if the viewport is letterboxed.
            var aspect = entry.targetAspect > 0f ? entry.targetAspect : 16f / 9f;
            if (width / height > aspect) width = height * aspect;
            else height = width / aspect;
            var point = area.center + new Vector2((DeskPoint.x - .5f) * width,
                (DeskPoint.y - .5f) * height);
            target.position = canvasRect.TransformPoint((Vector3)point);
            // Above the physical shop scene, below popup/item windows.
            target.SetAsFirstSibling();
        }
        catch (Exception ex) { Core.Log?.Warning("MP3 desktop position failed: " + ex.Message); }
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

    private static void RestoreSettings()
    {
        var data = PlayerStore.instance?.modData;
        if (data == null) return;
        if (data.ContainsKey(SaveFileKey) && LocalMusicLibrary.IsFileName(data[SaveFileKey]))
            _item.ModifyTag(LocalMp3Player.FileTag, (Il2CppSystem.Action<TagState>)(s => s.valueString = data[SaveFileKey]), true);
        if (data.ContainsKey(SaveVolumeKey) && int.TryParse(data[SaveVolumeKey], out var volume))
            _item.ModifyTag(LocalMp3Player.VolumeTag, (Il2CppSystem.Action<TagState>)(s => s.valueInt = Math.Clamp(volume, 0, 100)), true);
    }

    internal static void SaveSettings()
    {
        try
        {
            var store = PlayerStore.instance;
            if (store == null || _item == null) return;
            store.modData ??= new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
            store.modData[SaveFileKey] = _item.IsTag(LocalMp3Player.FileTag)
                ? _item.GetTagReadonly(LocalMp3Player.FileTag).valueString ?? "" : "";
            store.modData[SaveVolumeKey] = Math.Clamp(_item.GetTagReadonly(LocalMp3Player.VolumeTag)?.valueInt ?? 60,
                0, 100).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) { Core.Log?.Warning("MP3 desktop settings save failed: " + ex.Message); }
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
}

[HarmonyPatch(typeof(EmporiumEntry), "Start")]
internal static class DesktopMp3StartPatch { private static void Postfix(EmporiumEntry __instance) => DesktopMp3Player.Schedule(__instance); }
[HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.Validate))]
internal static class DesktopMp3LayoutPatch { private static void Postfix(EmporiumEntry __instance) => DesktopMp3Player.Position(__instance); }
[HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.UpdateScaleFactor))]
internal static class DesktopMp3ScalePatch { private static void Postfix(EmporiumEntry __instance) => DesktopMp3Player.Position(__instance); }
[HarmonyPatch(typeof(EmporiumEntry), "OnDestroy")]
internal static class DesktopMp3DestroyPatch { private static void Prefix(EmporiumEntry __instance) => DesktopMp3Player.DestroyScene(__instance); }
[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
internal static class DesktopMp3SavePatch { private static void Prefix() => DesktopMp3Player.SaveSettings(); }
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
