"""Extract actual cold-state and night/settlement callback methods for native API simulation."""
import re
from pathlib import Path
here=Path(__file__).resolve().parent
source=(here.parents[1]/'BrewingExpansion/Barrel/LiunianAger.cs').read_text(encoding='utf-8-sig')
def method(name):
    matches=re.findall(rf'^    (?:internal|private) static [^\n]+\b{name}\([^\n]*\)\s*\n    \{{.*?^    \}}',source,re.M|re.S)
    if len(matches)!=1:raise ValueError(name)
    return matches[0]
methods='\n'.join(method(n) for n in ['ChillStoredWine','CompleteCycle','BeginNight','FinishNight','AbortNight','ApplyCycleChill'])
callbacks=source[source.index('[HarmonyLib.HarmonyPatch(typeof(MachineHelper), nameof(MachineHelper.OnMachineActioned))]'):]
callbacks=callbacks.replace('private static','internal static')
(here/'ExtractedCold.cs').write_text('''// Generated from current production methods. Native API is simulated.
using Il2Cpp;
namespace XIAOWOBrewingBarrel;
internal static partial class LiunianAger {
private static readonly Dictionary<long, GameItem> NightCycles = new();
private static bool _inNight;
'''+methods+'\n}\n'+callbacks,encoding='utf-8')
