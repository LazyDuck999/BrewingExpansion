using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace XIAOWOBrewingBarrel;

internal static class OuterRingFaction
{
    internal const string Id = "BE_FACTION_OUTER_RING";
    internal const string AssassinId = "be_outer_ring_assassin";
    private const string ZeroBasedSaveMarker = "BrewingExpansion.OuterRingZeroBased.v1";
    private static bool _assassinationPending;
    private static readonly int[] Thresholds = { -100, -80, -60, -40, -20, 20, 40, 60, 80, 100 };
    private static readonly string[] NamesZh = {
        "外环追杀", "商路断绝", "灯牌失效", "货源紧缩", "预算收紧",
        "初获信任", "往来渐密", "稳固盟约", "外环贵客", "四十七的挚友"
    };
    private static readonly string[] NamesEn = {
        "Outer Ring Hit", "Trade Severed", "Sign Disabled", "Restricted Supply", "Tight Budget",
        "First Trust", "Growing Trade", "Firm Alliance", "Honored Guest", "Forty-Seven's Friend"
    };
    private static readonly string[] DescZh = {
        "外环区将派刺客来暗杀你。", "四十七酿酒商店关闭；泥泥预算减少 80%。",
        "酿酒灯牌无法召来四十七；泥泥预算减少 60%。",
        "四十七少带 4 颗种子；泥泥预算减少 40%。",
        "四十七少带 2 颗种子；泥泥预算减少 20%。",
        "四十七多带 2 颗种子；泥泥预算增加 20%。",
        "四十七多带 4 颗种子；泥泥预算增加 40%。",
        "四十七多带 6 颗种子；泥泥预算增加 60%。",
        "四十七多带 8 颗种子；泥泥预算增加 80%。",
        "四十七多带 10 颗种子；泥泥预算增加 100%。"
    };
    private static readonly string[] DescEn = {
        "The Outer Ring sends an assassin after you.", "Forty-Seven's brew shop closes; Nini's budget falls by 80%.",
        "The brewery sign cannot summon Forty-Seven; Nini's budget falls by 60%.",
        "Forty-Seven brings 4 fewer seeds; Nini's budget falls by 40%.",
        "Forty-Seven brings 2 fewer seeds; Nini's budget falls by 20%.",
        "Forty-Seven brings 2 more seeds; Nini's budget rises by 20%.",
        "Forty-Seven brings 4 more seeds; Nini's budget rises by 40%.",
        "Forty-Seven brings 6 more seeds; Nini's budget rises by 60%.",
        "Forty-Seven brings 8 more seeds; Nini's budget rises by 80%.",
        "Forty-Seven brings 10 more seeds; Nini's budget rises by 100%."
    };

    internal static void EnsureRegistered()
    {
        try
        {
            var store = PlayerStore.Instance;
            if (store == null || store.storeReputations == null) return;
            for (int i = 0; i < store.storeReputations.Count; i++)
                if (store.storeReputations[i]?.factionId == Id)
                {
                    var existing = store.storeReputations[i];
                    existing.factionDisplay = BrewingExpansion.Lang.T("外环区", "Outer Ring");
                    if (!HasZeroBasedMarker(store))
                    {
                        // 旧版本从 100 开始；升级时只重置一次，之后保留正常交易所得。
                        existing.ModReputationRaw(-existing.GetReputationExact());
                        MarkZeroBased(store);
                        MelonLogger.Msg("[BE-OUTER] 旧存档外环区声望已一次性重置为 0");
                    }
                    return;
                }

            // 原版声望支持负数，0 即中立，外环区直接使用 -100..100 档位。
            var security = StoreReputation.GetSecFaction();
            var faction = new StoreReputation();
            faction.factionId = Id;
            faction.factionDisplay = BrewingExpansion.Lang.T("外环区", "Outer Ring");
            faction.factionColor1 = security?.factionColor1 ?? "#98454D";
            faction.factionColor2 = security?.factionColor2 ?? "#98454D";
            faction.factionColor3 = security?.factionColor3 ?? "#98454D";
            if (security != null)
            {
                faction.strength = security.strength;
                faction.influence = security.influence;
                faction.actionCooldown = security.actionCooldown;
            }
            for (int i = 0; i < Thresholds.Length; i++)
            {
                var perk = Thresholds[i] < 0 ? StoreReputationPerk.SecWatchlist()
                    : StoreReputationPerk.SecProtection();
                perk.perkId = "BE_OUTER_" + (Thresholds[i] < 0 ? "N" + (-Thresholds[i]) : "P" + Thresholds[i]);
                perk.perkThreshold = Thresholds[i];
                perk.perkType = Thresholds[i] < 0 ? 0 : 1;
                perk.isUnlocked = false;
                perk.firstTimeUnlockActionDone = true;
                faction.perks.Add(perk);
            }
            store.storeReputations.Add(faction);
            MarkZeroBased(store);
            MelonLogger.Msg("[BE-OUTER] 外环区声望已加入存档，初始 0/200");
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 注册声望失败: " + e); }
    }

    private static bool HasZeroBasedMarker(PlayerStore store)
        => store.modData != null && store.modData.ContainsKey(ZeroBasedSaveMarker)
            && store.modData[ZeroBasedSaveMarker] == "1";

    private static void MarkZeroBased(PlayerStore store)
    {
        if (store.modData == null)
            store.modData = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
        store.modData[ZeroBasedSaveMarker] = "1";
    }

    internal static int Reputation
    {
        get
        {
            try
            {
                var rep = StoreReputation.GetStoreReputation(Id);
                return rep == null ? 0 : Math.Clamp(rep.GetReputation(), -200, 200);
            }
            catch { return 0; }
        }
    }

    internal static int Tier
    {
        get
        {
            int reputation = Reputation;
            return reputation < 0 ? -Math.Min(5, (-reputation) / 20) : Math.Min(5, reputation / 20);
        }
    }

    internal static int SeedCountModifier => Tier <= -3 ? 0 : Tier * 2;
    internal static int NiniBudget => (int)Math.Round(BrewingExpansion.BrewingExpansionMod.WeeklyTraderBudget
        * (1.0 + Math.Max(-4, Tier) * 0.2), MidpointRounding.AwayFromZero);
    internal static bool CanSummonFortySeven => Tier > -3;
    internal static bool CanOpenShop => Tier > -4;

    internal static StoreClient CreateAssassin()
    {
        try
        {
            var assassin = StoreClientListMorning.CreateULHitman();
            if (assassin == null) return null;
            assassin.identifier = AssassinId;
            assassin.displayName = BrewingExpansion.Lang.T("外环区刺客", "Outer Ring Assassin");
            assassin.clientFaction = Id;
            return assassin;
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 创建刺客失败: " + e); return null; }
    }

    internal static void QueueAssassin()
    {
        try
        {
            if (Tier > -5) return;
            var store = PlayerStore.Instance;
            var manager = store?.storeClientManager;
            var stack = manager?.clientStack;
            if (stack == null) return;
            if (store.currentClientInstance?.GetClientBlueprint()?.identifier == AssassinId) return;
            for (int i = 0; i < stack.Count; i++)
                if (stack[i]?.identifier == AssassinId) return;
            var assassin = CreateAssassin();
            if (assassin == null) return;
            manager.AddClient(assassin);
            MelonLogger.Msg("[BE-OUTER] 外环区刺客已进入访客队列");
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 刺客入队失败: " + e); }
    }

    internal static void StartAssassination()
    {
        if (_assassinationPending) return;
        _assassinationPending = true;
        MelonCoroutines.Start(AssassinationSequence());
    }

    private static IEnumerator AssassinationSequence()
    {
        yield return new WaitForSeconds(1.5f);
        try
        {
            // Use the native game-over path so the run closes and saves consistently.
            PlayerStore.Instance?.ExecuteGameOver("rent");
        }
        catch (Exception e)
        {
            _assassinationPending = false;
            MelonLogger.Warning("[BE-OUTER] 刺杀结算失败: " + e);
        }
    }

    internal static void OverrideGameOver(ref string reason, ref string description)
    {
        if (!_assassinationPending) return;
        reason = BrewingExpansion.Lang.T("外环区刺杀", "Outer Ring Assassination");
        description = BrewingExpansion.Lang.T(
            "你与外环区的关系跌至谷底。刺客循着酒香而来，这一夜再没有人等到你开门。",
            "Your standing in the Outer Ring reached its lowest point. An assassin followed the scent of wine, and no one saw your shop open again.");
        _assassinationPending = false;
    }

    internal static bool TryPerkText(StoreReputationPerk perk, int kind, ref string value)
    {
        var id = perk?.perkId;
        if (id == null || !id.StartsWith("BE_OUTER_", StringComparison.Ordinal)) return false;
        for (int i = 0; i < Thresholds.Length; i++)
        {
            var expected = "BE_OUTER_" + (Thresholds[i] < 0 ? "N" + (-Thresholds[i]) : "P" + Thresholds[i]);
            if (id != expected) continue;
            value = kind == 0 ? BrewingExpansion.Lang.T(NamesZh[i], NamesEn[i])
                : kind == 1 ? BrewingExpansion.Lang.T(DescZh[i], DescEn[i]) : "";
            return true;
        }
        return false;
    }
}

[HarmonyPatch(typeof(StoreReputation), nameof(StoreReputation.InitReputation))]
internal static class OuterRingInitPatch
{
    private static void Postfix() => OuterRingFaction.EnsureRegistered();
}

[HarmonyPatch(typeof(ReputationUIManager), nameof(ReputationUIManager.OpenUI))]
internal static class OuterRingUIPatch
{
    private static void Prefix() => OuterRingFaction.EnsureRegistered();
}

[HarmonyPatch(typeof(StoreReputation), nameof(StoreReputation.OnItemTraded))]
internal static class OuterRingTradePatch
{
    private static HashSet<string> _bargainFeatureIds;

    private static HashSet<string> BargainFeatureIds()
    {
        if (_bargainFeatureIds != null) return _bargainFeatureIds;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in new[]
        {
            ItemFeatureList.BargainMarkup(1), ItemFeatureList.BargainDiscount(1),
            ItemFeatureList.BlackmailMarkup(20), ItemFeatureList.BlackmailDiscount(20),
            ItemFeatureList.ThreatenMarkup(20), ItemFeatureList.ThreatenDiscount(20)
        })
            if (!string.IsNullOrEmpty(feature?.identifier)) ids.Add(feature.identifier);
        _bargainFeatureIds = ids;
        return ids;
    }

    private static double GetBargainMultiplier(GameItem item)
    {
        double multiplier = 1;
        var features = item.itemFeatures;
        var ids = BargainFeatureIds();
        for (int i = 0; features != null && i < features.Count; i++)
        {
            var feature = features[i];
            if (feature == null || feature.isDisabled || !ids.Contains(feature.identifier)) continue;
            // 原版的最终价格阶段会逐项乘以 max(0, 1 + 修正百分比 / 100)。
            multiplier *= Math.Max(0, 1 + feature.GetCurrentValueMod() / 100.0);
        }
        return multiplier;
    }

    private static void Prefix(ref string factionId, ref int value, ref int originalValue,
        StoreClient storeClient, bool isPlayerSold, GameItem gameItem, out int __state)
    {
        __state = 0;
        var id = storeClient?.identifier;
        if (id == BrewingExpansion.BrewingExpansionMod.WeeklyTraderId
            || id == BrewingExpansion.BrewingExpansionMod.SeedTrader47Id)
        {
            OuterRingFaction.EnsureRegistered();
            factionId = OuterRingFaction.Id;
            storeClient.clientFaction = OuterRingFaction.Id;
        }
        if (id != BrewingExpansion.BrewingExpansionMod.WeeklyTraderId || !isPlayerSold || gameItem == null)
            return;
        try
        {
            int baseValue = (int)Math.Min(int.MaxValue, Math.Max(0L, gameItem.GetBaseValue()));
            double bargainMultiplier = GetBargainMultiplier(gameItem);
            int reputationValue = (int)Math.Min(int.MaxValue,
                Math.Max(0, Math.Round(baseValue * bargainMultiplier, MidpointRounding.AwayFromZero)));
            if (!PlayerStore.IsHardMode())
            {
                int oldOriginal = (int)Math.Min(int.MaxValue, Math.Abs((long)originalValue));
                __state = RNG.MultiplyAndRound(oldOriginal, 0.05)
                    - RNG.MultiplyAndRound(reputationValue, 0.05);
            }
            value = reputationValue;
            originalValue = reputationValue;
            MelonLogger.Msg("[BE-OUTER] 泥泥交易声望按基础值 " + baseValue
                + " × 议价倍率 " + bargainMultiplier.ToString("0.##")
                + " = " + reputationValue + " 计算");
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 泥泥基础值与议价声望计算失败: " + e); }
    }

    private static void Postfix(int __state)
    {
        if (__state == 0) return;
        try
        {
            var store = PlayerStore.Instance;
            if (store != null) store.wildFavor = checked(store.wildFavor + __state);
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 野性好感还原失败: " + e); }
    }
}

[HarmonyPatch(typeof(StoreReputationPerk), nameof(StoreReputationPerk.GetLocalizedPerkName))]
internal static class OuterRingPerkNamePatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
        => OuterRingFaction.TryPerkText(__instance, 0, ref __result);
}

[HarmonyPatch(typeof(StoreReputationPerk), nameof(StoreReputationPerk.GetLocalizedDescription))]
internal static class OuterRingPerkDescriptionPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
        => OuterRingFaction.TryPerkText(__instance, 1, ref __result);
}

[HarmonyPatch(typeof(StoreReputationPerk), nameof(StoreReputationPerk.GetLocalizedFlavorText))]
internal static class OuterRingPerkFlavorPatch
{
    private static void Postfix(StoreReputationPerk __instance, ref string __result)
        => OuterRingFaction.TryPerkText(__instance, 2, ref __result);
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.BeginDay))]
internal static class OuterRingDayPatch
{
    private static void Postfix()
    {
        OuterRingFaction.EnsureRegistered();
        OuterRingFaction.QueueAssassin();
    }
}

[HarmonyPatch(typeof(StoreUIManager), nameof(StoreUIManager.OnNextClientArrived))]
internal static class OuterRingAssassinArrivalPatch
{
    private static void Postfix()
    {
        try
        {
            if (PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint()?.identifier == OuterRingFaction.AssassinId)
                OuterRingFaction.StartAssassination();
        }
        catch (Exception e) { MelonLogger.Warning("[BE-OUTER] 刺客到店判定失败: " + e); }
    }
}

[HarmonyPatch(typeof(GameOverUIManager), nameof(GameOverUIManager.DisplayUI))]
internal static class OuterRingAssassinationGameOverPatch
{
    private static void Prefix(ref string givenReason, ref string givenDescription)
        => OuterRingFaction.OverrideGameOver(ref givenReason, ref givenDescription);
}
