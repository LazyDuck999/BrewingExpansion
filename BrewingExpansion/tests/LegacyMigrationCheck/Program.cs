using Il2Cpp;
using XIAOWOBrewingBarrel;

var checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    checks++;
}
var pairs = new[]
{
    ("xiaowo_large_brewing_barrel", "be_large_brewing_barrel"),
    ("xiaowo_brewing_fruit_pulp", "be_brewing_fruit_pulp"),
    ("xiaowo_fruit_pulp_concentrator", "be_fruit_pulp_concentrator"),
    ("xiaowo_liunian_ager", "be_liunian_ager"),
};
foreach (var (oldId, currentId) in pairs)
{
    Check(LegacyItemMigration.CurrentId(oldId) == currentId, "old alias resolution");
    Check(LegacyItemMigration.CurrentId(currentId) == currentId, "current IDs remain unchanged");
}
foreach (var id in new[] { null, "", "wine_bottle", "xiaowo_foreign_mod_item", "xiaowo_liunian_storage", "xiaowo_liunian_modules" })
    Check(LegacyItemMigration.CurrentId(id) == id, "foreign/internal IDs remain unchanged");
var state = new object();
var shape = new object();
var children = new Il2CppSystem.Collections.Generic.List<long> { 101, 102, 103 };
var regions = new Il2CppSystem.Collections.Generic.List<int> { 4, 5, 6 };
var cabinet = new SaveItemNode { identifier = pairs[3].Item1, uuid = 99, uniqueId = 47,
    unitValue = 6666, itemState = state, itemShape = shape, childItems = children, childItemInventoryNode = regions };
var second = new SaveItemNode { identifier = pairs[3].Item2, uuid = 100, uniqueId = 48 };
var wine = new SaveItemNode { identifier = "be_collection_wine", uuid = 101, unitValue = 9000 };
var nodes = new Il2CppSystem.Collections.Generic.List<SaveItemNode> { cabinet, second, wine, null };
Check(LegacyItemMigration.MigrateSavedNodes(nodes) == 1, "only old node migrated");
Check(nodes.Count == 4 && ReferenceEquals(nodes[0], cabinet) && ReferenceEquals(nodes[1], second), "two owned cabinets are retained, not merged or duplicated");
Check(cabinet.identifier == pairs[3].Item2 && cabinet.uuid == 99 && cabinet.uniqueId == 47 && cabinet.unitValue == 6666, "identity and value preserved");
Check(ReferenceEquals(cabinet.itemState, state) && ReferenceEquals(cabinet.itemShape, shape), "tags and shape preserved");
Check(ReferenceEquals(cabinet.childItems, children) && children.SequenceEqual(new long[] { 101, 102, 103 }), "contained item UUID links preserved");
Check(ReferenceEquals(cabinet.childItemInventoryNode, regions) && regions.SequenceEqual(new[] { 4, 5, 6 }), "old inventory links preserved for subsequent Liunian repair");
Check(wine.identifier == "be_collection_wine" && wine.unitValue == 9000 && nodes[3] == null, "wine and null entries unaffected");
Check(LegacyItemMigration.MigrateSavedNodes(nodes) == 0 && LegacyItemMigration.MigrateSavedNodes(null) == 0, "repeated and empty migration safe");
var aliases = new Il2CppSystem.Collections.Generic.List<SaveItemNode>();
foreach (var pair in pairs) aliases.Add(new SaveItemNode { identifier = pair.Item1 });
Check(LegacyItemMigration.MigrateSavedNodes(aliases) == 4, "all four old save keys migrated");
var item = new GameItem { identifier = pairs[3].Item1 };
Check(LegacyItemMigration.Migrate(item) && item.identifier == pairs[3].Item2 && !LegacyItemMigration.Migrate(item), "existing item migration is in-place and idempotent");
Console.WriteLine($"PASS: {checks} legacy migration checks (native decode not exercised by stubs).");
