using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace XIAOWOBrewingBarrel;

internal static class NightBrewingShop
{
    private const string MidautumnPurchaseDayKey = "BrewingExpansion.MidautumnPurchaseDay.v1";
    private const int MidautumnRestockDays = 10;
    private static readonly string[] StockIds =
    {
        BrewingExpansion.BrewingExpansionMod.IncubatorId,
        BrewingExpansion.BrewingExpansionMod.WinePrinterId,
        "wine_yeast_infinite",
        BrewingExpansion.BrewingExpansionMod.GoldenYeastReactorId,
        BrewingExpansion.BrewingExpansionMod.AdvancedYeastReactorId,
        BrewingExpansion.BrewingExpansionMod.ForbiddenYeastReactorId,
        BrewingExpansion.BrewingExpansionMod.LowBlindboxId,
        BrewingExpansion.BrewingExpansionMod.MidBlindboxId,
        BrewingExpansion.BrewingExpansionMod.PremiumBlindboxId,
        BrewingExpansion.BrewingExpansionMod.WineBlindboxId,
        BrewingExpansion.BrewingExpansionMod.QingyunFilterId,
        BrewingExpansion.BrewingExpansionMod.NingluFilterId,
        BrewingExpansion.BrewingExpansionMod.YingfeiFilterId,
        BrewingExpansion.BrewingExpansionMod.XuxuBatteryId,
        BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId,
        BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId,
        BrewingExpansion.BrewingExpansionMod.FigurineBlindboxId,
        BrewingExpansion.BrewingExpansionMod.ContainerBlindboxId,
        BrewingExpansion.BrewingExpansionMod.CassetteBundleId,
        BrewingExpansion.BrewingExpansionMod.AfterglowBlindboxId,
        BrewingExpansion.BrewingExpansionMod.CompactSmugglerBayId,
        BrewingExpansion.BrewingExpansionMod.BrewerySignId,
        BrewingExpansion.BrewingExpansionMod.MyPerfumeId,
        BrewingExpansion.BrewingExpansionMod.FreedomPerfumeId,
        XuxuWaterPurifier.BasicId,
        XuxuWaterPurifier.MiddleId,
        XuxuWaterPurifier.AdvancedId,
        BrewingExpansion.BrewingExpansionMod.HentaiYeastId,
        BrewingExpansion.BrewingExpansionMod.NiniCardId,
        LiunianAger.Id,
        BrewingExpansion.BrewingExpansionMod.ClearWaterTankId,
        BrewingExpansion.BrewingExpansionMod.MultiStorageId,
        BarrelItem.Id,
        ConcentratorItem.Id,
        // Keep the three documents together immediately after the machines.
        MachineGuides.BarrelId,
        MachineGuides.ConcentratorId,
        MachineGuides.IncubatorId,
        BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxId,
    };

    private static bool IsSinglePurchaseStock(string id) =>
        id == BrewingExpansion.BrewingExpansionMod.BrewerySignId
        || id == BrewingExpansion.BrewingExpansionMod.NiniCardId
        || id == "wine_yeast_infinite"
        || id == BrewingExpansion.BrewingExpansionMod.GoldenYeastReactorId
        || id == BrewingExpansion.BrewingExpansionMod.AdvancedYeastReactorId;

    private static bool _brewingVisit;
    internal static bool IsBrewingVisit => _brewingVisit;
    private static bool _placingInventorInventory;
    private static bool _addingOurStock;
    private static Sprite _originalSceneSprite;
    private static Sprite _brewingSceneSprite;
    private static Sprite _portraitSprite;
    private static SpriteRenderer _portraitRenderer;
    private static readonly Vector2 DefaultMapButtonPosition = new(270f, -15f);

    internal static void EnsureMapButton(MapUIManager map)
    {
        if (map == null || map.upgradeMerchantButton == null) return;
        _brewingVisit = false;
        try
        {
            var parent = map.upgradeMerchantButton.transform.parent;
            var existing = parent.Find("BE_NightBrewingShop");
            if (existing != null)
            {
                existing.gameObject.SetActive(OuterRingFaction.CanOpenShop);
                var existingLabel = existing.gameObject.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingLabel != null)
                {
                    existingLabel.text = BarrelItem.Text("四十七酿酒商店", "47's Brew Shop");
                    existingLabel.ForceMeshUpdate();
                }
                UpdateMapButtonPosition(map, existing.GetComponent<RectTransform>());
                return;
            }

            var clone = UnityEngine.Object.Instantiate(map.upgradeMerchantButton, parent);
            clone.name = "BE_NightBrewingShop";
            var layout = clone.GetComponent<LayoutElement>();
            if (layout == null) layout = clone.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = clone.GetComponent<RectTransform>();
            rect.anchoredPosition = DefaultMapButtonPosition;

            var label = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                foreach (var behaviour in label.GetComponents<MonoBehaviour>())
                    if (behaviour != null && (behaviour.GetIl2CppType().Name == "TMPLocalizer"
                        || behaviour.GetIl2CppType().Name == "LocalizeStringEvent"))
                        UnityEngine.Object.DestroyImmediate(behaviour);
                var chineseFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                    .FirstOrDefault(font => font != null && font.name == "NotoSansSC-VariableFont_wght");
                if (chineseFont != null) label.font = chineseFont;
                label.text = BarrelItem.Text("四十七酿酒商店", "47's Brew Shop");
            }

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((UnityEngine.Events.UnityAction)(() =>
            {
                if (!OuterRingFaction.CanOpenShop) return;
                _brewingVisit = true;
                Core.Log?.Msg("NIGHT_SHOP: visiting Forty-Seven brewing shop");
                map.VisitUpgradeMerchant();
            }));
            clone.SetActive(OuterRingFaction.CanOpenShop);
            if (label != null) label.text = BarrelItem.Text("四十七酿酒商店", "47's Brew Shop");
            UpdateMapButtonPosition(map, rect);
            Core.Log?.Msg("NIGHT_SHOP: map button ready; label=" + (label?.text ?? "missing"));
        }
        catch (Exception e) { Core.Log?.Error("NIGHT_SHOP button: " + e); }
    }

    private static void UpdateMapButtonPosition(MapUIManager map, RectTransform shopRect)
    {
        if (map?.mapPanel == null || shopRect == null) return;
        shopRect.anchoredPosition = DefaultMapButtonPosition;
    }

    internal static void BeginPlacement()
    {
        _placingInventorInventory = _brewingVisit;
        if (_placingInventorInventory)
        {
            // Native shop placement creates and renders the stock immediately.
            // Prepare every embedded shop icon before the first item is built.
            BrewingExpansion.BrewingExpansionMod.EnsureSpriteAssetsReady();
            ApplySceneSprite(MapUIManager.Instance);
        }
    }

    internal static bool ShouldBlockOriginalStock => _placingInventorInventory && !_addingOurStock;

    private static bool Owns(PlayerStore store, string id)
    {
        var owned = store.FindAllItem(true);
        if (owned == null) return false;
        for (var i = 0; i < owned.Count; i++)
            if (owned[i]?.identifier == id) return true;
        return false;
    }

    private static bool MidautumnRestocking(PlayerStore store)
    {
        if (store?.modData == null || !store.modData.ContainsKey(MidautumnPurchaseDayKey)) return false;
        if (!int.TryParse(store.modData[MidautumnPurchaseDayKey], out var purchaseDay)) return false;
        var elapsed = BrewingExpansion.BrewingExpansionMod.GetCurrentGameDay() - purchaseDay;
        return elapsed >= 0 && elapsed < MidautumnRestockDays;
    }

    private static GameItem RecreatePlaceholder(string id)
    {
        if (id == BrewingExpansion.BrewingExpansionMod.IncubatorId) return BrewingExpansion.BrewingExpansionMod.CreateIncubatorItem();
        if (id == BrewingExpansion.BrewingExpansionMod.WinePrinterId) return BrewingExpansion.BrewingExpansionMod.CreateWinePrinterItem();
        if (id == BrewingExpansion.BrewingExpansionMod.GoldenYeastReactorId) return BrewingExpansion.BrewingExpansionMod.CreateGoldenYeastReactorItem();
        if (id == BrewingExpansion.BrewingExpansionMod.AdvancedYeastReactorId) return BrewingExpansion.BrewingExpansionMod.CreateAdvancedYeastReactorItem();
        if (id == BrewingExpansion.BrewingExpansionMod.ForbiddenYeastReactorId) return BrewingExpansion.BrewingExpansionMod.CreateForbiddenYeastReactorItem();
        if (id == BrewingExpansion.BrewingExpansionMod.LowBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateLowBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.MidBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateMidBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.PremiumBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreatePremiumBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.AfterglowBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateAfterglowBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.WineBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateWineBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.FigurineBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateFigurineBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.ContainerBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateContainerBlindbox();
        if (id == BrewingExpansion.BrewingExpansionMod.CassetteBundleId) return BrewingExpansion.BrewingExpansionMod.CreateCassetteBundle();
        if (id == BrewingExpansion.BrewingExpansionMod.QingyunFilterId) return BrewingExpansion.BrewingExpansionMod.CreateQingyunFilter();
        if (id == BrewingExpansion.BrewingExpansionMod.NingluFilterId) return BrewingExpansion.BrewingExpansionMod.CreateNingluFilter();
        if (id == BrewingExpansion.BrewingExpansionMod.YingfeiFilterId) return BrewingExpansion.BrewingExpansionMod.CreateYingfeiFilter();
        if (id == BrewingExpansion.BrewingExpansionMod.XuxuBatteryId) return BrewingExpansion.BrewingExpansionMod.CreateXuxuBattery();
        if (id == BrewingExpansion.BrewingExpansionMod.XuxuMidBatteryId) return BrewingExpansion.BrewingExpansionMod.CreateXuxuMidBattery();
        if (id == BrewingExpansion.BrewingExpansionMod.XuxuHighBatteryId) return BrewingExpansion.BrewingExpansionMod.CreateXuxuHighBattery();
        if (id == BrewingExpansion.BrewingExpansionMod.CompactSmugglerBayId) return BrewingExpansion.BrewingExpansionMod.CreateCompactSmugglerBay();
        if (id == BrewingExpansion.BrewingExpansionMod.BrewerySignId) return BrewingExpansion.BrewingExpansionMod.CreateBrewerySignItem();
        if (id == BrewingExpansion.BrewingExpansionMod.MyPerfumeId) return BrewingExpansion.BrewingExpansionMod.CreateMyPerfume();
        if (id == BrewingExpansion.BrewingExpansionMod.FreedomPerfumeId) return BrewingExpansion.BrewingExpansionMod.CreateFreedomPerfume();
        if (id == BrewingExpansion.BrewingExpansionMod.HentaiYeastId) return BrewingExpansion.BrewingExpansionMod.CreateHentaiYeast();
        if (id == BrewingExpansion.BrewingExpansionMod.NiniCardId) return BrewingExpansion.BrewingExpansionMod.CreateNiniCardItem();
        if (id == XuxuWaterPurifier.BasicId) return XuxuWaterPurifier.CreateBasic();
        if (id == XuxuWaterPurifier.MiddleId) return XuxuWaterPurifier.CreateMiddle();
        if (id == XuxuWaterPurifier.AdvancedId) return XuxuWaterPurifier.CreateAdvanced();
        if (id == LiunianAger.Id) return LiunianAger.Create();
        if (id == BrewingExpansion.BrewingExpansionMod.ClearWaterTankId) return BrewingExpansion.BrewingExpansionMod.CreateClearWaterTank();
        if (id == BrewingExpansion.BrewingExpansionMod.MultiStorageId) return BrewingExpansion.BrewingExpansionMod.CreateMultiStorage();
        if (id == BarrelItem.Id) return BarrelItem.Create();
        if (id == ConcentratorItem.Id) return ConcentratorItem.Create();
        if (id == BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxId) return BrewingExpansion.BrewingExpansionMod.CreateMidautumnBlindbox();
        for (var i = 0; i < MachineGuides.Ids.Length; i++)
            if (id == MachineGuides.Ids[i]) return MachineGuides.Create(i);
        return null;
    }

    internal static void RecordMidautumnPurchase(PlayerStore store, GameItem item)
    {
        if (!_brewingVisit || store == null || item?.identifier != BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxId)
            return;
        try
        {
            if (store.modData == null)
                store.modData = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
            var day = BrewingExpansion.BrewingExpansionMod.GetCurrentGameDay();
            store.modData[MidautumnPurchaseDayKey] = day.ToString();
            Core.Log?.Msg("NIGHT_SHOP: Mid-Autumn blind box bought on day " + day
                + "; next restock on day " + (day + MidautumnRestockDays));
        }
        catch (Exception e) { Core.Log?.Warning("NIGHT_SHOP Mid-Autumn purchase: " + e.Message); }
    }

    internal static void EndPlacement()
    {
        if (!_placingInventorInventory) return;
        _addingOurStock = true;
        var placed = 0;
        try
        {
            var store = PlayerStore.Instance;
            if (store == null) return;
            BrewingExpansion.BrewingExpansionMod.EnsureSpriteAssetsReady();
            // A new game does not run the LoadGame coroutine that normally
            // initializes the delayed Mid-Autumn factories. Do this after the
            // item-manager startup scan and before requesting shop stock.
            BrewingExpansion.BrewingExpansionMod.RegisterMidautumnItems();
            foreach (var id in StockIds)
            {
                if ((MachineGuides.Contains(id) || IsSinglePurchaseStock(id)) && Owns(store, id)) continue;
                if (id == BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxId && MidautumnRestocking(store)) continue;
                try
                {
                    var item = DirectoryMaster.Item(id, false);
                    if (item == null || (item.name == id && id != "wine_yeast_infinite")
                        || (id == BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxId
                            && item.spritePath != BrewingExpansion.BrewingExpansionMod.MidautumnBlindboxSpriteKey))
                    {
                        Core.Log?.Warning("NIGHT_SHOP item factory returned a placeholder; recreating " + id);
                        var rebuilt = RecreatePlaceholder(id);
                        if (rebuilt != null) item = rebuilt;
                    }
                    if (item == null || (item.name == id && id != "wine_yeast_infinite"))
                    { Core.Log?.Warning("NIGHT_SHOP missing valid item: " + id); continue; }
                    if (id == BrewingExpansion.BrewingExpansionMod.NiniCardId)
                        item.SetValue(BrewingExpansion.BrewingExpansionMod.NiniCardShopPrice);
                    store.AddDirectSellingItemToTable(item, false);
                    placed++;
                }
                catch (Exception e) { Core.Log?.Warning("NIGHT_SHOP stock " + id + ": " + e.Message); }
            }
            Core.Log?.Msg("NIGHT_SHOP: stocked " + placed + " items (guides/sign/card/reactor only if not owned)");
        }
        finally
        {
            _addingOurStock = false;
            _placingInventorInventory = false;
        }
    }

    internal static void Leave(MapUIManager map)
    {
        // The native exit animation still renders this scene. Restore the Doctor
        // only when the player explicitly enters the Doctor's shop again.
        _brewingVisit = false;
        _placingInventorInventory = false;
    }

    internal static void UpdateExitPrompt(ref string bodyText)
    {
        if (_brewingVisit)
            bodyText = BarrelItem.Text(
                "四十七会让无人机来帮你搬运留在地上的所有物品，确定离开吗？",
                "Forty-Seven will send drones to carry all items left on the floor. Are you sure you want to leave?");
    }

    internal static void BeforeVisit(MapUIManager map)
    {
        if (_brewingVisit) return;
        var render = GetSceneRenderer(map);
        if (render != null && _originalSceneSprite != null) render.sprite = _originalSceneSprite;
        if (_portraitRenderer != null) _portraitRenderer.gameObject.SetActive(false);
    }

    private static SpriteRenderer GetSceneRenderer(MapUIManager map)
    {
        if (map == null || map.upgradeStoreScene == null || map.upgradeStoreScene.transform.childCount == 0) return null;
        return map.upgradeStoreScene.transform.GetChild(0).GetComponent<SpriteRenderer>();
    }

    private static void ApplySceneSprite(MapUIManager map)
    {
        try
        {
            var render = GetSceneRenderer(map);
            if (render == null || render.sprite == null) return;
            if (_originalSceneSprite == null) _originalSceneSprite = render.sprite;
            if (_brewingSceneSprite == null)
            {
                var texture = LoadTexture("酿酒商店.png");
                if (texture == null) return;
                var ppu = _originalSceneSprite.pixelsPerUnit * texture.width / _originalSceneSprite.rect.width;
                _brewingSceneSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), ppu);
            }
            render.sprite = _brewingSceneSprite;
            if (_portraitSprite == null)
            {
                var texture = LoadTexture("商店四十七.png");
                if (texture == null) return;
                _portraitSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), _brewingSceneSprite.pixelsPerUnit);
            }
            if (_portraitRenderer == null || _portraitRenderer.transform.parent != render.transform)
            {
                var portrait = new GameObject("BE_FortySevenPortrait");
                portrait.transform.SetParent(render.transform, false);
                _portraitRenderer = portrait.AddComponent<SpriteRenderer>();
            }
            _portraitRenderer.sprite = _portraitSprite;
            _portraitRenderer.sortingLayerID = render.sortingLayerID;
            _portraitRenderer.sortingOrder = render.sortingOrder + 1;
            // Positions are measured in the provided 1672x941 scene image.
            var ppuScene = _brewingSceneSprite.pixelsPerUnit;
            _portraitRenderer.transform.localPosition = new Vector3(0f, 163f / ppuScene, 0f);
            _portraitRenderer.transform.localScale = new Vector3(1.65f, 1.65f, 1f);
            _portraitRenderer.gameObject.SetActive(true);
            Core.Log?.Msg("NIGHT_SHOP: Forty-Seven scene active");
        }
        catch (Exception e) { Core.Log?.Warning("NIGHT_SHOP scene: " + e); }
    }

    private static Texture2D LoadTexture(string file)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BrewingExpansion.Textures." + file);
        if (stream == null) { Core.Log?.Warning("NIGHT_SHOP asset missing: " + file); return null; }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, memory.ToArray())) return null;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        UnityEngine.Object.DontDestroyOnLoad(texture);
        return texture;
    }
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.OnItemBought))]
internal static class NightBrewingShopMidautumnPurchasePatch
{
    private static void Postfix(PlayerStore __instance, GameItem gameItem)
        => NightBrewingShop.RecordMidautumnPurchase(__instance, gameItem);
}

[HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.VisitUpgradeMerchant))]
internal static class NightBrewingShopVisitPatch
{
    private static void Prefix(MapUIManager __instance) => NightBrewingShop.BeforeVisit(__instance);
}

[HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.OpenUI))]
internal static class NightBrewingShopMapPatch
{
    private static void Postfix(MapUIManager __instance) => NightBrewingShop.EnsureMapButton(__instance);
}

[HarmonyPatch(typeof(StoreClientList), nameof(StoreClientList.PlaceInventorInventory))]
internal static class NightBrewingShopStockPatch
{
    private static void Prefix() => NightBrewingShop.BeginPlacement();
    private static void Postfix() => NightBrewingShop.EndPlacement();
}

[HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.AddDirectSellingItemToTable))]
internal static class NightBrewingShopBlockDoctorStockPatch
{
    private static bool Prefix() => !NightBrewingShop.ShouldBlockOriginalStock;
}

[HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.CheckItemLeftOnGround))]
internal static class NightBrewingShopExitPromptPatch
{
    private static void Prefix(ref string bodyText) => NightBrewingShop.UpdateExitPrompt(ref bodyText);
}

[HarmonyPatch(typeof(MapUIManager), nameof(MapUIManager.HandleLeaveUpgradeMerchant))]
internal static class NightBrewingShopConfirmedLeavePatch
{
    private static void Postfix(MapUIManager __instance) => NightBrewingShop.Leave(__instance);
}
