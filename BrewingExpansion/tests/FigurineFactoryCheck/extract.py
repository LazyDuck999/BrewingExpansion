"""Compile selected actual Class1 methods against a narrow simulated native API."""
import re
from pathlib import Path
here = Path(__file__).resolve().parent
source = (here.parents[1] / 'BrewingExpansion/Class1.cs').read_text(encoding='utf-8-sig')
def method(name):
    matches = re.findall(rf'^        (?:public|private) static [^\n]+\b{name}\([^\n]*\)\s*\n        \{{.*?^        \}}', source, re.M | re.S)
    if len(matches) != 1: raise ValueError(name)
    return matches[0]
methods = '\n'.join(method(n) for n in ['InitMidautumnDelayedFactories', 'CreateMidautumnCommon', 'CreateFigurine', 'RepairLegacyFigurines'])
arrays = source[source.index('        public const string IchigoFigurineId'):source.index('        // ★ 电子幽灵')]
ids = re.findall(r'\b[A-Z]\w+Id\b', method('InitMidautumnDelayedFactories'))
declarations = []
for name in set(ids) - {'FigurineIds'}:
    found = re.search(rf'public const string {name} = "[^"]+";', source)
    if not found: raise ValueError(name)
    if name not in arrays: declarations.append(found[0])
patch = source[source.index('    public static class PatchDirectoryMasterMidautumn'):]
prefix = re.search(r'        public static bool Prefix\(string identifier, bool isOwned, ref GameItem __result\).*?^        \}', patch, re.M | re.S)[0]
prefix = prefix.replace('public static bool Prefix(', 'public static bool CreateFromDirectory(')
if prefix.index('InitMidautumnDelayedFactories();') > prefix.index('TryGetValue('): raise ValueError('Late initialization')
for name in ['CreateMidautumnMoonFan', 'CreateMidautumnMoonLamp', 'CreateMidautumnJadeRabbit', 'CreateMidautumnLantern', 'CreateMidautumnBlindbox', 'CreateMidautumnMooncakeBox', 'CreateWurenMooncake', 'CreateMidautumnBag']:
    declarations.append(f'public static GameItem {name}() => new();')
declarations.append('public const string MidautumnMoonOsmanthusName = "fruit", MidautumnMoonOsmanthusSpriteKey = "fruit"; public static GameItem CreateFruitItem(string id, string name, string key) => new();')
(here / 'ExtractedProduction.cs').write_text('''// Generated from current production methods by extract.py. Native API is simulated.
using Il2Cpp; using MelonLoader; using BrewingExpansion;
namespace BrewingExpansion { public static partial class BrewingExpansionMod {
private static bool _midautumnFactoriesInited;
public const string DecorationTag = "DECORATION", OwnedTag = "IS_OWNED_TAG";
''' + '\n'.join(declarations) + '\n' + arrays + '\n' + methods + '\n' + prefix + '\n}}', encoding='utf-8')
