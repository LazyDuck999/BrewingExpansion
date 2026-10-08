using BrewingExpansion;
using Il2Cpp;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
var ids = new[] { BrewingExpansionMod.IchigoFigurineId, BrewingExpansionMod.RukiaFigurineId,
    BrewingExpansionMod.UryuFigurineId, BrewingExpansionMod.IridiumSeaCucumberFigurineId };
// Cold start: no explicit initialization, no delay, no LoadGame event.
for (int i = 0; i < ids.Length; i++)
{
    GameItem item = null;
    Check(!BrewingExpansionMod.CreateFromDirectory(ids[i], true, ref item) && item != null && item.name != ids[i], "Cold creation fell back to unknown");
    Check(item.spritePath == ids[i] && item.shape.width == 2 && item.shape.height == (i == 3 ? 2 : 3), "Wrong sprite/shape");
    Check(item.IsTag(BrewingExpansionMod.OwnedTag) && item.IsTag(BrewingExpansionMod.DecorationTag), "Reward missing ownership/decoration");
}
Check(BEState.MidautumnDelayedFactories.Count == 13, "Factories missing");
BrewingExpansionMod.InitMidautumnDelayedFactories();
Check(BEState.MidautumnDelayedFactories.Count == 13, "Repeated init duplicated factories");
GameItem notOwned = null;
BrewingExpansionMod.CreateFromDirectory(ids[0], false, ref notOwned);
Check(!notOwned.IsTag(BrewingExpansionMod.OwnedTag), "Unowned directory request became owned");
var original = new GameItem();
Check(BrewingExpansionMod.CreateFromDirectory("native_item", true, ref original), "Native directory fallback intercepted");

var store = PlayerStore.Instance;
for (int i = 0; i < ids.Length; i++)
{
    var item = new GameItem { identifier = ids[i], name = ids[i], shape = new() { width = 1, height = 1 },
        unitValue = 17, uniqueId = 900 + i, Location = new object() };
    item.EnableTag("custom_saved_tag", false);
    store.Items.Add(item);
}
store.Items.Add(original);
var before = store.Items.Select(i => (i.Pointer, i.uniqueId, i.Location, i.shape, i.unitValue)).ToArray();
BrewingExpansionMod.RepairLegacyFigurines();
for (int i = 0; i < ids.Length; i++)
{
    var item = store.Items[i];
    Check(item.name != ids[i] && item.spritePath == ids[i] && !string.IsNullOrEmpty(item.longDescription), "Placeholder metadata missing");
    Check(item.IsTag(BrewingExpansionMod.OwnedTag) && item.IsTag(BrewingExpansionMod.DecorationTag) && item.IsTag("custom_saved_tag"), "Saved tags lost");
    Check(before[i] == (item.Pointer, item.uniqueId, item.Location, item.shape, item.unitValue), "Saved identity/footprint changed");
}
Check(original.name == null && original.spritePath == null, "Native metadata changed");
store.Items[0].name = "my renamed figurine";
BrewingExpansionMod.RepairLegacyFigurines();
Check(store.Items.Count == 5 && store.Items[0].name == "my renamed figurine", "Recovery duplicates items or loses custom name");
Console.WriteLine($"PASS: {checks} figurine factory/recovery checks (extracted production methods; native API simulated).");

namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace MelonLoader { public static class MelonLogger { public static void Msg(string s) { } public static void Warning(string s) { } } }
namespace BrewingExpansion
{
    public static class BEState { public static readonly Dictionary<string, Func<GameItem>> MidautumnDelayedFactories = new(); }
    public static class Lang { public static string T(string zh, string en) => zh; }
}
namespace Il2Cpp
{
    public class GridShape { public int width, height; }
    public class GridShapeBuilder(int w, int h)
    { public GridShapeBuilder SetDataFill(int x, int y, int value) => this; public GridShape Build() => new() { width = w, height = h }; }
    public class GameItem
    {
        static long next;
        public IntPtr Pointer { get; } = (IntPtr)(++next);
        public string identifier, name, spritePath, shortDescription, longDescription;
        public int uniqueId; public long unitValue; public object Location; public GridShape shape;
        public Il2CppSystem.Collections.Generic.List<string> itemTypes;
        readonly HashSet<string> tags = new();
        public void SetName(string s) => name = s;
        public void SetSprite(string atlas, string key) => spritePath = key;
        public void SetSpriteQuick(string atlas, string key) => spritePath = key;
        public void SetValue(int n) => unitValue = n;
        public void EnableTag(string s, bool flag) => tags.Add(s);
        public bool IsTag(string s) => tags.Contains(s);
    }
    public static class ItemDirectory { public static GameItem CreateEmptyItem(string id) => new() { identifier = id }; }
    public class PlayerStore
    { public static PlayerStore Instance = new(); public System.Collections.Generic.List<GameItem> Items = new();
      public System.Collections.Generic.List<GameItem> FindAllItem(bool owned) => Items.Where(i => i.IsTag(BrewingExpansionMod.OwnedTag) == owned).ToList(); }
}
