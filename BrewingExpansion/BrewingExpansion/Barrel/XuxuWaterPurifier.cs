using System;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class XuxuWaterPurifier
{
    internal const string BasicId = "be_xuxu_water_purifier_basic";
    internal const string MiddleId = "be_xuxu_water_purifier_middle";
    internal const string AdvancedId = "be_xuxu_water_purifier_advanced";
    private const string VanillaPurifierId = "portable_water_purifier";

    internal static readonly string[] Ids = { BasicId, MiddleId, AdvancedId };
    internal static readonly string[] SpriteKeys =
    {
        "be_xuxu_water_purifier_basic",
        "be_xuxu_water_purifier_middle",
        "be_xuxu_water_purifier_advanced",
    };
    internal static readonly string[] PngNames =
    {
        "xuxu初级净水器", "xuxu中级净水器", "xuxu高级净水器",
    };
    internal static readonly int[] MaxDurabilities = { 500, 600, 1000 };
    internal static readonly int[] DailyRestores = { 20, 30, 100 };
    internal static readonly int[] Prices = { 600, 900, 2000 };

    private static readonly string[] PurifierLoreZh =
{
        "xuxu 做的第一件净水器。\n\n" +
        "不漂亮，但诚实。\n\n" +
        "它把能拦住的，全拦住了。",

        "xuxu 的第二件。\n\n" +
        "他不再满足于拦住看得见的。\n\n" +
        "他开始对付那些看不见的——那些早就被当成水的一部分、没人觉得是杂质的东西。",

        "xuxu 的最后一件。\n\n" +
        "他不再想拦住什么。\n\n" +
        "他只让水流过去，然后离开——干净得像是从来没有进来过。",
    };

    private static readonly string[] PurifierLoreEn =
    {
        "xuxu's first water purifier.\n\n" +
        "Not pretty. Honest.\n\n" +
        "It kept out everything it could.",

        "xuxu's second.\n\n" +
        "He was no longer content to hold back only what the eye could see.\n\n" +
        "He began to go after the unseen — the things that had been taken for water itself, that no one thought to call impure.",

        "xuxu's last.\n\n" +
        "He no longer wanted to hold anything back.\n\n" +
        "He simply let the water pass through, and leave — as clean as if it had never entered at all.",
    };
    internal static GameItem CreateBasic() => Create(0);
    internal static GameItem CreateMiddle() => Create(1);
    internal static GameItem CreateAdvanced() => Create(2);

    private static GameItem Create(int index)
    {
        // The portable purifier owns the native item-on-container action; water_filter is an insertable cartridge.
        var item = DirectoryMaster.Item(VanillaPurifierId, false)
            ?? throw new InvalidOperationException("Could not create the native portable water purifier.");
        item.identifier = Ids[index];
        item.SetName(BarrelItem.Text(PngNames[index],
            index == 0 ? "xuxu Basic Water Purifier" : index == 1
                ? "xuxu Intermediate Water Purifier" : "xuxu Advanced Water Purifier"));
        item.shortDescription = BarrelItem.Text(
    PurifierLoreZh[index] + "\n\n——对水容器使用。每净化 100 毫升水消耗 1 点耐久。上限 "
        + MaxDurabilities[index] + "，每日恢复 " + DailyRestores[index] + " 点。",
    PurifierLoreEn[index] + "\n\n— Use on a water container. Every 100 ml purified costs 1 durability. Max "
        + MaxDurabilities[index] + "; restores " + DailyRestores[index] + " daily.");
        item.longDescription = item.shortDescription;
        item.flavorText = string.Empty;
        item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
        item.modifiedShape = item.shape.Clone();
        DurabilityHelper.SetDurability(item, MaxDurabilities[index], MaxDurabilities[index], MaxDurabilities[index]);
        item.SetValue(Prices[index]);
        item.SetSprite("", SpriteKeys[index]);
        item.SetSpriteQuick("", SpriteKeys[index]);
        item.onCycleEndSlotItemFunc = null;
        item.ValidateShapeState();
        return item;
    }

    internal static void EnsureLoaded(GameItem item)
    {
        if (item == null || Array.IndexOf(Ids, item.identifier) < 0) return;
        var wasCartridge = item.IsTag("WATER_FILTER");
        if (wasCartridge) item.DisableTag("WATER_FILTER", false);
        if (item.IsTag("FILTER_SIZE_TAG")) item.DisableTag("FILTER_SIZE_TAG", false);
        item.onCycleEndSlotItemFunc = null;
        if (!wasCartridge && item.onThisTargetItemFunc != null) return;

        // Existing saves may contain items built from the old cartridge template.
        // Restore only this mod's drag-to-container callbacks from the native purifier.
        var native = DirectoryMaster.Item(VanillaPurifierId, false);
        if (native == null) return;
        item.maySelectSlotItemFunc = native.maySelectSlotItemFunc;
        item.canSelectSlotItemFunc = native.canSelectSlotItemFunc;
        item.mayThisTargetItemFunc = native.mayThisTargetItemFunc;
        item.canThisTargetItemFunc = native.canThisTargetItemFunc;
        item.onThisTargetItemFunc = native.onThisTargetItemFunc;
        item.mayItemTargetItemFunc = native.mayItemTargetItemFunc;
        item.canItemTargetItemFunc = native.canItemTargetItemFunc;
        item.onItemTargetItemFunc = native.onItemTargetItemFunc;
        item.mayItemTargetThisFunc = native.mayItemTargetThisFunc;
        item.canItemTargetThisFunc = native.canItemTargetThisFunc;
        item.onItemTargetThisFunc = native.onItemTargetThisFunc;
        if (native.itemTypes != null)
        {
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            for (var i = 0; i < native.itemTypes.Count; i++) types.Add(native.itemTypes[i]);
            item.itemTypes = types;
        }
    }

    internal static bool RestoreAtEndNight(GameItem item)
    {
        if (item == null) return false;
        var index = Array.IndexOf(Ids, item.identifier);
        if (index < 0) return false;
        var current = DurabilityHelper.GetRealDurability(item);
        var maximum = DurabilityHelper.GetMaxDurability(item);
        if (current >= maximum) return false;
        DurabilityHelper.ChangeDurability(item, Math.Min(DailyRestores[index], maximum - current));
        return true;
    }
}
