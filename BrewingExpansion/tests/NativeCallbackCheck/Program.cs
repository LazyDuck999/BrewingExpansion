using BrewingExpansion;
using Il2Cpp;
using Il2CppInterop.Runtime;

var checks = 0;
void Check(bool result, string name) { if (!result) throw new Exception(name); checks++; }
bool CanUse(GameItem item)
{
    var result = false;
    if (NativeActivationCanUsePatch.Prefix(item, ref result)) result = item.CanActivateSlotItem();
    return result;
}
bool MayUse(GameItem item)
{
    var result = false;
    if (NativeActivationMayUsePatch.Prefix(item, ref result)) result = item.MayActivateSlotItem();
    return result;
}
foreach (var id in new[] { BrewingExpansionMod.MyPerfumeId, BrewingExpansionMod.FreedomPerfumeId,
    BrewingExpansionMod.PremiumBlindboxId, BrewingExpansionMod.MidautumnBlindboxId,
    BrewingExpansionMod.MidautumnMooncakeBoxId, BrewingExpansionMod.LowBlindboxId,
    BrewingExpansionMod.MidBlindboxId, BrewingExpansionMod.AfterglowBlindboxId,
    BrewingExpansionMod.WineBlindboxId, BrewingExpansionMod.ContainerBlindboxId,
    BrewingExpansionMod.FigurineBlindboxId, BrewingExpansionMod.CassetteBundleId })
{
    var item = new GameItem { identifier = id, Owned = true, Uses = 5 };
    NativeItemActivation.Bind(item);
    NativeItemActivation.Bind(item);
    Check(item.canActivateSlotItemFunc == null && item.mayActivateSlotItemFunc == null,
        "Reflective Func still bound: " + id);
    BrewingExpansionMod.LastUsed = null;
    item.onActivateSlotItemFunc.Invoke(item, null, null);
    Check(BrewingExpansionMod.LastUsed == id && item.Calls == 1, "Wrong/duplicate activation: " + id);
    Check(CanUse(item) && MayUse(item), "Owned item unavailable: " + id);
    item.Owned = false;
    item.onActivateSlotItemFunc.Invoke(item, null, null);
    Check(!CanUse(item) && !MayUse(item) && item.Calls == 1,
        "Unowned item used: " + id);
}
var vanilla = new GameItem { identifier = "native_box" };
var vanillaCallback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameItem, GameInventory, SlotMarker>>(
    (Action<GameItem, GameInventory, SlotMarker>)((_, _, _) => { }));
vanilla.onActivateSlotItemFunc = vanillaCallback;
NativeItemActivation.Bind(vanilla);
Check(ReferenceEquals(vanilla.onActivateSlotItemFunc, vanillaCallback), "Vanilla callback overwritten");
var nativeResult = true;
Check(NativeActivationCanUsePatch.Prefix(vanilla, ref nativeResult) && nativeResult,
    "Vanilla can-use pipeline intercepted");
Check(NativeActivationMayUsePatch.Prefix(vanilla, ref nativeResult) && nativeResult,
    "Vanilla may-use pipeline intercepted");
// Reproduce the real failure on the old converted Func path, then ensure binding removes it.
var previous = new GameItem { identifier = BrewingExpansionMod.MyPerfumeId, Owned = true, Uses = 5 };
previous.mayActivateSlotItemFunc = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem, GameInventory, SlotMarker, bool>>(
    (Func<GameItem, GameInventory, SlotMarker, bool>)((_, _, _) => true));
try { previous.MayActivateSlotItem(); throw new Exception("Reflection failure not modeled"); }
catch (System.Reflection.TargetException) { checks++; }
NativeItemActivation.Bind(previous);
Check(CanUse(previous) && MayUse(previous) && previous.mayActivateSlotItemFunc == null,
    "Old reflection delegate not cleared");
var exhausted = new GameItem { identifier = BrewingExpansionMod.MyPerfumeId, Owned = true, Uses = 0 };
NativeItemActivation.Bind(exhausted);
exhausted.onActivateSlotItemFunc.Invoke(exhausted, null, null);
Check(exhausted.Calls == 0 && !CanUse(exhausted) && !MayUse(exhausted), "Exhausted perfume used");

ModHook.FailOn = "directory";
try { NativeGameEvents.Register(); throw new Exception("Missing simulated subscription error"); }
catch (InvalidOperationException) { }
Check(ModHook.Directory.Count == 0 && ModHook.Tooltips.Count == 0, "Partial subscription not rolled back");
ModHook.FailOn = null;
NativeGameEvents.Register(); NativeGameEvents.Register();
Check(ModHook.Directory.Count == 1 && ModHook.Tooltips.Count == 0, "Duplicate directory subscription or premature tooltip lock");
ModHook.Directory[0].Invoke(new ModItemDirectory());
Check(XIAOWOBrewingBarrel.NativeItemDirectoryRegistration.Calls == 1, "Directory event missing");
// Reproduce the actual ordering: the native Late event happens before more text is added.
foreach (var id in new[] { "recipe", "guide" })
    foreach (var advanced in new[] { false, true })
    {
        var builder = new RichTextBuilder(); builder.AddLine("header");
        foreach (var subscriber in ModHook.Tooltips) subscriber.Invoke(builder, new GameItem { identifier = id });
        builder.AddLine(advanced ? "advanced secret recipe" : "secret recipe");
        var result = builder.Build();
        RecipeTooltipLockPatch.Postfix(new GameItemElement { identifier = id }, ref result);
        Check(result.Lines.SequenceEqual(new[] { "locked" }), "Native post-Late text leaked: " + id);
    }
var full = new RichText { Lines = new() { "purchased recipe content" } };
var displayed = full;
PlayerStore.Instance.OwnedRecipe = true;
RecipeTooltipLockPatch.Postfix(new GameItemElement { identifier = "recipe" }, ref displayed);
Check(ReferenceEquals(full, displayed), "Owned recipe tooltip changed");
PlayerStore.Instance.OwnedRecipe = false;
RecipeTooltipLockPatch.Postfix(new GameItemElement { identifier = "native_item" }, ref displayed);
Check(ReferenceEquals(full, displayed), "Vanilla tooltip changed");
PlayerStore.Instance.FailCheck = true;
RecipeTooltipLockPatch.Postfix(new GameItemElement { identifier = "recipe" }, ref displayed);
Check(displayed.Lines.SequenceEqual(new[] { "locked" }), "Ownership error exposes recipe");
var storeBefore = PlayerStore.Instance;
PlayerStore.Instance = null;
displayed = full;
RecipeTooltipLockPatch.Postfix(new GameItemElement { identifier = "recipe" }, ref displayed);
Check(displayed.Lines.SequenceEqual(new[] { "locked" }), "New-game initialization exposes recipe");
PlayerStore.Instance = storeBefore; PlayerStore.Instance.FailCheck = false;
NativeGameEvents.Unregister(); NativeGameEvents.Unregister();
Check(ModHook.Directory.Count == 0 && ModHook.Tooltips.Count == 0, "Subscriptions not removed");
NativeGameEvents.Register(); NativeGameEvents.Unregister();
Check(ModHook.Directory.Count == 0, "Register after cleanup failed");

var originalOpens = 0;
var window = new PixelWindow();
window.openCallback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => originalOpens++));
var cabinet = new GameItem { identifier = "cabinet", contentWindow = window };
window.parentItems.Add(cabinet);
NativeWindowCallbacks.Bind(cabinet); NativeWindowCallbacks.Bind(cabinet);
window.openCallback.Invoke();
Check(originalOpens == 1 && XIAOWOBrewingBarrel.BarrelMaintenanceEvents.Calls == 1,
    "Window original callback lost or duplicated");
cabinet.contentWindow = new PixelWindow(); NativeWindowCallbacks.Bind(cabinet);
cabinet.contentWindow.parentItems.Add(cabinet);
cabinet.contentWindow.openCallback.Invoke();
Check(XIAOWOBrewingBarrel.BarrelMaintenanceEvents.Calls == 2, "Restored window not rebound");

ModHook.FailOn = "return";
try { XuxuMusic.NativeMusicEvents.Register(); throw new Exception("Missing simulated music error"); }
catch (InvalidOperationException) { }
Check(ModHook.Directory.Count == 0 && ModHook.Leave.Count == 0 && ModHook.Return.Count == 0,
    "Partial music subscription not rolled back");
ModHook.FailOn = null;
XuxuMusic.NativeMusicEvents.Register(); XuxuMusic.NativeMusicEvents.Register();
Check(ModHook.Directory.Count == 1 && ModHook.Leave.Count == 1 && ModHook.Return.Count == 1,
    "Music subscribed twice");
ModHook.Leave[0].Invoke(); ModHook.Return[0].Invoke();
Check(XuxuMusic.DesktopMp3Player.Leaves == 1 && XuxuMusic.DesktopMp3Player.Returns == 1,
    "Music fixture event missing");
XuxuMusic.NativeMusicEvents.Unregister();
Check(ModHook.Directory.Count == 0 && ModHook.Leave.Count == 0 && ModHook.Return.Count == 0,
    "Music subscriptions leaked");
Console.WriteLine($"Native callback checks passed: {checks} (simulated API; no game objects loaded).");
