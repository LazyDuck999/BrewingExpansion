using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace BrewingExpansion;

internal static class BrewingTraits
{
    internal const string MusicId = "be_xuxu_music";
    internal const string WineDevotionId = "be_wine_devotion";
    internal const string DrunkId = "be_dead_drunk";
    internal const string SeedAversionId = "be_seed_aversion";

    private static bool _musicGranted;
    private static int _lastLossDay = int.MinValue;
    private static int _wineCountFrame = int.MinValue;
    private static int _wineCountForFrame;
    private static readonly Dictionary<string, Sprite> Icons = new();

    internal static bool IsOurId(string id) => id == MusicId || id == WineDevotionId
        || id == DrunkId || id == SeedAversionId;

    internal static string Name(string id) => id switch
    {
        MusicId => Lang.T("xuxu音乐", "xuxu Music"),
        WineDevotionId => Lang.T("一心为酒", "Devoted to Wine"),
        DrunkId => Lang.T("酩酊大醉", "Dead Drunk"),
        SeedAversionId => Lang.T("厌恶种子", "Seed Aversion"),
        _ => id ?? ""
    };

    internal static string Description(string id) => id switch
    {
        MusicId => Lang.T("开局获得一个xuxu磁带捆绑包。", "Start with an xuxu Cassette Bundle."),
        WineDevotionId => Lang.T(
            "出售烟酒类物品时，价格提高20%；每拥有一瓶成品酒，再提高2%，这部分最多提高80%（合计最多提高100%）。出售其它物品时，价格降低25%。",
            "Alcohol and tobacco sell for 20% more. Each finished bottle of wine you own adds another 2%, up to 80% extra (100% total). Other items sell for 25% less."),
        DrunkId => Lang.T("交易烟酒类物品时价格提高50%，但每晚随机失去背包里的两件物品。",
            "Alcohol and tobacco trade prices rise by 50%, but two random backpack items vanish each night."),
        SeedAversionId => Lang.T("四十七每次上架的种子减少3颗。", "Forty-Seven stocks three fewer seeds each visit."),
        _ => ""
    };

    internal static void Register()
    {
        var perks = StartingPerkList.Perks;
        if (perks == null) return;
        Add(perks, MusicId, 1, StartingPerk.StartingPerkType.POSITIVE);
        Add(perks, WineDevotionId, 2, StartingPerk.StartingPerkType.POSITIVE);
        Add(perks, DrunkId, -2, StartingPerk.StartingPerkType.NEGATIVE);
        Add(perks, SeedAversionId, -2, StartingPerk.StartingPerkType.NEGATIVE);
    }

    private static void Add(Il2CppSystem.Collections.Generic.List<StartingPerk> perks,
        string id, int cost, StartingPerk.StartingPerkType type)
    {
        for (int i = 0; i < perks.Count; i++)
            if (perks[i] != null && perks[i].id == id) return;
        var perk = new StartingPerk();
        perk.id = id;
        perk.cost = cost;
        perk.type = type;
        perk.rawName = Name(id);
        perk.rawDescription = Description(id);
        perks.Add(perk);
    }

    internal static bool Has(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        try
        {
            var perks = PlayerStore.Instance?.startingPerks;
            for (int i = 0; perks != null && i < perks.Count; i++)
                if (perks[i] != null && perks[i].id == id) return true;
        }
        catch { }
        return false;
    }

    private static bool SelectedForNewGame(string id)
    {
        try
        {
            var perks = NewGameData.Instance?.startingPerks;
            for (int i = 0; perks != null && i < perks.Count; i++)
                if (perks[i] != null && perks[i].id == id) return true;
        }
        catch { }
        return Has(id);
    }

    internal static void ResetStart()
    {
        _musicGranted = false;
        _lastLossDay = int.MinValue;
        _wineCountFrame = int.MinValue;
    }

    internal static void ResetNightLoss()
    {
        _lastLossDay = int.MinValue;
        _wineCountFrame = int.MinValue;
    }

    internal static void GrantMusicBundle()
    {
        if (_musicGranted || !SelectedForNewGame(MusicId)) return;
        var emporium = EmporiumEntry.Instance;
        if (emporium?.invElement == null) return;
        try { BrewingExpansionMod.EnsureSpriteAssetsReady(); }
        catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 磁带贴图加载失败: " + ex.Message); }
        int placed = BrewerStarterPerk.Grant(emporium, BrewingExpansionMod.CassetteBundleId, 1);
        if (placed == 1) _musicGranted = true;
    }

    private static string IconResource(string id) => id switch
    {
        MusicId => "BrewingExpansion.Textures.xuxu音乐.png",
        WineDevotionId => "BrewingExpansion.Textures.一心为酒.png",
        DrunkId => "BrewingExpansion.Textures.酩酊大醉.png",
        SeedAversionId => "BrewingExpansion.Textures.厌恶种子.png",
        _ => null
    };

    internal static Sprite GetIcon(string id)
    {
        if (!IsOurId(id)) return null;
        return LoadIcon(id, IconResource(id), 32f);
    }

    internal static Sprite GetStartIcon() => LoadIcon("be_new_brewer_start_icon",
        "BrewingExpansion.Textures.新人酿酒师开局.png", 100f);

    private static Sprite LoadIcon(string id, string resourceName, float pixelsPerUnit)
    {
        if (Icons.TryGetValue(id, out var cached) && cached != null) return cached;
        try
        {
            using var stream = typeof(BrewingExpansionMod).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, memory.ToArray())) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, false);
            UnityEngine.Object.DontDestroyOnLoad(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = id;
            UnityEngine.Object.DontDestroyOnLoad(sprite);
            Icons[id] = sprite;
            return sprite;
        }
        catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 图标加载失败 " + id + ": " + ex.Message); return null; }
    }

    internal static void RegisterIcons()
    {
        var icons = StartingPerkIconLoader.perkIcons;
        if (icons == null) return;
        foreach (var id in new[] { MusicId, WineDevotionId, DrunkId, SeedAversionId })
        {
            if (icons.ContainsKey(id)) continue;
            var sprite = GetIcon(id);
            if (sprite != null) icons.Add(id, sprite);
        }
    }

    private static int FinishedWineCount()
    {
        int frame = Time.frameCount;
        if (_wineCountFrame == frame) return _wineCountForFrame;
        int count = 0;
        try
        {
            var owned = PlayerStore.Instance?.FindAllItem(true);
            for (int i = 0; owned != null && i < owned.Count && count < 40; i++)
            {
                var item = owned[i];
                if (item == null) continue;
                if (item.identifier == "wine_bottle")
                {
                    if (WineHelper.IsFinishedWine(item)) count++;
                    continue;
                }
                var types = item.itemTypes;
                for (int j = 0; types != null && j < types.Count; j++)
                    if (types[j] == "ALCOHOL") { count++; break; }
            }
        }
        catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 统计成品酒失败: " + ex.Message); }
        _wineCountFrame = frame;
        _wineCountForFrame = count;
        return count;
    }

    internal static long ApplyTradePrice(GameItem item, long price)
    {
        if (item == null || price <= 0) return price;
        bool owned = BrewingExpansionMod.IsOwnedByPlayerPublic(item);
        bool substance = PatchDismissTrigger47.IsNiniTradeItem(item);
        double multiplier = 1;
        if (owned && Has(WineDevotionId))
            multiplier *= substance ? 1.20 + Math.Min(FinishedWineCount(), 40) * 0.02 : 0.75;
        if (substance && Has(DrunkId)) multiplier *= 1.50;
        if (multiplier == 1) return price;
        return (long)Math.Min(int.MaxValue, Math.Max(1, Math.Round(price * multiplier)));
    }

    internal static void LoseBackpackItems()
    {
        if (!Has(DrunkId)) return;
        int day = BrewingExpansionMod.GetCurrentGameDay();
        if (_lastLossDay == day) return;
        var backpack = PlayerStore.Instance?.gridInv?.childItems;
        if (backpack == null) return;
        var candidates = new List<GameItem>();
        var seen = new HashSet<long>();
        for (int i = 0; i < backpack.Count; i++)
        {
            var item = backpack[i];
            if (item == null) continue;
            try
            {
                var children = item.FindAllChildItems();
                if (children == null || children.Count == 0)
                {
                    if (seen.Add(item.Pointer.ToInt64())) candidates.Add(item);
                    continue;
                }
                // Draw individual items inside a filled bag, never destroy the bag and all its contents.
                for (int j = 0; j < children.Count; j++)
                {
                    var child = children[j];
                    if (child == null || !seen.Add(child.Pointer.ToInt64())) continue;
                    var grandChildren = child.FindAllChildItems();
                    if (grandChildren == null || grandChildren.Count == 0) candidates.Add(child);
                }
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 跳过无法检查的背包物品: " + ex.Message); }
        }
        _lastLossDay = day;
        var random = new System.Random();
        int lost = 0;
        while (candidates.Count > 0 && lost < 2)
        {
            int index = random.Next(candidates.Count);
            var item = candidates[index];
            candidates.RemoveAt(index);
            try { item.Destroy(); lost++; }
            catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 丢失物品失败: " + ex.Message); }
        }
        MelonLogger.Msg("[BE-PERK] 酩酊大醉：背包失去 " + lost + " 件物品");
    }
}

[HarmonyPatch(typeof(GameItem), nameof(GameItem.GetNegociatedValue))]
internal static class BrewingTraitTradePricePatch
{
    [HarmonyPrefix]
    private static void Prefix(GameItem __instance) => BrewingExpansionMod.RefreshOurStoragePrice(__instance);

    [HarmonyPostfix]
    private static void Postfix(GameItem __instance, ref long __result)
    {
        try { __result = BrewingTraits.ApplyTradePrice(__instance, __result); }
        catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 交易价格加成失败: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.EndNight))]
internal static class BrewingTraitNightLossPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        try { BrewingTraits.LoseBackpackItems(); }
        catch (Exception ex) { MelonLogger.Warning("[BE-PERK] 夜间物品丢失失败: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.LoadGame))]
internal static class BrewingTraitLoadResetPatch
{
    [HarmonyPrefix]
    private static void Prefix() => BrewingTraits.ResetNightLoss();
}
