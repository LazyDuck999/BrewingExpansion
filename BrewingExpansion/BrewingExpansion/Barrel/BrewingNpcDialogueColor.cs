using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class BrewingNpcDialogueColor
{
    private const float LightenAmount = 0.30f;
    private static readonly Dictionary<long, Sprite> LightSprites = new();

    internal static bool IsBrewingVisitor(StoreClient client) =>
        client?.identifier == BrewingExpansion.BrewingExpansionMod.WeeklyTraderId
        || client?.identifier == BrewingExpansion.BrewingExpansionMod.SeedTrader47Id;

    // Native DialogBubbleList has both "blue" (name) and "bluep" (speech).
    // SetClientBubbleColor selects that pair from the client's bubbleColor.
    internal static void Apply(StoreClient client)
    {
        if (IsBrewingVisitor(client))
            client.bubbleColor = "blue";
    }

    internal static void ApplyLighterBlue(DialogUIManager ui)
    {
        if (!IsBrewingVisitor(PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint())) return;
        var name = ui?.clientNameBubbleImage;
        var speech = ui?.clientBubble?.backgroundImage;
        if (name?.sprite != null) name.sprite = LightSprite(name.sprite);
        if (speech?.sprite != null) speech.sprite = LightSprite(speech.sprite);
    }

    private static Sprite LightSprite(Sprite source)
    {
        var key = source.Pointer.ToInt64();
        if (LightSprites.TryGetValue(key, out var cached) && cached != null) return cached;
        // The native bubble sheet is readable. Copy only this sprite's pixels,
        // retaining transparency, border and pivot; never edit the shared sheet.
        var rect = source.textureRect;
        var width = (int)rect.width;
        var height = (int)rect.height;
        var pixels = source.texture.GetPixels((int)rect.x, (int)rect.y, width, height);
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            if (color.a == 0 || Mathf.Max(color.r, Mathf.Max(color.g, color.b)) < 0.15f) continue;
            color.r += (1f - color.r) * LightenAmount;
            color.g += (1f - color.g) * LightenAmount;
            color.b += (1f - color.b) * LightenAmount;
            pixels[i] = color;
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "BE_LightBlueDialogue",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot,
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
        UnityEngine.Object.DontDestroyOnLoad(texture);
        UnityEngine.Object.DontDestroyOnLoad(sprite);
        LightSprites[key] = sprite;
        return sprite;
    }
}

[HarmonyPatch(typeof(DialogUIManager), nameof(DialogUIManager.SetClientBubbleColor))]
internal static class BrewingNpcDialogueColorPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        try
        {
            // Saved visitor blueprints may retain the old template's color.
            // Apply at the native display boundary, including later reactions.
            BrewingNpcDialogueColor.Apply(PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint());
        }
        catch (Exception ex) { Core.Log?.Warning("Brewing NPC dialogue color: " + ex.Message); }
    }

    [HarmonyPostfix]
    private static void Postfix(DialogUIManager __instance)
    {
        try { BrewingNpcDialogueColor.ApplyLighterBlue(__instance); }
        catch (Exception ex) { Core.Log?.Warning("Brewing NPC light-blue dialogue: " + ex.Message); }
    }
}
