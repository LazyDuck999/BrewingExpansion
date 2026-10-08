using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
[assembly: AssemblyVersion("3.5.0.0")]
[assembly: AssemblyFileVersion("3.5.0.0")]
[assembly: AssemblyInformationalVersion("3.5")]
[assembly: MelonInfo(typeof(BrewingExpansion.BrewingExpansionMod),
    "Brewing Expansion", "3.5", "xuxu")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]

namespace BrewingExpansion
{
    // ★ 新增：把所有 Il2Cpp 类型的静态字段集中到这个独立类
    public class WineMarkEntry
    {
        public int hashcode;
        public string wineId;
        public int waterScore;
        public int contrabandLevel;
    }

    // ★★★ 新增：语言管理器 ★★★
    // ============ 语言管理器 ============
    public static class Lang
    {
        public static bool IsZH = true;
        private static bool _followGame = true;  // true=跟随游戏语言，false=手动指定
        private static bool _initialized = false;

        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Mods", "BrewingExpansion", "config.txt");

                if (File.Exists(path))
                {
                    string s = File.ReadAllText(path).Trim().ToLower();
                    if (s == "zh") { IsZH = true; _followGame = false; }
                    else if (s == "en") { IsZH = false; _followGame = false; }
                    else { _followGame = true; }  // auto / 空 / 其他值 → 跟随游戏
                }
                else
                {
                    // 默认跟随游戏语言；不再为了默认值创建额外配置文件。
                    _followGame = true;
                }
                ApplyGameLocale();
                MelonLogger.Msg("[BE-LANG] Init: IsZH=" + IsZH + ", followGame=" + _followGame);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-LANG] 读取配置失败: " + e.Message);
            }
        }

        // Prefer the game's current UI locale. A save's startLocale may be unavailable
        // on the main menu, and need not match a language changed in settings.
        public static void ApplyGameLocale()
        {
            UpdateGameLocale(true);
        }

        private static void UpdateGameLocale(bool reportErrors)
        {
            if (!_followGame) return;
            try
            {
                string locale = null;
                try { locale = LocHelper.GetCurrentLocaleCode(); }
                catch { /* Localization may not be initialized yet. */ }
                if (string.IsNullOrWhiteSpace(locale))
                    locale = PlayerStore.Instance?.startLocale;
                if (string.IsNullOrWhiteSpace(locale)) return;

                string s = locale.Trim().ToLowerInvariant();
                bool newIsZH;
                if (s.StartsWith("zh") || s == "cn" || s.Contains("chinese")) newIsZH = true;
                else if (s.StartsWith("en") || s.Contains("english")) newIsZH = false;
                else return;

                if (newIsZH == IsZH) return;
                IsZH = newIsZH;
                MelonLogger.Msg("[BE-LANG] 跟随游戏语言切换为 " + (IsZH ? "ZH" : "EN") + " (locale='" + s + "')");
            }
            catch (Exception e)
            {
                if (reportErrors) MelonLogger.Warning("[BE-LANG] ApplyGameLocale 异常: " + e.Message);
            }
        }

        public static string T(string zh, string en)
        {
            UpdateGameLocale(false);
            return IsZH ? zh : en;
        }
    }
    // ★★★ 语言管理器结束 ★★★
    // ★★★ 语言管理器结束 ★★★



    public class BrewingExpansionMod : MelonMod
    {
        // 四十七的每颗种子独立抽取；两个计数器随游戏存档保存。
        private static int _longingSeedMisses;
        private static int _afterglowSeedMisses;

        public static void ResetSeedPity()
        {
            _longingSeedMisses = 0;
            _afterglowSeedMisses = 0;
        }

        public const int SeedPriceCommon = 20;
        public const int SeedPriceRemote = 45;
        public const int SeedPriceLonging = 200;
        public const int SeedPriceAfterglow = 800;
        public const int FruitPriceCommon = 30;
        public const int FruitPriceRemote = 60;
        public const int FruitPriceLonging = 225;
        public const int FruitPriceAfterglow = 850;

        public const int IncubatorPrice = 300;
        public const int WinePrinterPrice = 300;
        public const int BasicCertPrice = 400;
        public const int MidCertPrice = 600;
        public const int HighCertPrice = 900;
        public const int GoldenYeastReactorPrice = 1000;
        public const int BrewerySignPrice = 100;
        public const int BrewingManualPrice = 10;
        public const float WORLD_UNITS_PER_CELL = 0.16f;
        public const float WineWorldWidthCells = 1f;

        public const int BasicCertGiftThreshold = 1500;
        public const int MidCertGiftThreshold = 3000;
        public const int HighCertGiftThreshold = 8000;
        public static string BasicCertGiftDialogue
        {
            get
            {
                return Lang.T(
                "你已经有资格走上xuxu的路了，加油，学徒。",
                "You are ready now to walk the path of xuxu. Keep at it, apprentice.");
            }
        }
        public static string MidCertGiftDialogue
        {
            get
            {
                return Lang.T(
                "看来你已经摸到门道了，拿着这张中级证书，踏上xuxu的路，去酿更好的酒吧。",
                "So you have found the way in. Take this intermediate certificate, step onto xuxu's path, and brew something finer.");
            }
        }
        public static string HighCertGiftDialogue
        {
            get
            {
                return Lang.T(
                "不错，xuxu当年也是这样。高级证书给你，别让我失望。",
                "Not bad. xuxu was the same, once. Here is the advanced certificate — do not disappoint me.");
            }
        }
        public static string WeeklyTraderNotEnoughDialogue
        {
            get
            {
                return Lang.T(
                "下次多屯点，这点价值根本不够我给你颁发证书啊。",
                "Stock up more next time. This much value will not earn you a certificate.");
            }
        }
        public static string WeeklyTraderAlreadyGiftedDialogue
        {
            get
            {
                return Lang.T(
                "这批货我收了，下次再来。",
                "I will take this batch. Come again.");
            }
        }

        public const int TierMass = 2;
        public const int TierCellar = 3;
        public const int TierNectar = 4;

        public const int BaseWineScore = 5;
        public const int IncubatorQuotaBase = 2;
        public const int IncubatorQuotaWithCert = 5;
        public const int IncubatorQuotaWithMidCert = 7;
        public const int IncubatorQuotaWithHighCert = 18;
        public const int FruitMaxCondition = 2;

        public const string VanillaSmallBottleId = "small_bottled_water";
        public const string TagPrefixWine = "be_wine_";
        public const string TagPrefixWater = "be_water_";

        public const int ScavSeedCountBase = 5;
        public const int ScavSeedCountWithCert = 7;
        public const int ScavSeedCountWithMidCert = 9;
        public const int ScavSeedCountWithHighCert = 13;

        public const string WeeklyTraderId = "be_weekly_trader";
        public static string WeeklyTraderName
        {
            get { return Lang.T("泥泥", "Nini"); }
        }
        public const int WeeklyTraderBudget = 12000;
        public const int WeeklyTraderDayOfWeek1 = 1;
        public const int WeeklyTraderDayOfWeek2 = 1;
        public static string WeeklyTraderGreeting
        {
            get
            {
                return Lang.T(
                "oi，小鬼，今天有烟酒卖吗？全卖我，交易额攒够了，我就给你颁发证书。",
                "Oi, kid — got any alcohol or tobacco to sell? Bring it to me. Trade enough, and I will issue you a certificate.");
            }
        }
        public const string WeeklyTraderPngName = "泥泥";
        public const string WeeklyTraderSpriteKey = "be_nini";
        public const string WeeklyTraderResourceName = "BrewingExpansion.Textures.泥泥.png";

        public const long WeeklyTraderPhoneNumber = 4747L;

        public const string SeedTrader47Id = "be_seed_trader_47";
        public static string SeedTrader47Name
        {
            get { return Lang.T("四十七", "Forty-Seven"); }
        }
        public const string SeedTrader47SpriteKey = "be_47";
        public const string SeedTrader47PngName = "四十七";
        public const string SeedTrader47ResourceName = "BrewingExpansion.Textures.四十七.png";
        public static string SeedTrader47Greeting
        {
            get
            {
                return Lang.T(
                "见没见过那个该死的泥泥？敢拿我卖你的种子酿酒给她喝，你就惨了。她要是来过，记得告诉我。",
                "Have you seen that damned Nini around? Brew wine from my seeds and give it to her, and you are finished. If she comes by, tell me.");
            }
        }
        public static string SeedTrader47DoneDialogue
        {
            get { return Lang.T("行。", "Fine."); }
        }

        public const string BrewerySignId = "be_brewery_sign";
        public static string BrewerySignName
        {
            get { return Lang.T("酿酒灯牌", "Brewery Sign"); }
        }
        public const string BrewerySignPngName = "酿酒灯牌";
        public const string BrewerySignSpriteKey = "be_brewery_sign";
        public const string BrewerySignResourceName = "BrewingExpansion.Textures.酿酒灯牌.png";
        // ★ 新加：泥泥的名片
        public const string NiniCardId = "be_nini_card";
        public static string NiniCardName
        {
            get { return Lang.T("泥泥的名片", "Nini's Business Card"); }
        }
        public const string NiniCardPngName = "号码泥泥";
        public const string NiniCardSpriteKey = "be_nini_card";
        public const string NiniCardResourceName = "BrewingExpansion.Textures.号码泥泥.png";
        public static string NiniCardDescription
        {
            get
            {
                return Lang.T(
                "宇宙最强的商人泥泥，静待你的召唤。",
                "Nini — the mightiest merchant in all the cosmos — awaits your call.");
            }
        }
        public const int NiniCardPrice = 0;          // 物品价值 = 0（定价）
        public const int NiniCardShopPrice = 300;      // 四十七售价 = 10

        public const string BrewingManualId = "be_brewing_manual";
        public static string BrewingManualName
        {
            get { return Lang.T("酿酒手册", "Brewing Manual"); }
        }
        public const string BrewingManualPngName = "酿酒手册";
        public const string BrewingManualSpriteKey = "be_brewing_manual";
        public const string BrewingManualResourceName = "BrewingExpansion.Textures.酿酒手册.png";
        public static string BrewingManualDescription
        {
            get
            {
                return Lang.T(
                "xuxu留下的酿酒手册。翻开它，你会看到所有酿酒的秘密。建议配合散落的配方残页一起阅读——那些残页需要你自己从四十七那里买齐。",
                "A brewing manual left behind by xuxu. Turn its pages, and every secret of brewing lies before you. Best read alongside the scattered recipe fragments — those you will need to gather yourself, from Forty-Seven.");
            }
        }
        public const string BrewingManualTag = "BE_BREWING_MANUAL";
        public static string RecipeLockedHint
        {
            get
            {
                return Lang.T(
                "尚未解锁。购买后即可查看内容。",
                "Not yet unlocked. Purchase to reveal its contents.");
            }
        }

        public const string RecipeId1 = "be_recipe_1";
        public const string RecipeId2 = "be_recipe_2";
        public const string RecipeId3 = "be_recipe_3";
        public const string RecipeId4 = "be_recipe_4";
        public const string RecipeId5 = "be_recipe_5";
        public const string RecipeId6 = "be_recipe_6";
        public const string RecipeId7 = "be_recipe_7";
        public const string RecipeId8 = "be_recipe_8";

        public const string CompactSmugglerBayId = "be_compact_smuggler_bay";
        public static string CompactSmugglerBayName
        {
            get { return Lang.T("四十七「小」暗格", "Forty-Seven's \"Compact\" Stash"); }
        }
        public const string CompactSmugglerBayPngName = "四十七「小」暗格";
        public const string CompactSmugglerBaySpriteKey = "be_compact_smuggler_bay";
        public const string CompactSmugglerBayResourceName = "BrewingExpansion.Textures.四十七「小」暗格.png";
        public static string CompactSmugglerBayDescription
        {
            get
            {
                return Lang.T(
                "四十七的私藏手艺。别看它小，里头的东西治安部怎么也搜不出来。",
                "Forty-Seven's private craft. Small as it is, the Ministry of Order will never find what lies inside.");
            }
        }
        public const int CompactSmugglerBayPrice = 1200;
        public const string CompactSmugglerBayTag = "BE_COMPACT_SMUGGLER_BAY";
        public const string SmugglerBayModId = "smuggler_bay_mod";

        public const string MultiStorageId = "be_multi_storage";
        public static string MultiStorageName
        {
            get { return Lang.T("多功能收纳仓", "Multi-Storage Unit"); }
        }
        public const string MultiStoragePngName = "多功能收纳仓";
        public const string MultiStorageSpriteKey = "be_multi_storage";
        public const string MultiStorageResourceName = "BrewingExpansion.Textures.多功能收纳仓.png";
        public static string MultiStorageDescription
        {
            get
            {
                return Lang.T(
                "四十七的得意之作。按右键可以切换外观。",
                "Forty-Seven's finest work. Right-click to change its appearance.");
            }
        }
        public const int MultiStoragePrice = 2500;
        public const string MultiStorageTag = "BE_MULTI_STORAGE";

        public static readonly string[] MultiStorageVariantSpriteKeys = new string[]
        {
        "be_multi_storage_01", "be_multi_storage_02", "be_multi_storage_03",
        "be_multi_storage_04", "be_multi_storage_05", "be_multi_storage_06",
        "be_multi_storage_07", "be_multi_storage_08", "be_multi_storage_09",
        "be_multi_storage_10", "be_multi_storage_11", "be_multi_storage_12",
        "be_multi_storage_13", "be_multi_storage_14", "be_multi_storage_15",
        "be_multi_storage_16", "be_multi_storage_17",
        };
        public static readonly string[] MultiStorageVariantPngNames = new string[]
        {
        "多功能收纳仓1", "多功能收纳仓2", "多功能收纳仓3",
        "多功能收纳仓4", "多功能收纳仓5", "多功能收纳仓6",
        "多功能收纳仓7", "多功能收纳仓8", "多功能收纳仓9",
        "多功能收纳仓10", "多功能收纳仓11", "多功能收纳仓12",
        "多功能收纳仓13", "多功能收纳仓14", "多功能收纳仓15",
        "多功能收纳仓16", "多功能收纳仓17",
        };
        public static readonly string[] MultiStorageVariantResourceNames = new string[]
        {
        "BrewingExpansion.Textures.多功能收纳仓1.png",
        "BrewingExpansion.Textures.多功能收纳仓2.png",
        "BrewingExpansion.Textures.多功能收纳仓3.png",
        "BrewingExpansion.Textures.多功能收纳仓4.png",
        "BrewingExpansion.Textures.多功能收纳仓5.png",
        "BrewingExpansion.Textures.多功能收纳仓6.png",
        "BrewingExpansion.Textures.多功能收纳仓7.png",
        "BrewingExpansion.Textures.多功能收纳仓8.png",
        "BrewingExpansion.Textures.多功能收纳仓9.png",
        "BrewingExpansion.Textures.多功能收纳仓10.png",
        "BrewingExpansion.Textures.多功能收纳仓11.png",
        "BrewingExpansion.Textures.多功能收纳仓12.png",
        "BrewingExpansion.Textures.多功能收纳仓13.png",
        "BrewingExpansion.Textures.多功能收纳仓14.png",
        "BrewingExpansion.Textures.多功能收纳仓15.png",
        "BrewingExpansion.Textures.多功能收纳仓16.png",
        "BrewingExpansion.Textures.多功能收纳仓17.png",
        };

        public const string ChiliId = "chili_fruit";
        public const string RiceId = "rice_fruit";
        public const string WormwoodId = "wormwood_fruit";
        public const string LemonFruitId = "lemon_fruit";
        public const string SugarcaneFruitId = "sugarcane_fruit";
        public const string LycheeId = "lychee_fruit";

        public const string JasmineSeedId = "jasmine_seed";
        public const string JasmineFruitId = "jasmine_fruit";
        public const string JasmineWineId = "jasmine_wine";
        public const string GreenGrapeSeedId = "green_grape_seed";
        public const string GreenGrapeFruitId = "green_grape_fruit";
        public const string GreenGrapeWineId = "green_grape_wine";
        public const string OsmanthusSeedId = "osmanthus_seed";
        public const string OsmanthusFruitId = "osmanthus_fruit";
        public const string OsmanthusWineId = "osmanthus_wine";
        public const string BayberrySeedId = "bayberry_seed";
        public const string BayberryFruitId = "bayberry_fruit";
        public const string BayberryWineId = "bayberry_wine";
        public const string VoidPlumSeedId = "void_plum_seed";
        public const string VoidPlumFruitId = "void_plum_fruit";
        public const string VoidPlumWineId = "void_plum_wine";
        public const string StardustBerrySeedId = "stardust_berry_seed";
        public const string StardustBerryFruitId = "stardust_berry_fruit";
        public const string StardustBerryWineId = "stardust_berry_wine";

        public const string GusuiWineId = "gusui_wine";
        public const string ChimaduWineId = "chimadu_wine";
        public const string SuantianBanzhanWineId = "suantian_banzhan_wine";
        public const string BipoLiuguangWineId = "bipo_liuguang_wine";
        public const string BanzhanSuankuWineId = "banzhan_suanku_wine";
        public const string DaopoShuangganWineId = "daopo_shuanggan_wine";
        public const string MaiganNiangWineId = "maigan_niang_wine";
        public const string MoxiangMiniangWineId = "moxiang_miniang_wine";
        public const string MoningQingxiaoWineId = "moning_qingxiao_wine";
        public const string HuazheLiunianWineId = "huazhe_liunian_wine";
        public const string BimaiLiujinWineId = "bimai_liujin_wine";
        public const string CuiyuQingzhuWineId = "cuiyu_qingzhu_wine";
        public const string ZiluJinfengWineId = "zilu_jinfeng_wine";
        public const string MoliQingtiWineId = "moli_qingti_wine";
        public const string GuixinChisuWineId = "guixin_chisu_wine";
        public const string JianghenChenzheWineId = "jianghen_chenzhe_wine";
        public const string ZhuoleiXiandanWineId = "zhuolei_xiandan_wine";
        public const string CuojiShuWineId = "cuoji_shu_wine";
        public const string ChenyuanMiniangWineId = "chenyuan_miniang_wine";
        public const string KongguKuaiWineId = "konggu_kuai_wine";
        public const string XingluoMaitianWineId = "xingluo_maitian_wine";
        public const string LengxingRuzhanWineId = "lengxing_ruzhan_wine";
        public const string YexiangChenjiWineId = "yexiang_chenji_wine";
        public const string XingzhuiQingtengWineId = "xingzhui_qingteng_wine";
        public const string XingchenWangeWineId = "xingchen_wange_wine";
        // ★ 清酿澄水箱
        public const string ClearWaterTankId = "be_clear_water_tank";
        public static string ClearWaterTankName
        {
            get { return Lang.T("清酿澄水箱", "Clear-Brew Settling Tank"); }
        }
        public const string ClearWaterTankPngName = "清酿澄水箱";
        public const string ClearWaterTankSpriteKey = "be_clear_water_tank";
        public const string ClearWaterTankResourceName = "BrewingExpansion.Textures.清酿澄水箱.png";
        public static string ClearWaterTankDescription
        {
            get
            {
                return Lang.T(
                "xuxu 亲手打造的澄水箱。能盛下三十升水，也能像原版水壶一样放滤芯净化。",
                "A settling tank handcrafted by xuxu. Holds thirty liters of water, and takes a filter cartridge just as the vanilla pitcher does.");
            }
        }
        public const string ClearWaterTankTag = "BE_CLEAR_WATER_TANK";
        public const int ClearWaterTankCapacityMl = 30000;
        private const string LiquidContainerCapacityTag = "LIQUID_CONTAINER_CAPACITY";
        public const int ClearWaterTankPrice = 200;
        public const string VanillaWaterPitcherId = "water_pitcher";

        // ★ xuxu暖心杯
        public const string XuxuWarmCupId = "be_xuxu_warm_cup";
        public static string XuxuWarmCupName
        {
            get { return Lang.T("xuxu暖心杯", "xuxu's Warmheart Cup"); }
        }
        public const string XuxuWarmCupPngName = "xuxu暖心杯";
        public const string XuxuWarmCupSpriteKey = "be_xuxu_warm_cup";
        public const string XuxuWarmCupResourceName = "BrewingExpansion.Textures.xuxu暖心杯.png";
        public static string XuxuWarmCupDescription
        {
            get
            {
                return Lang.T(
                "xuxu 亲手做的保温杯。17000 ml 的容量，比原版水壶能装。\n\n" +
                "晚上倒进去的热水，第二天早上还是温的。\n\n" +
                "——也能像原版水壶一样放滤芯净化。",
                "A thermos cup made by xuxu's own hands. 17,000 ml capacity — more than the vanilla pitcher holds.\n\n" +
                "Water poured in at night is still warm by morning.\n\n" +
                "— Takes a filter cartridge just as the vanilla pitcher does.");
            }
        }
        public const string XuxuWarmCupTag = "BE_XUXU_WARM_CUP";
        public const int XuxuWarmCupCapacityMl = 17000;
        public const int XuxuWarmCupPrice = 200;

        // ★ xuxu甜心杯
        public const string XuxuSweetCupId = "be_xuxu_sweet_cup";
        public static string XuxuSweetCupName
        {
            get { return Lang.T("xuxu甜心杯", "xuxu's Sweetheart Cup"); }
        }
        public const string XuxuSweetCupPngName = "xuxu甜心杯";
        public const string XuxuSweetCupSpriteKey = "be_xuxu_sweet_cup";
        public const string XuxuSweetCupResourceName = "BrewingExpansion.Textures.xuxu甜心杯.png";
        public static string XuxuSweetCupDescription
        {
            get
            {
                return Lang.T(
                "xuxu 的第二件作品。30000 ml 的容量，跟清酿澄水箱一样大。\n\n" +
                "这次他在杯身上画了一颗心——他说，喝水这件小事，也值得被认真对待。\n\n" +
                "——也能像原版水壶一样放滤芯净化。",
                "xuxu's second work. 30,000 ml capacity — as large as the Clear-Brew Settling Tank.\n\n" +
                "This time he painted a heart on the body of the cup. He said: even the small act of drinking water deserves to be taken seriously.\n\n" +
                "— Takes a filter cartridge just as the vanilla pitcher does.");
            }
        }
        public const string XuxuSweetCupTag = "BE_XUXU_SWEET_CUP";
        public const int XuxuSweetCupCapacityMl = 30000;
        public const int XuxuSweetCupPrice = 400;

        // ★ 四十七爱心杯
        public const string Love47CupId = "be_47_love_cup";
        public static string Love47CupName
        {
            get { return Lang.T("四十七爱心杯", "Forty-Seven's Heartfelt Cup"); }
        }
        public const string Love47CupPngName = "四十七爱心杯";
        public const string Love47CupSpriteKey = "be_47_love_cup";
        public const string Love47CupResourceName = "BrewingExpansion.Textures.四十七爱心杯.png";
        public static string Love47CupDescription
        {
            get
            {
                return Lang.T(
                "四十七定做的杯子。47000 ml 的容量，比他自己的暗格还能装。\n\n" +
                "杯子上没有图案，只在底部刻了一行小字：「多喝水」。\n\n" +
                "xuxu 说，这不是什么情话，是四十七真的担心他不喝水。\n\n" +
                "——也能像原版水壶一样放滤芯净化。",
                "A cup custom-made by Forty-Seven. 47,000 ml capacity — holds more than his own stash.\n\n" +
                "No pattern on the body, only a line of small text etched at the base: \"Drink more water.\"\n\n" +
                "xuxu says it is not a confession of love — it is simply that Forty-Seven truly worries he does not drink enough.\n\n" +
                "— Takes a filter cartridge just as the vanilla pitcher does.");
            }
        }
        public const string Love47CupTag = "BE_47_LOVE_CUP";
        public const int Love47CupCapacityMl = 47000;
        public const int Love47CupPrice = 1400;

        public const string MiscWineId = "be_misc_wine";
        public const string YidiaoWineId = "be_yidiao_wine";
        public const string CuofuWineId = "be_cuofu_wine";
        public const string YujinWineId = "be_yujin_wine";

        public const string DossierId = "dossier";
        // ★ 青筠滤芯
        public const string QingyunFilterId = "be_qingyun_filter";
        public const string QingyunFilterName = "青筠";
        public const string QingyunFilterPngName = "青筠";
        public const string QingyunFilterSpriteKey = "be_qingyun_filter";
        public const string QingyunFilterResourceName = "BrewingExpansion.Textures.青筠.png";
        public static string QingyunFilterDescription
        {
            get
            {
                return Lang.T(
                $"筠者，竹之青皮也。\n\n《礼记》说：如竹箭之有筠——竹的外皮坚韧如甲，护住内里的空。滤芯亦如是：外面一层青皮滤尽浊气，内里空空，只让最干净的水通过。\n\n水流过它时，如穿过整片竹林。\n\nxuxu 说：竹有节，所以能挺；滤有歇，所以能久。\n\n——每晚恢复 {QingyunFilterDailyRestore} 点耐久（上限 {QingyunFilterMaxDurability}）。",
                $"Yun — the green skin of the bamboo.\n\nThe Book of Rites says: as the bamboo arrow has its yun — an outer skin tough as armor, guarding the hollow within. Such is the filter: a single sheath of green that sieves the murk, hollow within, admitting only the purest water.\n\nWater passing through it is water passing through a whole bamboo grove.\n\nxuxu says: bamboo has its knots, and therefore stands; a filter has its rest, and therefore lasts.\n\n— Restores {QingyunFilterDailyRestore} durability each night (cap {QingyunFilterMaxDurability}).");
            }
        }
        public const string QingyunFilterTag = "BE_QINGYUN_FILTER";
        public const int QingyunFilterPrice = 800;
        public const int QingyunFilterMaxDurability = 600;
        public const int QingyunFilterDailyRestore = 60;
        public const string VanillaAdvFilterId = "water_filter_adv";
        // ★ 凝露滤芯（盲盒专属，不出售）
        public const string NingluFilterId = "be_ninglu_filter";
        public const string NingluFilterName = "凝露";
        public const string NingluFilterPngName = "凝露";
        public const string NingluFilterSpriteKey = "be_ninglu_filter";
        public const string NingluFilterResourceName = "BrewingExpansion.Textures.凝露.png";
        public static string NingluFilterDescription
        {
            get
            {
                return Lang.T(
                "露者，夜气所凝也。\n\n" +
                "《诗经》说：蒹葭苍苍，白露为霜——露生于夜，结于草叶，凝而不坠，是天与地之间最轻的一次相拥。\n\n" +
                "滤芯亦如是：外表如深海凝渊，内里澄澈，水从它身上经过，仿佛经过一整片静谧的深潭。\n\n" +
                "水与露本是一物，一个从天而降，一个从夜中生出。\n\n" +
                $"xuxu 说：露生于夜，故知静；水过其心，故知清。\n\n——每晚恢复 {NingluFilterDailyRestore} 点耐久（上限 {NingluFilterMaxDurability}）。",
                "Dew is the night's breath, made solid.\n\n" +
                "The Book of Songs says: thick grow the reeds, white dew becomes frost — dew is born of night, gathering on blades of grass, held without falling — the softest embrace ever exchanged between heaven and earth.\n\n" +
                "So it is with this filter: without, deep-sea stillness; within, perfect clarity. Water passing through it seems to cross a whole quiet pool.\n\n" +
                "Water and dew are one thing — one falls from the sky, one is born from night.\n\n" +
                $"xuxu says: dew is born of night, and so knows stillness; water passes through its heart, and so knows purity.\n\n— Restores {NingluFilterDailyRestore} durability each night (cap {NingluFilterMaxDurability}).");
            }
        }
        public const string NingluFilterTag = "BE_NINGLU_FILTER";
        public const int NingluFilterPrice = 1200;        // 不出售
        public const int NingluFilterMaxDurability = 800;
        public const int NingluFilterDailyRestore = 80;
        // ★ 樱绯滤芯（盲盒专属，不出售）
        public const string YingfeiFilterId = "be_yingfei_filter";
        public const string YingfeiFilterName = "樱绯";
        public const string YingfeiFilterPngName = "樱绯";
        public const string YingfeiFilterSpriteKey = "be_yingfei_filter";
        public const string YingfeiFilterResourceName = "BrewingExpansion.Textures.樱绯.png";
        public static string YingfeiFilterDescription
        {
            get
            {
                return Lang.T(
                "樱者，春之信使也。\n\n" +
                "《诗经》说：桃之夭夭，灼灼其华——一树花开，是春天最温柔的一场告白。\n\n" +
                "滤芯亦如是：外层如落樱拂水，粉而不艳，滤尽浊气；内里澄净如洗，只让最干净的水通过。\n\n" +
                "水流过它时，仿佛穿过一整个花期。\n\n" +
                $"xuxu 说：樱开于春，故惜其短；滤净于水，故守其清。\n\n——每晚恢复 {YingfeiFilterDailyRestore} 点耐久（上限 {YingfeiFilterMaxDurability}）。",
                "Sakura — the messenger of spring.\n\n" +
                "The Book of Songs says: the peach tree, tender and fair — how brilliant its blossoms. A whole tree in flower is the gentlest confession spring ever makes.\n\n" +
                "So it is with this filter: without, fallen petals brushing water, pink yet never gaudy, sifting every murk; within, clarity washed clean, admitting only the purest water.\n\n" +
                "Water passing through it crosses an entire blooming season.\n\n" +
                $"xuxu says: sakura blooms in spring, and so treasures its brevity; a filter purifies water, and so guards its clarity.\n\n— Restores {YingfeiFilterDailyRestore} durability each night (cap {YingfeiFilterMaxDurability}).");
            }
        }
        public const string YingfeiFilterTag = "BE_YINGFEI_FILTER";
        public const int YingfeiFilterPrice = 2000;
        public const int YingfeiFilterMaxDurability = 1000;
        public const int YingfeiFilterDailyRestore = 100;
        // 情思盲盒沿用原高级盲盒的 ID 和贴图，兼容旧存档。
        public const string PremiumBlindboxId = "be_premium_blindbox";
        public static string PremiumBlindboxName
        {
            get { return Lang.T("情思盲盒", "Longing Blindbox"); }
        }
        public const string PremiumBlindboxPngName = "高级盲盒";
        public const string PremiumBlindboxSpriteKey = "be_premium_blindbox";
        public const string PremiumBlindboxResourceName = "BrewingExpansion.Textures.高级盲盒.png";
        public static string PremiumBlindboxDescription
        {
            get
            {
                return Lang.T(
                "金色纹路封住盒盖，只留下情思的种子。\n\n双击打开，获得两颗情思种子；每种种子等概率。",
                "Golden patterns seal the lid around seeds of Longing.\n\nDouble-click to receive two Longing seeds. Each seed in this rarity is equally likely.");
            }
        }
        public const int PremiumBlindboxPrice = 3000;
        public const string PremiumBlindboxTag = "BE_PREMIUM_BLINDBOX";

        public const string AfterglowBlindboxId = "be_afterglow_blindbox";
        public const string AfterglowBlindboxPngName = "遗光盲盒";
        public const string AfterglowBlindboxSpriteKey = "be_afterglow_blindbox";
        public const string AfterglowBlindboxResourceName = "BrewingExpansion.Textures.遗光盲盒.png";
        public const string AfterglowBlindboxTag = "BE_AFTERGLOW_BLINDBOX";
        public const int AfterglowBlindboxPrice = 8000;
        public static string AfterglowBlindboxName => Lang.T("遗光盲盒", "Afterglow Blindbox");
        public static string AfterglowBlindboxDescription => Lang.T(
            "微光收在盒中，只留下遗光的种子。\n\n双击打开，获得两颗遗光种子；每种种子等概率。",
            "A faint light rests within, holding only Afterglow seeds.\n\nDouble-click to receive two Afterglow seeds. Each seed in this rarity is equally likely.");

        public const string StorageBayId = "storage_bay";

        public const string BasicCertId = "be_basic_cert";
        public static string BasicCertName
        {
            get { return Lang.T("初级酿酒证书", "Basic Brewing Certificate"); }
        }
        public const string BasicCertPngName = "初级酿酒证书";
        public const string BasicCertSpriteKey = "be_basic_cert";
        public const string BasicCertResourceName = "BrewingExpansion.Textures.初级酿酒证书.png";
        public static string BasicCertDescription
        {
            get
            {
                return Lang.T(
                "一张装裱精美的资格证。持有它能让种子孵化器每晚孵化五颗种子，四十七会带来七颗种子。四十七的远来概率固定 15%；情思从 3% 逐步升至 8%，遗光从 1% 逐步升至 5%，凡间占剩余概率。",
                "An elegantly framed certificate. The Seed Incubator hatches five seeds nightly, and Forty-Seven brings seven. From Afar stays at 15%; Longing rises from 3% to 8% and Afterglow from 1% to 5% after misses. Mortal fills the remainder.");
            }
        }
        public const string MidCertId = "be_mid_cert";
        public static string MidCertName
        {
            get { return Lang.T("中级酿酒证书", "Intermediate Brewing Certificate"); }
        }
        public const string MidCertPngName = "中级酿酒证书";
        public const string MidCertSpriteKey = "be_mid_cert";
        public const string MidCertResourceName = "BrewingExpansion.Textures.中级酿酒证书.png";
        public static string MidCertDescription
        {
            get
            {
                return Lang.T(
                "一张用料更考究的资格证。持有它能让种子孵化器每晚孵化七颗种子，四十七会带来九颗种子。四十七的情思与遗光概率在递增加成后再乘 1.3：分别为 3.9%～10.4% 与 1.3%～6.5%；远来固定 15%，凡间占剩余概率。",
                "A certificate of finer materials. The Seed Incubator hatches seven seeds nightly, and Forty-Seven brings nine. After the miss bonus, Longing and Afterglow odds are multiplied by 1.3: 3.9%–10.4% and 1.3%–6.5%. From Afar stays at 15%; Mortal fills the remainder.");
            }
        }
        public const string HighCertId = "be_high_cert";
        public static string HighCertName
        {
            get { return Lang.T("高级酿酒证书", "Advanced Brewing Certificate"); }
        }
        public const string HighCertPngName = "高级酿酒证书";
        public const string HighCertSpriteKey = "be_high_cert";
        public const string HighCertResourceName = "BrewingExpansion.Textures.高级酿酒证书.png";
        public static string HighCertDescription
        {
            get
            {
                return Lang.T(
                "一张用暗金线绣边的资格证。持有它能让种子孵化器每晚孵化十八颗种子，四十七会带来十三颗种子。四十七的情思与遗光概率在递增加成后再乘 1.6：分别为 4.8%～12.8% 与 1.6%～8%；远来固定 15%，凡间占剩余概率。这是 xuxu 亲手签发的最后一档。",
                "A certificate edged in dark-gold thread. The Seed Incubator hatches eighteen seeds nightly, and Forty-Seven brings thirteen. After the miss bonus, Longing and Afterglow odds are multiplied by 1.6: 4.8%–12.8% and 1.6%–8%. From Afar stays at 15%; Mortal fills the remainder. This is xuxu's final tier.");
            }
        }

        public const string GoldenYeastReactorId = "be_golden_yeast_reactor";
        public static string GoldenYeastReactorName
        {
            get { return Lang.T("中级酵母反应器", "Intermediate Yeast Reactor"); }
        }
        public const string GoldenYeastReactorPngName = "中级酵母反应器";
        public const string GoldenYeastReactorSpriteKey = "be_golden_yeast_reactor";
        public const string GoldenYeastReactorResourceName = "BrewingExpansion.Textures.中级酵母反应器.png";
        public static string GoldenYeastReactorDescription
        {
            get
            {
                return Lang.T(
                "xuxu的作品：一台能把酵母无限循环利用的生物反应器，同时把发酵时间从两天压缩到一天。",
                "xuxu's work: a bioreactor that recycles yeast without end, compressing fermentation from two days to one.");
            }
        }
        public const int GoldenYeastReactorPower = 4;

        public const string AdvancedYeastReactorId = "be_advanced_yeast_reactor";
        public static string AdvancedYeastReactorName
        {
            get { return Lang.T("高级酵母反应器", "Advanced Yeast Reactor"); }
        }
        public const string AdvancedYeastReactorPngName = "高级酵母反应器";
        public const string AdvancedYeastReactorSpriteKey = "be_advanced_yeast_reactor";
        public const string AdvancedYeastReactorResourceName = "BrewingExpansion.Textures.高级酵母反应器.png";
        public static string AdvancedYeastReactorDescription
        {
            get
            {
                return Lang.T(
                "xuxu的封笔之作：酵母无限循环，发酵时间压缩到一天。用它酿出的酒会带上「顶级违禁品」的标记。",
                "xuxu's final work: yeast recycled without end, fermentation compressed to a single night. Wine brewed with it carries the mark of \"top-tier contraband.\"");
            }
        }
        public const int AdvancedYeastReactorPower = 5;
        public const int AdvancedYeastReactorPrice = 8000;
        public const int AdvancedYeastReactorContrabandLevel = 4;

        // ★ 禁忌酵母反应器
        public const string ForbiddenYeastReactorId = "be_forbidden_yeast_reactor";
        public static string ForbiddenYeastReactorName
        {
            get { return Lang.T("禁忌酵母反应器", "Forbidden Yeast Reactor"); }
        }
        public const string ForbiddenYeastReactorPngName = "禁忌酵母反应器";
        public const string ForbiddenYeastReactorSpriteKey = "be_forbidden_yeast_reactor";
        public const string ForbiddenYeastReactorResourceName = "BrewingExpansion.Textures.禁忌酵母反应器.png";
        public static string ForbiddenYeastReactorDescription
        {
            get
            {
                return Lang.T(
                "xuxu 的秘密之作。\n\n" +
                "一枚封存在反应器里的红色合成酵母——它本该在反应结束后就死去，却被某种违规的手段困住了。\n\n" +
                "放入水，不需要果实，让它自己发酵。\n\n" +
                "一天后，你会得到一瓶阴光莓果酿。",
                "xuxu's secret work.\n\n" +
                "A red synthetic yeast sealed within the reactor — one that should have died when the reaction ended, yet is trapped by some forbidden means.\n\n" +
                "Pour in water; no fruit is needed. Let it ferment itself.\n\n" +
                "A day later, you will have a bottle of Neon Afterdream.");
            }
        }
        public const int ForbiddenYeastReactorPower = 5;
        public const int ForbiddenYeastReactorPrice = 50000;

        // ★ 原版合成酵母 ID
        public const string VanillaRedYeastId = "wine_yeast_red";
        public const string HentaiYeastId = "wine_yeast_hentai";
        public const string HentaiYeastTag = "BE_HENTAI_YEAST";
        public static string HentaiYeastName => Lang.T("hentai酵母", "hentai Yeast");
        public const string HentaiYeastPngName = "hentai酵母";
        public const string HentaiYeastSpriteKey = "be_hentai_yeast";
        public const string HentaiYeastResourceName = "BrewingExpansion.Textures.hentai酵母.png";
        public const int HentaiYeastPrice = 233;
        public const int HentaiYeastFermentationDays = 4;
        public static string HentaiYeastDescription => Lang.T(
            "陈年老臭酒专属发酵酵母，桂花荔枝磨粉制成。到底是何人调配的这个小粉末，这野路子配方经过了三次试酿后才成功，但气味太恶心了，没人说得清这是个啥原理，只要启用，必产生变态异味，这俩东西混在一起简直让人臭到昏厥，但偏偏有人喜欢这种独特风味。后来这个东西被人戏称为hentai酵母。一次性消耗品。",
            "A fermentation yeast made for old, foul-smelling wine, ground from osmanthus and lychee. No one knows who blended this little powder. The wild recipe took three trial brews to succeed, and the smell is unbearable. Once used, it always produces a bizarre stench. Yet some people love the unusual flavor. It came to be called hentai Yeast. Consumed after one use.");

        public const string IncubatorId = "be_incubator";
        public static string IncubatorName
        {
            get { return Lang.T("种子孵化器", "Seed Incubator"); }
        }
        public const string IncubatorPngName = "种子孵化器";
        public const string IncubatorSpriteKey = "be_incubator";
        public const string IncubatorResourceName = "BrewingExpansion.Textures.种子孵化器.png";
        public static string IncubatorDescription
        {
            get
            {
                return Lang.T(
                "把种子放进输入槽，有种子时过夜消耗 12 kJ 电量孵出果实；空仓不耗电。持有酿酒证书能提高每晚的孵化上限。",
                "Place seeds in the input area. A night with seeds uses 12 kJ to hatch fruit; an empty incubator uses no power. Brewing certificates raise the nightly limit.");
            }
        }

        public const string WinePrinterId = "be_wine_printer";
        public static string WinePrinterName
        {
            get { return Lang.T("酒瓶打印机", "Bottle Printer"); }
        }
        public const string WinePrinterPngName = "酒瓶打印机";
        public const string WinePrinterSpriteKey = "be_wine_printer";
        public const string WinePrinterResourceName = "BrewingExpansion.Textures.酒瓶打印机.png";
        public static string WinePrinterDescription
        {
            get
            {
                return Lang.T(
                "xuxu带来的天外科技，小手一点就会出现酒瓶。每次打印消耗 10 kJ 电量。",
                "Otherworldly tech brought by xuxu. A tap of the hand, and a bottle appears. Each print uses 10 kJ of energy.");
            }
        }


        public const string SlotTagBattery = "be_slot_battery";
        public const string SlotTagModule = "be_slot_module";
        public const string SlotTagInput = "be_slot_input";
        public const string SlotTagOutput = "be_slot_output";
        public const string MyPerfumeId = "be_contraband_perfume";
        public const string MyPerfumeUsesTagPrefix = "BE_PERFUME_USES_";
        public static string MyPerfumeName
        {
            get { return Lang.T("法外狂徒", "Outlaw"); }
        }
        public const string MyPerfumeSpriteKey = "be_contraband_perfume";
        public const string MyPerfumePngName = "法外狂徒";
        public const string MyPerfumeResourceName = "BrewingExpansion.Textures.法外狂徒.png";
        public const int MyPerfumePrice = 50000;
        public const int MyPerfumeMaxUses = 5;
        public const int MyPerfumeValuePerUse = -10000;
        public const int MyPerfumeCrimeReduction = 20000;
        public const string FreedomPerfumeId = "be_freedom_perfume";
        public const string FreedomPerfumeSpriteKey = "be_freedom_perfume";
        public const string FreedomPerfumePngName = "逍遥法外";
        public const string FreedomPerfumeResourceName = "BrewingExpansion.Textures.逍遥法外.png";
        public const int FreedomPerfumePrice = 5000;
        public const int FreedomPerfumeCrimeReduction = 2000;
        public const string FallbackWineKey = "be_fallback_wine";
        public const string FallbackWinePngName = "霓虹残梦";
        public const string FallbackWineResourceName = "BrewingExpansion.Textures.霓虹残梦.png";
        // ★ 霓虹残梦（盲盒专属，可出售的收藏酒）
        public const string NeonDreamWineId = "be_neon_dream_wine";
        public static string NeonDreamWineName
        {
            get { return Lang.T("霓虹残梦", "Neon Afterdream"); }
        }
        public const string NeonDreamWineSpriteKey = "be_neon_dream_wine";
        public static string NeonDreamWineDescription
        {
            get
            {
                return Lang.T(
                "一瓶没有配方的酒。\n\n" +
                "霓虹灯照在玻璃上，颜色就渗进去了。它不属于任何一棵树，也不属于任何一场丰收。\n\n" +
                "调酒师说，这是所有酿错的酒最后去的地方——它们不是失败了，只是没有等到对的人。\n\n" +
                "如果你在某个深夜喝到它，别问它从哪来。问你自己，想去哪。",
                "A bottle with no recipe.\n\n" +
                "Neon light falls upon the glass, and the color seeps in. It belongs to no tree, and to no harvest.\n\n" +
                "The bartender says this is where all wrongly-brewed wines finally go — they did not fail, they merely never met the right person.\n\n" +
                "If you taste it late some night, do not ask where it came from. Ask yourself — where do you want to go?");
            }
        }
        public const int NeonDreamWinePrice = 999;
        public const string NeonDreamWineTag = "BE_NEON_DREAM_WINE";
        // ★ 装饰品吸引力
        public const string DecorationTag = "BE_DECORATION";
        public const int DecorationAttractivenessPerItem = 100;  // 每种 +100
        public const string Trader47StockTag = "BE_47_STOCK";
        // ★ 中秋节系列
        public const string MidautumnMoonFanId = "be_midautumn_moon_fan";
        public static string MidautumnMoonFanName
        {
            get { return Lang.T("蟾宫折桂扇", "Toad Palace Cassia Fan"); }
        }
        public const string MidautumnMoonFanPngName = "蟾宫折桂扇";
        public const string MidautumnMoonFanSpriteKey = "be_midautumn_moon_fan";
        public const string MidautumnMoonFanResourceName = "BrewingExpansion.Textures.蟾宫折桂扇.png";
        public static string MidautumnMoonFanDescription
        {
            get
            {
                return Lang.T(
                "团扇轻摇，扇面绘一株老桂，花开如碎金。\n\n" +
                "传说月宫里那棵桂树，花开千年，香飘万里。执扇的人，仿佛从月中剪下了一缕秋风。\n\n" +
                "「蟾宫」是月亮的别称，「折桂」暗喻登科及第——中国人千年来最美好的两桩祝愿，都在这柄扇子里。\n\n" +
                "——摆放在店铺里，可提升店铺吸引力。",
                "A round fan, lightly waved. On its face, an old cassia tree, blossoms scattered like fragments of gold.\n\n" +
                "Legend tells of a cassia tree in the moon palace, blooming once in a thousand years, its fragrance drifting ten thousand miles. Whoever holds this fan has, it seems, cut a sliver of autumn wind from the moon itself.\n\n" +
                "\"Toad Palace\" is an old name for the moon; \"plucking the cassia\" — a metaphor for passing the imperial examination. The two most beautiful wishes the Chinese have held for a thousand years dwell together within this fan.\n\n" +
                "— Displayed in your shop, it raises your shop's attractiveness.");
            }
        }

        public const string MidautumnMoonLampId = "be_midautumn_moon_lamp";
        public static string MidautumnMoonLampName
        {
            get { return Lang.T("云开月明灯", "Cloud-Parted Moonlight Lamp"); }
        }
        public const string MidautumnMoonLampPngName = "云开月明灯";
        public const string MidautumnMoonLampSpriteKey = "be_midautumn_moon_lamp";
        public const string MidautumnMoonLampResourceName = "BrewingExpansion.Textures.云开月明灯.png";
        public static string MidautumnMoonLampDescription
        {
            get
            {
                return Lang.T(
                "一片薄云，托起一轮圆月。灯从月中透出来，柔和得像刚洗过的月色。\n\n" +
                "古人说：守得云开见月明。\n\n" +
                "无论夜多深、云多厚，总有一刻，云散、月现、人间团圆。\n\n" +
                "——摆放在店铺里，可提升店铺吸引力。",
                "A wisp of cloud carries a full moon. Light spills from within it, soft as freshly washed moonlight.\n\n" +
                "The ancients said: wait for the clouds to part, and you will see the bright moon.\n\n" +
                "However deep the night, however thick the clouds, there will come a moment — clouds disperse, moon appears, and the world is reunited.\n\n" +
                "— Displayed in your shop, it raises your shop's attractiveness.");
            }
        }

        public const string MidautumnJadeRabbitId = "be_midautumn_jade_rabbit";
        public static string MidautumnJadeRabbitName
        {
            get { return Lang.T("蟾宫玉兔", "Toad Palace Jade Rabbit"); }
        }
        public const string MidautumnJadeRabbitPngName = "蟾宫玉兔";
        public const string MidautumnJadeRabbitSpriteKey = "be_midautumn_jade_rabbit";
        public const string MidautumnJadeRabbitResourceName = "BrewingExpansion.Textures.蟾宫玉兔.png";
        public static string MidautumnJadeRabbitDescription
        {
            get
            {
                return Lang.T(
                "绒毛做的小白兔，怀里抱着一只小小的玉杵。\n\n" +
                "《酉阳杂俎》说，月中有桂树，树下有玉兔，长年捣药不止。\n\n" +
                "它捣了一千年的药，只为问一句：人间别来无恙？\n\n" +
                "——摆放在店铺里，可提升店铺吸引力。",
                "A small white rabbit made of plush, a tiny jade pestle cradled in its arms.\n\n" +
                "You Yang Za Zu says: in the moon there is a cassia tree; beneath the tree sits a jade rabbit, pounding medicine without rest.\n\n" +
                "It has pounded medicine for a thousand years, only to ask one question: how have you been, mortal world?\n\n" +
                "— Displayed in your shop, it raises your shop's attractiveness.");
            }
        }

        public const string MidautumnLanternId = "be_midautumn_lantern";
        public static string MidautumnLanternName
        {
            get { return Lang.T("花好月圆灯", "Blossom-Full Moon-Round Lantern"); }
        }
        public const string MidautumnLanternPngName = "花好月圆灯";
        public const string MidautumnLanternSpriteKey = "be_midautumn_lantern";
        public const string MidautumnLanternResourceName = "BrewingExpansion.Textures.花好月圆灯.png";
        public static string MidautumnLanternDescription
        {
            get
            {
                return Lang.T(
                "六角宫灯，灯身描着缠枝桂花，灯穗垂成流苏。\n\n" +
                "「花好月圆」是中国人对中秋最朴素的祝愿——花正盛，月正圆，人正在身边。\n\n" +
                "老人说，从前的人提着这样的灯走夜路，走得再远，也认得回家的方向。\n\n" +
                "——摆放在店铺里，可提升店铺吸引力。",
                "A six-sided palace lantern, its body painted with entwining cassia branches, its tassels dripping like fringe.\n\n" +
                "\"Blossoms in full, moon at its round\" — the most humble wish the Chinese hold for the Mid-Autumn — flowers blooming, moon full, loved ones still near.\n\n" +
                "The old ones said: in days past, people walked the night carrying lanterns like this. However far they traveled, they still knew the road home.\n\n" +
                "— Displayed in your shop, it raises your shop's attractiveness.");
            }
        }


        public const string MidautumnMooncakeBoxId = "be_midautumn_mooncake_box";
        public static string MidautumnMooncakeBoxName
        {
            get { return Lang.T("月团礼盒", "Moon-Cake Gift Box"); }
        }
        public const string MidautumnMooncakeBoxPngName = "月团礼盒";
        public const string MidautumnMooncakeBoxSpriteKey = "be_midautumn_mooncake_box";
        public const string MidautumnMooncakeBoxResourceName = "BrewingExpansion.Textures.月团礼盒.png";
        public static string MidautumnMooncakeBoxDescription
        {
            get
            {
                return Lang.T(
                "朱红漆盒，描着银线的桂树与月轮。掀开盒盖，五仁的香气便涌出来。\n\n" +
                "「月团」是月饼的古称。唐人将圆月揉进面团，做成一口大小的相思。\n\n" +
                "双击打开，得一枚五仁月饼。",
                "A cinnabar-red lacquer box, painted in silver veins with a cassia tree and the moon's disc. Lift the lid, and the fragrance of five kernels comes rushing out.\n\n" +
                "\"Moon-cake\" — yuetuan — was the cake's ancient name. The people of Tang kneaded the round moon into dough, shaping it into palm-sized pieces of longing.\n\n" +
                "Double-click to open — receive one Five-Kernel Mooncake.");
            }
        }
        public const string MidautumnMooncakeBoxTag = "BE_MIDAUTUMN_MOONCAKE_BOX";

        public const string MidautumnBagId = "be_midautumn_bag";
        public static string MidautumnBagName
        {
            get { return Lang.T("玉兔怀袖", "Jade Rabbit in the Sleeve"); }
        }
        public const string MidautumnBagPngName = "玉兔怀袖";
        public const string MidautumnBagSpriteKey = "be_midautumn_bag";
        public const string MidautumnBagResourceName = "BrewingExpansion.Textures.玉兔怀袖.png";
        public static string MidautumnBagDescription
        {
            get
            {
                return Lang.T(
                "奶茶色的斜挎包，浅棕色的包边，像一块刚出炉的月饼。\n\n" +
                "搭扣是一只小小的白兔，静静地卧在圆月旁边。\n\n" +
                "「怀袖」二字出自古诗——「置书怀袖中，三岁字不灭」。\n\n" +
                "背着它出门，就像把一小片中秋的夜色带在身上。\n\n" +
                "——中秋盲盒中最稀有的一件。",
                "A milk-tea-colored crossbody bag, edged in pale brown — like a freshly-baked mooncake.\n\n" +
                "The clasp is a small white rabbit, resting quietly beside the full moon.\n\n" +
                "\"Cherished in the sleeve\" — from an old poem: \"Placed the letter in my sleeve; three years, and the words have not faded.\"\n\n" +
                "Wear it out, and it is as though you carry a small patch of Mid-Autumn night upon you.\n\n" +
                "— The rarest piece in the Mid-Autumn blindbox.");
            }
        }

        public const string WurenMooncakeId = "be_wuren_mooncake";
        public static string WurenMooncakeName
        {
            get { return Lang.T("五仁月饼", "Five-Kernel Mooncake"); }
        }
        public const string WurenMooncakePngName = "五仁月饼";
        public const string WurenMooncakeSpriteKey = "be_wuren_mooncake";
        public const string WurenMooncakeResourceName = "BrewingExpansion.Textures.五仁月饼.png";
        public static string WurenMooncakeDescription
        {
            get
            {
                return Lang.T(
                "皮薄馅足，五种坚果蜜饯裹在一起。\n\n" +
                "有人爱它，有人恨它——但中秋的桌上，总要有这么一块。\n\n" +
                "咬开它，咬开的是一整个童年的中秋夜。",
                "Thin crust, generous filling — five kinds of nuts and candied fruits pressed together.\n\n" +
                "Some love it, some hate it — yet upon every Mid-Autumn table, there must be one.\n\n" +
                "Bite into it, and what you bite into is a whole childhood's Mid-Autumn night.");
            }
        }
        public const int WurenMooncakePrice = 815;

        public const string MidautumnBlindboxId = "be_midautumn_blindbox";
        public static string MidautumnBlindboxName
        {
            get { return Lang.T("中秋盲盒「限定」", "Mid-Autumn Blindbox (Limited)"); }
        }
        public const string MidautumnBlindboxPngName = "中秋盲盒「限定」";
        public const string MidautumnBlindboxSpriteKey = "be_midautumn_blindbox";
        public const string MidautumnBlindboxResourceName = "BrewingExpansion.Textures.中秋盲盒「限定」.png";
        public static string MidautumnBlindboxDescription
        {
            get
            {
                return Lang.T(
                "一只八角形的礼盒，盒面是深蓝的夜色，上面浮着一轮金黄的圆月。\n\n" +
                "月亮旁边，一只小小的玉兔正俯身看着人间。\n\n" +
                "盒盖用一条红绳系着，绳结打成团圆的模样。\n\n" +
                "打开它，可能是一柄折扇，可能是一盏灯，也可能是一盒月饼。\n\n" +
                "——其中的中秋装饰，能提升店铺的吸引力。\n" +
                "花好月圆之夜，谁不愿多走几步，来你这儿看看月亮？",
                "An octagonal gift box, its face a deep-blue night, upon which a golden moon floats.\n\n" +
                "Beside the moon, a small jade rabbit leans down, gazing at the mortal world.\n\n" +
                "The lid is bound with a red cord, the knot tied in the shape of reunion.\n\n" +
                "Open it — perhaps a folding fan, perhaps a lantern, perhaps a box of mooncakes.\n\n" +
                "— The Mid-Autumn decorations within can raise your shop's attractiveness.\n" +
                "On a night of blossoms and full moon, who would not walk a few extra steps to come see the moon with you?");
            }
        }
        public const int MidautumnBlindboxPrice = 4925;
        public const string MidautumnBlindboxTag = "BE_MIDAUTUMN_BLINDBOX";
        // ★ 中秋限定：月华桂 + 玉兔衔桂
        public const string MidautumnMoonOsmanthusId = "be_midautumn_moon_osmanthus";
        public static string MidautumnMoonOsmanthusName
        {
            get { return Lang.T("月华桂", "Moonlit Osmanthus"); }
        }
        public const string MidautumnMoonOsmanthusPngName = "月华桂";
        public const string MidautumnMoonOsmanthusSpriteKey = "be_midautumn_moon_osmanthus";
        public const string MidautumnMoonOsmanthusResourceName = "BrewingExpansion.Textures.月华桂.png";
        public static string MidautumnMoonOsmanthusDescription
        {
            get
            {
                return Lang.T(
                "中秋夜，满月升到中天。桂树的每一片叶子、每一粒花瓣，都被月光浸透了。\n\n" +
                "花瓣上凝着一层银白的微光，像把整个秋夜都收进了心里。\n\n" +
                "摘花的人说，这一株桂，是替月亮开的。",
                "On Mid-Autumn night, when the full moon climbs to its zenith, every leaf and every petal of the cassia tree is steeped in moonlight.\n\n" +
                "A silver sheen of light gathers on the blossoms — as if the whole autumn night had been gathered into one heart.\n\n" +
                "The one who picked these flowers said: this cassia tree blooms for the moon alone.");
            }
        }

        public const string MidautumnOsmanthusWineId = "be_midautumn_osmanthus_wine";
        public static string MidautumnOsmanthusWineName
        {
            get { return Lang.T("玉兔衔桂", "Jade Rabbit Carries the Osmanthus"); }
        }
        public const string MidautumnOsmanthusWinePngName = "玉兔衔桂";
        public const string MidautumnOsmanthusWineSpriteKey = "be_midautumn_osmanthus_wine";
        public const string MidautumnOsmanthusWineResourceName = "BrewingExpansion.Textures.玉兔衔桂.png";
        public static string MidautumnOsmanthusWineDescription
        {
            get
            {
                return Lang.T(
                "以月华桂与月华桂同酿——不掺凡间一粒尘。\n\n" +
                "酒液金黄，盛在杯里像盛了一小片秋天。\n\n" +
                "杯底沉着几粒桂花，据说饮到最后，能看见一只小兔子，从月亮上俯下身来。\n\n" +
                "「桂子月中落，天香云外飘」——说的就是这一杯。",
                "Brewed from Moonlit Osmanthus paired with Moonlit Osmanthus — not a grain of mortal dust stirred in.\n\n" +
                "The wine is golden; poured into a cup it holds a small patch of autumn.\n\n" +
                "A few osmanthus petals settle at the bottom. It is said that to drink to the last drop is to see a small rabbit, leaning down from the moon.\n\n" +
                "\"Cassia seeds fall from the moon; heavenly fragrance drifts beyond the clouds\" — this is that cup.");
            }
        }
        public const int MidautumnOsmanthusWinePrice = 815;

        // ★ 收藏酒系列（盲盒专属）
        public const string ShuangqingWineId = "be_shuangqing_wine";
        public static string ShuangqingWineName
        {
            get { return Lang.T("双清酿", "Twin Purity Brew"); }
        }
        public const string ShuangqingWinePngName = "双清酿";
        public const string ShuangqingWineSpriteKey = "be_shuangqing_wine";
        public const string ShuangqingWineResourceName = "BrewingExpansion.Textures.双清酿.png";
        public static string ShuangqingWineDescription
        {
            get
            {
                return Lang.T(
                "梅竹双清，本是文人画里最常见的题材。\n\n" +
                "竹有节，宁折不弯；梅有骨，凌寒独开。\n\n" +
                "一个不肯低头，一个不肯认输。\n\n" +
                "酿成酒，入口是竹叶的清苦，回味是梅花的暗香。\n\n" +
                "喝的人说，这酒喝的不是味道，是一口气。\n\n" +
                "——Dream",
                "Plum and bamboo, twin purities — the most familiar subject in literati painting.\n\n" +
                "The bamboo has its joints; it would rather break than bend. The plum has its bones; it blooms alone in the cold.\n\n" +
                "One refuses to bow. One refuses to yield.\n\n" +
                "Brewed into wine, the first sip is the green bitterness of bamboo leaves; the aftertaste is the hidden fragrance of plum blossom.\n\n" +
                "Drinkers say this wine is not about flavor. It is about a breath — a whole life's worth of one.\n\n" +
                "— Dream");
            }
        }

        public const string NanfengWineId = "be_nanfeng_wine";
        public static string NanfengWineName
        {
            get { return Lang.T("南风旧酿", "Old South Wind Brew"); }
        }
        public const string NanfengWinePngName = "南风旧酿";
        public const string NanfengWineSpriteKey = "be_nanfeng_wine";
        public const string NanfengWineResourceName = "BrewingExpansion.Textures.南风旧酿.png";
        public static string NanfengWineDescription
        {
            get
            {
                return Lang.T(
                "开坛时有海风与椰香。\n\n" +
                "适合故人重逢，也适合独自喝完。\n\n" +
                "——痣",
                "Sea breeze and coconut scent when the jar is opened.\n\n" +
                "Fit for old friends meeting again — and for finishing alone.\n\n" +
                "— Zhi");
            }
        }

        public const string FeimeiWineId = "be_feimei_wine";
        public static string FeimeiWineName
        {
            get { return Lang.T("绯莓春酿", "Crimson Berry Spring Brew"); }
        }
        public const string FeimeiWinePngName = "绯莓春酿";
        public const string FeimeiWineSpriteKey = "be_feimei_wine";
        public const string FeimeiWineResourceName = "BrewingExpansion.Textures.绯莓春酿.png";
        public static string FeimeiWineDescription
        {
            get
            {
                return Lang.T(
                "草莓是春天最柔软的那一口甜。\n\n" +
                "红得张扬，甜得直接，不掺任何杂质。\n\n" +
                "酿成酒，颜色像被春日的晚霞染过。\n\n" +
                "调酒师说，这酒不讲道理——它就是甜，就是要让人想起某个春天的下午，和某个笑得很好看的人。\n\n" +
                "——x",
                "Strawberry — spring's softest bite of sweetness.\n\n" +
                "Red with abandon, sweet with directness, no impurity mixed in.\n\n" +
                "Brewed into wine, its color seems dyed by a spring evening's sunset.\n\n" +
                "The bartender says this wine does not argue — it simply is sweet; it simply makes you remember a certain spring afternoon, and someone who smiled beautifully.\n\n" +
                "— x");
            }
        }

        public const string NeonIllusionWineId = "be_neon_illusion_wine";
        public static string NeonIllusionWineName
        {
            get { return Lang.T("霓虹幻梦", "Neon Illusion"); }
        }
        public const string NeonIllusionWinePngName = "霓虹幻梦";
        public const string NeonIllusionWineSpriteKey = "be_neon_illusion_wine";
        public const string NeonIllusionWineResourceName = "BrewingExpansion.Textures.霓虹幻梦.png";
        public static string NeonIllusionWineDescription
        {
            get
            {
                return Lang.T(
                "霓虹灯在城市上空亮起，红的、蓝的、紫的，像一场醒不过来的梦。\n\n" +
                "酒液在杯中折射出诡异的光，一杯下去，你会看见未来。\n\n" +
                "不过调酒师警告说：看见的，不一定是真的。\n\n" +
                "霓虹的尽头，是清醒，还是更深的迷幻？\n\n" +
                "——Nigzer",
                "Neon lights blaze above the city — red, blue, violet — like a dream one cannot wake from.\n\n" +
                "The wine refracts uncanny light in the glass. Drink one cup, and you will see the future.\n\n" +
                "But the bartender warns: what you see may not be true.\n\n" +
                "At the end of the neon — is it clarity, or a deeper fog?\n\n" +
                "— Nigzer");
            }
        }

        public const string IdealEndWineId = "be_ideal_end_wine";
        public static string IdealEndWineName
        {
            get { return Lang.T("理想的尽头", "The End of Ideals"); }
        }
        public const string IdealEndWinePngName = "理想的尽头";
        public const string IdealEndWineSpriteKey = "be_ideal_end_wine";
        public const string IdealEndWineResourceName = "BrewingExpansion.Textures.理想的尽头.png";
        public static string IdealEndWineDescription
        {
            get
            {
                return Lang.T(
                "原料为产自无名教堂废墟上的紫荆花丛，入口甘甜无比但带有莫名的回苦，酿造人与配方在内战中化作灰烬，一如她那未竟之梦。\n\n" +
                "——TYskyrim_***",
                "Brewed from redbud flowers growing upon the ruins of a nameless church. Sweetness beyond compare upon entry, with an inexplicable bitterness in return. The brewer and the recipe were reduced to ash in the civil war — as was her unfinished dream.\n\n" +
                "— TYskyrim_***");
            }
        }

        public const string LuofuWineId = "be_luofu_dream_wine";
        public static string LuofuWineName
        {
            get { return Lang.T("罗浮梦", "The Luofu Dream"); }
        }
        public const string LuofuWinePngName = "罗浮梦";
        public const string LuofuWineSpriteKey = "be_luofu_dream_wine";
        public const string LuofuWineResourceName = "BrewingExpansion.Textures.罗浮梦.png";
        public static string LuofuWineDescription
        {
            get
            {
                return Lang.T(
                "尝闻有地名为『罗浮』，凡失意者于其梅下醉卧，梦中皆得所求。惜好梦易醒，终一场镜花水月…\n\n" +
                "——玄宇",
                "I have heard of a place called Luofu — where the disheartened lie down drunk beneath its plum trees, and in their dreams, all they long for comes to pass. Yet a good dream is easily broken, and at the end — only flowers in the mirror, moon in the water.\n\n" +
                "— Xuan Yu");
            }
        }

        public const string MagicPigWineId = "be_magic_pig_wine";
        public static string MagicPigWineName
        {
            get { return Lang.T("神奇猪猪", "Magic Piggy"); }
        }
        public const string MagicPigWinePngName = "神奇猪猪";
        public const string MagicPigWineSpriteKey = "be_magic_pig_wine";
        public const string MagicPigWineResourceName = "BrewingExpansion.Textures.神奇猪猪.png";
        public static string MagicPigWineDescription
        {
            get
            {
                return Lang.T(
                "猪才是这个世界上最可爱的生物！\n\n" +
                "——#",
                "The pig is the most adorable creature in the world!\n\n" +
                "— #");
            }
        }

        public const string SeaBlueWineId = "be_sea_blue_wine";
        public static string SeaBlueWineName
        {
            get { return Lang.T("海蓝幻梦", "Sea-Blue Illusion"); }
        }
        public const string SeaBlueWinePngName = "海蓝幻梦";
        public const string SeaBlueWineSpriteKey = "be_sea_blue_wine";
        public const string SeaBlueWineResourceName = "BrewingExpansion.Textures.海蓝幻梦.png";
        public static string SeaBlueWineDescription
        {
            get
            {
                return Lang.T(
                "海是什么颜色的？\n\n" +
                "有人说蓝，有人说绿，有人说它根本没有颜色，只是反射了天空。\n\n" +
                "这瓶酒也一样——在不同的光线下，它会呈现不同的蓝。\n\n" +
                "喝下去，像喝下一整片海。\n\n" +
                "调酒师说，这酒适合一个人喝，因为大海从来都是孤独的。\n\n" +
                "——一切随缘",
                "What color is the sea?\n\n" +
                "Some say blue, some say green, some say it has no color at all — that it merely reflects the sky.\n\n" +
                "This wine is the same — under different light, it shows different blues.\n\n" +
                "Drink it down, and it is like drinking a whole sea.\n\n" +
                "The bartender says this wine is for drinking alone, because the sea is always lonely.\n\n" +
                "— Let It Be");
            }
        }

        public const string NianHeshiWineId = "be_nian_heshi_wine";
        public static string NianHeshiWineName => Lang.T("念何事", "What Was I Thinking?");
        public const string NianHeshiWinePngName = "念何事";
        public const string NianHeshiWineSpriteKey = "be_nian_heshi_wine";
        public const string NianHeshiWineResourceName = "BrewingExpansion.Textures.念何事.png";
        public const int NianHeshiWinePrice = 250;
        public static string NianHeshiWineDescription => Lang.T(
            "酿酒的学问从来容不下自负。这名年长的匠人浸淫酿酒数年，认定自己早已吃透所有发酵规律，不屑查阅笔记、不屑亲自尝试，觉得自己什么都知道，于是凭着盲目的自信大刀阔斧地重写配方。\n\n" +
            "他坚信自己的改良会诞生传世佳酿，可傲慢蒙蔽了判断，发酵彻底失控，混合物发生了无法预料的异变，没有醇香，只生成了这瓶不可描述的可怕酒。\n\n" +
            "酒体浑浊暗沉，气味混杂着酸腐与刺鼻的怪味。这瓶独特的酒也留在了历史的长河里，更印证一件事：就算懂得再多，也不能轻视酿造的力量。\n\n" +
            "最后这位神秘人只留下一句话:为什么桂花加荔枝酿出来了荔枝酒————1.0版本小彩蛋",
            "Brewing has never had room for arrogance. After years at his craft, an aging brewer believed he had mastered every rule of fermentation. He scorned his notes and refused to test his ideas. Certain he knew everything, he rewrote the recipe with blind confidence.\n\n" +
            "He was sure his changes would yield a legendary wine. Pride clouded his judgment instead: fermentation ran completely out of control, and the mixture changed in ways no one could predict. It produced no mellow aroma, only this indescribably dreadful wine.\n\n" +
            "The wine is murky and dark, with a smell of sour rot and something sharply strange. It too found a place in history, proving that however much one knows, one must never underestimate the power of brewing.\n\n" +
            "The mysterious brewer left just one question: Why did osmanthus and lychee turn into lychee wine? — A little Easter egg from version 1.0.");

        public const string RainbowRecipeWineId = "be_rainbow_recipe_wine";
        public static string RainbowRecipeWineName => Lang.T("彩虹配方", "Rainbow Recipe");
        public const string RainbowRecipeWinePngName = "彩虹配方";
        public const string RainbowRecipeWineSpriteKey = "be_rainbow_recipe_wine";
        public const string RainbowRecipeWineResourceName = "BrewingExpansion.Textures.彩虹配方.png";
        public static string RainbowRecipeWineDescription => Lang.T(
            "敬为酿酒mod制作了两款优质拓展的hollow。",
            "A toast to hollow, who made two excellent expansions for the brewing mod.");

        public const int CollectorWinePrice = 666;

        // ★ 樱雪囊（樱花挎包）
        public const string SakuraBagId = "be_sakura_bag";
        public static string SakuraBagName
        {
            get { return Lang.T("樱雪囊", "Sakura Snow Pouch"); }
        }
        public const string SakuraBagPngName = "樱雪囊";
        public const string SakuraBagSpriteKey = "be_sakura_bag";
        public const string SakuraBagResourceName = "BrewingExpansion.Textures.樱雪囊.png";
        public static string SakuraBagDescription
        {
            get
            {
                return Lang.T(
                "粉白色的斜挎包，缎面柔润如初雪。\n\n" +
                "包身上绣着一枝樱花，花瓣正纷纷落下——那是它最美的时刻。\n\n" +
                "樱花只开七日。开时极盛，落时极快。古人说，樱花之美，美在懂得告别。\n\n" +
                "背上它，就像把一整个春天，带在身边。\n\n" +
                "——高级盲盒中最温柔的一件。",
                "A pink-white crossbody bag, its satin surface soft as first snow.\n\n" +
                "A branch of sakura is embroidered on the body — petals falling one by one, which is when it is most beautiful.\n\n" +
                "Sakura blooms for only seven days. It blooms with abandon, and falls quickly. The ancients said sakura's beauty lies in knowing how to say goodbye.\n\n" +
                "Wear it, and it is like carrying a whole spring at your side.\n\n" +
                "— The gentlest piece in the Premium blindbox.");
            }
        }
        public const int SakuraBagPrice = 2525;

        // 凡间盲盒沿用原低级盲盒的 ID 和贴图。
        public const string LowBlindboxId = "be_low_blindbox";
        public static string LowBlindboxName
        {
            get { return Lang.T("凡间盲盒", "Mortal Blindbox"); }
        }
        public const string LowBlindboxPngName = "低级盲盒";
        public const string LowBlindboxSpriteKey = "be_low_blindbox";
        public const string LowBlindboxResourceName = "BrewingExpansion.Textures.低级盲盒.png";
        public static string LowBlindboxDescription
        {
            get
            {
                return Lang.T(
                "浅绿色的盒子里装着刚采下的种子。\n\n双击打开，获得两颗凡间种子；每种种子等概率。",
                "A pale-green box filled with freshly gathered seeds.\n\nDouble-click to receive two Mortal seeds. Each seed in this rarity is equally likely.");
            }
        }
        public const int LowBlindboxPrice = 100;
        public const string LowBlindboxTag = "BE_LOW_BLINDBOX";

        // 远来盲盒沿用原中级盲盒的 ID 和贴图。
        public const string MidBlindboxId = "be_mid_blindbox";
        public static string MidBlindboxName
        {
            get { return Lang.T("远来盲盒", "From Afar Blindbox"); }
        }
        public const string MidBlindboxPngName = "中级盲盒";
        public const string MidBlindboxSpriteKey = "be_mid_blindbox";
        public const string MidBlindboxResourceName = "BrewingExpansion.Textures.中级盲盒.png";
        public static string MidBlindboxDescription
        {
            get
            {
                return Lang.T(
                "深蓝色的盒子里封着远来的花与果。\n\n双击打开，获得两颗远来种子；每种种子等概率。",
                "A deep-blue box holding seeds from afar.\n\nDouble-click to receive two From Afar seeds. Each seed in this rarity is equally likely.");
            }
        }
        public const int MidBlindboxPrice = 300;
        public const string MidBlindboxTag = "BE_MID_BLINDBOX";

        // ★ 收藏酒盲盒
        public const string WineBlindboxId = "be_wine_blindbox";
        public static string WineBlindboxName
        {
            get { return Lang.T("收藏酒盲盒", "Collector's Wine Blindbox"); }
        }
        public const string WineBlindboxPngName = "收藏酒盲盒";
        public const string WineBlindboxSpriteKey = "be_wine_blindbox";
        public const string WineBlindboxResourceName = "BrewingExpansion.Textures.收藏酒盲盒.png";
        public static string WineBlindboxDescription
        {
            get
            {
                return Lang.T(
                "一只青色的小方盒，颜色介于竹叶与远山之间。\n\n" +
                "盒身温润如玉，打开时有一股淡淡的酒香。\n\n" +
                "里面装的是一瓶酒——不是为了喝，是为了记住。\n\n" +
                "「有些酒不是为了喝，是为了记住。」\n\n" +
                "——收藏家的盲盒。",
                "A small jade-green box, its color sitting between bamboo leaf and distant mountain.\n\n" +
                "The box is smooth as warm jade; when opened, a faint scent of wine drifts out.\n\n" +
                "Inside is a bottle of wine — not for drinking, but for remembering.\n\n" +
                "\"Some wines are not for drinking. They are for remembering.\"\n\n" +
                "— The collector's blindbox.");
            }
        }
        public const int WineBlindboxPrice = 2333;
        public const string WineBlindboxTag = "BE_WINE_BLINDBOX";

        public const string ContainerBlindboxId = "be_container_blindbox";
        public const string ContainerBlindboxSpriteKey = "be_container_blindbox";
        public const string ContainerBlindboxPngName = "容器盲盒";
        public const string ContainerBlindboxResourceName = "BrewingExpansion.Textures.容器盲盒.png";
        public const string ContainerBlindboxTag = "BE_CONTAINER_BLINDBOX";
        public const int ContainerBlindboxPrice = 1000;
        public static string ContainerBlindboxName => Lang.T("容器盲盒", "Container Blindbox");
        public static string ContainerBlindboxDescription => Lang.T(
            "双击打开，随机获得一件容器：牢力士 25%、电子幽灵 15%、樱雪囊 5%、猫又又小挎包 5%、xuxu暖心杯 30%、xuxu甜心杯 15%、四十七爱心杯 5%。",
            "Double-click to receive one container: Laolishi 25%, Electric Ghost 15%, Sakura Satchel 5%, Cat Youyou Satchel 5%, xuxu Warm Cup 30%, xuxu Sweet Cup 15%, or Forty-Seven's Heart Cup 5%.");

        public const string FigurineBlindboxId = "be_figurine_blindbox";
        public const string FigurineBlindboxSpriteKey = "be_figurine_blindbox";
        public const string FigurineBlindboxPngName = "手办盲盒";
        public const string FigurineBlindboxResourceName = "BrewingExpansion.Textures.手办盲盒.png";
        public const string FigurineBlindboxTag = "BE_FIGURINE_BLINDBOX";
        public const int FigurineBlindboxPrice = 5000;
        public static string FigurineBlindboxName => Lang.T("手办盲盒", "Figurine Blindbox");
        public static string FigurineBlindboxDescription => Lang.T(
            "双击打开，等概率获得黑崎一护、朽木露琪亚、石田雨龙或噢我的天啊是铱星大海参手办。每种手办拥有一件即可增加 100 点店铺吸引力，同款不叠加。",
            "Double-click for one of four equally likely figurines: Ichigo Kurosaki, Rukia Kuchiki, Uryu Ishida, or Oh My God, It's an Iridium Sea Cucumber. Each distinct figurine adds 100 shop attractiveness; duplicates do not stack.");

        public const string CassetteBundleId = "be_xuxu_cassette_bundle";
        public const string CassetteBundleSpriteKey = "be_xuxu_cassette_bundle";
        public const string CassetteBundlePngName = "xuxu磁带捆绑包";
        public const string CassetteBundleResourceName = "BrewingExpansion.Textures.xuxu磁带捆绑包.png";
        public const string CassetteBundleTag = "BE_XUXU_CASSETTE_BUNDLE";
        public const int CassetteBundlePrice = 700;
        public static string CassetteBundleName => Lang.T("xuxu磁带捆绑包", "xuxu Cassette Bundle");
        public static string CassetteBundleDescription => Lang.T(
            "双击打开，一次获得酒吧特调、霓虹街道、微醺之夜、弦间微风、酿酒有嘻哈五盘磁带。放入磁带播放器，即可播放对应的音乐。",
            "Double-click to receive all five cassettes: Bar Special, Neon Street, Tipsy Night, Breeze Between Strings, and Brewing Hip-Hop. Insert one into a cassette player to hear its music.");

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
        // ★ 电子幽灵（赛博朋克腰包）
        public const string CyberBagId = "be_cyber_bag";
        public static string CyberBagName
        {
            get { return Lang.T("电子幽灵", "Electric Ghost"); }
        }
        public const string CyberBagPngName = "电子幽灵";
        public const string CyberBagSpriteKey = "be_cyber_bag";
        public const string CyberBagResourceName = "BrewingExpansion.Textures.电子幽灵.png";
        public static string CyberBagDescription
        {
            get
            {
                return Lang.T(
                "深灰色的机能腰包，表面覆着一层哑光的碳纤维纹路。\n\n" +
                "拉链头上挂着一枚小小的电子标签，蓝色微光时明时灭，像一颗不会停止跳动的心脏。\n\n" +
                "据说它原先属于一位夜之城的信使——那人把所有的秘密都藏在腰间，直到某天连人带包消失在霓虹里。\n\n" +
                "现在它归你了。至于里面还能装下多少故事，看你自己。",
                "A dark-gray tactical waist bag, its surface covered in matte carbon fiber weave.\n\n" +
                "A small electronic tag hangs from the zipper — its blue light flickering on and off, like a heart that will not stop beating.\n\n" +
                "They say it once belonged to a night-city courier — one who kept every secret at his waist, until one day both man and bag vanished into the neon.\n\n" +
                "Now it is yours. As for how many more stories it can hold — that is for you to decide.");
            }
        }
        public const int CyberBagPrice = 800;

        // ★ 牢力士（搞笑包）
        public const string LaolishiBagId = "be_laolishi_bag";
        public static string LaolishiBagName
        {
            get { return Lang.T("牢力士", "Laolishi"); }
        }
        public const string LaolishiBagPngName = "牢力士";
        public const string LaolishiBagSpriteKey = "be_laolishi_bag";
        public const string LaolishiBagResourceName = "BrewingExpansion.Textures.牢力士.png";
        public static string LaolishiBagDescription
        {
            get
            {
                return Lang.T(
                "坐牢的时候用尽全力去拉士。",
                "In prison, pull the warrior with all your might.");
            }
        }
        public const int LaolishiBagPrice = 500;

        public const string CatYouyouBagId = "be_cat_yoyou_satchel";
        public const string CatYouyouBagSpriteKey = "be_cat_yoyou_satchel";
        public const string CatYouyouBagPngName = "猫又又小挎包";
        public const string CatYouyouBagResourceName = "BrewingExpansion.Textures.猫又又小挎包.png";
        public const int CatYouyouBagPrice = 1107;
        public static string CatYouyouBagName => Lang.T("猫又又小挎包", "Cat Youyou Satchel");
        public static string CatYouyouBagDescription => Lang.T(
            "据说，这个挎包是在危险来临的时候挡在大家身前的天使留下的物品。",
            "They say this satchel was left behind by an angel who stood before everyone when danger came.");

        // ★ xuxu初级电池
        public const string XuxuBatteryId = "be_xuxu_basic_battery";
        public static string XuxuBatteryName
        {
            get { return Lang.T("xuxu初级电池", "xuxu's Basic Battery"); }
        }
        public const string XuxuBatteryPngName = "xuxu初级电池";
        public const string XuxuBatterySpriteKey = "be_xuxu_basic_battery";
        public const string XuxuBatteryResourceName = "BrewingExpansion.Textures.xuxu初级电池.png";
        public static string XuxuBatteryDescription
        {
            get
            {
                return Lang.T(
                "xuxu的强化之路的第一个尝试，耐久200，每晚回复6kj，用就完了。",
                "xuxu's first attempt upon his path of enhancement. 200 durability, restores 6 kJ each night. Just use it.");
            }
        }
        public const int XuxuBatteryPrice = 800;
        // ★ 5 个电量状态贴图（4格 → 0格）
        public const string XuxuBatteryPngName4 = "xuxu初级电池1";
        public const string XuxuBatterySpriteKey4 = "be_xuxu_basic_battery_4";
        public const string XuxuBatteryResourceName4 = "BrewingExpansion.Textures.xuxu初级电池1.png";
        public const string XuxuBatteryPngName3 = "xuxu初级电池2";
        public const string XuxuBatterySpriteKey3 = "be_xuxu_basic_battery_3";
        public const string XuxuBatteryResourceName3 = "BrewingExpansion.Textures.xuxu初级电池2.png";
        public const string XuxuBatteryPngName2 = "xuxu初级电池3";
        public const string XuxuBatterySpriteKey2 = "be_xuxu_basic_battery_2";
        public const string XuxuBatteryResourceName2 = "BrewingExpansion.Textures.xuxu初级电池3.png";
        public const string XuxuBatteryPngName1 = "xuxu初级电池4";
        public const string XuxuBatterySpriteKey1 = "be_xuxu_basic_battery_1";
        public const string XuxuBatteryResourceName1 = "BrewingExpansion.Textures.xuxu初级电池4.png";
        public const string XuxuBatteryPngName0 = "xuxu初级电池5";
        public const string XuxuBatterySpriteKey0 = "be_xuxu_basic_battery_0";
        public const string XuxuBatteryResourceName0 = "BrewingExpansion.Textures.xuxu初级电池5.png";

        // ★ xuxu中级电池
        public const string XuxuMidBatteryId = "be_xuxu_mid_battery";
        public static string XuxuMidBatteryName
        {
            get { return Lang.T("xuxu中级电池", "xuxu's Intermediate Battery"); }
        }
        public const string XuxuMidBatteryPngName = "xuxu中级电池";
        public const string XuxuMidBatterySpriteKey = "be_xuxu_mid_battery";
        public const string XuxuMidBatteryResourceName = "BrewingExpansion.Textures.xuxu中级电池.png";
        public const string XuxuMidBatteryPngName4 = "xuxu中级电池1";
        public const string XuxuMidBatterySpriteKey4 = "be_xuxu_mid_battery_4";
        public const string XuxuMidBatteryResourceName4 = "BrewingExpansion.Textures.xuxu中级电池1.png";
        public const string XuxuMidBatteryPngName3 = "xuxu中级电池2";
        public const string XuxuMidBatterySpriteKey3 = "be_xuxu_mid_battery_3";
        public const string XuxuMidBatteryResourceName3 = "BrewingExpansion.Textures.xuxu中级电池2.png";
        public const string XuxuMidBatteryPngName2 = "xuxu中级电池3";
        public const string XuxuMidBatterySpriteKey2 = "be_xuxu_mid_battery_2";
        public const string XuxuMidBatteryResourceName2 = "BrewingExpansion.Textures.xuxu中级电池3.png";
        public const string XuxuMidBatteryPngName1 = "xuxu中级电池4";
        public const string XuxuMidBatterySpriteKey1 = "be_xuxu_mid_battery_1";
        public const string XuxuMidBatteryResourceName1 = "BrewingExpansion.Textures.xuxu中级电池4.png";
        public const string XuxuMidBatteryPngName0 = "xuxu中级电池5";
        public const string XuxuMidBatterySpriteKey0 = "be_xuxu_mid_battery_0";
        public const string XuxuMidBatteryResourceName0 = "BrewingExpansion.Textures.xuxu中级电池5.png";
        public static string XuxuMidBatteryDescription
        {
            get
            {
                return Lang.T(
                "xuxu的第二代作品，电量上限 350kJ，每晚自动回复8kj。",
                "xuxu's second-generation work. Max energy 350 kJ, restores 8 kJ each night.");
            }
        }
        public const int XuxuMidBatteryPrice = 1200;
        public const int XuxuMidBatteryMaxEnergy = 350;

        // ★ xuxu高级电池
        public const string XuxuHighBatteryId = "be_xuxu_high_battery";
        public static string XuxuHighBatteryName
        {
            get { return Lang.T("xuxu高级电池", "xuxu's Advanced Battery"); }
        }
        public const string XuxuHighBatteryPngName = "xuxu高级电池";
        public const string XuxuHighBatterySpriteKey = "be_xuxu_high_battery";
        public const string XuxuHighBatteryResourceName = "BrewingExpansion.Textures.xuxu高级电池.png";
        public const string XuxuHighBatteryPngName4 = "xuxu高级电池1";
        public const string XuxuHighBatterySpriteKey4 = "be_xuxu_high_battery_4";
        public const string XuxuHighBatteryResourceName4 = "BrewingExpansion.Textures.xuxu高级电池1.png";
        public const string XuxuHighBatteryPngName3 = "xuxu高级电池2";
        public const string XuxuHighBatterySpriteKey3 = "be_xuxu_high_battery_3";
        public const string XuxuHighBatteryResourceName3 = "BrewingExpansion.Textures.xuxu高级电池2.png";
        public const string XuxuHighBatteryPngName2 = "xuxu高级电池3";
        public const string XuxuHighBatterySpriteKey2 = "be_xuxu_high_battery_2";
        public const string XuxuHighBatteryResourceName2 = "BrewingExpansion.Textures.xuxu高级电池3.png";
        public const string XuxuHighBatteryPngName1 = "xuxu高级电池4";
        public const string XuxuHighBatterySpriteKey1 = "be_xuxu_high_battery_1";
        public const string XuxuHighBatteryResourceName1 = "BrewingExpansion.Textures.xuxu高级电池4.png";
        public const string XuxuHighBatteryPngName0 = "xuxu高级电池5";
        public const string XuxuHighBatterySpriteKey0 = "be_xuxu_high_battery_0";
        public const string XuxuHighBatteryResourceName0 = "BrewingExpansion.Textures.xuxu高级电池5.png";
        public static string XuxuHighBatteryDescription
        {
            get
            {
                return Lang.T(
                "xuxu的第三代作品，电量上限 500kJ，每晚恢复12kj",
                "xuxu's third-generation work. Max energy 500 kJ, restores 12 kJ each night.");
            }
        }
        public const int XuxuHighBatteryPrice = 2000;
        public const int XuxuHighBatteryMaxEnergy = 500;

        public const string OwnedTag = "IS_OWNED_TAG";


        public static readonly string[] SeedIds = new string[]
        {
            "chili_seed", "rice_seed", "wheat_seed", "wormwood_seed",
            "sichuan_pepper_seed", "lemon_seed", "yellow_grape_seed", "sugarcane_seed",
            "cucumber_seed",
            "lychee_seed", JasmineSeedId, GreenGrapeSeedId,
            "osmanthus_seed", "bayberry_seed",
            VoidPlumSeedId, StardustBerrySeedId,
        };
        private static string[] _seedNamesCache;
        private static bool _seedNamesCacheIsZH;
        public static string[] SeedNames
        {
            get
            {
                if (_seedNamesCache == null || _seedNamesCacheIsZH != Lang.IsZH)
                {
                    _seedNamesCache = new string[]
                    {
                        Lang.T("辣椒种子", "Chili Seed"),
                        Lang.T("水稻种子", "Rice Seed"),
                        Lang.T("小麦种子", "Wheat Seed"),
                        Lang.T("苦艾草种子", "Wormwood Seed"),
                        Lang.T("花椒种子", "Sichuan Pepper Seed"),
                        Lang.T("柠檬种子", "Lemon Seed"),
                        Lang.T("黄葡萄种子", "Yellow Grape Seed"),
                        Lang.T("甘蔗种子", "Sugarcane Seed"),
                        Lang.T("黄瓜种子", "Cucumber Seed"),
                        Lang.T("荔枝种子", "Lychee Seed"),
                        Lang.T("茉莉种子", "Jasmine Seed"),
                        Lang.T("青提种子", "Green Grape Seed"),
                        Lang.T("桂花种子", "Osmanthus Seed"),
                        Lang.T("杨梅种子", "Bayberry Seed"),
                        Lang.T("虚空李种子", "Void Plum Seed"),
                        Lang.T("星尘莓种子", "Stardust Berry Seed"),
                    };
                    _seedNamesCacheIsZH = Lang.IsZH;
                }
                return _seedNamesCache;
            }
        }
        public static readonly string[] SeedPngNames = new string[]
        {
            "辣椒种子", "水稻种子", "小麦种子", "苦艾草种子",
            "花椒种子", "柠檬种子", "黄葡萄种子", "甘蔗种子",
            "黄瓜种子", "荔枝种子", "茉莉种子", "青提种子",
            "桂花种子", "杨梅种子", "虚空李种子", "星尘莓种子",
        };
        public static readonly string[] SeedSpriteKeys = new string[]
        {
            "be_chili_seed", "be_rice_seed", "be_wheat_seed", "be_wormwood_seed",
            "be_sichuan_pepper_seed", "be_lemon_seed", "be_yellow_grape_seed", "be_sugarcane_seed",
            "be_cucumber_seed", "be_lychee_seed", "be_jasmine_seed", "be_green_grape_seed",
            "be_osmanthus_seed", "be_bayberry_seed", "be_void_plum_seed", "be_stardust_berry_seed",
        };
        public static readonly string[] SeedResourceNames = new string[]
        {
            "BrewingExpansion.Textures.辣椒种子.png",
            "BrewingExpansion.Textures.水稻种子.png",
            "BrewingExpansion.Textures.小麦种子.png",
            "BrewingExpansion.Textures.苦艾草种子.png",
            "BrewingExpansion.Textures.花椒种子.png",
            "BrewingExpansion.Textures.柠檬种子.png",
            "BrewingExpansion.Textures.黄葡萄种子.png",
            "BrewingExpansion.Textures.甘蔗种子.png",
            "BrewingExpansion.Textures.黄瓜种子.png",
            "BrewingExpansion.Textures.荔枝种子.png",
            "BrewingExpansion.Textures.茉莉种子.png",
            "BrewingExpansion.Textures.青提种子.png",
            "BrewingExpansion.Textures.桂花种子.png",
            "BrewingExpansion.Textures.杨梅种子.png",
            "BrewingExpansion.Textures.虚空李种子.png",
            "BrewingExpansion.Textures.星尘莓种子.png",
        };
        private static string[] _seedDescriptionsCache;
        private static bool _seedDescriptionsCacheIsZH;
        public static string[] SeedDescriptions
        {
            get
            {
                if (_seedDescriptionsCache == null || _seedDescriptionsCacheIsZH != Lang.IsZH)
                {
                    _seedDescriptionsCache = new string[]
                    {
                        Lang.T(
                            "一颗小小的辣椒种子。凡间的辛辣，不必昂贵，便能暖透一整夜。放进种子孵化器里，过夜就能长出果实。",
                            "A tiny chili seed. The mortal realm's fire — never costly, yet it can warm a whole night through. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗饱满的水稻种子。凡间最古老的口粮，养活了一代又一代人。放进种子孵化器里，过夜就能长出果实。",
                            "A plump rice seed. The mortal realm's oldest staple, it has fed generation upon generation. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗金黄的小麦种子。凡间最寻常的麦香，是每个清晨的开始。放进种子孵化器里，过夜就能长出果实。",
                            "A golden wheat seed. The mortal realm's plainest fragrance — the scent that begins every morning. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗带着苦味的苦艾草种子。凡间的苦，总要亲自尝过才懂。放进种子孵化器里，过夜就能长出果实。",
                            "A bitter wormwood seed. The mortal realm's bitterness — never understood until tasted first-hand. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗带刺的花椒种子。凡间的麻，藏在刺痛的余韵里。放进种子孵化器里，过夜就能长出果实。",
                            "A thorned Sichuan pepper seed. The mortal realm's numbness, hidden in the lingering afterglow of a sting. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗带着清香的柠檬种子。凡间的酸，是夏天给舌尖的一记耳光。放进种子孵化器里，过夜就能长出果实。",
                            "A fragrant lemon seed. The mortal realm's sourness — a slap summer deals across the tongue. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗圆润的黄葡萄种子。凡间的甜，被日光酿成了琥珀色。放进种子孵化器里，过夜就能长出果实。",
                            "A round yellow-grape seed. The mortal realm's sweetness, brewed amber by the sun. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗坚硬的甘蔗种子。凡间的甜，需要嚼碎才能尝到。放进种子孵化器里，过夜就能长出果实。",
                            "A hard sugarcane seed. The mortal realm's sweetness — only tasted once chewed through. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗扁平的黄瓜种子。凡间的清凉，从土地直抵唇边。放进种子孵化器里，过夜就能长出果实。",
                            "A flat cucumber seed. The mortal realm's coolness, carried from soil straight to the lips. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗棕褐色的荔枝种子。远来之物，下层区少见，上层区才配尝一口鲜。放进种子孵化器里，过夜就能长出果实。",
                            "A brown lychee seed. A thing from afar — scarce in the lower district, a fresh taste reserved for the upper alone. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗细小的茉莉种子，外壳带着夜露。远来的花，只在夜里开。放进种子孵化器里，过夜就能长出繁花。",
                            "A tiny jasmine seed, its husk beaded with night dew. A flower from afar, that blossoms only by dark. Set it in the Seed Incubator; by dawn, it blooms."),

                        Lang.T(
                            "一颗青绿色的青提种子。远来的果，等过一场霜才够甜。放进种子孵化器里，过夜就能长出果实。",
                            "A pale-green grape seed. A fruit from afar — only sweet once it has waited out a frost. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗细小的桂花种子，外壳薄如信笺。折桂的人说，来年花开时便回——放进种子孵化器里，过夜就能长出繁花。",
                            "A tiny osmanthus seed, its husk thin as a letter. The one who plucked the cassia said: when the blossoms return next year, I too will come home — Set it in the Seed Incubator; by dawn, it blooms."),

                        Lang.T(
                            "一颗圆润的杨梅种子，红得沉静。种下它的人不说等，只是一年一年地酿——放进种子孵化器里，过夜就能长出果实。",
                            "A round bayberry seed, red in quiet stillness. Whoever planted it spoke no word of waiting — only brewed on, year after year. Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗漆黑的核，沉得像一句没说完的话。空间站的葬礼之后，它从冷却舱的缝隙里长出来。种下它的人不问收成，只问一句：他去了哪里。放进种子孵化器里，过夜就能长出果实。",
                            "A jet-black pit, heavy as a sentence left unfinished. After the funeral at the station, it grew from a crack in the cooling bay. Whoever plants it asks no harvest — only this: where did he go? Set it in the Seed Incubator; by dawn, it fruits."),

                        Lang.T(
                            "一颗银白的籽，细看像一粒微缩的星。它长在冷却舱外的减压墙上，那里的辐射比别处更烈，光比别处更孤。放进种子孵化器里，过夜就能长出果实。",
                            "A silver-white seed — up close, a miniature star. It grew upon the depressurized wall beyond the cooling bay, where the radiation burns harsher than elsewhere, and the light stands lonelier than anywhere. Set it in the Seed Incubator; by dawn, it fruits."),
                    };
                    _seedDescriptionsCacheIsZH = Lang.IsZH;
                }
                return _seedDescriptionsCache;
            }
        }

        public static readonly string[] FruitIds = new string[]
        {
            "chili_fruit", "rice_fruit", "wheat_fruit", "wormwood_fruit",
            "sichuan_pepper_fruit", "lemon_fruit", "yellow_grape_fruit", "sugarcane_fruit",
            "cucumber_fruit",
            "lychee_fruit", JasmineFruitId, GreenGrapeFruitId,
            "osmanthus_fruit", "bayberry_fruit",
            VoidPlumFruitId, StardustBerryFruitId,
            MidautumnMoonOsmanthusId,
        };
        private static string[] _fruitNamesCache;
        private static bool _fruitNamesCacheIsZH;
        public static string[] FruitNames
        {
            get
            {
                if (_fruitNamesCache == null || _fruitNamesCacheIsZH != Lang.IsZH)
                {
                    _fruitNamesCache = new string[]
                    {
                        Lang.T("辣椒", "Chili"),
                        Lang.T("水稻", "Rice"),
                        Lang.T("小麦", "Wheat"),
                        Lang.T("苦艾草", "Wormwood"),
                        Lang.T("花椒", "Sichuan Pepper"),
                        Lang.T("柠檬", "Lemon"),
                        Lang.T("黄葡萄", "Yellow Grape"),
                        Lang.T("甘蔗", "Sugarcane"),
                        Lang.T("黄瓜", "Cucumber"),
                        Lang.T("荔枝", "Lychee"),
                        Lang.T("茉莉花", "Jasmine"),
                        Lang.T("青提", "Green Grape"),
                        Lang.T("桂花", "Osmanthus"),
                        Lang.T("杨梅", "Bayberry"),
                        Lang.T("虚空李", "Void Plum"),
                        Lang.T("星尘莓", "Stardust Berry"),
                        Lang.T("月华桂", "Moonlit Osmanthus"),
                    };
                    _fruitNamesCacheIsZH = Lang.IsZH;
                }
                return _fruitNamesCache;
            }
        }
        public static readonly string[] FruitPngNames = new string[]
        {
            "辣椒", "水稻", "小麦", "苦艾草", "花椒", "柠檬", "黄葡萄", "甘蔗",
            "黄瓜", "荔枝", "茉莉花", "青提", "桂花", "杨梅", "虚空李", "星尘莓",
            "月华桂",
        };
        public static readonly string[] FruitSpriteKeys = new string[]
        {
            "be_chili_fruit", "be_rice_fruit", "be_wheat_fruit", "be_wormwood_fruit",
            "be_sichuan_pepper_fruit", "be_lemon_fruit", "be_yellow_grape_fruit", "be_sugarcane_fruit",
            "be_cucumber_fruit", "be_lychee_fruit", "be_jasmine_fruit", "be_green_grape_fruit",
            "be_osmanthus_fruit", "be_bayberry_fruit", "be_void_plum_fruit", "be_stardust_berry_fruit",
            "be_midautumn_moon_osmanthus"
        };
        public static readonly string[] FruitResourceNames = new string[]
        {
            "BrewingExpansion.Textures.辣椒.png", "BrewingExpansion.Textures.水稻.png",
            "BrewingExpansion.Textures.小麦.png", "BrewingExpansion.Textures.苦艾草.png",
            "BrewingExpansion.Textures.花椒.png", "BrewingExpansion.Textures.柠檬.png",
            "BrewingExpansion.Textures.黄葡萄.png", "BrewingExpansion.Textures.甘蔗.png",
            "BrewingExpansion.Textures.黄瓜.png", "BrewingExpansion.Textures.荔枝.png",
            "BrewingExpansion.Textures.茉莉花.png", "BrewingExpansion.Textures.青提.png",
            "BrewingExpansion.Textures.桂花.png", "BrewingExpansion.Textures.杨梅.png",
            "BrewingExpansion.Textures.虚空李.png", "BrewingExpansion.Textures.星尘莓.png",
            "BrewingExpansion.Textures.月华桂.png"
        };
        private static string[] _fruitDescriptionsCache;
        private static bool _fruitDescriptionsCacheIsZH;
        public static string[] FruitDescriptions
        {
            get
            {
                if (_fruitDescriptionsCache == null || _fruitDescriptionsCacheIsZH != Lang.IsZH)
                {
                    _fruitDescriptionsCache = new string[]
                    {
                        Lang.T(
                            "一只鲜红的辣椒。凡间最廉价的暖意，咬一口便能熬过夜班。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A scarlet chili. The mortal realm's cheapest warmth — one bite, and the night shift can be endured. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一捧饱满的水稻。凡间最古老的安稳，一碗下肚便能撑过一整天。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A handful of plump rice. The mortal realm's oldest comfort — one bowl, and a whole day is borne. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一把金黄的小麦。凡间最寻常的饱足，被磨成面粉，也被酿成啤酒。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A sheaf of golden wheat. The mortal realm's plainest fullness — milled into flour, and brewed into beer. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一株苦艾草。凡间的苦，藏在草本香气背后。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A sprig of wormwood. The mortal realm's bitterness, hidden behind an herbal fragrance. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一把麻香的花椒。凡间的麻，让舌尖醒过来。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A handful of Sichuan pepper. The mortal realm's numbness — it wakes the tongue. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一只清香的柠檬。凡间的酸，比任何甜言蜜语都真切。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A fragrant lemon. The mortal realm's sourness — truer than any sweet talk. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一串圆润的黄葡萄。凡间的甜，被日光酿成金色。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A cluster of round yellow grapes. The mortal realm's sweetness, brewed golden by the sun. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一段坚硬的甘蔗。凡间的甜，藏在硬壳底下。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A length of hard sugarcane. The mortal realm's sweetness, hidden beneath a hard shell. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一根清脆的黄瓜。凡间的清凉，从田间直抵舌尖。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A crisp cucumber. The mortal realm's coolness — from field straight to the tip of the tongue. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一颗饱满的荔枝，果肉晶莹剔透。远来的珍果，甜得像上层区的一场旧梦。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A plump lychee, its flesh translucent as crystal. A precious fruit from afar, sweet as an old dream of the upper district. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一簇细碎的茉莉花，只在夜里开。远来的花，天亮之前就谢了。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A cluster of tiny jasmine blossoms, opening only by night. A flower from afar — withered before dawn. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一串青绿的青提，等过一场霜才够甜。远来的果，冷过一次才够沉。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A cluster of green grapes — sweet enough only after a frost. A fruit from afar — heavy enough only after it has once been cold. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一簇细碎的金黄桂花，香气袭人。那年别在襟前的桂香犹在，折桂的人却再没回头。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A cluster of small golden osmanthus, their fragrance overwhelming. The scent of the cassia pinned at the lapel that year lingers still — yet the one who plucked it never once looked back. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一颗鲜红多汁的杨梅，酸甜可口。从夏酿到冬，从浅绛沉到绯红，她终于明白有些人是等不回的。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A crimson, juicy bayberry — sweet and tart, a delight. Brewed from summer into winter, deepening from pale rose to scarlet, she finally understood: some people never come back, no matter how long one waits. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一颗乌黑的李子，果皮上没有一丝光泽，像被什么吸走了颜色。调酒师说，这是消失的质量——一个人走了，他留下的空缺比他的重量更沉。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A jet-black plum, its skin without a trace of luster — as if some force had drawn the color out. The bartender calls it \"vanished mass\" — when a person leaves, the absence left behind weighs heavier than they ever did. Eat it raw, or set it in a bottle to brew."),

                        Lang.T(
                            "一颗半透明的浆果，果肉里悬着极细的光点，像被封存了一整个星夜的碎屑。调酒师说，这是残留的光——人走后留下的不是重量，是那些还没说完的话，还在发光。可以直接食用，也可以放进酒瓶里酿酒。",
                            "A translucent berry, tiny points of light suspended within its flesh — like the debris of an entire starry night, sealed within. The bartender calls it \"residual light\" — what a person leaves behind is not weight, but words still unfinished, still glowing. Eat it raw, or set it in a bottle to brew."),

                        MidautumnMoonOsmanthusDescription,   // ★ 保留这行引用
                    };
                    _fruitDescriptionsCacheIsZH = Lang.IsZH;
                }
                return _fruitDescriptionsCache;
            }
        }

        public static readonly string[] WineIds = new string[]
        {
            "chili_wine", "rice_wine", "wheat_wine", "wormwood_wine",
            "sichuan_pepper_wine", "lemon_wine", "yellow_grape_wine", "sugarcane_wine",
            "cucumber_wine",
            "lychee_wine", JasmineWineId, GreenGrapeWineId,
            "osmanthus_wine", "bayberry_wine",
            VoidPlumWineId, StardustBerryWineId,
            GusuiWineId, ChimaduWineId, SuantianBanzhanWineId, BipoLiuguangWineId,
            BanzhanSuankuWineId, DaopoShuangganWineId, MaiganNiangWineId,
            "lychee_rice_wine", "lychee_wormwood_wine", "lychee_chili_wine",
            MoxiangMiniangWineId, MoningQingxiaoWineId, HuazheLiunianWineId,
            BimaiLiujinWineId, CuiyuQingzhuWineId, ZiluJinfengWineId,
            MoliQingtiWineId,
            GuixinChisuWineId, JianghenChenzheWineId, ZhuoleiXiandanWineId,
            CuojiShuWineId,
            ChenyuanMiniangWineId, KongguKuaiWineId, XingluoMaitianWineId, LengxingRuzhanWineId,
            YexiangChenjiWineId, XingzhuiQingtengWineId,
            XingchenWangeWineId,
            MiscWineId, YidiaoWineId, CuofuWineId, YujinWineId,
            MidautumnOsmanthusWineId,
            NianHeshiWineId,
        };

        // ★ 中文名字，索引与 WineIds 一一对应；也是磁盘 PNG 的文件名基础
        public static readonly string[] WineNamesZH = new string[]
        {
    "辣椒酒", "米酒", "啤酒", "苦艾酒", "花椒酒",
    "柠檬酒", "白兰地酒", "朗姆酒", "黄瓜酒", "荔枝酒",
    "夜香浮雪", "霜凝青提", "桂魄流金", "绛珠凝露", "坠羽晨雾",
    "余光未冷", "故穗", "赤麻渡", "酸甜半盏", "碧珀流光",
    "半盏酸苦", "稻珀双甘", "麦甘酿", "荔枝糯米冰酿", "箬叶幽香",
    "清荔流霞", "茉香米酿", "茉柠清晓", "花蔗流年", "碧麦流金",
    "翠玉清珠", "紫露金风", "茉莉青提", "桂信迟酸", "绛痕沉蔗",
    "灼泪衔丹", "错季书", "沉渊米酿", "空谷苦艾", "星落麦田",
    "冷星入盏", "夜香沉寂", "星坠青藤", "星尘挽歌",
    "杂酿", "异调", "错付", "余烬",
    "玉兔衔桂", "念何事",
        };

        // ★ 英文名字，索引与 WineIds 一一对应
        public static readonly string[] WineNamesEN = new string[]
        {
    "Chili Wine", "Rice Wine", "Beer", "Absinthe", "Sichuan Pepper Wine",
    "Lemon Wine", "Brandy", "Rum", "Cucumber Wine", "Lychee Wine",
    "Night Bloom, Drifting Snow", "Frostbound Grape", "Golden Osmanthus Soul",
    "Crimson Pearl Dew", "Fallen Feather, Morning Mist",
    "Afterglow Uncooled", "Ears of Home", "Scarlet Numbness Ferry",
    "Half Cup of Sweet and Sour", "Jade Amber, Flowing Light",
    "Half Cup of Bittersour", "Rice Amber, Twin Sweet", "Sweet Wheat Brew",
    "Lychee Sticky Rice Ice Brew", "Whisper of Reed Leaves",
    "Clear Lychee, Drifting Sunset", "Jasmine Rice Brew", "Jasmine Lemon Dawn",
    "Blossom and Cane, Years Adrift", "Jade Wheat, Flowing Gold",
    "Emerald Pearl", "Purple Dew, Golden Wind", "Jasmine Green Grape",
    "Osmanthus Letter, Belated Sour", "Crimson Trace, Sunk in Cane",
    "Burning Tears Holding Red", "A Letter Out of Season",
    "Sunken Abyss Rice Brew", "Hollow Valley Wormwood", "Starfall Wheat Field",
    "Cold Star in the Cup", "Night Fragrance, Silent and Still",
    "Star Fall, Green Vine", "Stardust Elegy",
    "Misc Brew", "Off-Key", "Wrongly Given", "Embers",
    "Jade Rabbit Carries the Osmanthus", "What Was I Thinking?",
        };

        public static string[] WineNames
        {
            get { return Lang.IsZH ? WineNamesZH : WineNamesEN; }
        }

        public static readonly string[] WineSpriteKeys = new string[]
        {
            "be_chili_wine", "be_rice_wine", "be_wheat_wine", "be_wormwood_wine",
            "be_sichuan_pepper_wine", "be_lemon_wine", "be_yellow_grape_wine", "be_sugarcane_wine",
            "be_cucumber_wine", "be_lychee_wine", "be_jasmine_wine", "be_green_grape_wine",
            "be_osmanthus_wine", "be_bayberry_wine", "be_void_plum_wine", "be_stardust_berry_wine",
            "be_gusui_wine", "be_chimadu_wine", "be_suantian_banzhan_wine", "be_bipo_liuguang_wine",
            "be_banzhan_suanku_wine", "be_daopo_shuanggan_wine", "be_maigan_niang_wine",
            "be_lychee_rice_wine", "be_lychee_wormwood_wine", "be_lychee_chili_wine",
            "be_moxiang_miniang_wine", "be_moning_qingxiao_wine", "be_huazhe_liunian_wine",
            "be_bimai_liujin_wine", "be_cuiyu_qingzhu_wine", "be_zilu_jinfeng_wine",
            "be_moli_qingti_wine", "be_guixin_chisu_wine", "be_jianghen_chenzhe_wine",
            "be_zhuolei_xiandan_wine", "be_cuoji_shu_wine",
            "be_chenyuan_miniang_wine", "be_konggu_kuai_wine", "be_xingluo_maitian_wine",
            "be_lengxing_ruzhan_wine", "be_yexiang_chenji_wine", "be_xingzhui_qingteng_wine",
            "be_xingchen_wange_wine", "be_misc_wine", "be_yidiao_wine", "be_cuofu_wine", "be_yujin_wine",
            "be_midautumn_osmanthus_wine", NianHeshiWineSpriteKey
        };

        public static readonly string[] WineResourceNames = new string[]
        {
            "BrewingExpansion.Textures.辣椒酒.png", "BrewingExpansion.Textures.米酒.png",
            "BrewingExpansion.Textures.啤酒.png", "BrewingExpansion.Textures.苦艾酒.png",
            "BrewingExpansion.Textures.花椒酒.png", "BrewingExpansion.Textures.柠檬酒.png",
            "BrewingExpansion.Textures.白兰地酒.png", "BrewingExpansion.Textures.朗姆酒.png",
            "BrewingExpansion.Textures.黄瓜酒.png", "BrewingExpansion.Textures.荔枝酒.png",
            "BrewingExpansion.Textures.夜香浮雪.png", "BrewingExpansion.Textures.霜凝青提.png",
            "BrewingExpansion.Textures.桂魄流金.png", "BrewingExpansion.Textures.绛珠凝露.png",
            "BrewingExpansion.Textures.坠羽晨雾.png", "BrewingExpansion.Textures.余光未冷.png",
            "BrewingExpansion.Textures.故穗.png", "BrewingExpansion.Textures.赤麻渡.png",
            "BrewingExpansion.Textures.酸甜半盏.png", "BrewingExpansion.Textures.碧珀流光.png",
            "BrewingExpansion.Textures.半盏酸苦.png", "BrewingExpansion.Textures.稻珀双甘.png",
            "BrewingExpansion.Textures.麦甘酿.png", "BrewingExpansion.Textures.荔枝糯米冰酿.png",
            "BrewingExpansion.Textures.箬叶幽香.png", "BrewingExpansion.Textures.清荔流霞.png",
            "BrewingExpansion.Textures.茉香米酿.png", "BrewingExpansion.Textures.茉柠清晓.png",
            "BrewingExpansion.Textures.花蔗流年.png", "BrewingExpansion.Textures.碧麦流金.png",
            "BrewingExpansion.Textures.翠玉清珠.png", "BrewingExpansion.Textures.紫露金风.png",
            "BrewingExpansion.Textures.茉莉青提.png", "BrewingExpansion.Textures.桂信迟酸.png",
            "BrewingExpansion.Textures.绛痕沉蔗.png", "BrewingExpansion.Textures.灼泪衔丹.png",
            "BrewingExpansion.Textures.错季书.png", "BrewingExpansion.Textures.沉渊米酿.png",
            "BrewingExpansion.Textures.空谷苦艾.png", "BrewingExpansion.Textures.星落麦田.png",
            "BrewingExpansion.Textures.冷星入盏.png", "BrewingExpansion.Textures.夜香沉寂.png",
            "BrewingExpansion.Textures.星坠青藤.png", "BrewingExpansion.Textures.星尘挽歌.png",
            "BrewingExpansion.Textures.杂酿.png", "BrewingExpansion.Textures.异调.png",
            "BrewingExpansion.Textures.错付.png", "BrewingExpansion.Textures.余烬.png",
            "BrewingExpansion.Textures.玉兔衔桂.png", NianHeshiWineResourceName
        };

        private static string[] _wineDescriptionsCache;
        private static bool _wineDescriptionsCacheIsZH;
        public static string[] WineDescriptions
        {
            get
            {
                if (_wineDescriptionsCache == null || _wineDescriptionsCacheIsZH != Lang.IsZH)
                {
                    _wineDescriptionsCache = new string[]
                    {
                        Lang.T(
                            "用辣椒酿成的烈酒，入口火辣，回味却带一丝甘甜。下层区的工人喜欢在夜班前来一杯暖身。",
                            "A fiery spirit brewed from chili — hot on the tongue, with a thread of sweetness in the aftertaste. The workers of the lower district like a glass before the night shift to warm themselves."),

                        Lang.T(
                            "以大米酿制的甜酒，醇厚柔和。据说最早是下层区的老太太们用来招待客人的私房酒。",
                            "A sweet wine brewed from rice — mellow, soft. They say it began as the private brew of the lower district's old women, offered to guests."),

                        Lang.T(
                            "用小麦酿造的饮品，清爽微苦，是下层区最常见的平民酒。",
                            "A drink brewed from wheat — crisp and faintly bitter, the common man's most ordinary wine in the lower district."),

                        Lang.T(
                            "以苦艾草酿成，带有浓郁的草本香气。传说喝多了会产生幻觉，因此在上层区被明令禁止。",
                            "Brewed from wormwood, heavy with herbal fragrance. Legend says too much brings visions — and so it is banned outright in the upper district."),

                        Lang.T(
                            "麻香四溢的烈酒，入喉酥麻。只有真正的老饕才懂它的妙处。",
                            "A spirit brimming with numbing fragrance — a tingling at the throat. Only the true connoisseur understands its subtlety."),

                        Lang.T(
                            "清冽酸甜，适合冰镇饮用。上层区的贵妇们把它当作消暑的雅致饮品。",
                            "Clear, tart-sweet, best served chilled. The upper district's ladies take it as a refined drink to beat the heat."),

                        Lang.T(
                            "以黄葡萄酿成的烈性果酒，色泽琥珀，被上层区视为珍品。年份越久越昂贵。",
                            "A strong fruit wine brewed from yellow grapes, amber in hue, prized in the upper district. The older the vintage, the dearer the price."),

                        Lang.T(
                            "以甘蔗酿成的甜酒，是水手和走私客的最爱。一瓶下肚，什么话都敢说。",
                            "A sweet wine brewed from sugarcane — beloved by sailors and smugglers alike. One bottle down, and any word dares to be spoken."),

                        Lang.T(
                            "带着清冽草木香的淡绿酒液，入口清凉，回味微苦。据说下层区的老农夫会在盛夏午后独饮一杯。",
                            "A pale green wine, crisp with the scent of grass and wood — cool going down, faintly bitter in the aftertaste. They say the old farmers of the lower district drink a cup alone on midsummer afternoons."),

                        Lang.T(
                            "以荔枝酿成的甜香果酒，入口如蜜，回味悠长。是上层区宴席上的常客。",
                            "A sweetly fragrant fruit wine brewed from lychee — honey on the tongue, long on the finish. A regular fixture at upper district banquets."),

                        Lang.T(
                            "上层区的花园里，茉莉只在夜里开。花开的时候，整条巷子都是香的，可天亮之前就谢了。酿这酒的人说，茉莉不是给白天喝的，是给夜里睡不着的人喝的。",
                            "In the upper district's gardens, jasmine blooms only at night. When it flowers, the whole alley is perfumed — yet it wilts before dawn. The one who brews this says: jasmine is not for the day; it is for those who cannot sleep at night."),

                        Lang.T(
                            "青提是上层区阳台上唯一能活的果树。霜降那天摘下的青提最甜，因为冷过一次的甜才够沉。",
                            "Green grape is the only fruit tree that survives on upper district balconies. Grapes picked on the day of first frost are sweetest — because only sweetness that has once been cold is heavy enough."),

                        Lang.T(
                            "那年桂花开时，她折一枝别在我襟前，说：等你回来，一同酿酒。我一去千里，终没回头。",
                            "That year when the osmanthus bloomed, she plucked a sprig and pinned it to my lapel: \"When you come back, we will brew together.\" I went a thousand miles away — and never turned back."),

                        Lang.T(
                            "杨梅熟透的那个夏天，他说：来年桂花开时便回。我一坛一坛地酿，从夏到秋，从秋到冬。",
                            "The summer the bayberries ripened, he said: when the osmanthus blooms again next year, I will return. I brewed jar after jar — summer to autumn, autumn to winter."),

                        Lang.T(
                            "酒液粉白，倒在杯里像一小片褪了色的晨雾。虚空李代表消失的质量——一个人走了，他留下的空缺轻得像一片羽毛。",
                            "The wine is pink-white, poured into a cup like a small patch of faded morning mist. Void Plum stands for vanished mass — when a person leaves, the absence left behind is as light as a feather."),

                        Lang.T(
                            "酒液蓝紫，只在烛光下才显出极细的银星，像把一整片夜空沉进了杯底。",
                            "The wine is blue-violet; only by candlelight do faint silver stars appear, as if an entire night sky had sunk to the bottom of the cup."),

                        Lang.T(
                            "每个空间站居民都记得地球的麦田和稻田。酿酒师把两种穗子一起酿，叫「故穗」。",
                            "Every space station resident remembers Earth's wheat fields and rice paddies. The brewer ferments both kinds of grain together and calls it \"Ears of Home.\""),

                        Lang.T(
                            "边境站的人叫它「渡」。一口从痛苦渡到麻木，从清醒渡到梦乡。渡人，也渡己。",
                            "The border-station folk call it \"The Crossing.\" One sip ferries you from pain to numbness, from waking to dream. It ferries others — and it ferries oneself."),

                        Lang.T(
                            "柠檬的酸里带甜，甘蔗的甜里带酸——像过日子的滋味。",
                            "Lemon's sourness carries sweetness; sugarcane's sweetness carries sour — like the taste of getting by."),

                        Lang.T(
                            "黄葡萄的琥珀，黄瓜的碧玉，同在一杯里流转。下层区的夏夜，最凉快的一口。",
                            "Yellow grape's amber, cucumber's jade — both flow within a single cup. The coolest sip on a lower district summer night."),

                        Lang.T(
                            "苦艾草和柠檬，都是苦的。可两个苦的东西放在一起，反而尝出了清。",
                            "Wormwood and lemon — both bitter. Yet two bitter things together brew something clear."),

                        Lang.T(
                            "米香清，葡萄甜。一个淡，一个浓，双甘交汇。下层区老太太们的私藏配方。",
                            "Rice-fragrant, grape-sweet. One light, one rich — two sweets meeting. A private recipe of the lower district's old women."),

                        Lang.T(
                            "麦香淡，甘蔗甜。淡里藏甜，藏住一整年。",
                            "Wheat-fragrant, sugarcane-sweet. Sweetness hidden in lightness, kept through a whole year."),

                        Lang.T(
                            "xuxu的最爱，荔枝与糯米相遇，米香清甜如夏夜凉风。",
                            "xuxu's favorite — lychee meets glutinous rice; the fragrance is sweet and clear as a summer night's breeze."),

                        Lang.T(
                            "艾情好苦让我怎么荔枝。",
                            "Wormwood is so bitter — how am I meant to stay clear-headed?"),

                        Lang.T(
                            "荔枝的柔甜先到，辣椒的火辣紧随其后。",
                            "Lychee's soft sweetness arrives first; chili's fire follows close behind."),

                        Lang.T(
                            "茉莉配米酿，是下层区老太太们的秘方。甜的米酒让人昏沉，香的茉莉让人清醒。",
                            "Jasmine with rice brew — a secret recipe of the lower district's old women. The sweet rice wine makes you drowsy; the fragrant jasmine keeps you clear."),

                        Lang.T(
                            "茉莉的香，柠檬的酸。一个太柔，一个太利。",
                            "Jasmine's fragrance, lemon's sharpness. One too soft, one too keen."),

                        Lang.T(
                            "甘蔗是甜的，甜到发腻；茉莉是香的，香到发虚。",
                            "Sugarcane is sweet — sweet to the point of cloying. Jasmine is fragrant — fragrant to the point of emptiness."),

                        Lang.T(
                            "青提清甜，小麦微苦。一杯里同时有甜有苦，才喝得下去一辈子。",
                            "Green grape is sweet, wheat faintly bitter. Only a cup that holds both sweet and bitter can be drunk for a lifetime."),

                        Lang.T(
                            "青提和黄瓜，都是清物。因为它们不争，才能在夏天活下来。",
                            "Green grape and cucumber — both are cool things. Because they contend for nothing, they survive the summer."),

                        Lang.T(
                            "青提是浅紫的，黄葡萄是琥珀的。最好的搭配从来不是最像的。",
                            "Green grape is pale violet, yellow grape amber. The best pairings are never the most alike."),

                        Lang.T(
                            "茉莉是夜里开的花，青提是霜降才甜的果。最干净的味道，是一个人在深夜想起另一个人的时候。",
                            "Jasmine is a flower that blooms by night; green grape is a fruit sweet only after frost. The cleanest flavor is that of one person thinking of another in the dead of night."),

                        Lang.T(
                            "那年襟前的桂花早已枯成信笺的颜色，诺言却比花期还短。",
                            "The osmanthus pinned to my lapel that year has long since withered to the color of old letters — yet the promise lasted shorter than the blooming season."),

                        Lang.T(
                            "他说来年桂花开时便回，她便用甘蔗的甜去哄骗杨梅的酸。",
                            "He said he would return when the osmanthus bloomed next year. So she used sugarcane's sweetness to coax away bayberry's sour."),

                        Lang.T(
                            "最后一滴泪落下时，她往坛里扔了一把辣椒。",
                            "As the last tear fell, she tossed a fistful of chili into the jar."),

                        Lang.T(
                            "用盛夏最后的杨梅，接住初秋第一枝桂花。",
                            "Take the last bayberries of high summer — catch the first osmanthus of early autumn."),

                        Lang.T(
                            "米是最日常的东西，可虚空李不是日常。其实每一次告别，都是从一碗最普通的米饭开始的。",
                            "Rice is the most everyday thing — but Void Plum is not everyday at all. Every farewell, in truth, begins with the most ordinary bowl of rice."),

                        Lang.T(
                            "苦艾草本身就是苦的，虚空李是沉的。人在最悲伤的时候反而哭不出来。",
                            "Wormwood is bitter in itself; Void Plum is heavy. In the deepest sorrow, one cannot weep."),

                        Lang.T(
                            "星尘莓是残留的光，小麦是土地的重量。",
                            "Stardust Berry is residual light; wheat is the weight of the earth."),

                        Lang.T(
                            "星尘莓带着夜空的凉，柠檬带着白天的酸。",
                            "Stardust Berry carries the cool of night; lemon carries the sour of day."),

                        Lang.T(
                            "茉莉是夜里才开的花，虚空李是葬礼后长出的果。",
                            "Jasmine is a flower that opens only at night; Void Plum is a fruit that grows after the funeral."),

                        Lang.T(
                            "星尘莓是夜空中残留的光，青提是阳台上挂着的第一串果。",
                            "Stardust Berry is the residual light in a night sky; green grape is the first cluster hanging on a balcony."),

                        Lang.T(
                            "空间站的葬礼与地面不同。金属棺从减压舱推出去的时候，整片夜空都亮了一下。所有告别都是延迟的引力。",
                            "A funeral in space is unlike one on the ground. When the metal coffin is pushed out from the airlock, the whole night sky brightens for a moment. Every farewell is delayed gravity."),

                        Lang.T(
                            "混在一起，什么都没酿成。倒出来是浑浊的，闻起来有一股说不清的甜。",
                            "Mixed together, nothing brewed. What pours out is murky, and it smells of an indefinable sweetness."),

                        Lang.T(
                            "远道而来的东西，调不到一处去。不是所有远来的都该见面。",
                            "Things that come from far away cannot always be tuned to one another. Not everything from afar is meant to meet."),

                        Lang.T(
                            "她把一整年的花都酿进去了，可那个人从来不喝这种酒。",
                            "She brewed a whole year of blossoms into it — but that person never drank this kind of wine."),

                        Lang.T(
                            "空间站的葬礼后，调酒师把两颗果实放进同一只瓶，可他忘记了配方。这杯酒，敬所有连告别都没能好好说出口的人。",
                            "After the space station funeral, the bartender placed two fruits in the same bottle — but he forgot the recipe. This glass is raised to everyone who never got to say goodbye properly."),

                        MidautumnOsmanthusWineDescription,
                        NianHeshiWineDescription,
                    };
                    _wineDescriptionsCacheIsZH = Lang.IsZH;
                }
                return _wineDescriptionsCache;
            }
        }

        public static readonly Dictionary<string, int> WineBasePriceOverride = new Dictionary<string, int>
        {
            { "chili_wine", 115 }, { "rice_wine", 115 }, { "wheat_wine", 115 },
            { "wormwood_wine", 115 }, { "sichuan_pepper_wine", 115 }, { "lemon_wine", 115 },
            { "yellow_grape_wine", 115 }, { "sugarcane_wine", 115 }, { "cucumber_wine", 115 },
            { "lychee_wine", 220 }, { JasmineWineId, 220 }, { GreenGrapeWineId, 220 },
            { "osmanthus_wine", 520 }, { "bayberry_wine", 520 },
            { VoidPlumWineId, 2000 }, { StardustBerryWineId, 2000 },
            { GusuiWineId, 115 }, { ChimaduWineId, 115 }, { SuantianBanzhanWineId, 115 },
            { BipoLiuguangWineId, 115 }, { BanzhanSuankuWineId, 115 },
            { DaopoShuangganWineId, 115 }, { MaiganNiangWineId, 115 },
            { "lychee_rice_wine", 180 }, { "lychee_wormwood_wine", 180 }, { "lychee_chili_wine", 180 },
            { MoxiangMiniangWineId, 180 }, { MoningQingxiaoWineId, 180 },
            { HuazheLiunianWineId, 180 }, { BimaiLiujinWineId, 180 },
            { CuiyuQingzhuWineId, 180 }, { ZiluJinfengWineId, 180 },
            { MoliQingtiWineId, 220 },
            { GuixinChisuWineId, 350 }, { JianghenChenzheWineId, 350 }, { ZhuoleiXiandanWineId, 350 },
            { CuojiShuWineId, 1314 },
            { ChenyuanMiniangWineId, 700 }, { KongguKuaiWineId, 700 },
            { XingluoMaitianWineId, 700 }, { LengxingRuzhanWineId, 700 },
            { YexiangChenjiWineId, 800 }, { XingzhuiQingtengWineId, 800 },
            { XingchenWangeWineId, 3500 },
            { MiscWineId, 80 }, { YidiaoWineId, 115 }, { CuofuWineId, 220 }, { YujinWineId, 650 },
            { MidautumnOsmanthusWineId, MidautumnOsmanthusWinePrice },
            { NianHeshiWineId, NianHeshiWinePrice }
        };

        public const int WineAgeValueModPerDay = 3;

        public static readonly string[] CertIds = new string[] { BasicCertId, MidCertId, HighCertId };
        private static string[] _certNamesCache;
        private static bool _certNamesCacheIsZH;
        public static string[] CertNames
        {
            get
            {
                if (_certNamesCache == null || _certNamesCacheIsZH != Lang.IsZH)
                {
                    _certNamesCache = new string[] { BasicCertName, MidCertName, HighCertName };
                    _certNamesCacheIsZH = Lang.IsZH;
                }
                return _certNamesCache;
            }
        }
        public static readonly string[] CertPngNames = new string[] { BasicCertPngName, MidCertPngName, HighCertPngName };
        public static readonly string[] CertSpriteKeys = new string[] { BasicCertSpriteKey, MidCertSpriteKey, HighCertSpriteKey };
        public static readonly string[] CertResourceNames = new string[] { BasicCertResourceName, MidCertResourceName, HighCertResourceName };
        private static string[] _certDescriptionsCache;
        private static bool _certDescriptionsCacheIsZH;
        public static string[] CertDescriptions
        {
            get
            {
                if (_certDescriptionsCache == null || _certDescriptionsCacheIsZH != Lang.IsZH)
                {
                    _certDescriptionsCache = new string[] { BasicCertDescription, MidCertDescription, HighCertDescription };
                    _certDescriptionsCacheIsZH = Lang.IsZH;
                }
                return _certDescriptionsCache;
            }
        }
        public static readonly int[] CertPrices = new int[] { BasicCertPrice, MidCertPrice, HighCertPrice };

        public static readonly string[] RecipeIds = new string[]
        {
            RecipeId1, RecipeId2, RecipeId3, RecipeId4, RecipeId5, RecipeId6, RecipeId7, RecipeId8,
        };
        private static string[] _recipeNamesCache;
        private static bool _recipeNamesCacheIsZH;
        public static string[] RecipeNames
        {
            get
            {
                if (_recipeNamesCache == null || _recipeNamesCacheIsZH != Lang.IsZH)
                {
                    _recipeNamesCache = new string[]
                    {
                        Lang.T("失败手札", "Notes on Failure"),
                        Lang.T("凡间单酿", "Mortal Single Brew"),
                        Lang.T("凡间双穗", "Mortal Twin Grain"),
                        Lang.T("远来之酿", "Brews from Afar"),
                        Lang.T("远凡交汇", "Where Far Meets Near"),
                        Lang.T("双远合酿", "Twin Far Brew"),
                        Lang.T("情思手记", "Journal of Longing"),
                        Lang.T("遗光残页", "Fragment of Afterglow"),
                    };
                    _recipeNamesCacheIsZH = Lang.IsZH;
                }
                return _recipeNamesCache;
            }
        }
        public static readonly string[] RecipePngNames = new string[]
        {
            "酿酒配方1", "酿酒配方2", "酿酒配方3", "酿酒配方4",
            "酿酒配方5", "酿酒配方6", "酿酒配方7", "酿酒配方8",
        };
        public static readonly string[] RecipeSpriteKeys = new string[]
        {
            "be_recipe_1", "be_recipe_2", "be_recipe_3", "be_recipe_4",
            "be_recipe_5", "be_recipe_6", "be_recipe_7", "be_recipe_8",
        };
        public static readonly string[] RecipeResourceNames = new string[]
        {
            "BrewingExpansion.Textures.酿酒配方1.png",
            "BrewingExpansion.Textures.酿酒配方2.png",
            "BrewingExpansion.Textures.酿酒配方3.png",
            "BrewingExpansion.Textures.酿酒配方4.png",
            "BrewingExpansion.Textures.酿酒配方5.png",
            "BrewingExpansion.Textures.酿酒配方6.png",
            "BrewingExpansion.Textures.酿酒配方7.png",
            "BrewingExpansion.Textures.酿酒配方8.png",
        };
        public static readonly int[] RecipePrices = new int[]
        {
            50, 50, 50, 50, 50, 50, 50, 50,
        };
        private static string[] _recipeDescriptionsCache;
        private static bool _recipeDescriptionsCacheIsZH;
        public static string[] RecipeDescriptions
        {
            get
            {
                if (_recipeDescriptionsCache == null || _recipeDescriptionsCacheIsZH != Lang.IsZH)
                {
                    _recipeDescriptionsCache = new string[]
                    {
                        Lang.T(
                            "【失败手札】\n\n记录了所有配方错误后会酿成的酒。\n\n" +
                            "· 凡间材料错配 → 杂酿\n" +
                            "· 远来材料错配 → 异调\n" +
                            "· 情思材料错配 → 错付\n" +
                            "· 遗光材料错配 → 余烬\n\n" +
                            "——学徒最该读的一页。",
                            "【Notes on Failure】\n\nRecords every wine that results from a wrong recipe.\n\n" +
                            "· Mortal materials mismatched → Misc Brew\n" +
                            "· Far materials mismatched → Off-Key\n" +
                            "· Longing materials mismatched → Wrongly Given\n" +
                            "· Afterglow materials mismatched → Embers\n\n" +
                            "— The page every apprentice should read first."),

                        Lang.T(
                            "【凡间单酿】\n\n凡间果实的单配方。\n\n" +
                            "· 辣椒×2 → 辣椒酒\n" +
                            "· 水稻×2 → 米酒\n" +
                            "· 小麦×2 → 啤酒\n" +
                            "· 苦艾草×2 → 苦艾酒\n" +
                            "· 花椒×2 → 花椒酒\n" +
                            "· 柠檬×2 → 柠檬酒\n" +
                            "· 黄葡萄×2 → 白兰地酒\n" +
                            "· 甘蔗×2 → 朗姆酒\n" +
                            "· 黄瓜×2 → 黄瓜酒\n\n" +
                            "——凡间有凡间的味道。",
                            "【Mortal Single Brew】\n\nSingle-fruit recipes for mortal fruits.\n\n" +
                            "· Chili ×2 → Chili Wine\n" +
                            "· Rice ×2 → Rice Wine\n" +
                            "· Wheat ×2 → Beer\n" +
                            "· Wormwood ×2 → Absinthe\n" +
                            "· Sichuan Pepper ×2 → Sichuan Pepper Wine\n" +
                            "· Lemon ×2 → Lemon Wine\n" +
                            "· Yellow Grape ×2 → Brandy\n" +
                            "· Sugarcane ×2 → Rum\n" +
                            "· Cucumber ×2 → Cucumber Wine\n\n" +
                            "— The mortal realm has its own flavor."),

                        Lang.T(
                            "【凡间双穗】\n\n两种凡间果实的合酿。\n\n" +
                            "· 小麦+水稻 → 故穗\n" +
                            "· 辣椒+花椒 → 赤麻渡\n" +
                            "· 柠檬+甘蔗 → 酸甜半盏\n" +
                            "· 黄葡萄+黄瓜 → 碧珀流光\n" +
                            "· 苦艾草+柠檬 → 半盏酸苦\n" +
                            "· 水稻+黄葡萄 → 稻珀双甘\n" +
                            "· 小麦+甘蔗 → 麦甘酿\n\n" +
                            "——凡间与凡间的相逢。",
                            "【Mortal Twin Grain】\n\nBrews from two mortal fruits.\n\n" +
                            "· Wheat + Rice → Ears of Home\n" +
                            "· Chili + Sichuan Pepper → Scarlet Numbness Ferry\n" +
                            "· Lemon + Sugarcane → Half Cup of Sweet and Sour\n" +
                            "· Yellow Grape + Cucumber → Jade Amber, Flowing Light\n" +
                            "· Wormwood + Lemon → Half Cup of Bittersour\n" +
                            "· Rice + Yellow Grape → Rice Amber, Twin Sweet\n" +
                            "· Wheat + Sugarcane → Sweet Wheat Brew\n\n" +
                            "— Mortal meets mortal."),

                        Lang.T(
                            "【远来之酿】\n\n远来果实的单配方。\n\n" +
                            "· 荔枝×2 → 荔枝酒\n" +
                            "· 茉莉花×2 → 夜香浮雪\n" +
                            "· 青提×2 → 霜凝青提\n\n" +
                            "——上层区捎来的味道。",
                            "【Brews from Afar】\n\nSingle-fruit recipes for fruits from afar.\n\n" +
                            "· Lychee ×2 → Lychee Wine\n" +
                            "· Jasmine ×2 → Night Bloom, Drifting Snow\n" +
                            "· Green Grape ×2 → Frostbound Grape\n\n" +
                            "— A taste carried back from the upper district."),

                        Lang.T(
                            "【远凡交汇】\n\n远来果实与凡间果实的合酿。\n\n" +
                            "· 荔枝+水稻 → 荔枝糯米冰酿\n" +
                            "· 荔枝+苦艾草 → 箬叶幽香\n" +
                            "· 荔枝+辣椒 → 清荔流霞\n" +
                            "· 茉莉花+水稻 → 茉香米酿\n" +
                            "· 茉莉花+柠檬 → 茉柠清晓\n" +
                            "· 茉莉花+甘蔗 → 花蔗流年\n" +
                            "· 青提+小麦 → 碧麦流金\n" +
                            "· 青提+黄瓜 → 翠玉清珠\n" +
                            "· 青提+黄葡萄 → 紫露金风\n\n" +
                            "——一个从远方来，一个从近处生。",
                            "【Where Far Meets Near】\n\nBrews from one fruit from afar and one mortal fruit.\n\n" +
                            "· Lychee + Rice → Lychee Sticky Rice Ice Brew\n" +
                            "· Lychee + Wormwood → Whisper of Reed Leaves\n" +
                            "· Lychee + Chili → Clear Lychee, Drifting Sunset\n" +
                            "· Jasmine + Rice → Jasmine Rice Brew\n" +
                            "· Jasmine + Lemon → Jasmine Lemon Dawn\n" +
                            "· Jasmine + Sugarcane → Blossom and Cane, Years Adrift\n" +
                            "· Green Grape + Wheat → Jade Wheat, Flowing Gold\n" +
                            "· Green Grape + Cucumber → Emerald Pearl\n" +
                            "· Green Grape + Yellow Grape → Purple Dew, Golden Wind\n\n" +
                            "— One comes from afar, one is born nearby."),

                        Lang.T(
                            "【双远合酿】\n\n两种远来果实的合酿。\n\n" +
                            "· 茉莉花+青提 → 茉莉青提\n\n" +
                            "——远来的花遇见远来的果。",
                            "【Twin Far Brew】\n\nBrews from two fruits from afar.\n\n" +
                            "· Jasmine + Green Grape → Jasmine Green Grape\n\n" +
                            "— A far flower meets a far fruit."),

                        Lang.T(
                            "【情思手记】\n\n情思果实的配方。\n\n" +
                            "· 桂花×2 → 桂魄流金\n" +
                            "· 杨梅×2 → 绛珠凝露\n" +
                            "· 桂花+柠檬 → 桂信迟酸\n" +
                            "· 杨梅+甘蔗 → 绛痕沉蔗\n" +
                            "· 杨梅+辣椒 → 灼泪衔丹\n" +
                            "· 桂花+杨梅 → 错季书\n\n" +
                            "——所有的等，都酿成了酒。",
                            "【Journal of Longing】\n\nRecipes for longing fruits.\n\n" +
                            "· Osmanthus ×2 → Golden Osmanthus Soul\n" +
                            "· Bayberry ×2 → Crimson Pearl Dew\n" +
                            "· Osmanthus + Lemon → Osmanthus Letter, Belated Sour\n" +
                            "· Bayberry + Sugarcane → Crimson Trace, Sunk in Cane\n" +
                            "· Bayberry + Chili → Burning Tears Holding Red\n" +
                            "· Osmanthus + Bayberry → A Letter Out of Season\n\n" +
                            "— Every wait has been brewed into wine."),

                        Lang.T(
                            "【遗光残页】\n\n遗光果实的配方。\n\n" +
                            "· 虚空李×2 → 坠羽晨雾\n" +
                            "· 星尘莓×2 → 余光未冷\n" +
                            "· 虚空李+水稻 → 沉渊米酿\n" +
                            "· 虚空李+苦艾草 → 空谷苦艾\n" +
                            "· 星尘莓+小麦 → 星落麦田\n" +
                            "· 星尘莓+柠檬 → 冷星入盏\n" +
                            "· 虚空李+茉莉花 → 夜香沉寂\n" +
                            "· 星尘莓+青提 → 星坠青藤\n" +
                            "· 虚空李+星尘莓 → 星尘挽歌\n\n" +
                            "——所有告别都是延迟的引力。",
                            "【Fragment of Afterglow】\n\nRecipes for afterglow fruits.\n\n" +
                            "· Void Plum ×2 → Fallen Feather, Morning Mist\n" +
                            "· Stardust Berry ×2 → Afterglow Uncooled\n" +
                            "· Void Plum + Rice → Sunken Abyss Rice Brew\n" +
                            "· Void Plum + Wormwood → Hollow Valley Wormwood\n" +
                            "· Stardust Berry + Wheat → Starfall Wheat Field\n" +
                            "· Stardust Berry + Lemon → Cold Star in the Cup\n" +
                            "· Void Plum + Jasmine → Night Fragrance, Silent and Still\n" +
                            "· Stardust Berry + Green Grape → Star Fall, Green Vine\n" +
                            "· Void Plum + Stardust Berry → Stardust Elegy\n\n" +
                            "— Every farewell is delayed gravity."),
                    };
                    _recipeDescriptionsCacheIsZH = Lang.IsZH;
                }
                return _recipeDescriptionsCache;
            }
        }

        public static readonly Dictionary<long, string> SlotTagsByPointer = new Dictionary<long, string>();
        public static readonly Dictionary<string, byte[]> PngBytes = new Dictionary<string, byte[]>();

        public static bool Generated = false;
        public static DateTime LastXuxuBatteryModifierTime = DateTime.MinValue;
        public static readonly Dictionary<int, List<WineMarkEntry>> BottleMarks = new Dictionary<int, List<WineMarkEntry>>();

        // ★ 电池电量切图

        public static int CurrentSlotId = -1;
        public static bool BasicCertGifted = false;
        public static bool MidCertGifted = false;
        public static bool HighCertGifted = false;
        public static long CurrentTraderSaleTotal = 0;
        public static int TotalGameDays = 0;
        public static int LastNiniDialDay = -1;
        public static bool NiniDialedEver = false;
        public static int Trader47LastSummonedDay = -1;   // ★ 记录上次召唤时的 TotalGameDays
        public static int Trader47FinishedDay = -1;
        public static int Trader47StockedDay = -1;
        public static int NiniPendingDay = -1;
        private const string SaveDataModKey = "BrewingExpansion.State.v1";

        public static int GetCurrentGameDay()
        {
            try { return StoreStation.GetDayCounter(); }
            catch { return TotalGameDays; }
        }

        // ============ 工具方法 ============
        public static long SlotPointer(GameInventory slot)
        {
            if (slot == null) return 0;
            try { return (long)slot.Pointer; } catch { return 0; }
        }

        public static int GetCurrentSaveSlotId()
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return -1;
                return store.saveSlotId;
            }
            catch { return -1; }
        }

        public static string GetDataPath(int slotId)
        {
            string fileName = slotId >= 0 ? ("brewing_data_" + slotId + ".txt") : "brewing_data_default.txt";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "BrewingExpansion", fileName);
        }

        public static void SaveData()
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return;
                if (store.modData == null)
                    store.modData = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();

                var lines = new List<string>();
                if (BasicCertGifted) lines.Add("GIFT_CERT");
                if (MidCertGifted) lines.Add("GIFT_CERT_MID");
                if (HighCertGifted) lines.Add("GIFT_CERT_HIGH");
                lines.Add("DAYS|" + TotalGameDays);
                lines.Add("LAST_NINI_DIAL|" + LastNiniDialDay);
                if (NiniDialedEver) lines.Add("NINI_DIALED|1");
                lines.Add("NINI_PENDING|" + NiniPendingDay);
                lines.Add("TRADER_47_FINISHED|" + Trader47FinishedDay);
                lines.Add("TRADER_47_STOCKED|" + Trader47StockedDay);
                lines.Add("TRADER_TOTAL|" + CurrentTraderSaleTotal);
                lines.Add("SEED_PITY|" + _longingSeedMisses + "|" + _afterglowSeedMisses);

                foreach (var kv in BottleMarks)
                    foreach (var e in kv.Value)
                        lines.Add(kv.Key + "|" + e.hashcode + "|" + e.wineId + "|" + e.waterScore + "|" + e.contrabandLevel);

                store.modData[SaveDataModKey] = string.Join("\n", lines);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-WINE] SaveData 异常: " + e.Message); }
        }

        public static void LoadData()
        {
            try
            {
                BottleMarks.Clear();
                BasicCertGifted = false;
                MidCertGifted = false;
                HighCertGifted = false;
                TotalGameDays = 0;
                LastNiniDialDay = -1;
                NiniDialedEver = false;
                NiniPendingDay = -1;
                Trader47FinishedDay = -1;
                Trader47StockedDay = -1;
                CurrentTraderSaleTotal = 0;
                ResetSeedPity();

                CurrentSlotId = GetCurrentSaveSlotId();
                var store = PlayerStore.Instance;
                string[] lines;
                bool importedLegacy = false;
                bool hasPendingNiniState = false;
                if (store?.modData != null && store.modData.ContainsKey(SaveDataModKey))
                {
                    string saved = store.modData[SaveDataModKey] ?? "";
                    lines = saved.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    string path = GetDataPath(CurrentSlotId);
                    if (!File.Exists(path)) return;
                    lines = File.ReadAllLines(path);
                    importedLegacy = true;
                }
                int count = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line == "GIFT_CERT") { BasicCertGifted = true; continue; }
                    if (line == "GIFT_CERT_MID") { MidCertGifted = true; continue; }
                    if (line == "GIFT_CERT_HIGH") { HighCertGifted = true; continue; }
                    if (line.StartsWith("DAYS|"))
                    {
                        int d = 0;
                        if (int.TryParse(line.Substring(5), out d)) TotalGameDays = d;
                        continue;
                    }
                    if (line.StartsWith("LAST_NINI_DIAL|"))
                    {
                        int d = -1;
                        if (int.TryParse(line.Substring(15), out d)) LastNiniDialDay = d;
                        continue;
                    }
                    if (line == "NINI_DIALED|1") { NiniDialedEver = true; continue; }
                    if (line.StartsWith("NINI_PENDING|"))
                    {
                        hasPendingNiniState = true;
                        int d = -1;
                        if (int.TryParse(line.Substring(13), out d)) NiniPendingDay = d;
                        continue;
                    }
                    if (line.StartsWith("TRADER_47_FINISHED|"))
                    {
                        int d = -1;
                        if (int.TryParse(line.Substring(19), out d)) Trader47FinishedDay = d;
                        continue;
                    }
                    if (line.StartsWith("TRADER_47_STOCKED|"))
                    {
                        int d = -1;
                        if (int.TryParse(line.Substring(18), out d)) Trader47StockedDay = d;
                        continue;
                    }
                    if (line.StartsWith("TRADER_TOTAL|"))
                    {
                        long t = 0;
                        if (long.TryParse(line.Substring(13), out t)) CurrentTraderSaleTotal = t;
                        continue;
                    }
                    if (line.StartsWith("SEED_PITY|"))
                    {
                        string[] pity = line.Split('|');
                        if (pity.Length == 3)
                        {
                            if (int.TryParse(pity[1], out int longing)) _longingSeedMisses = Math.Max(0, longing);
                            if (int.TryParse(pity[2], out int afterglow)) _afterglowSeedMisses = Math.Max(0, afterglow);
                        }
                        continue;
                    }

                    string[] parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    int uid, hc, score;
                    if (!int.TryParse(parts[0], out uid)) continue;
                    if (!int.TryParse(parts[1], out hc)) continue;
                    string wineId = parts[2];
                    if (!int.TryParse(parts[3], out score)) score = 0;

                    int contraLvl = 0;
                    if (parts.Length >= 5) int.TryParse(parts[4], out contraLvl);

                    if (!BottleMarks.ContainsKey(uid)) BottleMarks[uid] = new List<WineMarkEntry>();
                    BottleMarks[uid].Add(new WineMarkEntry { hashcode = hc, wineId = wineId, waterScore = score, contrabandLevel = contraLvl });
                    count++;
                }
                // 旧存档没有单独的联系人解锁标志，沿用已成功召唤的记录。
                if (LastNiniDialDay >= 0 || NiniPendingDay >= 0)
                    NiniDialedEver = true;
                // 旧版外部文件可能来自读档后的未来一天，不能让电话冷却穿越存档。
                int savedDay = TotalGameDays;
                int gameDay = GetCurrentGameDay();
                if (gameDay > 0)
                {
                    if (savedDay != gameDay)
                    {
                        LastNiniDialDay = -1;
                        NiniPendingDay = -1;
                        Trader47FinishedDay = -1;
                        Trader47StockedDay = -1;
                        MelonLogger.Msg("[BE-WINE] 存档天数 " + gameDay + " 与旧状态天数 " + savedDay
                            + " 不同，清除跨档访客冷却");
                    }
                    TotalGameDays = gameDay;
                    // 旧版只保存了拨号冷却，没有保存泥泥是否真的进入队列。
                    if (!hasPendingNiniState && LastNiniDialDay == gameDay)
                        LastNiniDialDay = -1;
                }
                if (LastNiniDialDay > TotalGameDays) LastNiniDialDay = -1;
                if (importedLegacy)
                {
                    SaveData();
                    MelonLogger.Msg("[BE-WINE] 已将旧版外部数据导入游戏存档；旧文件保留作备份");
                }
                MelonLogger.Msg("[BE-WINE] 槽 " + CurrentSlotId + " 读取 " + count + " 条，自定义天数=" + TotalGameDays
    + "，泥泥累计=" + CurrentTraderSaleTotal);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-WINE] LoadData 异常: " + e.Message); }
        }

        public override void OnDeinitializeMelon()
        {
            try { NativeGameEvents.Unregister(); }
            catch (Exception ex) { MelonLogger.Warning("[BE-NATIVE] Event shutdown failed: " + ex); }
            NativeWindowCallbacks.ClearStamps();
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Brewing Expansion Mod loaded!");
            NativeGameEvents.Register();
            Lang.Init();   // ★ 这行必须在
            // Register only managed factory delegates here; no Unity assets/items are created.
            RegisterMidautumnItems();
            try { BrewingMarketEvents.Register(); }
            catch (Exception e) { LoggerInstance.Warning("[BE-EVENT] 初始注册失败: " + e.Message); }

            // ★ 双语数组自检，长度对不上直接报错
            if (WineIds.Length != WineNamesZH.Length || WineIds.Length != WineNamesEN.Length)
                LoggerInstance.Error("[BE-LANG] WineIds=" + WineIds.Length
                    + " WineNamesZH=" + WineNamesZH.Length
                    + " WineNamesEN=" + WineNamesEN.Length + " 长度不一致！");
            if (WineSpriteKeys.Length != WineNamesZH.Length)
                LoggerInstance.Error("[BE-LANG] WineSpriteKeys=" + WineSpriteKeys.Length
                    + " 与 WineNamesZH=" + WineNamesZH.Length + " 不一致！");
            if (WineResourceNames.Length != WineNamesZH.Length)
                LoggerInstance.Error("[BE-LANG] WineResourceNames=" + WineResourceNames.Length
                    + " 与 WineNamesZH=" + WineNamesZH.Length + " 不一致！");

            //LoadAllPngBytes();
            //DumpWaterHelperMethods();
            //DumpAttractionSystem();
            //DumpBatteryClass();
            //DumpPowerHelper();
            //DumpItemSpriteModifierClass();
            //DumpSpriteDictMethods();
            //DumpGameItemElementMethods();
            //DumpGameItemElementFields();
            //DumpLoadedModClass();
            //MelonCoroutines.Start(DeferredInit());


        }

#if LIUNIAN_AGING_TEST
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            XIAOWOBrewingBarrel.LiunianAgingRuntimeTest.Schedule();
        }
#endif

#if GLOBAL_MACHINE_TEST
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            XIAOWOBrewingBarrel.GlobalMachineRuntimeTest.Schedule();
        }
#endif

        // ★ 临时诊断：探测游戏语言存在哪
        public static void DumpGameLanguage()
        {
            MelonLogger.Msg("[BE-LANG-DIAG] ===== 开始探测游戏语言 =====");

            // 1. 系统语言
            try
            {
                MelonLogger.Msg("[BE-LANG-DIAG] Application.systemLanguage = "
                    + UnityEngine.Application.systemLanguage);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-LANG-DIAG] systemLanguage 异常: " + e.Message); }

            // 2. PlayerPrefs 常见 key
            try
            {
                string[] keys = new string[] {
            "language", "Language", "lang", "Lang",
            "locale", "Locale", "currentLanguage", "CurrentLanguage",
            "selectedLanguage", "SelectedLanguage", "gameLanguage"
        };
                foreach (var k in keys)
                {
                    try
                    {
                        if (PlayerPrefs.HasKey(k))
                            MelonLogger.Msg("[BE-LANG-DIAG] PlayerPrefs['" + k + "'] = " + PlayerPrefs.GetString(k));
                    }
                    catch { }
                }
            }
            catch { }

            // 3. 扫描所有含 Language / Localization 的类
            try
            {
                var asm = typeof(PlayerStore).Assembly;
                foreach (var t in asm.GetTypes())
                {
                    string n = t.Name;
                    if (n.IndexOf("Language", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Localization", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Locale", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Setting", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    MelonLogger.Msg("[BE-LANG-DIAG] 候选类: " + t.FullName);

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    try
                    {
                        foreach (var f in t.GetFields(flags))
                        {
                            string fn = f.Name.ToLower();
                            if (fn.Contains("lang") || fn.Contains("locale"))
                            {
                                try
                                {
                                    object v = f.IsStatic ? f.GetValue(null) : null;
                                    MelonLogger.Msg("[BE-LANG-DIAG]   field(static) " + f.Name + " : " + f.FieldType.Name + " = " + (v?.ToString() ?? "?"));
                                }
                                catch { }
                            }
                        }
                        foreach (var p in t.GetProperties(flags))
                        {
                            string pn = p.Name.ToLower();
                            if (pn.Contains("lang") || pn.Contains("locale"))
                            {
                                try
                                {
                                    if (p.GetIndexParameters().Length > 0) continue;
                                    object v = (p.GetGetMethod(true)?.IsStatic == true) ? p.GetValue(null) : null;
                                    MelonLogger.Msg("[BE-LANG-DIAG]   prop(static) " + p.Name + " : " + p.PropertyType.Name + " = " + (v?.ToString() ?? "?"));
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-LANG-DIAG] 扫描类异常: " + e.Message); }

            // 4. 扫描 PlayerStore 上含 lang / locale 的字段
            try
            {
                var t = typeof(PlayerStore);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                foreach (var f in t.GetFields(flags))
                {
                    string fn = f.Name.ToLower();
                    if (fn.Contains("lang") || fn.Contains("locale") || fn.Contains("setting"))
                    {
                        object v = null;
                        try { if (f.IsStatic) v = f.GetValue(null); } catch { }
                        MelonLogger.Msg("[BE-LANG-DIAG] PlayerStore.field " + f.Name + " : " + f.FieldType.Name + " = " + (v?.ToString() ?? "?"));
                    }
                }
                foreach (var p in t.GetProperties(flags))
                {
                    string pn = p.Name.ToLower();
                    if (pn.Contains("lang") || pn.Contains("locale") || pn.Contains("setting"))
                    {
                        object v = null;
                        try { if (p.GetGetMethod(true)?.IsStatic == true) v = p.GetValue(null); } catch { }
                        MelonLogger.Msg("[BE-LANG-DIAG] PlayerStore.prop " + p.Name + " : " + p.PropertyType.Name + " = " + (v?.ToString() ?? "?"));
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-LANG-DIAG] PlayerStore 扫描异常: " + e.Message); }

            MelonLogger.Msg("[BE-LANG-DIAG] ===== 探测结束 =====");
        }

        // ★★★ 统一的延迟初始化：所有重型操作都在这里，延迟 3 秒 + 分帧
        private static IEnumerator DeferredInit()
        {
            yield return new WaitForSeconds(8.0f);
            try { InitMidautumnDelayedFactories(); }
            catch (Exception e) { MelonLogger.Warning("[BE] InitMidautumnDelayedFactories failed: " + e.Message); }
            yield return null;
            try
            {
                GenerateAllSprites();
                MelonLogger.Msg("[BE] Deferred init: sprite generation complete, total " + BEState.GeneratedSprites.Count);
            }
            catch (Exception e) { MelonLogger.Warning("[BE] Deferred init: sprite generation failed: " + e.Message); }
        }

        // ★★★ 新增：紧跟在 OnInitializeMelon 的结束 } 后面
        private static IEnumerator DeferredSpriteGeneration()
        {
            // 等一帧，让 Unity 主循环起来
            yield return null;

            try
            {
                GenerateAllSprites();
                MelonLogger.Msg("[BE] Deferred sprite generation complete, total " + BEState.GeneratedSprites.Count);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] Deferred sprite generation failed: " + e.Message);
            }
        }


        private static bool _midautumnRegistered = false;
        public static bool MidautumnRegisteredPublic { get { return _midautumnRegistered; } }
        public static void MarkMidautumnRegistered() { _midautumnRegistered = true; }

        // ★ 延迟工厂字典：5 个装饰类中秋物品，绕开 ItemManager 扫描

        private static bool _midautumnFactoriesInited = false;

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

        public static void RegisterMidautumnItems()
        {
            if (_midautumnRegistered) return;

            // ★ 只初始化延迟工厂字典，不注册到任何 Directory
            // 这样 ItemManager 扫描 FoodItemDirectory / MiscItemDirectory 时
            // 找不到中秋物品的 ID，实现「隐身」
            InitMidautumnDelayedFactories();

            MarkMidautumnRegistered();
            MelonLogger.Msg("[BE-MID] 中秋物品已激活（延迟工厂模式，未注册到目录），工厂数="
                + BEState.MidautumnDelayedFactories.Count);
        }

        private static bool _spriteAssetsReady = false;
        // A shop can open before the normal load/day callback has prepared every icon.
        // Check actual live sprites, not only the two starter sprites or a cached flag.
        private static readonly string[] ShopSpriteKeys =
        {
            IncubatorSpriteKey, WinePrinterSpriteKey, GoldenYeastReactorSpriteKey,
            AdvancedYeastReactorSpriteKey, ForbiddenYeastReactorSpriteKey,
            LowBlindboxSpriteKey, MidBlindboxSpriteKey, PremiumBlindboxSpriteKey,
            AfterglowBlindboxSpriteKey, WineBlindboxSpriteKey,
            FigurineBlindboxSpriteKey, ContainerBlindboxSpriteKey, CassetteBundleSpriteKey,
            QingyunFilterSpriteKey, NingluFilterSpriteKey, YingfeiFilterSpriteKey,
            XuxuBatterySpriteKey, XuxuMidBatterySpriteKey, XuxuHighBatterySpriteKey,
            CompactSmugglerBaySpriteKey, BrewerySignSpriteKey,
            MyPerfumeSpriteKey, FreedomPerfumeSpriteKey, NiniCardSpriteKey,
            HentaiYeastSpriteKey,
            ClearWaterTankSpriteKey, MultiStorageSpriteKey, MidautumnBlindboxSpriteKey
        };
        // ★ 记下所有四十七上架的物品，方便离场时清理
        public static readonly List<GameItem> Trader47StockList = new List<GameItem>();

        public static void EnsureSpriteAssetsReady()
        {
            if (_spriteAssetsReady && ShopSpritesAlive()) return;

            // New games can create custom items and visitors before LoadGame/BeginDay.
            try
            {
                LoadAllPngBytes();
                MelonLogger.Msg("[BE] PNG 加载完成，PngBytes=" + PngBytes.Count);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] LoadAllPngBytes 失败: " + e.Message);
                return;
            }

            try
            {
                Generated = false;          // ★ 强制重新生成（因为之前 Generated 被设成 true 了）
                GenerateAllSprites();
                MelonLogger.Msg("[BE] sprite 生成完成，共 " + BEState.GeneratedSprites.Count + " 个");
                _spriteAssetsReady = ShopSpritesAlive();
                if (!_spriteAssetsReady)
                    MelonLogger.Warning("[BE] 贴图初始化不完整，将在下次使用时重试");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] GenerateAllSprites 失败: " + e.Message); }
        }

        private static bool ShopSpritesAlive()
        {
            if (!IsSpriteAlive(FindOurSprite(SeedTrader47SpriteKey)))
            { MelonLogger.Warning("[BE-SPRITE] 缺少贴图: " + SeedTrader47SpriteKey); return false; }
            if (!IsSpriteAlive(FindOurSprite(SeedSpriteKeys[0])))
            { MelonLogger.Warning("[BE-SPRITE] 缺少贴图: " + SeedSpriteKeys[0]); return false; }
            foreach (var key in ShopSpriteKeys)
                if (!IsSpriteAlive(FindOurSprite(key)))
                { MelonLogger.Warning("[BE-SPRITE] 缺少商店贴图: " + key); return false; }
            foreach (var key in XIAOWOBrewingBarrel.XuxuWaterPurifier.SpriteKeys)
                if (!IsSpriteAlive(FindOurSprite(key)))
                { MelonLogger.Warning("[BE-SPRITE] 缺少净水器贴图: " + key); return false; }
            foreach (var key in FigurineSpriteKeys)
                if (!IsSpriteAlive(FindOurSprite(key)))
                { MelonLogger.Warning("[BE-SPRITE] 缺少手办贴图: " + key); return false; }
            return true;
        }

        // ★ 新增：dump 吸引力系统
        public static void DumpAttractionSystem()
        {
            try
            {
                // 1. PlayerStore 里跟 attraction / appeal / visitor / client 相关的字段和方法
                MelonLogger.Msg("[BE-ATTRACT] ===== PlayerStore =====");
                DumpMembers(typeof(PlayerStore), new[] { "attract", "appeal", "visitor", "client", "reputation", "shop", "store", "level" });

                // 2. StoreClientManager
                MelonLogger.Msg("[BE-ATTRACT] ===== StoreClientManager =====");
                DumpMembers(typeof(StoreClientManager), null);

                // 3. ShowcaseHelper（展示柜，可能有吸引力加成）
                MelonLogger.Msg("[BE-ATTRACT] ===== ShowcaseHelper =====");
                DumpMembers(typeof(ShowcaseHelper), null);

                // 4. 试试有没有专门的吸引力类
                var asm = typeof(PlayerStore).Assembly;
                foreach (var t in asm.GetTypes())
                {
                    string n = t.Name;
                    if (n.IndexOf("Attract", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Appeal", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("Visitor", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        MelonLogger.Msg("[BE-ATTRACT] 候选类: " + t.FullName);
                        DumpMembers(t, null);
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-ATTRACT] DumpAttractionSystem 异常: " + e.Message); }
        }

        // ★ 诊断：找原版电池相关的类
        public static void DumpBatteryClass()
        {
            try
            {
                var asm = typeof(PlayerStore).Assembly;
                foreach (var t in asm.GetTypes())
                {
                    string n = t.Name;
                    if (n.IndexOf("Energy", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Breeder", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Credit", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    MelonLogger.Msg("[BE-BAT-DUMP] 候选类: " + t.FullName);
                    DumpMembers(t, null);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-BAT-DUMP] 异常: " + e.Message); }
        }

        // ★ 新增：dump PowerHelper 找每晚恢复逻辑
        public static void DumpPowerHelper()
        {
            try
            {
                var t = typeof(PowerHelper);
                var flags = BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Instance | BindingFlags.Static;
                MelonLogger.Msg("[BE-PWR-DUMP] === PowerHelper 全部方法 ===");
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    if (m.IsSpecialName) continue;
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-PWR-DUMP]   ").Append(m.ReturnType.Name).Append(" ")
                      .Append(m.Name).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }

                try
                {
                    var gi = DirectoryMaster.Item("energy_credit_breeder", false);
                    if (gi != null) DumpAllPrimitiveFields(gi, "energy_credit_breeder 模板");
                }
                catch { }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PWR-DUMP] " + e.Message); }
        }   // ← 新方法结束

        // ★ dump 电池所有 tag 及 valueInt

        public static void DumpBatteryTags(GameItem it, string label)
        {
            if (it == null) return;
            try
            {
                MelonLogger.Msg("[BE-BAT-TAG] === " + label + " ===");

                // ★ 1. 拿 state 属性
                object state = null;
                try
                {
                    var p = typeof(GameItem).GetProperty("state",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (p != null) state = p.GetValue(it);
                }
                catch (Exception e) { MelonLogger.Warning("[BE-BAT-TAG] 读 state 异常: " + e.Message); }

                if (state == null)
                {
                    MelonLogger.Warning("[BE-BAT-TAG] state == null");
                    return;
                }

                var st = state.GetType();
                MelonLogger.Msg("[BE-BAT-TAG] state 类型: " + st.FullName);

                // ★ 2. dump TagSystem 所有字段和属性
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                MelonLogger.Msg("[BE-BAT-TAG] --- TagSystem 字段 ---");
                foreach (var f in st.GetFields(flags))
                {
                    if (f.Name.StartsWith("Native")) continue;
                    string vs = "?";
                    try
                    {
                        var v = f.GetValue(state);
                        vs = (v == null) ? "null"
                            : (f.FieldType.IsPrimitive ? v.ToString()
                            : (v is string s ? s : "<" + v.GetType().Name + ">"));
                    }
                    catch { }
                    MelonLogger.Msg("[BE-BAT-TAG]   field " + f.Name + " : " + f.FieldType.Name + " = " + vs);
                }

                MelonLogger.Msg("[BE-BAT-TAG] --- TagSystem 属性 ---");
                foreach (var p in st.GetProperties(flags))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    string vs = "?";
                    try
                    {
                        var v = p.GetValue(state);
                        vs = (v == null) ? "null"
                            : (p.PropertyType.IsPrimitive ? v.ToString()
                            : (v is string s ? s : "<" + v.GetType().Name + ">"));
                    }
                    catch { }
                    MelonLogger.Msg("[BE-BAT-TAG]   prop " + p.Name + " : " + p.PropertyType.Name + " = " + vs);
                }

                MelonLogger.Msg("[BE-BAT-TAG] --- TagSystem 方法 ---");
                foreach (var m in st.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    if (m.IsSpecialName) continue;
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-BAT-TAG]   ").Append(m.ReturnType.Name).Append(" ").Append(m.Name).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
                // ★ 遍历 state.dict 所有 tag
                try
                {
                    object dictObj = null;
                    try { dictObj = st.GetProperty("dict", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state); } catch { }
                    if (dictObj == null)
                        try { dictObj = st.GetProperty("_dict_k__BackingField", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state); } catch { }
                    if (dictObj == null) { MelonLogger.Warning("[BE-BAT-TAG] dict 为 null"); return; }

                    var dt = dictObj.GetType();
                    var countProp = dt.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                    int cnt = -1;
                    try { if (countProp != null) cnt = (int)countProp.GetValue(dictObj); } catch { }
                    MelonLogger.Msg("[BE-BAT-TAG] === state.dict 共 " + cnt + " 个 tag ===");

                    var keysProp = dt.GetProperty("Keys", BindingFlags.Public | BindingFlags.Instance);
                    var keysObj = keysProp?.GetValue(dictObj);
                    if (keysObj == null) { MelonLogger.Warning("[BE-BAT-TAG] Keys 为 null"); return; }

                    var getEnumM = keysObj.GetType().GetMethod("GetEnumerator", Type.EmptyTypes);
                    var en = getEnumM?.Invoke(keysObj, null);
                    if (en == null) { MelonLogger.Warning("[BE-BAT-TAG] 无法枚举 Keys"); return; }

                    var moveNextM = en.GetType().GetMethod("MoveNext", Type.EmptyTypes);
                    var currentProp = en.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
                    var itemProp = dt.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);

                    int idx = 0;
                    while (idx < 100)
                    {
                        bool more = false;
                        try { more = (bool)moveNextM.Invoke(en, null); } catch { }
                        if (!more) break;

                        object key = null;
                        try { key = currentProp.GetValue(en); } catch { }
                        string keyStr = key?.ToString() ?? "null";

                        object val = null;
                        try { if (itemProp != null) val = itemProp.GetValue(dictObj, new object[] { key }); } catch { }

                        int vi = int.MinValue;
                        float vf = float.MinValue;
                        string vs = null;
                        if (val != null)
                        {
                            try { var p = val.GetType().GetProperty("valueInt", BindingFlags.Public | BindingFlags.Instance); if (p != null) vi = (int)p.GetValue(val); } catch { }
                            try { var p = val.GetType().GetProperty("valueFloat", BindingFlags.Public | BindingFlags.Instance); if (p != null) vf = (float)p.GetValue(val); } catch { }
                            try { var p = val.GetType().GetProperty("valueString", BindingFlags.Public | BindingFlags.Instance); if (p != null) vs = (string)p.GetValue(val); } catch { }
                        }
                        MelonLogger.Msg("[BE-BAT-TAG]   [" + idx + "] '" + keyStr + "' | valueInt=" + vi + " valueFloat=" + vf + " valueString=" + vs);
                        idx++;
                    }
                }
                catch (Exception ex) { MelonLogger.Warning("[BE-BAT-TAG] 遍历 dict 异常: " + ex.Message); }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-BAT-TAG] 异常: " + e.Message); }
        }

        // ★ 直接写回 TagSystem.dict，绕过 GetTagReadonly 的值拷贝问题
        public static bool SetTagInt(GameItem item, string tagName, int value)
        {
            if (item == null) return false;
            try
            {
                // 1. 拿 state
                var stateProp = typeof(GameItem).GetProperty("state",
                    BindingFlags.Public | BindingFlags.Instance);
                object state = stateProp?.GetValue(item);
                if (state == null)
                {
                    MelonLogger.Warning("[BE-TAG-SET] state == null");
                    return false;
                }

                // 2. 拿 dict
                var dictProp = state.GetType().GetProperty("dict",
                    BindingFlags.Public | BindingFlags.Instance);
                object dict = dictProp?.GetValue(state);
                if (dict == null)
                {
                    MelonLogger.Warning("[BE-TAG-SET] dict == null");
                    return false;
                }

                var dictType = dict.GetType();
                var flags = BindingFlags.Public | BindingFlags.Instance;

                // 3. 取 TagState
                object tagState = null;
                var itemProp = dictType.GetProperty("Item", flags);
                if (itemProp != null)
                {
                    try { tagState = itemProp.GetValue(dict, new object[] { tagName }); }
                    catch (Exception e) { MelonLogger.Warning("[BE-TAG-SET] 取 " + tagName + " 异常: " + e.Message); }
                }

                if (tagState == null)
                {
                    MelonLogger.Warning("[BE-TAG-SET] " + tagName + " 不存在");
                    return false;
                }

                // 4. 改 valueInt
                var viProp = tagState.GetType().GetProperty("valueInt", flags);
                if (viProp == null || !viProp.CanWrite)
                {
                    MelonLogger.Warning("[BE-TAG-SET] " + tagName + " 没有可写 valueInt");
                    return false;
                }

                int old = (int)viProp.GetValue(tagState);
                viProp.SetValue(tagState, value);
                int now = (int)viProp.GetValue(tagState);

                // 5. 塞回字典：先试 Item 属性 setter，再试 set_Item 方法
                bool written = false;
                try
                {
                    itemProp.SetValue(dict, new object[] { tagName, tagState });
                    written = true;
                }
                catch { }

                if (!written)
                {
                    var setItem = dictType.GetMethod("set_Item", flags);
                    if (setItem != null)
                    {
                        try
                        {
                            setItem.Invoke(dict, new object[] { tagName, tagState });
                            written = true;
                        }
                        catch (Exception e) { MelonLogger.Warning("[BE-TAG-SET] set_Item 异常: " + e.Message); }
                    }
                }

                if (!written)
                {
                    MelonLogger.Warning("[BE-TAG-SET] " + tagName + " 写回字典失败");
                    return false;
                }

                MelonLogger.Msg("[BE-TAG-SET] " + tagName + " : " + old + " → " + now);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-TAG-SET] 异常: " + e.Message);
                return false;
            }
        }
        // 递归 dump 一个 tag 容器
        private static void DumpTagContainer(object container)
        {
            if (container == null) return;
            try
            {
                var t = container.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // 试当 List/Dictionary
                var countProp = t.GetProperty("Count", flags);
                if (countProp != null)
                {
                    int n = -1;
                    try { n = (int)countProp.GetValue(container); } catch { }
                    MelonLogger.Msg("[BE-BAT-TAG]   容器 Count=" + n);

                    var itemProp = t.GetProperty("Item", flags);
                    var keysProp = t.GetProperty("Keys", flags);

                    if (keysProp != null)
                    {
                        // Dictionary
                        try
                        {
                            var keys = keysProp.GetValue(container) as System.Collections.IEnumerable;
                            if (keys != null && itemProp != null)
                            {
                                foreach (var k in keys)
                                {
                                    try
                                    {
                                        var v = itemProp.GetValue(container, new object[] { k });
                                        DumpTagEntry(k?.ToString(), v);
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                    else if (itemProp != null)
                    {
                        // List
                        for (int i = 0; i < n; i++)
                        {
                            try
                            {
                                var v = itemProp.GetValue(container, new object[] { i });
                                DumpTagEntry("[" + i + "]", v);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-BAT-TAG] DumpTagContainer 异常: " + e.Message); }
        }

        private static void DumpTagEntry(string key, object state)
        {
            if (state == null) { MelonLogger.Msg("[BE-BAT-TAG]     " + key + " = null"); return; }
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var st = state.GetType();
                string info = key + " (" + st.Name + ")";

                // 打印几个可能的字段
                string[] fields = { "identifier", "valueInt", "valueFloat", "valueString", "valueBool", "tagName" };
                foreach (var fn in fields)
                {
                    try
                    {
                        var p = st.GetProperty(fn, flags);
                        if (p != null && p.CanRead)
                        {
                            var v = p.GetValue(state);
                            info += " | " + fn + "=" + (v?.ToString() ?? "null");
                        }
                    }
                    catch { }
                }
                MelonLogger.Msg("[BE-BAT-TAG]     " + info);
            }
            catch { }
        }
        // ★ 诊断：dump ItemSpriteModifier 类
        public static void DumpItemSpriteModifierClass()
        {
            try
            {
                var t = typeof(ItemSpriteModifier);
                MelonLogger.Msg("[BE-ISM-DUMP] === ItemSpriteModifier ===");
                DumpMembers(t, null);

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var c in t.GetConstructors(flags))
                {
                    var ps = c.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-ISM-DUMP]   .ctor(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-ISM-DUMP] 异常: " + e.Message); }
        }
        // ★ 新增：dump SpriteDict 所有 Get/Resolve 相关方法
        public static void DumpSpriteDictMethods()
        {
            try
            {
                var t = typeof(SpriteDict);
                var flags = BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Instance | BindingFlags.Static;
                MelonLogger.Msg("[BE-SD] === SpriteDict 方法 ===");
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    string n = m.Name;
                    if (n.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Get", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("Resolve", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-SD]   ").Append(m.ReturnType.Name).Append(" ")
                      .Append(n).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-SD] " + e.Message); }
        }

        public static void DumpGameItemElementMethods()
        {
            try
            {
                var t = typeof(GameItemElement);
                var flags = BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Instance | BindingFlags.Static;
                MelonLogger.Msg("[BE-GIE] === GameItemElement 方法 ===");
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    string n = m.Name.ToLower();
                    if (n.Contains("sprite") || n.Contains("atlas") || n.Contains("load")
                        || n.Contains("resolve") || n.Contains("icon") || n.Contains("image"))
                    {
                        var ps = m.GetParameters();
                        var sb = new System.Text.StringBuilder();
                        sb.Append("[BE-GIE]   ").Append(m.ReturnType.Name).Append(" ")
                          .Append(m.Name).Append("(");
                        for (int i = 0; i < ps.Length; i++)
                        {
                            if (i > 0) sb.Append(", ");
                            sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                        }
                        sb.Append(")");
                        MelonLogger.Msg(sb.ToString());
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-GIE] " + e.Message); }
        }

        public static void DumpGameItemElementFields()
        {
            try
            {
                var t = typeof(GameItemElement);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                MelonLogger.Msg("[BE-GIE-FIELDS] === GameItemElement 字段 ===");
                foreach (var f in t.GetFields(flags))
                {
                    if (f.Name.StartsWith("Native")) continue;
                    MelonLogger.Msg("[BE-GIE-FIELDS]   " + f.Name + " : " + f.FieldType.Name);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-GIE-FIELDS] " + e.Message); }
        }

        public static void DumpLoadedModClass()
        {
            try
            {
                var t = AccessTools.TypeByName("LoadedMod") ?? AccessTools.TypeByName("Il2Cpp.LoadedMod");
                if (t == null) { MelonLogger.Warning("[BE-LM] 找不到 LoadedMod 类"); return; }
                MelonLogger.Msg("[BE-LM] === LoadedMod (" + t.FullName + ") ===");
                var flags = BindingFlags.Public | BindingFlags.NonPublic
                          | BindingFlags.Instance | BindingFlags.Static;
                foreach (var f in t.GetFields(flags))
                {
                    if (f.Name.StartsWith("Native")) continue;
                    MelonLogger.Msg("[BE-LM]   field " + f.Name + " : " + f.FieldType.Name);
                }
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    if (m.IsSpecialName) continue;
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-LM]   ").Append(m.ReturnType.Name).Append(" ")
                      .Append(m.Name).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-LM] " + e.Message); }
        }

        // ★ 诊断：dump 原版 energy_credit_breeder 模板
        public static void DumpVanillaBatteryTemplate()
        {
            try
            {
                var gi = DirectoryMaster.Item("energy_credit_breeder", false);
                if (gi == null) { MelonLogger.Warning("[BE-VBAT] 拿不到原版模板"); return; }

                MelonLogger.Msg("[BE-VBAT] === 原版 energy_credit_breeder 模板 ===");
                try { MelonLogger.Msg("[BE-VBAT]   name=" + (gi.name ?? "?")); } catch { }
                try { MelonLogger.Msg("[BE-VBAT]   identifier=" + (gi.identifier ?? "?")); } catch { }
                try { MelonLogger.Msg("[BE-VBAT]   unitValue=" + gi.unitValue); } catch { }
                try { MelonLogger.Msg("[BE-VBAT]   unitBaseValue=" + gi.unitBaseValue); } catch { }

                try
                {
                    var types = gi.itemTypes;
                    if (types != null)
                        for (int i = 0; i < types.Count; i++)
                            MelonLogger.Msg("[BE-VBAT]   itemType[" + i + "]=" + types[i]);
                }
                catch { }

                DumpAllPrimitiveFields(gi, "energy_credit_breeder 模板");

                // 所有含 "sprite" 的字段
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                foreach (var f in gi.GetType().GetFields(flags))
                {
                    try
                    {
                        string fn = f.Name.ToLower();
                        if (!fn.Contains("sprite") && !fn.Contains("name")) continue;
                        if (f.Name.StartsWith("Native")) continue;
                        object v = f.GetValue(gi);
                        MelonLogger.Msg("[BE-VBAT]   sprite字段 " + f.Name + " = " + (v?.ToString() ?? "null"));
                    }
                    catch { }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-VBAT] 异常: " + e.Message); }
        }
        private static void DumpMembers(Type t, string[] keywordFilter)
        {
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

                // 字段
                foreach (var f in t.GetFields(flags))
                {
                    if (f.Name.StartsWith("Native")) continue;
                    if (keywordFilter != null)
                    {
                        bool hit = false;
                        for (int i = 0; i < keywordFilter.Length; i++)
                            if (f.Name.IndexOf(keywordFilter[i], StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                        if (!hit) continue;
                    }
                    MelonLogger.Msg("[BE-ATTRACT]   field " + f.Name + " : " + f.FieldType.Name);
                }

                // 方法
                foreach (var m in t.GetMethods(flags))
                {
                    if (m.Name.StartsWith("Native")) continue;
                    if (m.IsSpecialName && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_")) continue;
                    if (keywordFilter != null)
                    {
                        bool hit = false;
                        for (int i = 0; i < keywordFilter.Length; i++)
                            if (m.Name.IndexOf(keywordFilter[i], StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                        if (!hit) continue;
                    }
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-ATTRACT]   ").Append(m.ReturnType.Name).Append(" ").Append(m.Name).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-ATTRACT] DumpMembers " + t.Name + " 异常: " + e.Message); }
        }

        private static void LoadAllPngBytes()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string texDir = Path.Combine(root, "Mods", "BrewingExpansion", "Textures");
            int fromFile = 0, fromDll = 0;
            var asm = System.Reflection.Assembly.GetExecutingAssembly();

            for (int i = 0; i < SeedPngNames.Length; i++)
            {
                string key = SeedSpriteKeys[i];
                string p = Path.Combine(texDir, SeedPngNames[i] + ".png");
                if (File.Exists(p)) { PngBytes[key] = File.ReadAllBytes(p); fromFile++; continue; }
                if (i < SeedResourceNames.Length)
                    using (var s = asm.GetManifestResourceStream(SeedResourceNames[i]))
                        if (s != null) { using (var ms = new MemoryStream()) { s.CopyTo(ms); PngBytes[key] = ms.ToArray(); fromDll++; } continue; }
                MelonLogger.Warning("[BE] 种子 PNG 缺失: " + SeedPngNames[i]);
            }

            for (int i = 0; i < FruitPngNames.Length; i++)
            {
                string key = FruitSpriteKeys[i];
                string p = Path.Combine(texDir, FruitPngNames[i] + ".png");
                if (File.Exists(p)) { PngBytes[key] = File.ReadAllBytes(p); fromFile++; continue; }
                if (i < FruitResourceNames.Length)
                    using (var s = asm.GetManifestResourceStream(FruitResourceNames[i]))
                        if (s != null) { using (var ms = new MemoryStream()) { s.CopyTo(ms); PngBytes[key] = ms.ToArray(); fromDll++; } continue; }
                MelonLogger.Warning("[BE] 果实 PNG 缺失: " + FruitPngNames[i]);
            }

            {
                string p = Path.Combine(texDir, FallbackWinePngName + ".png");
                if (File.Exists(p)) { PngBytes[FallbackWineKey] = File.ReadAllBytes(p); fromFile++; }
                else
                    using (var s = asm.GetManifestResourceStream(FallbackWineResourceName))
                        if (s != null) { using (var ms = new MemoryStream()) { s.CopyTo(ms); PngBytes[FallbackWineKey] = ms.ToArray(); fromDll++; } }
            }
            // ★ 加这一句：把同一张 PNG 也注册到霓虹残梦自己的 key
            if (PngBytes.ContainsKey(FallbackWineKey))
                PngBytes[NeonDreamWineSpriteKey] = PngBytes[FallbackWineKey];

            // ★ 强制用中文名去磁盘找（外置 Textures 文件夹里的文件名都是中文）
            for (int i = 0; i < WineNamesZH.Length; i++)
            {
                string key = WineSpriteKeys[i];
                string p = Path.Combine(texDir, WineNamesZH[i] + ".png");
                if (File.Exists(p)) { PngBytes[key] = File.ReadAllBytes(p); fromFile++; continue; }
                if (i < WineResourceNames.Length)
                    using (var s = asm.GetManifestResourceStream(WineResourceNames[i]))
                        if (s != null) { using (var ms = new MemoryStream()) { s.CopyTo(ms); PngBytes[key] = ms.ToArray(); fromDll++; } continue; }
                if (PngBytes.ContainsKey(FallbackWineKey)) PngBytes[key] = PngBytes[FallbackWineKey];
            }

            LoadSingle(asm, texDir, IncubatorPngName, IncubatorSpriteKey, IncubatorResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, WinePrinterPngName, WinePrinterSpriteKey, WinePrinterResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < CertPngNames.Length; i++)
                LoadSingle(asm, texDir, CertPngNames[i], CertSpriteKeys[i], CertResourceNames[i], ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, GoldenYeastReactorPngName, GoldenYeastReactorSpriteKey, GoldenYeastReactorResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, AdvancedYeastReactorPngName, AdvancedYeastReactorSpriteKey, AdvancedYeastReactorResourceName, ref fromFile, ref fromDll, GoldenYeastReactorSpriteKey);
            LoadSingle(asm, texDir, ForbiddenYeastReactorPngName, ForbiddenYeastReactorSpriteKey, ForbiddenYeastReactorResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, BrewerySignPngName, BrewerySignSpriteKey, BrewerySignResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, NiniCardPngName, NiniCardSpriteKey, NiniCardResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MyPerfumePngName, MyPerfumeSpriteKey, MyPerfumeResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, FreedomPerfumePngName, FreedomPerfumeSpriteKey, FreedomPerfumeResourceName, ref fromFile, ref fromDll, MyPerfumeSpriteKey);
            LoadSingle(asm, texDir, WeeklyTraderPngName, WeeklyTraderSpriteKey, WeeklyTraderResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, SeedTrader47PngName, SeedTrader47SpriteKey, SeedTrader47ResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, BrewingManualPngName, BrewingManualSpriteKey, BrewingManualResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < MultiStorageVariantPngNames.Length; i++)
                LoadSingle(asm, texDir, MultiStorageVariantPngNames[i], MultiStorageVariantSpriteKeys[i],
                           MultiStorageVariantResourceNames[i], ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, MultiStoragePngName, MultiStorageSpriteKey, MultiStorageResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            MakeSpriteIndependent(MultiStorageSpriteKey, WORLD_UNITS_PER_CELL * 2);
            for (int i = 0; i < MultiStorageVariantSpriteKeys.Length; i++)
                MakeSpriteIndependent(MultiStorageVariantSpriteKeys[i], WORLD_UNITS_PER_CELL * 2);
            LoadSingle(asm, texDir, CompactSmugglerBayPngName, CompactSmugglerBaySpriteKey, CompactSmugglerBayResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, ClearWaterTankPngName, ClearWaterTankSpriteKey, ClearWaterTankResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, ClearWaterTankPngName, ClearWaterTankSpriteKey, ClearWaterTankResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            // ★ 新增三个杯子
            LoadSingle(asm, texDir, XuxuWarmCupPngName, XuxuWarmCupSpriteKey, XuxuWarmCupResourceName, ref fromFile, ref fromDll, ClearWaterTankSpriteKey);
            LoadSingle(asm, texDir, XuxuSweetCupPngName, XuxuSweetCupSpriteKey, XuxuSweetCupResourceName, ref fromFile, ref fromDll, ClearWaterTankSpriteKey);
            LoadSingle(asm, texDir, Love47CupPngName, Love47CupSpriteKey, Love47CupResourceName, ref fromFile, ref fromDll, ClearWaterTankSpriteKey);
            LoadSingle(asm, texDir, QingyunFilterPngName, QingyunFilterSpriteKey, QingyunFilterResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, NingluFilterPngName, NingluFilterSpriteKey, NingluFilterResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, YingfeiFilterPngName, YingfeiFilterSpriteKey, YingfeiFilterResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < XIAOWOBrewingBarrel.XuxuWaterPurifier.Ids.Length; i++)
                LoadSingle(asm, texDir, XIAOWOBrewingBarrel.XuxuWaterPurifier.PngNames[i],
                    XIAOWOBrewingBarrel.XuxuWaterPurifier.SpriteKeys[i],
                    "BrewingExpansion.Textures." + XIAOWOBrewingBarrel.XuxuWaterPurifier.PngNames[i] + ".png",
                    ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, PremiumBlindboxPngName, PremiumBlindboxSpriteKey, PremiumBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, AfterglowBlindboxPngName, AfterglowBlindboxSpriteKey, AfterglowBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            // ★ 收藏酒系列
            LoadSingle(asm, texDir, ShuangqingWinePngName, ShuangqingWineSpriteKey, ShuangqingWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, NanfengWinePngName, NanfengWineSpriteKey, NanfengWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, FeimeiWinePngName, FeimeiWineSpriteKey, FeimeiWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, NeonIllusionWinePngName, NeonIllusionWineSpriteKey, NeonIllusionWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, IdealEndWinePngName, IdealEndWineSpriteKey, IdealEndWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, LuofuWinePngName, LuofuWineSpriteKey, LuofuWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MagicPigWinePngName, MagicPigWineSpriteKey, MagicPigWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, SeaBlueWinePngName, SeaBlueWineSpriteKey, SeaBlueWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, NianHeshiWinePngName, NianHeshiWineSpriteKey, NianHeshiWineResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, HentaiYeastPngName, HentaiYeastSpriteKey, HentaiYeastResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, RainbowRecipeWinePngName, RainbowRecipeWineSpriteKey, RainbowRecipeWineResourceName, ref fromFile, ref fromDll, null);
            // ★ 中秋节系列
            LoadSingle(asm, texDir, MidautumnMoonFanPngName, MidautumnMoonFanSpriteKey, MidautumnMoonFanResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnMoonLampPngName, MidautumnMoonLampSpriteKey, MidautumnMoonLampResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnJadeRabbitPngName, MidautumnJadeRabbitSpriteKey, MidautumnJadeRabbitResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnLanternPngName, MidautumnLanternSpriteKey, MidautumnLanternResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnMooncakeBoxPngName, MidautumnMooncakeBoxSpriteKey, MidautumnMooncakeBoxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnBagPngName, MidautumnBagSpriteKey, MidautumnBagResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, SakuraBagPngName, SakuraBagSpriteKey, SakuraBagResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, CyberBagPngName, CyberBagSpriteKey, CyberBagResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, LaolishiBagPngName, LaolishiBagSpriteKey, LaolishiBagResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, CatYouyouBagPngName, CatYouyouBagSpriteKey, CatYouyouBagResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);

            LoadSingle(asm, texDir, XuxuBatteryPngName, XuxuBatterySpriteKey, XuxuBatteryResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, XuxuBatteryPngName4, XuxuBatterySpriteKey4, XuxuBatteryResourceName4, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuBatteryPngName3, XuxuBatterySpriteKey3, XuxuBatteryResourceName3, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuBatteryPngName2, XuxuBatterySpriteKey2, XuxuBatteryResourceName2, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuBatteryPngName1, XuxuBatterySpriteKey1, XuxuBatteryResourceName1, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuBatteryPngName0, XuxuBatterySpriteKey0, XuxuBatteryResourceName0, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName, XuxuMidBatterySpriteKey, XuxuMidBatteryResourceName, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName4, XuxuMidBatterySpriteKey4, XuxuMidBatteryResourceName4, ref fromFile, ref fromDll, XuxuBatterySpriteKey4);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName3, XuxuMidBatterySpriteKey3, XuxuMidBatteryResourceName3, ref fromFile, ref fromDll, XuxuBatterySpriteKey3);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName2, XuxuMidBatterySpriteKey2, XuxuMidBatteryResourceName2, ref fromFile, ref fromDll, XuxuBatterySpriteKey2);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName1, XuxuMidBatterySpriteKey1, XuxuMidBatteryResourceName1, ref fromFile, ref fromDll, XuxuBatterySpriteKey1);
            LoadSingle(asm, texDir, XuxuMidBatteryPngName0, XuxuMidBatterySpriteKey0, XuxuMidBatteryResourceName0, ref fromFile, ref fromDll, XuxuBatterySpriteKey0);

            LoadSingle(asm, texDir, XuxuHighBatteryPngName, XuxuHighBatterySpriteKey, XuxuHighBatteryResourceName, ref fromFile, ref fromDll, XuxuBatterySpriteKey);
            LoadSingle(asm, texDir, XuxuHighBatteryPngName4, XuxuHighBatterySpriteKey4, XuxuHighBatteryResourceName4, ref fromFile, ref fromDll, XuxuBatterySpriteKey4);
            LoadSingle(asm, texDir, XuxuHighBatteryPngName3, XuxuHighBatterySpriteKey3, XuxuHighBatteryResourceName3, ref fromFile, ref fromDll, XuxuBatterySpriteKey3);
            LoadSingle(asm, texDir, XuxuHighBatteryPngName2, XuxuHighBatterySpriteKey2, XuxuHighBatteryResourceName2, ref fromFile, ref fromDll, XuxuBatterySpriteKey2);
            LoadSingle(asm, texDir, XuxuHighBatteryPngName1, XuxuHighBatterySpriteKey1, XuxuHighBatteryResourceName1, ref fromFile, ref fromDll, XuxuBatterySpriteKey1);
            LoadSingle(asm, texDir, XuxuHighBatteryPngName0, XuxuHighBatterySpriteKey0, XuxuHighBatteryResourceName0, ref fromFile, ref fromDll, XuxuBatterySpriteKey0);
            LoadSingle(asm, texDir, WurenMooncakePngName, WurenMooncakeSpriteKey, WurenMooncakeResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnBlindboxPngName, MidautumnBlindboxSpriteKey, MidautumnBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, LowBlindboxPngName, LowBlindboxSpriteKey, LowBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidBlindboxPngName, MidBlindboxSpriteKey, MidBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, WineBlindboxPngName, WineBlindboxSpriteKey, WineBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, ContainerBlindboxPngName, ContainerBlindboxSpriteKey, ContainerBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, FigurineBlindboxPngName, FigurineBlindboxSpriteKey, FigurineBlindboxResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, CassetteBundlePngName, CassetteBundleSpriteKey, CassetteBundleResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < XIAOWOBrewingBarrel.CustomCassettes.PngNames.Length; i++)
                LoadSingle(asm, texDir, XIAOWOBrewingBarrel.CustomCassettes.PngNames[i],
                    XIAOWOBrewingBarrel.CustomCassettes.SpriteKeys[i],
                    "BrewingExpansion.Textures." + XIAOWOBrewingBarrel.CustomCassettes.PngNames[i] + ".png",
                    ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < FigurineIds.Length; i++)
                LoadSingle(asm, texDir, FigurinePngNames[i], FigurineSpriteKeys[i],
                    "BrewingExpansion.Textures." + FigurinePngNames[i] + ".png", ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnMoonOsmanthusPngName, MidautumnMoonOsmanthusSpriteKey, MidautumnMoonOsmanthusResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            LoadSingle(asm, texDir, MidautumnOsmanthusWinePngName, MidautumnOsmanthusWineSpriteKey, MidautumnOsmanthusWineResourceName, ref fromFile, ref fromDll, IncubatorSpriteKey);
            for (int i = 0; i < RecipePngNames.Length; i++)
                LoadSingle(asm, texDir, RecipePngNames[i], RecipeSpriteKeys[i], RecipeResourceNames[i], ref fromFile, ref fromDll, IncubatorSpriteKey);

            // ★ 检查哪些酒没有独立贴图（会被霓虹残梦替代）
            try
            {
                var missing = new List<string>();
                for (int i = 0; i < WineNamesZH.Length; i++)
                {
                    // Resource files keep their Chinese filenames in every locale.
                    string winePngName = WineNamesZH[i];
                    string p = Path.Combine(texDir, winePngName + ".png");
                    bool hasFile = File.Exists(p);
                    bool hasResource = false;

                    if (!hasFile && i < WineResourceNames.Length)
                    {
                        try
                        {
                            using (var s = asm.GetManifestResourceStream(WineResourceNames[i]))
                                if (s != null) hasResource = true;
                        }
                        catch { }
                    }

                    if (!hasFile && !hasResource)
                        missing.Add(winePngName);
                }

                if (missing.Count > 0)
                    MelonLogger.Warning("[BE] " + missing.Count + " 种酒没有独立贴图（显示为霓虹残梦）: " + string.Join(", ", missing));
                else
                    MelonLogger.Msg("[BE] 所有 " + WineNames.Length + " 种酒都有独立贴图");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 检查酒贴图异常: " + e.Message); }
            MelonLogger.Msg("[BE] 已加载 " + PngBytes.Count + " 张 PNG（外部 " + fromFile + " + DLL " + fromDll + "）");

        }

        private static void LoadSingle(System.Reflection.Assembly asm, string texDir, string pngName, string key, string resourceName, ref int fromFile, ref int fromDll, string fallbackKey)
        {
            string p = Path.Combine(texDir, pngName + ".png");
            if (File.Exists(p)) { PngBytes[key] = File.ReadAllBytes(p); fromFile++; return; }
            using (var s = asm.GetManifestResourceStream(resourceName))
            {
                if (s != null) { using (var ms = new MemoryStream()) { s.CopyTo(ms); PngBytes[key] = ms.ToArray(); fromDll++; } return; }
            }
            if (!string.IsNullOrEmpty(fallbackKey) && PngBytes.ContainsKey(fallbackKey))
                PngBytes[key] = PngBytes[fallbackKey];
        }

        // HandleInitialItem runs before the normal in-game texture load on a new save.
        // Prepare only the two custom starter items before their inventory UI is created.
        public static void EnsureStarterItemSprites()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            string texDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "BrewingExpansion", "Textures");
            int fromFile = 0, fromDll = 0;
            LoadSingle(asm, texDir, ClearWaterTankPngName, ClearWaterTankSpriteKey,
                ClearWaterTankResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, BrewerySignPngName, BrewerySignSpriteKey,
                BrewerySignResourceName, ref fromFile, ref fromDll, null);
            LoadSingle(asm, texDir, IncubatorPngName, IncubatorSpriteKey,
                IncubatorResourceName, ref fromFile, ref fromDll, null);
            MakeSpriteIndependent(ClearWaterTankSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(BrewerySignSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(IncubatorSpriteKey, WORLD_UNITS_PER_CELL * 3);
            if (FindOurSprite(ClearWaterTankSpriteKey) == null || FindOurSprite(BrewerySignSpriteKey) == null
                || FindOurSprite(IncubatorSpriteKey) == null)
                MelonLogger.Warning("[BE-PERK] 开局物品贴图加载失败");
        }

        // ============ 物品创建 ============
        public static GameItem CreateSeedItem(string seedId, string displayName, string spriteKey)
        {
            var item = ItemDirectory.CreateEmptyItem(seedId);
            if (item == null) return null;

            // ★ 忽略传入的 displayName，改用当前语言的数组值
            for (int i = 0; i < SeedIds.Length; i++)
                if (SeedIds[i] == seedId) { displayName = SeedNames[i]; break; }

            int price = SeedPriceCommon;
            if (seedId == "lychee_seed" || seedId == JasmineSeedId || seedId == GreenGrapeSeedId) price = SeedPriceRemote;
            else if (seedId == "osmanthus_seed" || seedId == "bayberry_seed") price = SeedPriceLonging;
            else if (seedId == VoidPlumSeedId || seedId == StardustBerrySeedId) price = SeedPriceAfterglow;

            item.SetName(displayName);
            item.SetValue(price);
            item.SetSprite("", spriteKey);
            item.shape = new GridShapeBuilder(1, 2).SetDataFill(1, 2, 1).Build();
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            types.Add("SEED"); types.Add("FOOD"); types.Add("MATERIAL");
            item.itemTypes = types;
            EnsureSeedFoodTag(item);
            try { item.AddClientItemBuyingFeature(); } catch { }
            try
            {
                for (int i = 0; i < SeedIds.Length; i++)
                    if (SeedIds[i] == seedId) { item.shortDescription = SeedDescriptions[i]; item.longDescription = SeedDescriptions[i]; break; }
            }
            catch { }
            return item;
        }

        internal static void EnsureSeedFoodTag(GameItem item)
        {
            // Native estimates match market events against state tags; trade
            // features match itemTypes. Keep both food classifications aligned.
            if (item != null && IsOurSeed(item.identifier) && !item.IsTag("FOOD"))
                item.EnableTag("FOOD", false);
        }

        public static GameItem CreateFruitItem(string fruitId, string displayName, string spriteKey)
        {
            var item = ItemDirectory.CreateEmptyItem(fruitId);
            if (item == null) return null;

            // ★ 忽略传入的 displayName，改用当前语言的数组值
            for (int i = 0; i < FruitIds.Length; i++)
                if (FruitIds[i] == fruitId) { displayName = FruitNames[i]; break; }

            int price = FruitPriceCommon;
            if (fruitId == MidautumnMoonOsmanthusId) price = 0;
            else if (fruitId == "lychee_fruit" || fruitId == JasmineFruitId || fruitId == GreenGrapeFruitId) price = FruitPriceRemote;
            else if (fruitId == "osmanthus_fruit" || fruitId == "bayberry_fruit") price = FruitPriceLonging;
            else if (fruitId == VoidPlumFruitId || fruitId == StardustBerryFruitId) price = FruitPriceAfterglow;

            item.SetName(displayName);
            item.SetValue(price);
            item.SetSprite("", spriteKey);
            item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            types.Add("FOOD");
            item.itemTypes = types;
            item.EnableTag("FOOD", false);
            try { WineHelper.InitBerry(item); } catch { }
            try { WineHelper.SetBerryCondition(item, FruitMaxCondition, 0); } catch { }
            try { item.AddClientItemBuyingFeature(); } catch { }
            try
            {
                for (int i = 0; i < FruitIds.Length; i++)
                    if (FruitIds[i] == fruitId) { item.shortDescription = FruitDescriptions[i]; item.longDescription = FruitDescriptions[i]; break; }
            }
            catch { }
            return item;
        }

        public static GameItem CreateCertItem(string certId, string certName, string spriteKey, string description, int price)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(certId);
                if (item == null) return null;

                // ★ 忽略传入的 certName，改用当前语言的数组值
                for (int i = 0; i < CertIds.Length; i++)
                    if (CertIds[i] == certId) { certName = CertNames[i]; break; }

                // ★ 忽略传入的 description，改用当前语言的值
                for (int i = 0; i < CertIds.Length; i++)
                    if (CertIds[i] == certId) { description = CertDescriptions[i]; break; }


                item.SetName(certName);
                item.SetValue(price);
                item.SetSprite("", spriteKey);
                item.shape = new GridShapeBuilder(3, 2).SetDataFill(3, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("DOCUMENT");
                item.itemTypes = types;
                try { item.shortDescription = description; } catch { }
                try { item.longDescription = description; } catch { }
                return item;
            }
            catch { return null; }
        }

        public static GameItem CreateBrewerySignItem()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(BrewerySignId);
                if (item == null) return null;
                item.SetName(BrewerySignName);
                item.SetValue(BrewerySignPrice);
                item.SetSprite("", BrewerySignSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();

                // The sign is a showcase trigger, not a customer shopping category.
                // An empty type list also prevents the untranslated internal type code
                // from appearing in its tooltip.
                item.itemTypes = new Il2CppSystem.Collections.Generic.List<string>();

                try { item.EnableTag("BE_BREWERY_SIGN", false); } catch { }


                string d = Lang.T(
    "一盏写着「酿酒」的霓虹灯牌。挂在展示柜里，会吸引一位专门卖种子的神秘客人上门。",
    "A neon sign reading \"Brewery.\" Displayed in your showcase, it will attract a mysterious seed-trader to your door.");
                try { item.shortDescription = d; } catch { }
                try { item.longDescription = d; } catch { }
                return item;
            }
            catch { return null; }
        }

        // ★ 泥泥的名片
        public static GameItem CreateNiniCardItem()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(NiniCardId);
                if (item == null) return null;
                item.SetName(NiniCardName);
                item.SetValue(NiniCardPrice);
                item.SetSprite("", NiniCardSpriteKey);
                item.shape = new GridShapeBuilder(2, 1).SetDataFill(2, 1, 1).Build();

                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("DOCUMENT");
                item.itemTypes = types;

                try { item.shortDescription = NiniCardDescription; } catch { }
                try { item.longDescription = NiniCardDescription; } catch { }
                return item;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateNiniCardItem 异常: " + e.Message);
                return null;
            }
        }

        // ★ 判断是否是泥泥名片
        public static bool IsNiniCard(GameItem item)
        {
            if (item == null) return false;
            try { return (item.identifier ?? "") == NiniCardId; } catch { return false; }
        }
        public static GameItem CreateMyPerfume()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(MyPerfumeId);
                if (item == null) return null;
                item.SetName(MyPerfumeName);
                item.SetSprite("", MyPerfumeSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();

                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;

                // 顶级违禁品
                try { ContrabandHelper.InitContrabandItem(item, 4); } catch { }
                item.SetValue(MyPerfumePrice);

                SetPerfumeUses(item, MyPerfumeMaxUses);
                UpdatePerfumeDescription(item);

                // 使用次数
                /*try
                {
                    UseCountHelper.InitUseCountItem(
                        item,
                        MyPerfumeMaxUses,
                        true,
                        true,
                        true,
                        MyPerfumePrice,
                        MyPerfumeValuePerUse);
                    MelonLogger.Msg("[BE] InitUseCountItem 成功");
                }
                catch (Exception e) { MelonLogger.Warning("[BE] InitUseCountItem 失败: " + e.Message); }*/

                // 右键使用
                // 右键使用

                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateMyPerfume 异常: " + e.Message); return null; }
        }
        public static GameItem CreateFreedomPerfume()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(FreedomPerfumeId);
                if (item == null) return null;
                item.SetName(Lang.T("逍遥法外", "Beyond the Law"));
                item.SetSprite("", FreedomPerfumeSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;
                try { ContrabandHelper.InitContrabandItem(item, 4); } catch { }
                item.SetValue(FreedomPerfumePrice);
                SetPerfumeUses(item, MyPerfumeMaxUses);
                UpdatePerfumeDescription(item);
                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateFreedomPerfume 异常: " + e.Message); return null; }
        }
        // ★ 香水辅助方法（加在 CreateMyPerfume 后面）
        public static bool IsMyPerfume(GameItem item)
        {
            if (item == null) return false;
            try { return (item.identifier ?? "") == MyPerfumeId; } catch { return false; }
        }

        public static bool IsPerfume(GameItem item)
        {
            if (item == null) return false;
            try { return item.identifier == MyPerfumeId || item.identifier == FreedomPerfumeId; }
            catch { return false; }
        }

        private static int PerfumePrice(GameItem item) => item?.identifier == FreedomPerfumeId
            ? FreedomPerfumePrice : MyPerfumePrice;

        private static int PerfumeCrimeReduction(GameItem item) => item?.identifier == FreedomPerfumeId
            ? FreedomPerfumeCrimeReduction : MyPerfumeCrimeReduction;



        public static int GetPerfumeUses(GameItem item)
        {
            if (item == null) return 0;
            try
            {
                for (int i = 0; i <= MyPerfumeMaxUses; i++)
                    if (item.IsTag(MyPerfumeUsesTagPrefix + i)) return i;
            }
            catch { }
            return 0;
        }

        public static void SetPerfumeUses(GameItem item, int uses)
        {
            if (item == null) return;
            try
            {
                for (int i = 0; i <= MyPerfumeMaxUses; i++)
                    item.DisableTag(MyPerfumeUsesTagPrefix + i, false);
                item.EnableTag(MyPerfumeUsesTagPrefix + uses, false);
            }
            catch { }
        }

        private static int _lastPerfumeUseId = -1;
        private static DateTime _lastPerfumeUseTime = DateTime.MinValue;
        private const double PerfumeUseCooldownSeconds = 0.5;

        public static void UseMyPerfume(GameItem item)
        {
            try
            {
                if (!IsPerfume(item)) return;

                // ★ 冷却：同一物品 0.5 秒内只能触发一次
                int uid = -1;
                try { uid = item.uniqueId; } catch { }
                DateTime now = DateTime.Now;
                if (uid == _lastPerfumeUseId && (now - _lastPerfumeUseTime).TotalSeconds < PerfumeUseCooldownSeconds)
                {
                    MelonLogger.Msg("[BE-DIAG] 冷却中，忽略重复触发");
                    return;
                }
                _lastPerfumeUseId = uid;
                _lastPerfumeUseTime = now;

                int uses = GetPerfumeUses(item);
                if (uses <= 0) return;

                ReduceCrime(PerfumeCrimeReduction(item));

                int newUses = uses - 1;
                SetPerfumeUses(item, newUses);
                UpdatePerfumeDescription(item);

                long newValue = (long)PerfumePrice(item) * newUses / MyPerfumeMaxUses;
                try { item.SetValue(newValue); } catch { }

                MelonLogger.Msg("[BE-DIAG] 香水使用：剩余 " + newUses + " 次");

                if (newUses <= 0)
                    try { item.Destroy(); } catch { }
            }
            catch { }
        }

        public static void UpdatePerfumeDescription(GameItem item)
        {
            if (item == null) return;
            try
            {
                int uses = GetPerfumeUses(item);
                var isFreedom = item.identifier == FreedomPerfumeId;
                string desc = isFreedom
                    ? Lang.T(
                        "每次使用消除 2000 点罪证。\n剩余使用次数：" + uses + " / " + MyPerfumeMaxUses,
                        "Each use removes 2,000 points of evidence.\nUses remaining: " + uses + " / " + MyPerfumeMaxUses)
                    : Lang.T(
                        "顶级违禁品。每次使用消除 20000 点罪证。\n剩余使用次数：" + uses + " / " + MyPerfumeMaxUses,
                        "Top-tier contraband. Each use removes 20,000 points of evidence.\nUses remaining: " + uses + " / " + MyPerfumeMaxUses);
                try { item.shortDescription = desc; } catch { }
                try { item.longDescription = desc; } catch { }
            }
            catch { }
        }

        public static void RefreshOwnedPerfumePrices()
        {
            try
            {
                var items = PlayerStore.Instance?.FindAllItem(true);
                if (items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (!IsPerfume(item)) continue;
                    int uses = GetPerfumeUses(item);
                    item.SetValue((long)PerfumePrice(item) * uses / MyPerfumeMaxUses);
                    UpdatePerfumeDescription(item);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 香水旧物品定价更新失败: " + e.Message); }
        }

        // ★ 新增：降低罪证
        public static void ReduceCrime(int amount)
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null || store.secData == null) return;
                var sec = store.secData;

                // ★ 诊断 1：打印三个并行 List 的全部内容
                var types = sec.crimeTypes;
                var cur = sec.crimeAmount;
                var tot = sec.crimeAmountTotal;
                MelonLogger.Msg("[BE-DIAG] === SecData 罪证明细 ===");
                MelonLogger.Msg("[BE-DIAG] crimeTypes.count=" + (types == null ? -1 : types.Count)
                    + "  crimeAmount.count=" + (cur == null ? -1 : cur.Count)
                    + "  crimeAmountTotal.count=" + (tot == null ? -1 : tot.Count));

                if (types != null && cur != null)
                {
                    int n = types.Count;
                    for (int i = 0; i < n; i++)
                    {
                        string t = (i < types.Count) ? (types[i] ?? "null") : "-";
                        int a = (i < cur.Count) ? cur[i] : -1;
                        int b = (tot != null && i < tot.Count) ? tot[i] : -1;
                        MelonLogger.Msg("[BE-DIAG]   [" + i + "] type='" + t + "'  amount=" + a + "  total=" + b);
                    }
                }

                // ★ 诊断 2：把 GetTotalCrimeValue 和 GetContrabandTotalCrimeValue 都打出来
                try { MelonLogger.Msg("[BE-DIAG] GetTotalCrimeValue() = " + sec.GetTotalCrimeValue()); } catch { }
                try { MelonLogger.Msg("[BE-DIAG] GetContrabandTotalCrimeValue() = " + sec.GetContrabandTotalCrimeValue()); } catch { }

                // ★ 扣减：crimeAmount 和 crimeAmountTotal 都扣
                int remaining = amount;
                if (cur != null)
                {
                    for (int i = 0; i < cur.Count && remaining > 0; i++)
                    {
                        int c = cur[i];
                        if (c <= 0) continue;
                        int deduct = c < remaining ? c : remaining;
                        cur[i] = c - deduct;
                        remaining -= deduct;
                    }
                }
                if (tot != null)
                {
                    int remaining2 = amount;
                    for (int i = 0; i < tot.Count && remaining2 > 0; i++)
                    {
                        int c = tot[i];
                        if (c <= 0) continue;
                        int deduct = c < remaining2 ? c : remaining2;
                        tot[i] = c - deduct;
                        remaining2 -= deduct;
                    }
                }

                // ★ 诊断 3：扣完后再打一遍
                MelonLogger.Msg("[BE-DIAG] === 扣减后 ===");
                if (types != null && cur != null)
                {
                    int n = types.Count;
                    for (int i = 0; i < n; i++)
                    {
                        string t = (i < types.Count) ? (types[i] ?? "null") : "-";
                        int a = (i < cur.Count) ? cur[i] : -1;
                        int b = (tot != null && i < tot.Count) ? tot[i] : -1;
                        MelonLogger.Msg("[BE-DIAG]   [" + i + "] type='" + t + "'  amount=" + a + "  total=" + b);
                    }
                }
                try { MelonLogger.Msg("[BE-DIAG] 扣减后 GetTotalCrimeValue() = " + sec.GetTotalCrimeValue()); } catch { }
                try { MelonLogger.Msg("[BE-DIAG] 扣减后 GetContrabandTotalCrimeValue() = " + sec.GetContrabandTotalCrimeValue()); } catch { }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-DIAG] ReduceCrime 异常: " + e.Message); }
        }

        // ★ 酿酒手册：以原版 storage_bay 为模板创建，保留双击打开机制
        public static GameItem CreateBrewingManualAsContainer()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(StorageBayId, false); } catch (Exception e) { MelonLogger.Warning("[BE-DIAG] DirectoryMaster.Item(storage_bay) 异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE-DIAG] 拿不到 storage_bay 模板");
                    return null;
                }

                MelonLogger.Msg("[BE-DIAG] storage_bay 拿到:"
                    + " id=" + (gi.identifier ?? "null")
                    + " name=" + (gi.name ?? "null")
                    + " contentWindow=" + (gi.contentWindow != null)
                    + " child=" + (gi.child != null)
                    + " activateDefault=" + gi.activateDefault
                    + " onActivate=" + (gi.onActivateSlotItemFunc != null)
                    + " childCount=" + (gi.FindAllChildItems()?.Count ?? -1));

                // 改外观
                try { gi.SetName(BrewingManualName); } catch { }
                try { gi.SetValue(BrewingManualPrice); } catch { }
                try { gi.SetSprite("", BrewingManualSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", BrewingManualSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(2, 3).SetDataFill(2, 3, 1).Build(); } catch { }
                try { gi.shortDescription = BrewingManualDescription; } catch { }
                try { gi.longDescription = BrewingManualDescription; } catch { }

                // 加 tag 标记（identifier 保持 storage_bay，避免破坏原版机制）
                try { gi.EnableTag(BrewingManualTag, false); } catch { }
                // ★ 覆盖原版 storage_bay 的切换列表：只允许切换成手册自己
                try
                {
                    var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(2);
                    arr[0] = BrewingManualSpriteKey;
                    arr[1] = BrewingManualSpriteKey;

                    ToggleAppearanceHelper.MakeSpriteCyclable(gi, "", arr);
                    MelonLogger.Msg("[BE] 手册切换列表已覆盖");
                }
                catch (Exception e) { MelonLogger.Warning("[BE] 覆盖手册切换列表失败: " + e.Message); }

                // 尝试覆盖接收规则为"只接受配方"
                try
                {
                    Il2Cpp.GameInventory inv = null;
                    try
                    {
                        if (gi.child != null)
                        {
                            var ct = gi.child.GetType();
                            var prop = ct.GetProperty("inventory",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (prop != null) inv = prop.GetValue(gi.child) as Il2Cpp.GameInventory;
                            if (inv == null)
                            {
                                var f = ct.GetField("inventory",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (f != null) inv = f.GetValue(gi.child) as Il2Cpp.GameInventory;
                            }
                        }
                    }
                    catch { }

                    if (inv != null)
                    {
                        var allowedTags = new Il2CppSystem.Collections.Generic.List<string>();
                        var allowedIds = new Il2CppSystem.Collections.Generic.List<string>();
                        for (int i = 0; i < RecipeIds.Length; i++) allowedIds.Add(RecipeIds[i]);
                        for (int i = 0; i < XIAOWOBrewingBarrel.MachineGuides.Ids.Length; i++)
                            allowedIds.Add(XIAOWOBrewingBarrel.MachineGuides.Ids[i]);

                        ContainerHelper.InitContainerItem(inv, gi, allowedTags, allowedIds);
                        MelonLogger.Msg("[BE-DIAG] 覆盖接收规则成功（只接受配方）");
                    }
                    else
                    {
                        MelonLogger.Msg("[BE-DIAG] 找不到 inventory，无法覆盖接收规则（原版规则仍生效）");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-DIAG] 覆盖接收规则异常: " + e.Message); }

                return gi;
            }

            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateBrewingManualAsContainer 异常: " + e.Message);
                return null;
            }

        }

        // ★ 新加的方法，插在这里
        // 辅助：找"真"托管字段，排除 Interop 生成的辅助字段
        private static FieldInfo FindManagedField(Type t, string keyword, Type fieldType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            MelonLogger.Msg("[BE-DIAG] === 扫描 " + t.FullName + "，共 " + t.GetFields(flags).Length + " 字段 ===");

            FieldInfo nameMatch = null;
            foreach (var f in t.GetFields(flags))
            {
                // 打印所有含 Shape 的字段（诊断用）
                if (f.Name.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MelonLogger.Msg("[BE-DIAG]  Shape 字段: " + f.Name + " / " + f.FieldType.FullName);
                }

                if (f.Name.StartsWith("NativeFieldInfoPtr_")) continue;
                if (f.Name.StartsWith("NativeMethodInfoPtr_")) continue;
                if (!f.Name.Contains(keyword)) continue;

                MelonLogger.Msg("[BE-DIAG]  keyword 命中: " + f.Name + " / " + f.FieldType.FullName);

                if (nameMatch == null) nameMatch = f;
                // 不再比较类型，直接返回
            }

            if (nameMatch != null) MelonLogger.Msg("[BE-DIAG] 返回字段: " + nameMatch.Name);
            else MelonLogger.Warning("[BE-DIAG] 没找到含关键词 '" + keyword + "' 的字段");

            return nameMatch;
        }

        public static GameItem CreateCompactSmugglerBay()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(SmugglerBayModId, false); } catch (Exception e) { MelonLogger.Warning("[BE-DIAG] Item(smuggler_bay_mod) 异常: " + e.Message); }
                if (gi == null) { MelonLogger.Warning("[BE-DIAG] 拿不到 smuggler_bay_mod 模板"); return null; }

                try { gi.SetName(CompactSmugglerBayName); } catch { }
                try { gi.SetValue(CompactSmugglerBayPrice); } catch { }
                try { gi.SetSprite("", CompactSmugglerBaySpriteKey); } catch { }
                try { gi.SetSpriteQuick("", CompactSmugglerBaySpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build(); } catch { }
                try { gi.shortDescription = CompactSmugglerBayDescription; } catch { }
                try { gi.longDescription = CompactSmugglerBayDescription; } catch { }

                try { gi.EnableTag(CompactSmugglerBayTag, false); } catch { }

                // ★ 扩容：用游戏自己的 SetShape(width, height) 方法
                try
                {
                    int targetW = 12;
                    int targetH = 12;

                    if (gi.child != null && gi.child.child != null)
                    {
                        var inner = gi.child.child;
                        var gridInv = inner.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            MelonLogger.Msg("[BE-DIAG] 调用 SetShape(" + targetW + ", " + targetH + ")");
                            try { gridInv.SetShape(targetW, targetH); }
                            catch (Exception ex) { MelonLogger.Warning("[BE-BAG] SetShape 异常: " + ex.Message); }

                            try
                            {
                                var s = gridInv.inventoryShape;
                                if (s != null)
                                    MelonLogger.Msg("[BE-BAG] 调用后 inventoryShape=" + s.width + "x" + s.height);
                            }
                            catch { }

                            try { gridInv.Validate(); } catch { /* UI 未就绪时忽略 */ }
                        }
                        else MelonLogger.Warning("[BE-DIAG] inner 不是 GameGridInventory");
                    }
                    else MelonLogger.Warning("[BE-DIAG] gi.child 或 gi.child.child == null");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-DIAG] 扩容异常: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateCompactSmugglerBay 异常: " + e.Message);
                return null;
            }
        }

        // ★ 清酿澄水箱：以 water_pitcher 为模板，容量 30L
        public static GameItem CreateClearWaterTank()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaWaterPitcherId, false); }
                catch (Exception e) { MelonLogger.Warning("[BE-DIAG] 拿 water_pitcher 模板异常: " + e.Message); }
                if (gi == null) { MelonLogger.Warning("[BE-DIAG] 拿不到 water_pitcher 模板"); return null; }

                // 改外观
                try { gi.SetName(ClearWaterTankName); } catch { }
                try { gi.SetValue(ClearWaterTankPrice); } catch { }
                try
                {
                    gi.SetSprite("", ClearWaterTankSpriteKey);
                    MelonLogger.Msg("[BE-DIAG] SetSprite 调用成功");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-DIAG] SetSprite 异常: " + e.Message); }

                try { gi.SetSpriteQuick("", ClearWaterTankSpriteKey); } catch { }

                try { gi.SetSpriteQuick("", ClearWaterTankSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(2, 3).SetDataFill(2, 3, 1).Build(); } catch { }
                try { gi.shortDescription = ClearWaterTankDescription; } catch { }
                try { gi.longDescription = ClearWaterTankDescription; } catch { }

                // tag 标记
                try { gi.EnableTag(ClearWaterTankTag, false); } catch { }

                // ★ 覆盖液体容器容量：30L = 30000 ml
                try
                {
                    WaterHelper.InitLiquidContainerItem(
                        gi,
                        ClearWaterTankCapacityMl,   // capacityInMl
                        ClearWaterTankPrice,        // basePrice
                        true,                        // isFilterContainer（保留滤芯功能）
                        false,                       // isGraduated
                        false,                       // isDropper
                        true,                        // isTransparentContainer
                        3);                          // sheetPage
                    EnsureClearWaterTankCapacity(gi);
                    MelonLogger.Msg("[BE] 清酿澄水箱初始化完成，容量 " + ClearWaterTankCapacityMl + " ml");
                }
                catch (Exception e) { MelonLogger.Warning("[BE] InitLiquidContainerItem 失败: " + e.Message); }
                // ★ 清空水质角标回调
                try { ClearSpriteModifierCallbacks(gi); } catch { }
                // InitLiquidContainerItem may restore the vanilla pitcher's footprint.
                try { gi.shape = new GridShapeBuilder(2, 3).SetDataFill(2, 3, 1).Build(); } catch { }
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateClearWaterTank 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通用水容器创建
        private static GameItem CreateWaterContainerGeneric(
            string id, string name, string spriteKey, string desc, string tag,
            int price, int capacityMl)
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaWaterPitcherId, false); }
                catch (Exception e) { MelonLogger.Warning("[BE-CUP] 拿 water_pitcher 模板异常: " + e.Message); }
                if (gi == null) { MelonLogger.Warning("[BE-CUP] 拿不到 water_pitcher 模板"); return null; }

                try { gi.SetName(name); } catch { }
                try { gi.SetValue(price); } catch { }
                try { gi.SetSprite("", spriteKey); } catch { }
                try { gi.SetSpriteQuick("", spriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(2, 3).SetDataFill(2, 3, 1).Build(); } catch { }
                try { gi.shortDescription = desc; } catch { }
                try { gi.longDescription = desc; } catch { }

                try { gi.EnableTag(tag, false); } catch { }

                try
                {
                    WaterHelper.InitLiquidContainerItem(
                        gi,
                        capacityMl,
                        price,
                        true,    // isFilterContainer
                        false,   // isGraduated
                        false,   // isDropper
                        true,    // isTransparentContainer
                        3);      // sheetPage
                    MelonLogger.Msg("[BE-CUP] " + name + " 初始化完成，容量 " + capacityMl + " ml");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-CUP] InitLiquidContainerItem 失败: " + e.Message); }

                try { ClearSpriteModifierCallbacks(gi); } catch { }
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-CUP] CreateWaterContainerGeneric " + id + " 异常: " + e.Message);
                return null;
            }
        }

        public static GameItem CreateXuxuWarmCup()
        {
            return CreateWaterContainerGeneric(
                XuxuWarmCupId, XuxuWarmCupName, XuxuWarmCupSpriteKey, XuxuWarmCupDescription,
                XuxuWarmCupTag, XuxuWarmCupPrice, XuxuWarmCupCapacityMl);
        }

        public static GameItem CreateXuxuSweetCup()
        {
            return CreateWaterContainerGeneric(
                XuxuSweetCupId, XuxuSweetCupName, XuxuSweetCupSpriteKey, XuxuSweetCupDescription,
                XuxuSweetCupTag, XuxuSweetCupPrice, XuxuSweetCupCapacityMl);
        }

        public static GameItem CreateLove47Cup()
        {
            return CreateWaterContainerGeneric(
                Love47CupId, Love47CupName, Love47CupSpriteKey, Love47CupDescription,
                Love47CupTag, Love47CupPrice, Love47CupCapacityMl);
        }

        // ★ 判断是否我们的水容器（清酿澄水箱 + 三个杯子）
        public static bool IsOurWaterContainer(GameItem item)
        {
            if (item == null) return false;
            try
            {
                if (item.IsTag(ClearWaterTankTag)) return true;
                if (item.IsTag(XuxuWarmCupTag)) return true;
                if (item.IsTag(XuxuSweetCupTag)) return true;
                if (item.IsTag(Love47CupTag)) return true;
            }
            catch { }
            return false;
        }

        // ★ 根据物品返回对应 sprite key
        public static string GetOurWaterContainerSpriteKey(GameItem item)
        {
            if (item == null) return null;
            try
            {
                if (item.IsTag(ClearWaterTankTag)) return ClearWaterTankSpriteKey;
                if (item.IsTag(XuxuWarmCupTag)) return XuxuWarmCupSpriteKey;
                if (item.IsTag(XuxuSweetCupTag)) return XuxuSweetCupSpriteKey;
                if (item.IsTag(Love47CupTag)) return Love47CupSpriteKey;
            }
            catch { }
            return null;
        }

        // ★ 青筠滤芯
        public static GameItem CreateQingyunFilter()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaAdvFilterId, false); }
                catch (Exception e) { MelonLogger.Warning("[BE-DIAG] 拿 water_filter_adv 模板异常: " + e.Message); }
                if (gi == null) { MelonLogger.Warning("[BE-DIAG] 拿不到 water_filter_adv 模板"); return null; }

                try { gi.SetName(QingyunFilterName); } catch { }
                try { gi.SetValue(QingyunFilterPrice); } catch { }
                try { gi.SetSprite("", QingyunFilterSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", QingyunFilterSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(1, 3).SetDataFill(1, 3, 1).Build(); } catch { }
                try { gi.shortDescription = QingyunFilterDescription; } catch { }
                try { gi.longDescription = QingyunFilterDescription; } catch { }

                try { gi.EnableTag(QingyunFilterTag, false); } catch { }
                // ★ 实验：把 WATER_FILTER tag 的 valueInt 设为 600
                try
                {
                    var tagState = gi.GetTagReadonly("WATER_FILTER");
                    if (tagState != null)
                    {
                        MelonLogger.Msg("[BE-TAG] 赋值前 valueInt = " + tagState.valueInt);
                        tagState.valueInt = QingyunFilterMaxDurability;
                        MelonLogger.Msg("[BE-TAG] 赋值后 valueInt = " + tagState.valueInt);
                    }
                    else
                    {
                        MelonLogger.Msg("[BE-TAG] WATER_FILTER tag 为 null");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-TAG] 设置 valueInt 异常: " + e.Message); }



                // ★ 把耐久上限从 400 改成 600
                try
                {
                    int cur = DurabilityHelper.GetRealDurability(gi);
                    int max = DurabilityHelper.GetMaxDurability(gi);
                    int def = DurabilityHelper.GetDefDurability(gi);
                    MelonLogger.Msg("[BE-QY] 青筠耐久：当前=" + cur + " 上限=" + max + " 默认=" + def);
                    if (max < QingyunFilterMaxDurability)
                    {
                        DurabilityHelper.SetDurability(gi, QingyunFilterMaxDurability,
                            QingyunFilterMaxDurability, QingyunFilterMaxDurability);
                        int newMax = DurabilityHelper.GetMaxDurability(gi);
                        int newCur = DurabilityHelper.GetRealDurability(gi);
                        MelonLogger.Msg("[BE-QY] 改完后：当前=" + newCur + " 上限=" + newMax);
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-QY] 改上限异常: " + e.Message); }
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateQingyunFilter 异常: " + e.Message);
                return null;
            }
        }

        // ★ 凝露滤芯：盲盒专属，耐久 800，每晚回 80
        public static GameItem CreateNingluFilter()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaAdvFilterId, false); } catch { }
                if (gi == null) return null;

                try { gi.SetName(NingluFilterName); } catch { }
                try { gi.SetValue(NingluFilterPrice); } catch { }
                try { gi.SetSprite("", NingluFilterSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", NingluFilterSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(1, 3).SetDataFill(1, 3, 1).Build(); } catch { }
                try { gi.shortDescription = NingluFilterDescription; } catch { }
                try { gi.longDescription = NingluFilterDescription; } catch { }

                try { gi.EnableTag(NingluFilterTag, false); } catch { }

                // 耐久上限 800，满耐久
                try
                {
                    DurabilityHelper.SetDurability(gi, NingluFilterMaxDurability,
                        NingluFilterMaxDurability, NingluFilterMaxDurability);
                    int max = DurabilityHelper.GetMaxDurability(gi);
                    MelonLogger.Msg("[BE-NL] 凝露耐久设定：" + max + "/" + NingluFilterMaxDurability);
                }
                catch (Exception e) { MelonLogger.Warning("[BE-NL] 耐久设定失败: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateNingluFilter 异常: " + e.Message);
                return null;
            }
        }

        // ★ 樱绯滤芯：盲盒专属，耐久 1000，每晚回 100
        public static GameItem CreateYingfeiFilter()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaAdvFilterId, false); } catch { }
                if (gi == null) return null;

                try { gi.SetName(YingfeiFilterName); } catch { }
                try { gi.SetValue(YingfeiFilterPrice); } catch { }
                try { gi.SetSprite("", YingfeiFilterSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", YingfeiFilterSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(1, 3).SetDataFill(1, 3, 1).Build(); } catch { }
                try { gi.shortDescription = YingfeiFilterDescription; } catch { }
                try { gi.longDescription = YingfeiFilterDescription; } catch { }

                try { gi.EnableTag(YingfeiFilterTag, false); } catch { }

                // 耐久上限 1000，满耐久
                try
                {
                    DurabilityHelper.SetDurability(gi, YingfeiFilterMaxDurability,
                        YingfeiFilterMaxDurability, YingfeiFilterMaxDurability);
                    int max = DurabilityHelper.GetMaxDurability(gi);
                    MelonLogger.Msg("[BE-YF] 樱绯耐久设定：" + max + "/" + YingfeiFilterMaxDurability);
                }
                catch (Exception e) { MelonLogger.Warning("[BE-YF] 耐久设定失败: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateYingfeiFilter 异常: " + e.Message);
                return null;
            }
        }

        // ★ 霓虹残梦
        public static GameItem CreateNeonDreamWine()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(NeonDreamWineId);
                if (item == null) return null;

                item.SetName(NeonDreamWineName);
                item.SetValue(NeonDreamWinePrice);
                item.SetSprite("", NeonDreamWineSpriteKey);
                item.shape = new GridShapeBuilder(1, 4).SetDataFill(1, 4, 1).Build();

                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                types.Add("ALCOHOL");
                types.Add("SUBSTANCE");
                item.itemTypes = types;

                // 让它能陈化增值（跟其他酒一样）
                try { AgableHelper.InitAgable(item, WineAgeValueModPerDay); } catch { }

                try { item.EnableTag(NeonDreamWineTag, false); } catch { }
                try { item.shortDescription = NeonDreamWineDescription; } catch { }
                try { item.longDescription = NeonDreamWineDescription; } catch { }
                return item;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateNeonDreamWine 异常: " + e.Message);
                return null;
            }
        }

        public static bool IsNeonDreamWine(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(NeonDreamWineTag); } catch { return false; }
        }

        // ============ 收藏酒系列 ============

        private static GameItem CreateCollectorWine(string id, string name, string spriteKey, string desc,
            int price = CollectorWinePrice)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(id);
                if (item == null) return null;
                item.SetName(name);
                item.SetValue(price);
                item.SetSprite("", spriteKey);
                item.shape = new GridShapeBuilder(1, 4).SetDataFill(1, 4, 1).Build();

                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                types.Add("ALCOHOL");
                types.Add("SUBSTANCE");
                item.itemTypes = types;

                try { AgableHelper.InitAgable(item, WineAgeValueModPerDay); } catch { }
                try { item.shortDescription = desc; } catch { }
                try { item.longDescription = desc; } catch { }

                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateCollectorWine " + id + " 异常: " + e.Message); return null; }
        }

        public static GameItem CreateShuangqingWine() { return CreateCollectorWine(ShuangqingWineId, ShuangqingWineName, ShuangqingWineSpriteKey, ShuangqingWineDescription); }
        public static GameItem CreateNanfengWine() { return CreateCollectorWine(NanfengWineId, NanfengWineName, NanfengWineSpriteKey, NanfengWineDescription); }
        public static GameItem CreateFeimeiWine() { return CreateCollectorWine(FeimeiWineId, FeimeiWineName, FeimeiWineSpriteKey, FeimeiWineDescription); }
        public static GameItem CreateNeonIllusionWine() { return CreateCollectorWine(NeonIllusionWineId, NeonIllusionWineName, NeonIllusionWineSpriteKey, NeonIllusionWineDescription); }
        public static GameItem CreateIdealEndWine() { return CreateCollectorWine(IdealEndWineId, IdealEndWineName, IdealEndWineSpriteKey, IdealEndWineDescription); }
        public static GameItem CreateLuofuWine() { return CreateCollectorWine(LuofuWineId, LuofuWineName, LuofuWineSpriteKey, LuofuWineDescription); }
        public static GameItem CreateMagicPigWine() { return CreateCollectorWine(MagicPigWineId, MagicPigWineName, MagicPigWineSpriteKey, MagicPigWineDescription); }
        public static GameItem CreateSeaBlueWine() { return CreateCollectorWine(SeaBlueWineId, SeaBlueWineName, SeaBlueWineSpriteKey, SeaBlueWineDescription); }
        public static GameItem CreateNianHeshiWine() { return CreateCollectorWine(NianHeshiWineId, NianHeshiWineName, NianHeshiWineSpriteKey, NianHeshiWineDescription, NianHeshiWinePrice); }
        public static GameItem CreateRainbowRecipeWine() { return CreateCollectorWine(RainbowRecipeWineId, RainbowRecipeWineName, RainbowRecipeWineSpriteKey, RainbowRecipeWineDescription); }
        // ============ 中秋节系列物品 ============

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

        public static GameItem CreateMidautumnMoonFan() { return CreateMidautumnCommon(MidautumnMoonFanId, MidautumnMoonFanName, MidautumnMoonFanSpriteKey, MidautumnMoonFanDescription, 2, 3); }
        public static GameItem CreateMidautumnMoonLamp() { return CreateMidautumnCommon(MidautumnMoonLampId, MidautumnMoonLampName, MidautumnMoonLampSpriteKey, MidautumnMoonLampDescription, 2, 2); }
        public static GameItem CreateMidautumnJadeRabbit() { return CreateMidautumnCommon(MidautumnJadeRabbitId, MidautumnJadeRabbitName, MidautumnJadeRabbitSpriteKey, MidautumnJadeRabbitDescription, 2, 2); }
        public static GameItem CreateMidautumnLantern() { return CreateMidautumnCommon(MidautumnLanternId, MidautumnLanternName, MidautumnLanternSpriteKey, MidautumnLanternDescription, 2, 2); }

        public static GameItem CreateFigurine(int index)
        {
            if (index < 0 || index >= FigurineIds.Length) return null;
            return CreateMidautumnCommon(FigurineIds[index],
                Lang.T(FigurinePngNames[index], FigurineEnglishNames[index]), FigurineSpriteKeys[index],
                Lang.T(FigurineChineseDescriptions[index], FigurineEnglishDescriptions[index]), 2, index == 3 ? 2 : 3);
        }

        // Figurines only enter player inventories as owned blind-box rewards. Repair
        // metadata on the same saved object, including unknown placeholders lacking ownership.
        // Keep its UUID, inventory position and saved footprint to avoid overlapping neighbors.
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

        // 月团礼盒（不加吸引力）
        public static GameItem CreateMidautumnMooncakeBox()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(MidautumnMooncakeBoxId);
                if (item == null) return null;
                item.SetName(MidautumnMooncakeBoxName);
                item.SetValue(0);
                item.SetSprite("", MidautumnMooncakeBoxSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;
                try { item.EnableTag(MidautumnMooncakeBoxTag, false); } catch { }
                try { item.shortDescription = MidautumnMooncakeBoxDescription; } catch { }
                try { item.longDescription = MidautumnMooncakeBoxDescription; } catch { }
                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateMidautumnMooncakeBox 异常: " + e.Message); return null; }
        }

        public static GameItem CreateMidautumnBag()
        {
            try
            {
                // ★ 拿原版挎包做模板
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("satchel", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 satchel 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 satchel 模板");
                    return null;
                }
                // ★ 关键：identifier 保持原版 satchel，读档不依赖延迟注册
                try { gi.identifier = "satchel"; } catch { }
                // ===== 改外观 =====
                try { gi.identifier = "satchel"; } catch { }
                try { gi.SetName(MidautumnBagName); } catch { }
                try { gi.SetValue(0); } catch { }
                try { gi.SetSprite("", MidautumnBagSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", MidautumnBagSpriteKey); } catch { }
                try { gi.SetShape(new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build()); } catch { }
                try { gi.shortDescription = MidautumnBagDescription; } catch { }
                try { gi.longDescription = MidautumnBagDescription; } catch { }

                // ===== ★ 扩容：加在这里 =====
                try
                {
                    int targetW = 20;
                    int targetH = 15;

                    if (gi.child != null && gi.child.child != null)
                    {
                        var inner = gi.child.child;
                        var gridInv = inner.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            MelonLogger.Msg("[BE-BAG] 调用 SetShape(" + targetW + ", " + targetH + ")");
                            try
                            {
                                var result = gridInv.SetShape(targetW, targetH);
                                MelonLogger.Msg("[BE-BAG] SetShape 返回 = " + (result != null ? "非null" : "null"));
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-BAG] SetShape 异常: " + ex.Message); }

                            try
                            {
                                var s = gridInv.inventoryShape;
                                if (s != null)
                                    MelonLogger.Msg("[BE-BAG] 调用后 inventoryShape=" + s.width + "x" + s.height);
                            }
                            catch { }

                            try { gridInv.Validate(); MelonLogger.Msg("[BE-BAG] Validate 成功"); }
                            catch (Exception ex) { MelonLogger.Warning("[BE-BAG] Validate 异常: " + ex.Message); }
                        }
                        else MelonLogger.Warning("[BE-BAG] inner 不是 GameGridInventory");
                    }
                    else MelonLogger.Warning("[BE-BAG] gi.child 或 gi.child.child == null");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-BAG] 扩容异常: " + e.Message); }

                // ===== 加我们的识别 tag =====
                try { gi.EnableTag("BE_MIDAUTUMN_BAG", false); } catch { }

                MelonLogger.Msg("[BE] 玉兔怀袖创建完成（克隆 satchel）");
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateMidautumnBag 异常: " + e.Message);
                return null;
            }
        }

        // ★ 樱雪囊（克隆原版挎包，保留容器功能）
        public static GameItem CreateSakuraBag()
        {
            try
            {
                // ★ 拿原版挎包做模板
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("satchel", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 satchel 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 satchel 模板");
                    return null;
                }

                // ===== 改外观 =====
                try { gi.SetName(SakuraBagName); } catch { }
                try { gi.SetValue(SakuraBagPrice); } catch { }
                try { gi.SetSprite("", SakuraBagSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", SakuraBagSpriteKey); } catch { }
                try { gi.SetShape(new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build()); } catch { }
                try { gi.shortDescription = SakuraBagDescription; } catch { }
                try { gi.longDescription = SakuraBagDescription; } catch { }

                // ===== ★ 扩容：10×15 =====
                try
                {
                    int targetW = 12;
                    int targetH = 10;

                    if (gi.child != null && gi.child.child != null)
                    {
                        var inner = gi.child.child;
                        var gridInv = inner.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            MelonLogger.Msg("[BE-SAKURA] 调用 SetShape(" + targetW + ", " + targetH + ")");
                            try
                            {
                                var result = gridInv.SetShape(targetW, targetH);
                                MelonLogger.Msg("[BE-SAKURA] SetShape 返回 = " + (result != null ? "非null" : "null"));
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-SAKURA] SetShape 异常: " + ex.Message); }

                            try
                            {
                                var s = gridInv.inventoryShape;
                                if (s != null)
                                    MelonLogger.Msg("[BE-SAKURA] 调用后 inventoryShape=" + s.width + "x" + s.height);
                            }
                            catch { }

                            try { gridInv.Validate(); MelonLogger.Msg("[BE-SAKURA] Validate 成功"); }
                            catch (Exception ex) { MelonLogger.Warning("[BE-SAKURA] Validate 异常: " + ex.Message); }
                        }
                        else MelonLogger.Warning("[BE-SAKURA] inner 不是 GameGridInventory");
                    }
                    else MelonLogger.Warning("[BE-SAKURA] gi.child 或 gi.child.child == null");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-SAKURA] 扩容异常: " + e.Message); }

                // ===== 加我们的识别 tag =====
                try { gi.EnableTag("BE_SAKURA_BAG", false); } catch { }

                MelonLogger.Msg("[BE] 樱雪囊创建完成（克隆 satchel）");
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateSakuraBag 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通过 tag 识别樱雪囊
        public static bool IsSakuraBag(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_SAKURA_BAG"); } catch { return false; }
        }

        // ★ 电子幽灵（克隆原版挎包，保留容器功能）
        public static GameItem CreateCyberBag()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("satchel", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 satchel 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 satchel 模板");
                    return null;
                }

                // ===== 改外观 =====
                try { gi.SetName(CyberBagName); } catch { }
                try { gi.SetValue(CyberBagPrice); } catch { }
                try { gi.SetSprite("", CyberBagSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", CyberBagSpriteKey); } catch { }
                // ★ 3 宽 × 2 高（横着的）
                try { gi.SetShape(new GridShapeBuilder(3, 2).SetDataFill(3, 2, 1).Build()); } catch { }
                try { gi.shortDescription = CyberBagDescription; } catch { }
                try { gi.longDescription = CyberBagDescription; } catch { }

                // ===== ★ 扩容：8×8 =====
                try
                {
                    int targetW = 8;
                    int targetH = 8;

                    if (gi.child != null && gi.child.child != null)
                    {
                        var inner = gi.child.child;
                        var gridInv = inner.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            MelonLogger.Msg("[BE-CYBER] 调用 SetShape(" + targetW + ", " + targetH + ")");
                            try
                            {
                                var result = gridInv.SetShape(targetW, targetH);
                                MelonLogger.Msg("[BE-CYBER] SetShape 返回 = " + (result != null ? "非null" : "null"));
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-CYBER] SetShape 异常: " + ex.Message); }

                            try
                            {
                                var s = gridInv.inventoryShape;
                                if (s != null)
                                    MelonLogger.Msg("[BE-CYBER] 调用后 inventoryShape=" + s.width + "x" + s.height);
                            }
                            catch { }

                            try { gridInv.Validate(); MelonLogger.Msg("[BE-CYBER] Validate 成功"); }
                            catch (Exception ex) { MelonLogger.Warning("[BE-CYBER] Validate 异常: " + ex.Message); }
                        }
                        else MelonLogger.Warning("[BE-CYBER] inner 不是 GameGridInventory");
                    }
                    else MelonLogger.Warning("[BE-CYBER] gi.child 或 gi.child.child == null");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-CYBER] 扩容异常: " + e.Message); }

                // ===== 加我们的识别 tag =====
                try { gi.EnableTag("BE_CYBER_BAG", false); } catch { }

                MelonLogger.Msg("[BE] 电子幽灵创建完成（克隆 satchel）");
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateCyberBag 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通过 tag 识别电子幽灵
        public static bool IsCyberBag(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_CYBER_BAG"); } catch { return false; }
        }
        // ★ 牢力士（克隆原版挎包，保留容器功能）
        public static GameItem CreateLaolishiBag()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("satchel", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 satchel 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 satchel 模板");
                    return null;
                }

                // ===== 改外观 =====
                try { gi.SetName(LaolishiBagName); } catch { }
                try { gi.SetValue(LaolishiBagPrice); } catch { }
                try { gi.SetSprite("", LaolishiBagSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", LaolishiBagSpriteKey); } catch { }
                // ★ 3 宽 × 2 高（跟电子幽灵一样）
                try { gi.SetShape(new GridShapeBuilder(3, 2).SetDataFill(3, 2, 1).Build()); } catch { }
                try { gi.shortDescription = LaolishiBagDescription; } catch { }
                try { gi.longDescription = LaolishiBagDescription; } catch { }

                // ===== ★ 扩容：6×6 =====
                try
                {
                    int targetW = 6;
                    int targetH = 6;

                    if (gi.child != null && gi.child.child != null)
                    {
                        var inner = gi.child.child;
                        var gridInv = inner.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            MelonLogger.Msg("[BE-LAOLISHI] 调用 SetShape(" + targetW + ", " + targetH + ")");
                            try
                            {
                                var result = gridInv.SetShape(targetW, targetH);
                                MelonLogger.Msg("[BE-LAOLISHI] SetShape 返回 = " + (result != null ? "非null" : "null"));
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-LAOLISHI] SetShape 异常: " + ex.Message); }

                            try
                            {
                                var s = gridInv.inventoryShape;
                                if (s != null)
                                    MelonLogger.Msg("[BE-LAOLISHI] 调用后 inventoryShape=" + s.width + "x" + s.height);
                            }
                            catch { }

                            try { gridInv.Validate(); MelonLogger.Msg("[BE-LAOLISHI] Validate 成功"); }
                            catch (Exception ex) { MelonLogger.Warning("[BE-LAOLISHI] Validate 异常: " + ex.Message); }
                        }
                        else MelonLogger.Warning("[BE-LAOLISHI] inner 不是 GameGridInventory");
                    }
                    else MelonLogger.Warning("[BE-LAOLISHI] gi.child 或 gi.child.child == null");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-LAOLISHI] 扩容异常: " + e.Message); }

                // ===== 加我们的识别 tag =====
                try { gi.EnableTag("BE_LAOLISHI_BAG", false); } catch { }

                MelonLogger.Msg("[BE] 牢力士创建完成（克隆 satchel）");
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateLaolishiBag 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通过 tag 识别牢力士
        public static bool IsLaolishiBag(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_LAOLISHI_BAG"); } catch { return false; }
        }

        public static GameItem CreateCatYouyouBag()
        {
            try
            {
                var bag = DirectoryMaster.Item("satchel", false);
                if (bag == null) return null;
                // Keep the vanilla template identifier so saved bags remain loadable.
                bag.identifier = "satchel";
                bag.SetName(CatYouyouBagName);
                bag.SetValue(CatYouyouBagPrice);
                bag.SetSprite("", CatYouyouBagSpriteKey);
                bag.SetSpriteQuick("", CatYouyouBagSpriteKey);
                bag.SetShape(new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build());
                bag.shortDescription = CatYouyouBagDescription;
                bag.longDescription = CatYouyouBagDescription;

                var inventory = bag.child?.child?.TryCast<GameGridInventory>();
                if (inventory == null)
                    throw new InvalidOperationException("satchel inventory is missing");
                inventory.SetShape(8, 8);
                if (inventory.inventoryShape?.width != 8 || inventory.inventoryShape.height != 8)
                    throw new InvalidOperationException("satchel inventory could not resize to 8x8");
                inventory.Validate();
                bag.EnableTag("BE_CAT_YOYOU_BAG", false);
                return bag;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] 创建猫又又小挎包失败: " + e.Message);
                return null;
            }
        }

        private static void SetXuxuBatteryPrice(GameItem item, int price)
        {
            // 原版电池 GetBaseValue = unitValue + lateUnitValue
            //   + unitBaseValue + 当前电量的价值。满电时应等于目标定价。
            int maxEnergy = item.GetTagReadonly("power_source_item_max_energy")?.valueInt ?? 0;
            int chassisValue = Math.Max(0, price - Math.Max(0, maxEnergy));
            item.SetValue(chassisValue);
            item.unitBaseValue = 0;
            item.unitValue = chassisValue;
            item.lateUnitValue = 0;
        }

        // ★ xuxu初级电池（克隆原版自充电能量电池）
        public static GameItem CreateXuxuBattery()
        {
            try
            {
                // ★ 拿原版自充电能量电池做模板
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("energy_credit_breeder", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 energy_credit_breeder 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 energy_credit_breeder 模板");
                    return null;
                }

                // ===== 改外观 =====
                try { gi.SetName(XuxuBatteryName); } catch { }
                try { gi.identifier = XuxuBatteryId; } catch (Exception e) { MelonLogger.Warning("[BE] 改 identifier 失败: " + e.Message); }

                // 自定义贴图不属于原版 atlas，必须使用空 atlas 路径。
                try { gi.SetSprite("", XuxuBatterySpriteKey); } catch { }
                MelonLogger.Msg("[BE-BAT] 现在电池的贴图名 = " + (gi.spritePath ?? "null"));
                try { gi.shortDescription = XuxuBatteryDescription; } catch { }
                try { gi.longDescription = XuxuBatteryDescription; } catch { }

                // ===== 加识别 tag =====
                try { gi.EnableTag("BE_XUXU_BATTERY", false); } catch { }
                // ★ 覆盖电量上限 + 每晚恢复速率 + 供电带宽
                try
                {
                    int newMax = 200;          // 电量上限
                    int newRecharge = 6;      // 每晚恢复电量
                    int newBandwidth = 20;     // 供电/传输速率

                    DumpBatteryTags(gi, "xuxu电池-改前");

                    SetTagInt(gi, "power_source_item_max_energy", newMax);
                    SetTagInt(gi, "power_source_item_energy", newMax);
                    SetTagInt(gi, "power_source_item_recharge", newRecharge);
                    SetTagInt(gi, "power_source_item_bandwidth", newBandwidth);

                    DumpBatteryTags(gi, "xuxu电池-改后");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-BAT] 改上限异常: " + e.Message); }
                try { SetXuxuBatteryPrice(gi, XuxuBatteryPrice); } catch (Exception e) { MelonLogger.Warning("[BE-BAT] 定价失败: " + e.Message); }

                // ★ 诊断：看看 sprite modifier 回调返回什么
                try
                {
                    object fObj = null;
                    try { fObj = gi.getItemSpriteModifierForItemFunc; }
                    catch (Exception ex) { MelonLogger.Warning("[BE-BATTERY-MOD] 读字段异常: " + ex.Message); }

                    if (fObj == null)
                    {
                        MelonLogger.Msg("[BE-BATTERY-MOD] getItemSpriteModifierForItemFunc 为 null");
                    }
                    else
                    {
                        MelonLogger.Msg("[BE-BATTERY-MOD] func 类型=" + fObj.GetType().FullName);

                        object listObj = null;
                        try
                        {
                            var invokeM = fObj.GetType().GetMethod("Invoke",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (invokeM != null)
                                listObj = invokeM.Invoke(fObj, new object[] { gi });
                            else
                                MelonLogger.Msg("[BE-BATTERY-MOD] 找不到 Invoke 方法");
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning("[BE-BATTERY-MOD] Invoke 异常: " + ex.Message);
                        }

                        if (listObj == null)
                        {
                            MelonLogger.Msg("[BE-BATTERY-MOD] modifier 返回 null");
                        }
                        else
                        {
                            int count = -1;
                            try
                            {
                                var cp = listObj.GetType().GetProperty("Count");
                                if (cp != null) count = (int)cp.GetValue(listObj);
                            }
                            catch { }
                            MelonLogger.Msg("[BE-BATTERY-MOD] modifier 数量=" + count);

                            try
                            {
                                var itemProp = listObj.GetType().GetProperty("Item");
                                if (itemProp != null && count > 0)
                                {
                                    for (int i = 0; i < count; i++)
                                    {
                                        object m = null;
                                        try { m = itemProp.GetValue(listObj, new object[] { i }); }
                                        catch { continue; }
                                        if (m == null) { MelonLogger.Msg("[BE-BATTERY-MOD]   [" + i + "] null"); continue; }

                                        MelonLogger.Msg("[BE-BATTERY-MOD]   [" + i + "] type=" + m.GetType().FullName);
                                        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                                        foreach (var fld in m.GetType().GetFields(flags))
                                        {
                                            try
                                            {
                                                if (fld.Name.StartsWith("Native")) continue;
                                                object v = fld.GetValue(m);
                                                string vs = (v == null) ? "null" : v.ToString();
                                                if (vs.Length > 60) vs = vs.Substring(0, 60) + "...";
                                                MelonLogger.Msg("[BE-BATTERY-MOD]     " + fld.Name
                                                    + " : " + fld.FieldType.Name + " = " + vs);
                                            }
                                            catch { }
                                        }
                                        foreach (var pr in m.GetType().GetProperties(flags))
                                        {
                                            try
                                            {
                                                if (!pr.CanRead) continue;
                                                if (pr.GetIndexParameters().Length > 0) continue;
                                                object v = pr.GetValue(m);
                                                string vs = (v == null) ? "null" : v.ToString();
                                                if (vs.Length > 60) vs = vs.Substring(0, 60) + "...";
                                                MelonLogger.Msg("[BE-BATTERY-MOD]     prop " + pr.Name
                                                    + " : " + pr.PropertyType.Name + " = " + vs);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning("[BE-BATTERY-MOD] dump list 元素异常: " + ex.Message);
                            }
                        }
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-BATTERY-MOD] 异常: " + e.Message); }

                MelonLogger.Msg("[BE] xuxu初级电池创建完成（克隆 energy_credit_breeder）");

                // ===== 给电池装 sprite modifier：根据电量返回对应贴图 =====

                try
                {
                    // 用托管 System.Func 定义逻辑
                    System.Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>> managedFunc =
                        (GameItem battery) =>
                        {
                            var list = new Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>();
                            BrewingExpansionMod.LastXuxuBatteryModifierTime = DateTime.Now;
                            try
                            {
                                if (battery == null) return list;

                                int energy = 0, maxEnergy = 100;
                                try { var e = battery.GetTagReadonly("power_source_item_energy"); if (e != null) energy = e.valueInt; } catch { }
                                try { var e = battery.GetTagReadonly("power_source_item_max_energy"); if (e != null) maxEnergy = e.valueInt; } catch { }
                                if (maxEnergy <= 0) maxEnergy = 100;

                                float pct = (float)energy / maxEnergy;
                                string key;
                                if (pct >= 0.99f) key = XuxuBatterySpriteKey;
                                else if (pct >= 0.8f) key = XuxuBatterySpriteKey4;
                                else if (pct >= 0.6f) key = XuxuBatterySpriteKey3;
                                else if (pct >= 0.4f) key = XuxuBatterySpriteKey2;
                                else if (pct >= 0.2f) key = XuxuBatterySpriteKey1;
                                else key = XuxuBatterySpriteKey0;



                                if (!Generated) GenerateAllSprites();
                                Sprite sp = null;
                                if (BEState.GeneratedSprites.TryGetValue(key, out sp) && sp != null)
                                {
                                    var mod = new ItemSpriteModifier(sp,
                                        new Vector2(0f, 0f), 0, false, 0, false, Color.white);
                                    list.Add(mod);
                                }
                                else
                                {
                                    MelonLogger.Warning("[BE-BAT-MOD] sprite 查不到: " + key);
                                }
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-BAT-MOD] " + ex.Message); }
                            return list;
                        };

                    // ★ 关键：用 DelegateSupport.ConvertDelegate 转成 Il2Cpp 委托
                    var il2cppFunc = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<
                        Il2CppSystem.Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>>>(managedFunc);

                    if (il2cppFunc == null)
                    {
                        MelonLogger.Warning("[BE-BAT-MOD] ★ ConvertDelegate 返回 null！");
                    }
                    else
                    {
                        gi.getItemSpriteModifierForItemFunc = il2cppFunc;

                        var readBack = gi.getItemSpriteModifierForItemFunc;
                        MelonLogger.Msg("[BE-BAT-MOD] 赋值后读回 = " + (readBack == null ? "null" : "非null"));
                        if (readBack == null)
                            MelonLogger.Warning("[BE-BAT-MOD] ★ 赋值失败，字段仍是 null");
                        else
                            MelonLogger.Msg("[BE-BAT-MOD] 已给电池装上 getItemSpriteModifierForItemFunc");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-BAT-MOD] 装函数失败: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateXuxuBattery 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通用电池创建：初级/中级/高级共用
        public static GameItem CreateXuxuBatteryGeneric(
            string itemId, string itemName, string desc,
            int price, int maxEnergy, int recharge,
            string tag,
            string spriteKey0, string spriteKey1, string spriteKey2,
            string spriteKey3, string spriteKey4, string spriteKey5)
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item("energy_credit_breeder", false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 energy_credit_breeder 模板异常: " + e.Message); }
                if (gi == null) { MelonLogger.Warning("[BE] 拿不到 energy_credit_breeder 模板"); return null; }

                try { gi.SetName(itemName); } catch { }
                try { gi.identifier = itemId; } catch { }

                try { gi.SetSprite("", spriteKey0); } catch { }
                try { gi.shortDescription = desc; } catch { }
                try { gi.longDescription = desc; } catch { }

                try { gi.EnableTag(tag, false); } catch { }

                try
                {
                    SetTagInt(gi, "power_source_item_max_energy", maxEnergy);
                    SetTagInt(gi, "power_source_item_energy", maxEnergy);
                    SetTagInt(gi, "power_source_item_recharge", recharge);
                }
                catch (Exception e) { MelonLogger.Warning("[BE] 改上限异常: " + e.Message); }
                try { SetXuxuBatteryPrice(gi, price); } catch (Exception e) { MelonLogger.Warning("[BE] 电池定价失败: " + e.Message); }

                // 5 档切图 modifier
                try
                {
                    int capturedMax = maxEnergy;
                    string k0 = spriteKey0, k1 = spriteKey1, k2 = spriteKey2,
                           k3 = spriteKey3, k4 = spriteKey4, k5 = spriteKey5;
                    System.Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>> managedFunc =
                        (GameItem battery) =>
                        {
                            var list = new Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>();
                            try
                            {
                                if (battery == null) return list;

                                int energy = 0, maxE = capturedMax;
                                try { var e = battery.GetTagReadonly("power_source_item_energy"); if (e != null) energy = e.valueInt; } catch { }
                                try { var e = battery.GetTagReadonly("power_source_item_max_energy"); if (e != null) maxE = e.valueInt; } catch { }
                                if (maxE <= 0) maxE = capturedMax;

                                float pct = (float)energy / maxE;
                                string key;
                                if (pct >= 0.99f) key = k0;
                                else if (pct >= 0.8f) key = k1;
                                else if (pct >= 0.6f) key = k2;
                                else if (pct >= 0.4f) key = k3;
                                else if (pct >= 0.2f) key = k4;
                                else key = k5;

                                if (!Generated) GenerateAllSprites();
                                Sprite sp = null;
                                if (BEState.GeneratedSprites.TryGetValue(key, out sp) && sp != null)
                                {
                                    var mod = new ItemSpriteModifier(sp,
                                        new Vector2(0f, 0f), 0, false, 0, false, Color.white);
                                    list.Add(mod);
                                }
                            }
                            catch (Exception ex) { MelonLogger.Warning("[BE-BAT-MOD] " + ex.Message); }
                            return list;
                        };

                    var il2cppFunc = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<
                        Il2CppSystem.Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>>>(managedFunc);
                    if (il2cppFunc != null)
                        gi.getItemSpriteModifierForItemFunc = il2cppFunc;
                }
                catch (Exception e) { MelonLogger.Warning("[BE] 装 modifier 失败: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateXuxuBatteryGeneric " + itemId + " 异常: " + e.Message);
                return null;
            }
        }

        public static GameItem CreateXuxuMidBattery()
        {
            return CreateXuxuBatteryGeneric(
                XuxuMidBatteryId, XuxuMidBatteryName, XuxuMidBatteryDescription,
                XuxuMidBatteryPrice, XuxuMidBatteryMaxEnergy, 8, "BE_XUXU_MID_BATTERY",
                XuxuMidBatterySpriteKey, XuxuMidBatterySpriteKey4, XuxuMidBatterySpriteKey3,
                XuxuMidBatterySpriteKey2, XuxuMidBatterySpriteKey1, XuxuMidBatterySpriteKey0);
        }

        public static GameItem CreateXuxuHighBattery()
        {
            return CreateXuxuBatteryGeneric(
                XuxuHighBatteryId, XuxuHighBatteryName, XuxuHighBatteryDescription,
                XuxuHighBatteryPrice, XuxuHighBatteryMaxEnergy, 12, "BE_XUXU_HIGH_BATTERY",
                XuxuHighBatterySpriteKey, XuxuHighBatterySpriteKey4, XuxuHighBatterySpriteKey3,
                XuxuHighBatterySpriteKey2, XuxuHighBatterySpriteKey1, XuxuHighBatterySpriteKey0);
        }

        public static bool IsXuxuMidBattery(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_XUXU_MID_BATTERY"); } catch { return false; }
        }

        public static bool IsXuxuHighBattery(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_XUXU_HIGH_BATTERY"); } catch { return false; }
        }

        // ★ 通过 tag 识别 xuxu初级电池
        public static bool IsXuxuBattery(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_XUXU_BATTERY"); } catch { return false; }
        }

        // ============ 三个新盲盒 ============

        private static GameItem CreateBlindboxGeneric(string id, string name, string spriteKey, string desc, int price, string tag)
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(id);
                if (item == null) return null;
                item.SetName(name);
                item.SetValue(price);
                item.SetSprite("", spriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;
                try { item.EnableTag(tag, false); } catch { }
                try { item.shortDescription = desc; } catch { }
                try { item.longDescription = desc; } catch { }
                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateBlindboxGeneric " + id + " 异常: " + e.Message); return null; }
        }

        public static GameItem CreateLowBlindbox() { return CreateBlindboxGeneric(LowBlindboxId, LowBlindboxName, LowBlindboxSpriteKey, LowBlindboxDescription, LowBlindboxPrice, LowBlindboxTag); }
        public static GameItem CreateMidBlindbox() { return CreateBlindboxGeneric(MidBlindboxId, MidBlindboxName, MidBlindboxSpriteKey, MidBlindboxDescription, MidBlindboxPrice, MidBlindboxTag); }
        public static GameItem CreateAfterglowBlindbox() { return CreateBlindboxGeneric(AfterglowBlindboxId, AfterglowBlindboxName, AfterglowBlindboxSpriteKey, AfterglowBlindboxDescription, AfterglowBlindboxPrice, AfterglowBlindboxTag); }
        public static GameItem CreateWineBlindbox() { return CreateBlindboxGeneric(WineBlindboxId, WineBlindboxName, WineBlindboxSpriteKey, WineBlindboxDescription, WineBlindboxPrice, WineBlindboxTag); }
        public static GameItem CreateContainerBlindbox() { return CreateBlindboxGeneric(ContainerBlindboxId, ContainerBlindboxName, ContainerBlindboxSpriteKey, ContainerBlindboxDescription, ContainerBlindboxPrice, ContainerBlindboxTag); }
        public static GameItem CreateFigurineBlindbox() { return CreateBlindboxGeneric(FigurineBlindboxId, FigurineBlindboxName, FigurineBlindboxSpriteKey, FigurineBlindboxDescription, FigurineBlindboxPrice, FigurineBlindboxTag); }
        public static GameItem CreateCassetteBundle() { return CreateBlindboxGeneric(CassetteBundleId, CassetteBundleName, CassetteBundleSpriteKey, CassetteBundleDescription, CassetteBundlePrice, CassetteBundleTag); }

        public static bool IsLowBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(LowBlindboxTag); } catch { return false; } }
        public static bool IsMidBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(MidBlindboxTag); } catch { return false; } }
        public static bool IsAfterglowBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(AfterglowBlindboxTag); } catch { return false; } }
        public static bool IsWineBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(WineBlindboxTag); } catch { return false; } }
        public static bool IsContainerBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(ContainerBlindboxTag); } catch { return false; } }
        public static bool IsFigurineBlindbox(GameItem item) { if (item == null) return false; try { return item.IsTag(FigurineBlindboxTag); } catch { return false; } }
        public static bool IsCassetteBundle(GameItem item) { if (item == null) return false; try { return item.identifier == CassetteBundleId || item.IsTag(CassetteBundleTag); } catch { return false; } }

        private static readonly string[] ContainerBlindboxItemIds =
        {
            LaolishiBagId, CyberBagId, SakuraBagId, CatYouyouBagId,
            XuxuWarmCupId, XuxuSweetCupId, Love47CupId
        };
        private static readonly int[] ContainerBlindboxWeights = { 25, 15, 5, 5, 30, 15, 5 };
        private static readonly int[] FigurineBlindboxWeights = { 25, 25, 25, 25 };

        // SeedIds are grouped by rarity: Mortal 0-8, From Afar 9-11,
        // Longing 12-13, and Afterglow 14-15.
        private static string PickTierSeed(System.Random rng, int start, int count)
        {
            return SeedIds[start + rng.Next(count)];
        }

        private static readonly string[] WineBlindboxItemIds = new string[]
        {
    NeonDreamWineId,
    ShuangqingWineId,
    NanfengWineId,
    FeimeiWineId,
    NeonIllusionWineId,
    IdealEndWineId,
    LuofuWineId,
    MagicPigWineId,
    SeaBlueWineId,
    RainbowRecipeWineId,
        };
        internal static bool IsCollectorWine(GameItem item)
        {
            if (item == null) return false;
            string id = item.identifier ?? "";
            return id == NianHeshiWineId || Array.IndexOf(WineBlindboxItemIds, id) >= 0;
        }

        private static readonly int[] WineBlindboxWeights = new int[]
        {
    10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        };

        private static void OpenBlindboxGeneric(GameItem box, string[] itemIds, int[] weights, string logPrefix,
            int prizeCount = 1, Func<System.Random, string> pickId = null)
        {
            if (box == null) return;
            try
            {
                InitMidautumnDelayedFactories();
                int boxUid = -1;
                try { boxUid = box.uniqueId; } catch { }
                DateTime nowBox = DateTime.Now;
                if (boxUid == _lastBoxUniqueId && (nowBox - _lastBoxOpenTime).TotalSeconds < BoxOpenCooldownSeconds) return;
                _lastBoxUniqueId = boxUid;
                _lastBoxOpenTime = nowBox;

                GameGridInventory gridInv = null;
                try { var pi = box.parentInventory; if (pi != null) gridInv = pi.TryCast<GameGridInventory>(); } catch { }

                var rng = new System.Random();
                int totalWeight = 0;
                if (pickId == null)
                {
                    if (itemIds == null || weights == null || itemIds.Length != weights.Length) return;
                    for (int i = 0; i < weights.Length; i++) totalWeight += weights[i];
                    if (totalWeight <= 0) return;
                }
                if (prizeCount < 1) return;
                var prizes = new GameItem[prizeCount];
                for (int draw = 0; draw < prizeCount; draw++)
                {
                    string pickedId;
                    if (pickId != null) pickedId = pickId(rng);
                    else
                    {
                        int roll = rng.Next(totalWeight);
                        int acc = 0;
                        int pickedIndex = 0;
                        for (int i = 0; i < weights.Length; i++)
                        {
                            acc += weights[i];
                            if (roll < acc) { pickedIndex = i; break; }
                        }
                        pickedId = itemIds[pickedIndex];
                    }
                    MelonLogger.Msg(logPrefix + " 第" + (draw + 1) + "次抽出：" + pickedId);
                    try { prizes[draw] = DirectoryMaster.Item(pickedId, true); } catch { }
                    if (prizes[draw] == null || (pickedId.StartsWith("be_", StringComparison.Ordinal)
                        && prizes[draw].name == pickedId))
                    {
                        try { prizes[draw]?.Destroy(); } catch { }
                        for (int created = 0; created < draw; created++)
                            try { prizes[created]?.Destroy(); } catch { }
                        MelonLogger.Warning(logPrefix + " 创建 " + pickedId + " 失败，盲盒不销毁");
                        return;
                    }
                }

                bool allPlaced = true;
                foreach (var prize in prizes)
                {
                    bool placed = false;
                    if (gridInv != null)
                    {
                        try
                        {
                            SlotMarker marker = gridInv.TryFindOneValidInventorySlot_TopMiddle(prize, false);
                            if (marker != null && marker.IsValid())
                            {
                                if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                                int accepted = marker.AcceptUnchecked();
                                if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                            }
                        }
                        catch { }
                        if (!placed)
                        {
                            try
                            {
                                SlotMarker marker = gridInv.TryFindOneValidInventorySlot(prize);
                                if (marker != null && marker.IsValid())
                                {
                                    if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                                    int accepted = marker.AcceptUnchecked();
                                    if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                                }
                            }
                            catch { }
                        }
                    }
                    if (!placed)
                    {
                        try
                        {
                            var store = PlayerStore.Instance;
                            if (store != null) { store.AddDirectSellingItemToTable(prize, true); placed = true; }
                        }
                        catch { }
                    }
                    if (!placed)
                    {
                        allPlaced = false;
                        break;
                    }
                }
                if (!allPlaced)
                {
                    foreach (var prize in prizes) try { prize?.Destroy(); } catch { }
                    MelonLogger.Warning(logPrefix + " 背包和柜台都没有空间，盲盒保留未开启");
                    return;
                }
                try { box.Destroy(); }
                catch (Exception ex)
                {
                    foreach (var prize in prizes) try { prize?.Destroy(); } catch { }
                    MelonLogger.Warning(logPrefix + " 盲盒销毁失败，已撤销本次奖品: " + ex.Message);
                }
            }
            catch (Exception e) { MelonLogger.Warning(logPrefix + " 开箱异常: " + e.Message); }
        }

        public static void OpenLowBlindbox(GameItem box)
        {
            OpenBlindboxGeneric(box, null, null, "[BE-LOWBOX]", 2,
                rng => PickTierSeed(rng, 0, 9));
        }
        public static void OpenMidBlindbox(GameItem box)
        {
            OpenBlindboxGeneric(box, null, null, "[BE-MIDBOX]", 2,
                rng => PickTierSeed(rng, 9, 3));
        }
        public static void OpenAfterglowBlindbox(GameItem box)
        {
            OpenBlindboxGeneric(box, null, null, "[BE-AFTERGLOWBOX]", 2,
                rng => PickTierSeed(rng, 14, 2));
        }
        public static void OpenWineBlindbox(GameItem box) { OpenBlindboxGeneric(box, WineBlindboxItemIds, WineBlindboxWeights, "[BE-WINEBOX]"); }
        public static void OpenContainerBlindbox(GameItem box) { OpenBlindboxGeneric(box, ContainerBlindboxItemIds, ContainerBlindboxWeights, "[BE-CONTAINERBOX]"); }
        public static void OpenFigurineBlindbox(GameItem box) { OpenBlindboxGeneric(box, FigurineIds, FigurineBlindboxWeights, "[BE-FIGURINEBOX]"); }
        public static void OpenCassetteBundle(GameItem box)
        {
            var ids = new List<string>(XIAOWOBrewingBarrel.CustomCassettes.ItemIds).ToArray();
            if (ids.Length != XIAOWOBrewingBarrel.CustomCassettes.PngNames.Length)
            {
                MelonLogger.Warning("[BE-CASSETTEBOX] 磁带未全部注册，捆绑包保留未开启");
                return;
            }
            var next = 0;
            OpenBlindboxGeneric(box, null, null, "[BE-CASSETTEBOX]", ids.Length, _ => ids[next++]);
        }

        // ★ 通过 tag 识别玉兔怀袖
        public static bool IsMidautumnBag(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_MIDAUTUMN_BAG"); } catch { return false; }
        }

        public static void RepairMidautumnBags()
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return;
                var all = store.FindAllItem(true);
                if (all == null) return;

                int scanned = 0, fixedId = 0, fixedShape = 0, fixedTag = 0;

                for (int i = 0; i < all.Count; i++)
                {
                    var it = all[i];
                    if (it == null) continue;
                    scanned++;

                    bool isBag = false;
                    try { isBag = it.IsTag("BE_MIDAUTUMN_BAG"); } catch { }

                    if (!isBag)
                    {
                        try
                        {
                            string sp = it.spritePath ?? "";
                            if (sp == MidautumnBagSpriteKey)
                            {
                                try { it.EnableTag("BE_MIDAUTUMN_BAG", false); fixedTag++; } catch { }
                                isBag = true;
                            }
                        }
                        catch { }
                    }
                    if (!isBag) continue;

                    int uid = -1;
                    try { uid = it.uniqueId; } catch { }

                    string curId = "";
                    try { curId = it.identifier ?? ""; } catch { }
                    if (curId != "satchel")
                    {
                        MelonLogger.Msg("[BE-REPAIR] uid=" + uid + " 修 identifier: '" + curId + "' → 'satchel'");
                        try { it.identifier = "satchel"; } catch { }
                        fixedId++;
                    }

                    try
                    {
                        if (it.child != null && it.child.child != null)
                        {
                            var gridInv = it.child.child.TryCast<GameGridInventory>();
                            if (gridInv != null)
                            {
                                int curW = 0, curH = 0;
                                try
                                {
                                    var s = gridInv.inventoryShape;
                                    if (s != null) { curW = s.width; curH = s.height; }
                                }
                                catch { }

                                if (curW < 20 || curH < 15)
                                {
                                    MelonLogger.Msg("[BE-REPAIR] uid=" + uid + " 扩容 " + curW + "x" + curH + " → 20x15");
                                    try { gridInv.SetShape(20, 15); fixedShape++; }
                                    catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] SetShape 异常: " + ex.Message); }
                                    try { gridInv.Validate(); } catch { }
                                }
                            }
                        }
                    }
                    catch (Exception e) { MelonLogger.Warning("[BE-REPAIR] 扩容异常: " + e.Message); }
                }

                MelonLogger.Msg("[BE-REPAIR] 扫描 " + scanned + " 件，id 修复 " + fixedId
                    + " 件，tag 恢复 " + fixedTag + " 件，形状扩容 " + fixedShape + " 件");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-REPAIR] 异常: " + e.Message); }
        }

        // Older satchel clones changed only `shape`; the game's placement grid
        // reads `modifiedShape`, which remained at the vanilla one-cell size.
        public static void RepairCustomBagShapes()
        {
            try
            {
                var items = PlayerStore.Instance?.FindAllItem(true);
                if (items == null) return;
                int repaired = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    try
                    {
                        if (EnsureCustomBagShape(items[i])) repaired++;
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-BAG] 单个旧背包占格修复失败: " + ex.Message); }
                }
                if (repaired > 0) MelonLogger.Msg("[BE-BAG] 已修复 " + repaired + " 个旧背包的占格");
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-BAG] 旧背包占格扫描失败: " + ex.Message); }
        }

        public static bool EnsureCustomBagShape(GameItem item)
        {
            if (!TryGetCustomBagSize(item, out int width, out int height)) return false;
            var shape = item.shape;
            var effective = item.modifiedShape;
            if (shape == null || shape.width != width || shape.height != height)
                item.SetShape(new GridShapeBuilder(width, height).SetDataFill(width, height, 1).Build());
            else if (effective == null || effective.width * effective.height != width * height)
                item.ValidateShapeState();
            else return false;
            effective = item.modifiedShape;
            if (effective == null || effective.width * effective.height != width * height)
                MelonLogger.Warning("[BE-BAG] 背包占格修复未生效: " + item.name);
            return true;
        }

        private static bool TryGetCustomBagSize(GameItem item, out int width, out int height)
        {
            width = height = 0;
            if (item == null) return false;
            var sprite = item.spritePath;
            var id = item.identifier;
            if (sprite == MidautumnBagSpriteKey || id == MidautumnBagId || item.IsTag("BE_MIDAUTUMN_BAG"))
            { width = 3; height = 3; }
            else if (sprite == SakuraBagSpriteKey || id == SakuraBagId || item.IsTag("BE_SAKURA_BAG"))
            { width = 3; height = 3; }
            else if (sprite == CyberBagSpriteKey || id == CyberBagId || item.IsTag("BE_CYBER_BAG"))
            { width = 3; height = 2; }
            else if (sprite == LaolishiBagSpriteKey || id == LaolishiBagId || item.IsTag("BE_LAOLISHI_BAG"))
            { width = 3; height = 2; }
            else if (sprite == CatYouyouBagSpriteKey || id == CatYouyouBagId || item.IsTag("BE_CAT_YOYOU_BAG"))
            { width = 3; height = 3; }
            return width != 0;
        }

        private static int TryGetUid(GameItem it)
        {
            try { return it.uniqueId; } catch { return -1; }
        }

        // 五仁月饼（3x3，食物，不能吃）
        public static GameItem CreateWurenMooncake()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(WurenMooncakeId);
                if (item == null) return null;
                item.SetName(WurenMooncakeName);
                item.SetValue(WurenMooncakePrice);
                item.SetSprite("", WurenMooncakeSpriteKey);
                item.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("FOOD");
                item.itemTypes = types;
                try { item.shortDescription = WurenMooncakeDescription; } catch { }
                try { item.longDescription = WurenMooncakeDescription; } catch { }
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateWurenMooncake 异常: " + e.Message); return null; }
        }

        // 中秋盲盒「限定」
        public static GameItem CreateMidautumnBlindbox()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(MidautumnBlindboxId);
                if (item == null) return null;
                item.SetName(MidautumnBlindboxName);
                item.SetValue(MidautumnBlindboxPrice);
                item.SetSprite("", MidautumnBlindboxSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;
                try { item.EnableTag(MidautumnBlindboxTag, false); } catch { }
                try { item.shortDescription = MidautumnBlindboxDescription; } catch { }
                try { item.longDescription = MidautumnBlindboxDescription; } catch { }
                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e) { MelonLogger.Warning("[BE] CreateMidautumnBlindbox 异常: " + e.Message); return null; }
        }

        public static bool IsMidautumnBlindbox(GameItem item)
        {
            if (item == null) return false;
            if (item.identifier == MidautumnBlindboxId) return true;
            try { return item.IsTag(MidautumnBlindboxTag); } catch { return false; }
        }

        // Saves made while the Mid-Autumn factory was delayed can contain an
        // identifier-only placeholder. Repair the existing object in place so
        // its inventory location and unique ID remain intact.
        public static void RepairLegacyMidautumnBlindboxes()
        {
            try
            {
                var all = PlayerStore.Instance?.FindAllItem(true);
                if (all == null) return;
                int repaired = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    try
                    {
                        var item = all[i];
                        if (item?.identifier != MidautumnBlindboxId) continue;
                        bool needsRepair = item.name != MidautumnBlindboxName
                            || item.spritePath != MidautumnBlindboxSpriteKey;
                        if (!needsRepair) continue;
                        item.SetName(MidautumnBlindboxName);
                        item.SetValue(MidautumnBlindboxPrice);
                        item.SetSprite("", MidautumnBlindboxSpriteKey);
                        item.SetSpriteQuick("", MidautumnBlindboxSpriteKey);
                        item.shortDescription = MidautumnBlindboxDescription;
                        item.longDescription = MidautumnBlindboxDescription;
                        item.EnableTag(MidautumnBlindboxTag, false);
                        repaired++;
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 单个中秋盲盒修复失败: " + ex.Message); }
                }
                if (repaired > 0) MelonLogger.Msg("[BE-REPAIR] 已修复 " + repaired + " 个旧存档中秋盲盒贴图和名称");
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 中秋盲盒修复失败: " + ex); }
        }

        public static void RefreshOwnedSeedBlindboxes()
        {
            try
            {
                var all = PlayerStore.Instance?.FindAllItem(true);
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    try
                    {
                        var item = all[i];
                        if (item == null) continue;
                        string name, description, spriteKey;
                        int price;
                        if (item.identifier == LowBlindboxId)
                        { name = LowBlindboxName; description = LowBlindboxDescription; spriteKey = LowBlindboxSpriteKey; price = LowBlindboxPrice; }
                        else if (item.identifier == MidBlindboxId)
                        { name = MidBlindboxName; description = MidBlindboxDescription; spriteKey = MidBlindboxSpriteKey; price = MidBlindboxPrice; }
                        else if (item.identifier == PremiumBlindboxId)
                        { name = PremiumBlindboxName; description = PremiumBlindboxDescription; spriteKey = PremiumBlindboxSpriteKey; price = PremiumBlindboxPrice; }
                        else if (item.identifier == CassetteBundleId)
                        { name = CassetteBundleName; description = CassetteBundleDescription; spriteKey = CassetteBundleSpriteKey; price = CassetteBundlePrice; }
                        else continue;
                        item.SetName(name);
                        item.SetValue(price);
                        item.SetSprite("", spriteKey);
                        item.SetSpriteQuick("", spriteKey);
                        item.shortDescription = description;
                        item.longDescription = description;
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 种子盲盒更新失败: " + ex.Message); }
                }
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 种子盲盒扫描失败: " + ex.Message); }
        }

        public static void RefreshOwnedBatteryPrices()
        {
            try
            {
                var all = PlayerStore.Instance?.FindAllItem(true);
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    try
                    {
                        var item = all[i];
                        if (item == null) continue;
                        if (item.identifier == XuxuBatteryId) SetXuxuBatteryPrice(item, XuxuBatteryPrice);
                        else if (item.identifier == XuxuMidBatteryId) SetXuxuBatteryPrice(item, XuxuMidBatteryPrice);
                        else if (item.identifier == XuxuHighBatteryId) SetXuxuBatteryPrice(item, XuxuHighBatteryPrice);
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 电池价格更新失败: " + ex.Message); }
                }
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-REPAIR] 电池扫描失败: " + ex.Message); }
        }

        public static bool IsMidautumnMooncakeBox(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(MidautumnMooncakeBoxTag); } catch { return false; }
        }

        // ★ 中秋盲盒内容与权重
        private static readonly string[] MidautumnBlindboxItemIds = new string[]
        {
            MidautumnMoonFanId,      // 蟾宫折桂扇
            MidautumnMoonLampId,     // 云开月明灯
            MidautumnJadeRabbitId,   // 蟾宫玉兔
            MidautumnLanternId,      // 花好月圆灯
            MidautumnMoonOsmanthusId,  // ★ 月华桂
            MidautumnMooncakeBoxId,  // 月团礼盒
            MidautumnBagId,          // 兔衔明月
        };
        private static readonly int[] MidautumnBlindboxWeights = new int[]
        {
            10,  // 扇 10%
            10,  // 灯 10%
            10,  // 兔 10%
            10,  // 灯笼 10%
            25,  // 月华桂25%
            30,  // 礼盒 30%
            5,   // 挎包 5%
        };

        // ★ 中秋盲盒开箱逻辑
        private static int _lastMidautumnBoxUid = -1;
        private static DateTime _lastMidautumnBoxTime = DateTime.MinValue;
        public static void OpenMidautumnBlindbox(GameItem box)
        {
            if (box == null) return;
            try
            {
                int boxUid = -1;
                try { boxUid = box.uniqueId; } catch { }
                DateTime nowBox = DateTime.Now;
                if (boxUid == _lastMidautumnBoxUid
                    && (nowBox - _lastMidautumnBoxTime).TotalSeconds < BoxOpenCooldownSeconds) return;
                _lastMidautumnBoxUid = boxUid;
                _lastMidautumnBoxTime = nowBox;

                GameGridInventory gridInv = null;
                try { var pi = box.parentInventory; if (pi != null) gridInv = pi.TryCast<GameGridInventory>(); } catch { }

                int totalWeight = 0;
                for (int i = 0; i < MidautumnBlindboxWeights.Length; i++) totalWeight += MidautumnBlindboxWeights[i];
                if (totalWeight <= 0) return;
                var rng = new System.Random();
                int roll = rng.Next(totalWeight);
                int acc = 0;
                int pickedIndex = 0;
                for (int i = 0; i < MidautumnBlindboxWeights.Length; i++)
                {
                    acc += MidautumnBlindboxWeights[i];
                    if (roll < acc) { pickedIndex = i; break; }
                }
                string pickedId = MidautumnBlindboxItemIds[pickedIndex];
                MelonLogger.Msg("[BE-MIDAUTUMN] 抽出：" + pickedId + "（roll=" + roll + "/" + totalWeight + "）");

                GameItem prize = null;
                try { prize = DirectoryMaster.Item(pickedId, true); } catch { }
                if (prize == null) { MelonLogger.Warning("[BE-MIDAUTUMN] 创建 " + pickedId + " 失败，盲盒不销毁"); return; }

                try { box.Destroy(); } catch { }

                bool placed = false;
                if (gridInv != null)
                {
                    try
                    {
                        SlotMarker marker = gridInv.TryFindOneValidInventorySlot_TopMiddle(prize, false);
                        if (marker != null && marker.IsValid())
                        {
                            if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                            int accepted = marker.AcceptUnchecked();
                            if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                        }
                    }
                    catch { }
                    if (!placed)
                    {
                        try
                        {
                            SlotMarker marker = gridInv.TryFindOneValidInventorySlot(prize);
                            if (marker != null && marker.IsValid())
                            {
                                if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                                int accepted = marker.AcceptUnchecked();
                                if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                            }
                        }
                        catch { }
                    }
                }
                if (!placed)
                {
                    try
                    {
                        var store = PlayerStore.Instance;
                        if (store != null) { store.AddDirectSellingItemToTable(prize, true); placed = true; }
                    }
                    catch { }
                }
                if (!placed) MelonLogger.Warning("[BE-MIDAUTUMN] 开箱后无处安放，物品丢失");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-MIDAUTUMN] 开箱异常: " + e.Message); }
        }

        // ★ 月团礼盒开箱逻辑
        private static int _lastMooncakeBoxUid = -1;
        private static DateTime _lastMooncakeBoxTime = DateTime.MinValue;
        public static void OpenMidautumnMooncakeBox(GameItem box)
        {
            if (box == null) return;
            try
            {
                int boxUid = -1;
                try { boxUid = box.uniqueId; } catch { }
                DateTime nowBox = DateTime.Now;
                if (boxUid == _lastMooncakeBoxUid
                    && (nowBox - _lastMooncakeBoxTime).TotalSeconds < BoxOpenCooldownSeconds) return;
                _lastMooncakeBoxUid = boxUid;
                _lastMooncakeBoxTime = nowBox;

                GameGridInventory gridInv = null;
                try { var pi = box.parentInventory; if (pi != null) gridInv = pi.TryCast<GameGridInventory>(); } catch { }

                GameItem prize = null;
                try { prize = DirectoryMaster.Item(WurenMooncakeId, true); } catch { }
                if (prize == null) { MelonLogger.Warning("[BE-MIDAUTUMN] 创建五仁月饼失败，礼盒不销毁"); return; }

                try { box.Destroy(); } catch { }

                bool placed = false;
                if (gridInv != null)
                {
                    try
                    {
                        SlotMarker marker = gridInv.TryFindOneValidInventorySlot_TopMiddle(prize, false);
                        if (marker != null && marker.IsValid())
                        {
                            if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                            int accepted = marker.AcceptUnchecked();
                            if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                        }
                    }
                    catch { }
                    if (!placed)
                    {
                        try
                        {
                            SlotMarker marker = gridInv.TryFindOneValidInventorySlot(prize);
                            if (marker != null && marker.IsValid())
                            {
                                if (marker.itemGridShape != null) try { prize.shape = marker.itemGridShape; } catch { }
                                int accepted = marker.AcceptUnchecked();
                                if (accepted > 0) { placed = true; try { gridInv.Validate(); } catch { } }
                            }
                        }
                        catch { }
                    }
                }
                if (!placed)
                {
                    try
                    {
                        var store = PlayerStore.Instance;
                        if (store != null) { store.AddDirectSellingItemToTable(prize, true); placed = true; }
                    }
                    catch { }
                }
                if (!placed) MelonLogger.Warning("[BE-MIDAUTUMN] 礼盒开箱后无处安放，物品丢失");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-MIDAUTUMN] 礼盒开箱异常: " + e.Message); }
        }

        // ★ 计算装饰品吸引力加成
        public static int CalculateDecorationAttractiveness()
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return 0;
                var all = store.FindAllItem(true);
                if (all == null) return 0;

                // ★ 按 identifier 去重：每种装饰品只生效一个
                var seen = new HashSet<string>();
                for (int i = 0; i < all.Count; i++)
                {
                    var it = all[i];
                    if (it == null) continue;
                    try
                    {
                        if (!it.IsTag(DecorationTag)) continue;
                        string id = it.identifier ?? "";
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!seen.Contains(id)) seen.Add(id);
                    }
                    catch { }
                }

                return seen.Count * DecorationAttractivenessPerItem;
            }
            catch { return 0; }
        }

        // ★ 四十七的货：打 tag 后上架
        private static void Add47StockToTable(GameItem item)
        {
            if (item == null) return;
            try { item.EnableTag(Trader47StockTag, false); } catch { }
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return;
                store.AddDirectSellingItemToTable(item, false);

                // ★ 新增：记下这次上架的物品
                Trader47StockList.Add(item);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-NPC-47] 上架物品失败: " + e.Message); }
        }

        // ★ 高级盲盒
        public static GameItem CreatePremiumBlindbox()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(PremiumBlindboxId);
                if (item == null) return null;
                item.SetName(PremiumBlindboxName);
                item.SetValue(PremiumBlindboxPrice);
                item.SetSprite("", PremiumBlindboxSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();

                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("LUXURY_ITEM");
                item.itemTypes = types;

                try { item.EnableTag(PremiumBlindboxTag, false); } catch { }
                try { item.shortDescription = PremiumBlindboxDescription; } catch { }
                try { item.longDescription = PremiumBlindboxDescription; } catch { }
                NativeItemActivation.Bind(item);
                return item;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreatePremiumBlindbox 异常: " + e.Message);
                return null;
            }
        }

        // ★ 盲盒开箱去重：同一物品 0.5 秒内只开一次
        private static int _lastBoxUniqueId = -1;
        private static DateTime _lastBoxOpenTime = DateTime.MinValue;
        private const double BoxOpenCooldownSeconds = 0.5;
        public static void OpenPremiumBlindbox(GameItem box)
        {
            OpenBlindboxGeneric(box, null, null, "[BE-BOX]", 2,
                rng => PickTierSeed(rng, 12, 2));
        }

        public static bool IsPremiumBlindbox(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(PremiumBlindboxTag); } catch { return false; }
        }

        public static bool IsYingfeiFilter(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(YingfeiFilterTag); } catch { return false; }
        }

        public static bool IsNingluFilter(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(NingluFilterTag); } catch { return false; }
        }

        public static bool IsQingyunFilter(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(QingyunFilterTag); } catch { return false; }
        }

        public static void RefreshOwnedFilterDescriptions()
        {
            try
            {
                var owned = PlayerStore.Instance?.FindAllItem(true);
                if (owned == null) return;
                for (int i = 0; i < owned.Count; i++)
                {
                    try
                    {
                        var item = owned[i];
                        if (item == null) continue;
                        string description = IsQingyunFilter(item) ? QingyunFilterDescription
                            : IsNingluFilter(item) ? NingluFilterDescription
                            : IsYingfeiFilter(item) ? YingfeiFilterDescription : null;
                        if (description == null) continue;
                        item.shortDescription = description;
                        item.longDescription = description;
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ★ 诊断：打印物品的所有 tag 相关字段
        public static void DumpItemTags(GameItem item, string label)
        {
            if (item == null) { MelonLogger.Msg("[BE-TAG] " + label + ": item == null"); return; }
            try
            {
                var t = item.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                MelonLogger.Msg("[BE-TAG] === " + label + " ===");

                foreach (var f in t.GetFields(flags))
                {
                    try
                    {
                        if (f.Name.StartsWith("Native")) continue;
                        string fn = f.Name;
                        string tn = f.FieldType.Name;

                        object v = f.GetValue(item);
                        string vs = "null";
                        if (v != null)
                        {
                            try
                            {
                                if (v is System.Collections.IEnumerable en && !(v is string))
                                {
                                    var items = new List<string>();
                                    int i = 0;
                                    foreach (var elem in en)
                                    {
                                        if (i++ > 200) { items.Add("..."); break; }
                                        items.Add(elem?.ToString() ?? "null");
                                    }
                                    vs = "[" + items.Count + "] " + string.Join(" | ", items);
                                }
                                else
                                {
                                    vs = v.ToString();
                                }
                            }
                            catch (Exception ex)
                            {
                                vs = "<" + ex.Message + ">";
                            }
                        }

                        bool interesting = false;
                        if (fn.IndexOf("tag", StringComparison.OrdinalIgnoreCase) >= 0) interesting = true;
                        if (tn.IndexOf("tag", StringComparison.OrdinalIgnoreCase) >= 0) interesting = true;
                        if (fn.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0) interesting = true;
                        if (vs.Contains("380") || vs.Contains("400") || vs.Contains("600")) interesting = true;
                        if (vs.Contains("DURABILITY") || vs.Contains("FILTER")) interesting = true;

                        if (interesting)
                            MelonLogger.Msg("[BE-TAG]   " + fn + " : " + tn + " = " + vs);
                    }
                    catch { }
                }

                // 猜常见 tag 名
                var guessTags = new string[]
                {
                    "WATER_FILTER",
                    "WATER_FILTER_DURABILITY",
                    "WATER_FILTER_MAX_DURABILITY",
                    "FILTER_DURABILITY",
                    "DURABILITY",
                    "IS_WATER_FILTER",
                    "FILTER",
                    "WATER_FILTER_ADV",
                    "WATER_FILTER_USES",
                    "FILTER_USES",
                };
                MelonLogger.Msg("[BE-TAG] === 猜测 tag 名 ===");
                foreach (var g in guessTags)
                {
                    try
                    {
                        bool has = item.IsTag(g);
                        MelonLogger.Msg("[BE-TAG]   IsTag(\"" + g + "\") = " + has);
                    }
                    catch (Exception ex) { MelonLogger.Warning("[BE-TAG]   IsTag(" + g + ") 异常: " + ex.Message); }
                    // ★ 打印 WATER_FILTER 和 FILTER 的 TagState 内容
                    DumpTagState(item, "WATER_FILTER");
                    DumpTagState(item, "FILTER");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-TAG] DumpItemTags 异常: " + e.Message);
            }
        }

        // ★ 诊断：打印一个 tag 的 TagState 内容
        public static void DumpTagState(GameItem item, string tagName)
        {
            try
            {
                if (item == null) return;
                var t = typeof(GameItem);
                var m = t.GetMethod("GetTagReadonly",
                    BindingFlags.Public | BindingFlags.Instance);
                if (m == null) { MelonLogger.Warning("[BE-TAG] 找不到 GetTagReadonly"); return; }

                object state = null;
                try { state = m.Invoke(item, new object[] { tagName }); }
                catch (Exception ex) { MelonLogger.Warning("[BE-TAG] GetTagReadonly(" + tagName + ") 异常: " + ex.Message); return; }

                if (state == null) { MelonLogger.Msg("[BE-TAG] tag=" + tagName + " → TagState=null"); return; }

                var st = state.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                MelonLogger.Msg("[BE-TAG] === TagState(" + tagName + ") ===");

                foreach (var f in st.GetFields(flags))
                {
                    try
                    {
                        object v = f.GetValue(state);
                        MelonLogger.Msg("[BE-TAG]   field " + f.Name + " : " + f.FieldType.Name + " = " + (v?.ToString() ?? "null"));
                    }
                    catch { }
                }
                foreach (var p in st.GetProperties(flags))
                {
                    try
                    {
                        object v = null;
                        try { v = p.GetValue(state); } catch { }
                        MelonLogger.Msg("[BE-TAG]   prop " + p.Name + " : " + p.PropertyType.Name + " = " + (v?.ToString() ?? "null"));
                    }
                    catch { }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-TAG] DumpTagState 异常: " + e.Message); }
        }

        // ★ 诊断：打印物品所有字段（只打印基本类型 + 名字含关键词的）
        public static void DumpAllPrimitiveFields(object obj, string label)
        {
            if (obj == null) return;
            try
            {
                var t = obj.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                MelonLogger.Msg("[BE-DUMP] === " + label + " (" + t.FullName + ") ===");

                foreach (var f in t.GetFields(flags))
                {
                    try
                    {
                        if (f.Name.StartsWith("Native")) continue;
                        string tn = f.FieldType.Name;

                        // 只处理基本类型和 string
                        bool isPrimitive = (f.FieldType.IsPrimitive
                            || tn == "String"
                            || tn == "Int32"
                            || tn == "Int64"
                            || tn == "Single"
                            || tn == "Double"
                            || tn == "Boolean");

                        object v = null;
                        try { v = f.GetValue(obj); } catch { continue; }
                        string vs = v?.ToString() ?? "null";
                        if (vs.Length > 80) vs = vs.Substring(0, 80) + "...";

                        // 打印条件：基本类型 或 名字含关键词
                        bool nameInteresting = false;
                        string fn = f.Name.ToLower();
                        if (fn.Contains("dura") || fn.Contains("filter")
                            || fn.Contains("value") || fn.Contains("count")
                            || fn.Contains("condition") || fn.Contains("level")
                            || fn.Contains("current") || fn.Contains("max"))
                            nameInteresting = true;

                        if (isPrimitive || nameInteresting)
                            MelonLogger.Msg("[BE-DUMP]   field " + f.Name + " : " + tn + " = " + vs);
                    }
                    catch { }
                }

                // 属性：只打印基本类型
                foreach (var p in t.GetProperties(flags))
                {
                    try
                    {
                        if (!p.CanRead) continue;
                        string tn = p.PropertyType.Name;
                        if (tn != "Int32" && tn != "Int64" && tn != "Single"
                            && tn != "Double" && tn != "Boolean" && tn != "String") continue;

                        object v = null;
                        try { v = p.GetValue(obj); } catch { continue; }
                        string vs = v?.ToString() ?? "null";
                        if (vs.Length > 80) vs = vs.Substring(0, 80) + "...";

                        MelonLogger.Msg("[BE-DUMP]   prop " + p.Name + " : " + tn + " = " + vs);
                    }
                    catch { }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-DUMP] DumpAllPrimitiveFields 异常: " + e.Message); }
        }

        // ★ 诊断：打印物品的所有 ItemFeature
        public static void DumpItemFeatures(GameItem item, string label)
        {
            if (item == null) return;
            try
            {
                MelonLogger.Msg("[BE-FEAT] === " + label + " ===");

                // 尝试从 ItemFeatureList 或 item 上找 feature 集合
                var t = item.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                bool found = false;
                foreach (var p in t.GetProperties(flags))
                {
                    try
                    {
                        if (!p.CanRead) continue;
                        string pn = p.Name.ToLower();
                        if (!pn.Contains("feature")) continue;

                        var v = p.GetValue(item);
                        if (v == null) continue;
                        found = true;
                        MelonLogger.Msg("[BE-FEAT] 属性 " + p.Name + " = " + v.GetType().Name);

                        DumpFeatureCollection(v);
                    }
                    catch { }
                }

                foreach (var f in t.GetFields(flags))
                {
                    try
                    {
                        string fn = f.Name.ToLower();
                        if (!fn.Contains("feature")) continue;
                        if (f.Name.StartsWith("Native")) continue;

                        var v = f.GetValue(item);
                        if (v == null) continue;
                        found = true;
                        MelonLogger.Msg("[BE-FEAT] 字段 " + f.Name + " = " + v.GetType().Name);

                        DumpFeatureCollection(v);
                    }
                    catch { }
                }

                if (!found)
                    MelonLogger.Msg("[BE-FEAT] 没找到 feature 相关字段");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-FEAT] DumpItemFeatures 异常: " + e.Message); }
        }

        private static void DumpFeatureCollection(object collection)
        {
            try
            {
                if (collection == null) { MelonLogger.Msg("[BE-FEAT]   (null collection)"); return; }

                // 尝试当成 Il2Cpp List<T> 处理
                var t = collection.GetType();
                var countProp = t.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                var itemProp = t.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
                if (countProp != null && itemProp != null)
                {
                    int count = 0;
                    try { count = (int)countProp.GetValue(collection); } catch { }
                    MelonLogger.Msg("[BE-FEAT]   list count = " + count);

                    for (int i = 0; i < count && i < 20; i++)
                    {
                        object elem = null;
                        try { elem = itemProp.GetValue(collection, new object[] { i }); } catch { }
                        MelonLogger.Msg("[BE-FEAT]   -- feature[" + i + "] --");
                        DumpFeatureFields(elem);
                    }
                    return;
                }

                // 兜底：当成普通 IEnumerable
                if (collection is System.Collections.IEnumerable en && !(collection is string))
                {
                    int idx = 0;
                    foreach (var elem in en)
                    {
                        MelonLogger.Msg("[BE-FEAT]   -- feature[" + idx + "] --");
                        DumpFeatureFields(elem);
                        idx++;
                        if (idx > 20) break;
                    }
                }
                else
                {
                    DumpFeatureFields(collection);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-FEAT] DumpFeatureCollection 异常: " + e.Message); }
        }

        private static void DumpFeatureFields(object feature)
        {
            if (feature == null) { MelonLogger.Msg("[BE-FEAT]     (null)"); return; }
            try
            {
                var t = feature.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                MelonLogger.Msg("[BE-FEAT]     type=" + t.FullName);

                foreach (var f in t.GetFields(flags))
                {
                    try
                    {
                        if (f.Name.StartsWith("Native")) continue;
                        if (!f.FieldType.IsPrimitive && f.FieldType.Name != "String") continue;
                        var v = f.GetValue(feature);
                        MelonLogger.Msg("[BE-FEAT]       field " + f.Name + " : " + f.FieldType.Name + " = " + (v?.ToString() ?? "null"));
                    }
                    catch { }
                }

                foreach (var p in t.GetProperties(flags))
                {
                    try
                    {
                        if (!p.CanRead) continue;
                        var pt = p.PropertyType;
                        if (!pt.IsPrimitive && pt.Name != "String") continue;
                        var v = p.GetValue(feature);
                        MelonLogger.Msg("[BE-FEAT]       prop " + p.Name + " : " + pt.Name + " = " + (v?.ToString() ?? "null"));
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ============ 新增：字段快照对比工具 ============
        // ============ 字段快照对比工具（拍 ItemFeature，不拍 GameItem） ============
        private static readonly Dictionary<string, string> _filterSnapshot = new Dictionary<string, string>();
        private static string _filterSnapshotOwner = null;

        // 从青筠身上找出 DURABILITY_TAG 这个 ItemFeature
        public static object FindDurabilityFeature(GameItem item)
        {
            if (item == null) return null;
            try
            {
                var list = item.itemFeatures;
                if (list == null) return null;
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    if (f == null) continue;
                    string id = null;
                    try { id = f.identifier; } catch { }
                    if (id == "DURABILITY_TAG") return f;
                }
            }
            catch { }
            return null;
        }

        // 拍一张 DURABILITY_TAG 的快照，或者对比上一张快照
        public static void SnapshotDurability(GameItem item, string tag)
        {
            if (item == null)
            {
                MelonLogger.Msg("[BE-SNAP] " + tag + " item == null");
                return;
            }

            var feature = FindDurabilityFeature(item);
            if (feature == null)
            {
                MelonLogger.Msg("[BE-SNAP] " + tag + " 找不到 DURABILITY_TAG");
                return;
            }

            try
            {
                var t = feature.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var current = new Dictionary<string, string>();

                // 收集所有字段
                foreach (var f in t.GetFields(flags))
                {
                    if (f.Name.StartsWith("Native")) continue;
                    try
                    {
                        var v = f.GetValue(feature);
                        string vs = (v == null) ? "null"
                            : (f.FieldType.IsPrimitive ? v.ToString()
                            : (v is string s ? s : "<" + v.GetType().Name + ">"));
                        current["F:" + f.Name] = vs;
                    }
                    catch { }
                }

                // 收集所有属性
                foreach (var p in t.GetProperties(flags))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    try
                    {
                        var v = p.GetValue(feature);
                        string vs = (v == null) ? "null"
                            : (p.PropertyType.IsPrimitive ? v.ToString()
                            : (v is string s ? s : "<" + v.GetType().Name + ">"));
                        current["P:" + p.Name] = vs;
                    }
                    catch { }
                }

                // ★ 关键：用固定 tag 名，靠"时序"对比
                if (_filterSnapshotOwner != tag || _filterSnapshot.Count == 0)
                {
                    // 第一次见到这个 tag，重建仓库（不打印，省日志）
                    MelonLogger.Msg("[BE-SNAP] === " + tag + " 基准快照（已记录，不打印）===");
                    _filterSnapshot.Clear();
                    foreach (var kv in current) _filterSnapshot[kv.Key] = kv.Value;
                    _filterSnapshotOwner = tag;
                }
                else
                {
                    // 同一个 tag 第二次调用，做对比
                    MelonLogger.Msg("[BE-SNAP] === " + tag + " 变化对比 ===");
                    int changed = 0;
                    foreach (var kv in current)
                    {
                        string oldVal;
                        if (_filterSnapshot.TryGetValue(kv.Key, out oldVal))
                        {
                            if (oldVal != kv.Value)
                            {
                                MelonLogger.Msg("[BE-SNAP]   ★ " + kv.Key + " : " + oldVal + " → " + kv.Value);
                                changed++;
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("[BE-SNAP]   + 新增 " + kv.Key + " = " + kv.Value);
                        }
                    }
                    if (changed == 0)
                        MelonLogger.Msg("[BE-SNAP]   （无变化）");

                    // 更新快照，供下次对比
                    _filterSnapshot.Clear();
                    foreach (var kv in current) _filterSnapshot[kv.Key] = kv.Value;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-SNAP] SnapshotDurability 异常: " + e.Message); }
        }
        // ============ 快照工具结束 ============
        // ============ 新增结束 ============

        public static void DumpWaterHelperMethods()
        {
            try
            {
                var t = typeof(WaterHelper);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                MelonLogger.Msg("[BE-SNAP] === WaterHelper 方法列表 ===");
                foreach (var m in t.GetMethods(flags))
                {
                    string n = m.Name;
                    if (n.IndexOf("filter", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("dura", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("charge", StringComparison.OrdinalIgnoreCase) < 0
                        && n.IndexOf("value", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var ps = m.GetParameters();
                    var sb = new System.Text.StringBuilder();
                    sb.Append("[BE-SNAP]   ").Append(m.ReturnType.Name).Append(" ").Append(n).Append("(");
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ps[i].ParameterType.Name).Append(" ").Append(ps[i].Name);
                    }
                    sb.Append(")");
                    MelonLogger.Msg(sb.ToString());
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-SNAP] DumpWaterHelperMethods 异常: " + e.Message); }
        }

        // ★ 新增：dump 原版吸引力阈值
        public static void DumpAttractThresholds()
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null)
                {
                    MelonLogger.Msg("[BE-ATTRACT-TH] PlayerStore.Instance == null");
                    return;
                }

                MelonLogger.Msg("[BE-ATTRACT-TH] === 原版吸引力阈值 ===");
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   baseStoreAttractiveness = " + store.baseStoreAttractiveness); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   baseStoreAttractiveness 异常: " + e.Message); }
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   projectorAttractivenessBonus = " + store.projectorAttractivenessBonus); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   projectorAttractivenessBonus 异常: " + e.Message); }
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   GetCurrentStoreAttractiveness() = " + store.GetCurrentStoreAttractiveness()); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   GetCurrentStoreAttractiveness 异常: " + e.Message); }

                // ★ 阈值是静态的，用类名访问
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   ONE_NORMAL_ATT_THRESHOLD = " + StoreClientManager.ONE_NORMAL_ATT_THRESHOLD); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   ONE 异常: " + e.Message); }
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   TWO_NORMAL_ATT_THRESHOLD = " + StoreClientManager.TWO_NORMAL_ATT_THRESHOLD); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   TWO 异常: " + e.Message); }
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   THREE_NORMAL_ATT_THRESHOLD = " + StoreClientManager.THREE_NORMAL_ATT_THRESHOLD); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   THREE 异常: " + e.Message); }
                try { MelonLogger.Msg("[BE-ATTRACT-TH]   MINIMUM_ATT_DISPLAY_CASE = " + StoreClientManager.MINIMUM_ATT_DISPLAY_CASE); } catch (Exception e) { MelonLogger.Msg("[BE-ATTRACT-TH]   MINIMUM 异常: " + e.Message); }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-ATTRACT-TH] DumpAttractThresholds 异常: " + e.Message); }
        }

        public static bool IsClearWaterTank(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(ClearWaterTankTag); } catch { return false; }
        }

        public static void EnsureClearWaterTankCapacity(GameItem item)
        {
            if (!IsClearWaterTank(item)) return;
            const int capacityInParts = ClearWaterTankCapacityMl * 1000;
            var capacity = item.GetTagReadonly(LiquidContainerCapacityTag);
            if (capacity == null || capacity.valueInt == capacityInParts) return;
            SetTagInt(item, LiquidContainerCapacityTag, capacityInParts);
        }
        // ★ 新方法结束

        public static GameItem CreateMultiStorage()
        {
            try
            {
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(SmugglerBayModId, false); } catch { }
                if (gi == null) return null;

                gi.SetName(MultiStorageName);
                gi.SetValue(MultiStoragePrice);
                gi.SetSprite("", MultiStorageSpriteKey);
                gi.SetSpriteQuick("", MultiStorageSpriteKey);
                gi.shape = new GridShapeBuilder(2, 4).SetDataFill(2, 4, 1).Build();
                try { gi.EnableTag(MultiStorageTag, false); } catch { }

                // 扩容到 15x20
                try
                {
                    if (gi.child != null && gi.child.child != null)
                    {
                        var gridInv = gi.child.child.TryCast<GameGridInventory>();
                        if (gridInv != null)
                        {
                            gridInv.SetShape(15, 20);
                            gridInv.Validate();
                        }
                    }
                }
                catch { }

                // ★ 注册右键菜单的"切换外观"（18 张 = 主贴图 + 17 变体）
                try
                {
                    int totalCount = 1 + MultiStorageVariantSpriteKeys.Length;
                    var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(totalCount);

                    // 第 0 张 = 主贴图
                    arr[0] = MultiStorageSpriteKey;
                    // 第 1~17 张 = 变体
                    for (int i = 0; i < MultiStorageVariantSpriteKeys.Length; i++)
                        arr[i + 1] = MultiStorageVariantSpriteKeys[i];

                    ToggleAppearanceHelper.MakeSpriteCyclable(gi, "", arr);
                    MelonLogger.Msg("[BE] MakeSpriteCyclable 成功，共 " + arr.Length + " 张贴图");
                }
                catch (Exception e) { MelonLogger.Warning("[BE] MakeSpriteCyclable 失败: " + e.Message); }

                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateMultiStorage 异常: " + e.Message);
                return null;
            }
        }

        public static GameItem CreateRecipeItem(int index)
        {
            try
            {
                if (index < 0 || index >= RecipeIds.Length) return null;
                var item = ItemDirectory.CreateEmptyItem(RecipeIds[index]);
                if (item == null) return null;
                item.SetName(RecipeNames[index]);
                item.SetValue(RecipePrices[index]);
                item.SetSprite("", RecipeSpriteKeys[index]);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("DOCUMENT");
                item.itemTypes = types;
                try { item.shortDescription = RecipeDescriptions[index]; } catch { }
                try { item.longDescription = RecipeDescriptions[index]; } catch { }
                return item;
            }
            catch { return null; }
        }

        public static GameItem CreateHentaiYeast()
        {
            try
            {
                // Native water-only fermentation recognizes the synthetic red yeast.
                // Keep its identifier and use our own tag to select the output wine.
                var item = DirectoryMaster.Item(VanillaRedYeastId, false);
                if (item == null) return null;
                item.EnableTag(HentaiYeastTag, false);
                item.SetName(HentaiYeastName);
                item.SetValue(HentaiYeastPrice);
                item.DisableTag("WINE_YEAST_REUSABLE_TAG", false);
                item.SetSprite("", HentaiYeastSpriteKey);
                item.SetSpriteQuick("", HentaiYeastSpriteKey);
                item.shape = new GridShapeBuilder(1, 2).SetDataFill(1, 2, 1).Build();
                item.modifiedShape = item.shape.Clone();
                item.shortDescription = HentaiYeastDescription;
                item.longDescription = HentaiYeastDescription;
                item.ValidateShapeState();
                return item;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[BE] hentai yeast creation failed: " + ex);
                return null;
            }
        }

        public static void RefreshOwnedRecipePrices()
        {
            try
            {
                var all = PlayerStore.Instance?.FindAllItem(true);
                if (all == null) return;
                for (int i = 0; i < all.Count; i++)
                {
                    var item = all[i];
                    if (item != null && IsOurRecipe(item.identifier)) item.SetValue(50);
                }
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-RECIPE] 更新旧配方价格失败: " + ex.Message); }
        }

        public static bool IsHentaiYeast(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(HentaiYeastTag) || item.identifier == HentaiYeastId; }
            catch { return false; }
        }

        public static void UpgradeLegacyHentaiYeast(GameItem bottle)
        {
            if (bottle == null) return;
            var children = bottle.FindAllChildItems();
            if (children == null) return;
            for (int i = 0; i < children.Count; i++)
            {
                var yeast = children[i];
                if (yeast == null || yeast.identifier != HentaiYeastId) continue;
                yeast.EnableTag(HentaiYeastTag, false);
                yeast.identifier = VanillaRedYeastId;
            }
        }

        public static GameItem CreateGoldenYeastReactorItem()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(GoldenYeastReactorId);
                if (item == null) return null;
                item.SetName(GoldenYeastReactorName);
                item.SetValue(GoldenYeastReactorPrice);
                item.SetSprite("", GoldenYeastReactorSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("HOUSEHOLD_GOOD");
                item.itemTypes = types;
                try { WineHelper.InitInfiniteYeast(item, GoldenYeastReactorPower); } catch { }
                try { item.shortDescription = GoldenYeastReactorDescription; } catch { }
                try { item.longDescription = GoldenYeastReactorDescription; } catch { }
                return item;
            }
            catch { return null; }
        }

        public static GameItem CreateAdvancedYeastReactorItem()
        {
            try
            {
                var item = ItemDirectory.CreateEmptyItem(AdvancedYeastReactorId);
                if (item == null) return null;
                item.SetName(AdvancedYeastReactorName);
                item.SetValue(AdvancedYeastReactorPrice);
                item.SetSprite("", AdvancedYeastReactorSpriteKey);
                item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                var types = new Il2CppSystem.Collections.Generic.List<string>();
                types.Add("HOUSEHOLD_GOOD");
                item.itemTypes = types;
                try { WineHelper.InitInfiniteYeast(item, AdvancedYeastReactorPower); } catch { }
                try { ContrabandHelper.InitContrabandItem(item, AdvancedYeastReactorContrabandLevel); } catch { }
                try { item.shortDescription = AdvancedYeastReactorDescription; } catch { }
                try { item.longDescription = AdvancedYeastReactorDescription; } catch { }
                return item;
            }
            catch { return null; }
        }

        // ★ 禁忌酵母反应器 = 克隆合成酵母 + InitInfiniteYeast
        public static GameItem CreateForbiddenYeastReactorItem()
        {
            try
            {
                // 1. 拿原版合成酵母做模板
                GameItem gi = null;
                try { gi = DirectoryMaster.Item(VanillaRedYeastId, false); }
                catch (Exception e) { MelonLogger.Warning("[BE] 拿 wine_yeast_red 模板异常: " + e.Message); }
                if (gi == null)
                {
                    MelonLogger.Warning("[BE] 拿不到 wine_yeast_red 模板");
                    return null;
                }

                // 2. 改外观（不改 identifier，保留它"就是合成酵母"的身份）
                try { gi.SetName(ForbiddenYeastReactorName); } catch { }
                try { gi.SetValue(ForbiddenYeastReactorPrice); } catch { }
                try { gi.SetSprite("", ForbiddenYeastReactorSpriteKey); } catch { }
                try { gi.SetSpriteQuick("", ForbiddenYeastReactorSpriteKey); } catch { }
                try { gi.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build(); } catch { }
                try { gi.shortDescription = ForbiddenYeastReactorDescription; } catch { }
                try { gi.longDescription = ForbiddenYeastReactorDescription; } catch { }

                // 3. 加我们的识别 tag（不改 identifier 的前提下标记"这个是禁忌反应器"）
                try { gi.EnableTag("BE_FORBIDDEN_YEAST_REACTOR", false); } catch { }

                // 4. ★ 关键：加上"无限循环 + 时间压缩"，跟中级/高级酵母反应器一样
                try
                {
                    WineHelper.InitInfiniteYeast(gi, ForbiddenYeastReactorPower);
                    MelonLogger.Msg("[BE] 禁忌酵母反应器 InitInfiniteYeast 成功");
                }
                catch (Exception e) { MelonLogger.Warning("[BE] InitInfiniteYeast 失败: " + e.Message); }

                // 5. 违禁标记
                try { ContrabandHelper.InitContrabandItem(gi, 3); } catch { }

                MelonLogger.Msg("[BE] 禁忌酵母反应器创建完成");
                return gi;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE] CreateForbiddenYeastReactorItem 异常: " + e.Message);
                return null;
            }
        }

        // ★ 通过我们加的 tag 识别，而不是 identifier（因为 identifier 保持 wine_yeast_red）
        public static bool IsForbiddenYeastReactor(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag("BE_FORBIDDEN_YEAST_REACTOR"); } catch { return false; }
        }

        public static GameItem CreateIncubatorItem()
        {
            try
            {
                var furnace = MachineFurnace.Furnace();
                if (furnace != null)
                {
                    furnace.identifier = IncubatorId;
                    furnace.SetName(IncubatorName);
                    furnace.SetValue(IncubatorPrice);
                    furnace.SetSprite("", IncubatorSpriteKey);
                    furnace.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
                    // Incubation is handled by PatchSeedToFruit. Do not also run the
                    // native furnace's nightly processing or power consumption.
                    furnace.onCycleEndSlotItemFunc = null;
                    IncubatorLayout.Configure(furnace);
                    try { furnace.shortDescription = IncubatorDescription; } catch { }
                    try { furnace.longDescription = IncubatorDescription; } catch { }
                    return furnace;
                }
            }
            catch { }
            var item = ItemDirectory.CreateEmptyItem(IncubatorId);
            if (item == null) return null;
            item.SetName(IncubatorName);
            item.SetValue(IncubatorPrice);
            item.SetSprite("", IncubatorSpriteKey);
            item.shape = new GridShapeBuilder(3, 3).SetDataFill(3, 3, 1).Build();
            var types = new Il2CppSystem.Collections.Generic.List<string>();
            types.Add("MACHINE");
            item.itemTypes = types;
            try { MachineryHelper.InitMachinery(item, 5, true); } catch { }
            IncubatorLayout.EnsureEnergyUsage(item);
            try { item.shortDescription = IncubatorDescription; } catch { }
            try { item.longDescription = IncubatorDescription; } catch { }
            return item;
        }

        public static GameItem CreateWinePrinterItem()
        {
            try
            {
                var printer = MachineBottlePrinter.BottlePrinter();
                if (printer != null)
                {
                    printer.identifier = WinePrinterId;
                    printer.SetName(WinePrinterName);
                    printer.SetValue(WinePrinterPrice);
                    printer.SetSprite("", WinePrinterSpriteKey);
                    XIAOWOBrewingBarrel.WinePrinterLayout.Ensure(printer);
                    try { printer.shortDescription = WinePrinterDescription; } catch { }
                    try { printer.longDescription = WinePrinterDescription; } catch { }
                    try { printer.flavorText = ""; } catch { }
                    try { printer.customText = ""; } catch { }
                    return printer;
                }
            }
            catch { }
            return null;
        }

        public static void RefreshMachineDescription(GameItem item)
        {
            if (item == null) return;
            try
            {
                string id = item.identifier ?? "";
                if (id == WinePrinterId) { try { item.shortDescription = WinePrinterDescription; item.longDescription = WinePrinterDescription; } catch { } }
                else if (id == IncubatorId) { try { item.shortDescription = IncubatorDescription; item.longDescription = IncubatorDescription; } catch { } }
            }
            catch { }
        }

        private static (int mortal, int remote, int longing, int afterglow) GetFortySevenSeedOdds()
        {
            int certLevel = Math.Clamp(GetCurrentCertLevel(), 0, 3);
            return SeedOddsCalculator.Calculate(_longingSeedMisses, _afterglowSeedMisses, certLevel);
        }

        public static List<string> PickRandomSeedsForScav(int count)
        {
            var result = new List<string>();
            try
            {
                var rng = new System.Random();
                var commonPool = new List<string>();
                var remotePool = new List<string>();
                var longingPool = new List<string>();
                var afterglowPool = new List<string>();
                for (int i = 0; i < SeedIds.Length; i++)
                {
                    string sid = SeedIds[i];
                    if (sid == "chili_seed" || sid == "rice_seed" || sid == "wheat_seed"
                        || sid == "wormwood_seed" || sid == "sichuan_pepper_seed"
                        || sid == "lemon_seed" || sid == "yellow_grape_seed"
                        || sid == "sugarcane_seed" || sid == "cucumber_seed") commonPool.Add(sid);
                    else if (sid == "lychee_seed" || sid == JasmineSeedId || sid == GreenGrapeSeedId) remotePool.Add(sid);
                    else if (sid == "osmanthus_seed" || sid == "bayberry_seed") longingPool.Add(sid);
                    else if (sid == VoidPlumSeedId || sid == StardustBerrySeedId) afterglowPool.Add(sid);
                }

                for (int slot = 0; slot < count; slot++)
                {
                    var odds = GetFortySevenSeedOdds();
                    int r = rng.Next(10000);
                    List<string> pool;
                    int rarity;
                    if (r < odds.mortal) { pool = commonPool; rarity = 0; }
                    else if (r < odds.mortal + odds.remote) { pool = remotePool; rarity = 1; }
                    else if (r < odds.mortal + odds.remote + odds.longing) { pool = longingPool; rarity = 2; }
                    else { pool = afterglowPool; rarity = 3; }
                    if (pool.Count == 0) continue;
                    result.Add(pool[rng.Next(pool.Count)]);
                    (_longingSeedMisses, _afterglowSeedMisses) =
                        SeedOddsCalculator.Advance(_longingSeedMisses, _afterglowSeedMisses, rarity);
                }
            }
            catch { }
            return result;
        }

        public static void InjectNpcSprite(string key, Sprite sp)
        {
            try
            {
                if (string.IsNullOrEmpty(key) || sp == null) return;
                var dict = SpriteDict.Instance;
                if (dict == null) return;
                if (dict.spriteDictionary == null)
                    dict.spriteDictionary = new Il2CppSystem.Collections.Generic.Dictionary<string, Sprite>();
                dict.spriteDictionary[key] = sp;
            }
            catch { }
        }

        private static void RewriteClonedDialogue(Dialogue dialogue, string name, string text)
        {
            // 保留原版对话对象上的交易回调，只替换玩家实际会看到的台词。
            for (int i = 0; dialogue != null && i < 16; i++)
            {
                dialogue.SetText(name, text);
                dialogue = dialogue.nextDialogue;
            }
        }

        private static void RewriteClonedVisitorDialogues(StoreClient client, string name,
            string greeting, string accepted, string done)
        {
            if (client == null) return;
            RewriteClonedDialogue(client.mainDialogue, name, greeting);
            // Luxury 模板开场白的第二句是原版 NPC 台词；其结束回调为空操作。
            if (client.mainDialogue != null) client.mainDialogue.nextDialogue = null;
            RewriteClonedDialogue(client.acceptDealDialogue, name, accepted);
            RewriteClonedDialogue(client.allDoneDialogue, name, done);
            RewriteClonedDialogue(client.repeatDialogue, name, greeting);
            RewriteClonedDialogue(client.placedWrongItemWhenSellingToDialogue, name, done);
            RewriteClonedDialogue(client.placeRightItemWhenSellingToDialogue, name, accepted);
            RewriteClonedDialogue(client.interogationDialogue, name, done);
            RewriteClonedDialogue(client.glasseDialogue, name, done);
        }

        public static StoreClient CreateWeeklyTrader()
        {
            try
            {
                var c = StoreClientList.CreateUpperLowerVisitorLuxury();
                if (c == null) return null;
                c.identifier = WeeklyTraderId;
                XIAOWOBrewingBarrel.BrewingNpcDialogueColor.Apply(c);
                c.displayName = WeeklyTraderName;
                c.clientFaction = XIAOWOBrewingBarrel.OuterRingFaction.Id;
                try { c.clientIntent = StoreClient.ClientIntent.BUY; } catch { }
                try { c.isIntroduced = true; } catch { }
                // ★ 面板上不再显示「奢侈品」，改成烟酒标签
                try
                {
                    if (c.clientBuyingTagList == null)
                        c.clientBuyingTagList = new Il2CppSystem.Collections.Generic.List<string>();
                    c.clientBuyingTagList.Clear();
                    // SUBSTANCE 在游戏本地化里是「烟酒类」
                    c.clientBuyingTagList.Add("SUBSTANCE");
                }
                catch (Exception e) { MelonLogger.Warning("[BE-NINI] 改收购标签失败: " + e.Message); }

                if (PngBytes.ContainsKey(WeeklyTraderSpriteKey))
                {
                    if (!Generated) GenerateAllSprites();
                    Sprite sp = null;
                    if (BEState.GeneratedSprites.TryGetValue(WeeklyTraderSpriteKey, out sp) && sp != null)
                        InjectNpcSprite(WeeklyTraderSpriteKey, sp);
                    c.spriteName = WeeklyTraderSpriteKey;
                    try
                    {
                        if (c.possibleSprites == null) c.possibleSprites = new Il2CppSystem.Collections.Generic.List<string>();
                        c.possibleSprites.Clear();
                        c.possibleSprites.Add(WeeklyTraderSpriteKey);
                    }
                    catch { }
                }
                RewriteClonedVisitorDialogues(c, WeeklyTraderName, WeeklyTraderGreeting,
                    Lang.T("烟酒我收下了。", "I'll take the alcohol and tobacco."),
                    Lang.T("今天就到这儿吧。", "That will do for today."));
                int budget = XIAOWOBrewingBarrel.OuterRingFaction.NiniBudget;
                c.clientCash = budget;
                c.useClientBudget = true;
                try { c.SetBudget(budget); } catch { }
                return c;
            }
            catch { return null; }
        }

        public static StoreClient CreateSeedTrader47()
        {
            try
            {
                // A caller may build the first-day visitor before BeginDay's fallback.
                EnsureSpriteAssetsReady();
                var c = StoreClientList.CreateUpperLowerVisitorLuxury();
                if (c == null) return null;
                c.identifier = SeedTrader47Id;
                XIAOWOBrewingBarrel.BrewingNpcDialogueColor.Apply(c);
                c.displayName = SeedTrader47Name;
                c.clientFaction = XIAOWOBrewingBarrel.OuterRingFaction.Id;
                try { c.clientIntent = StoreClient.ClientIntent.SELL; } catch { }
                try { c.isIntroduced = true; } catch { }

                if (PngBytes.ContainsKey(SeedTrader47SpriteKey))
                {
                    if (!Generated) GenerateAllSprites();
                    Sprite sp = null;
                    if (BEState.GeneratedSprites.TryGetValue(SeedTrader47SpriteKey, out sp) && sp != null)
                        InjectNpcSprite(SeedTrader47SpriteKey, sp);
                    c.spriteName = SeedTrader47SpriteKey;
                    try
                    {
                        if (c.possibleSprites == null) c.possibleSprites = new Il2CppSystem.Collections.Generic.List<string>();
                        c.possibleSprites.Clear();
                        c.possibleSprites.Add(SeedTrader47SpriteKey);
                    }
                    catch { }
                }
                RewriteClonedVisitorDialogues(c, SeedTrader47Name, SeedTrader47Greeting,
                    Lang.T("成交。", "Deal."), SeedTrader47DoneDialogue);

                return c;
            }
            catch { return null; }
        }



        // ★ 公共补货方法：任何时候调用都能上架
        public static void StockSeeds47Now()
        {

            var store = PlayerStore.Instance;
            if (store == null) return;
            EnsureSpriteAssetsReady();

            int count = GetCurrentScavSeedCount();
            var picks = PickRandomSeedsForScav(count);
            var logList = new List<string>();
            for (int k = 0; k < picks.Count; k++)
            {
                string seedId = picks[k];
                var seed = DirectoryMaster.Item(seedId, false);
                if (seed == null)
                    for (int i = 0; i < SeedIds.Length; i++)
                        if (SeedIds[i] == seedId)
                        {
                            seed = CreateSeedItem(seedId, SeedNames[i], SeedSpriteKeys[i]);
                            break;
                        }
                if (seed == null) continue;
                Add47StockToTable(seed);
                logList.Add(seedId);
            }
            MelonLogger.Msg("[BE-NPC] 四十七上架 " + logList.Count + " 个种子: " + string.Join(", ", logList));

            try
            {
                var ownedRecipes = new HashSet<string>();
                var allRecipes = store.FindAllItem(true);
                if (allRecipes != null)
                    for (int i = 0; i < allRecipes.Count; i++)
                    {
                        var item = allRecipes[i];
                        if (item != null && IsOurRecipe(item.identifier) && IsOwnedByPlayer(item))
                            ownedRecipes.Add(item.identifier);
                    }
                var missingRecipes = new List<int>();
                for (int i = 0; i < RecipeIds.Length; i++)
                    if (!ownedRecipes.Contains(RecipeIds[i])) missingRecipes.Add(i);
                if (missingRecipes.Count > 0)
                {
                    var idx = missingRecipes[new System.Random().Next(missingRecipes.Count)];
                    var recipe = CreateRecipeItem(idx);
                    if (recipe != null)
                    {
                        Add47StockToTable(recipe);
                        MelonLogger.Msg("[BE-NPC] 四十七上架未拥有配方: " + RecipeNames[idx]);
                    }
                }
                else MelonLogger.Msg("[BE-NPC] 玩家已拥有全部酿酒配方，四十七不再上架配方");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-NPC] 上架配方异常: " + e.Message); }

            try
            {
                int manualCount = 0;
                var all2 = store.FindAllItem(true);
                if (all2 != null)
                    for (int i = 0; i < all2.Count; i++)
                    {
                        var it = all2[i];
                        if (it == null) continue;
                        string id = it.identifier ?? "";
                        if (IsBrewingManual(it)) manualCount++;
                    }
                if (manualCount == 0)
                {
                    var x = DirectoryMaster.Item(BrewingManualId, false);
                    if (x != null) { Add47StockToTable(x); MelonLogger.Msg("[BE-NPC] 四十七上架酿酒手册"); }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-NPC] 上架手册异常: " + e.Message); }

        }

        // ★★★ 统一的 sprite 查找：支持完整匹配 / 去路径匹配 / be_ 前缀匹配 ★★★
        public static Sprite FindOurSprite(string nameOrPath)
        {
            if (string.IsNullOrEmpty(nameOrPath)) return null;
            // Starter-item sprites can be ready before the full sprite batch is generated.
            // The dictionary itself records which individual sprites are available.

            Sprite sp;

            // 1. 直接匹配
            if (BEState.GeneratedSprites.TryGetValue(nameOrPath, out sp) && sp != null)
                return sp;

            // 2. 去掉路径部分再匹配（"Items/xxx/be_yyy" → "be_yyy"）
            int slashIdx = nameOrPath.LastIndexOf('/');
            if (slashIdx >= 0 && slashIdx < nameOrPath.Length - 1)
            {
                string shortName = nameOrPath.Substring(slashIdx + 1);
                if (BEState.GeneratedSprites.TryGetValue(shortName, out sp) && sp != null)
                    return sp;
            }

            // 3. 去掉路径 + 后缀（"xxx/be_yyy.png" → "be_yyy"）
            int dotIdx = nameOrPath.LastIndexOf('.');
            if (dotIdx > slashIdx)
            {
                string shortName2 = nameOrPath.Substring(slashIdx + 1, dotIdx - slashIdx - 1);
                if (BEState.GeneratedSprites.TryGetValue(shortName2, out sp) && sp != null)
                    return sp;
            }

            return null;
        }


        public static bool IsSpriteAlivePublic(Sprite sp) { return IsSpriteAlive(sp); }

        public static void RegenerateAllSprites()
        {
            try { Generated = false; GenerateAllSprites(); }
            catch { }
        }

        public static void GenerateAllSprites()
        {
            if (Generated) return;
            Generated = true;

            for (int i = 0; i < SeedSpriteKeys.Length; i++)
                MakeSpriteIndependent(SeedSpriteKeys[i], WORLD_UNITS_PER_CELL);
            for (int i = 0; i < FruitSpriteKeys.Length; i++)
                MakeSpriteIndependent(FruitSpriteKeys[i], WORLD_UNITS_PER_CELL * 2);
            for (int i = 0; i < WineSpriteKeys.Length; i++)
                MakeSpriteIndependent(WineSpriteKeys[i], WORLD_UNITS_PER_CELL * WineWorldWidthCells);

            MakeSpriteIndependent(IncubatorSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(WinePrinterSpriteKey, WORLD_UNITS_PER_CELL * 3);
            for (int i = 0; i < CertSpriteKeys.Length; i++)
                MakeSpriteIndependent(CertSpriteKeys[i], WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(GoldenYeastReactorSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(AdvancedYeastReactorSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(ForbiddenYeastReactorSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(BrewerySignSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(NiniCardSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MyPerfumeSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(FreedomPerfumeSpriteKey, WORLD_UNITS_PER_CELL * 2);

            MakeSpriteIndependent(BrewingManualSpriteKey, WORLD_UNITS_PER_CELL * 2);
            // ★ 新增：小暗格贴图
            MakeSpriteIndependent(CompactSmugglerBaySpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(ClearWaterTankSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(XuxuWarmCupSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(XuxuSweetCupSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(Love47CupSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(QingyunFilterSpriteKey, WORLD_UNITS_PER_CELL * 1);
            MakeSpriteIndependent(NingluFilterSpriteKey, WORLD_UNITS_PER_CELL * 1);
            MakeSpriteIndependent(YingfeiFilterSpriteKey, WORLD_UNITS_PER_CELL * 1);
            foreach (var key in XIAOWOBrewingBarrel.XuxuWaterPurifier.SpriteKeys)
                MakeSpriteIndependent(key, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(PremiumBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(AfterglowBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            // ★ 中秋节系列
            MakeSpriteIndependent(MidautumnMoonFanSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnMoonLampSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnJadeRabbitSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnLanternSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnMooncakeBoxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnBagSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(SakuraBagSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(CyberBagSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(LaolishiBagSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(CatYouyouBagSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(XuxuBatterySpriteKey, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuBatterySpriteKey4, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuBatterySpriteKey3, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuBatterySpriteKey2, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuBatterySpriteKey1, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuBatterySpriteKey0, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey4, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey3, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey2, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey1, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuMidBatterySpriteKey0, WORLD_UNITS_PER_CELL);

            MakeSpriteIndependent(XuxuHighBatterySpriteKey, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuHighBatterySpriteKey4, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuHighBatterySpriteKey3, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuHighBatterySpriteKey2, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuHighBatterySpriteKey1, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(XuxuHighBatterySpriteKey0, WORLD_UNITS_PER_CELL);

            MakeSpriteIndependent(WurenMooncakeSpriteKey, WORLD_UNITS_PER_CELL * 3);
            MakeSpriteIndependent(MidautumnBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(LowBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(WineBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(ContainerBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(FigurineBlindboxSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(CassetteBundleSpriteKey, WORLD_UNITS_PER_CELL * 2);
            for (int i = 0; i < XIAOWOBrewingBarrel.CustomCassettes.SpriteKeys.Length; i++)
                MakeSpriteIndependent(XIAOWOBrewingBarrel.CustomCassettes.SpriteKeys[i], WORLD_UNITS_PER_CELL * 2);
            for (int i = 0; i < FigurineSpriteKeys.Length; i++)
                MakeSpriteIndependent(FigurineSpriteKeys[i], WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnMoonOsmanthusSpriteKey, WORLD_UNITS_PER_CELL * 2);
            MakeSpriteIndependent(MidautumnOsmanthusWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(NeonDreamWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            // ★ 收藏酒系列
            MakeSpriteIndependent(ShuangqingWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(NanfengWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(FeimeiWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(NeonIllusionWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(IdealEndWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(LuofuWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(MagicPigWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(SeaBlueWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(NianHeshiWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);
            MakeSpriteIndependent(HentaiYeastSpriteKey, WORLD_UNITS_PER_CELL);
            MakeSpriteIndependent(RainbowRecipeWineSpriteKey, WORLD_UNITS_PER_CELL * WineWorldWidthCells);

            for (int i = 0; i < RecipeSpriteKeys.Length; i++)
                MakeSpriteIndependent(RecipeSpriteKeys[i], WORLD_UNITS_PER_CELL * 2);

            if (PngBytes.ContainsKey(WeeklyTraderSpriteKey))
                MakeSpriteIndependent(WeeklyTraderSpriteKey, WORLD_UNITS_PER_CELL * 3, 100f, new Vector2(0.5f, 0.5f));
            if (PngBytes.ContainsKey(SeedTrader47SpriteKey))
                MakeSpriteIndependent(SeedTrader47SpriteKey, WORLD_UNITS_PER_CELL * 3, 100f, new Vector2(0.5f, 0.5f));


            // ★ 把所有 sprite 直接注入游戏 sprite 缓存
            MelonLogger.Msg("[BE-DIAG] 完成，共 " + BEState.GeneratedSprites.Count + " 个 sprite");
        }

        private static bool IsSpriteAlive(Sprite sp)
        {
            try
            {

                if (sp == null) return false;
                var tex = sp.texture;
                if (tex == null) return false;
                return tex.width > 0;
            }
            catch { return false; }
        }

        private static void MakeSpriteIndependent(string key, float worldWidth, float ppuOverride = 0f, Vector2? pivotOverride = null)
        {
            Sprite existing;
            if (BEState.GeneratedSprites.TryGetValue(key, out existing) && existing != null && IsSpriteAlive(existing)) return;
            if (!PngBytes.TryGetValue(key, out var pngBytes)) return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, pngBytes)) return;

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.anisoLevel = 0;

            try
            {
                var src = tex.GetPixels32();
                int len = src.Length;
                var buf = new Color32[len];
                for (int i = 0; i < len; i++) buf[i] = src[i];
                bool changed = false;
                for (int i = 0; i < len; i++)
                    if (buf[i].a == 0 && (buf[i].r != 0 || buf[i].g != 0 || buf[i].b != 0))
                    { buf[i] = new Color32(0, 0, 0, 0); changed = true; }
                if (changed)
                {
                    var dst = new Il2CppStructArray<Color32>(len);
                    for (int i = 0; i < len; i++) dst[i] = buf[i];
                    tex.SetPixels32(dst);
                }
            }
            catch { }

            tex.Apply(false, false);
            try { tex.hideFlags = HideFlags.DontUnloadUnusedAsset; UnityEngine.Object.DontDestroyOnLoad(tex); } catch { }

            int w = tex.width, h = tex.height;
            float ppu = ppuOverride > 0f ? ppuOverride : (w / worldWidth);
            Vector2 pivot = pivotOverride.HasValue ? pivotOverride.Value : new Vector2(0.5f, 0f);

            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
            try { sp.name = key; } catch { }   // ★ 关键：给 sprite 设置名字
            try { sp.hideFlags = HideFlags.DontUnloadUnusedAsset; } catch { }

            BEState.KeptTextures.Add(tex);
            BEState.GeneratedSprites[key] = sp;
        }

        // ★ 把一个 sprite 注册到另一个 key 上（别名）
        private static void RegisterSpriteAlias(string aliasKey, string sourceKey)
        {
            Sprite src;
            if (BEState.GeneratedSprites.TryGetValue(sourceKey, out src) && src != null)
                BEState.GeneratedSprites[aliasKey] = src;
        }

        // ============ 判断 ============
        public static int GetWineIndexById(string wineId)
        {
            for (int j = 0; j < WineIds.Length; j++) if (wineId == WineIds[j]) return j;
            return -1;
        }

        public static bool IsOurSeed(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int j = 0; j < SeedIds.Length; j++) if (id == SeedIds[j]) return true;
            return false;
        }

        public static bool IsOurFruit(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int j = 0; j < FruitIds.Length; j++) if (id == FruitIds[j]) return true;
            return false;
        }

        public static bool IsOurWineName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int j = 0; j < WineNamesZH.Length; j++)
                if (name == WineNamesZH[j] || name == WineNamesEN[j]) return true;
            return false;
        }

        public static bool HasOurWineTag(GameItem bottle)
        {
            if (bottle == null) return false;

            // 第 1 层：我们的 tag（最精确）
            try
            {
                for (int i = 0; i < WineIds.Length; i++)
                    if (bottle.IsTag(TagPrefixWine + WineIds[i])) return true;
            }
            catch { }

            // 第 2 层：spritePath（不受语言影响，最稳定）
            try
            {
                string sp = bottle.spritePath ?? "";
                if (!string.IsNullOrEmpty(sp))
                {
                    for (int i = 0; i < WineSpriteKeys.Length; i++)
                        if (sp == WineSpriteKeys[i]) return true;
                }
            }
            catch { }

            // 第 3 层：名字（同时检查中英文，避免语言时序问题）
            try
            {
                string n = bottle.name ?? "";
                if (string.IsNullOrEmpty(n)) return false;
                for (int i = 0; i < WineNames.Length; i++)
                {
                    string nameEN = WineNames[i];  // 当前语言的名字
                    if (n == nameEN) return true;
                }
                // 再检查另一语言的版本（不管当前 Lang 是啥）
                // 用一个静态字典缓存两个语言的名字
                for (int i = 0; i < WineIds.Length; i++)
                {
                    string zh, en;
                    if (TryGetWineNames(i, out zh, out en))
                    {
                        if (n == zh || n == en) return true;
                    }
                }
            }
            catch { }

            return false;
        }

        // ★ 直接查两个数组，不再硬编码
        private static bool TryGetWineNames(int idx, out string zh, out string en)
        {
            zh = null; en = null;
            if (idx < 0 || idx >= WineNamesZH.Length || idx >= WineNamesEN.Length) return false;
            zh = WineNamesZH[idx];
            en = WineNamesEN[idx];
            return true;
        }

        public static bool IsOurCert(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int j = 0; j < CertIds.Length; j++) if (id == CertIds[j]) return true;
            return false;
        }

        public static bool IsOurRecipe(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int j = 0; j < RecipeIds.Length; j++) if (id == RecipeIds[j]) return true;
            return false;
        }

        public static bool IsOurBrewingDocument(string id) =>
            IsOurRecipe(id) || XIAOWOBrewingBarrel.MachineGuides.Contains(id);

        // ★ 通过 tag 识别酿酒手册（因为 identifier 是 storage_bay）
        public static bool IsBrewingManual(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(BrewingManualTag); } catch { return false; }
        }

        public static bool IsCompactSmugglerBay(GameItem item)
        {
            if (item == null) return false;
            try { return item.IsTag(CompactSmugglerBayTag); } catch { return false; }
        }


        public static bool IsFoodBuyer(StoreClient client)
        {
            if (client == null) return false;
            try
            {
                var tags = client.clientBuyingTagList;
                if (tags != null)
                    for (int i = 0; i < tags.Count; i++)
                    {
                        string t = tags[i];
                        if (t == "FOOD" || t == "TREAT" || t == "BREVAGE" || t == "INGREDIENT") return true;
                    }
            }
            catch { }
            return false;
        }

        public static string GetFruitIdForSeed(string seedId)
        {
            for (int j = 0; j < SeedIds.Length; j++)
                if (seedId == SeedIds[j] && j < FruitIds.Length) return FruitIds[j];
            return null;
        }

        public static int GetCurrentCertLevel()
        {
            int level = 0;
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return 0;
                var allItems = store.FindAllItem(true);
                if (allItems == null) return 0;
                for (int i = 0; i < allItems.Count; i++)
                {
                    var it = allItems[i];
                    if (it == null) continue;
                    string id = it.identifier ?? "";
                    if (id == HighCertId) return 3;
                    if (id == MidCertId && level < 2) level = 2;
                    if (id == BasicCertId && level < 1) level = 1;
                }
            }
            catch { }
            return level;
        }

        public static void RefreshOwnedCertificateDescriptions()
        {
            var owned = PlayerStore.Instance?.FindAllItem(true);
            if (owned == null) return;
            for (int i = 0; i < owned.Count; i++)
            {
                try
                {
                    var item = owned[i];
                    if (item == null) continue;
                    for (int cert = 0; cert < CertIds.Length; cert++)
                    {
                        if (item.identifier != CertIds[cert]) continue;
                        var description = CertDescriptions[cert];
                        item.shortDescription = description;
                        item.longDescription = description;
                        break;
                    }
                }
                catch { }
            }
        }

        public static int GetCurrentScavSeedCount()
        {
            int count = ScavSeedCountBase;
            try
            {
                int lvl = GetCurrentCertLevel();
                if (lvl >= 3) count = ScavSeedCountWithHighCert;
                else if (lvl >= 2) count = ScavSeedCountWithMidCert;
                else if (lvl >= 1) count = ScavSeedCountWithCert;
            }
            catch { }
            if (BrewingTraits.Has(BrewingTraits.SeedAversionId)) count = Math.Max(0, count - 3);
            count = Math.Max(0, count + XIAOWOBrewingBarrel.OuterRingFaction.SeedCountModifier);
            return count;
        }

        public static int GetCurrentIncubatorQuota()
        {
            int quota = IncubatorQuotaBase;
            try
            {
                int lvl = GetCurrentCertLevel();
                if (lvl >= 3) quota = IncubatorQuotaWithHighCert;
                else if (lvl >= 2) quota = IncubatorQuotaWithMidCert;
                else if (lvl >= 1) quota = IncubatorQuotaWithCert;
            }
            catch { }
            return quota;
        }

        public static void SetTraderDialogue(StoreClient trader, string text, string tag)
        {
            if (trader == null || string.IsNullOrEmpty(text)) return;
            try
            {
                // ★ 强制用当前语言的泥泥名，不用可能残留旧语言的 d.title
                string title = WeeklyTraderName;

                var d = trader.acceptDealDialogue;
                if (d != null) d.SetText(title, text);
                try { trader.acceptDealDialoguePlayed = false; } catch { }

                var ad = trader.allDoneDialogue;
                if (ad != null) ad.SetText(title, text);
            }
            catch { }
        }

        public static bool GiveCertToPlayer(string certId, string certName, string certSpriteKey)
        {
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return false;
                var cert = DirectoryMaster.Item(certId, false);
                if (cert == null) return false;
                try { cert.SetSprite("", certSpriteKey); } catch { }
                try { cert.SetSpriteQuick("", certSpriteKey); } catch { }
                store.AddDirectSellingItemToTable(cert, true);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-CERT] 发放 " + certName + " 失败: " + e.Message);
                return false;
            }
        }

        public static bool HasBrewerySign(bool allowSavedShowcase = false)
        {
            try
            {
                if (ShowcaseHelper.IsShowcaseContainItemById(BrewerySignId)) return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-DIAG] HasBrewerySign 异常: " + e.Message);
            }
            if (allowSavedShowcase)
                try
                {
                    var saved = PlayerStore.Instance?.showcaseInvJSON;
                    if (!string.IsNullOrEmpty(saved) && saved.Contains(BrewerySignId)) return true;
                }
                catch { }
            return false;
        }

        // ============ 酒 tag ============
        private static void ApplyBottleTags(GameItem bottle, string wineId, int waterScore)
        {
            try
            {
                if (bottle == null || string.IsNullOrEmpty(wineId)) return;
                for (int i = 0; i < WineIds.Length; i++)
                    try { bottle.DisableTag(TagPrefixWine + WineIds[i], false); } catch { }
                for (int i = 0; i <= 9; i++)
                    try { bottle.DisableTag(TagPrefixWater + i, false); } catch { }
                if (waterScore < 0) waterScore = 0;
                if (waterScore > 9) waterScore = 9;
                try { bottle.EnableTag(TagPrefixWine + wineId, false); } catch { }
                try { bottle.EnableTag(TagPrefixWater + waterScore, false); } catch { }
            }
            catch { }
        }

        private static void RemoveBottleTags(GameItem bottle)
        {
            try
            {
                if (bottle == null) return;
                for (int i = 0; i < WineIds.Length; i++)
                    try { bottle.DisableTag(TagPrefixWine + WineIds[i], false); } catch { }
                for (int i = 0; i <= 9; i++)
                    try { bottle.DisableTag(TagPrefixWater + i, false); } catch { }
            }
            catch { }
        }

        private static bool TryReadBottleTags(GameItem bottle, out string wineId, out int waterScore)
        {
            wineId = null; waterScore = -1;
            try
            {
                if (bottle == null) return false;
                for (int i = 0; i < WineIds.Length; i++)
                    try { if (bottle.IsTag(TagPrefixWine + WineIds[i])) { wineId = WineIds[i]; break; } } catch { }
                if (string.IsNullOrEmpty(wineId)) return false;
                for (int i = 9; i >= 0; i--)
                    try { if (bottle.IsTag(TagPrefixWater + i)) { waterScore = i; break; } } catch { }
                if (waterScore < 0) waterScore = 0;
                return true;
            }
            catch { return false; }
        }

        public static void MarkBottle(GameItem bottle, string wineId, int waterScore) { MarkBottle(bottle, wineId, waterScore, 0); }

        public static void MarkBottle(GameItem bottle, string wineId, int waterScore, int contrabandLevel)
        {
            try
            {
                if (bottle == null || string.IsNullOrEmpty(wineId)) return;
                int uid = bottle.uniqueId;
                int hc = bottle.GetHashCode();
                if (waterScore < 0) waterScore = 0;
                ApplyBottleTags(bottle, wineId, waterScore);
                if (!BottleMarks.ContainsKey(uid)) BottleMarks[uid] = new List<WineMarkEntry>();
                var list = BottleMarks[uid];
                WineMarkEntry existing = null;
                for (int i = 0; i < list.Count; i++) if (list[i].hashcode == hc) { existing = list[i]; break; }
                if (existing != null)
                {
                    existing.wineId = wineId;
                    existing.waterScore = waterScore;
                    existing.contrabandLevel = contrabandLevel;
                }
                else
                    list.Add(new WineMarkEntry { hashcode = hc, wineId = wineId, waterScore = waterScore, contrabandLevel = contrabandLevel });
                SaveData();
            }
            catch { }
        }

        public static bool ReadBottleMarkFull(GameItem bottle, out string wineId, out int waterScore)
        {
            int contraLvl;
            return ReadBottleMarkFull(bottle, out wineId, out waterScore, out contraLvl);
        }

        public static bool ReadBottleMarkFull(GameItem bottle, out string wineId, out int waterScore, out int contrabandLevel)
        {
            wineId = null; waterScore = -1; contrabandLevel = 0;
            try
            {
                if (bottle == null) return false;
                int uid = bottle.uniqueId;
                int hc = bottle.GetHashCode();
                List<WineMarkEntry> list;
                if (BottleMarks.TryGetValue(uid, out list) && list.Count > 0)
                {
                    WineMarkEntry match = null;
                    for (int i = 0; i < list.Count; i++) if (list[i].hashcode == hc) { match = list[i]; break; }
                    if (match == null) match = list[0];
                    if (match != null)
                    {
                        wineId = match.wineId;
                        waterScore = match.waterScore;
                        contrabandLevel = match.contrabandLevel;
                        return true;
                    }
                }
                return TryReadBottleTags(bottle, out wineId, out waterScore);
            }
            catch { return false; }
        }

        public static string ReadBottleMark(GameItem bottle)
        {
            string wineId; int score;
            if (ReadBottleMarkFull(bottle, out wineId, out score)) return wineId;
            return null;
        }

        public static void RemoveBottleMark(GameItem bottle)
        {
            try
            {
                if (bottle == null) return;
                RemoveBottleTags(bottle);
                int uid = bottle.uniqueId;
                int hc = bottle.GetHashCode();
                List<WineMarkEntry> list;
                if (!BottleMarks.TryGetValue(uid, out list)) return;
                WineMarkEntry match = null;
                for (int i = 0; i < list.Count; i++) if (list[i].hashcode == hc) { match = list[i]; break; }
                if (match != null) list.Remove(match);
                else if (list.Count > 0) list.RemoveAt(0);
                if (list.Count == 0) BottleMarks.Remove(uid);
                SaveData();
            }
            catch { }
        }

        public static int ScoreToTier(int waterScore)
        {
            int totalScore = BaseWineScore + waterScore;
            if (totalScore >= 6) return TierNectar;
            if (totalScore >= 5) return TierCellar;
            return TierMass;
        }



        // ★ 缓存空委托，避免每次新建导致 GC 回收后 Il2Cpp 引用悬空
        private static Func<GameItem, Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>> _emptySelfDelegate;
        private static Func<GameItem, GameInventory, SlotMarker, GridShape, TagSystem, GameItem,
                            Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>> _emptyParentDelegate;

        public static void ClearSpriteModifierCallbacks(GameItem item)
        {
            if (item == null) return;
            try
            {
                if (_emptySelfDelegate == null)
                    _emptySelfDelegate = (gi) => new Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>();
                item.getItemSpriteModifierForItemFunc = _emptySelfDelegate;
            }
            catch { }
            try
            {
                if (_emptyParentDelegate == null)
                    _emptyParentDelegate = (gi, inv, slot, shape, ts, p) =>
                        new Il2CppSystem.Collections.Generic.List<ItemSpriteModifier>();
                item.parentItemSpriteModifierForItemFunc = _emptyParentDelegate;
            }
            catch { }
        }

        public static int GetWineBasePriceOverrideByName(string wineName)
        {
            if (string.IsNullOrEmpty(wineName)) return -1;
            for (int i = 0; i < WineIds.Length; i++)
            {
                string zh, en;
                if (!TryGetWineNames(i, out zh, out en)) continue;
                if (wineName != zh && wineName != en) continue;   // ★ 双语都接受
                int price;
                if (WineBasePriceOverride.TryGetValue(WineIds[i], out price)) return price;
                return -1;
            }
            return -1;
        }

        public static void RefreshOurWineBasePrice(GameItem bottle)
        {
            try
            {
                if (bottle == null) return;
                int overridePrice = GetWineBasePriceOverrideByName(bottle.name);
                if (overridePrice < 0) return;
                long previousBase = -1;
                try { previousBase = bottle.unitBaseValue; } catch { }
                try { bottle.unitBaseValue = overridePrice; } catch { }
                long curVal = -1;
                try { curVal = bottle.unitValue; } catch { }
                // Existing unaged bottles from v2.5.60 still carry the old 444 value.
                if (overridePrice == NianHeshiWinePrice && previousBase == 444 && curVal == 444)
                {
                    try { if (AgableHelper.GetAge(bottle) == 0) bottle.unitValue = overridePrice; } catch { }
                    return;
                }
                // 旧版杂酿沿用了空酒瓶的 100 基础价值；只修正未陈化的旧瓶。
                if (overridePrice == 80 && curVal == 100)
                {
                    try
                    {
                        if (AgableHelper.GetAge(bottle) == 0) bottle.unitValue = 80;
                    }
                    catch { }
                    return;
                }
                if (curVal >= 0 && curVal < overridePrice)
                    try { bottle.unitValue = overridePrice; } catch { }
            }
            catch { }
        }

        public static bool ApplyWineReplacement(GameItem bottle, string wineId, string source)
        {
            try
            {
                int idx = GetWineIndexById(wineId);
                if (idx < 0) return false;
                if (IsOurWineName(bottle.name ?? "")) return false;

                string wineIdFromMark; int waterScore; int contraLvl;
                ReadBottleMarkFull(bottle, out wineIdFromMark, out waterScore, out contraLvl);
                if (waterScore < 0) waterScore = 0;

                try
                {
                    var kids = bottle.FindAllChildItems();
                    if (kids != null && kids.Count > 0)
                        for (int i = 0; i < kids.Count; i++)
                            try { kids[i].Destroy(); } catch { }
                }
                catch { }

                ClearSpriteModifierCallbacks(bottle);

                try { bottle.SetName(WineNames[idx]); } catch { return false; }
                try { bottle.shortDescription = WineDescriptions[idx]; } catch { }
                try { bottle.longDescription = WineDescriptions[idx]; } catch { }

                string key = WineSpriteKeys[idx];
                try { bottle.SetSprite("", key); } catch { }
                try { bottle.SetSpriteQuick("", key); } catch { }

                try { AgableHelper.InitAgable(bottle, WineAgeValueModPerDay); } catch { }

                try
                {
                    var types = bottle.itemTypes;
                    if (types == null) { types = new Il2CppSystem.Collections.Generic.List<string>(); bottle.itemTypes = types; }
                    bool hasLuxury = false, hasAlcohol = false, hasSubstance = false;
                    for (int i = 0; i < types.Count; i++)
                    {
                        string t = types[i];
                        if (t == "LUXURY_ITEM") hasLuxury = true;
                        else if (t == "ALCOHOL") hasAlcohol = true;
                        else if (t == "SUBSTANCE") hasSubstance = true;
                    }
                    if (!hasLuxury) types.Add("LUXURY_ITEM");
                    if (!hasAlcohol) types.Add("ALCOHOL");
                    if (!hasSubstance) types.Add("SUBSTANCE");
                }
                catch { }

                int overridePrice;
                if (WineBasePriceOverride.TryGetValue(wineId, out overridePrice))
                {
                    try { bottle.unitBaseValue = overridePrice; } catch { }
                    if (wineId == MiscWineId || wineId == NianHeshiWineId)
                        try { bottle.unitValue = overridePrice; } catch { }
                    long curVal = -1;
                    try { curVal = bottle.unitValue; } catch { }
                    if (curVal >= 0 && curVal < overridePrice)
                        try { bottle.unitValue = overridePrice; } catch { }
                }

                if (contraLvl > 0)
                    try { ContrabandHelper.InitContrabandItem(bottle, contraLvl); } catch { }
                // ★ 不删标记：防止别的 mod 改回名字后我们救不回来
                //RemoveBottleMark(bottle);
                RemoveBottleMark(bottle);
                return true;
            }
            catch { return false; }
        }

        public static int SweepAllWineBottles(string source)
        {
            int replaced = 0;
            try
            {
                var store = PlayerStore.Instance;
                if (store == null) return 0;
                var allItems = store.FindAllItem(true);
                if (allItems == null) return 0;

                for (int i = 0; i < allItems.Count; i++)
                {
                    var it = allItems[i];
                    if (it == null) continue;
                    if ((it.identifier ?? "") != "wine_bottle") continue;



                    RefreshOurWineBasePrice(it);
                    string wineId = ReadBottleMark(it);
                    if (string.IsNullOrEmpty(wineId)) continue;
                    bool isFinished = false;
                    try { isFinished = WineHelper.IsFinishedWine(it); } catch { }
                    if (!isFinished) continue;
                    if (ApplyWineReplacement(it, wineId, source)) replaced++;
                }
                if (replaced > 0)
                    MelonLogger.Msg("[BE-WINE] " + source + " 共刷新 " + replaced + " 瓶果酒");

            }
            catch { }
            return replaced;
        }

        private static bool IsOwnedByPlayer(GameItem item)
        {
            try { if (item == null) return false; return item.IsTag(OwnedTag); }
            catch { return false; }
        }
        public static bool IsOwnedByPlayerPublic(GameItem item)
        {
            return IsOwnedByPlayer(item);
        }

        private static bool IsOurStorageContainer(GameItem item)
        {
            if (item == null) return false;
            try
            {
                if (item.IsTag(CompactSmugglerBayTag) || item.IsTag(MultiStorageTag)
                    || item.identifier == CompactSmugglerBayId || item.identifier == MultiStorageId)
                    return true;
                // Older multi-storage saves were cloned from smuggler_bay_mod
                // before the dedicated tag was added. Recognize their saved art.
                if (item.identifier != SmugglerBayModId) return false;
                string sprite = item.spritePath ?? "";
                if (sprite == CompactSmugglerBaySpriteKey || sprite == MultiStorageSpriteKey) return true;
                for (int i = 0; i < MultiStorageVariantSpriteKeys.Length; i++)
                    if (sprite == MultiStorageVariantSpriteKeys[i]) return true;
                string name = item.name ?? "";
                return name == "多功能收纳仓" || name == "Multi-Storage Unit"
                    || name == "四十七「小」暗格" || name == "Forty-Seven's \"Compact\" Stash";
            }
            catch { return false; }
        }

        public static void RefreshOurStoragePrice(GameItem item)
        {
            if (item == null) return;
            try
            {
                string id = item.identifier ?? "";
                if (id != CompactSmugglerBayId && id != MultiStorageId && id != SmugglerBayModId) return;
                if (!IsOurStorageContainer(item)) return;
                string sprite = item.spritePath ?? "";
                bool compact = id == CompactSmugglerBayId || item.IsTag(CompactSmugglerBayTag)
                    || sprite == CompactSmugglerBaySpriteKey
                    || item.name == "四十七「小」暗格"
                    || item.name == "Forty-Seven's \"Compact\" Stash";
                int price = compact ? CompactSmugglerBayPrice : MultiStoragePrice;
                if (item.unitValue != price) item.SetValue(price);
            }
            catch { }
        }

        public static void RefreshOwnedStoragePrices()
        {
            try
            {
                var all = PlayerStore.Instance?.FindAllItem(true);
                for (int i = 0; all != null && i < all.Count; i++) RefreshOurStoragePrice(all[i]);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-REPAIR] 收纳物品价格扫描失败: " + e.Message); }
        }

        private static bool IsItemStorageContainer(GameItem item)
        {
            if (item == null) return false;
            if (IsOurStorageContainer(item)) return true;
            if (IsBrewingManual(item)) return false;
            try
            {
                // Only the game's actual storage bays and smuggler bays are
                // forbidden. Other items with an inner grid (test kits, beer
                // crates, machines, etc.) remain valid contents.
                string id = item.identifier ?? "";
                return id == StorageBayId
                    || id.StartsWith("storage_bay_", StringComparison.Ordinal)
                    || id.EndsWith("_storage_bay", StringComparison.Ordinal)
                    || id == "smuggler_bay"
                    || id.StartsWith("smuggler_bay_", StringComparison.Ordinal)
                    || id.EndsWith("_smuggler_bay", StringComparison.Ordinal);
            }
            catch { }
            return false;
        }

        // ★ 槽位接受：手册用 tag 识别，配方只在目标为档案箱/手册时检查 owned
        public static bool? CheckSlotAccepts(GameInventory slot, GameItem item)
        {
            if (item == null || slot == null) return null;
            var machineRule = XIAOWOBrewingBarrel.BarrelSlotRules.Accepts(slot, item);
            if (machineRule.HasValue && !machineRule.Value) return false;
            string itemId = item.identifier ?? "";

            GameItem parentItem = null;
            try { parentItem = slot.GetParentItem(); } catch { }
            string parentId = parentItem != null ? (parentItem.identifier ?? "") : "";
            bool isTargetManual = IsBrewingManual(parentItem);

            // Block nesting in both directions while leaving ordinary inventory
            // placement and already-saved contents untouched.
            if (IsOurStorageContainer(parentItem) && IsItemStorageContainer(item)) return false;
            if (IsOurStorageContainer(item) && IsItemStorageContainer(parentItem)) return false;

            // ★ 目标容器是酿酒手册：只接受 DOCUMENT 类型的物品
            if (isTargetManual)
            {
                bool isDoc = false;
                try
                {
                    var types = item.itemTypes;
                    if (types != null)
                        for (int i = 0; i < types.Count; i++)
                            if (types[i] == "DOCUMENT") { isDoc = true; break; }
                }
                catch { }

                if (!isDoc) return false;  // 非文档一律拒绝

                // ★ 我们的配方：检查 owned
                if (IsOurBrewingDocument(itemId))
                {
                    bool owned = false;
                    try
                    {
                        var store = PlayerStore.Instance;
                        if (store != null) owned = store.IsPlayerOwnThisItem(itemId);
                    }
                    catch { }
                    return owned;
                }

                // ★ 我们的名片：检查 owned
                if (IsNiniCard(item))
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }

                // ★ 我们的证书：检查 owned
                if (IsOurCert(itemId))
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }

                // ★ 酿酒手册本身：检查 owned（防止把别人的手册夹带进去）
                if (IsBrewingManual(item))
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }

                // 其他文档一律允许
                return true;
            }

            // 证书：只有目标为档案箱时才检查 owned
            if (IsOurCert(itemId))
            {
                if (parentId == DossierId)
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }
                return null;
            }

            // 配方：可放进档案箱（未拥有拒绝）
            if (IsOurBrewingDocument(itemId))
            {
                if (parentId == DossierId)
                {
                    bool owned = false;
                    try
                    {
                        var store = PlayerStore.Instance;
                        if (store != null) owned = store.IsPlayerOwnThisItem(itemId);
                    }
                    catch { }
                    return owned;
                }
                return null;
            }

            // ★ 泥泥的名片：只有目标为档案箱时才检查 owned
            if (IsNiniCard(item))
            {
                if (parentId == DossierId)
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }
                return null;   // 其他槽位不覆盖，走原版逻辑
            }

            // ★ 手册：只有目标为档案箱时才检查 owned
            if (IsBrewingManual(item))
            {
                if (parentId == DossierId)
                {
                    if (!IsOwnedByPlayer(item)) return false;
                    return true;
                }
                return null;   // 其他槽位不覆盖，走原版逻辑
            }

            // 种子、果实逻辑（不变）
            bool isSeed = IsOurSeed(itemId);
            bool isFruit = IsOurFruit(itemId);
            if (!isSeed && !isFruit) return null;

            try { if (slot is GameSlotInventory) return false; } catch { }

            bool belongsToIncubator = false;
            if (parentId == IncubatorId) belongsToIncubator = true;

            long ptr = SlotPointer(slot);
            string slotTag = null;
            if (ptr != 0) SlotTagsByPointer.TryGetValue(ptr, out slotTag);

            if (slotTag == SlotTagInput || slotTag == SlotTagOutput) belongsToIncubator = true;

            if (belongsToIncubator && !IsOwnedByPlayer(item)) return false;

            if (slotTag == SlotTagModule) return false;
            if (slotTag == SlotTagInput) return isSeed;
            if (slotTag == SlotTagOutput) return isFruit;

            if (belongsToIncubator) return true;
            return null;
        }
    }

    // ============ 发酵判定 ============
    [HarmonyPatch(typeof(WineHelper), "TryStartFermentation")]
    public static class PatchTryStartHentaiFermentation
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem item)
        {
            try { BrewingExpansionMod.UpgradeLegacyHentaiYeast(item); }
            catch (Exception ex) { MelonLogger.Warning("[BE-WINE] legacy hentai yeast upgrade failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(WineHelper), "StartFermentation")]
    public static class PatchStartFermentationPrefix
    {


        [HarmonyPrefix]
        public static void Prefix(GameItem item, out bool __state)
        {
            __state = false;
            try
            {
                if (item == null) return;

                // ★ 只处理酒瓶
                string __id = "";
                try { __id = item.identifier ?? ""; } catch { }
                if (__id != "wine_bottle") return;

                int waterScore = 0;
                // ... 后续逻辑
                try { waterScore = WineHelper.GetWaterQualityScore(item); } catch { }
                var children = item.FindAllChildItems();
                if (children == null) return;

                var fruits = new List<string>();
                bool hasAdvancedReactor = false;
                bool hasHentaiYeast = false;
                for (int i = 0; i < children.Count; i++)
                {
                    var c = children[i];
                    if (c == null) continue;
                    string cid = c.identifier ?? "";
                    if (BrewingExpansionMod.IsOurFruit(cid)) fruits.Add(cid);
                    if (cid == BrewingExpansionMod.AdvancedYeastReactorId) hasAdvancedReactor = true;
                    if (BrewingExpansionMod.IsHentaiYeast(c)) hasHentaiYeast = true;
                }
                if (hasHentaiYeast)
                {
                    BrewingExpansionMod.MarkBottle(item, BrewingExpansionMod.NianHeshiWineId, waterScore);
                    __state = true;
                    return;
                }
                if (fruits.Count < 1) return;

                string wineId = ResolveRecipe(fruits);
                if (string.IsNullOrEmpty(wineId)) wineId = ResolveMiscWine(fruits);

                int contraLvl = hasAdvancedReactor ? BrewingExpansionMod.AdvancedYeastReactorContrabandLevel : 0;
                BrewingExpansionMod.MarkBottle(item, wineId, waterScore, contraLvl);
                if (hasAdvancedReactor) try { ContrabandHelper.InitContrabandItem(item, contraLvl); } catch { }
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-WINE] 发酵配方标记失败: " + ex.Message); }
        }

        [HarmonyPostfix]
        public static void Postfix(GameItem item, bool __state)
        {
            if (!__state || item == null) return;
            try
            {
                var remaining = item.GetTagReadonly("FERMENTATION_TIME_REMAINING_INT");
                if (remaining == null)
                {
                    MelonLogger.Warning("[BE-WINE] hentai yeast started without a fermentation timer");
                    return;
                }
                if (!BrewingExpansionMod.SetTagInt(item, "FERMENTATION_TIME_REMAINING_INT",
                    BrewingExpansionMod.HentaiYeastFermentationDays)
                    || item.GetTagReadonly("FERMENTATION_TIME_REMAINING_INT")?.valueInt
                        != BrewingExpansionMod.HentaiYeastFermentationDays)
                    MelonLogger.Warning("[BE-WINE] hentai yeast fermentation timer was not saved");
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-WINE] hentai yeast timer failed: " + ex.Message); }
        }

        private static string ResolveRecipe(List<string> fruits)
        {
            if (fruits.Count == 0) return null;
            if (fruits.Count == 2)
            {
                string a = fruits[0], b = fruits[1];
                if (a == b) return GetSingleFruitWine(a);
                var set = new HashSet<string> { a, b };

                if (set.SetEquals(new[] { "wheat_fruit", "rice_fruit" })) return BrewingExpansionMod.GusuiWineId;
                if (set.SetEquals(new[] { "chili_fruit", "sichuan_pepper_fruit" })) return BrewingExpansionMod.ChimaduWineId;
                if (set.SetEquals(new[] { "lemon_fruit", "sugarcane_fruit" })) return BrewingExpansionMod.SuantianBanzhanWineId;
                if (set.SetEquals(new[] { "yellow_grape_fruit", "cucumber_fruit" })) return BrewingExpansionMod.BipoLiuguangWineId;
                if (set.SetEquals(new[] { "wormwood_fruit", "lemon_fruit" })) return BrewingExpansionMod.BanzhanSuankuWineId;
                if (set.SetEquals(new[] { "rice_fruit", "yellow_grape_fruit" })) return BrewingExpansionMod.DaopoShuangganWineId;
                if (set.SetEquals(new[] { "wheat_fruit", "sugarcane_fruit" })) return BrewingExpansionMod.MaiganNiangWineId;
                if (set.SetEquals(new[] { "lychee_fruit", "rice_fruit" })) return "lychee_rice_wine";
                if (set.SetEquals(new[] { "lychee_fruit", "wormwood_fruit" })) return "lychee_wormwood_wine";
                if (set.SetEquals(new[] { "lychee_fruit", "chili_fruit" })) return "lychee_chili_wine";
                if (set.SetEquals(new[] { BrewingExpansionMod.JasmineFruitId, "rice_fruit" })) return BrewingExpansionMod.MoxiangMiniangWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.JasmineFruitId, "lemon_fruit" })) return BrewingExpansionMod.MoningQingxiaoWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.JasmineFruitId, "sugarcane_fruit" })) return BrewingExpansionMod.HuazheLiunianWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.GreenGrapeFruitId, "wheat_fruit" })) return BrewingExpansionMod.BimaiLiujinWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.GreenGrapeFruitId, "cucumber_fruit" })) return BrewingExpansionMod.CuiyuQingzhuWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.GreenGrapeFruitId, "yellow_grape_fruit" })) return BrewingExpansionMod.ZiluJinfengWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.JasmineFruitId, BrewingExpansionMod.GreenGrapeFruitId })) return BrewingExpansionMod.MoliQingtiWineId;
                if (set.SetEquals(new[] { "osmanthus_fruit", "lemon_fruit" })) return BrewingExpansionMod.GuixinChisuWineId;
                if (set.SetEquals(new[] { "bayberry_fruit", "sugarcane_fruit" })) return BrewingExpansionMod.JianghenChenzheWineId;
                if (set.SetEquals(new[] { "bayberry_fruit", "chili_fruit" })) return BrewingExpansionMod.ZhuoleiXiandanWineId;
                if (set.SetEquals(new[] { "osmanthus_fruit", "bayberry_fruit" })) return BrewingExpansionMod.CuojiShuWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.VoidPlumFruitId, "rice_fruit" })) return BrewingExpansionMod.ChenyuanMiniangWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.VoidPlumFruitId, "wormwood_fruit" })) return BrewingExpansionMod.KongguKuaiWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.StardustBerryFruitId, "wheat_fruit" })) return BrewingExpansionMod.XingluoMaitianWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.StardustBerryFruitId, "lemon_fruit" })) return BrewingExpansionMod.LengxingRuzhanWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.VoidPlumFruitId, BrewingExpansionMod.JasmineFruitId })) return BrewingExpansionMod.YexiangChenjiWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.StardustBerryFruitId, BrewingExpansionMod.GreenGrapeFruitId })) return BrewingExpansionMod.XingzhuiQingtengWineId;
                if (set.SetEquals(new[] { BrewingExpansionMod.VoidPlumFruitId, BrewingExpansionMod.StardustBerryFruitId })) return BrewingExpansionMod.XingchenWangeWineId;
                return null;
            }
            return null;
        }

        private static string GetSingleFruitWine(string f)
        {
            if (f == "chili_fruit") return "chili_wine";
            if (f == "rice_fruit") return "rice_wine";
            if (f == "wheat_fruit") return "wheat_wine";
            if (f == "wormwood_fruit") return "wormwood_wine";
            if (f == "sichuan_pepper_fruit") return "sichuan_pepper_wine";
            if (f == "lemon_fruit") return "lemon_wine";
            if (f == "yellow_grape_fruit") return "yellow_grape_wine";
            if (f == "sugarcane_fruit") return "sugarcane_wine";
            if (f == "cucumber_fruit") return "cucumber_wine";
            if (f == "lychee_fruit") return "lychee_wine";
            if (f == BrewingExpansionMod.JasmineFruitId) return BrewingExpansionMod.JasmineWineId;
            if (f == BrewingExpansionMod.GreenGrapeFruitId) return BrewingExpansionMod.GreenGrapeWineId;
            if (f == "osmanthus_fruit") return "osmanthus_wine";
            if (f == "bayberry_fruit") return "bayberry_wine";
            if (f == BrewingExpansionMod.VoidPlumFruitId) return BrewingExpansionMod.VoidPlumWineId;
            if (f == BrewingExpansionMod.StardustBerryFruitId) return BrewingExpansionMod.StardustBerryWineId;
            if (f == BrewingExpansionMod.MidautumnMoonOsmanthusId) return BrewingExpansionMod.MidautumnOsmanthusWineId;
            return null;
        }

        private static string ResolveMiscWine(List<string> fruits)
        {
            int maxR = 0;
            for (int i = 0; i < fruits.Count; i++)
            {
                int r = GetRarity(fruits[i]);
                if (r > maxR) maxR = r;
            }
            if (maxR == 3) return BrewingExpansionMod.YujinWineId;
            if (maxR == 2) return BrewingExpansionMod.CuofuWineId;
            if (maxR == 1) return BrewingExpansionMod.YidiaoWineId;
            return BrewingExpansionMod.MiscWineId;
        }

        private static int GetRarity(string fruitId)
        {
            if (fruitId == "lychee_fruit" || fruitId == BrewingExpansionMod.JasmineFruitId
                || fruitId == BrewingExpansionMod.GreenGrapeFruitId) return 1;
            if (fruitId == "osmanthus_fruit" || fruitId == "bayberry_fruit") return 2;
            if (fruitId == BrewingExpansionMod.VoidPlumFruitId
                || fruitId == BrewingExpansionMod.StardustBerryFruitId) return 3;
            return 0;
        }
    }

    [HarmonyPatch]
    public static class PatchSpriteAtlasGetSprite
    {
        private static Type FindAtlasType()
        {
            // TypeByName enumerates every type in every loaded assembly. Some
            // generated Unity modules contain unloadable types, which makes
            // Harmony print warnings even though SpriteAtlas is available.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType("UnityEngine.U2D.SpriteAtlas", false)
                        ?? assembly.GetType("UnityEngine.SpriteAtlas", false);
                    if (type != null) return type;
                }
                catch (Exception) { }
            }
            return null;
        }

        [HarmonyPrepare]
        public static bool Prepare()
        {
            var t = FindAtlasType();
            if (t == null) { MelonLogger.Warning("[BE-ATLAS] 找不到 SpriteAtlas 类型"); return false; }
            var m = t.GetMethod("GetSprite", new[] { typeof(string) });
            if (m == null) { MelonLogger.Warning("[BE-ATLAS] SpriteAtlas 没有 GetSprite(string)"); return false; }
            MelonLogger.Msg("[BE-ATLAS] 找到 SpriteAtlas.GetSprite(string)，挂 patch");
            return true;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            return FindAtlasType()?.GetMethod("GetSprite", new[] { typeof(string) });
        }

        private static readonly HashSet<string> _atlasDiagLogged = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(string name, ref Sprite __result)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return;

                // ★ 诊断：只打印一次，看看游戏传进来什么 name
                if (name.IndexOf("be_", StringComparison.OrdinalIgnoreCase) >= 0
                    && _atlasDiagLogged.Add(name))
                {
                    MelonLogger.Msg("[BE-ATLAS-DIAG] name='" + name + "' old=" + (__result == null ? "null" : "OK"));
                }

                Sprite ours = BrewingExpansionMod.FindOurSprite(name);
                if (ours != null)
                {
                    __result = ours;
                    if (_atlasDiagLogged.Contains(name))
                        MelonLogger.Msg("[BE-ATLAS-DIAG]   → 换成我们自己的 sprite");
                }
            }
            catch { }
        }
    }

    [HarmonyPatch]
    public static class PatchGameItemElementResolveItemSprite
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            var m = AccessTools.Method(typeof(GameItemElement), "ResolveItemSprite", Type.EmptyTypes);
            if (m == null) { MelonLogger.Warning("[BE-GIE-PATCH] 找不到 ResolveItemSprite"); return false; }
            MelonLogger.Msg("[BE-GIE-PATCH] 挂上 ResolveItemSprite");
            return true;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GameItemElement), "ResolveItemSprite", Type.EmptyTypes);
        }

        private static readonly HashSet<string> _gieDiagLogged = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(GameItemElement __instance, ref Sprite __result)
        {
            try
            {
                if (__instance == null) return;

                // 充能器会按原版电池路径解析图标；直接按自定义电池 ID
                // 取对应电量的贴图，避免依赖被原版重写的 spritePath。
                if (XIAOWOBrewingBarrel.XuxuBatteryVisuals.TryGetKey(__instance, out var batteryKey))
                {
                    var batterySprite = BrewingExpansionMod.FindOurSprite(batteryKey);
                    if (batterySprite != null) __result = batterySprite;
                    return;
                }

                string sp = __instance.spritePath ?? "";

                // ★ 诊断：只打印一次
                if (sp.IndexOf("be_", StringComparison.OrdinalIgnoreCase) >= 0
                    && _gieDiagLogged.Add(sp))
                {
                    MelonLogger.Msg("[BE-GIE-DIAG] spritePath='" + sp + "' old=" + (__result == null ? "null" : "OK"));
                }

                Sprite ours = BrewingExpansionMod.FindOurSprite(sp);
                if (ours != null)
                {
                    __result = ours;
                    if (_gieDiagLogged.Contains(sp))
                        MelonLogger.Msg("[BE-GIE-DIAG]   → 换成我们自己的 sprite");
                }
            }
            catch { }
        }
    }

    // ============ SpriteDict / 物品注册 ============
    [HarmonyPatch(typeof(SpriteDict), "GetSprite")]
    public static class PatchSpriteDictGetSprite
    {
        [HarmonyPrefix]
        public static bool Prefix(string key, ref Sprite __result)
        {
            try
            {
                if (string.IsNullOrEmpty(key)) return true;

                Sprite ours = BrewingExpansionMod.FindOurSprite(key);
                if (ours != null)
                {
                    __result = ours;
                    return false;
                }
            }
            catch { }
            return true;
        }
    }

    // 原版充能器的外观贴图直接调用 RenderHandler.LoadFromAtlas，
    // 不经过 GameItemElement 或 SpriteDict；空 atlas 的自定义电池会显示问号。
    [HarmonyPatch(typeof(RenderHandler), "LoadFromAtlas", new[] { typeof(string), typeof(string) })]
    public static class PatchRenderHandlerLoadFromAtlas
    {
        [HarmonyPrefix]
        public static bool Prefix(string name, ref Sprite __result)
        {
            try
            {
                if (string.IsNullOrEmpty(name) || !name.StartsWith("be_xuxu_", StringComparison.Ordinal))
                    return true;
                var sprite = BrewingExpansionMod.FindOurSprite(name);
                if (sprite == null) return true;
                __result = sprite;
                return false;
            }
            catch { return true; }
        }
    }

    // ★ 这里加回来
    [HarmonyPatch(typeof(GameItemElement), "ResolveSpriteByName")]
    public static class Patch_ResolveSpriteByName
    {
        private static readonly HashSet<string> BatteryDiagnostics = new HashSet<string>();

        [HarmonyPrefix]
        public static bool Prefix(string name, ref Sprite __result)
        {
            try
            {
                if (string.IsNullOrEmpty(name)) return true;

                Sprite ours = BrewingExpansionMod.FindOurSprite(name);
                if (ours != null)
                {
                    __result = ours;
                    return false;
                }
            }
            catch { }
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(GameItemElement __instance, string name, ref Sprite __result)
        {
            try
            {
                if (!XIAOWOBrewingBarrel.XuxuBatteryVisuals.TryGetKey(__instance, out var key)) return;
                var sprite = BrewingExpansionMod.FindOurSprite(key);
                if (sprite != null) __result = sprite;
                if (BatteryDiagnostics.Add(__instance.identifier ?? key))
                    MelonLogger.Msg("[BE-PWR] 电池图标解析: " + __instance.identifier
                        + " requested=" + name + " custom=" + key
                        + " found=" + (sprite != null));
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PWR] 电池图标解析失败: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(WineDirectory), "InitDirectory")]
    public static class PatchHentaiYeastDirectory
    {
        [HarmonyPostfix]
        public static void Postfix(WineDirectory __instance)
        {
            try
            {
                System.Func<GameItem> factory = () => BrewingExpansionMod.CreateHentaiYeast();
                __instance.Add(BrewingExpansionMod.HentaiYeastId, factory);
            }
            catch (Exception ex) { MelonLogger.Warning("[BE] hentai yeast registration failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(FoodItemDirectory), "InitDirectory")]
    public static class PatchRegisterItems
    {
        private static bool _registered = false;
        public static FoodItemDirectory Instance = null;
        public static bool DirReady = false;

        [HarmonyPostfix]
        public static void Postfix(FoodItemDirectory __instance)
        {
            MelonLogger.Msg("[BE] FoodItemDirectory 物品注册完成（临时禁用注册）。");

            if (_registered || __instance == null) return;
            _registered = true;
            Instance = __instance;
            DirReady = true;

            for (int i = 0; i < BrewingExpansionMod.SeedIds.Length; i++)
            {
                string id = BrewingExpansionMod.SeedIds[i];
                string name = BrewingExpansionMod.SeedNames[i];
                string key = BrewingExpansionMod.SeedSpriteKeys[i];
                try
                {
                    string cid = id, cname = name, ckey = key;
                    System.Func<GameItem> f = () => BrewingExpansionMod.CreateSeedItem(cid, cname, ckey);
                    __instance.Add(id, f);
                }
                catch { }
            }

            for (int i = 0; i < BrewingExpansionMod.FruitIds.Length; i++)
            {
                string id = BrewingExpansionMod.FruitIds[i];
                string name = BrewingExpansionMod.FruitNames[i];
                string key = BrewingExpansionMod.FruitSpriteKeys[i];
                if (id == BrewingExpansionMod.MidautumnMoonOsmanthusId) continue;
                try
                {
                    string cid = id, cname = name, ckey = key;
                    System.Func<GameItem> f = () => BrewingExpansionMod.CreateFruitItem(cid, cname, ckey);
                    __instance.Add(id, f);
                }
                catch { }
            }

            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateIncubatorItem(); __instance.Add(BrewingExpansionMod.IncubatorId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateWinePrinterItem(); __instance.Add(BrewingExpansionMod.WinePrinterId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateBrewerySignItem(); __instance.Add(BrewingExpansionMod.BrewerySignId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateNiniCardItem(); __instance.Add(BrewingExpansionMod.NiniCardId, f); } catch { }

            for (int i = 0; i < BrewingExpansionMod.RecipeIds.Length; i++)
            {
                int captured = i;
                string id = BrewingExpansionMod.RecipeIds[i];
                try
                {
                    System.Func<GameItem> f = () => BrewingExpansionMod.CreateRecipeItem(captured);
                    __instance.Add(id, f);
                }
                catch { }
            }

            for (int i = 0; i < XIAOWOBrewingBarrel.MachineGuides.Ids.Length; i++)
            {
                int captured = i;
                string id = XIAOWOBrewingBarrel.MachineGuides.Ids[i];
                try
                {
                    System.Func<GameItem> f = () => XIAOWOBrewingBarrel.MachineGuides.Create(captured);
                    __instance.Add(id, f);
                }
                catch (Exception ex) { MelonLogger.Warning("[BE] 注册机器说明失败 " + id + ": " + ex.Message); }
            }

            for (int i = 0; i < BrewingExpansionMod.CertIds.Length; i++)
            {
                string id = BrewingExpansionMod.CertIds[i];
                string name = BrewingExpansionMod.CertNames[i];
                string key = BrewingExpansionMod.CertSpriteKeys[i];
                string desc = BrewingExpansionMod.CertDescriptions[i];
                int price = BrewingExpansionMod.CertPrices[i];
                try
                {
                    string cid = id, cname = name, ckey = key, cdesc = desc;
                    int cprice = price;
                    System.Func<GameItem> f = () => BrewingExpansionMod.CreateCertItem(cid, cname, ckey, cdesc, cprice);
                    __instance.Add(id, f);
                }
                catch { }
            }

            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateGoldenYeastReactorItem(); __instance.Add(BrewingExpansionMod.GoldenYeastReactorId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateAdvancedYeastReactorItem(); __instance.Add(BrewingExpansionMod.AdvancedYeastReactorId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateForbiddenYeastReactorItem(); __instance.Add(BrewingExpansionMod.ForbiddenYeastReactorId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateNeonDreamWine(); __instance.Add(BrewingExpansionMod.NeonDreamWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateShuangqingWine(); __instance.Add(BrewingExpansionMod.ShuangqingWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateNanfengWine(); __instance.Add(BrewingExpansionMod.NanfengWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateFeimeiWine(); __instance.Add(BrewingExpansionMod.FeimeiWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateNeonIllusionWine(); __instance.Add(BrewingExpansionMod.NeonIllusionWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateIdealEndWine(); __instance.Add(BrewingExpansionMod.IdealEndWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateLuofuWine(); __instance.Add(BrewingExpansionMod.LuofuWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateMagicPigWine(); __instance.Add(BrewingExpansionMod.MagicPigWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateSeaBlueWine(); __instance.Add(BrewingExpansionMod.SeaBlueWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateNianHeshiWine(); __instance.Add(BrewingExpansionMod.NianHeshiWineId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateRainbowRecipeWine(); __instance.Add(BrewingExpansionMod.RainbowRecipeWineId, f); } catch { }
            // ★ 中秋节系列

            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateSakuraBag(); __instance.Add(BrewingExpansionMod.SakuraBagId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateCyberBag(); __instance.Add(BrewingExpansionMod.CyberBagId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateLaolishiBag(); __instance.Add(BrewingExpansionMod.LaolishiBagId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateCatYouyouBag(); __instance.Add(BrewingExpansionMod.CatYouyouBagId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateXuxuBattery(); __instance.Add(BrewingExpansionMod.XuxuBatteryId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateXuxuMidBattery(); __instance.Add(BrewingExpansionMod.XuxuMidBatteryId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateXuxuHighBattery(); __instance.Add(BrewingExpansionMod.XuxuHighBatteryId, f); } catch { }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateWurenMooncake(); __instance.Add(BrewingExpansionMod.WurenMooncakeId, f); } catch { }

            MelonLogger.Msg("[BE] FoodItemDirectory 物品注册完成。");
        }
    }

    // ★ 酿酒手册：注册到 MiscItemDirectory，用 storage_bay 当模板
    [HarmonyPatch(typeof(MiscItemDirectory), "InitDirectory")]
    public static class PatchMiscDirInit
    {
        private static bool _registered = false;
        public static MiscItemDirectory Instance = null;

        [HarmonyPostfix]
        public static void Postfix(MiscItemDirectory __instance)
        {
            MelonLogger.Msg("[BE] MiscItemDirectory.InitDirectory 被调用（临时禁用注册）。");

            if (_registered || __instance == null) return;
            _registered = true;
            Instance = __instance;

            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateBrewingManualAsContainer();
                __instance.Add(BrewingExpansionMod.BrewingManualId, f);
                MelonLogger.Msg("[BE] 酿酒手册已注册（storage_bay 模板）");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册酿酒手册失败: " + e.Message); }
            // ★ 新增：注册四十七「小」暗格
            try
            {
                System.Func<GameItem> f2 = () => BrewingExpansionMod.CreateCompactSmugglerBay();
                __instance.Add(BrewingExpansionMod.CompactSmugglerBayId, f2);
                MelonLogger.Msg("[BE] 四十七「小」暗格已注册");
            }

            catch (Exception e) { MelonLogger.Warning("[BE] 注册小暗格失败: " + e.Message); }
            // ★ 注册多功能收纳仓
            try
            {
                System.Func<GameItem> f3 = () => BrewingExpansionMod.CreateMultiStorage();
                __instance.Add(BrewingExpansionMod.MultiStorageId, f3);
                MelonLogger.Msg("[BE] 多功能收纳仓已注册");
            }

            catch (Exception e) { MelonLogger.Warning("[BE] 注册多功能收纳仓失败: " + e.Message); }
            XIAOWOBrewingBarrel.CustomCassettes.RegisterItems(__instance);

            // 注册香水
            try
            {
                System.Func<GameItem> f4 = () => BrewingExpansionMod.CreateMyPerfume();
                __instance.Add(BrewingExpansionMod.MyPerfumeId, f4);
                MelonLogger.Msg("[BE] 香水已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册香水失败: " + e.Message); }
            try
            {
                System.Func<GameItem> freedomPerfume = () => BrewingExpansionMod.CreateFreedomPerfume();
                __instance.Add(BrewingExpansionMod.FreedomPerfumeId, freedomPerfume);
                MelonLogger.Msg("[BE] 逍遥法外已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册逍遥法外失败: " + e.Message); }

            // 情思盲盒沿用旧 ID，旧存档里的盲盒仍可打开。
            try
            {
                System.Func<GameItem> f5 = () => BrewingExpansionMod.CreatePremiumBlindbox();
                __instance.Add(BrewingExpansionMod.PremiumBlindboxId, f5);
                MelonLogger.Msg("[BE] 情思盲盒已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册情思盲盒失败: " + e.Message); }

            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateLowBlindbox(); __instance.Add(BrewingExpansionMod.LowBlindboxId, f); MelonLogger.Msg("[BE] 凡间盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册凡间盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateMidBlindbox(); __instance.Add(BrewingExpansionMod.MidBlindboxId, f); MelonLogger.Msg("[BE] 远来盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册远来盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateAfterglowBlindbox(); __instance.Add(BrewingExpansionMod.AfterglowBlindboxId, f); MelonLogger.Msg("[BE] 遗光盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册遗光盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateWineBlindbox(); __instance.Add(BrewingExpansionMod.WineBlindboxId, f); MelonLogger.Msg("[BE] 收藏酒盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册收藏酒盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateContainerBlindbox(); __instance.Add(BrewingExpansionMod.ContainerBlindboxId, f); MelonLogger.Msg("[BE] 容器盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册容器盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateFigurineBlindbox(); __instance.Add(BrewingExpansionMod.FigurineBlindboxId, f); MelonLogger.Msg("[BE] 手办盲盒已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册手办盲盒失败: " + e.Message); }
            try { System.Func<GameItem> f = () => BrewingExpansionMod.CreateCassetteBundle(); __instance.Add(BrewingExpansionMod.CassetteBundleId, f); MelonLogger.Msg("[BE] xuxu磁带捆绑包已注册"); } catch (Exception e) { MelonLogger.Warning("[BE] 注册磁带捆绑包失败: " + e.Message); }
        }
    }

    // ★ 清酿澄水箱注册到 AmenitiesItemDirectory
    [HarmonyPatch(typeof(AmenitiesItemDirectory), "InitDirectory")]
    public static class PatchAmenitiesDirInit
    {
        private static bool _registered = false;

        [HarmonyPostfix]
        public static void Postfix(AmenitiesItemDirectory __instance)
        {
            MelonLogger.Msg("[BE] AmenitiesItemDirectory.InitDirectory 被调用（临时禁用注册）。");

            if (_registered || __instance == null) return;
            _registered = true;

            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateClearWaterTank();
                __instance.Add(BrewingExpansionMod.ClearWaterTankId, f);
                MelonLogger.Msg("[BE] 清酿澄水箱已注册到 AmenitiesItemDirectory");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册清酿澄水箱失败: " + e.Message); }

            // ★ 新增三个杯子
            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateXuxuWarmCup();
                __instance.Add(BrewingExpansionMod.XuxuWarmCupId, f);
                MelonLogger.Msg("[BE] xuxu暖心杯已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册 xuxu暖心杯失败: " + e.Message); }

            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateXuxuSweetCup();
                __instance.Add(BrewingExpansionMod.XuxuSweetCupId, f);
                MelonLogger.Msg("[BE] xuxu甜心杯已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册 xuxu甜心杯失败: " + e.Message); }

            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateLove47Cup();
                __instance.Add(BrewingExpansionMod.Love47CupId, f);
                MelonLogger.Msg("[BE] 四十七爱心杯已注册");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册四十七爱心杯失败: " + e.Message); }

            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateQingyunFilter();
                __instance.Add(BrewingExpansionMod.QingyunFilterId, f);
                MelonLogger.Msg("[BE] 青筠已注册到 AmenitiesItemDirectory");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册青筠失败: " + e.Message); }

            // ★ 凝露：注册永久保留（以后盲盒要靠它创建）
            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateNingluFilter();
                __instance.Add(BrewingExpansionMod.NingluFilterId, f);
                MelonLogger.Msg("[BE] 凝露已注册到 AmenitiesItemDirectory");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册凝露失败: " + e.Message); }

            // ★ 樱绯：注册永久保留（以后盲盒要靠它创建）
            try
            {
                System.Func<GameItem> f = () => BrewingExpansionMod.CreateYingfeiFilter();
                __instance.Add(BrewingExpansionMod.YingfeiFilterId, f);
                MelonLogger.Msg("[BE] 樱绯已注册到 AmenitiesItemDirectory");
            }
            catch (Exception e) { MelonLogger.Warning("[BE] 注册樱绯失败: " + e.Message); }
        }
    }




    // ★ 高级盲盒：双击开箱

    // ★ 中秋盲盒：双击开箱

    // ★ 月团礼盒：双击开箱

    // ★ 中秋盲盒：允许激活


    // ★ 三个新盲盒：双击开箱



    // ★ 月团礼盒：允许激活


    // ★ 高级盲盒：允许激活

    // ★ 高级盲盒：可以激活



    // ============ 其他 Patch ============
    [HarmonyPatch]
    public static class PatchFurnaceClosure_Learn
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            MelonLogger.Warning("[BE] 炉子回调目标缺失或不唯一，跳过挂钩");
            return false;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            try
            {
                return ClosureHookTarget.FindUnique(typeof(MachineFurnace), typeof(bool),
                    new[] { typeof(GameItem), typeof(GameInventory) }, "furnace", typeof(GameItem));
            }
            catch { }
            return null;
        }

        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            try
            {
                if (__instance == null) return;
                var t = __instance.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                GameItem furnace = null;
                GameInventory left = null, right = null, module = null;

                foreach (var p in t.GetProperties(flags))
                {
                    try
                    {
                        string pn = p.Name;
                        if (pn != "furnace" && pn != "itemInventoryLeft"
                            && pn != "itemInventoryRight" && pn != "itemInventoryModule") continue;
                        object v = p.GetValue(__instance);
                        if (v == null) continue;
                        if (furnace == null && pn == "furnace" && v is GameItem gi) furnace = gi;
                        else if (left == null && pn == "itemInventoryLeft" && v is GameInventory gli) left = gli;
                        else if (right == null && pn == "itemInventoryRight" && v is GameInventory gri) right = gri;
                        else if (module == null && pn == "itemInventoryModule" && v is GameInventory gmi) module = gmi;
                    }
                    catch { }
                }

                if (furnace == null) return;
                string id = furnace.identifier ?? "";
                string nm = furnace.name ?? "";
                if ((id != BrewingExpansionMod.IncubatorId) && (nm != BrewingExpansionMod.IncubatorName)) return;

                if (left != null)
                {
                    long p = BrewingExpansionMod.SlotPointer(left);
                    if (p != 0) BrewingExpansionMod.SlotTagsByPointer[p] = BrewingExpansionMod.SlotTagInput;
                }
                if (right != null)
                {
                    long p = BrewingExpansionMod.SlotPointer(right);
                    if (p != 0) BrewingExpansionMod.SlotTagsByPointer[p] = BrewingExpansionMod.SlotTagOutput;
                }
                if (module != null)
                {
                    long p = BrewingExpansionMod.SlotPointer(module);
                    if (p != 0) BrewingExpansionMod.SlotTagsByPointer[p] = BrewingExpansionMod.SlotTagModule;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameItem), "GetCurrentValue")]
    public static class PatchGetCurrentValuePrice
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem __instance) => BrewingExpansionMod.RefreshOurStoragePrice(__instance);

        [HarmonyPostfix]
        public static void Postfix(GameItem __instance, ref long __result)
        {
            try
            {
                if (__instance == null) return;
                int overridePrice = BrewingExpansionMod.GetWineBasePriceOverrideByName(__instance.name);
                if (overridePrice < 0) return;
                if (overridePrice <= 100) return;
                long curVal = 0;
                try { curVal = __instance.unitValue; } catch { }
                if (curVal >= overridePrice) return;
                __result += (overridePrice - 100);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(WineHelper), "CreateTooltip")]
    public static class PatchWineTooltipRefresh
    {


        [HarmonyPrefix]
        public static void Prefix(GameItem item)
        {
            try
            {
                if (item == null) return;

                // ★ 只处理酒瓶
                string id = "";
                try { id = item.identifier ?? ""; } catch { }
                if (id != "wine_bottle") return;

                BrewingExpansionMod.RefreshOurWineBasePrice(item);
            }
            catch { }
        }
    }

    // Collector wines retain their luxury category, but substance buyers may buy them
    // regardless of faction. Hook this specific refusal rule so other native restrictions remain.
    [HarmonyPatch(typeof(StoreClient), nameof(StoreClient.IsRefusingLuxuryItem))]
    public static class PatchCollectorWineLuxuryRefusal
    {
        [HarmonyPostfix]
        public static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
        {
            if (!__result || __instance == null || !BrewingExpansionMod.IsCollectorWine(gameItem)) return;
            var buyingTags = __instance.clientBuyingTagList;
            if (buyingTags == null) return;
            for (int i = 0; i < buyingTags.Count; i++)
                if (buyingTags[i] == "SUBSTANCE" || buyingTags[i] == "ALCOHOL")
                {
                    __result = false;
                    return;
                }
        }
    }

    [HarmonyPatch]
    public static class PatchIsClientBuyingThisItem
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            var m = TargetMethod();
            if (m == null) { MelonLogger.Warning("[BE] IsClientBuyingThisItem 找不到，跳过"); return false; }
            MelonLogger.Msg("[BE] 挂上 IsClientBuyingThisItem: " + m);
            return true;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            try
            {
                var t = typeof(StoreClient);
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name != "IsClientBuyingThisItem") continue;
                    if (m.ReturnType != typeof(bool)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(GameItem)) return m;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE] TargetMethod 异常: " + e.Message); }
            return null;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
        {
            bool isNini = false;
            try
            {
                if (__instance == null || gameItem == null) return;
                isNini = (__instance.identifier ?? "") == BrewingExpansionMod.WeeklyTraderId;
                if (isNini)
                {
                    // 无论原版结果如何，泥泥都只收烟酒。
                    __result = PatchDismissTrigger47.IsNiniTradeItem(gameItem);
                    return;
                }
                // The targeted luxury-rule patch handles collectors in the native flow.
                // Do not override a remaining refusal (contraband, variety, etc.) below.
                if (BrewingExpansionMod.IsCollectorWine(gameItem)) return;
                if (__result) return;                 // 其他 NPC 的原版判定保持不变

                // ★ 只处理我们的物品
                if (!IsOurItemForSelling(gameItem)) return;

                // ★ 其他 NPC：按 itemTypes 和 clientBuyingTagList 求交集
                var npcTags = __instance.clientBuyingTagList;
                var itemTypes = gameItem.itemTypes;
                if (npcTags == null || itemTypes == null) return;

                for (int i = 0; i < npcTags.Count; i++)
                {
                    string nt = npcTags[i];
                    if (string.IsNullOrEmpty(nt)) continue;
                    for (int j = 0; j < itemTypes.Count; j++)
                    {
                        if (itemTypes[j] == nt)
                        {
                            __result = true;
                            return;
                        }
                    }
                }
            }
            catch
            {
                if (isNini) __result = false;
            }
        }

        // ★ 判断是不是我们 mod 的物品
        private static bool IsOurItemForSelling(GameItem item)
        {
            try
            {
                if (item == null) return false;
                string id = item.identifier ?? "";
                string name = item.name ?? "";

                // 1. 所有 be_ 开头的（种子、果实、酒、机器、盲盒、证书、手册、容器……）
                if (id.StartsWith("be_", StringComparison.OrdinalIgnoreCase) ||
                    BrewingExpansionMod.IsOurFruit(id) || BrewingExpansionMod.IsOurSeed(id)) return true;

                // 2. 我们改造过的酒瓶（原版 id 是 wine_bottle）
                if (id == "wine_bottle" && BrewingExpansionMod.IsOurWineName(name)) return true;

                // 3. 我们改造过的原版模板实例（暗格、酵母反应器、背包等）
                //    这些 id 保持原版，靠 tag 识别
                try
                {
                    if (item.IsTag(BrewingExpansionMod.CompactSmugglerBayTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.MultiStorageTag)) return true;
                    if (item.IsTag("BE_MIDAUTUMN_BAG")) return true;
                    if (item.IsTag("BE_SAKURA_BAG")) return true;
                    if (item.IsTag("BE_CYBER_BAG")) return true;
                    if (item.IsTag("BE_LAOLISHI_BAG")) return true;
                    if (item.IsTag("BE_XUXU_BATTERY")) return true;
                    if (item.IsTag("BE_XUXU_MID_BATTERY")) return true;
                    if (item.IsTag("BE_XUXU_HIGH_BATTERY")) return true;
                    if (item.IsTag("BE_FORBIDDEN_YEAST_REACTOR")) return true;
                    if (item.IsTag(BrewingExpansionMod.ClearWaterTankTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.XuxuWarmCupTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.XuxuSweetCupTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.Love47CupTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.QingyunFilterTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.NingluFilterTag)) return true;
                    if (item.IsTag(BrewingExpansionMod.YingfeiFilterTag)) return true;
                }
                catch { }

                return false;
            }
            catch { return false; }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "CanSellThisItem", new Type[] { typeof(GameItem), typeof(bool) })]
    public static class PatchNiniSaleRestriction
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance, GameItem gameItem, ref bool __result)
        {
            if (!__result || __instance == null) return;
            bool isNini = false;
            try
            {
                isNini = __instance.currentClientInstance?.GetClientBlueprint()?.identifier
                    == BrewingExpansionMod.WeeklyTraderId;
                if (isNini && !PatchDismissTrigger47.IsNiniTradeItem(gameItem))
                    __result = false;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-NINI] 收购判定失败: " + e.Message);
                if (isNini) __result = false;
            }
        }
    }

    // ★ 诊断：dump 原版 NPC 的购买 tag 和物品的 itemTypes
    [HarmonyPatch(typeof(StoreClient), "IsClientBuyingThisItem")]
    public static class PatchDiagIsBuying
    {
        private static readonly HashSet<string> _logged = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(StoreClient __instance, GameItem gameItem, ref bool __result)
        {
            try
            {
                if (__instance == null || gameItem == null) return;

                string itemId = gameItem.identifier ?? "";
                string itemName = gameItem.name ?? "";

                // 只关心我们 mod 的物品
                bool isOurs =
                    BrewingExpansionMod.IsOurSeed(itemId) ||
                    BrewingExpansionMod.IsOurFruit(itemId) ||
                    BrewingExpansionMod.IsOurWineName(itemName) ||
                    itemId.StartsWith("be_");
                if (!isOurs) return;

                string key = (__instance.identifier ?? "?") + "|" + itemId;
                if (!_logged.Add(key)) return;   // 同一组合只打一次

                // dump 物品的 itemTypes
                string itemTypes = "";
                try
                {
                    var t = gameItem.itemTypes;
                    if (t != null) for (int i = 0; i < t.Count; i++) itemTypes += "[" + t[i] + "]";
                }
                catch { }

                // dump NPC 的 clientBuyingTagList
                string buyingTags = "";
                try
                {
                    var tags = __instance.clientBuyingTagList;
                    if (tags != null) for (int i = 0; i < tags.Count; i++) buyingTags += "[" + tags[i] + "]";
                }
                catch { }

                MelonLogger.Msg("[BE-BUY-DIAG] ==========");
                MelonLogger.Msg("[BE-BUY-DIAG] client=" + (__instance.identifier ?? "?")
                    + "  item=" + itemId + "  name=" + itemName);
                MelonLogger.Msg("[BE-BUY-DIAG] 原版结果 result=" + __result);
                MelonLogger.Msg("[BE-BUY-DIAG] 物品 itemTypes=" + itemTypes);
                MelonLogger.Msg("[BE-BUY-DIAG] NPC clientBuyingTagList=" + buyingTags);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameItemElement), "HandleInnateModSprite")]
    public static class PatchHandleInnateModSprite
    {
        [HarmonyPrefix]
        public static bool Prefix(GameItemElement __instance)
        {
            try { if (__instance != null && BrewingExpansionMod.IsOurWineName(__instance.name ?? "")) return false; } catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(WineHelper), "GetWineQualityTier")]
    public static class PatchGetWineQualityTier
    {
        [HarmonyPostfix]
        public static void Postfix(GameItem __0)
        {
            try
            {
                if (__0 == null) return;
                string id = "";
                try { id = __0.identifier ?? ""; } catch { }
                if (id != "wine_bottle") return;
                if (!BrewingExpansionMod.HasOurWineTag(__0)) return;

                // ★ 不改返回值，只清委托——让游戏不画任何角标
                BrewingExpansionMod.ClearSpriteModifierCallbacks(__0);
            }
            catch { }
        }
    }


    [HarmonyPatch(typeof(WineHelper), "GetWaterQualityScore")]
    public static class PatchGetWaterQualityScore
    {
        [HarmonyPostfix]
        public static void Postfix(GameItem __0)
        {
            try
            {
                if (__0 == null) return;
                string id = "";
                try { id = __0.identifier ?? ""; } catch { }
                if (id != "wine_bottle") return;
                if (!BrewingExpansionMod.HasOurWineTag(__0)) return;

                // ★ 不改返回值，只清委托
                BrewingExpansionMod.ClearSpriteModifierCallbacks(__0);
            }
            catch { }
        }
    }

    [HarmonyPatch]
    public static class PatchBottlePrinterTryPrint
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            MelonLogger.Warning("[BE] 打印机回调目标缺失或不唯一，跳过挂钩");
            return false;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            try
            {
                return ClosureHookTarget.FindUnique(typeof(MachineBottlePrinter), typeof(void),
                    new[] { typeof(string), typeof(int) }, "machine", typeof(GameItem));
            }
            catch { }
            return null;
        }

        [HarmonyPrefix]
        public static void Prefix(object __instance, ref string bottleId, ref int cost)
        {
            try
            {
                if (__instance == null || string.IsNullOrEmpty(bottleId)) return;
                var t = __instance.GetType();
                GameItem machine = null;
                var prop = t.GetProperty("machine", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null) machine = prop.GetValue(__instance) as GameItem;
                if (machine == null)
                {
                    var f = t.GetField("machine", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) machine = f.GetValue(__instance) as GameItem;
                }
                if (machine == null) return;
                if ((machine.identifier ?? "") != BrewingExpansionMod.WinePrinterId) return;
                if (bottleId == BrewingExpansionMod.VanillaSmallBottleId) bottleId = "wine_bottle";
                cost = 10;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(MachineBottlePrinter), "CreateTooltip")]
    public static class PatchBottlePrinterTooltip
    {
        [HarmonyPrefix]
        public static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (item == null) return true;
                if ((item.identifier ?? "") != BrewingExpansionMod.WinePrinterId) return true;
                BrewingExpansionMod.RefreshMachineDescription(item);
                return false;
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameSlotInventory), "MayHaveValidInventorySlot")]
    public static class PatchAccept_GSI { [HarmonyPostfix] public static void Postfix(GameSlotInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(GameSlotInventory), "CheckInventorySlotStillValid")]
    public static class PatchCheck_GSI { [HarmonyPostfix] public static void Postfix(GameSlotInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(GameGridScrollableInventory), "MayHaveValidInventorySlot")]
    public static class PatchAccept_GGSI { [HarmonyPostfix] public static void Postfix(GameGridScrollableInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(GameGridScrollableInventory), "CheckInventorySlotStillValid")]
    public static class PatchCheck_GGSI { [HarmonyPostfix] public static void Postfix(GameGridScrollableInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(GameGridInventory), "MayHaveValidInventorySlot")]
    public static class PatchAccept_GGI { [HarmonyPostfix] public static void Postfix(GameGridInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(GameGridInventory), "CheckInventorySlotStillValid")]
    public static class PatchCheck_GGI { [HarmonyPostfix] public static void Postfix(GameGridInventory __instance, GameItem item, ref bool __result) { var r = BrewingExpansionMod.CheckSlotAccepts(__instance, item); if (r.HasValue) __result = r.Value; } }

    [HarmonyPatch(typeof(PlayerStore), "EndNight")]
    [HarmonyPriority(Priority.High)]
    public static class PatchSeedToFruit
    {
        internal const int EnergyPerNight = IncubatorLayout.EnergyPerNight;
        private static readonly List<GameItem> NightIncubators = new List<GameItem>();

        [HarmonyPrefix]
        public static void Prefix()
        {
            NightIncubators.Clear();
            var allItems = XIAOWOBrewingBarrel.OwnedInventorySnapshot.Items();
            if (allItems == null) return;
            for (int i = 0; i < allItems.Count; i++)
                if (allItems[i]?.identifier == BrewingExpansionMod.IncubatorId)
                    NightIncubators.Add(allItems[i]);
        }

        internal static GameItem Battery(GameItem incubator)
        {
            if (incubator?.contentWindow?.child == null) return null;
            var slots = GraphUtils.FindAllChildrenType<GameSlotInventory>(incubator.contentWindow.child,
                (Il2CppSystem.Func<GameSlotInventory, bool>)(slot => slot != null));
            if (slots == null) return null;
            for (int i = 0; i < slots.Count; i++)
            {
                var battery = slots[i]?.currentItem;
                if (battery != null && battery.GetTagReadonly("power_source_item_energy") != null)
                    return battery;
            }
            return null;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                int quota = BrewingExpansionMod.GetCurrentIncubatorQuota();
                var store = PlayerStore.Instance;
                if (store == null) return;

                var byOwner = new Dictionary<GameItem, List<GameItem>>();
                var seen = new HashSet<long>();
                int poweredIncubators = 0;
                for (int i = 0; i < NightIncubators.Count; i++)
                {
                    var it = NightIncubators[i];
                    if (it == null) continue;
                    string id = it.identifier ?? "";
                    string nm = it.name ?? "";
                    if ((id != BrewingExpansionMod.IncubatorId) && (nm != BrewingExpansionMod.IncubatorName)) continue;
                    if (!seen.Add(it.Pointer.ToInt64())) continue;

                    var input = IncubatorLayout.Input(it);
                    var inputItems = input?.childItems;
                    if (inputItems == null || inputItems.Count == 0) continue;
                    var readySeeds = new List<GameItem>();
                    for (int k = 0; k < inputItems.Count; k++)
                    {
                        var seed = inputItems[k];
                        if (seed == null) continue;
                        string seedId = seed.identifier ?? "";
                        if (BrewingExpansionMod.IsOurSeed(seedId)
                            && !string.IsNullOrEmpty(BrewingExpansionMod.GetFruitIdForSeed(seedId)))
                            readySeeds.Add(seed);
                    }
                    if (readySeeds.Count == 0) continue;

                    var battery = Battery(it);
                    int energyBefore = battery?.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
                    if (battery == null || !PowerHelper.DrawPowerSource(battery, EnergyPerNight)) continue;
                    poweredIncubators++;
                    int energyAfter = battery.GetTagReadonly("power_source_item_energy")?.valueInt ?? -1;
                    MelonLogger.Msg("[BE] 种子孵化器电量 " + energyBefore + " -> " + energyAfter);
                    byOwner[it] = readySeeds;
                }
                if (poweredIncubators > 0)
                    MelonLogger.Msg("[BE] " + poweredIncubators + " 台种子孵化器各耗电 " + EnergyPerNight + " kJ");
                if (byOwner.Count == 0) return;

                int totalProcessed = 0;
                foreach (var kv in byOwner)
                {
                    var owner = kv.Key;
                    var seeds = kv.Value;
                    int processed = 0;
                    for (int idx = 0; idx < seeds.Count && processed < quota; idx++)
                    {
                        var seed = seeds[idx];
                        if (seed == null) continue;
                        string seedId = seed.identifier ?? "";
                        string fruitId = BrewingExpansionMod.GetFruitIdForSeed(seedId);
                        if (string.IsNullOrEmpty(fruitId)) continue;

                        try
                        {
                            GameItem fruit = null;
                            try { fruit = DirectoryMaster.Item(fruitId, true); } catch { }
                            if (fruit == null) continue;

                            bool placed = false;
                            try
                            {
                                var output = IncubatorLayout.Output(owner);
                                if (output != null)
                                {
                                    SlotMarker marker = output.TryFindOneValidInventorySlot(fruit);
                                    if (marker != null && marker.IsValid())
                                    {
                                        if (marker.itemGridShape != null) fruit.shape = marker.itemGridShape;
                                        placed = marker.AcceptUnchecked() > 0;
                                        if (placed) output.Validate();
                                    }
                                }
                            }
                            catch { }

                            if (!placed)
                                try { store.AddDirectSellingItemToTable(fruit, true); placed = true; } catch { }

                            if (placed)
                            {
                                try { seed.Destroy(); } catch { }
                                processed++; totalProcessed++;
                            }
                        }
                        catch { }
                    }
                }
                if (totalProcessed > 0)
                    MelonLogger.Msg("[BE] 孵化器共处理 " + totalProcessed + " 颗种子");
            }
            catch { }
            finally { NightIncubators.Clear(); }
        }
    }

    [HarmonyPatch(typeof(WineHelper), "FinishFermentation")]
    public static class PatchFinishFermentation
    { [HarmonyPostfix] public static void Postfix(GameItem item) { } }

    [HarmonyPatch(typeof(PlayerStore), "EndNight")]
    [HarmonyPriority(Priority.Low)]
    public static class PatchEndNightWineSweep
    {
        [HarmonyPostfix]
        public static void Postfix() { try { BrewingExpansionMod.SweepAllWineBottles("EndNight"); } catch { } }
    }

    [HarmonyPatch(typeof(PlayerStore), "SaveGame")]
    public static class PatchSaveBrewingState
    {
        [HarmonyPrefix]
        public static void Prefix() { BrewingExpansionMod.SaveData(); }
    }

    [HarmonyPatch(typeof(PlayerStore), "LoadGame")]
    public static class PatchLoadGameWineRefresh
    {
        // ★ 新增：LoadGame 之前同步注册中秋物品，让读档能找到模板
        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                // Saved items resolve their sprites while LoadGame is running.
                // Prepare the atlas before the first item is deserialized.
                BrewingExpansionMod.RegisterMidautumnItems();
                BrewingExpansionMod.EnsureSpriteAssetsReady();
                MelonLogger.Msg("[BE-LOAD] Prefix 进入，MidautumnRegistered="
                    + BrewingExpansionMod.MidautumnRegisteredPublic);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-LOAD] 注册异常: " + e.Message); }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                BrewingExpansionMod.EnsureSpriteAssetsReady();
                XIAOWOBrewingBarrel.BarrelMaintenanceEvents.ScheduleFullSweep();
                Lang.ApplyGameLocale();
                BrewingExpansionMod.RepairLegacyFigurines();
                BrewingExpansionMod.LoadData();
                XIAOWOBrewingBarrel.OuterRingFaction.EnsureRegistered();
                PatchScheduleWeeklyTrader.ResetScheduledDay();
                PatchQingyunDailyRestore.ResetAfterLoad();
                BrewingExpansionMod.RefreshOwnedCertificateDescriptions();
                BrewingExpansionMod.RefreshOwnedFilterDescriptions();
                BrewingExpansionMod.RefreshOwnedRecipePrices();
                NiniPhoneBook.EnsureContact();
                BrewingExpansionMod.Trader47LastSummonedDay = -1;
                PatchPhoneHandleCallNini.ResetAfterLoad();
                var loadedStore = PlayerStore.Instance;
                if (loadedStore != null && loadedStore.storeState == PlayerStore.StoreState.OPEN)
                {
                    PatchQueue47WithDailyClients.EnsureQueued(loadedStore, true);
                    PatchPhoneHandleCallNini.RestorePending(loadedStore);
                }
                MelonCoroutines.Start(RepairVisitorsAfterLoad());
                MelonCoroutines.Start(DelayedMidautumnRegister());
                MelonCoroutines.Start(DelayedRepairMidautumnBags());


                try

                {

                    var store = PlayerStore.Instance;
                    if (store != null)
                    {
                        var ids = store.futurStoreClientIdQueue;
                        var days = store.futurStoreClientDayQueue;
                        if (ids != null && days != null)
                            for (int i = ids.Count - 1; i >= 0; i--)
                                if (ids[i] == BrewingExpansionMod.SeedTrader47Id)
                                {
                                    ids.RemoveAt(i);
                                    if (i < days.Count) days.RemoveAt(i);
                                }
                    }
                }
                catch { }

                MelonCoroutines.Start(DelayedSweep());
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-LOAD] 读档后初始化失败: " + ex); }
        }
        private static System.Collections.IEnumerator DelayedMidautumnRegister()
        {
            yield return new WaitForSeconds(5f);
            if (!BrewingExpansionMod.MidautumnRegisteredPublic)
            {
                BrewingExpansionMod.RegisterMidautumnItems();   // 内部自己 Mark
            }
        }

        // ★ 新增：等延迟注册完成后再扫描修复
        private static System.Collections.IEnumerator DelayedRepairMidautumnBags()
        {
            // 先等 6 秒，让 DelayedMidautumnRegister（5 秒）先跑完
            yield return new WaitForSeconds(6.0f);

            // 如果 6 秒后还没注册上（理论上不会），再等 2 秒
            int safety = 0;
            while (!BrewingExpansionMod.MidautumnRegisteredPublic && safety < 4)
            {
                yield return new WaitForSeconds(2.0f);
                safety++;
            }

            if (!BrewingExpansionMod.MidautumnRegisteredPublic)
            {
                MelonLogger.Warning("[BE-REPAIR] 中秋物品尚未注册，放弃本次修复");
                yield break;
            }

            // 再等 1 秒，让物品 UI 初始化完
            yield return new WaitForSeconds(1.0f);

            BrewingExpansionMod.RepairMidautumnBags();
            BrewingExpansionMod.RepairCustomBagShapes();
            BrewingExpansionMod.RepairLegacyMidautumnBlindboxes();
            BrewingExpansionMod.RefreshOwnedSeedBlindboxes();
            BrewingExpansionMod.RefreshOwnedBatteryPrices();
            BrewingExpansionMod.RefreshOwnedPerfumePrices();
            BrewingExpansionMod.RefreshOwnedStoragePrices();
        }

        private static IEnumerator DelayedSweep()
        {
            yield return new WaitForSeconds(3f);
            BrewingExpansionMod.SweepAllWineBottles("LoadGame");
        }

        private static IEnumerator RepairVisitorsAfterLoad()
        {
            yield return new WaitForSeconds(1f);
            var store = PlayerStore.Instance;
            if (store == null || store.storeState != PlayerStore.StoreState.OPEN) yield break;
            PatchQueue47WithDailyClients.EnsureQueued(store, true);
            PatchPhoneHandleCallNini.RestorePending(store);
            PatchStock47OnArrival.TryStock();
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "BeginDay")]
    public static class PatchBeginDayWineRefresh
    {
        private static bool _thDumped = false;
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                BrewingExpansionMod.EnsureSpriteAssetsReady();
                XIAOWOBrewingBarrel.BarrelMaintenanceEvents.ScheduleFullSweep();

                Lang.ApplyGameLocale();   // ★ 加这一行
                BrewingExpansionMod.RefreshOwnedCertificateDescriptions();
                BrewingExpansionMod.RefreshOwnedFilterDescriptions();
                BrewingExpansionMod.RefreshOwnedRecipePrices();
                int gameDay = BrewingExpansionMod.GetCurrentGameDay();
                if (gameDay > 0 && BrewingExpansionMod.TotalGameDays != gameDay)
                {
                    BrewingExpansionMod.TotalGameDays = gameDay;
                    BrewingExpansionMod.SaveData();
                }
                // ★★★ 新增：开始新的一天时修复中秋背包（此时 UI 一定就绪）

                BrewingExpansionMod.SweepAllWineBottles("BeginDay");

                if (!_thDumped)
                {
                    BrewingExpansionMod.DumpAttractThresholds();
                    _thDumped = true;
                }

            }
            catch { }
        }

    }

    // 在四十七实际到店时上货。旧协程从入队开始只等待十分钟，队伍长时会提前超时。
    [HarmonyPatch(typeof(StoreUIManager), "OnNextClientArrived")]
    public static class PatchStock47OnArrival
    {
        private static long _stockedInstance;
        private static int _stockedDay = -1;

        [HarmonyPostfix]
        public static void Postfix() { TryStock(); }

        internal static void TryStock()
        {
            try
            {
                var store = PlayerStore.Instance;
                var instance = store?.currentClientInstance;
                var client = instance?.GetClientBlueprint();
                if (client == null || client.identifier != BrewingExpansionMod.SeedTrader47Id) return;

                long pointer = (long)instance.Pointer;
                int day = BrewingExpansionMod.GetCurrentGameDay();
                if (BrewingExpansionMod.Trader47StockedDay == day
                    || (pointer != 0 && pointer == _stockedInstance && day == _stockedDay)) return;

                // Older saves have no stocked-day marker. A leftover recipe or
                // manual is not evidence that any seeds were stocked.
                var all = store.FindAllItem(true);
                if (all != null)
                    for (int i = 0; i < all.Count; i++)
                        try
                        {
                            var item = all[i];
                            if (item == null || !item.IsTag(BrewingExpansionMod.Trader47StockTag)) continue;
                            if (BrewingExpansionMod.IsOwnedByPlayerPublic(item)) continue;
                            if (!BrewingExpansionMod.IsOurSeed(item.identifier)) continue;
                            if (item.parentInventory == null) continue;
                            _stockedInstance = pointer;
                            _stockedDay = day;
                            BrewingExpansionMod.Trader47StockedDay = day;
                            BrewingExpansionMod.SaveData();
                            return;
                        }
                        catch (Exception ex) { MelonLogger.Warning("[BE-NPC-47] 检查旧货失败: " + ex.Message); }

                int before = BrewingExpansionMod.Trader47StockList.Count;
                BrewingExpansionMod.StockSeeds47Now();
                bool stockedSeed = false;
                for (int i = before; i < BrewingExpansionMod.Trader47StockList.Count; i++)
                    if (BrewingExpansionMod.IsOurSeed(BrewingExpansionMod.Trader47StockList[i]?.identifier))
                    { stockedSeed = true; break; }
                if (stockedSeed)
                {
                    _stockedInstance = pointer;
                    _stockedDay = day;
                    BrewingExpansionMod.Trader47StockedDay = day;
                    BrewingExpansionMod.SaveData();
                    MelonLogger.Msg("[BE-NPC-47] 到店后已上架商品");
                }
                else MelonLogger.Warning("[BE-NPC-47] 到店后未能上架种子");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-NPC-47] 到店补货异常: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(StoreClientListDict), "CreateStoreClient")]
    public static class PatchCreateStoreClient
    {
        [HarmonyPrefix]
        public static bool Prefix(string identifier, ref StoreClient __result)
        {
            try
            {
                if (identifier == BrewingExpansionMod.WeeklyTraderId)
                { __result = BrewingExpansionMod.CreateWeeklyTrader(); return false; }
                if (identifier == BrewingExpansionMod.SeedTrader47Id)
                { __result = BrewingExpansionMod.CreateSeedTrader47(); return false; }
                if (identifier == XIAOWOBrewingBarrel.OuterRingFaction.AssassinId)
                { __result = XIAOWOBrewingBarrel.OuterRingFaction.CreateAssassin(); return false; }
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(StoreClientListDict), "GetClientDisplayName")]
    public static class PatchGetClientDisplayName
    {
        [HarmonyPrefix]
        public static bool Prefix(string identifier, ref string __result)
        {
            try
            {
                if (identifier == BrewingExpansionMod.WeeklyTraderId) { __result = BrewingExpansionMod.WeeklyTraderName; return false; }
                if (identifier == BrewingExpansionMod.SeedTrader47Id) { __result = BrewingExpansionMod.SeedTrader47Name; return false; }
                if (identifier == XIAOWOBrewingBarrel.OuterRingFaction.AssassinId)
                { __result = Lang.T("外环区刺客", "Outer Ring Assassin"); return false; }
            }
            catch { }
            return true;
        }
    }


    // 原版开门时先生成当天和预定访客；计数和第一位客人选定之前补入 MOD 访客。
    [HarmonyPatch(typeof(PlayerStore), "HandleFutureClientQueue")]
    public static class PatchFutureQueue47
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance)
        {
            PatchQueue47WithDailyClients.EnsureQueued(__instance, false);
            PatchPhoneHandleCallNini.RestorePending(__instance);
        }
    }

    // 再在开门结束时检查一次，处理读档后展示柜稍晚恢复的情况。
    [HarmonyPatch(typeof(PlayerStore), "OnShutterOpened")]
    public static class PatchQueue47WithDailyClients
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance)
        {
            EnsureQueued(__instance, false);
            PatchPhoneHandleCallNini.RestorePending(__instance);
        }

        internal static void EnsureQueued(PlayerStore __instance, bool allowSavedShowcase)
        {
            try
            {
                if (__instance == null) return;
                if (!XIAOWOBrewingBarrel.OuterRingFaction.CanSummonFortySeven) return;
                if (!BrewingExpansionMod.HasBrewerySign(allowSavedShowcase)) return;
                var mgr = __instance.storeClientManager;
                var stack = mgr?.clientStack;
                if (stack == null) return;
                int day = BrewingExpansionMod.GetCurrentGameDay();
                if (BrewingExpansionMod.Trader47FinishedDay == day ||
                    BrewingExpansionMod.Trader47LastSummonedDay == day)
                    return;
                if (__instance.currentClientInstance?.GetClientBlueprint()?.identifier == BrewingExpansionMod.SeedTrader47Id)
                {
                    BrewingExpansionMod.Trader47LastSummonedDay = day;
                    return;
                }
                for (int i = 0; i < stack.Count; i++)
                    if (stack[i] != null && (stack[i].identifier ?? "") == BrewingExpansionMod.SeedTrader47Id)
                    {
                        BrewingExpansionMod.Trader47LastSummonedDay = day;
                        return;
                    }

                var trader = BrewingExpansionMod.CreateSeedTrader47();
                if (trader == null) return;
                int previousCount = stack.Count;
                mgr.AddClient(trader);
                if (stack.Count <= previousCount) return;
                BrewingExpansionMod.Trader47LastSummonedDay = day;
                MelonLogger.Msg("[BE-NPC-47] 开门时已将四十七加入当天队尾；队列人数 "
                    + previousCount + " → " + stack.Count);
            }
            catch (Exception e) { MelonLogger.Error("[BE-NPC-47] 加入当天队列失败: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "DismissCurrentClient")]
    public static class PatchDismissTrigger47
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerStore __instance)
        {
            try
            {
                var id = __instance?.currentClientInstance?.GetClientBlueprint()?.identifier ?? "";
                int day = BrewingExpansionMod.GetCurrentGameDay();
                if (id == BrewingExpansionMod.SeedTrader47Id)
                    BrewingExpansionMod.Trader47FinishedDay = day;
                else if (id == BrewingExpansionMod.WeeklyTraderId)
                    BrewingExpansionMod.NiniPendingDay = -1;
                else return;
                BrewingExpansionMod.SaveData();
            }
            catch (Exception e)
            {
                // 这里只是记录访客状态，出错也不该中断 EndNight / Dismiss 流程。
                MelonLogger.Warning("[BE-NPC] 记录访客离场状态失败: " + e.Message);
            }
        }

        internal static bool IsNiniTradeItem(GameItem item)
        {
            if (item == null) return false;
            try
            {
                var types = item.itemTypes;
                if (types != null)
                    for (int i = 0; i < types.Count; i++)
                    {
                        string type = types[i];
                        if (type == "ALCOHOL" || type == "CIGARETTE" || type == "TOBACCO") return true;
                    }

                if (item.IsTag("CIGARETTE_TAG") || item.IsTag("TOBACCO_TAG")) return true;
                string id = item.identifier ?? "";
                return id.StartsWith("packet_", StringComparison.OrdinalIgnoreCase)
                    && id.IndexOf("cigarette", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance)
        {
            if (__instance != null) Clear47Stock(__instance);
        }

        private static void Clear47Stock(PlayerStore store)
        {
            try
            {
                int cleared = 0, skipped = 0;
                for (int i = BrewingExpansionMod.Trader47StockList.Count - 1; i >= 0; i--)
                {
                    var it = BrewingExpansionMod.Trader47StockList[i];
                    BrewingExpansionMod.Trader47StockList.RemoveAt(i);
                    if (it == null) continue;

                    bool owned = false;
                    try { owned = BrewingExpansionMod.IsOwnedByPlayerPublic(it); } catch { }
                    if (owned) { skipped++; continue; }

                    try { if (it.Pointer == IntPtr.Zero) continue; } catch { }

                    try
                    {
                        if (it.Pointer == IntPtr.Zero) continue;
                        it.Destroy();
                        cleared++;
                    }
                    catch { /* 静默忽略已销毁的物品 */ }
                }
                if (cleared > 0 || skipped > 0)
                    MelonLogger.Msg("[BE-NPC-47] Clear47Stock: 清掉 " + cleared + " 件，跳过 " + skipped + " 件（已买）");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-NPC-47] Clear47Stock 异常: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "EndNight")]
    [HarmonyPriority(Priority.Last)]
    public static class PatchScheduleWeeklyTrader
    {
        private static DateTime _lastEndNight = DateTime.MinValue;
        private const double EndNightCooldownSeconds = 5.0;

        public static void ResetScheduledDay() { _lastEndNight = DateTime.MinValue; }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var now = DateTime.Now;
                double elapsed = (now - _lastEndNight).TotalSeconds;

                if (elapsed < EndNightCooldownSeconds)
                {
                    MelonLogger.Msg("[BE-DAY] EndNight 重复触发（距上次 " + elapsed.ToString("F2") + " 秒），跳过");
                    return;
                }
                _lastEndNight = now;

                var store = PlayerStore.Instance;
                if (store == null) return;
                BrewingExpansionMod.TotalGameDays++;
                BrewingExpansionMod.SaveData();

                MelonLogger.Msg("[BE-DAY] 天数 +1 → " + BrewingExpansionMod.TotalGameDays);
            }
            catch { }
        }
    }

    internal static class NiniPhoneBook
    {
        private const string ContactMarker = "BrewingExpansion.Nini";

        internal static void EnsureContact()
        {
            try
            {
                if (!BrewingExpansionMod.NiniDialedEver) return;
                var store = PlayerStore.Instance;
                var contacts = store?.PhoneClientDict;
                if (contacts == null) return;
                long number = BrewingExpansionMod.WeeklyTraderPhoneNumber;
                StorePhoneClient contact;
                if (contacts.ContainsKey(number))
                    contact = contacts[number];
                else
                {
                    contact = new StorePhoneClient();
                    contacts[number] = contact;
                }
                if (contact == null) return;
                contact.phoneClientType = StorePhoneClient.PhoneClientType.None;
                contact.phoneState = StorePhoneClient.PhoneState.Regular;
                contact.displayName = BrewingExpansionMod.WeeklyTraderName;
                contact.locID = ContactMarker;
                contact.dialogFuncId = ContactMarker;
                contact.dialedBefore = true;
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PHONE] 添加泥泥快捷拨号失败: " + e.Message); }
        }

        internal static bool IsNini(StorePhoneClient contact)
        {
            try { return contact != null && contact.dialogFuncId == ContactMarker; }
            catch { return false; }
        }
    }

    [HarmonyPatch(typeof(PhoneBookUIManager), "OpenUI")]
    public static class PatchNiniPhoneBookOpen
    {
        [HarmonyPrefix]
        public static void Prefix() { NiniPhoneBook.EnsureContact(); }
    }

    [HarmonyPatch(typeof(StorePhoneClient), "GetLocalizedDisplayName")]
    public static class PatchNiniPhoneBookName
    {
        [HarmonyPrefix]
        public static bool Prefix(StorePhoneClient __instance, ref string __result)
        {
            if (!NiniPhoneBook.IsNini(__instance)) return true;
            __result = BrewingExpansionMod.WeeklyTraderName;
            return false;
        }
    }

    // 原版快捷拨号走 AutoCall → StartPhoneDialog，不会经过手动拨号的 HandleCall。
    [HarmonyPatch(typeof(PhoneUIManager), "AutoCall")]
    public static class PatchNiniQuickDial
    {
        [HarmonyPrefix]
        public static bool Prefix(PhoneUIManager __instance, long number)
        {
            if (number != BrewingExpansionMod.WeeklyTraderPhoneNumber) return true;
            try
            {
                __instance.StopCall();
                __instance.HandleCall(number);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PHONE] 泥泥快捷拨号失败: " + e.Message); }
            return false;
        }
    }

    // 原版 StartMainDialogue 即使 StartDialogue 返回 false，也会把对话标记为已开始。
    // 泥泥可能在电话结束前立刻到店，因此仅对泥泥保留一次短暂重试。
    [HarmonyPatch(typeof(StoreClient), "StartMainDialogue")]
    public static class PatchNiniArrivalDialogue
    {
        [HarmonyPrefix]
        public static bool Prefix(StoreClient __instance)
        {
            if (__instance == null || __instance.identifier != BrewingExpansionMod.WeeklyTraderId)
                return true;
            try
            {
                if (__instance.isMainDialogueStarted) return false;
                var dialog = __instance.mainDialogue;
                var ui = DialogUIManager.Instance;
                if (dialog != null && ui != null && ui.StartDialogue(dialog))
                    __instance.isMainDialogueStarted = true;
                else
                    MelonCoroutines.Start(Retry(__instance));
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PHONE] 泥泥到店台词启动失败: " + e.Message); }
            return false;
        }

        private static IEnumerator Retry(StoreClient client)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                yield return new WaitForSeconds(0.5f);
                try
                {
                    var current = PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();
                    if (current == null || current.identifier != BrewingExpansionMod.WeeklyTraderId)
                        yield break;
                    if (client.isMainDialogueStarted) yield break;
                    var ui = DialogUIManager.Instance;
                    if (client.mainDialogue != null && ui != null && ui.StartDialogue(client.mainDialogue))
                    {
                        client.isMainDialogueStarted = true;
                        MelonLogger.Msg("[BE-PHONE] 泥泥到店台词重试成功");
                        yield break;
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[BE-PHONE] 泥泥到店台词重试异常: " + e.Message);
                    yield break;
                }
            }
            MelonLogger.Warning("[BE-PHONE] 泥泥到店台词等待超时");
        }
    }

    [HarmonyPatch(typeof(PhoneUIManager), "HandleCall")]
    public static class PatchPhoneHandleCallNini
    {
        private static DateTime _lastDialTime = DateTime.MinValue;
        private static readonly object _dialLock = new object();
        private const int DialCooldownDays = 5;
        private static int _loadGeneration;

        internal static void ResetAfterLoad()
        {
            lock (_dialLock)
            {
                _loadGeneration++;
                _lastDialTime = DateTime.MinValue;
            }
        }

        internal static void RestorePending(PlayerStore store)
        {
            try
            {
                int day = BrewingExpansionMod.GetCurrentGameDay();
                if (store == null || BrewingExpansionMod.NiniPendingDay != day) return;
                var mgr = store.storeClientManager;
                var stack = mgr?.clientStack;
                if (stack == null) return;
                if (store.currentClientInstance?.GetClientBlueprint()?.identifier == BrewingExpansionMod.WeeklyTraderId)
                    return;
                for (int i = 0; i < stack.Count; i++)
                    if (stack[i] != null && stack[i].identifier == BrewingExpansionMod.WeeklyTraderId)
                        return;
                var nini = BrewingExpansionMod.CreateWeeklyTrader();
                if (nini == null) return;
                int previousCount = stack.Count;
                mgr.AddNextClient(nini);
                if (stack.Count > previousCount)
                    MelonLogger.Msg("[BE-PHONE] 读档后恢复尚未到访的泥泥");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PHONE] 恢复泥泥失败: " + e.Message); }
        }

        [HarmonyPrefix]
        public static bool Prefix(PhoneUIManager __instance, long currentNumber)
        {
            try
            {
                if (__instance == null) return true;
                if (currentNumber != BrewingExpansionMod.WeeklyTraderPhoneNumber) return true;

                lock (_dialLock)
                {
                    if ((DateTime.Now - _lastDialTime).TotalSeconds < 5.0) return false;
                    _lastDialTime = DateTime.Now;
                }
                bool hasCard = false;
                try
                {
                    var store0 = PlayerStore.Instance;
                    if (store0 != null)
                    {
                        var all = store0.FindAllItem(true);
                        if (all != null)
                            for (int i = 0; i < all.Count; i++)
                            {
                                var it = all[i];
                                if (it != null && BrewingExpansionMod.IsNiniCard(it)) { hasCard = true; break; }
                            }
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[BE-PHONE] 检查名片异常: " + e.Message); }

                if (!hasCard)
                {
                    MelonLogger.Msg("[BE-PHONE] 没有泥泥的名片，无法拨号");
                    MelonCoroutines.Start(NoCardCallCoroutine(__instance));
                    return false;
                }
                int lastDay = BrewingExpansionMod.LastNiniDialDay;
                int day = BrewingExpansionMod.GetCurrentGameDay();
                if (lastDay >= 0 && day - lastDay >= 0 && day - lastDay < DialCooldownDays)
                {
                    int remain = DialCooldownDays - (day - lastDay);
                    MelonLogger.Msg("[BE-PHONE] 最近已经拨过了，" + remain + " 天后再打");
                    MelonCoroutines.Start(CooldownCallCoroutine(__instance, remain));
                    return false;
                }

                MelonLogger.Msg("[BE-PHONE] 拨号: " + currentNumber + " → 泥泥");
                MelonCoroutines.Start(AnswerNiniCoroutine(__instance, _loadGeneration));
                return false;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-PHONE] HandleCall Prefix 异常: " + e.Message);
                return true;
            }
        }

        private static IEnumerator AnswerNiniCoroutine(PhoneUIManager ui, int generation)
        {
            PlayAudioField(ui, "phoneCalling");
            yield return new WaitForSeconds(2.5f);
            if (generation != _loadGeneration) yield break;

            StopAudioField(ui, "phoneCalling");
            PlayAudioField(ui, "phoneClientPickup");
            yield return new WaitForSeconds(0.5f);
            if (generation != _loadGeneration) yield break;

            ShowPhoneLine(Lang.T("……喂？", "...Hello?"));
            yield return new WaitForSeconds(1.5f);
            if (generation != _loadGeneration) yield break;
            ShowPhoneLine(Lang.T("有好酒就等着，我这就来。", "Got good wine? Wait for me — I'm on my way."));
            yield return new WaitForSeconds(1.5f);
            if (generation != _loadGeneration) yield break;

            // 电话界面尚在播报时，排在队首的泥泥会立即到店；原版此时
            // StartDialogue 可能因电话对话占用而失败，并永久标记台词已开始。
            PlayAudioField(ui, "phoneClientDrop");
            yield return new WaitForSeconds(0.3f);
            if (generation != _loadGeneration) yield break;
            try { ui.CloseUI(); } catch { }
            yield return new WaitForSeconds(0.3f);
            if (generation != _loadGeneration) yield break;

            var store = PlayerStore.Instance;
            if (store == null) yield break;

            bool already = false;
            try
            {
                var stack = store.storeClientManager?.clientStack;
                if (stack != null)
                    for (int i = 0; i < stack.Count; i++)
                    {
                        var c = stack[i];
                        if (c != null && (c.identifier ?? "") == BrewingExpansionMod.WeeklyTraderId)
                        { already = true; break; }
                    }
            }
            catch { }

            bool queued = already;
            if (!already)
            {
                var nini = BrewingExpansionMod.CreateWeeklyTrader();
                if (nini != null)
                {
                    try
                    {
                        int previousCount = store.storeClientManager.clientStack?.Count ?? -1;
                        store.storeClientManager.AddNextClient(nini);
                        queued = store.storeClientManager.clientStack != null &&
                            store.storeClientManager.clientStack.Count > previousCount;
                        if (queued) MelonLogger.Msg("[BE-PHONE] 泥泥已召唤");
                    }
                    catch (Exception e) { MelonLogger.Warning("[BE-PHONE] AddNextClient 失败: " + e.Message); }
                }
            }
            else
            {
                MelonLogger.Msg("[BE-PHONE] 泥泥已在队列里，跳过");
            }

            if (queued)
            {
                int day = BrewingExpansionMod.GetCurrentGameDay();
                BrewingExpansionMod.LastNiniDialDay = day;
                BrewingExpansionMod.NiniPendingDay = day;
                BrewingExpansionMod.NiniDialedEver = true;
                NiniPhoneBook.EnsureContact();
                BrewingExpansionMod.SaveData();
            }
        }

        private static IEnumerator CooldownCallCoroutine(PhoneUIManager ui, int remainDays)
        {
            PlayAudioField(ui, "phoneCalling");
            yield return new WaitForSeconds(1.5f);

            StopAudioField(ui, "phoneCalling");
            PlayAudioField(ui, "phoneClientPickup");
            yield return new WaitForSeconds(0.5f);

            ShowPhoneLine(Lang.T(
    "我最近已经来过一次了，" + remainDays + " 天后再来吧。",
    "I already came by recently. Try again in " + remainDays + " days."));
            yield return new WaitForSeconds(2.0f);

            PlayAudioField(ui, "phoneClientDrop");
            yield return new WaitForSeconds(0.3f);
            try { ui.CloseUI(); } catch { }
        }

        private static IEnumerator NoCardCallCoroutine(PhoneUIManager ui)
        {
            PlayAudioField(ui, "phoneCalling");
            yield return new WaitForSeconds(1.5f);

            StopAudioField(ui, "phoneCalling");
            PlayAudioField(ui, "phoneNoNumber");
            yield return new WaitForSeconds(0.5f);

            ShowPhoneLine(Lang.T(
    "您拨打的号码是空号，请查证后再拨。",
    "The number you have dialed does not exist. Please check and try again."));
            yield return new WaitForSeconds(2.5f);

            PlayAudioField(ui, "phoneClientDrop");
            yield return new WaitForSeconds(0.3f);
            try { ui.CloseUI(); } catch { }
        }

        private static void PlayAudioField(PhoneUIManager ui, string fieldName)
        {
            try
            {
                var f = typeof(PhoneUIManager).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;
                var audio = f.GetValue(ui);
                if (audio == null) return;
                var m = audio.GetType().GetMethod("Play", Type.EmptyTypes);
                m?.Invoke(audio, null);
            }
            catch { }
        }

        private static void StopAudioField(PhoneUIManager ui, string fieldName)
        {
            try
            {
                var f = typeof(PhoneUIManager).GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;
                var audio = f.GetValue(ui);
                if (audio == null) return;
                var m = audio.GetType().GetMethod("Stop", Type.EmptyTypes);
                m?.Invoke(audio, null);
            }
            catch { }
        }

        private static void ShowPhoneLine(string text)
        {
            try
            {
                var mgr = DialoguePhoneUIManager.Instance;
                if (mgr == null || mgr.clientBubble == null || mgr.clientBubble.textfield == null) return;
                // 原版 PlayClientSingleText 会从 currentClientInstance 读取称呼。
                // 电话访客尚未到店，故直接用原版电话气泡显示文字和泥泥的名字。
                mgr.clientBubble.gameObject.SetActive(true);
                mgr.clientBubble.textfield.text = text;
                if (mgr.clientNameBubble != null) mgr.clientNameBubble.SetActive(true);
                if (mgr.clientNameTMP != null)
                    mgr.clientNameTMP.text = BrewingExpansionMod.WeeklyTraderName;
                MelonLogger.Msg("[BE-PHONE] 台词: " + text);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[BE-PHONE] ShowPhoneLine 异常: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "SellItem")]
    public static class PatchSellItemGiftCert
    {
        private static long _lastItemPtr = 0;
        private static DateTime _lastItemTime = DateTime.MinValue;
        private const double SellItemDedupSeconds = 0.05;

        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance, GameItem item)
        {
            try
            {
                if (__instance == null || item == null) return;

                long ptr = 0;
                try { ptr = (long)item.Pointer; } catch { }
                DateTime now = DateTime.Now;
                if (ptr != 0 && ptr == _lastItemPtr
                    && (now - _lastItemTime).TotalSeconds < SellItemDedupSeconds)
                {
                    return;
                }
                _lastItemPtr = ptr;
                _lastItemTime = now;

                StoreClient client = null;
                try
                {
                    var ci = __instance.currentClientInstance;
                    if (ci != null) client = ci.GetClientBlueprint();
                }
                catch { }

                if (client == null) return;
                if ((client.identifier ?? "") != BrewingExpansionMod.WeeklyTraderId) return;

                long itemVal = 0;
                try { itemVal = item.unitValue; } catch { }

                BrewingExpansionMod.CurrentTraderSaleTotal += itemVal;
                long total = BrewingExpansionMod.CurrentTraderSaleTotal;
                BrewingExpansionMod.SaveData();

                MelonLogger.Msg("[BE-CERT] 泥泥累计 = " + total + "（+" + itemVal + "）");

                if (!BrewingExpansionMod.BasicCertGifted
                    && total >= BrewingExpansionMod.BasicCertGiftThreshold)
                {
                    MelonLogger.Msg("[BE-CERT] ★★★ 触发初级证书赠送（累计 " + total + "）");
                    if (BrewingExpansionMod.GiveCertToPlayer(
                        BrewingExpansionMod.BasicCertId,
                        BrewingExpansionMod.BasicCertName,
                        BrewingExpansionMod.BasicCertSpriteKey))
                    {
                        BrewingExpansionMod.BasicCertGifted = true;
                        BrewingExpansionMod.CurrentTraderSaleTotal -= BrewingExpansionMod.BasicCertGiftThreshold;
                        total = BrewingExpansionMod.CurrentTraderSaleTotal;
                        BrewingExpansionMod.SaveData();
                        BrewingExpansionMod.SetTraderDialogue(client, BrewingExpansionMod.BasicCertGiftDialogue, "初");
                        MelonLogger.Msg("[BE-CERT] 初级触发后，剩余累计 = " + total);
                    }
                }

                if (BrewingExpansionMod.BasicCertGifted
                    && !BrewingExpansionMod.MidCertGifted
                    && total >= BrewingExpansionMod.MidCertGiftThreshold)
                {
                    MelonLogger.Msg("[BE-CERT] ★★★ 触发中级证书赠送（累计 " + total + "）");
                    if (BrewingExpansionMod.GiveCertToPlayer(
                        BrewingExpansionMod.MidCertId,
                        BrewingExpansionMod.MidCertName,
                        BrewingExpansionMod.MidCertSpriteKey))
                    {
                        BrewingExpansionMod.MidCertGifted = true;
                        BrewingExpansionMod.CurrentTraderSaleTotal -= BrewingExpansionMod.MidCertGiftThreshold;
                        total = BrewingExpansionMod.CurrentTraderSaleTotal;
                        BrewingExpansionMod.SaveData();
                        BrewingExpansionMod.SetTraderDialogue(client, BrewingExpansionMod.MidCertGiftDialogue, "中");
                        MelonLogger.Msg("[BE-CERT] 中级触发后，剩余累计 = " + total);
                    }
                }

                if (BrewingExpansionMod.MidCertGifted
                    && !BrewingExpansionMod.HighCertGifted
                    && total >= BrewingExpansionMod.HighCertGiftThreshold)
                {
                    MelonLogger.Msg("[BE-CERT] ★★★ 触发高级证书赠送（累计 " + total + "）");
                    if (BrewingExpansionMod.GiveCertToPlayer(
                        BrewingExpansionMod.HighCertId,
                        BrewingExpansionMod.HighCertName,
                        BrewingExpansionMod.HighCertSpriteKey))
                    {
                        BrewingExpansionMod.HighCertGifted = true;
                        BrewingExpansionMod.CurrentTraderSaleTotal -= BrewingExpansionMod.HighCertGiftThreshold;
                        total = BrewingExpansionMod.CurrentTraderSaleTotal;
                        BrewingExpansionMod.SaveData();
                        BrewingExpansionMod.SetTraderDialogue(client, BrewingExpansionMod.HighCertGiftDialogue, "高");
                        MelonLogger.Msg("[BE-CERT] 高级触发后，剩余累计 = " + total);
                    }
                }

                if (BrewingExpansionMod.HighCertGifted)
                {
                    BrewingExpansionMod.SetTraderDialogue(client, BrewingExpansionMod.WeeklyTraderAlreadyGiftedDialogue, "已");
                }
                else if (!BrewingExpansionMod.BasicCertGifted
                    && total < BrewingExpansionMod.BasicCertGiftThreshold)
                {
                    BrewingExpansionMod.SetTraderDialogue(client, BrewingExpansionMod.WeeklyTraderNotEnoughDialogue, "未");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-CERT] SellItem 异常: " + e.Message); }
        }
    }

    // Existing saves may restore the pitcher's 6 L capacity tag. The moisture farm
    // calls GetFreeCapacity before filling, so repair the tag at that boundary.
    [HarmonyPatch(typeof(WaterHelper), "GetFreeCapacity")]
    public static class PatchClearTankFreeCapacity
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem item)
        {
            try { BrewingExpansionMod.EnsureClearWaterTankCapacity(item); }
            catch (Exception e) { MelonLogger.Warning("[BE-TANK] 修复容量失败: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(WaterFeatureHelper), "UpdateWaterFeature")]
    public static class PatchTankWaterFeature
    {
        [HarmonyPrefix]
        public static bool Prefix(GameItem gameItem)
        {
            try
            {
                if (BrewingExpansionMod.IsOurWaterContainer(gameItem)) return false;
            }
            catch { }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "EndNight")]
    public static class PatchQingyunDailyRestore
    {
        private static DateTime _lastRun = DateTime.MinValue;
        private const double CooldownSeconds = 3.0;

        public static void ResetAfterLoad() { _lastRun = DateTime.MinValue; }

        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                var now = DateTime.Now;
                if ((now - _lastRun).TotalSeconds < CooldownSeconds) return;

                var store = PlayerStore.Instance;
                if (store == null) return;
                var all = store.FindAllItem(true);
                if (all == null) return;
                _lastRun = now;

                int countQY = 0, countNL = 0, countYF = 0, countPurifiers = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    var item = all[i];
                    if (item == null) continue;
                    try
                    {

                    if (BrewingExpansionMod.IsQingyunFilter(item))
                    {
                        try
                        {
                            int cur = DurabilityHelper.GetRealDurability(item);
                            int max = DurabilityHelper.GetMaxDurability(item);
                            if (cur < max)
                            {
                                DurabilityHelper.ChangeDurability(item, BrewingExpansionMod.QingyunFilterDailyRestore);
                                countQY++;
                            }
                        }
                        catch { }
                        continue;
                    }

                    if (BrewingExpansionMod.IsNingluFilter(item))
                    {
                        try
                        {
                            int cur = DurabilityHelper.GetRealDurability(item);
                            int max = DurabilityHelper.GetMaxDurability(item);
                            if (cur < max)
                            {
                                DurabilityHelper.ChangeDurability(item, BrewingExpansionMod.NingluFilterDailyRestore);
                                countNL++;
                            }
                        }
                        catch { }
                        continue;
                    }

                    if (BrewingExpansionMod.IsYingfeiFilter(item))
                    {
                        try
                        {
                            int cur = DurabilityHelper.GetRealDurability(item);
                            int max = DurabilityHelper.GetMaxDurability(item);
                            if (cur < max)
                            {
                                DurabilityHelper.ChangeDurability(item, BrewingExpansionMod.YingfeiFilterDailyRestore);
                                countYF++;
                            }
                        }
                        catch { }
                        continue;
                    }

                    try
                    {
                        if (XIAOWOBrewingBarrel.XuxuWaterPurifier.RestoreAtEndNight(item))
                            countPurifiers++;
                    }
                    catch (Exception e) { MelonLogger.Warning("[BE-PURIFIER] 每日恢复失败: " + e.Message); }
                    }
                    catch (Exception e) { MelonLogger.Warning("[BE-NIGHT] 跳过异常滤芯或净水器: " + e.Message); }
                }

                if (countQY > 0) MelonLogger.Msg("[BE-QY] 每日恢复完成，共 " + countQY + " 个青筠");
                if (countNL > 0) MelonLogger.Msg("[BE-NL] 每日恢复完成，共 " + countNL + " 个凝露");
                if (countYF > 0) MelonLogger.Msg("[BE-YF] 每日恢复完成，共 " + countYF + " 个樱绯");
                if (countPurifiers > 0) MelonLogger.Msg("[BE-PURIFIER] 每日恢复完成，共 " + countPurifiers + " 个净水器");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-QY] 每日恢复异常: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(WaterFeatureHelper), "OnLiquidChanged")]
    public static class PatchTankLiquidChanged
    {
        [HarmonyPostfix]
        public static void Postfix(GameItem target)
        {
            try
            {
                if (BrewingExpansionMod.IsOurWaterContainer(target))
                    BrewingExpansionMod.ClearSpriteModifierCallbacks(target);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(WaterFeatureHelper), "OnLiquidContainerPourInto")]
    public static class PatchTankPourInto
    {
        [HarmonyPostfix]
        public static void Postfix(GameItem target)
        {
            try
            {
                if (BrewingExpansionMod.IsOurWaterContainer(target))
                    BrewingExpansionMod.ClearSpriteModifierCallbacks(target);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameItem), "SetSprite")]
    public static class PatchClearTankSetSprite
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem __instance, ref string __1)
        {
            try
            {
                if (__instance == null) return;
                string key = BrewingExpansionMod.GetOurWaterContainerSpriteKey(__instance);
                if (string.IsNullOrEmpty(key)) return;
                __1 = key;
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "GetCurrentStoreAttractiveness")]
    public static class PatchStoreAttractiveness
    {
        private static int _lastLoggedResult = -1;

        [HarmonyPostfix]
        public static void Postfix(PlayerStore __instance, ref int __result)
        {
            try
            {
                if (__instance == null) return;

                int baseVal = 0;
                try { baseVal = __instance.baseStoreAttractiveness; } catch { }
                if (baseVal <= 0) baseVal = __result;

                int bonus = BrewingExpansionMod.CalculateDecorationAttractiveness();
                int finalVal = baseVal + bonus;

                if (finalVal > __result) __result = finalVal;

                if (__result != _lastLoggedResult)
                {
                    MelonLogger.Msg("[BE-DECOR] 吸引力 基础=" + baseVal + " + " + bonus + " = " + __result);
                    _lastLoggedResult = __result;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(GameItem), "SetSprite")]
    public static class PatchXuxuBatterySetSpriteReplace
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem __instance, ref string __0, ref string __1)
        {
            try
            {
                // 原版充能器按电量写入原版 battery 图标。我们的电池
                // 必须改用自己的图标与空 atlas 路径，避免充能格显示问号。
                if (__1 != null && __1.StartsWith("be_xuxu_", StringComparison.Ordinal)) return;
                if (!XIAOWOBrewingBarrel.XuxuBatteryVisuals.TryGetKey(__instance, out var key)) return;
                __0 = "";
                __1 = key;
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PWR] 设置电池贴图失败: " + e.Message); }
        }
    }

    // The leave-store backpack picker builds separate choice items. Repair
    // their footprint before the native response grid places them.
    [HarmonyPatch(typeof(EmporiumEntry), nameof(EmporiumEntry.PrepChoice))]
    public static class PatchCustomBagChoiceShape
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem choiceItem, string name)
        {
            try
            {
                if (BrewingExpansionMod.EnsureCustomBagShape(choiceItem))
                    MelonLogger.Msg("[BE-BAG] 选择界面背包占格已更新: " + (name ?? choiceItem?.name));
            }
            catch (Exception ex) { MelonLogger.Warning("[BE-BAG] 选择界面占格修复失败: " + ex.Message); }
        }
    }

    // ★ 诊断：双击玉兔怀袖时打印所有关键字段

    // ★ 拦截 DirectoryMaster.Item：5 个装饰类中秋物品从自定义工厂拿
    [HarmonyPatch]
    public static class PatchDirectoryMasterMidautumn
    {
        [HarmonyPrepare]
        public static bool Prepare()
        {
            return TargetMethod() != null;
        }

        [HarmonyTargetMethod]
        public static System.Reflection.MethodBase TargetMethod()
        {
            try
            {
                var t = typeof(DirectoryMaster);
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static))
                {
                    if (m.Name != "Item") continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2
                        && ps[0].ParameterType == typeof(string)
                        && ps[1].ParameterType == typeof(bool)
                        && m.ReturnType == typeof(GameItem))
                        return m;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[BE-MID] TargetMethod: " + e.Message); }
            return null;
        }

        [HarmonyPrefix]
        public static bool Prefix(string identifier, bool isOwned, ref GameItem __result)
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
    }

    [HarmonyPatch(typeof(GameItem), "SetSpriteQuick")]
    public static class PatchXuxuBatterySetSpriteQuick
    {
        [HarmonyPrefix]
        public static void Prefix(GameItem __instance, ref string __0, ref string __1)
        {
            try
            {
                if (__1 != null && __1.StartsWith("be_xuxu_", StringComparison.Ordinal)) return;
                if (!XIAOWOBrewingBarrel.XuxuBatteryVisuals.TryGetKey(__instance, out var key)) return;
                __0 = "";
                __1 = key;
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PWR] 快速设置电池贴图失败: " + e.Message); }
        }
    }

    public static class BrewerStarterPerk
    {
        public const string Id = "be_brewer_starter";
        private static bool grantedThisStart;
        private static Sprite iconSprite;
        private static Texture2D iconTexture;

        public static string DisplayName => Lang.T("xuxu赞助", "xuxu Sponsorship");
        public static string Description => Lang.T(
            "xuxu为你的酿酒事业提供赞助。开局获得一台水质扫描仪和一台种子孵化器。",
            "xuxu sponsors your brewing venture. Start with an AquaScan and a Seed Incubator.");

        public static void Register()
        {
            var perks = StartingPerkList.Perks;
            if (perks == null) return;
            for (int i = 0; i < perks.Count; i++)
                if (perks[i] != null && perks[i].id == Id)
                {
                    perks[i].cost = 1;
                    return;
                }

            var perk = new StartingPerk();
            perk.id = Id;
            perk.cost = 1;
            perk.type = StartingPerk.StartingPerkType.POSITIVE;
            perk.rawName = DisplayName;
            perk.rawDescription = Description;
            perks.Add(perk);
            MelonLogger.Msg("[BE-PERK] 已注册xuxu赞助，消耗 1 特性点");
        }

        public static void ResetGrant() { grantedThisStart = false; }

        private static bool HasSelection(Il2CppSystem.Collections.Generic.List<StartingPerk> perks)
        {
            if (perks == null) return false;
            for (int i = 0; i < perks.Count; i++)
                if (perks[i] != null && perks[i].id == Id) return true;
            return false;
        }

        public static void GrantInitialItems()
        {
            if (grantedThisStart) return;
            bool selected = HasSelection(NewGameData.Instance?.startingPerks)
                || HasSelection(PlayerStore.Instance?.startingPerks);
            if (!selected) return;

            var emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.invElement == null)
            {
                MelonLogger.Warning("[BE-PERK] 开局库存尚未就绪，无法发放酿酒师入门物品");
                return;
            }

            try { BrewingExpansionMod.EnsureStarterItemSprites(); }
            catch (Exception e) { MelonLogger.Warning("[BE-PERK] 开局贴图初始化失败: " + e.Message); }
            grantedThisStart = true;
            int placed = 0;
            placed += Grant(emporium, "aquascan", 1);
            placed += Grant(emporium, BrewingExpansionMod.IncubatorId, 1);
            MelonLogger.Msg("[BE-PERK] xuxu赞助已发放 " + placed + "/2 件开局物品");
        }

        public static int Grant(EmporiumEntry emporium, string id, int count)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    // The directory entry is an empty kit; the vanilla helper fills it with water_test_srtip.
                    var item = id == "water_test_container"
                        ? PreBuiltItemHelper.FullWaterTestStripBox()
                        : DirectoryMaster.Item(id, true);
                    if (item == null) { MelonLogger.Warning("[BE-PERK] 无法创建物品 " + id); continue; }
                    if (id == BrewingExpansionMod.ClearWaterTankId)
                        item.shape = new GridShapeBuilder(2, 3).SetDataFill(2, 3, 1).Build();
                    else if (id == BrewingExpansionMod.BrewerySignId)
                        item.shape = new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build();
                    // TryAcceptAll returns the quantity still unplaced, not the accepted quantity.
                    int remaining = GraphUtils.TryAcceptAll(emporium.invElement.Cast<GraphNodeStorage>(), item);
                    if (remaining > 0 && emporium.backInvinvElement != null)
                        remaining = GraphUtils.TryAcceptAll(emporium.backInvinvElement.Cast<GraphNodeStorage>(), item);
                    if (remaining == 0) placed++;
                    else MelonLogger.Warning("[BE-PERK] 库存空间不足，未能放入 " + id);
                }
                catch (Exception e) { MelonLogger.Warning("[BE-PERK] 发放 " + id + " 失败: " + e.Message); }
            }
            return placed;
        }

        public static Sprite GetIcon()
        {
            if (iconSprite != null) return iconSprite;
            try
            {
                using (var stream = typeof(BrewingExpansionMod).Assembly.GetManifestResourceStream(
                    "BrewingExpansion.Textures.xuxu赞助.png"))
                {
                    if (stream == null) return null;
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        iconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(iconTexture, memory.ToArray())) return null;
                    }
                }
                iconTexture.filterMode = FilterMode.Point;
                iconTexture.wrapMode = TextureWrapMode.Clamp;
                iconTexture.Apply(false, false);
                UnityEngine.Object.DontDestroyOnLoad(iconTexture);
                iconSprite = Sprite.Create(iconTexture,
                    new Rect(0, 0, iconTexture.width, iconTexture.height),
                    new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                iconSprite.name = Id;
                UnityEngine.Object.DontDestroyOnLoad(iconSprite);
            }
            catch (Exception e) { MelonLogger.Warning("[BE-PERK] 图标加载失败: " + e.Message); }
            return iconSprite;
        }

        public static void RegisterIcon()
        {
            var icons = StartingPerkIconLoader.perkIcons;
            if (icons == null || icons.ContainsKey(Id)) return;
            var icon = GetIcon();
            if (icon != null) icons.Add(Id, icon);
        }
    }

    // Save a vanilla start type: the game's native save-list code assumes values 0-13.
    // A modData marker distinguishes this profile from the ordinary apprentice.
    public static class NewBrewerStart
    {
        public const int LegacyStartTypeValue = 14;
        public const string SaveMarker = "be_new_brewer_start";
        private const string TabName = "BE_NewBrewerStart";
        private static bool selected;
        private static bool grantedThisStart;
        public static bool IsSelected => selected;

        public static bool HasSaveMarker(PlayerStore player)
        {
            try { return player?.modData != null && player.modData.ContainsKey(SaveMarker)
                && player.modData[SaveMarker] == "1"; }
            catch { return false; }
        }

        public static string DisplayName => Lang.T("新人酿酒师", "Novice Brewer");
        public static string Description => Lang.T(
            "你是一个专业的酿酒师，从小你就听说过xuxu的传说，也想走上酿酒的道路。现在，你可以开始了。",
            "You are a professional brewer. Since childhood, you have heard the legend of xuxu and dreamed of following the brewer's path. Now you can begin.");
        public static string StartingEquipment => Lang.T(
            "- 普通酵母 ×4\n- 空酒瓶 ×4\n- 清酿澄水箱 ×1\n- 酿酒灯牌 ×1\n- 水霓虹灯牌 ×1\n- 普通过滤器 ×1\n- 水质检测试剂盒 ×1",
            "- Regular Yeast ×4\n- Empty Wine Bottle ×4\n- Clear-Brew Settling Tank ×1\n- Brewery Sign ×1\n- Water Neon Sign ×1\n- Basic Water Filter ×1\n- Water Test Kit ×1");

        public static void ResetSelection() { selected = false; }
        public static void ResetGrant() { grantedThisStart = false; }

        public static void InstallTab(MainMenuUIController menu)
        {
            try
            {
                if (menu?.tabGeneralist == null) return;
                var sourceButton = menu.tabGeneralist.GetComponentInParent<UnityEngine.UI.Button>();
                if (sourceButton == null || sourceButton.transform.parent == null) return;
                var parent = sourceButton.transform.parent;
                if (parent.Find(TabName) != null) return;

                var clone = UnityEngine.Object.Instantiate(sourceButton.gameObject, parent);
                clone.name = TabName;
                clone.transform.SetSiblingIndex(sourceButton.transform.GetSiblingIndex() + 1);
                // The cloned apprentice button has a background and a separate profession icon.
                var icon = BrewingTraits.GetStartIcon();
                if (icon != null)
                {
                    var originalIcon = IconProvider.Instance?.apprentice;
                    UnityEngine.UI.Image candidate = null;
                    int candidates = 0;
                    foreach (var childImage in clone.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    {
                        if (childImage == null || childImage.gameObject == clone || childImage.sprite == null
                            || childImage.sprite == menu.tab || childImage.sprite == menu.tabSelected) continue;
                        if (originalIcon != null && childImage.sprite == originalIcon)
                        {
                            candidate = childImage;
                            candidates = 1;
                            break;
                        }
                        candidate = childImage;
                        candidates++;
                    }
                    if (candidates == 1)
                    {
                        candidate.sprite = icon;
                        candidate.preserveAspect = true;
                    }
                    else MelonLogger.Warning("[BE-START] 无法确定新人酿酒师按钮的职业图标");
                }
                var label = clone.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
                if (label != null)
                {
                    foreach (var behaviour in label.GetComponents<MonoBehaviour>())
                        if (behaviour != null && (behaviour.GetIl2CppType().Name == "TMPLocalizer"
                            || behaviour.GetIl2CppType().Name == "LocalizeStringEvent"))
                            UnityEngine.Object.DestroyImmediate(behaviour);
                    label.text = DisplayName;
                }
                var button = clone.GetComponent<UnityEngine.UI.Button>();
                if (button == null) return;
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityEngine.Events.UnityAction)(() => Show(menu)));
                clone.SetActive(true);
                MelonLogger.Msg("[BE-START] 新人酿酒师专精选项已加入开局页面");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-START] 创建专精选项失败: " + e.Message); }
        }

        public static void Show(MainMenuUIController menu)
        {
            try
            {
                // Reuse the apprentice page's window and start-button setup, then replace its data.
                menu.OnGeneralistClick();
                var newGame = NewGameData.Instance;
                if (newGame == null) return;
                newGame.startType = NewGameData.StartType.APPRENTICE;
                selected = true;
                if (menu.title != null) menu.title.text = DisplayName;
                if (menu.body != null) menu.body.text = Description;
                if (menu.startingEquipment != null) menu.startingEquipment.text = StartingEquipment;
                if (menu.note != null) menu.note.text = "";
                if (menu.tabGeneralist != null) menu.tabGeneralist.sprite = menu.tab;
                var tab = menu.tabGeneralist.GetComponentInParent<UnityEngine.UI.Button>()?.transform.parent?.Find(TabName);
                var image = tab?.GetComponent<UnityEngine.UI.Image>();
                if (image != null) image.sprite = menu.tabSelected;
                MelonLogger.Msg("[BE-START] 已选择新人酿酒师专精");
            }
            catch (Exception e) { MelonLogger.Warning("[BE-START] 选择专精失败: " + e.Message); }
        }

        public static void ResetTab(MainMenuUIController menu)
        {
            selected = false;
            try
            {
                var tab = menu?.tabGeneralist?.GetComponentInParent<UnityEngine.UI.Button>()?.transform.parent?.Find(TabName);
                var image = tab?.GetComponent<UnityEngine.UI.Image>();
                if (image != null) image.sprite = menu.tab;
            }
            catch { }
        }

        public static void GrantInitialItems()
        {
            if (grantedThisStart) return;
            var newGame = NewGameData.Instance;
            var player = PlayerStore.Instance;
            if (!selected && (int)(newGame?.startType ?? NewGameData.StartType.NONE) != LegacyStartTypeValue
                && (int)(player?.startType ?? NewGameData.StartType.NONE) != LegacyStartTypeValue) return;
            var emporium = EmporiumEntry.Instance;
            if (emporium?.invElement == null) return;

            try { BrewingExpansionMod.EnsureStarterItemSprites(); }
            catch (Exception e) { MelonLogger.Warning("[BE-START] 开局贴图初始化失败: " + e.Message); }
            if (player != null)
            {
                if (player.modData == null)
                    player.modData = new Il2CppSystem.Collections.Generic.Dictionary<string, string>();
                player.modData[SaveMarker] = "1";
                player.startType = NewGameData.StartType.APPRENTICE;
            }
            if (newGame != null) newGame.startType = NewGameData.StartType.APPRENTICE;
            grantedThisStart = true;
            int placed = 0;
            placed += BrewerStarterPerk.Grant(emporium, "wine_yeast", 4);
            placed += BrewerStarterPerk.Grant(emporium, "wine_bottle", 4);
            placed += BrewerStarterPerk.Grant(emporium, BrewingExpansionMod.ClearWaterTankId, 1);
            placed += BrewerStarterPerk.Grant(emporium, BrewingExpansionMod.BrewerySignId, 1);
            placed += BrewerStarterPerk.Grant(emporium, "sign_water", 1);
            placed += BrewerStarterPerk.Grant(emporium, "water_filter", 1);
            placed += BrewerStarterPerk.Grant(emporium, "water_test_container", 1);
            selected = false;
            MelonLogger.Msg("[BE-START] 新人酿酒师已发放 " + placed + "/13 件开局物品");
        }
    }

    [HarmonyPatch(typeof(MainMenuUIController), "OnNewGameClick")]
    public static class PatchNewBrewerStartTab
    {
        [HarmonyPostfix]
        public static void Postfix(MainMenuUIController __instance)
        {
            NewBrewerStart.ResetSelection();
            NewBrewerStart.InstallTab(__instance);
        }
    }

    [HarmonyPatch(typeof(MainMenuUIController), "ResetAllTab")]
    public static class PatchNewBrewerStartTabReset
    {
        [HarmonyPostfix]
        public static void Postfix(MainMenuUIController __instance) { NewBrewerStart.ResetTab(__instance); }
    }

    [HarmonyPatch(typeof(NewGameData), "GetStartDisplayName")]
    public static class PatchNewBrewerStartName
    {
        [HarmonyPostfix]
        public static void Postfix(NewGameData.StartType startType, ref string __result)
        {
            if ((int)startType == NewBrewerStart.LegacyStartTypeValue) __result = NewBrewerStart.DisplayName;
        }
    }

    [HarmonyPatch(typeof(IconProvider), "GetIcon")]
    public static class PatchNewBrewerStartIcon
    {
        [HarmonyPostfix]
        public static void Postfix(NewGameData.StartType startType, ref Sprite __result)
        {
            if ((int)startType == NewBrewerStart.LegacyStartTypeValue)
                __result = BrewingTraits.GetStartIcon() ?? __result;
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "IsStartTypeInLimitedBuild")]
    public static class PatchNewBrewerStartAvailable
    {
        [HarmonyPostfix]
        public static void Postfix(NewGameData.StartType startType, ref bool __result)
        {
            if ((int)startType == NewBrewerStart.LegacyStartTypeValue) __result = true;
        }
    }

    [HarmonyPatch(typeof(StartingPerkList), "InitStartingPerk")]
    public static class PatchRegisterBrewerStarterPerk
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        public static void Postfix() { BrewerStarterPerk.Register(); BrewingTraits.Register(); }
    }

    [HarmonyPatch(typeof(PerkUIController), "OpenUI")]
    public static class PatchBrewerStarterPerkUI
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            BrewerStarterPerk.Register();
            BrewerStarterPerk.RegisterIcon();
            BrewingTraits.Register();
            BrewingTraits.RegisterIcons();
        }
    }

    [HarmonyPatch(typeof(StartingPerkElement), "Start")]
    public static class PatchBrewerStarterPerkIcon
    {
        [HarmonyPrefix]
        public static void Prefix() { BrewerStarterPerk.RegisterIcon(); BrewingTraits.RegisterIcons(); }

        [HarmonyPostfix]
        public static void Postfix(StartingPerkElement __instance)
        {
            if (__instance == null || __instance.perk == null) return;
            var icon = __instance.perk.id == BrewerStarterPerk.Id
                ? BrewerStarterPerk.GetIcon() : BrewingTraits.GetIcon(__instance.perk.id);
            if (icon != null && __instance.icon != null) __instance.icon.sprite = icon;
        }
    }

    [HarmonyPatch(typeof(StartingPerk), "GetLocalizedDisplayName")]
    public static class PatchBrewerStarterPerkName
    {
        [HarmonyPostfix]
        public static void Postfix(StartingPerk __instance, ref string __result)
        {
            if (__instance != null && __instance.id == BrewerStarterPerk.Id)
                __result = BrewerStarterPerk.DisplayName;
            else if (__instance != null && BrewingTraits.IsOurId(__instance.id))
                __result = BrewingTraits.Name(__instance.id);
        }
    }

    [HarmonyPatch(typeof(StartingPerk), "GetLocalizedDescription")]
    public static class PatchBrewerStarterPerkDescription
    {
        [HarmonyPostfix]
        public static void Postfix(StartingPerk __instance, ref string __result)
        {
            if (__instance != null && __instance.id == BrewerStarterPerk.Id)
                __result = BrewerStarterPerk.Description;
            else if (__instance != null && BrewingTraits.IsOurId(__instance.id))
                __result = BrewingTraits.Description(__instance.id);
        }
    }

    [HarmonyPatch(typeof(PlayerStore), "StartNewGame")]
    public static class PatchBrewerStarterPerkReset
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            // Prepare custom assets for every start type, before any starting items exist.
            BrewingExpansionMod.RegisterMidautumnItems();
            BrewingExpansionMod.EnsureSpriteAssetsReady();
            BrewerStarterPerk.ResetGrant();
            BrewingTraits.ResetStart();
            NewBrewerStart.ResetGrant();
            BrewingExpansionMod.ResetSeedPity();
        }
    }

    [HarmonyPatch(typeof(NewGameData), "HandleInitialItem")]
    public static class PatchBrewerStarterPerkItems
    {
        [HarmonyPrefix]
        public static void Prefix(ref bool __state)
        {
            // Recheck after the new-game scene transition, before vanilla or mod item creation.
            // This is independent of the selected start type and does not require LoadGame.
            BrewingExpansionMod.EnsureSpriteAssetsReady();
            __state = NewBrewerStart.IsSelected;
            if (!__state) return;
            // Suppress the vanilla apprentice inventory only while this method runs.
            // Restore the valid persisted enum before the game writes a save.
            var newGame = NewGameData.Instance;
            var player = PlayerStore.Instance;
            if (newGame != null) newGame.startType = NewGameData.StartType.NONE;
            if (player != null) player.startType = NewGameData.StartType.NONE;
        }

        [HarmonyPostfix]
        public static void Postfix(bool __state)
        {
            if (__state)
            {
                var newGame = NewGameData.Instance;
                var player = PlayerStore.Instance;
                if (newGame != null) newGame.startType = NewGameData.StartType.APPRENTICE;
                if (player != null) player.startType = NewGameData.StartType.APPRENTICE;
            }
            NewBrewerStart.GrantInitialItems();
            BrewerStarterPerk.GrantInitialItems();
            BrewingTraits.GrantMusicBundle();
        }
    }

    [HarmonyPatch(typeof(SavePreviewData), "GenerateData")]
    public static class PatchNewBrewerStartPreview
    {
        [HarmonyPostfix]
        public static void Postfix(ref SavePreviewData __result)
        {
            if (__result == null) return;
            if (!NewBrewerStart.IsSelected && !NewBrewerStart.HasSaveMarker(PlayerStore.Instance)) return;
            __result.startType = NewGameData.StartType.APPRENTICE;
            __result.startName = NewBrewerStart.DisplayName;
        }
    }

    // 原版普通事件池在抽取时读取蓝图。此处只注册一次，旧存档也会在下一次抽取时获得新事件。
    public static class BrewingMarketEvents
    {
        private const string ShortageId = "BE_SUBSTANCE_SHORTAGE";
        private const string SurplusId = "BE_SUBSTANCE_SURPLUS";
        private const string PartyId = "BE_UPPER_PARTY";
        private const string UpperSurplusId = "BE_UPPER_SUBSTANCE_SURPLUS";

        private static Il2CppSystem.Func<StoreEvent> shortageFactory;
        private static Il2CppSystem.Func<StoreEvent> surplusFactory;
        private static Il2CppSystem.Func<StoreEvent> partyFactory;
        private static Il2CppSystem.Func<StoreEvent> upperSurplusFactory;

        private static StoreEvent Create(string id, string zhName, string enName,
            string zhDescription, string enDescription, int modifier, StoreEvent.EventArea area)
        {
            var name = Lang.T(zhName, enName);
            var storeEvent = new StoreEvent();
            storeEvent.identifier = id;
            storeEvent.newsName = name;
            storeEvent.displayName = name;
            storeEvent.newsDescription = Lang.T(zhDescription, enDescription);
            storeEvent.eventType = StoreEvent.EventType.NORMALE;
            storeEvent.eventArea = area;
            storeEvent.duration = 3;
            storeEvent.cooldown = 7;
            storeEvent.importance = 5;
            storeEvent.isDurationVisible = true;
            storeEvent.negociationDatas.Add(new NegociationData("SUBSTANCE", modifier, name, name));
            foreach (var otherId in new[] { ShortageId, SurplusId, PartyId, UpperSurplusId })
                if (otherId != id) storeEvent.imcompatibleEvents.Add(otherId);
            return storeEvent;
        }

        private static StoreEvent CreateShortage() => Create(ShortageId,
            "烟酒资源短缺", "Alcohol and Tobacco Shortage",
            "烟酒供给紧张，烟酒类物品的交易价格上涨 50%。",
            "Alcohol and tobacco are scarce. Trade prices for these goods rise by 50%.",
            50, StoreEvent.EventArea.ALL);

        private static StoreEvent CreateSurplus() => Create(SurplusId,
            "烟酒资源过剩", "Alcohol and Tobacco Surplus",
            "烟酒供应过剩，烟酒类物品的交易价格下降 50%。",
            "Alcohol and tobacco are in surplus. Trade prices for these goods fall by 50%.",
            -50, StoreEvent.EventArea.ALL);

        private static StoreEvent CreateParty() => Create(PartyId,
            "上层区派对狂欢", "Upper District Party",
            "上层区正在举行派对，烟酒类物品的交易价格上涨 25%。",
            "The upper district is celebrating. Trade prices for alcohol and tobacco rise by 25%.",
            25, StoreEvent.EventArea.UPPER);

        private static StoreEvent CreateUpperSurplus() => Create(UpperSurplusId,
            "上层区烟酒过剩", "Upper District Alcohol and Tobacco Surplus",
            "上层区烟酒库存积压，烟酒类物品的交易价格下降 25%。",
            "Alcohol and tobacco stocks are piling up in the upper district. Trade prices fall by 25%.",
            -25, StoreEvent.EventArea.UPPER);

        public static void Register()
        {
            var blueprints = StoreEventManager.normalEventBlueprints;
            if (blueprints == null) return;
            for (int i = 0; i < blueprints.Count; i++)
                if (blueprints[i] != null && blueprints[i].identifier == ShortageId) return;

            shortageFactory = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreEvent>>(
                (System.Func<StoreEvent>)CreateShortage);
            surplusFactory = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreEvent>>(
                (System.Func<StoreEvent>)CreateSurplus);
            partyFactory = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreEvent>>(
                (System.Func<StoreEvent>)CreateParty);
            upperSurplusFactory = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Func<StoreEvent>>(
                (System.Func<StoreEvent>)CreateUpperSurplus);

            blueprints.Add(new StoreEventBlueprint(shortageFactory, 5, ShortageId));
            blueprints.Add(new StoreEventBlueprint(surplusFactory, 5, SurplusId));
            blueprints.Add(new StoreEventBlueprint(partyFactory, 5, PartyId));
            blueprints.Add(new StoreEventBlueprint(upperSurplusFactory, 5, UpperSurplusId));
            MelonLogger.Msg("[BE-EVENT] 已注册四种烟酒市场事件");
        }
    }

    [HarmonyPatch(typeof(StoreEventManager), "PickRandomEventByType")]
    public static class PatchRegisterBrewingMarketEvents
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            try { BrewingMarketEvents.Register(); }
            catch (Exception e) { MelonLogger.Warning("[BE-EVENT] 注册失败: " + e.Message); }
        }
    }

}
