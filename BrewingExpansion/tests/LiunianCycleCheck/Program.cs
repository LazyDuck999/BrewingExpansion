using Il2Cpp;
using HarmonyLib;
using XIAOWOBrewingBarrel;
var checks = 0;
void Check(bool result,string label) { if (!result) throw new Exception(label); checks++; }
void Reset() {
    LiunianAger.AbortNight(); Core.Log.Messages.Clear(); LiunianModuleBatch.Enabled = true;
    MachineHelper.Optimize = true; MachineHelper.PowerDraws = MachineHelper.Completed = 0; MachineHelper.BeforeCompleted = null;
    ModuleEffectHelper.OriginalActions = ModuleEffectHelper.TempComputes = 0;
    ModuleHelper.Computes = ModuleHelper.Applies = 0; ModuleEffectHelper.ActionOrder.Clear();
    ModuleEffectHelper.moduleEffects.Clear(); Harmony.ForeignMethod = null; Harmony.Info = null;
    LiunianNativeAddress.Base = 0x10000000;
    LiunianAger.ValueRefreshes = LiunianAger.DirtyMarks = 0;
}
GameItem Cabinet(int wines=0,int modules=0) {
    var machine = new GameItemElement { identifier = LiunianAger.Id, uniqueId = 101, Quality = 30000 };
    for (var i=0;i<wines;i++) {
        var wine = new GameItem { identifier = "wine", Parent = machine };
        wine.ModifyTag("WINE_CONTAINER_TAG",s => s.Enable()); machine.Storage.childItems.Add(wine);
    }
    for(var i=0;i<modules;i++) machine.Modules.childItems.Add(new() { identifier = "system_module_quality", Parent = machine, uniqueId = i });
    machine.Validations = 0; return machine;
}
void Effect(GameItem module,string id,int slot=1) => module.ModifyTag("MODULE_EFFECT" + slot + "_TAG",s => { s.Enable(); s.valueString = id; });
void RegisterDegrade() => ModuleEffectHelper.moduleEffects["MODULE_EFFECT_DEGRADING"] = new() {
    onUsedAction = new() { method_ptr = new(LiunianNativeAddress.Base + 0x4E4780), Effect = item => ModuleEffectHelper.Degrade(item,1) }
};
void Settle(GameItem machine) {
    using var batch = LiunianModuleBatch.Begin(machine,machine.Modules);
    MachineHelper.OnMachineActioned(machine,machine.Modules);
}
void Cycle(GameItem machine) {
    LiunianAgingCycle.Bind(machine); machine.onCycleEndSlotItemFunc(machine,null,null);
}
Reset(); var full = Cabinet(80,30);
Cycle(full);
Check(full.Storage.childItems.All(w => AgableHelper.GetAge(w) == 241 && w.unitValue == 823),"Turbo changed days/price");
Check(full.Storage.childItems.All(w => w.itemFeatures.Any(f => f.identifier == "cold" && !f.isDisabled)),"Turbo missed cold feature");
Check(MachineHelper.PowerDraws == 1 && MachineHelper.Completed == 1,"Cycle drew power or completed multiple times");
Check(ModuleEffectHelper.OriginalActions == 0 && ModuleEffectHelper.TempComputes == 1
    && ModuleHelper.Computes == 1 && ModuleHelper.Applies == 1,"Pure quality modules did not merge group calculations");
Check(full.Validations == 2,"Age/chill shared parent not validated once per phase");
Check(Core.Log.Messages.Any(m => m.Contains("parentDeferred=80") && m.Contains("moduleMode=quality-batch")),"Missing cycle profiling counts");
Check(Core.Log.Messages.Any(m => m.Contains("chillMs=") && m.Contains("valueMs=")),"Missing cold/value profiling");
var firstAge = AgableHelper.GetAge(full.Storage.childItems[0]);
Cycle(full);
Check(AgableHelper.GetAge(full.Storage.childItems[0]) > firstAge && MachineHelper.PowerDraws == 2,"Repeated turbo lost accumulated age");
Check(full.Storage.childItems.All(w => w.itemFeatures.Count == 1 && !w.itemFeatures[0].isDisabled),"Repeated turbo duplicated/lost cold feature");
Reset(); var reentrant = Cabinet(1,3);
MachineHelper.BeforeCompleted = machine => Cycle(machine); Cycle(reentrant);
Check(MachineHelper.PowerDraws == 1 && MachineHelper.Completed == 1
    && AgableHelper.GetAge(reentrant.Storage.childItems[0]) == 241,"Reentrant callback repeated aging/power");
MachineHelper.BeforeCompleted = null; Cycle(reentrant);
Check(MachineHelper.PowerDraws == 2,"Reentrancy guard leaked into following cycle");
Reset(); var nestedA = Cabinet(0,3); var nestedB = Cabinet(0,4);
using(var outer = LiunianModuleBatch.Begin(nestedA,nestedA.Modules)) {
    using(var inner = LiunianModuleBatch.Begin(nestedB,nestedB.Modules)) {
        MachineHelper.OnMachineActioned(nestedB,nestedB.Modules);
        Check(inner.Batched == 4,"Inner module scope did not settle");
    }
    MachineHelper.OnMachineActioned(nestedA,nestedA.Modules);
    Check(outer.Batched == 3,"Nested module scope did not restore outer scope");
}
Reset(); var ordinary = Cabinet(0,4); ordinary.identifier = "wine_rack";
using(var scope = LiunianModuleBatch.Begin(ordinary,ordinary.Modules)) {
    MachineHelper.OnMachineActioned(ordinary,ordinary.Modules);
    Check(scope.Batched == 4 && ModuleEffectHelper.OriginalActions == 0
        && ModuleEffectHelper.TempComputes == 1,"Ordinary machine did not use global settlement");
}
Check(LiunianModuleUseBatchPatch.Prefix(ordinary.Modules.childItems[0],ordinary.Modules,ordinary),"Module scope leaked after disposal");
Reset(); var native = Cabinet(0,30); MachineHelper.Optimize = false;
MachineHelper.OnMachineActioned(native,native.Modules);
Check(ModuleEffectHelper.TempComputes == 31 && native.Quality == 3000,"Native model reference invalid");
var referenceQuality = native.Quality;
Reset(); var batched = Cabinet(0,30); Settle(batched);
Check(batched.Quality == referenceQuality && ModuleEffectHelper.TempComputes == 1,"Quality result differs from native reference");
// Preserve two-slot use actions, order and permanent loss exactly.
Reset(); RegisterDegrade(); native = Cabinet(0,12);
foreach(var m in native.Modules.childItems) { Effect(m,"MODULE_EFFECT_DEGRADING"); Effect(m,"MODULE_EFFECT_DEGRADING",2); }
MachineHelper.Optimize = false; MachineHelper.OnMachineActioned(native,native.Modules);
var reference = native.Modules.childItems.Select(m => (m.Permanent,m.Temporary,m.Uses)).ToArray();
var order = ModuleEffectHelper.ActionOrder.ToArray(); referenceQuality = native.Quality;
Reset(); RegisterDegrade(); batched = Cabinet(0,12);
foreach(var m in batched.Modules.childItems) { Effect(m,"MODULE_EFFECT_DEGRADING"); Effect(m,"MODULE_EFFECT_DEGRADING",2); }
Settle(batched);
Check(batched.Modules.childItems.Select(m => (m.Permanent,m.Temporary,m.Uses)).SequenceEqual(reference),"Degrading module loss differs");
Check(ModuleEffectHelper.ActionOrder.SequenceEqual(order),"Module/slot action order differs");
Check(batched.Quality == referenceQuality && ModuleEffectHelper.TempComputes == 1,"Degrade result/group recompute mismatch");
Reset(); var premium = Cabinet(0,4);
ModuleEffectHelper.moduleEffects["MODULE_EFFECT_PREMIUM"] = new() { onAnyAction = new() { method_ptr = new(LiunianNativeAddress.Base + 0x2FF7B0) } };
foreach(var m in premium.Modules.childItems) Effect(m,"MODULE_EFFECT_PREMIUM");
using(var scope=LiunianModuleBatch.Begin(premium,premium.Modules)) {
    MachineHelper.OnMachineActioned(premium,premium.Modules);
    Check(scope.Batched == 4,"Native no-op premium actions rejected");
}
// Unknown/custom/learning module effects retain native action execution.
foreach(var scenario in new[] { "custom-id","learning","replaced-used","replaced-any","multicast","address-mismatch" }) {
    Reset(); RegisterDegrade(); var machine = Cabinet(0,3); var module = machine.Modules.childItems[1];
    if(scenario == "custom-id") module.identifier = "be_custom_module";
    else if(scenario == "learning") {
        Effect(module,"MODULE_EFFECT_LEARNING_ALGORITHM");
        ModuleEffectHelper.moduleEffects["MODULE_EFFECT_LEARNING_ALGORITHM"] = new() { onUsedAction = new() { Effect = m => m.Uses++ } };
    } else {
        Effect(module,"MODULE_EFFECT_DEGRADING"); var entry = ModuleEffectHelper.moduleEffects["MODULE_EFFECT_DEGRADING"];
        if(scenario == "replaced-used") entry.onUsedAction.method_ptr = new(123);
        if(scenario == "replaced-any") entry.onAnyAction = new() { method_ptr = new(456) };
        if(scenario == "multicast") entry.onUsedAction.Multicast = new[] { entry.onUsedAction,entry.onUsedAction };
        if(scenario == "address-mismatch") LiunianNativeAddress.Base = 0;
    }
    using var scope = LiunianModuleBatch.Begin(machine,machine.Modules);
    MachineHelper.OnMachineActioned(machine,machine.Modules);
    Check(scope.Mode == "native-special-module" && scope.Batched == 0
        && ModuleEffectHelper.OriginalActions == 3 && ModuleEffectHelper.TempComputes == 4,"Special module not preserved: " + scenario);
}
Reset(); var overlapped = Cabinet(0,8);
Harmony.ForeignMethod = typeof(MachineHelper).GetMethod(nameof(MachineHelper.OnMachineActioned));
Harmony.Info = new(); Harmony.Info.Prefixes.Add(new() { owner = "ProbablyOptimized", PatchMethod = typeof(string).GetMethod("ToString",Type.EmptyTypes) });
using(var scope = LiunianModuleBatch.Begin(overlapped,overlapped.Modules)) {
    MachineHelper.OnMachineActioned(overlapped,overlapped.Modules);
    Check(scope.Mode == "native-overlap:ProbablyOptimized" && scope.Batched == 0
        && ModuleEffectHelper.OriginalActions == 8,"Foreign settlement hook not detected");
}
// Unknown module introduced after preflight sees caught-up machine quality.
Reset(); RegisterDegrade(); var changed = Cabinet(0,3); Effect(changed.Modules.childItems[0],"MODULE_EFFECT_DEGRADING");
var observed = -1;
ModuleEffectHelper.moduleEffects["unknown"] = new() { onUsedAction = new() { Effect = _ => observed = changed.Quality } };
changed.Modules.childItems[0].WhenValidating = () => Effect(changed.Modules.childItems[1],"unknown");
using(var scope = LiunianModuleBatch.Begin(changed,changed.Modules)) {
    MachineHelper.OnMachineActioned(changed,changed.Modules);
    Check(scope.Mode == "native-module-changed" && observed == 299,"Changed module read stale derived quality");
}
// Exceptional action exits restore scope and derived stats without replaying use.
Reset(); RegisterDegrade(); var changedSlot = Cabinet(0,3); var firstModule = changedSlot.Modules.childItems[0];
Effect(firstModule,"MODULE_EFFECT_DEGRADING"); observed = -1;
ModuleEffectHelper.moduleEffects["unknown-slot"] = new() { onUsedAction = new() { Effect = _ => observed = changedSlot.Quality } };
firstModule.WhenValidating = () => { firstModule.WhenValidating = null; Effect(firstModule,"unknown-slot",2); };
using(var scope = LiunianModuleBatch.Begin(changedSlot,changedSlot.Modules)) {
    MachineHelper.OnMachineActioned(changedSlot,changedSlot.Modules);
    Check(scope.Mode == "native-module-changed" && observed == 299,"Changed second-slot action read stale quality");
    Check(firstModule.Uses == 1,"Changed slot replayed already completed degradation");
}
Reset(); RegisterDegrade(); var faulty = Cabinet(0,3); Effect(faulty.Modules.childItems[0],"MODULE_EFFECT_DEGRADING");
faulty.Modules.childItems[0].WhenValidating = () => throw new InvalidOperationException("Injected action failure");
try { Settle(faulty); throw new Exception("Fault not exercised"); } catch(InvalidOperationException) { checks++; }
Check(faulty.Modules.childItems[0].Uses == 1 && faulty.Quality == 299,"Failed action replayed or derived stats stale");
faulty.Modules.childItems[0].WhenValidating = null;
Check(LiunianModuleUseBatchPatch.Prefix(faulty.Modules.childItems[0],faulty.Modules,faulty),"Module batch leaked after exception");
Settle(faulty); Check(faulty.Modules.childItems[0].Uses == 2,"Following cycle suppressed after exception");
// Parent validation scopes isolate cabinets, nest correctly and clean up faults.
Reset(); var a = Cabinet(); var b = Cabinet();
using(var outer = LiunianValidationBatch.Begin(a)) {
    a.ValidateShapeState(); a.ValidateShapeState(); b.ValidateShapeState();
    using(var inner = LiunianValidationBatch.Begin(b)) { b.ValidateShapeState(); a.ValidateShapeState(); }
    Check(b.Validations == 2 && outer.Deferred == 2,"Nested cabinet scope isolation failed");
}
Check(a.Validations == 2 && !LiunianValidationBatch.TryDefer(a),"Parent scope did not flush/restore");
a.FailValidation = true;
using(LiunianValidationBatch.Begin(a)) a.ValidateShapeState();
a.FailValidation = false;
Check(!LiunianValidationBatch.TryDefer(a),"Parent scope leaked after validation exception");
var dead = (GameItemElement)Cabinet(); dead.Destroyed = true;
using(LiunianValidationBatch.Begin(dead)) dead.ValidateShapeState();
Check(dead.Validations == 0,"Destroyed cabinet validated");
Reset(); var unpaid = Cabinet(3,3); unpaid.Power = false; Cycle(unpaid);
Check(MachineHelper.PowerDraws == 0 && MachineHelper.Completed == 0 && unpaid.Storage.childItems.All(w => AgableHelper.GetAge(w) == 0),"Unpowered cycle updated age/modules");
Reset(); var empty = Cabinet(0,3); Cycle(empty);
Check(MachineHelper.PowerDraws == 0 && MachineHelper.Completed == 0,"Empty cabinet consumed power/modules");
Reset(); var mixed = Cabinet(3,3); mixed.Storage.childItems[0].ModifyTag("WINE_COUNTERFEIT_TAG",s => s.Enable());
mixed.Storage.childItems[1].FailTag = "AGE_INT"; Cycle(mixed);
Check(AgableHelper.GetAge(mixed.Storage.childItems[0]) == 0 && mixed.Storage.childItems[0].unitValue == 100,"Counterfeit wine aged");
Check(mixed.Storage.childItems[1].unitValue == 100 && !mixed.Storage.childItems[1].state.dict.ContainsKey("AGE_INT"),"Failed bottle write not restored");
Check(AgableHelper.GetAge(mixed.Storage.childItems[2]) == 241 && MachineHelper.Completed == 1,"Failed bottle blocked healthy wine/modules");
Reset(); var aged = Cabinet(1,3); var wineMax = aged.Storage.childItems[0];
wineMax.ModifyTag("AGE_INT",s => { s.Enable(); s.valueInt = int.MaxValue; }); Cycle(aged);
Check(ExtendedWineAge.TryRead(wineMax,out var extended) && extended == (long)int.MaxValue + 241,"Batched cycle lost extended age");
Check(wineMax.GetTagReadonly("AGE_INT").valueInt == int.MaxValue && wineMax.IsTag(ExtendedWineAge.ExtendedTag),"Native age projection/tag changed");
// Night expiry happens after machine cycles; final cold pass must follow it.
Reset(); var night = Cabinet(8,3);
for(var n=0;n<3;n++) {
    LiunianChillAfterNightPatch.Prefix(); Cycle(night);
    foreach(var wine in night.Storage.childItems) foreach(var feature in wine.itemFeatures) feature.isDisabled = true;
    LiunianChillAfterNightPatch.Postfix();
    Check(night.Storage.childItems.All(w => w.itemFeatures.Any(f => f.identifier == "cold" && !f.isDisabled)),"Cold lost on night " + n);
}
Check(AgableHelper.GetAge(night.Storage.childItems[0]) > 241,"Consecutive nights stopped aging");
LiunianChillAfterNightPatch.Prefix(); Cycle(night);
var injected = new InvalidOperationException();
Check(ReferenceEquals(LiunianChillAfterNightPatch.Finalizer(injected),injected),"Night error replaced/swallowed");
foreach(var wine in night.Storage.childItems) foreach(var feature in wine.itemFeatures) feature.isDisabled = true;
Cycle(night);
Check(night.Storage.childItems.All(w => w.itemFeatures.Any(f => !f.isDisabled)),"Failed night left turbo in deferred-night state");
// General entry points: scopes are automatic for unrelated native machines.
foreach (var id in new[] { "wine_rack", "water_filter", "fridge", "recharger" }) {
    Reset(); var machine = Cabinet(2,5); machine.identifier = id;
    MachineHelper.OnMachineActioned(machine,machine.Modules);
    Check(ModuleEffectHelper.TempComputes == 1 && ModuleHelper.Computes == 1 && ModuleHelper.Applies == 1,
        "Global module entry missed " + id);
    Check(machine.Storage.childItems.All(w => AgableHelper.GetAge(w) == 0 && w.itemFeatures.Count == 0),
        "Module entry introduced aging/chilling into " + id);
}
Reset(); var genericRack = Cabinet(5,4); genericRack.identifier = "wine_rack";
LiunianAgingCycle.RunRack(genericRack,genericRack.Storage,genericRack.Modules,genericRack.Battery);
Check(genericRack.Storage.childItems.All(w => AgableHelper.GetAge(w) == 241 && w.unitValue == 823 && w.itemFeatures.Count == 0),
    "Native rack batch altered aging/chilling");
Check(MachineHelper.PowerDraws == 1 && genericRack.Validations == 1,"Native rack power/parent merge differs");
Reset(); var threadMachine = Cabinet(0,3);
using(var scope=LiunianModuleBatch.Begin(threadMachine,threadMachine.Modules))
    Check(Task.Run(() => LiunianModuleUseBatchPatch.Prefix(threadMachine.Modules.childItems[0],threadMachine.Modules,threadMachine))
        .GetAwaiter().GetResult(),"Module scope leaked across threads");
Reset(); var ilConflict = Cabinet(0,4);
Harmony.ForeignMethod = typeof(MachineHelper).GetMethod(nameof(MachineHelper.OnMachineActioned));
Harmony.Info = new(); Harmony.Info.ILManipulators.Add(new() { PatchMethod=typeof(string).GetMethod("ToString",Type.EmptyTypes) });
using(var batch = LiunianModuleBatch.Begin(ilConflict,ilConflict.Modules))
    Check(batch.Mode == "native-overlap:unknown", "Unknown IL manipulator not detected");
Console.WriteLine("PASS: " + checks + " actual rack/global-module/cold contracts (native API simulated; no in-game timings).");
