using System;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class XuxuBatteryVisuals
{
    // Sprite lookup is read-only here: calling SetSprite while the charger is
    // updating it can re-enter the native update routine.
    internal static bool TryGetKey(GameItem item, out string key)
    {
        key = null;
        var keys = Keys(item?.identifier);
        if (keys == null) return false;
        var energy = item.GetTagReadonly("power_source_item_energy")?.valueInt ?? 0;
        var maximum = item.GetTagReadonly("power_source_item_max_energy")?.valueInt ?? 0;
        if (maximum <= 0) maximum = 1;
        var ratio = Math.Clamp((double)energy / maximum, 0, 1);
        var index = ratio >= 0.99 ? 0
            : ratio >= 0.8 ? 1
            : ratio >= 0.6 ? 2
            : ratio >= 0.4 ? 3
            : ratio >= 0.2 ? 4 : 5;
        key = keys[index];
        return true;
    }

    private static string[] Keys(string id) => id switch
    {
        BrewingExpansion.BrewingExpansionMod.XuxuBatteryId => Basic,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId => Middle,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId => Advanced,
        _ => null
    };

    private static readonly string[] Basic =
    {
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey,
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey4,
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey3,
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey2,
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey1,
        BrewingExpansion.BrewingExpansionMod.XuxuBatterySpriteKey0
    };
    private static readonly string[] Middle =
    {
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey4,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey3,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey2,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey1,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatterySpriteKey0
    };
    private static readonly string[] Advanced =
    {
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey4,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey3,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey2,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey1,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatterySpriteKey0
    };
}
