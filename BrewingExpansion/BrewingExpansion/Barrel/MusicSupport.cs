using Il2Cpp;
using UnityEngine;

namespace BrewingExpansion;

// Shared services for XuxuMusic.dll. Player behavior and its Harmony patches
// live only in the separate music mod; this prerequisite owns shared assets.
public static class MusicSupport
{
    public const int ApiVersion = 1;
    public const string SpriteAtlas = "XIAOWO/BrewingBarrel";
    public static bool Loading => XIAOWOBrewingBarrel.BarrelLoadPatch.Loading;
    public static void EnsureAssetsReady() => BrewingExpansionMod.EnsureSpriteAssetsReady();
    public static Sprite GetMp3Sprite() => XIAOWOBrewingBarrel.BarrelSprite.Get("mp3");
    public static string Text(string zh, string en) => Lang.T(zh, en);
    public static bool IsPlayerOwned(GameItem item) => BrewingExpansionMod.IsOwnedByPlayerPublic(item);
}
