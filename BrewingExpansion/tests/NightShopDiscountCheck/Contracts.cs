namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyFinalizer : Attribute { }
}
namespace Il2Cpp
{
    public class GameItem(nint pointer, bool owned = false)
    {
        public nint Pointer = pointer;
        public bool Owned = owned;
        public void OnAddedToWeightedArea(bool ignoreOnItemAddedToWeighted = false) { }
        public void AddItemFeature(ItemFeature itemFeature) { }
    }
    public class ItemFeature(string id) { public string identifier = id; }
    public static class GeneralHelper { public static bool IsItemOwned(GameItem item) => item.Owned; }
    public static class ItemFeatureList { public static ItemFeature DiscountBuy15() => new("native_discount_buy_15"); }
}
namespace XIAOWOBrewingBarrel
{
    public static class NightBrewingShop { public static bool IsBrewingVisit; }
    public static class Core { public static Logger Log = new(); }
    public class Logger { public void Warning(string text) { } }
}
