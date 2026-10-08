using System;
using System.Diagnostics;

namespace XIAOWOBrewingBarrel;

internal static class LiunianNativeAddress
{
    internal static readonly long Base = FindBase();
    private static long FindBase()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
                if (string.Equals(module.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                    return module.BaseAddress.ToInt64();
        }
        catch (Exception ex) { Core.Log?.Warning("Liunian native action check unavailable: " + ex.Message); }
        return 0;
    }
}
