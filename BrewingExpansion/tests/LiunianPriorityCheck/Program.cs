using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Il2Cpp;
using ProbablyOptimized;
using XIAOWOBrewingBarrel;

var checks = 0;
void Check(bool value,string label) { if(!value) throw new Exception(label); checks++; }
if(args.Length != 1) throw new ArgumentException("Reviewed ProbablyOptimized DLL path required (metadata only).");
Check(LiunianOptimizerCompatibility.MatchesReviewedFile(args[0]),"Reviewed binary hash mismatch");
Check(!LiunianOptimizerCompatibility.MatchesReviewedFile(typeof(Fixture).Assembly.Location),"Unknown binary accepted");
MetadataContract.Check(args[0],Check);
var foreign = new HarmonyLib.Harmony("contract.optimizer");
var own = new HarmonyLib.Harmony("contract.liunian");
MethodInfo PatchEntry(string type,string method="Prefix") => AccessTools.Method(typeof(Fixture).Assembly.GetType("ProbablyOptimized."+type),method);
void Patch(Type type,string name,string entry,Type[] signature=null) => foreign.Patch(AccessTools.Method(type,name,signature),prefix:new HarmonyMethod(PatchEntry(entry)));
Patch(typeof(MachineHelper),nameof(MachineHelper.OnMachineActioned),"MachineActionedPatch");
Patch(typeof(ModuleEffectHelper),nameof(ModuleEffectHelper.ComputeAllModuleTempStat),"Skip_ComputeAllModuleTempStat");
Patch(typeof(ModuleHelper),nameof(ModuleHelper.ComputeModuleEffect),"Skip_ComputeModuleEffect",new[]{typeof(Il2CppSystem.Collections.Generic.List<GameItem>),typeof(GameItem)});
Patch(typeof(ModuleHelper),nameof(ModuleHelper.ApplyBasicModuleEffect),"Skip_ApplyBasicModuleEffect");
Patch(typeof(GameItem),nameof(GameItem.ValidateShapeState),"Dedup_TempStatShapeValidate");
Patch(typeof(GameItemElement),nameof(GameItemElement.Validate),"Defer_ItemElementValidate");
foreign.Patch(AccessTools.Method(typeof(ModuleEffectHelper),nameof(ModuleEffectHelper.ResetAllTempStat)),
    prefix:new HarmonyMethod(PatchEntry("Guard_ResetAllTempStat")),postfix:new HarmonyMethod(PatchEntry("Guard_ResetAllTempStat","Postfix")));
own.PatchAll(typeof(LiunianModuleUseBatchPatch));
own.PatchAll(typeof(LiunianParentValidationPatch));
own.PatchAll(typeof(LiunianChillOnCyclePatch));

GameItemElement Cabinet(string id=LiunianAger.Id) {
    var machine = new GameItemElement { identifier=id,Quality=30000,uniqueId=45 };
    var wine = new GameItemElement { identifier="wine",Parent=machine };
    Fixture.Block=false; wine.ModifyTag("WINE_CONTAINER_TAG",s => s.Enable()); Fixture.Block=true;
    machine.Storage.childItems.Add(wine);
    for(var i=0;i<4;i++) machine.Modules.childItems.Add(new GameItemElement { identifier="system_module_quality",Parent=machine,uniqueId=i });
    return machine;
}
var ordinary = Cabinet("wine_rack");
// Warm the original Harmony wrappers before adding managed-entry adapters.
for(var i=0;i<40;i++) {
    MachineHelper.OnMachineActioned(ordinary,ordinary.Modules);
    ModuleEffectHelper.ComputeAllModuleTempStat(ordinary.Modules.childItems);
    ModuleHelper.ComputeModuleEffect(ordinary.Modules.childItems,ordinary);
    ModuleHelper.ApplyBasicModuleEffect(ordinary.Modules,null,ordinary);
    ordinary.ValidateShapeState(); ordinary.Validate();
}
Check(Fixture.Machine == 40 && MachineHelper.Completed == 0,"Foreign fixture not warmed/executed");
LiunianOptimizerCompatibility.InstallReviewed(typeof(Fixture).Assembly);
var nativeMethod = AccessTools.Method(typeof(MachineHelper),nameof(MachineHelper.OnMachineActioned));
var foreignPatch = HarmonyLib.Harmony.GetPatchInfo(nativeMethod).Prefixes.Single(p => p.owner == "contract.optimizer");
Check(LiunianOptimizerCompatibility.Covers(foreignPatch,nativeMethod),"Installed adapter not recognized");
var machine = Cabinet(); var wineItem=(GameItemElement)machine.Storage.childItems[0];
var beforeMachine=Fixture.Machine; var beforeStats=Fixture.Stats; var beforeShape=Fixture.Shape;
var beforeResetIn=Fixture.ResetIn; var beforeResetOut=Fixture.ResetOut;
LiunianAgingCycle.Bind(machine); machine.onCycleEndSlotItemFunc(machine,null,null);
Check(Fixture.Machine == beforeMachine,"Foreign replacement ran for own cabinet");
Check(Fixture.Stats == beforeStats && Fixture.Shape == beforeShape,"Foreign skip/dedup ran inside owned work");
Check(Fixture.ResetIn == beforeResetIn && Fixture.ResetOut == beforeResetOut,"Foreign reset state changed inside owned work");
Check(MachineHelper.PowerDraws == 1 && MachineHelper.Completed == 1,"Native completion/power not exactly once");
Check(ModuleEffectHelper.TempComputes == 1 && ModuleHelper.Computes == 1 && ModuleHelper.Applies == 1,"Owned quality batch suppressed/repeated");
Check(AgableHelper.GetAge(wineItem) == 241 && wineItem.unitValue == 823,"Owned aging/value changed");
Check(wineItem.itemFeatures.Count == 1 && !wineItem.itemFeatures[0].isDisabled,"Owned turbo cold missing");
Check(machine.Modules.childItems.All(m => m.IsTag("TEMP_2")),"Final modified module state not validated");
Check(!LiunianPriorityScope.OwnsSettlement && !LiunianPriorityScope.OwnsValidation,"Cycle scopes leaked");

using(var scope=LiunianPriorityScope.Begin(machine)) {
    var beforeVisual=Fixture.Visual; wineItem.Validate();
    Check(wineItem.VisualValidations == 1 && Fixture.Visual == beforeVisual,"Owned visual validation still deferred");
    ordinary.Validate();
    Check(ordinary.VisualValidations == 0 && Fixture.Visual == beforeVisual+1,"Unowned visual validation changed");
    var before=Fixture.Shape; ordinary.ValidateShapeState();
    Check(Fixture.Shape == before+1,"Unowned shape validation exempted");
    MachineHelper.OnMachineActioned(ordinary,ordinary.Modules);
    Check(Fixture.Machine == beforeMachine+1,"Nested ordinary machine lost its replacement");
    using(var call=LiunianPriorityScope.EnterMachine(ordinary)) {
        var stats=Fixture.Stats; ModuleEffectHelper.ComputeAllModuleTempStat(ordinary.Modules.childItems);
        Check(Fixture.Stats == stats+1,"Nested unrelated machine stats exempted");
    }
    Check(LiunianPriorityScope.OwnsSettlement,"Nested machine context not restored");
    var nested = Cabinet();
    using(var inner=LiunianPriorityScope.Begin(nested)) {
        Check(LiunianPriorityScope.OwnsMachine(nested) && !LiunianPriorityScope.OwnsMachine(machine),"Nested cabinet ownership wrong");
    }
    Check(LiunianPriorityScope.OwnsMachine(machine),"Nested cabinet scope not restored");
    Check(Task.Run(() => !LiunianPriorityScope.OwnsSettlement).GetAwaiter().GetResult(),"Priority leaked across threads");
    wineItem.Parent=ordinary; var visual=Fixture.Visual; wineItem.Validate();
    Check(Fixture.Visual == visual+1 && wineItem.VisualValidations == 1,"Moved item retained ownership exemption");
    wineItem.Parent=machine;
}
var post=Fixture.Machine; MachineHelper.OnMachineActioned(ordinary,ordinary.Modules);
Check(Fixture.Machine == post+1,"Foreign behavior not restored after own cycle");
using(var scope=LiunianPriorityScope.Begin(machine)) {
    using(var parent=LiunianValidationBatch.Begin(machine)) {
        machine.ValidateShapeState(); machine.ValidateShapeState();
        Check(parent.Deferred == 2 && !LiunianPriorityScope.OwnsValidation,"Skipped original leaked validation context");
    }
    machine.FailValidation=true;
    try { machine.ValidateShapeState(); throw new Exception("Expected validation fault"); } catch(InvalidOperationException) { checks++; }
    machine.FailValidation=false;
    Check(!LiunianPriorityScope.OwnsValidation,"Validation finalizer did not restore faulted state");
}
MachineHelper.BeforeCompleted = _ => throw new InvalidOperationException("cycle failure");
machine.onCycleEndSlotItemFunc(machine,null,null);
Check(!LiunianPriorityScope.OwnsSettlement,"Faulted completion leaked ownership");
MachineHelper.BeforeCompleted=null;
post=Fixture.Machine; MachineHelper.OnMachineActioned(ordinary,ordinary.Modules);
Check(Fixture.Machine == post+1,"Foreign replacement broken after fault");
// An additional unknown assembly's hook still disables the module fast path.
var unknownType=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("UnreviewedContract"),AssemblyBuilderAccess.Run)
    .DefineDynamicModule("main").DefineType("UnknownPatch",TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed);
var unknownPrefix=unknownType.DefineMethod("Prefix",MethodAttributes.Public|MethodAttributes.Static,typeof(bool),Type.EmptyTypes);
var emitter=unknownPrefix.GetILGenerator(); emitter.Emit(OpCodes.Ldc_I4_1); emitter.Emit(OpCodes.Ret);
var unknown=new HarmonyLib.Harmony("contract.unknown");
var tempMethod=AccessTools.Method(typeof(ModuleEffectHelper),nameof(ModuleEffectHelper.ComputeAllModuleTempStat));
unknown.Patch(tempMethod,prefix:new HarmonyMethod(unknownType.CreateType().GetMethod("Prefix")));
using(var scope=LiunianPriorityScope.Begin(machine))
using(var batch=LiunianModuleBatch.Begin(machine,machine.Modules)) {
    var temp=ModuleEffectHelper.TempComputes; var replacement=Fixture.Machine;
    MachineHelper.OnMachineActioned(machine,machine.Modules);
    Check(batch.Mode=="native-overlap:contract.unknown" && batch.Batched==0,"Unreviewed assembly enabled fast path");
    Check(ModuleEffectHelper.TempComputes==temp+5 && Fixture.Machine==replacement,"Unknown fallback reenabled reviewed replacement");
}
unknown.UnpatchSelf();
// A missing or additionally modified guard is not accepted as covered.
var managedEntry=foreignPatch.PatchMethod;
var guardInfo=HarmonyLib.Harmony.GetPatchInfo(managedEntry);
new HarmonyLib.Harmony("contract.tamper").Patch(managedEntry,prefix:new HarmonyMethod(AccessTools.Method(typeof(Tamper),nameof(Tamper.Prefix))));
Check(!LiunianOptimizerCompatibility.Covers(foreignPatch,nativeMethod),"Unknown managed overlap accepted");
new HarmonyLib.Harmony("contract.tamper").Unpatch(managedEntry,HarmonyPatchType.All,"contract.tamper");
Check(LiunianOptimizerCompatibility.Covers(foreignPatch,nativeMethod),"Removed managed overlap did not recover coverage");
LiunianOptimizerCompatibility.Shutdown();
Check(!LiunianOptimizerCompatibility.Covers(foreignPatch,nativeMethod),"Removed guard still accepted");
using(var scope=LiunianPriorityScope.Begin(machine)) {
    post=Fixture.Machine; MachineHelper.OnMachineActioned(machine,machine.Modules);
    Check(Fixture.Machine == post+1,"Shutdown retained a blocking adapter");
}
own.UnpatchSelf(); foreign.UnpatchSelf();
Console.WriteLine("PASS: "+checks+" real installed HarmonyX adapter/ordering contracts; game APIs simulated, third-party DLL not executed.");
internal static class Tamper { internal static void Prefix() { } }
