namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string m) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
}
namespace Il2Cpp
{
    public class TagState { public bool valueEnabled; }
    public class TagSystem
    {
        public Dictionary<string, TagState> dict = new();
        public bool IsTag(string key) => dict.TryGetValue(key, out var s) && s.valueEnabled;
    }
    public class GameItem
    {
        public string identifier;
        public int uniqueId;
        public GameInventory parentInventory;
        public Il2CppSystem.Collections.Generic.List<string> itemTypes = new();
        public TagSystem state = new();
        public bool IsTag(string key) => state.IsTag(key);
        // Native absent-tag semantics: non-null, but disabled. No mutation.
        public TagState GetTagReadonly(string key) => state.dict.TryGetValue(key, out var s) ? s : new();
    }
    public class GameInventory
    {
        private static int next;
        public IntPtr Pointer = new(++next);
        public string identifier;
        public Il2CppSystem.Collections.Generic.List<GameItem> childItems = new();
        public bool RejectAccept, ThrowAfterAccept;
        public bool Expel(GameItem item)
        {
            if (!childItems.Remove(item)) return false;
            item.parentInventory = null;
            return true;
        }
        public bool UncheckedAccept(GameItem item)
        {
            if (RejectAccept) return false;
            if (item.parentInventory != null) throw new Exception("Duplicate parent attachment");
            childItems.Add(item); item.parentInventory = this;
            if (ThrowAfterAccept) throw new Exception("UI validation failed after attachment");
            return true;
        }
    }
    public class GameGridInventory : GameInventory
    {
        public Func<GameItem, bool> Filter = _ => true;
        public bool MayHaveValidInventorySlot(GameItem item) => Filter(item);
    }
    public class GameSlotInventory : GameInventory { }
    public class PixelElement { public T TryCast<T>() where T : class => this as T; }
    // Real inventories also implement PixelElement; wrap only the cast in the stub.
    public class SlotElement : PixelElement
    {
        public GameSlotInventory Slot;
        public new T TryCast<T>() where T : class => Slot as T;
    }
    public class GridPixelElement
    {
        public SlotElement Battery = new();
        public SlotElement GetElement(int x, int y) => x == 0 && y == 1 ? Battery : null;
    }
    public class MachineWineRack { public static bool CanAgeWine(GameItem item) => item.itemTypes.Contains("ALCOHOL"); }
    public class SaveItemNode
    {
        public string identifier;
        public long uuid, unitValue;
        public int uniqueId;
        public TagSystem itemState = new();
        public object itemShape;
        public GameItem tempLink;
        public Il2CppSystem.Collections.Generic.List<string> itemTypes = new();
        public Il2CppSystem.Collections.Generic.List<long> childItems = new();
        public Il2CppSystem.Collections.Generic.List<int> childItemInventoryNode = new();
    }
    public class SaveManager { public static GameItem DecodeNodes(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems) => null; }
    public class PlayerStore
    {
        public static PlayerStore instance;
        public Il2CppSystem.Collections.Generic.List<GameItem> FindAllItem(bool recursive) => null;
    }
    public class DirectoryMaster { public static GameItem Item(string identifier) => null; }
}
namespace XIAOWOBrewingBarrel
{
    public class Logger { public void Msg(string s) { } public void Warning(string s) { } public void Error(string s) { } }
    public static class Core { public static Logger Log = new(); }
    public static class BarrelItem { public const string Id = "be_large_brewing_barrel"; }
    public static class PulpItem
    {
        public const string Id = "be_brewing_fruit_pulp";
        public static bool RestoreRejectedMove(Il2Cpp.GameGridInventory source, Il2Cpp.GameItem item) => source.UncheckedAccept(item);
    }
    public static class ConcentratorItem { public const string Id = "be_fruit_pulp_concentrator"; }
    public static class LiunianAger
    {
        public const string Id = "be_liunian_ager";
        public const int BatterySaveNode = 4, ModuleSaveNode = 5, StorageSaveNode = 6;
        public static readonly List<(Il2Cpp.GameItem Item, bool Forced)> Repaired = new();
        public static void EnsureLoaded(Il2Cpp.GameItem item, bool forceRepair = false) => Repaired.Add((item, forceRepair));
    }
}
