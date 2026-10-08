using Il2Cpp;
using UnityEngine;

namespace XuxuMusic;

// Reuse the prerequisite's locale, sprite atlas and ownership helpers.
internal static class MusicAssets
{
    internal const string SpriteAtlas = BrewingExpansion.MusicSupport.SpriteAtlas;
    internal static Sprite GetSprite() => BrewingExpansion.MusicSupport.GetMp3Sprite();
    internal static string Text(string zh, string en) => BrewingExpansion.MusicSupport.Text(zh, en);
    internal static bool IsOwned(GameItem item) => BrewingExpansion.MusicSupport.IsPlayerOwned(item);
}