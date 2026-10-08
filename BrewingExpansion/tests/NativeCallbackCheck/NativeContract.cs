// Narrow test doubles for the game's extension contracts, not implementations of game settlement.
namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HarmonyPatch(System.Type type, string method) : System.Attribute
    { public System.Type TargetType { get; } = type; public string TargetMethod { get; } = method; }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class HarmonyPrefix : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class HarmonyPostfix : System.Attribute { }
}
namespace Il2CppSystem
{
    public abstract class Callback
    {
        static long next; public System.IntPtr Pointer { get; } = (System.IntPtr)(++next);
        protected readonly System.Delegate Target;
        protected Callback(System.Delegate target) => Target = target;
    }
    public sealed class Action(System.Delegate target) : Callback(target)
    { public void Invoke() => ((System.Action)Target)(); }
    public sealed class Action<T>(System.Delegate target) : Callback(target)
    { public void Invoke(T a) => ((System.Action<T>)Target)(a); }
    public sealed class Action<T, U>(System.Delegate target) : Callback(target)
    { public void Invoke(T a, U b) => ((System.Action<T, U>)Target)(a, b); }
    public sealed class Action<T, U, V>(System.Delegate target) : Callback(target)
    { public void Invoke(T a, U b, V c) => ((System.Action<T, U, V>)Target)(a, b, c); }
    public sealed class Func<T, U, V, R>(System.Delegate target) : Callback(target)
    { public R Invoke(T a, U b, V c) => ((System.Func<T, U, V, R>)Target)(a, b, c); }
}
namespace Il2CppInterop.Runtime
{
    public static class DelegateSupport
    { public static T ConvertDelegate<T>(System.Delegate action) => (T)System.Activator.CreateInstance(typeof(T), action); }
}
namespace MelonLoader
{ public static class MelonLogger { public static void Msg(string s) { } public static void Warning(string s) { } } }
namespace Il2Cpp
{
    public class GameInventory { } public class SlotMarker { } public class ModItemDirectory { }
    public class GameItem
    {
        public string identifier; public bool Owned; public int Uses, Calls; public PixelWindow contentWindow;
        public Il2CppSystem.Func<GameItem, GameInventory, SlotMarker, bool> canActivateSlotItemFunc, mayActivateSlotItemFunc;
        public Il2CppSystem.Action<GameItem, GameInventory, SlotMarker> onActivateSlotItemFunc;
        // Model the observed native reflection failure, not just Func.Invoke success.
        public bool CanActivateSlotItem()
        { if (canActivateSlotItemFunc != null) throw new System.Reflection.TargetException(); return false; }
        public bool MayActivateSlotItem()
        { if (mayActivateSlotItemFunc != null) throw new System.Reflection.TargetException(); return false; }
    }
    public class PixelWindow
    { static long next; public IntPtr Pointer { get; } = (IntPtr)(++next); public Il2CppSystem.Action openCallback;
      public List<GameItem> parentItems = new(); }
    public class RichTextBuilder
    { public List<string> Lines = new(); public void Clear() => Lines.Clear(); public void AddLine(string s) => Lines.Add(s);
      public RichText Build() => new() { Lines = new(Lines) }; }
    public class RichText { public List<string> Lines = new(); }
    public class GameItemElement : GameItem { }
    public class PlayerStore
    { public static PlayerStore Instance = new(); public bool OwnedRecipe, FailCheck;
      public bool IsPlayerOwnThisItem(string id) { if (FailCheck) throw new InvalidOperationException(); return OwnedRecipe; } }
    public class EmporiumEntry { public static EmporiumEntry Instance = new(); }
    public static class ModHook
    {
        public static string FailOn;
        public static List<Il2CppSystem.Action<ModItemDirectory>> Directory = new();
        public static List<Il2CppSystem.Action<RichTextBuilder, GameItem>> Tooltips = new();
        public static List<Il2CppSystem.Action> Leave = new(), Return = new();
        static void Add<T>(List<T> list, T value, string name)
        { if (FailOn == name) throw new InvalidOperationException(name); list.Add(value); }
        public static void add_OnModItemDirectoryInit(Il2CppSystem.Action<ModItemDirectory> v) => Add(Directory, v, "directory");
        public static void remove_OnModItemDirectoryInit(Il2CppSystem.Action<ModItemDirectory> v) => Directory.Remove(v);
        public static void add_OnCreateTooltipLate(Il2CppSystem.Action<RichTextBuilder, GameItem> v) => Add(Tooltips, v, "tooltip");
        public static void remove_OnCreateTooltipLate(Il2CppSystem.Action<RichTextBuilder, GameItem> v) => Tooltips.Remove(v);
        public static void add_OnLeavingStoreLate(Il2CppSystem.Action v) => Add(Leave, v, "leave");
        public static void remove_OnLeavingStoreLate(Il2CppSystem.Action v) => Leave.Remove(v);
        public static void add_OnReturningStoreLate(Il2CppSystem.Action v) => Add(Return, v, "return");
        public static void remove_OnReturningStoreLate(Il2CppSystem.Action v) => Return.Remove(v);
    }
}
namespace BrewingExpansion
{
    public static class BrewingExpansionMod
    {
        public const string MyPerfumeId = "be_contraband_perfume", FreedomPerfumeId = "be_freedom_perfume",
            PremiumBlindboxId = "be_premium_blindbox", MidautumnBlindboxId = "be_midautumn_blindbox",
            MidautumnMooncakeBoxId = "be_midautumn_mooncake_box", LowBlindboxId = "be_low_blindbox",
            MidBlindboxId = "be_mid_blindbox", AfterglowBlindboxId = "be_afterglow_blindbox",
            WineBlindboxId = "be_wine_blindbox", ContainerBlindboxId = "be_container_blindbox",
            FigurineBlindboxId = "be_figurine_blindbox", CassetteBundleId = "be_xuxu_cassette_bundle";
        public const string RecipeLockedHint = "locked"; public static string LastUsed;
        public static bool IsOurBrewingDocument(string id) => id is "recipe" or "guide";
        public static bool IsOwnedByPlayerPublic(Il2Cpp.GameItem item) => item.Owned;
        public static bool IsPerfume(Il2Cpp.GameItem item) => item.identifier is MyPerfumeId or FreedomPerfumeId;
        public static int GetPerfumeUses(Il2Cpp.GameItem item) => item.Uses;
        static void Use(Il2Cpp.GameItem item, string expected)
        { LastUsed = expected; item.Calls++; }
        public static void UseMyPerfume(Il2Cpp.GameItem i) => Use(i, i.identifier);
        public static void OpenPremiumBlindbox(Il2Cpp.GameItem i) => Use(i, PremiumBlindboxId);
        public static void OpenMidautumnBlindbox(Il2Cpp.GameItem i) => Use(i, MidautumnBlindboxId);
        public static void OpenMidautumnMooncakeBox(Il2Cpp.GameItem i) => Use(i, MidautumnMooncakeBoxId);
        public static void OpenLowBlindbox(Il2Cpp.GameItem i) => Use(i, LowBlindboxId);
        public static void OpenMidBlindbox(Il2Cpp.GameItem i) => Use(i, MidBlindboxId);
        public static void OpenAfterglowBlindbox(Il2Cpp.GameItem i) => Use(i, AfterglowBlindboxId);
        public static void OpenWineBlindbox(Il2Cpp.GameItem i) => Use(i, WineBlindboxId);
        public static void OpenContainerBlindbox(Il2Cpp.GameItem i) => Use(i, ContainerBlindboxId);
        public static void OpenFigurineBlindbox(Il2Cpp.GameItem i) => Use(i, FigurineBlindboxId);
        public static void OpenCassetteBundle(Il2Cpp.GameItem i) => Use(i, CassetteBundleId);
    }
    internal static class IncubatorLayout { public static void Refresh(Il2Cpp.PixelWindow w) { } }
}
namespace XIAOWOBrewingBarrel
{
    internal static class NativeItemDirectoryRegistration { public static int Calls; public static void Register(Il2Cpp.ModItemDirectory d) => Calls++; }
    internal static class BarrelItem { public static void CenterWindowControls(Il2Cpp.PixelWindow w) { } }
    internal static class ConcentratorItem { public static void AlignWindowControls(Il2Cpp.PixelWindow w) { } }
    internal static class LiunianAger { public static void RefreshWindowLayout(Il2Cpp.PixelWindow w) { } }
    internal static class BarrelMaintenanceEvents { public static int Calls; public static void QueueItem(Il2Cpp.GameItem i) => Calls++; }
    internal static class Core { public static Logger Log = new(); }
    internal class Logger { public void Warning(string s) { } }
}
namespace XuxuMusic
{
    internal static class LocalMp3Player { public static void Register(Il2Cpp.ModItemDirectory d) { } }
    internal static class DesktopMp3Player
    { public static int Leaves, Returns; public static void Leave(Il2Cpp.EmporiumEntry e) => Leaves++; public static void Arrive(Il2Cpp.EmporiumEntry e) => Returns++; }
    internal static class Core { public static XIAOWOBrewingBarrel.Logger Log = new(); }
}
