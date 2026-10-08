using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class BarrelSprite
{
    private static readonly Dictionary<string, Sprite> Cache = new();

    internal static Sprite Get(string key)
    {
        if (Cache.TryGetValue(key, out var cached) && cached != null && cached) return cached;
        string file;
        int cells;
        switch (key)
        {
            case "mp3":
                file = "mp3.png";
                cells = 3;
                break;
            case "barrel_idle":
            case "barrel_active":
                file = "xuxu酿造桶.png";
                cells = 3;
                break;
            case "concentrator":
                file = "原浆浓缩器.png";
                cells = 3;
                break;
            case "liunian":
                file = "流年.png";
                cells = 3;
                break;
            default:
                if (!key.StartsWith("pulp_", StringComparison.Ordinal)) return null;
                file = key.Substring(5) + "原浆.png";
                cells = 2;
                break;
        }

        using var stream = typeof(BarrelSprite).Assembly.GetManifestResourceStream(
            "BrewingExpansion.Textures." + file);
        if (stream == null) return null;
        byte[] bytes;
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            bytes = memory.ToArray();
        }
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes)) return null;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        UnityEngine.Object.DontDestroyOnLoad(texture);
        var ppu = texture.width / (BrewingExpansion.BrewingExpansionMod.WORLD_UNITS_PER_CELL * cells);
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(.5f, .5f), ppu);
        sprite.name = key;
        UnityEngine.Object.DontDestroyOnLoad(sprite);
        Cache[key] = sprite;
        return sprite;
    }
}

[HarmonyPatch(typeof(RenderHandler), nameof(RenderHandler.LoadFromAtlas), typeof(string), typeof(string))]
internal static class BarrelSpritePatch
{
    private static bool Prefix(string __0, string __1, ref Sprite __result)
    {
        if (__0 != BarrelItem.SpriteAtlas) return true;
        __result = BarrelSprite.Get(__1);
        return false;
    }
}
