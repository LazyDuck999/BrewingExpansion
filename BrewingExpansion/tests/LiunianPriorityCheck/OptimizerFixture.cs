// Instrumented contracts for the reviewed entry signatures; no third-party
// implementation is included or loaded. Prefix counters make execution visible.
using Il2Cpp;
namespace ProbablyOptimized;
internal static class Fixture { internal static int Machine, Stats, Shape, Visual, ResetIn, ResetOut; internal static bool Block = true; }
internal static class MachineActionedPatch { private static bool Prefix(GameItem machine, GameInventory moduleGrid) { Fixture.Machine++; return !Fixture.Block; } }
internal static class Skip_ComputeAllModuleTempStat { private static bool Prefix() { Fixture.Stats++; return !Fixture.Block; } }
internal static class Skip_ComputeModuleEffect { private static bool Prefix() { Fixture.Stats++; return !Fixture.Block; } }
internal static class Skip_ApplyBasicModuleEffect { private static bool Prefix() { Fixture.Stats++; return !Fixture.Block; } }
internal static class Guard_ResetAllTempStat { private static void Prefix() { Fixture.ResetIn++; } private static void Postfix() { Fixture.ResetOut++; } }
internal static class Dedup_TempStatShapeValidate { private static bool Prefix() { Fixture.Shape++; return !Fixture.Block; } }
internal static class Defer_ItemElementValidate { private static bool Prefix(GameItemElement __instance) { Fixture.Visual++; return !Fixture.Block; } }
