using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace XuxuMusic;

internal static class LocalMp3Player
{
    internal const string Id = "be_mp3_player";
    internal const string FileTag = "BE_MP3_FILENAME";
    internal const string VolumeTag = "BE_MP3_VOLUME";
    private static readonly Dictionary<long, Controls> Windows = new();
    private static readonly HashSet<long> CenteredButtons = new();
    private static Controls _active;
    private static AudioClip _clip;
    private static LocalMusicLibrary.Song _loadedSong;
    private static UnityWebRequest _request;
    private static int _generation;
    private static bool _paused;
    private static bool _loading;
    private static bool _systemPaused;
    private static bool _ownAudioCall;
    private static bool _musicVolumeOverridden;
    private static float _nativeMusicVolume;

    private sealed class Controls
    {
        internal GameItem Item;
        internal PixelWindow Window;
        internal TagElement Status;
        internal TagElement Library;
        internal TagElement Volume;
        internal List<LocalMusicLibrary.Song> Songs = new();
        internal int Index;
        internal readonly List<ButtonElement> Buttons = new();
    }

    internal static bool IsPlayer(GameItem item) => item?.identifier == Id;
    internal static string MusicFolder => LocalMusicLibrary.Folder(Application.dataPath);
    internal static bool IsOwned(GameItem item) => IsPlayer(item)
        && (DesktopMp3Player.IsFixture(item) || MusicAssets.IsOwned(item));

    internal static void Register(ModItemDirectory directory)
    {
        try
        {
            // Like dossier/cassette fixtures: created by the scene, not listed
            // in the public item catalogue or available to spawn tools.
            directory.factoryDictionary.Remove(Id);
        }
        catch (Exception ex) { Core.Log?.Warning("MP3 registration failed: " + ex.Message); }
        try { LocalMusicLibrary.EnsureFolder(Application.dataPath); }
        catch (Exception ex) { Core.Log?.Warning("MP3 folder unavailable: " + ex.Message); }
    }

    internal static GameItem CreateFixed()
    {
        var item = ItemDirectory.CreateEmptyItem(Id)
            ?? throw new InvalidOperationException("Cannot create MP3 player.");
        item.uniqueId = -3303; // Separate from native fixture IDs -101..-103.
        item.shape = new GridShapeBuilder(3, 2).SetDataFill(3, 2, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetSprite(MusicAssets.SpriteAtlas, "mp3");
        item.SetValue(0);
        item.ModifyTag(VolumeTag, (Il2CppSystem.Action<TagState>)(s => s.valueInt = 60), true);
        EnsureLoaded(item);
        item.ValidateShapeState();
        return item;
    }

    internal static void EnsureLoaded(GameItem item)
    {
        if (!IsPlayer(item)) return;
        // Also normalize old, already spawned MP3s when they are loaded.
        // Native fixed facilities have no MACHINE type and are important.
        if (item.itemTypes != null && item.itemTypes.Count > 0) item.itemTypes.Clear();
        if (item.IsTag("MACHINE")) item.DisableTag("MACHINE", true);
        if (!item.IsTag("IMPORTANT_TAG")) item.EnableTag("IMPORTANT_TAG", true);
        // Native selection uses InvokeAllReduce / DynamicInvoke, which cannot
        // invoke these converted managed Func delegates. Scoped Prefix guards
        // reject only our player before that path, including legacy spawned ones.
        item.canSelectSlotItemFunc = null;
        item.maySelectSlotItemFunc = null;
        if (item.spritePath != "mp3" || item.spriteAtlasPath != MusicAssets.SpriteAtlas)
            item.SetSprite(MusicAssets.SpriteAtlas, "mp3");
        if (item.shape?.width != 3 || item.shape?.height != 2)
        {
            item.shape = new GridShapeBuilder(3, 2).SetDataFill(3, 2, 1).Build();
            item.modifiedShape = item.shape.Clone();
            item.ValidateShapeState();
        }
        item.SetName(MusicAssets.Text("xuxu的MP3", "xuxu's MP3"));
        var description = MusicAssets.Text(
            "把自己的旋律留在店里。将MP3文件放入游戏目录的 Mods\\xuxumusic 文件夹，单击桌面上固定的播放器。\n\n自动读取全部MP3，支持播放、暂停、上一首、下一首、音量调节与刷新列表。按文件名排序，播放结束后顺序循环。\n\n欢迎大家体验酿酒拓展、香烟拓展及更多外环区mod！\n\n——来自xuxu",
            "Leave your own melodies in the shop. Put MP3 files in Mods\\xuxumusic inside the game folder, then click the fixed desktop player.\n\nReads all MP3s, sorted by filename. Play, pause, skip, adjust volume or refresh the list. Songs continue in a repeating playlist.\n\nEveryone is welcome to try Brewing Expansion, Tobacco Expansion and more Outer Ring mods!\n\n— From xuxu");
        if (item.shortDescription != description) item.shortDescription = description;
        if (item.longDescription != description) item.longDescription = description;
        var key = item.Pointer.ToInt64();
        if (Windows.TryGetValue(key, out var controls) && controls.Item.uniqueId == item.uniqueId
            && controls.Window?.Pointer == item.contentWindow?.Pointer) return;
        if (controls != null)
        {
            Stop(controls);
            if (controls.Window.IsVisible()) controls.Window.Hide();
        }
        Build(item);
    }

    private static void Build(GameItem item)
    {
        var c = new Controls { Item = item, Window = new PixelWindow(true),
            Status = new TagElement(240), Library = new TagElement(240), Volume = new TagElement(70) };
        UpdateVolume(c);
        var root = new GridPixelElement(1, 5, true).SetSpacing(3, 3);
        root.Attach(c.Status.SetText(MusicAssets.Text("等待播放", "Ready")).Cast<PixelElement>(), 0, 0);
        root.Attach(c.Library.SetText("Mods\\xuxumusic").Cast<PixelElement>(), 0, 1);
        var transport = new GridPixelElement(5, 1, true).SetSpacing(2, 0);
        transport.Attach(Button(c, MusicAssets.Text("上一首", "Prev"), () => Move(c, -1)), 0, 0);
        transport.Attach(Button(c, MusicAssets.Text("播放", "Play"), () => Play(c)), 1, 0);
        transport.Attach(Button(c, MusicAssets.Text("暂停", "Pause"), () => Pause(c)), 2, 0);
        transport.Attach(Button(c, MusicAssets.Text("下一首", "Next"), () => Move(c, 1)), 3, 0);
        transport.Attach(Button(c, MusicAssets.Text("停止", "Stop"), () => Stop(c)), 4, 0);
        root.Attach(transport.Cast<PixelElement>(), 0, 2);
        var volume = new GridPixelElement(3, 1, true).SetSpacing(3, 0);
        volume.Attach(Button(c, "-", () => ChangeVolume(c, -10)), 0, 0);
        volume.Attach(c.Volume.Cast<PixelElement>(), 1, 0);
        volume.Attach(Button(c, "+", () => ChangeVolume(c, 10)), 2, 0);
        root.Attach(volume.Cast<PixelElement>(), 0, 3);
        root.Attach(Button(c, MusicAssets.Text("刷新歌曲列表", "Refresh songs"), () => Refresh(c), 240), 0, 4);
        c.Window.SetTitle(MusicAssets.Text("xuxu的MP3", "xuxu's MP3"));
        c.Window.Attach(root.Cast<PixelElement>());
        c.Window.openCallback = (Il2CppSystem.Action)(() => Refresh(c));
        item.SetContentWindow(c.Window);
        Windows[item.Pointer.ToInt64()] = c;
        UpdateVolume(c);
    }

    private static PixelElement Button(Controls controls, string label, Action action, int width = 48)
    {
        var button = new ButtonElement(label, width, -1,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() =>
        {
            try { action(); }
            catch (Exception ex) { Core.Log?.Warning("MP3 control failed: " + ex); }
        })));
        controls.Buttons.Add(button);
        CenteredButtons.Add(button.Pointer.ToInt64());
        CenterButton(button);
        return button.Cast<PixelElement>();
    }

    internal static void CenterButton(ButtonElement button)
    {
        if (button == null || !CenteredButtons.Contains(button.Pointer.ToInt64())) return;
        var node = button.textNode;
        if (node?.rectTransform == null || node.text == null) return;
        // Native Validate already gives this text rectangle the button's width
        // minus its border, but ALSO offsets it by half the label width gap.
        // Keep the full rectangle and remove that second centering offset.
        node.rectTransform.anchoredPosition = new Vector2(1f, -1f);
        node.text.alignment = TextAlignmentOptions.Center;
    }

    private static string FileName(Controls c) => c.Item.IsTag(FileTag) ? c.Item.GetTagReadonly(FileTag).valueString : "";
    private static int Volume(Controls c) => Math.Clamp(c.Item.IsTag(VolumeTag)
        ? c.Item.GetTagReadonly(VolumeTag).valueInt : 60, 0, 100);

    private static void Refresh(Controls c)
    {
        if (!IsOwned(c.Item)) return;
        try
        {
            var selected = FileName(c);
            c.Songs = LocalMusicLibrary.Scan(MusicFolder);
            c.Index = LocalMusicLibrary.IndexOf(c.Songs, selected);
            if (_active == c && c.Songs.Count > 0
                && !string.Equals(c.Songs[c.Index].FileName, selected, StringComparison.OrdinalIgnoreCase)) Stop(c);
            c.Library.SetText(MusicAssets.Text("共 " + c.Songs.Count + " 首 · Mods\\xuxumusic",
                c.Songs.Count + " songs · Mods\\xuxumusic"));
            if (c.Songs.Count == 0)
            {
                Stop(c);
                c.Status.SetText(MusicAssets.Text("请将MP3文件放入 Mods\\xuxumusic", "Put MP3 files in Mods\\xuxumusic"));
            }
            else if (_active != c) c.Status.SetText(Short(c.Songs[c.Index].Name));
            UpdateVolume(c);
        }
        catch (Exception ex)
        {
            c.Status.SetText(MusicAssets.Text("无法读取音乐文件夹", "Cannot read music folder"));
            Core.Log?.Warning("MP3 scan failed: " + ex.Message);
        }
    }

    internal static void Open(GameItem item)
    {
        if (!IsOwned(item)) return;
        EnsureLoaded(item);
        var c = Windows[item.Pointer.ToInt64()];
        Refresh(c);
        c.Window.Show().Center();
    }

    private static void Play(Controls c)
    {
        if (!IsOwned(c.Item)) return;
        if (_active == c && _paused && OwnsAudio())
        {
            _paused = false;
            AudioCommand(() => AudioManager.Instance.UnpauseMusic());
            c.Status.SetText(MusicAssets.Text("播放：", "Playing: ") + Short(_loadedSong.Name));
            return;
        }
        if (_active == c && _loading) return;
        Refresh(c);
        if (c.Songs.Count > 0) Start(c);
    }

    private static void Move(Controls c, int offset)
    {
        if (!IsOwned(c.Item)) return;
        if (c.Songs.Count == 0) Refresh(c);
        if (c.Songs.Count == 0) return;
        c.Index = LocalMusicLibrary.NextIndex(c.Index, offset, c.Songs.Count);
        Start(c);
    }

    private static void Start(Controls c)
    {
        if (!IsOwned(c.Item) || c.Songs.Count == 0) return;
        CancelPending();
        AudioCommand(AudioManager.StopAudioCoroutine);
        if (OwnsAudio()) AudioCommand(() => AudioManager.Instance.StopMusic());
        if (_active != null && _active != c) _active.Status.SetText(MusicAssets.Text("已停止", "Stopped"));
        _active = c;
        _paused = false;
        _loading = true;
        var song = c.Songs[c.Index];
        c.Item.ModifyTag(FileTag, (Il2CppSystem.Action<TagState>)(s => s.valueString = song.FileName), true);
        DesktopMp3Player.SaveSettings();
        c.Status.SetText(MusicAssets.Text("加载：", "Loading: ") + Short(song.Name));
        MelonCoroutines.Start(LoadAndPlay(c, song, _generation));
    }

    private static IEnumerator LoadAndPlay(Controls c, LocalMusicLibrary.Song song, int generation)
    {
        AudioClip loaded = null;
        if (_clip != null && _loadedSong == song) loaded = _clip;
        else
        {
            UnityWebRequest request = null;
            UnityWebRequestAsyncOperation operation = null;
            string error = null;
            try
            {
                request = UnityWebRequestMultimedia.GetAudioClip(new Uri(song.FullPath).AbsoluteUri, AudioType.MPEG);
                request.timeout = 30;
                operation = request.SendWebRequest();
            }
            catch (Exception ex) { error = ex.Message; }
            if (operation == null)
            {
                request?.Dispose();
                LoadFailed(c, generation, error);
                yield break;
            }
            _request = request;
            try
            {
                yield return operation;
                if (generation != _generation) yield break;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try { loaded = DownloadHandlerAudioClip.GetContent(request); }
                    catch (Exception ex) { error = ex.Message; }
                }
                else error = request.error;
            }
            finally
            {
                if (ReferenceEquals(_request, request)) _request = null;
                request.Dispose();
            }
        }
        if (generation != _generation) yield break;
        if (loaded == null || loaded.length <= 0f)
        {
            if (loaded != null && loaded != _clip) UnityEngine.Object.Destroy(loaded);
            LoadFailed(c, generation, "Empty or unsupported MP3");
            yield break;
        }
        var previous = _clip;
        _clip = loaded;
        _loadedSong = song;
        UnityEngine.Object.DontDestroyOnLoad(loaded);
        try
        {
            AudioDirectory.RegisterModClip(song.TrackId, loaded);
            AudioCommand(() =>
            {
                // The native cassette player shares this music channel. Cancel its completion callback.
                AudioManager.StopAudioCoroutine();
                AudioManager.Instance.PlayMusic(song.TrackId);
                ApplyPlayerVolume(Volume(c));
                AudioManager.Instance.audioSourceMusic.loop = false;
                if (_systemPaused) AudioManager.Instance.PauseMusic();
            });
        }
        catch (Exception ex) { LoadFailed(c, generation, ex.Message); yield break; }
        finally { if (previous != null && previous != loaded) UnityEngine.Object.Destroy(previous); }
        _loading = false;
        c.Status.SetText(MusicAssets.Text("播放：", "Playing: ") + Short(song.Name));
        // This coroutine exists only during playback; there is no global frame hook or file watcher.
        while (generation == _generation && _active == c)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (generation != _generation || _active != c) yield break;
            if (!IsOwned(c.Item)) { Stop(c); yield break; }
            if (!OwnsAudio()) { Interrupt(); yield break; }
            if (_paused || _systemPaused) continue;
            var source = AudioManager.Instance.audioSourceMusic;
            // A game-wide audio pause retains a nonzero playhead. Natural completion resets it.
            if (!source.isPlaying && (source.time <= 0f || source.time >= _clip.length - 0.01f))
            {
                Move(c, 1);
                yield break;
            }
        }
    }

    private static void LoadFailed(Controls c, int generation, string error)
    {
        if (generation != _generation) return;
        _loading = false;
        RestoreNativeMusicVolume();
        _active = null;
        c.Status.SetText(MusicAssets.Text("读取失败，请换歌或检查MP3文件", "Load failed; skip or check this MP3"));
        Core.Log?.Warning("MP3 load failed: " + error);
    }

    private static void Pause(Controls c)
    {
        if (_active != c) return;
        if (_loading) { Stop(c); return; }
        if (!OwnsAudio()) return;
        _paused = true;
        AudioCommand(() => AudioManager.Instance.PauseMusic());
        c.Status.SetText(MusicAssets.Text("已暂停：", "Paused: ") + Short(_loadedSong.Name));
    }

    private static void Stop(Controls c)
    {
        if (_active != c) return;
        CancelPending();
        if (OwnsAudio()) AudioCommand(() => AudioManager.Instance.StopMusic());
        RestoreNativeMusicVolume();
        _active = null;
        _paused = false;
        _loading = false;
        if (c.Window.handler != null && c.Status.handler != null)
            c.Status.SetText(MusicAssets.Text("已停止", "Stopped"));
    }

    private static void ChangeVolume(Controls c, int change)
    {
        if (!IsOwned(c.Item)) return;
        var volume = Math.Clamp(Volume(c) + change, 0, 100);
        c.Item.ModifyTag(VolumeTag, (Il2CppSystem.Action<TagState>)(s => s.valueInt = volume), true);
        DesktopMp3Player.SaveSettings();
        if (_active == c && OwnsAudio()) ApplyPlayerVolume(volume);
        UpdateVolume(c);
    }

    private static void UpdateVolume(Controls c) => c.Volume.SetText(MusicAssets.Text("音量 ", "Volume ") + Volume(c) + "%");
    private static void ApplyPlayerVolume(int volume)
    {
        var source = AudioManager.Instance?.audioSourceMusic;
        if (source == null) return;
        // Save once per takeover, not on every track or volume change.
        if (!_musicVolumeOverridden) _nativeMusicVolume = source.volume;
        _musicVolumeOverridden = true;
        source.volume = Math.Clamp(volume, 0, 100) / 100f;
    }

    private static void RestoreNativeMusicVolume()
    {
        if (!_musicVolumeOverridden) return;
        _musicVolumeOverridden = false;
        try
        {
            // Use the current preference, including changes made while the
            // MP3 was playing. Fall back to the original value during teardown.
            var options = EmporiumOption.Instance;
            var source = AudioManager.Instance?.audioSourceMusic;
            if (source != null)
                source.volume = options != null
                    ? Math.Clamp(options.musicVolume, 0f, 1f) : _nativeMusicVolume;
        }
        catch (Exception ex) { Core.Log?.Warning("MP3 music volume restore failed: " + ex.Message); }
    }

    private static string Short(string name) => name.Length > 26 ? name.Substring(0, 25) + "…" : name;
    private static bool OwnsAudio()
    {
        var manager = AudioManager.Instance;
        return _clip != null && manager != null && manager.audioSourceMusic?.clip?.Pointer == _clip.Pointer;
    }
    private static void CancelPending()
    {
        unchecked { _generation++; }
        try { _request?.Abort(); } catch { }
        _request = null;
    }
    private static void AudioCommand(Action action)
    {
        _ownAudioCall = true;
        try { action(); }
        finally { _ownAudioCall = false; }
    }

    internal static void Interrupt()
    {
        if (_ownAudioCall || _active == null) return;
        var c = _active;
        CancelPending();
        RestoreNativeMusicVolume();
        _active = null;
        _paused = false;
        _loading = false;
        c.Status.SetText(MusicAssets.Text("已停止", "Stopped"));
    }

    internal static void ExternalPause(bool paused)
    {
        if (_ownAudioCall || _active == null || !OwnsAudio()) return;
        _paused = paused;
        _active.Status.SetText(MusicAssets.Text(paused ? "已暂停：" : "播放：", paused ? "Paused: " : "Playing: ")
            + Short(_loadedSong.Name));
    }

    internal static void Reset()
    {
        if (_active != null) Stop(_active);
        CancelPending();
        foreach (var c in Windows.Values) DisposeWindow(c.Window);
        Windows.Clear();
        CenteredButtons.Clear();
        _systemPaused = false;
        DesktopMp3Player.Release();
    }

    internal static void Remove(GameItem item)
    {
        if (!IsPlayer(item) || !Windows.Remove(item.Pointer.ToInt64(), out var c)) return;
        Stop(c);
        foreach (var button in c.Buttons) CenteredButtons.Remove(button.Pointer.ToInt64());
        DisposeWindow(c.Window);
    }

    internal static void DisposeWindow(PixelWindow window)
    {
        if (window?.handler == null) return;
        var root = window.handler.gameObject;
        if (root == null) return;
        // Hide uses native render services. They can already be gone when
        // EmporiumEntry.OnDestroy is invoked during application shutdown.
        if (RenderHandler.current != null && window.inner != null)
        {
            try { window.Hide(); }
            catch (Exception ex) { Core.Log?.Warning("MP3 window cleanup: " + ex.Message); }
        }
        UnityEngine.Object.Destroy(root);
    }

    internal static void SystemPause(bool paused)
    {
        _systemPaused = paused;
        if (!paused && _paused && OwnsAudio()) AudioCommand(() => AudioManager.Instance.PauseMusic());
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.ActivateSlotItem))]
internal static class Mp3ActivatePatch
{
    private static bool Prefix(GameItem __instance)
    {
        if (!LocalMp3Player.IsPlayer(__instance)) return true;
        try { LocalMp3Player.Open(__instance); }
        catch (Exception ex) { Core.Log?.Warning("MP3 window failed: " + ex); }
        return false;
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.CanActivateSlotItem))]
internal static class Mp3CanActivatePatch
{
    private static void Postfix(GameItem __instance, ref bool __result)
    { if (LocalMp3Player.IsPlayer(__instance)) __result = LocalMp3Player.IsOwned(__instance); }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.MayActivateSlotItem))]
internal static class Mp3MayActivatePatch
{
    private static void Postfix(GameItem __instance, ref bool __result)
    { if (LocalMp3Player.IsPlayer(__instance)) __result = LocalMp3Player.IsOwned(__instance); }
}

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlayMusic))]
internal static class Mp3ForeignPlayPatch { private static void Prefix() => LocalMp3Player.Interrupt(); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.StopMusic))]
internal static class Mp3ForeignStopPatch { private static void Prefix() => LocalMp3Player.Interrupt(); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlayAndCallback))]
internal static class Mp3ForeignCallbackPatch { private static void Prefix() => LocalMp3Player.Interrupt(); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PauseMusic))]
internal static class Mp3ExternalPausePatch { private static void Prefix() => LocalMp3Player.ExternalPause(true); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.UnpauseMusic))]
internal static class Mp3ExternalResumePatch { private static void Postfix() => LocalMp3Player.ExternalPause(false); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PauseAllSource))]
internal static class Mp3SystemPausePatch { private static void Prefix() => LocalMp3Player.SystemPause(true); }

[HarmonyPatch(typeof(AudioManager), nameof(AudioManager.ResumeAllSource))]
internal static class Mp3SystemResumePatch { private static void Postfix() => LocalMp3Player.SystemPause(false); }

[HarmonyPatch(typeof(StoreReputation), nameof(StoreReputation.InitReputation))]
internal static class Mp3NewRunPatch
{
    private static void Prefix() => LocalMp3Player.Reset();
    private static void Postfix() => DesktopMp3Player.Schedule(EmporiumEntry.Instance);
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.LoadGame))]
internal static class Mp3LoadResetPatch
{
    private static void Prefix()
    {
        Mp3LoadState.Loading = true;
        LocalMp3Player.Reset();
    }
    private static Exception Finalizer(Exception __exception)
    {
        Mp3LoadState.Loading = false;
        if (__exception == null) DesktopMp3Player.Schedule(EmporiumEntry.Instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.Destroy))]
internal static class Mp3DestroyedPatch
{
    private static void Postfix(GameItemElement __instance)
    {
        try { LocalMp3Player.Remove(__instance); }
        catch (Exception ex) { Core.Log?.Warning("MP3 cleanup failed: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(ButtonElement), nameof(ButtonElement.Validate))]
internal static class Mp3ButtonCenterPatch
{
    private static void Postfix(ButtonElement __instance) => LocalMp3Player.CenterButton(__instance);
}
