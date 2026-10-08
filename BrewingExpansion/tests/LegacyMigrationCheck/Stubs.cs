namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}
namespace HarmonyLib
{
    public class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch(System.Type type, string method) { }
    }
}
namespace Il2Cpp
{
    public class GameItem { public string identifier; }
    public class SaveItemNode
    {
        public string identifier;
        public long uuid, unitValue;
        public int uniqueId;
        public object itemState, itemShape;
        public Il2CppSystem.Collections.Generic.List<long> childItems;
        public Il2CppSystem.Collections.Generic.List<int> childItemInventoryNode;
    }
    public class PlayerStore
    {
        public static PlayerStore instance;
        public Il2CppSystem.Collections.Generic.List<GameItem> items;
        public Il2CppSystem.Collections.Generic.List<GameItem> FindAllItem(bool recursive) => items;
    }
    public class DirectoryMaster { public static GameItem Item(string identifier) => null; }
}
namespace XIAOWOBrewingBarrel
{
    public static class BarrelItem { public const string Id = "be_large_brewing_barrel"; }
    public static class PulpItem { public const string Id = "be_brewing_fruit_pulp"; }
    public static class ConcentratorItem { public const string Id = "be_fruit_pulp_concentrator"; }
    public static class LiunianAger { public const string Id = "be_liunian_ager"; }
    public static class Core { public static Logger Log = new(); }
    public class Logger { public void Msg(string text) { } public void Warning(string text) { } }
}
