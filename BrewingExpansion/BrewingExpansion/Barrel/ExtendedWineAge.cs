using System;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace XIAOWOBrewingBarrel;

// Per-bottle state is serialized/cloned by the game's own TagState format.
// AGE_INT remains readable by native APIs; this tag holds the full age only
// after crossing its range. No extra save file or per-frame registry is needed.
internal static class ExtendedWineAge
{
    internal const string AgeTag = "AGE_INT";
    internal const string ExtendedTag = "BE_WINE_AGE64";
    private const string ValuePerDayTag = "AGE_VALUE_MOD_PER_DAY_INT";

    internal static bool IsWine(GameItem wine) => wine != null && wine.IsTag("WINE_CONTAINER_TAG");

    private static TagState StoredTag(GameItem wine, string name)
    {
        var tags = wine?.state?.dict;
        return tags != null && tags.TryGetValue(name, out var tag) ? tag : null;
    }

    internal static bool TryRead(GameItem wine, out long age)
    {
        age = 0;
        if (!IsWine(wine)) return false;
        var nativeAge = AgableHelper.GetAge(wine);
        var storedAge = StoredTag(wine, AgeTag);
        if (storedAge != null) nativeAge = Math.Max(nativeAge, storedAge.valueInt);
        var saved = StoredTag(wine, ExtendedTag);
        // Earlier batches wrote numeric fields without enabling their tags.
        // Recover that record only while AGE_INT still carries its max-value
        // projection; a native ResetAge (zero) must not resurrect old age.
        var legacyExtension = nativeAge == int.MaxValue && saved?.valueLong > int.MaxValue;
        if (saved == null || (!saved.valueEnabled && !legacyExtension))
        {
            age = nativeAge;
            return age >= 0;
        }
        if (saved.valueLong < 0) return false;
        // Retain a native increment made by another mod below the int limit.
        // A valid extended record also survives a corrupted native projection.
        age = Math.Max(saved.valueLong, nativeAge);
        return true;
    }

    internal static bool TryAdvance(GameItem wine, int days, out AgingBatchMath.Plan plan)
    {
        plan = default;
        if (!TryRead(wine, out var age)) return false;
        var oldValue = wine.unitValue;
        var rate = wine.IsTag(ValuePerDayTag) ? wine.GetTagReadonly(ValuePerDayTag)?.valueInt ?? 3 : 3;
        if (!AgingBatchMath.TryPlan(age, oldValue, days, rate, out plan)) return false;
        if (plan.Days == 0) return true;

        var oldAge = StoredTag(wine, AgeTag)?.Clone();
        var oldExtendedAge = StoredTag(wine, ExtendedTag)?.Clone();
        var needsExtension = plan.Age > int.MaxValue || oldExtendedAge?.valueEnabled == true;
        var next = plan;
        try
        {
            // Write the compatibility projection before the final validation.
            // ModifyTag creates a DISABLED tag. Native OnAge explicitly calls
            // Enable(); without it HasAged/tooltip omit the newly written age.
            wine.ModifyTag(AgeTag, (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueInt = next.NativeAge; }), true);
            if (needsExtension)
                wine.ModifyTag(ExtendedTag, (Il2CppSystem.Action<TagState>)(s => { s.Enable(); s.valueLong = next.Age; }), true);
            wine.unitValue = next.Value;
        }
        catch
        {
            RestoreTag(wine, AgeTag, oldAge);
            RestoreTag(wine, ExtendedTag, oldExtendedAge);
            wine.unitValue = oldValue;
            throw;
        }
        // Numeric settlement is committed even if a window cannot refresh yet.
        try { wine.ValidateShapeState(); }
        catch (Exception ex) { Core.Log?.Warning("Wine aged; display validation deferred: " + ex.Message); }
        return true;
    }

    private static void RestoreTag(GameItem wine, string name, TagState snapshot)
    {
        if (snapshot == null) wine.state.dict.Remove(name);
        else wine.state.dict[name] = snapshot;
    }

    internal static void ResetExtension(GameItem wine)
    {
        if (StoredTag(wine, ExtendedTag) == null) return;
        wine.ModifyTag(ExtendedTag, (Il2CppSystem.Action<TagState>)(s => { s.valueLong = 0; s.Disable(); }));
    }
}

[HarmonyPatch(typeof(AgableHelper), nameof(AgableHelper.HasAged))]
internal static class ExtendedWineAgePresencePatch
{
    private static void Postfix(GameItem item, ref bool __result)
    {
        // Old unenabled AGE_INT records must also be visible before the next
        // machine cycle. Read existing state only; do not mutate on hover.
        if (!__result && ExtendedWineAge.TryRead(item, out var age) && age > 0) __result = true;
    }
}

[HarmonyPatch(typeof(AgableHelper), nameof(AgableHelper.OnAge))]
internal static class ExtendedWineAgeDailyPatch
{
    private static bool _reportedFailure;
    // Ordinary items/young wines keep the native method. Guard only wines whose
    // next native +1 could overflow, or which already carry our long record.
    private static bool Prefix(GameItem item)
    {
        if (!ExtendedWineAge.IsWine(item) || item.IsTag("WINE_COUNTERFEIT_TAG")) return true;
        if (!item.IsTag(ExtendedWineAge.ExtendedTag) && AgableHelper.GetAge(item) < int.MaxValue) return true;
        try
        {
            if (!ExtendedWineAge.TryAdvance(item, 1, out _) && !_reportedFailure)
            {
                _reportedFailure = true;
                Core.Log?.Warning("Skipped wine aging due to an invalid age or currency overflow: " + item.uniqueId);
            }
        }
        catch (Exception ex)
        {
            // A native rack may invoke this millions of times in one cycle.
            if (!_reportedFailure)
            {
                _reportedFailure = true;
                Core.Log?.Error("Extended daily wine aging failed: " + ex);
            }
        }
        // Do not replay native aging after a failed/overflow-limited update.
        return false;
    }
}

[HarmonyPatch(typeof(AgableHelper), nameof(AgableHelper.ResetAge))]
internal static class ExtendedWineAgeResetPatch
{
    private static void Postfix(GameItem item)
    {
        if (item == null) return;
        try { ExtendedWineAge.ResetExtension(item); }
        catch (Exception ex) { Core.Log?.Warning("Could not clear extended wine age after reset: " + ex.Message); }
    }
}

[HarmonyPatch(typeof(AgableHelper), nameof(AgableHelper.AddAgeTooltipLine))]
internal static class ExtendedWineAgeTooltipPatch
{
    private static bool Prefix(RichTextBuilder builder, GameItem item, string locKey)
    {
        if (builder == null || !ExtendedWineAge.TryRead(item, out var age) || age <= int.MaxValue) return true;
        try
        {
            // Use the same localized mechanic and text color as the native line.
            // The generated Int64 is a blittable struct with one readonly long
            // field and no value constructor. Reinterpret its eight bytes for
            // the wrapper's native boxing API; do not convert through a float.
            var number = System.Runtime.CompilerServices.Unsafe.As<long, Il2CppSystem.Int64>(ref age);
            var args = new Il2CppReferenceArray<Il2CppSystem.Object>(1);
            args[0] = number.BoxIl2CppObject();
            builder.AddLine(LocHelper.GetLocalizedMechanic(locKey, args), true, RenderHandler.ColorPalette.LightCyan);
            return false;
        }
        catch (Exception ex)
        {
            Core.Log?.Warning("Extended wine-age localization unavailable: " + ex.Message);
            builder.AddLine(BarrelItem.Text("陈化期：" + age.ToString(CultureInfo.InvariantCulture) + "天",
                "Age: " + age.ToString(CultureInfo.InvariantCulture) + " days"), true, RenderHandler.ColorPalette.LightCyan);
            return false;
        }
    }
}
