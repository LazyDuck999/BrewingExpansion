using System.Reflection;
using Il2Cpp;
using MelonLoader;
using XuxuMusic;
using UnityEngine;
var checks = 0;
void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
void Frames() { MelonCoroutines.Frame(); MelonCoroutines.Frame(); }
void Reset() {
    DesktopMp3Player.Release(); MelonCoroutines.Pending.Clear();
    DesktopMp3Player.Installed = DesktopMp3Player.Positioned = 0;
    ItemTooltipHandler.current = null; OverlayHandler.current = null;
    PlayerStore.instance = new(); Mp3LoadState.Loading = false;
    Core.Log.Warnings.Clear();
}
var first = new EmporiumEntry { Pointer = new(1) };
var other = new EmporiumEntry { Pointer = new(2) };
EmporiumEntry.Instance = first;
Reset();
DesktopMp3Player.Schedule(first);
MelonCoroutines.Frame();
DesktopMp3Player.Leave(first);
Frames();
Check(DesktopMp3Player.Installed == 0 && MelonCoroutines.Pending.Count == 0, "Late install after leaving before fixture creation");
DesktopMp3Player.Schedule(first); Frames();
Check(DesktopMp3Player.Installed == 0, "Load callback scheduled while away");
DesktopMp3Player.Arrive(first); Frames();
Check(DesktopMp3Player.Installed == 1 && DesktopMp3Player.Fixture.Active, "Return did not rebuild cancelled fixture");
DesktopMp3Player.Item.contentWindow.Visible = true;
DesktopMp3Player.Leave(first);
Check(!DesktopMp3Player.Fixture.Active && !DesktopMp3Player.Item.contentWindow.Visible, "Leaving did not hide fixture and controls");
DesktopMp3Player.Arrive(first);
Check(DesktopMp3Player.Fixture.Active && DesktopMp3Player.Installed == 1, "Return duplicated installed fixture");
Reset(); DesktopMp3Player.Schedule(first); DesktopMp3Player.Schedule(first); Frames();
Check(DesktopMp3Player.Installed == 1, "Duplicate initialization");
Reset(); DesktopMp3Player.Schedule(first); DesktopMp3Player.Release(); Frames();
Check(DesktopMp3Player.Installed == 0, "Reset resurrected pending fixture");
Reset(); DesktopMp3Player.Schedule(first); DesktopMp3Player.DestroyScene(first); Frames();
Check(DesktopMp3Player.Installed == 0, "Destroy before installation did not cancel pending task");
Reset(); DesktopMp3Player.Schedule(first); DesktopMp3Player.DestroyScene(other); DesktopMp3Player.Leave(other); Frames();
Check(DesktopMp3Player.Installed == 1, "Unrelated scene event cancelled fixture");
var old = DesktopMp3Player.Fixture;
EmporiumEntry.Instance = other;
DesktopMp3Player.Arrive(other); Frames();
Check(DesktopMp3Player.Installed == 2 && old.Destroyed && DesktopMp3Player.Fixture.Active,
    "Returning to another scene activated stale fixture");
Reset(); EmporiumEntry.Instance = first; DesktopMp3Player.Schedule(first);
Mp3LoadState.Loading = true; Frames();
Check(DesktopMp3Player.Installed == 0, "Install during save loading");
Mp3LoadState.Loading = false; Frames();
Check(DesktopMp3Player.Installed == 1, "Loading completion did not permit installation");
// New-game reset cancels previous waits; its Postfix must schedule a fresh one.
Reset(); DesktopMp3Player.Schedule(first);
typeof(Mp3NewRunPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
typeof(Mp3NewRunPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
Frames(); Check(DesktopMp3Player.Installed == 1, "New-game reset lost fixture initialization");
var listener = DesktopMp3Player.Item;
ItemTooltipHandler.current = new() { currentTooltipListener = listener };
OverlayHandler.current = new();
DesktopMp3Player.ClearForTest();
Check(OverlayHandler.current.Hidden == 1 && ItemTooltipHandler.current.currentTooltipListener == null,
    "Normal tooltip cleanup changed");
var unrelated = new GameItem { Pointer = new(43) };
ItemTooltipHandler.current.currentTooltipListener = unrelated;
DesktopMp3Player.ClearForTest();
Check(OverlayHandler.current.Hidden == 1 && ItemTooltipHandler.current.currentTooltipListener == unrelated,
    "Another item's shared tooltip hidden");
ItemTooltipHandler.current.currentTooltipListener = listener;
OverlayHandler.current.DeadChildren = true;
DesktopMp3Player.Release();
Check(Core.Log.Warnings.Count == 0 && ItemTooltipHandler.current.currentTooltipListener == null,
    "Scene cleanup invoked destroyed overlay children");
Reset(); DesktopMp3Player.Schedule(first); Frames();
DesktopMp3Player.SetHover(new() { Destroyed = true });
ItemTooltipHandler.current = new() { Destroyed = true };
var fixture = DesktopMp3Player.Fixture;
DesktopMp3Player.Release();
Check(Core.Log.Warnings.Count == 0 && fixture.Destroyed, "Destroyed Unity wrappers accessed during cleanup");
Reset(); DesktopMp3Player.Schedule(first); Frames();
fixture = DesktopMp3Player.Fixture;
DesktopMp3Player.Item.FailDetach = true;
DesktopMp3Player.Release();
Check(fixture.Destroyed && DesktopMp3Player.Item == null, "Failed item cleanup skipped fixture destruction");
foreach (var mp3 in new[] { new GameItem { identifier = "be_mp3_player" }, new GameItem { identifier = "be_mp3_player", Pointer = new(99) } }) {
    var result = true;
    if (DesktopMp3SelectPatch.Prefix(mp3, ref result)) result = mp3.CanSelectSlotItem();
    Check(!result, "Selection entered native reflection path");
    result = true;
    if (DesktopMp3MaySelectPatch.Prefix(mp3, ref result)) result = mp3.MaySelectSlotItem();
    Check(!result, "May-select entered native reflection path");
}
var native = new GameItem { identifier = "cassette_player" };
var nativeResult = true;
Check(DesktopMp3SelectPatch.Prefix(native, ref nativeResult) && nativeResult, "Native selection intercepted");
Check(DesktopMp3MaySelectPatch.Prefix(native, ref nativeResult) && nativeResult, "Native may-select intercepted");
Console.WriteLine("Actual desktop lifecycle/selection checks passed: " + checks);
