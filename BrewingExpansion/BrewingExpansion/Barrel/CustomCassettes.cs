using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace XIAOWOBrewingBarrel;

internal static class CustomCassettes
{
    private const int TapePrice = 100;
    private static readonly string[] BundledMusic =
    {
        "酒吧特调.mp3", "霓虹街道.mp3", "微醺之夜.mp3", "弦间微风.mp3", "酿酒有嘻哈.mp3"
    };
    internal static readonly string[] PngNames =
    {
        "酒吧特调", "霓虹街道", "微醺之夜", "弦间微风", "酿酒有嘻哈"
    };
    internal static readonly string[] SpriteKeys =
    {
        "be_cassette_bar_special", "be_cassette_neon_street",
        "be_cassette_tipsy_night", "be_cassette_string_breeze", "be_cassette_brewing_hiphop"
    };
    private static readonly string[] DescriptionsZh =
    {
        "冰块轻碰杯沿，吧台的灯将夜色调成琥珀。人声渐远，杯底留下一点只属于此刻的甜。",
        "雨后的街面盛满霓虹，行人的影子在光里交错。沿着节拍向前，整座城都在为归途亮灯。",
        "酒意恰好漫过心事，窗外的月色依旧清醒。让夜走得慢些，让没说出口的话在旋律里落座。",
        "指尖掠过琴弦，风从音符的缝隙穿过。它带走喧嚣，留下恰好能听见自己的宁静。",
        "不知道何意味。"
    };
    private static readonly string[] DescriptionsEn =
    {
        "Ice brushes the rim; lamplight stirs the night into amber. As the voices fade, a little sweetness lingers at the bottom of the glass.",
        "Rain-washed streets hold the neon, and shadows cross in its glow. Follow the beat; the whole city lights the way home.",
        "The wine softens old worries while the moon stays clear. Let the night linger, and let unsaid words find their place in the melody.",
        "A fingertip brushes the strings; a breeze slips between the notes. It carries the noise away and leaves a quiet in which you can hear yourself.",
        "No one knows what it means."
    };
    private static readonly List<Tape> Tapes = new();
    private static readonly Dictionary<string, AudioClip> LoadedClips = new();
    private static readonly HashSet<string> LoadingClips = new();
    private static bool _discovered;

    private sealed class Tape
    {
        internal string Path;
        internal string ItemId;
        internal string TrackId;
        internal string Name;
        internal string Sprite;
        internal string CustomSprite;
        internal string DescriptionZh;
        internal string DescriptionEn;
        internal AudioType AudioType;
    }

    internal static IEnumerable<string> ItemIds
    {
        get { Discover(); return Tapes.Select(tape => tape.ItemId); }
    }

    internal static void RegisterItems(MiscItemDirectory directory)
    {
        if (directory == null) return;
        Discover();
        foreach (var tape in Tapes)
        {
            var captured = tape;
            try
            {
                directory.Add(captured.ItemId, (Func<GameItem>)(() => CreateItem(captured)));
                Core.Log?.Msg("CASSETTE: registered " + captured.ItemId + " (" + captured.Name + ")");
            }
            catch (Exception e) { Core.Log?.Warning("CASSETTE: register " + captured.ItemId + ": " + e.Message); }
        }
        LoadMusic();
    }

    private static void Discover()
    {
        if (_discovered) return;
        try
        {
            // Unity's audio loader reads a file URI. Extract the bundled MP3s to a
            // private temp directory so the release only needs the mod DLL.
            var cache = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BrewingExpansionMusic");
            Directory.CreateDirectory(cache);
            foreach (var fileName in BundledMusic)
            {
                var resourceName = "BrewingExpansion.music." + fileName;
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Core.Log?.Warning("CASSETTE: bundled resource missing: " + resourceName);
                    continue;
                }
                var cacheFile = System.IO.Path.Combine(cache, fileName);
                using (var output = File.Create(cacheFile)) stream.CopyTo(output);
                AddTape(cacheFile);
            }
            _discovered = true;
            Core.Log?.Msg("CASSETTE: discovered " + Tapes.Count + " songs");
        }
        catch (Exception e) { Core.Log?.Warning("CASSETTE: scan failed: " + e); }
    }

    private static void AddTape(string path)
    {
        var audioType = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".ogg" => AudioType.OGGVORBIS,
            ".wav" => AudioType.WAV,
            ".mp3" => AudioType.MPEG,
            _ => AudioType.UNKNOWN
        };
        if (audioType == AudioType.UNKNOWN) return;
        var name = System.IO.Path.GetFileNameWithoutExtension(path).Trim();
        if (name.Length == 0) return;
        var key = System.IO.Path.GetFileName(path).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .Substring(0, 16).ToLowerInvariant();
        Tapes.Add(new Tape
        {
            Path = path,
            ItemId = "be_music_cassette_" + hash,
            TrackId = "be_music_track_" + hash,
            Name = name,
            Sprite = "cassette" + (Tapes.Count % 7 + 1),
            CustomSprite = SpriteKeys[Tapes.Count],
            DescriptionZh = DescriptionsZh[Tapes.Count],
            DescriptionEn = DescriptionsEn[Tapes.Count],
            AudioType = audioType
        });
    }

    private static GameItem CreateItem(Tape tape)
    {
        var item = MiscItemDirectory.MusicCassette(tape.TrackId, tape.Name, tape.Sprite, "xuxu", 0);
        if (item == null) return null;
        item.identifier = tape.ItemId;
        item.SetName(tape.Name);
        item.SetSprite("", tape.CustomSprite);
        item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
        item.modifiedShape = item.shape.Clone();
        item.SetValue(TapePrice);
        var description = Description(tape);
        item.shortDescription = description;
        item.longDescription = description;
        return item;
    }

    private static string Description(Tape tape) => BarrelItem.Text(
        tape.DescriptionZh + "\n\n——来自xuxu的酿酒拓展",
        tape.DescriptionEn + "\n\n— From xuxu's Brewing Expansion");

    internal static bool EnsureLoaded(GameItem item)
    {
        if (item?.identifier?.StartsWith("be_music_cassette_", StringComparison.Ordinal) != true)
            return false;
        Discover();
        var tape = Tapes.FirstOrDefault(candidate => candidate.ItemId == item.identifier);
        if (tape == null) return false;
        if (item.shape == null || item.shape.width != 2 || item.shape.height != 2
            || item.modifiedShape == null || item.modifiedShape.width != 2 || item.modifiedShape.height != 2)
        {
            item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
            item.modifiedShape = item.shape.Clone();
        }
        if (item.spritePath != tape.CustomSprite) item.SetSprite("", tape.CustomSprite);
        var description = Description(tape);
        if (item.shortDescription != description) item.shortDescription = description;
        if (item.longDescription != description) item.longDescription = description;
        return true;
    }

    internal static void LoadMusic()
    {
        Discover();
        foreach (var tape in Tapes)
        {
            if (LoadedClips.TryGetValue(tape.TrackId, out var clip))
            {
                AudioDirectory.RegisterModClip(tape.TrackId, clip);
                continue;
            }
            if (LoadingClips.Add(tape.TrackId)) MelonCoroutines.Start(LoadClip(tape));
        }
    }

    private static IEnumerator LoadClip(Tape tape)
    {
        UnityWebRequest request = null;
        try
        {
            request = UnityWebRequestMultimedia.GetAudioClip(new Uri(tape.Path).AbsoluteUri, tape.AudioType);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Core.Log?.Warning("CASSETTE: could not load " + tape.Name + ": " + request.error);
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                Core.Log?.Warning("CASSETTE: empty audio clip " + tape.Name);
                yield break;
            }
            UnityEngine.Object.DontDestroyOnLoad(clip);
            LoadedClips[tape.TrackId] = clip;
            AudioDirectory.RegisterModClip(tape.TrackId, clip);
            Core.Log?.Msg("CASSETTE: loaded " + tape.Name + " (" + clip.length + " s)");
        }
        finally
        {
            try { request?.Dispose(); } catch { }
            LoadingClips.Remove(tape.TrackId);
        }
    }
}

[HarmonyPatch(typeof(AudioDirectory), "Start")]
internal static class CustomCassettesAudioPatch
{
    private static void Postfix() => CustomCassettes.LoadMusic();
}
