using System.Collections;
namespace UnityEngine {
    internal class Object {
        public bool Destroyed;
        public static void Destroy(Object o) { o.Destroyed = true; }
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b)
            || (ReferenceEquals(a, null) && !ReferenceEquals(b, null) && b.Destroyed)
            || (ReferenceEquals(b, null) && !ReferenceEquals(a, null) && a.Destroyed);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    internal class GameObject : Object {
        public bool Active = true;
        public void SetActive(bool active) {
            if (Destroyed) throw new NullReferenceException("Destroyed Unity object");
            Active = active;
        }
    }
}
namespace Il2Cpp {
    internal class EmporiumEntry : UnityEngine.Object {
        public IntPtr Pointer;
        public static EmporiumEntry Instance;
    }
    internal class GameItem {
        public string identifier;
        public IntPtr Pointer = new(42);
        private PixelWindow _window;
        public bool FailDetach;
        public PixelWindow contentWindow { get => _window; set {
            if (FailDetach) throw new NullReferenceException("Dead item window"); _window = value;
        } }
        public bool CanSelectSlotItem() => throw new System.Reflection.TargetException();
        public bool MaySelectSlotItem() => throw new System.Reflection.TargetException();
    }
    internal class PixelWindow {
        public bool Visible;
        public bool IsVisible() => Visible;
        public void Hide() { Visible = false; }
    }
    internal class ItemTooltipHandler : UnityEngine.Object {
        public static ItemTooltipHandler current;
        private GameItem _listener;
        public GameItem currentTooltipListener {
            get { if (Destroyed) throw new NullReferenceException(); return _listener; }
            set { if (Destroyed) throw new NullReferenceException(); _listener = value; }
        }
    }
    internal class OverlayHandler : UnityEngine.Object {
        public static OverlayHandler current;
        public int Hidden;
        public bool DeadChildren;
        public void HideTooltip() { if (DeadChildren) throw new NullReferenceException(); Hidden++; }
    }
    internal class PlayerStore { public static PlayerStore instance; }
    internal class Node { public object rectTransform = new(); }
    internal class RenderHandler {
        public static RenderHandler current = new();
        public object windowUICanvas = new();
        public Node windowRoot = new();
    }
}
namespace MelonLoader {
    internal static class MelonCoroutines {
        public static readonly List<IEnumerator> Pending = new();
        public static void Start(IEnumerator task) => Pending.Add(task);
        public static void Frame() {
            foreach (var task in Pending.ToArray()) if (!task.MoveNext()) Pending.Remove(task);
        }
    }
}
namespace HarmonyLib {
    [AttributeUsage(AttributeTargets.Class)]
    internal class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
}
namespace BrewingExpansion { internal static class MusicSupport { public static bool Loading = false; } }
namespace XuxuMusic {
    internal static class Mp3LoadState { public static bool Loading; }
    internal class Logger {
        public readonly List<string> Warnings = new();
        public void Warning(string message) => Warnings.Add(message);
    }
    internal static class Core { public static readonly Logger Log = new(); }
    internal static class LocalMp3Player {
        public static int Removed;
        public static bool IsPlayer(Il2Cpp.GameItem item) => item?.identifier == "be_mp3_player";
        public static void Remove(Il2Cpp.GameItem item) => Removed++;
        public static void Reset() => DesktopMp3Player.Release();
    }
}
