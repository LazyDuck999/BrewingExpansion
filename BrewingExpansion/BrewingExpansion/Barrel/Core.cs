using MelonLoader;

namespace XIAOWOBrewingBarrel;

// The barrel now lives in BrewingExpansion.dll; keep its original logging calls local.
internal static class Core
{
    internal static readonly BarrelLogger Log = new();
}

internal sealed class BarrelLogger
{
    internal void Msg(string message) => MelonLogger.Msg("[BE-BARREL] " + message);
    internal void Warning(string message) => MelonLogger.Warning("[BE-BARREL] " + message);
    internal void Error(string message) => MelonLogger.Error("[BE-BARREL] " + message);
}
