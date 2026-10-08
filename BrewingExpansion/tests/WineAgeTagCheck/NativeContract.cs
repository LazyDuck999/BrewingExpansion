// A test double for the inspected native tag contract, not a game runtime test.
// ModifyTag/GetTag create disabled entries; reads use the modified snapshot;
// ValidateShapeState clones state; OnAge explicitly enables AGE_INT.
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
namespace Il2CppSystem
{
    public class Object { public object Value; }
    public readonly struct Int64
    {
        private readonly long value;
        public Int64(long number) { value = number; }
        public Object BoxIl2CppObject() => new() { Value = value };
    }
    public delegate void Action<T>(T value);
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppReferenceArray<T>(int size)
    {
        private readonly T[] data = new T[size];
        public T this[int index] { get => data[index]; set => data[index] = value; }
    }
}
namespace Il2Cpp
{
    public class TagState
    {
        public bool valueEnabled;
        public int valueInt;
        public long valueLong;
        public TagState Enable() { valueEnabled = true; return this; }
        public TagState Disable() { valueEnabled = false; return this; }
        public TagState Clone() => (TagState)MemberwiseClone();
    }
    public class TagSystem
    {
        public Dictionary<string, TagState> dict = new();
        public TagState GetTag(string name)
        {
            if (!dict.TryGetValue(name, out var tag)) dict[name] = tag = new();
            return tag;
        }
        public TagSystem Clone() => new() { dict = dict.ToDictionary(p => p.Key, p => p.Value.Clone()) };
    }
    public class GameItem
    {
        public TagSystem state = new();
        public TagSystem modifiedState = new();
        public long unitValue = 100;
        public int uniqueId;
        public int Validations;
        public string FailTag;
        public bool IsTag(string name) => modifiedState.dict.TryGetValue(name, out var tag) && tag.valueEnabled;
        public TagState GetTagReadonly(string name) => modifiedState.GetTag(name).Clone();
        public GameItem ModifyTag(string name, Il2CppSystem.Action<TagState> action, bool skipValidate = false)
        {
            action(state.GetTag(name));
            if (name == FailTag) throw new InvalidOperationException("Injected write failure");
            if (!skipValidate) ValidateShapeState();
            return this;
        }
        public void DisableTag(string name, bool skipValidate = false) => ModifyTag(name, s => s.Disable(), skipValidate);
        public void ValidateShapeState() { modifiedState = state.Clone(); Validations++; }
    }
    public static class AgableHelper
    {
        public static int GetAge(GameItem item) => item.GetTagReadonly("AGE_INT").valueInt;
        public static bool HasAged(GameItem item) => item.IsTag("AGE_INT");
        public static void OnAge(GameItem item) => item.ModifyTag("AGE_INT", s => { s.Enable(); s.valueInt++; });
        public static void ResetAge(GameItem item) => item.ModifyTag("AGE_INT", s => { s.Disable(); s.valueInt = 0; });
        public static void AddAgeTooltipLine(RichTextBuilder builder, GameItem item, string locKey = "mech_wine_age") =>
            builder.AddLine("Age: " + GetAge(item));
    }
    public class RichTextBuilder
    {
        public string Text = "";
        public void AddLine(string text, bool ignoreEmpty = true, RenderHandler.ColorPalette color = default) => Text += text;
    }
    public static class RenderHandler { public enum ColorPalette { LightCyan } }
    public static class LocHelper
    {
        public static string GetLocalizedMechanic(string key, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> args) =>
            "Age: " + args[0].Value;
    }
}
namespace XIAOWOBrewingBarrel
{
    public static class Core { public static Logger Log = new(); }
    public class Logger { public void Warning(string text) { } public void Error(string text) { } }
    public static class BarrelItem { public static string Text(string zh, string en) => en; }
}
