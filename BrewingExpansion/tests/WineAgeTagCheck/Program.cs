using System.Reflection;
using Il2Cpp;
using XIAOWOBrewingBarrel;

var checks = 0;
void Check(bool result, string label)
{
    if (!result) throw new Exception(label);
    checks++;
}
GameItem Wine()
{
    var wine = new GameItem();
    wine.ModifyTag("WINE_CONTAINER_TAG", s => s.Enable());
    return wine;
}
void Tooltip(RichTextBuilder builder, GameItem wine)
{
    var prefix = typeof(ExtendedWineAgeTooltipPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
    if ((bool)prefix.Invoke(null, [builder, wine, "mech_wine_age"])) AgableHelper.AddAgeTooltipLine(builder, wine);
}
bool HasAged(GameItem wine)
{
    object[] args = [wine, AgableHelper.HasAged(wine)];
    typeof(ExtendedWineAgePresencePatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
    return (bool)args[1];
}
var fresh = Wine();
var validations = fresh.Validations;
Check(ExtendedWineAge.TryAdvance(fresh, 241, out _), "First turbo batch rejected");
Check(fresh.IsTag("AGE_INT") && AgableHelper.HasAged(fresh) && AgableHelper.GetAge(fresh) == 241,
    "First turbo did not enable a visible native age");
Check(fresh.unitValue == 823 && fresh.Validations == validations + 1, "First batch price/validation mismatch");
var firstTooltip = new RichTextBuilder();
Tooltip(firstTooltip, fresh);
Check(firstTooltip.Text.Contains("241"), "First turbo tooltip requires an overnight update");
Check(ExtendedWineAge.TryAdvance(fresh, 241, out _) && AgableHelper.GetAge(fresh) == 482, "Second turbo lost first age");

var maxed = Wine();
maxed.ModifyTag("AGE_INT", s => { s.Enable(); s.valueInt = int.MaxValue; });
Check(ExtendedWineAge.TryAdvance(maxed, 241, out var cross), "Boundary turbo rejected");
Check(maxed.IsTag(ExtendedWineAge.ExtendedTag) && ExtendedWineAge.TryRead(maxed, out var full) && full == (long)int.MaxValue + 241,
    "Extended tag remains disabled or truncated");
var extendedTooltip = new RichTextBuilder();
Tooltip(extendedTooltip, maxed);
Check(extendedTooltip.Text.Contains(cross.Age.ToString()), "Immediate tooltip shows only Int32 projection");
Check(ExtendedWineAge.TryAdvance(maxed, 241, out var next) && next.Age == cross.Age + 241 && maxed.unitValue == 1546,
    "Repeated turbo discarded extended days");

var legacy = Wine();
legacy.ModifyTag("AGE_INT", s => { s.Enable(); s.valueInt = int.MaxValue; });
legacy.ModifyTag(ExtendedWineAge.ExtendedTag, s => s.valueLong = (long)int.MaxValue + 10_000);
Check(!legacy.IsTag(ExtendedWineAge.ExtendedTag), "Legacy fixture did not reproduce disabled extension");
Check(ExtendedWineAge.TryRead(legacy, out var recovered) && recovered == (long)int.MaxValue + 10_000,
    "Old disabled extension was not recovered");
var legacyTooltip = new RichTextBuilder();
Tooltip(legacyTooltip, legacy);
Check(legacyTooltip.Text.Contains(recovered.ToString()), "Legacy tooltip needs another night");
Check(ExtendedWineAge.TryAdvance(legacy, 5, out var migrated) && migrated.Age == recovered + 5
    && legacy.IsTag(ExtendedWineAge.ExtendedTag), "Legacy next batch did not enable/preserve saved age");
AgableHelper.ResetAge(legacy);
ExtendedWineAge.ResetExtension(legacy);
Check(!legacy.IsTag(ExtendedWineAge.ExtendedTag) && ExtendedWineAge.TryRead(legacy, out var reset) && reset == 0,
    "Reset resurrected extended age");
Check(ExtendedWineAge.TryAdvance(legacy, 1, out var restart) && restart.Age == 1
    && !legacy.IsTag(ExtendedWineAge.ExtendedTag), "Reset wine resumed old extended age");

var youngLegacy = Wine();
youngLegacy.ModifyTag("AGE_INT", s => s.valueInt = 241);
Check(HasAged(youngLegacy), "Legacy disabled age is hidden until another cycle");
Check(ExtendedWineAge.TryAdvance(youngLegacy, 2, out var young) && young.Age == 243
    && youngLegacy.IsTag("AGE_INT"), "Disabled native age was discarded");
AgableHelper.ResetAge(youngLegacy);
Check(!HasAged(youngLegacy), "Reset wine remains marked as aged");
var broken = Wine();
broken.ModifyTag("AGE_INT", s => { s.Enable(); s.valueInt = int.MaxValue; });
broken.FailTag = ExtendedWineAge.ExtendedTag;
try { ExtendedWineAge.TryAdvance(broken, 241, out _); throw new Exception("Fault not injected"); }
catch (InvalidOperationException) { }
Check(broken.unitValue == 100 && broken.state.dict["AGE_INT"].valueInt == int.MaxValue
    && !broken.state.dict.ContainsKey(ExtendedWineAge.ExtendedTag), "Write failure left price/age partially committed");
var overflow = Wine();
overflow.unitValue = long.MaxValue;
Check(!ExtendedWineAge.TryAdvance(overflow, 1, out _) && !overflow.state.dict.ContainsKey("AGE_INT")
    && overflow.unitValue == long.MaxValue, "Currency overflow mutated age");
Console.WriteLine($"PASS: {checks} native-tag contract regression checks (not in-game verification).");
