// Generated from current production methods by extract.py. Native API is simulated.
using Il2Cpp; using MelonLoader; using BrewingExpansion;
namespace BrewingExpansion { public static partial class BrewingExpansionMod {
private static bool _midautumnFactoriesInited;
public const string DecorationTag = "DECORATION", OwnedTag = "IS_OWNED_TAG";
public const string WurenMooncakeId = "be_wuren_mooncake";
public const string MidautumnMooncakeBoxId = "be_midautumn_mooncake_box";
public const string MidautumnJadeRabbitId = "be_midautumn_jade_rabbit";
public const string MidautumnMoonOsmanthusId = "be_midautumn_moon_osmanthus";
public const string MidautumnBlindboxId = "be_midautumn_blindbox";
public const string MidautumnMoonLampId = "be_midautumn_moon_lamp";
public const string MidautumnLanternId = "be_midautumn_lantern";
public const string MidautumnMoonFanId = "be_midautumn_moon_fan";
public const string MidautumnBagId = "be_midautumn_bag";
public static GameItem CreateMidautumnMoonFan() => new();
public static GameItem CreateMidautumnMoonLamp() => new();
public static GameItem CreateMidautumnJadeRabbit() => new();
public static GameItem CreateMidautumnLantern() => new();
public static GameItem CreateMidautumnBlindbox() => new();
public static GameItem CreateMidautumnMooncakeBox() => new();
public static GameItem CreateWurenMooncake() => new();
public static GameItem CreateMidautumnBag() => new();
public const string MidautumnMoonOsmanthusName = "fruit", MidautumnMoonOsmanthusSpriteKey = "fruit"; public static GameItem CreateFruitItem(string id, string name, string key) => new();
        public const string IchigoFigurineId = "be_ichigo_figurine";
        public const string RukiaFigurineId = "be_rukia_figurine";
        public const string UryuFigurineId = "be_uryu_figurine";
        public const string IridiumSeaCucumberFigurineId = "be_iridium_sea_cucumber_figurine";
        private static readonly string[] FigurineIds = { IchigoFigurineId, RukiaFigurineId, UryuFigurineId, IridiumSeaCucumberFigurineId };
        private static readonly string[] FigurinePngNames = { "黑崎一护", "朽木露琪亚", "石田雨龙", "噢我的天啊是铱星大海参" };
        private static readonly string[] FigurineSpriteKeys = { "be_ichigo_figurine", "be_rukia_figurine", "be_uryu_figurine", "be_iridium_sea_cucumber_figurine" };
        private static readonly string[] FigurineEnglishNames = { "Ichigo Kurosaki", "Rukia Kuchiki", "Uryu Ishida", "Oh My God, It's an Iridium Sea Cucumber" };
        private static readonly string[] FigurineChineseDescriptions =
        {
            "橘发少年握紧斩魄刀，像下一秒就要跃出展示柜。守护重要之人的决心，被凝在这小小的身影里。拥有它，店铺吸引力 +100；同款不叠加。",
            "黑色死霸装与白色围巾在风里扬起。她把勇气藏在平静的目光中，也把一段相遇留在了这座展示柜里。拥有它，店铺吸引力 +100；同款不叠加。",
            "眼镜后的目光沉着，灵弓的弦仿佛仍在发亮。他不必高声宣告自己的信念，只需把箭稳稳射向前方。拥有它，店铺吸引力 +100；同款不叠加。",
            "从铱星的海底捞起一只大海参，连星露谷的渔夫都要惊呼：噢我的天啊！把它摆在店里，路过的人总会停下来看第二眼。拥有它，店铺吸引力 +100；同款不叠加。"
        };
        private static readonly string[] FigurineEnglishDescriptions =
        {
            "The orange-haired swordsman grips his blade, ready to leap from the display case. His resolve to protect those dear to him lives in this tiny figure. Adds 100 shop attractiveness; duplicates do not stack.",
            "Her black robes and white scarf catch an unseen breeze. A quiet gaze holds both courage and the memory of a fateful meeting. Adds 100 shop attractiveness; duplicates do not stack.",
            "Behind his glasses, his gaze stays steady and the bowstring seems to glow. He lets a carefully aimed arrow speak for his convictions. Adds 100 shop attractiveness; duplicates do not stack.",
            "An iridium sea cucumber, hauled up from the depths near Stardew Valley. Even a seasoned fisher would cry, 'Oh my god!' Passersby always stop for a second look. Adds 100 shop attractiveness; duplicates do not stack."
        };

        public static void InitMidautumnDelayedFactories()
        {
            if (_midautumnFactoriesInited) return;

            BEState.MidautumnDelayedFactories[MidautumnMoonFanId] = () => CreateMidautumnMoonFan();
            BEState.MidautumnDelayedFactories[MidautumnMoonLampId] = () => CreateMidautumnMoonLamp();
            BEState.MidautumnDelayedFactories[MidautumnJadeRabbitId] = () => CreateMidautumnJadeRabbit();
            BEState.MidautumnDelayedFactories[MidautumnLanternId] = () => CreateMidautumnLantern();
            BEState.MidautumnDelayedFactories[MidautumnMoonOsmanthusId] = () => CreateFruitItem(
                MidautumnMoonOsmanthusId, MidautumnMoonOsmanthusName, MidautumnMoonOsmanthusSpriteKey);

            // ★ 新增：这三个也走工厂，避开 ItemManager
            BEState.MidautumnDelayedFactories[MidautumnBlindboxId] = () => CreateMidautumnBlindbox();
            BEState.MidautumnDelayedFactories[MidautumnMooncakeBoxId] = () => CreateMidautumnMooncakeBox();
            BEState.MidautumnDelayedFactories[WurenMooncakeId] = () => CreateWurenMooncake();
            BEState.MidautumnDelayedFactories[MidautumnBagId] = () => CreateMidautumnBag();
            for (int i = 0; i < FigurineIds.Length; i++)
            {
                int index = i;
                BEState.MidautumnDelayedFactories[FigurineIds[i]] = () => CreateFigurine(index);
            }

            _midautumnFactoriesInited = true;

            MelonLogger.Msg("[BE-MID] 延迟工厂初始化完成，共 " + BEState.MidautumnDelayedFactories.Count + " 个");
        }
        private static GameItem CreateMidautumnCommon(string id, string name, string spriteKey, string desc, int w, int h)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(id);
                if (item == null) return null;
                item.SetName(name);
                item.SetValue(0);
                item.SetSprite("", spriteKey);
                item.shape = new GridShapeBuilder(w, h).SetDataFill(w, h, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;
                try { item.EnableTag(DecorationTag, false); } catch { }
                try { item.shortDescription = desc; } catch { }
                try { item.longDescription = desc; } catch { }
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateMidautumnCommon " + id + " 异常: " + e.Message); return null; }
        }
        public static GameItem CreateFigurine(int index)
        {
            if (index < 0 || index >= FigurineIds.Length) return null;
            return CreateMidautumnCommon(FigurineIds[index],
                Lang.T(FigurinePngNames[index], FigurineEnglishNames[index]), FigurineSpriteKeys[index],
                Lang.T(FigurineChineseDescriptions[index], FigurineEnglishDescriptions[index]), 2, index == 3 ? 2 : 3);
        }
        public static void RepairLegacyFigurines()
        {
            var store = PlayerStore.Instance;
            if (store == null) return;
            var seen = new HashSet<long>();
            int repaired = 0;
            foreach (var owned in new[] { true, false })
            {
                var items = store.FindAllItem(owned);
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null) continue;
                    int index = Array.IndexOf(FigurineIds, item.identifier);
                    if (index < 0 || !seen.Add(item.Pointer.ToInt64())) continue;
                    try
                    {
                        if (string.IsNullOrEmpty(item.name) || item.name == item.identifier)
                            item.SetName(Lang.T(FigurinePngNames[index], FigurineEnglishNames[index]));
                        item.SetSprite("", FigurineSpriteKeys[index]);
                        item.SetSpriteQuick("", FigurineSpriteKeys[index]);
                        item.shortDescription = Lang.T(FigurineChineseDescriptions[index], FigurineEnglishDescriptions[index]);
                        item.longDescription = item.shortDescription;
                        var types = new Il2CppSystem.Collections.Generic.List<string>();
                        types.Add("LUXURY_ITEM");
                        item.itemTypes = types;
                        item.EnableTag(DecorationTag, false);
                        item.EnableTag(OwnedTag, false);
                        repaired++;
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-FIGURINE] 修复失败: " + ex.Message); }
                }
            }
            if (repaired > 0) MelonLogger.Msg("[BE-FIGURINE] 已恢复 " + repaired + " 个手办的名称、贴图及拥有标签");
        }
        public static bool CreateFromDirectory(string identifier, bool isOwned, ref GameItem __result)
        {
            try
            {
                if (string.IsNullOrEmpty(identifier)) return true;

                // A request can arrive before LoadGame or any delayed coroutine.
                BrewingExpansionMod.InitMidautumnDelayedFactories();

                System.Func<GameItem> factory = null;
                if (BEState.MidautumnDelayedFactories.TryGetValue(identifier, out factory))
                {
                    try
                    {
                        __result = factory();
                        if (__result != null && isOwned)
                        {
                            try { __result.EnableTag(BrewingExpansionMod.OwnedTag, false); } catch { }
                        }
                        MelonLogger.Msg("[BE-MID] ★ 从延迟工厂生成: " + identifier + " isOwned=" + isOwned);
                    }
                    catch (Exception e)
                    {
                        MelonLogger.Warning("[BE-MID] 工厂异常 " + identifier + ": " + e.Message);
                        __result = null;
                    }
                    return false;
                }
            }
            catch { }
            return true;
        }
}}