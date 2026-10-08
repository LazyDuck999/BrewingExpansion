using System;
using Il2Cpp;

namespace XIAOWOBrewingBarrel;

internal static class MachineGuides
{
    internal const int Price = 10;
    internal const string BarrelId = "be_barrel_usage_guide";
    internal const string ConcentratorId = "be_concentrator_usage_guide";
    internal const string IncubatorId = "be_incubator_usage_guide";

    internal static readonly string[] Ids = { BarrelId, ConcentratorId, IncubatorId };

    internal static bool Contains(string id) =>
        id == BarrelId || id == ConcentratorId || id == IncubatorId;

    private static string Name(int index) => index switch
    {
        0 => BrewingExpansion.Lang.T("酿造桶使用说明", "Brewing Barrel Guide"),
        1 => BrewingExpansion.Lang.T("原浆浓缩器使用说明", "Pulp Concentrator Guide"),
        2 => BrewingExpansion.Lang.T("种子孵化器使用说明", "Seed Incubator Guide"),
        _ => ""
    };

    private static string Description(int index) => index switch
    {
        0 => BrewingExpansion.Lang.T(
            "【酿造桶使用说明】\n\n" +
            "四只空瓶，两份原浆，候一夜，果香便有了归处。\n\n" +
            "原浆区放入两份原浆，每份须由四个同种果实浓缩而成；空瓶区放四个空酒瓶。" +
            "酵母区放四份普通酵母，或一只可重复使用的酵母反应器。" +
            "向桶中注满 3000 ml 水，清空成品区，按下「开始发酵」。\n\n" +
            "酒瓶留在桶内，次日酿成的酒会进入成品区；若提前取走发酵中的酒瓶，酒随瓶继续发酵，桶即可再酿新的一批。",
            "【Brewing Barrel Guide】\n\n" +
            "Four empty bottles, two pulps, one night: a place for fruit to become wine.\n\n" +
            "Place two pulps in the pulp area, each concentrated from four fruits of one kind. " +
            "Put four empty wine bottles in the bottle area. Add four ordinary yeasts, or one reusable yeast reactor, to the yeast area. " +
            "Fill the barrel with 3000 ml of water, clear the output area, then press Start Fermentation.\n\n" +
            "Bottles left inside move to the output area when finished the next day. " +
            "Take fermenting bottles out early and they continue fermenting in your bag; the barrel is ready for another batch."),
        1 => BrewingExpansion.Lang.T(
            "【原浆浓缩器使用说明】\n\n" +
            "四枚同味的果实，收拢成一份未启封的香气。\n\n" +
            "在电池区装入电池，果实区放四个同种果实，并为原浆区留出 2×2 格。" +
            "按下「浓缩」，消耗 8 kJ 电量，产出一份原浆。" +
            "取出原浆，交给酿造桶；它是酿酒原料，不必打开。",
            "【Pulp Concentrator Guide】\n\n" +
            "Four fruits of one kind, gathered into a single sealed fragrance.\n\n" +
            "Install a battery, place four matching fruits in the fruit area, and leave a 2×2 space in the pulp area. " +
            "Press Concentrate to spend 8 kJ and produce one pulp. " +
            "Take the pulp to the brewing barrel. It is an ingredient, not a container to open."),
        2 => BrewingExpansion.Lang.T(
            "【种子孵化器使用说明】\n\n" +
            "把种子交给黑夜，清晨便能收下果实。\n\n" +
            "电源装入电池，输入区放入种子。有种子的夜晚消耗 12 kJ，空仓不耗电；每颗种子孵成一个对应果实，优先落入输出区。" +
            "每晚可孵化的上限为两颗；初级、中级、高级证书分别提高到五、七、十八颗。" +
            "电量不足则当晚停工；输出区放不下的果实会送到店内台面。",
            "【Seed Incubator Guide】\n\n" +
            "Give the seeds to the night; gather their fruit at dawn.\n\n" +
            "Install a battery in the power slot and place seeds in the input area. A night with seeds costs 12 kJ; an empty incubator uses no power. " +
            "each seed becomes its matching fruit, placed in the output area first. " +
            "The nightly limit is two seeds, raised to five, seven, or eighteen by the basic, middle, or advanced certificate. " +
            "Without enough power, no seeds hatch that night. Fruit that cannot fit in the output area goes to the shop table."),
        _ => ""
    };

    internal static GameItem Create(int index)
    {
        if ((uint)index >= Ids.Length) return null;
        try
        {
            var item = ItemDirectory.CreateEmptyItem(Ids[index]);
            if (item == null) return null;
            item.SetName(Name(index));
            item.SetValue(Price);
            if (!SetVanillaDocumentSprite(item, index))
            {
                item.Destroy();
                return null;
            }
            item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            types.Add("DOCUMENT");
            item.itemTypes = types;
            item.shortDescription = Description(index);
            item.longDescription = Description(index);
            return item;
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("Machine guide creation failed: " + Ids[index] + ": " + ex);
            return null;
        }
    }

    private static bool SetVanillaDocumentSprite(GameItem item, int index)
    {
        GameItem example = null;
        try
        {
            var directory = BrewingExpansion.PatchMiscDirInit.Instance;
            if (directory != null)
                try
                {
                    example = index switch
                    {
                        0 => directory.MentorContract(),
                        1 => directory.AugIdGuide(),
                        2 => directory.BusinessPermit(),
                        _ => null
                    };
                }
                catch (Exception ex) { Core.Log?.Warning("Native guide art unavailable: " + ex.Message); }
            if (example != null && string.IsNullOrEmpty(example.spritePath))
            {
                try { example.Destroy(); } catch { }
                example = null;
            }
            example ??= MiscItemDirectory.Note();
            if (example == null || string.IsNullOrEmpty(example.spritePath)) return false;
            item.SetSprite(example.spriteAtlasPath ?? "", example.spritePath);
            return true;
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("Could not use native document art for " + Ids[index] + ": " + ex);
            return false;
        }
        finally
        {
            try { example?.Destroy(); } catch { }
        }
    }
}
