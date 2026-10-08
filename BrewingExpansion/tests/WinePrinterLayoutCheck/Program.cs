using Il2Cpp;
using XIAOWOBrewingBarrel;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
GameItem Printer(int size = 1) => new() { identifier = BrewingExpansion.BrewingExpansionMod.WinePrinterId,
    shape = new GridShape { width = size, height = size }, modifiedShape = new GridShape { width = size, height = size } };
Check(!WinePrinterLayout.Ensure(null), "null is ignored");
foreach (var id in new[] { "bottle_printer", "cassette_player", "be_mp3_player", "be_liunian_ager" })
{
    var other = new GameItem { identifier = id };
    Check(!WinePrinterLayout.Ensure(other) && other.SetCalls == 0 && other.ValidateCalls == 0, "other items are untouched: " + id);
}
var fresh = Printer(); var state = fresh.state; var inventory = fresh.contentWindow;
Check(WinePrinterLayout.Ensure(fresh) && fresh.shape.width == 3 && fresh.modifiedShape.width == 3
    && fresh.SetCalls == 1, "factory-sized printer uses concrete SetShape");
Check(ReferenceEquals(fresh.state, state) && ReferenceEquals(fresh.contentWindow, inventory)
    && fresh.unitValue == 300, "repair preserves machine data");
Check(!WinePrinterLayout.Ensure(fresh) && fresh.SetCalls == 1, "repeat shape repair is idempotent");
var effectiveWrong = Printer(3); effectiveWrong.modifiedShape = new GridShape { width = 1, height = 1 };
Check(WinePrinterLayout.Ensure(effectiveWrong) && effectiveWrong.SetCalls == 0
    && effectiveWrong.ValidateCalls == 1 && effectiveWrong.modifiedShape.height == 3, "correct base shape revalidates old effective shape");
var missing = new GameItem { identifier = fresh.identifier };
Check(WinePrinterLayout.Ensure(missing) && missing.shape.width == 3 && missing.modifiedShape.height == 3, "missing shape repaired");
var one = Printer(); var two = Printer();
WinePrinterLayout.Ensure(one); WinePrinterLayout.Ensure(two);
Check(!ReferenceEquals(one.shape, two.shape), "printers do not share mutable shape objects");
var nodes = new Il2CppSystem.Collections.Generic.List<SaveItemNode>
{
    new() { identifier = one.identifier, tempLink = one },
    new() { identifier = "be_wine_printer", tempLink = Printer() },
    new() { identifier = "cassette_player", tempLink = new GameItem { identifier = "cassette_player" } },
    new() { identifier = "be_wine_printer" }, null
};
WinePrinterLoadLayoutPatch.Postfix(nodes);
Check(nodes[1].tempLink.modifiedShape.width == 3 && nodes[2].tempLink.SetCalls == 0,
    "native decode boundary only repairs actual printers");
WinePrinterLoadLayoutPatch.Postfix(null);
Check(!WinePrinterLayout.Ensure(nodes[1].tempLink), "loaded printer repair is idempotent");
Console.WriteLine($"PASS: {checks} printer layout regression checks (native item operations simulated).");

namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type t, string m) { } }
    public class HarmonyPostfix : Attribute { }
}
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace Il2Cpp
{
    public class GridShape { public int width, height; }
    public class GridShapeBuilder
    {
        private readonly int width, height;
        public GridShapeBuilder(int w, int h) { width = w; height = h; }
        public GridShapeBuilder SetDataFill(int w, int h, int value) => this;
        public GridShape Build() => new() { width = width, height = height };
    }
    public class GameItem
    {
        public string identifier;
        public GridShape shape, modifiedShape;
        public object state = new(), contentWindow = new();
        public long unitValue = 300;
        public int SetCalls, ValidateCalls;
        public GameItem SetShape(GridShape s) { shape = s; SetCalls++; ValidateShapeState(); return this; }
        public void ValidateShapeState() { ValidateCalls++; modifiedShape = new() { width = shape.width, height = shape.height }; }
    }
    public class SaveItemNode { public string identifier; public GameItem tempLink; }
    public class SaveManager { public static GameItem DecodeNodes(Il2CppSystem.Collections.Generic.List<SaveItemNode> savedItems) => null; }
}
namespace BrewingExpansion { public static class BrewingExpansionMod { public const string WinePrinterId = "be_wine_printer"; } }
namespace XIAOWOBrewingBarrel
{
    public static class Core { public static Logger Log = new(); }
    public class Logger { public void Warning(string text) { } }
}
