using System;

namespace XIAOWOBrewingBarrel;

internal static class NativeActionIdentity
{
    // Read identity only, never detour these shared addresses. Unknown game
    // builds/delegates disable batching rather than invoking an address.
    internal static bool IsSingleNative(Il2CppSystem.Delegate action, int rva)
    {
        if (action == null || LiunianNativeAddress.Base == 0) return false;
        var entries = action.GetInvocationList();
        return entries != null && entries.Length == 1
            && entries[0].method_ptr.ToInt64() == LiunianNativeAddress.Base + rva;
    }
}
