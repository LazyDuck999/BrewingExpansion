using Il2Cpp;
using XIAOWOBrewingBarrel;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
SaveItemNode Node(string id, string type = null)
{
    var n = new SaveItemNode { identifier = id };
    if (type != null) n.itemTypes.Add(type);
    return n;
}
GameItem Item(string id, string type)
{
    var item = new GameItem { identifier = id }; item.itemTypes.Add(type); return item;
}
var batteryNode = Node("custom_cell", "ENERGY_CELL");
var moduleNode = Node("system_capped_neural_core", "MODULE");
var wineNode = Node("wine_bottle", "ALCOHOL");
foreach (var oldIndex in Enumerable.Range(0, 10))
{
    Check(LiunianInventoryRegions.RegionFor(batteryNode, oldIndex) == 4, "battery to battery region");
    Check(LiunianInventoryRegions.RegionFor(moduleNode, oldIndex) == 5, "module to module region");
    Check(LiunianInventoryRegions.RegionFor(wineNode, oldIndex) == 6, "wine to storage region");
}
var taggedModule = Node("foreign_core"); taggedModule.itemState.dict["MODULE_TAG"] = new() { valueEnabled = true };
Check(LiunianInventoryRegions.RegionFor(taggedModule, 3) == 5, "tagged module without saved types");
var fallbackModule = Node("scrap_module");
Check(LiunianInventoryRegions.RegionFor(fallbackModule, 3) == 5, "module ID fallback also works from wrong region");
var unknown = Node("instruction_manual");
Check(LiunianInventoryRegions.RegionFor(unknown, 4) == -1, "unknown content is not blindly reclassified");
var module = Item("system_capped_neural_core", "MODULE");
var cell = Item("custom_cell", "ENERGY_CELL");
var wine = Item("wine_bottle", "ALCOHOL");
Check(module.GetTagReadonly("power_source_item_max_energy") != null, "native missing-tag object is non-null");
Check(!LiunianInventoryRegions.IsBattery(module) && module.state.dict.Count == 0, "module is not a battery; lookup does not add tags");
Check(LiunianInventoryRegions.IsBattery(cell), "energy-cell type preserved");
var tagCell = Item("power_pack", "OTHER"); tagCell.state.dict["power_source_item"] = new() { valueEnabled = true };
Check(LiunianInventoryRegions.IsBattery(tagCell), "enabled power tag preserved");
tagCell.state.dict["power_source_item"].valueEnabled = false;
Check(!LiunianInventoryRegions.IsBattery(tagCell), "disabled power tag is not a battery");
var layout = new GridPixelElement(); var slot = new GameSlotInventory(); layout.Battery.Slot = slot;
var modules = new GameGridInventory { identifier = "xiaowo_liunian_modules", Filter = i => i.itemTypes.Contains("MODULE") };
var storage = new GameGridInventory { identifier = "xiaowo_liunian_storage", Filter = i => i.itemTypes.Contains("ALCOHOL") };
slot.UncheckedAccept(module); slot.UncheckedAccept(cell); modules.UncheckedAccept(wine);
var secondModule = Item("other_core", "MODULE"); storage.UncheckedAccept(secondModule);
LiunianInventoryRepair.Repair(layout, modules, storage, 1);
Check(module.parentInventory == modules && secondModule.parentInventory == modules, "modules moved from battery and wine areas");
Check(cell.parentInventory == slot && wine.parentInventory == storage, "battery retained and wine restored");
Check(slot.childItems.Count + modules.childItems.Count + storage.childItems.Count == 4, "repair neither loses nor duplicates items");
LiunianInventoryRepair.Repair(layout, modules, storage, 1);
Check(modules.childItems.Count == 2 && storage.childItems.Count == 1 && slot.childItems.Count == 1, "repeat repair is idempotent");
var blocked = Item("blocked_core", "MODULE"); slot.UncheckedAccept(blocked); modules.Filter = _ => false;
LiunianInventoryRepair.Repair(layout, modules, storage, 1);
Check(blocked.parentInventory == slot, "full/rejecting target retains original item");
modules.Filter = _ => true; modules.RejectAccept = true;
LiunianInventoryRepair.Repair(layout, modules, storage, 1);
Check(blocked.parentInventory == slot && slot.childItems.Count == 2, "failed accept restores source once");
modules.RejectAccept = false; modules.ThrowAfterAccept = true;
LiunianInventoryRepair.Repair(layout, modules, storage, 1);
Check(blocked.parentInventory == modules && !slot.childItems.Contains(blocked), "post-attachment UI failure does not duplicate");

foreach (var id in new[] { "xiaowo_liunian_ager", "be_liunian_ager" })
foreach (var scenario in new[] { new[] { 3, 4, 5 }, new[] { 4, 5, 6 }, new[] { 5, 4, 5 } })
{
    var cabinet = Node(id); cabinet.uuid = 100; cabinet.uniqueId = 42;
    batteryNode.uuid = 1; moduleNode.uuid = 2; wineNode.uuid = 3; unknown.uuid = 4;
    foreach (var u in new long[] { 1, 2, 3, 4, 999 }) cabinet.childItems.Add(u);
    foreach (var r in scenario.Concat(new[] { 7, 8 })) cabinet.childItemInventoryNode.Add(r);
    var ns = new Il2CppSystem.Collections.Generic.List<SaveItemNode> { cabinet, batteryNode, moduleNode, wineNode, unknown, null };
    LiunianSavedInventoryPatch.Prefix(ns);
    Check(cabinet.identifier == LiunianAger.Id && cabinet.childItemInventoryNode.SequenceEqual(new[] { 4, 5, 6, 7, 8 }), "3.0/3.2.1/already-corrupt index migration");
    Check(cabinet.uniqueId == 42 && cabinet.childItems.SequenceEqual(new long[] { 1, 2, 3, 4, 999 }), "identity and child links retained");
    LiunianSavedInventoryPatch.Prefix(ns);
    Check(cabinet.childItemInventoryNode.SequenceEqual(new[] { 4, 5, 6, 7, 8 }), "repeated decode prefix retains repaired links");
    cabinet.tempLink = new GameItem { identifier = LiunianAger.Id };
    LiunianAger.Repaired.Clear(); LiunianSavedInventoryPatch.Postfix(ns);
    Check(LiunianAger.Repaired.Count == 1 && LiunianAger.Repaired[0] == (cabinet.tempLink, true), "complete decode forces cabinet repair past early cache");
}
LiunianSavedInventoryPatch.Prefix(null); LiunianSavedInventoryPatch.Postfix(null);
// Captured local saved cabinet: one cell, 30 modules and 28 wines. These
// checks exercise actual saved IDs/tags, including a simulated legacy layout
// and a previously saved misplaced module. They do not decode a native save.
using (var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "local-save-fixture.json"))))
{
    var fixtureNodes = new Il2CppSystem.Collections.Generic.List<SaveItemNode>();
    foreach (var j in fixture.RootElement.EnumerateArray())
    {
        var n = Node(j.GetProperty("identifier").GetString());
        n.uuid = j.GetProperty("uuid").GetInt64(); n.uniqueId = j.GetProperty("uniqueId").GetInt32();
        n.unitValue = j.GetProperty("unitValue").GetInt64();
        n.itemShape = j.GetProperty("itemShape").GetRawText();
        foreach (var type in j.GetProperty("itemTypes").EnumerateArray()) n.itemTypes.Add(type.GetString());
        foreach (var tag in j.GetProperty("enabledTags").EnumerateArray()) n.itemState.dict[tag.GetString()] = new() { valueEnabled = true };
        foreach (var u in j.GetProperty("childItems").EnumerateArray()) n.childItems.Add(u.GetInt64());
        foreach (var r in j.GetProperty("childItemInventoryNode").EnumerateArray()) n.childItemInventoryNode.Add(r.GetInt32());
        fixtureNodes.Add(n);
    }
    var cabinet = fixtureNodes[0]; var normal = cabinet.childItemInventoryNode.ToArray();
    var values = fixtureNodes.Select(n => (n.unitValue, n.uniqueId, n.itemShape, n.itemState)).ToArray();
    foreach (var scenario in new[] { "normal321", "legacy30", "misplaced33" })
    {
        cabinet.identifier = scenario == "legacy30" ? "xiaowo_liunian_ager" : LiunianAger.Id;
        for (var i = 0; i < normal.Length; i++)
            cabinet.childItemInventoryNode[i] = scenario == "legacy30" ? normal[i] - 1
                : scenario == "misplaced33" && normal[i] == 5 ? 4 : normal[i];
        LiunianSavedInventoryPatch.Prefix(fixtureNodes);
        Check(cabinet.childItemInventoryNode.SequenceEqual(normal), "captured local contents: " + scenario);
        Check(fixtureNodes.Select(n => (n.unitValue, n.uniqueId, n.itemShape, n.itemState)).SequenceEqual(values), "saved wine prices, identities, tags and shapes retained: " + scenario);
    }
}
Console.WriteLine($"PASS: {checks} Liunian compatibility checks (native inventories simulated; game save decode not executed).");
