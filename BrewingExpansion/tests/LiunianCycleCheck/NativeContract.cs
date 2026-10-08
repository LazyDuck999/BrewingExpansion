// Simulation of inspected native contracts. This does not load game objects.
using System.Reflection;
using XIAOWOBrewingBarrel;
#if !REAL_HARMONY
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch(Type type, string method) : Attribute { public Type Type = type; public string Method = method; }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyFinalizer : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class HarmonyPriority(int priority) : Attribute { public int Value = priority; }
    public static class Priority { public const int Last = 0, First = 800; }
    public static class AccessTools {
        public static MethodInfo Method(Type t, string name, Type[] parameters = null) => parameters == null
            ? t.GetMethod(name, BindingFlags.Public | BindingFlags.Static)
            : t.GetMethod(name, parameters);
    }
    public class Patch { public MethodInfo PatchMethod; public string owner; }
    public class Patches {
        public List<Patch> Prefixes = new(), Postfixes = new(), Transpilers = new(), Finalizers = new(), ILManipulators = new();
    }
    public static class Harmony {
        public static MethodInfo ForeignMethod;
        public static Patches Info;
        public static Patches GetPatchInfo(MethodBase method) => Equals(method, ForeignMethod) ? Info : null;
    }
}
#endif
namespace Il2CppSystem {
    public class Object { public object Value; }
    public readonly struct Int64(long value) { public Object BoxIl2CppObject() => new() { Value = value }; }
    public delegate void Action<T>(T a);
    public delegate void Action<T,U,V>(T a,U b,V c);
    public delegate R Func<T,R>(T a);
    public class Delegate {
        public IntPtr method_ptr;
        public Delegate[] Multicast;
        public Delegate[] GetInvocationList() => Multicast ?? new[] { this };
    }
    public class NativeAction : Delegate {
        public System.Action<Il2Cpp.GameItem> Effect;
        public void Invoke(Il2Cpp.GameItem item) => Effect?.Invoke(item);
    }
}
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> { } }
namespace Il2CppInterop.Runtime {
    public static class DelegateSupport {
        public static T ConvertDelegate<T>(System.Delegate d) =>
            (T)(object)System.Delegate.CreateDelegate(typeof(T), d.Target, d.Method);
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {
    public class Il2CppReferenceArray<T>(int count) {
        private readonly T[] values = new T[count];
        public T this[int i] { get => values[i]; set => values[i] = value; }
    }
}
namespace Il2Cpp {
    public class GraphNodeStorage {
        private static long next;
        public IntPtr Pointer = new(++next);
        public T Cast<T>() where T : class => this as T;
    }
    public class GameInventory : GraphNodeStorage {
        public Il2CppSystem.Collections.Generic.List<GameItem> childItems = new();
        public GameItem Owner;
        public GameItem GetParentItem() => Owner;
    }
    public class GameGridInventory : GameInventory { }
    public class GameSlotInventory : GameInventory { }
    public class SlotMarker { }
    public class ItemFeature { public string identifier; public bool isDisabled; }
    public class TagState {
        public bool valueEnabled; public int valueInt; public long valueLong; public string valueString;
        public TagState Enable() { valueEnabled = true; return this; }
        public TagState Disable() { valueEnabled = false; return this; }
        public TagState Clone() => (TagState)MemberwiseClone();
    }
    public class TagSystem {
        public Dictionary<string,TagState> dict = new();
        public TagState GetTag(string name) {
            if (!dict.TryGetValue(name,out var tag)) dict[name] = tag = new();
            return tag;
        }
        public TagSystem Clone() => new() { dict = dict.ToDictionary(p => p.Key,p => p.Value.Clone()) };
    }
    public class GameItem : GraphNodeStorage {
        public string identifier;
        public int uniqueId, Validations, Quality, Permanent = 100, Temporary = 100, Uses;
        public long unitValue = 100;
        public bool Destroyed, FailValidation, Power = true;
        public string FailTag;
        public System.Action WhenValidating;
        public GameItem Parent;
        public GameInventory parentInventory => Parent == null ? null : new() { Owner = Parent };
        public GameGridInventory Storage = new(), Modules = new();
        public GameSlotInventory Battery = new();
        public Il2CppSystem.Collections.Generic.List<GameItem> Children = new();
        public List<ItemFeature> itemFeatures = new();
        public TagSystem state = new(), modifiedState = new();
        public Il2CppSystem.Action<GameItem,GameInventory,SlotMarker> onCycleEndSlotItemFunc = (_,_,_) => { };
        public bool IsTag(string tag) => modifiedState.dict.TryGetValue(tag,out var s) && s.valueEnabled;
        public TagState GetTagReadonly(string tag) => modifiedState.GetTag(tag).Clone();
        public GameItem ModifyTag(string tag, Il2CppSystem.Action<TagState> action, bool skipValidate = false) {
            action(state.GetTag(tag));
            if (tag == FailTag) throw new InvalidOperationException("Injected write failure");
            if (!skipValidate) ValidateShapeState(); return this;
        }
        public void DisableTag(string tag,bool skipValidate = false) => ModifyTag(tag,s => s.Disable(),skipValidate);
        public T TryCast<T>() where T : class => this as T;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void ValidateShapeState() {
#if !REAL_HARMONY
            if (!LiunianParentValidationPatch.Prefix(this)) return;
#endif
            if (FailValidation) throw new InvalidOperationException("Injected validation failure");
            WhenValidating?.Invoke(); Validations++; modifiedState = state.Clone(); Parent?.ValidateShapeState();
        }
    }
    public class GameItemElement : GameItem {
        public bool IsDestroyed() => Destroyed;
        public int VisualValidations;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Validate() => VisualValidations++;
    }
    public static class GraphUtils {
        public static Il2CppSystem.Collections.Generic.List<T> FindAllChildrenType<T>(GraphNodeStorage root, Il2CppSystem.Func<T,bool> filter) where T : GameItem {
            var result = new Il2CppSystem.Collections.Generic.List<T>();
            void Visit(GameItem item) { if (item is T v && filter(v)) result.Add(v); foreach(var c in item.Children) Visit(c); }
            foreach(var item in ((GameInventory)root).childItems) Visit(item);
            return result;
        }
    }
    public static class MachineWineRack {
        public static bool CanAgeWine(GameItem item) => item.IsTag("WINE_CONTAINER_TAG");
        public static int GetAgingDays(GameItem machine) => 1 + Math.Max(0,machine.Quality) / 125;
    }
    public static class AgableHelper {
        public static int GetAge(GameItem item) => item.GetTagReadonly("AGE_INT").valueInt;
        public static bool HasAged(GameItem item) => item.IsTag("AGE_INT");
        public static void OnAge(GameItem item) => item.ModifyTag("AGE_INT",s => { s.Enable(); s.valueInt++; });
        public static void ResetAge(GameItem item) => item.ModifyTag("AGE_INT",s => { s.Disable(); s.valueInt = 0; });
        public static void AddAgeTooltipLine(RichTextBuilder b,GameItem item,string locKey = "mech_wine_age") => b.AddLine("Age: " + GetAge(item));
    }
    public static class MachineFridge {
        public static ItemFeature CreateChilledFeature() => new() { identifier = "cold" };
        public static void ChillItem(GameItem item) {
            var old = item.itemFeatures.FirstOrDefault(f => f.identifier == "cold");
            if (old == null) item.itemFeatures.Add(new() { identifier = "cold" }); else old.isDisabled = false;
            item.ValidateShapeState();
        }
    }
    public static class ModuleEffectHelper {
        public class ModuleEffect { public Il2CppSystem.NativeAction onAnyAction, onUsedAction; }
        public static Dictionary<string,ModuleEffect> moduleEffects = new();
        public static int OriginalActions, TempComputes;
        public static readonly List<long> ActionOrder = new();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ActivateOnUseAction(GameItem item,GameInventory moduleGrid,GameItem machine) {
#if !REAL_HARMONY
            if (MachineHelper.Optimize && !LiunianModuleUseBatchPatch.Prefix(item,moduleGrid,machine)) return;
#endif
            OriginalActions++;
            foreach(var tag in new[] { "MODULE_EFFECT1_TAG","MODULE_EFFECT2_TAG" })
                if (item.IsTag(tag)) moduleEffects[item.GetTagReadonly(tag).valueString].onUsedAction?.Invoke(item);
            ComputeAllModuleTempStat(moduleGrid.childItems);
            ModuleHelper.ComputeModuleEffect(moduleGrid.childItems,machine);
            ModuleHelper.ApplyBasicModuleEffect(moduleGrid,null,machine);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ComputeAllModuleTempStat(Il2CppSystem.Collections.Generic.List<GameItem> list) {
            TempComputes++; foreach(var item in list) {
#if REAL_HARMONY
                ResetAllTempStat(item);
#else
                item.Temporary = item.Permanent;
#endif
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ResetAllTempStat(GameItem item) {
            item.Temporary = item.Permanent;
#if REAL_HARMONY
            for(var i=0;i<3;i++) item.ModifyTag("TEMP_"+i,s => { s.Enable(); s.valueInt=item.Permanent; });
#endif
        }
        public static void HandleModuleEffectAction(GameItem item,GameInventory grid) { }
        public static void Degrade(GameItem item,int amount) {
            ActionOrder.Add(item.uniqueId); item.Uses++; item.Permanent -= amount; item.ValidateShapeState();
        }
    }
    public static class ModuleHelper {
        public static int Computes, Applies;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ComputeModuleEffect(GameInventory grid,GameItem item,GameItem machine) => ComputeModuleEffect(grid.childItems,machine);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ComputeModuleEffect(Il2CppSystem.Collections.Generic.List<GameItem> list,GameItem machine) {
            Computes++; machine.Quality = list.Sum(m => m.Temporary);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void ApplyBasicModuleEffect(GameInventory grid,GameItem item,GameItem machine) { Applies++; }
    }
    public static class MachineHelper {
        public static bool Optimize = true;
        public static int PowerDraws, Completed;
        public static System.Action<GameItem> BeforeCompleted;
        public static bool TryDrawCyclePower(GameItem machine,GameSlotInventory slot) { if (!machine.Power) return false; PowerDraws++; return true; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static void OnMachineActioned(GameItem machine,GameInventory moduleGrid) {
#if !REAL_HARMONY
            MachineModuleCycleBatchPatch.Prefix(machine,moduleGrid,out var moduleScope);
            try {
#endif
            foreach(var item in moduleGrid.childItems) ModuleEffectHelper.ActivateOnUseAction(item,moduleGrid,machine);
            ModuleEffectHelper.ComputeAllModuleTempStat(moduleGrid.childItems);
            ModuleHelper.ComputeModuleEffect(moduleGrid,null,machine);
            ModuleHelper.ApplyBasicModuleEffect(moduleGrid,null,machine);
            BeforeCompleted?.Invoke(machine); Completed++;
#if !REAL_HARMONY
            MachineModuleCycleBatchPatch.Postfix(machine);
            LiunianChillOnCyclePatch.Postfix(machine);
            } finally { MachineModuleCycleBatchPatch.Finalizer(null,moduleScope); }
#endif
        }
    }
    public class PlayerStore { public static void EndNight() { } }
    public class RichTextBuilder { public string Text; public void AddLine(string s,bool ignoreEmpty = true,RenderHandler.ColorPalette color = default) => Text += s; }
    public static class RenderHandler { public enum ColorPalette { LightCyan } }
    public static class LocHelper {
        public static string GetLocalizedMechanic(string key,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> args) => "Age: " + args[0].Value;
    }
}
namespace XIAOWOBrewingBarrel {
    internal static class LiunianNativeAddress { internal static long Base = 0x10000000; }
    internal static class Core { internal static Logger Log = new(); }
    internal class Logger { internal List<string> Messages = new(); public void Msg(string s) => Messages.Add(s); public void Warning(string s) => Messages.Add(s); public void Error(string s) => Messages.Add(s); }
    internal static class BarrelItem { public static string Text(string zh,string en) => en; }
    internal static partial class LiunianAger {
        internal const string Id = "be_liunian_ager";
        internal static int ValueRefreshes, DirtyMarks;
        internal static Il2Cpp.GameGridInventory Storage(Il2Cpp.GameItem item) => item.Storage;
        internal static Il2Cpp.GameGridInventory Modules(Il2Cpp.GameItem item) => item.Modules;
        internal static Il2Cpp.GameSlotInventory Battery(Il2Cpp.GameItem item) => item.Battery;
        internal static void MarkValueDirty(Il2Cpp.GameGridInventory grid) { DirtyMarks++; }
        internal static void RefreshVisibleValueIfNeeded(Il2Cpp.GameItem item) { ValueRefreshes++; }
    }
}
