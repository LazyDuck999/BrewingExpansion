using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

if (args.Length != 3) throw new ArgumentException("Music DLL, prerequisite DLL and MP3 PNG required.");
using var musicFile = File.OpenRead(args[0]);
using var musicPe = new PEReader(musicFile);
using var brewingFile = File.OpenRead(args[1]);
using var brewingPe = new PEReader(brewingFile);
var music = musicPe.GetMetadataReader();
var brewing = brewingPe.GetMetadataReader();
var checks = 0;
void Check(bool value, string label)
{
    if (!value) throw new Exception(label);
    checks++;
}
string TypeName(MetadataReader md, EntityHandle handle) => handle.Kind switch
{
    HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)handle).Namespace)
        + "." + md.GetString(md.GetTypeReference((TypeReferenceHandle)handle).Name),
    HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Namespace)
        + "." + md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
    _ => ""
};
var refs = music.AssemblyReferences.Select(music.GetAssemblyReference).ToArray();
Check(music.GetString(music.GetAssemblyDefinition().Name) == "XuxuMusic", "Wrong music assembly name");
Check(refs.Any(r => music.GetString(r.Name) == "BrewingExpansion"), "Missing prerequisite assembly reference");
var dep = music.GetAssemblyDefinition().GetCustomAttributes().Select(music.GetCustomAttribute)
    .Single(a => a.Constructor.Kind == HandleKind.MemberReference
        && TypeName(music, music.GetMemberReference((MemberReferenceHandle)a.Constructor).Parent)
            == "MelonLoader.MelonAdditionalDependenciesAttribute");
var depBlob = music.GetBlobReader(dep.Value);
Check(depBlob.ReadUInt16() == 1 && depBlob.ReadInt32() == 1
    && depBlob.ReadSerializedString() == "BrewingExpansion", "Wrong MelonLoader dependency declaration");
Check(music.GetAssemblyDefinition().GetCustomAttributes().Select(music.GetCustomAttribute)
    .Any(a => a.Constructor.Kind == HandleKind.MemberReference
        && TypeName(music, music.GetMemberReference((MemberReferenceHandle)a.Constructor).Parent)
            == "MelonLoader.HarmonyDontPatchAllAttribute"), "Unconditional auto-patching bypasses compatibility guard");
var brewingTypes = brewing.TypeDefinitions.Select(h => TypeName(brewing, h)).ToArray();
Check(brewingTypes.Contains("BrewingExpansion.MusicSupport"), "Missing prerequisite API");
Check(!brewingTypes.Any(t => t.EndsWith(".PatchGameItemGetShape") || t.EndsWith(".PatchGameItemSetShape")),
    "Unsafe native shared-address shape patches still present");
Check(brewingTypes.Contains("XIAOWOBrewingBarrel.WinePrinterLoadLayoutPatch"),
    "Printer shape restoration missing after removing shared getter/setter hooks");
Check(!brewingTypes.Any(t => t.Contains("DesktopMp3Player") || t.Contains("LocalMp3Player")
    || t.Contains("FixedMp3Creation")), "Player still duplicated inside prerequisite");
Check(music.TypeDefinitions.Any(h => TypeName(music, h) == "XuxuMusic.DesktopMp3Player"), "Player missing from addon");
Check(music.TypeReferences.Any(h => TypeName(music, h) == "BrewingExpansion.MusicSupport"), "Addon does not call shared API");
foreach (var (md, modName) in new[] { (music, "MusicMod"), (brewing, "BrewingExpansionMod") })
{
    var type = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == modName);
    Check(!type.GetMethods().Select(md.GetMethodDefinition)
        .Any(m => md.GetString(m.Name) is "OnUpdate" or "OnLateUpdate"), "Global frame hook present in " + modName);
}
var imageResource = brewing.ManifestResources.Select(brewing.GetManifestResource)
    .Single(r => brewing.GetString(r.Name) == "BrewingExpansion.Textures.mp3.png");
var bytes = brewingPe.GetSectionData(brewingPe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress
    + (int)imageResource.Offset).GetReader();
Check(bytes.ReadBytes(bytes.ReadInt32()).SequenceEqual(File.ReadAllBytes(args[2])), "Shared sprite bytes differ");
foreach (var png in new[] { "黑崎一护", "朽木露琪亚", "石田雨龙", "噢我的天啊是铱星大海参" })
{
    var resource = brewing.ManifestResources.Select(brewing.GetManifestResource)
        .Single(r => brewing.GetString(r.Name) == "BrewingExpansion.Textures." + png + ".png");
    var spriteBytes = brewingPe.GetSectionData(brewingPe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress
        + (int)resource.Offset).GetReader();
    Check(spriteBytes.ReadBytes(spriteBytes.ReadInt32()).SequenceEqual(File.ReadAllBytes(
        Path.Combine(Path.GetDirectoryName(args[2])!, png + ".png"))), "Figurine PNG missing or changed: " + png);
}
Check(!music.ManifestResources.Any(), "Addon unexpectedly duplicates embedded assets");
foreach (var path in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(args[2])!, "..", "music"), "*.mp3"))
{
    var resource = brewing.ManifestResources.Select(brewing.GetManifestResource)
        .Single(r => brewing.GetString(r.Name) == "BrewingExpansion.music." + Path.GetFileName(path));
    var audioBytes = brewingPe.GetSectionData(brewingPe.PEHeaders.CorHeader!.ResourcesDirectory.RelativeVirtualAddress
        + (int)resource.Offset).GetReader();
    Check(audioBytes.ReadBytes(audioBytes.ReadInt32()).SequenceEqual(File.ReadAllBytes(path)),
        "Embedded cassette audio differs from processed source: " + Path.GetFileName(path));
}
string Constant(MetadataReader md, string typeName, string fieldName)
{
    var type = md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Name) == typeName);
    var field = type.GetFields().Select(md.GetFieldDefinition).Single(f => md.GetString(f.Name) == fieldName);
    return Encoding.Unicode.GetString(md.GetBlobBytes(md.GetConstant(field.GetDefaultValue()).Value));
}
Check(Constant(music, "LocalMp3Player", "Id") == "be_mp3_player", "Legacy player ID changed");
Check(Constant(music, "DesktopMp3Player", "SaveFileKey") == "BrewingExpansion.DesktopMp3.File.v1"
    && Constant(music, "DesktopMp3Player", "SaveVolumeKey") == "BrewingExpansion.DesktopMp3.Volume.v1",
    "Saved song/volume compatibility changed");
Check(new[] { "NativeGameEvents", "NativeItemActivation", "NativeWindowCallbacks" }
    .All(n => brewingTypes.Contains("BrewingExpansion." + n)), "Native callback migration missing");
Check(new[] { "NativeActivationCanUsePatch", "NativeActivationMayUsePatch" }
    .All(n => brewingTypes.Contains("BrewingExpansion." + n)), "Scoped eligibility reflection guards missing");
var activationType = brewing.TypeDefinitions.Select(brewing.GetTypeDefinition)
    .Single(t => brewing.GetString(t.Name) == "NativeItemActivation");
Check(activationType.GetFields().Select(brewing.GetFieldDefinition)
    .All(f => brewing.GetString(f.Name) != "CanUse"), "Reflection-unsafe converted Func retained");
var musicTypes = music.TypeDefinitions.Select(h => TypeName(music, h)).ToArray();
Check(musicTypes.Contains("XuxuMusic.NativeMusicEvents"), "Native music events missing");
Check(!musicTypes.Any(t => t.EndsWith(".Mp3DirectoryPatch") || t.EndsWith(".DesktopMp3LeavePatch")
    || t.EndsWith(".DesktopMp3ArrivePatch")), "Migrated music patches still compiled");
Check(!brewingTypes.Any(t => t.EndsWith(".BarrelDirectoryPatch") || t.EndsWith(".BarrelWindowShowPatch")
    || t.EndsWith(".BarrelWindowShowPositionedPatch") || t.EndsWith(".PatchRecipeTooltip")),
    "Migrated directory/window/tooltip patches still compiled");
foreach (var (md, names) in new[] {
    (brewing, new[] { "add_OnModItemDirectoryInit", "remove_OnModItemDirectoryInit" }),
    (music, new[] { "add_OnModItemDirectoryInit", "remove_OnModItemDirectoryInit", "add_OnLeavingStoreLate", "remove_OnLeavingStoreLate", "add_OnReturningStoreLate", "remove_OnReturningStoreLate" }) })
    Check(names.All(n => md.MemberReferences.Select(md.GetMemberReference).Any(m => md.GetString(m.Name) == n
        && TypeName(md, m.Parent) == "Il2Cpp.ModHook")), "Native event subscriptions/cleanup missing");
Check(brewingTypes.Contains("BrewingExpansion.RecipeTooltipLockPatch"), "Final recipe tooltip lock missing");
var nativeEvents = brewing.TypeDefinitions.Select(brewing.GetTypeDefinition)
    .Single(t => brewing.GetString(t.Name) == "NativeGameEvents");
Check(nativeEvents.GetFields().Select(brewing.GetFieldDefinition).All(f => brewing.GetString(f.Name) != "TooltipReady"),
    "Premature native tooltip event still retained");
var playerType = music.TypeDefinitions.Select(music.GetTypeDefinition)
    .Single(t => music.GetString(t.Name) == "LocalMp3Player");
Check(playerType.GetFields().Select(music.GetFieldDefinition).All(f => music.GetString(f.Name) != "NeverSelect"),
    "Reflection-unsafe MP3 selection Func retained");
foreach (var name in new[] { "DesktopMp3SelectPatch", "DesktopMp3MaySelectPatch" })
{
    var guard = music.TypeDefinitions.Select(music.GetTypeDefinition).Single(t => music.GetString(t.Name) == name);
    var methods = guard.GetMethods().Select(music.GetMethodDefinition).Select(m => music.GetString(m.Name)).ToArray();
    Check(methods.Contains("Prefix") && !methods.Contains("Postfix"), "MP3 selection checked after unsafe native call: " + name);
}
Check(new[] { "LiunianModuleBatch", "LiunianValidationBatch", "LiunianModuleUseBatchPatch", "LiunianParentValidationPatch" }
    .All(n => brewingTypes.Contains("XIAOWOBrewingBarrel." + n)), "Scoped Liunian cycle optimization missing");
var nightPatch = brewing.TypeDefinitions.Select(brewing.GetTypeDefinition)
    .Single(t => brewing.GetString(t.Name) == "LiunianChillAfterNightPatch");
Check(nightPatch.GetMethods().Select(brewing.GetMethodDefinition)
    .Any(m => brewing.GetString(m.Name) == "Finalizer" && m.GetCustomAttributes().Select(brewing.GetCustomAttribute)
        .Any(a => a.Constructor.Kind == HandleKind.MemberReference
            && TypeName(brewing, brewing.GetMemberReference((MemberReferenceHandle)a.Constructor).Parent)
                == "HarmonyLib.HarmonyFinalizer")), "Failed-night cleanup missing");
Check(!brewingTypes.Contains("XIAOWOBrewingBarrel.LiunianOptimizerCompatibility")
    && !brewingTypes.Contains("XIAOWOBrewingBarrel.LiunianPriorityScope"),
    "Withdrawn startup native-rehook adapter retained");
Check(new[] { "WineRackAgingOptimization", "NativeWineRackBatchFactoryPatch", "MachineModuleCycleBatchPatch" }
    .All(n => brewingTypes.Contains("XIAOWOBrewingBarrel." + n)), "Global native optimization entry missing");
Check(!brewingTypes.Any(n => n.Contains("GlobalMachineRuntimeTest") || n.Contains("GlobalTestNo")),
    "Local automatic test/save guards present in distributable DLL");
Console.WriteLine("Addon/prerequisite release checks passed: " + checks);
