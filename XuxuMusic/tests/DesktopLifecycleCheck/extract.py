"""Run actual fixture scheduling/cleanup and scoped selection guards with simulated native UI."""
import re
from pathlib import Path
here = Path(__file__).resolve().parent
source = (here.parents[1] / 'DesktopMp3Player.cs').read_text(encoding='utf-8-sig')
def method(name):
    matches = re.findall(rf'^    (?:internal|private) static [^\n]+\b{name}\([^\n]*\)\s*\n    \{{.*?^    \}}', source, re.M | re.S)
    if len(matches) != 1: raise ValueError(name)
    return matches[0]
fields = source[source.index('    private static EmporiumEntry _entry;'):source.index('    internal static bool IsFixture')]
methods = '\n'.join(method(n) for n in ['Schedule', 'InstallWhenReady', 'Hover', 'ClearHover', 'Leave', 'Arrive', 'Release', 'DestroyScene'])
guards = source[source.index('[HarmonyPatch(typeof(GameItem), nameof(GameItem.CanSelectSlotItem))]'):]
local = (here.parents[1] / 'LocalMp3Player.cs').read_text(encoding='utf-8-sig')
assert 'NeverSelect' not in local
assert 'item.canSelectSlotItemFunc = null;' in local and 'item.maySelectSlotItemFunc = null;' in local
newrun = re.search(r'internal static class Mp3NewRunPatch\s*\{.*?^\}', local, re.M | re.S)[0]
(here / 'ExtractedProduction.cs').write_text('''// Generated from current production methods; native rendering is simulated.
using System.Collections; using Il2Cpp; using MelonLoader; using HarmonyLib; using UnityEngine;
namespace XuxuMusic {
internal static class DesktopMp3Player {
''' + fields + methods + '''
internal static int Installed, Positioned;
private static void Install(EmporiumEntry entry) {
    Release(); _sceneEntry = _entry = entry; _fixture = new(); _hoverRange = new();
    _item = new() { identifier = "be_mp3_player", contentWindow = new() }; Installed++;
}
internal static void Position(EmporiumEntry entry) { Positioned++; }
internal static void SetHover(GameObject hover) { _hoverRange = hover; }
internal static GameItem Item => _item;
internal static GameObject Fixture => _fixture;
internal static void ClearForTest() => ClearHover();
}
''' + guards + '\n' + newrun + '\n}', encoding='utf-8')
