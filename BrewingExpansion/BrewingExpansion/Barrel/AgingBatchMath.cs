using System;

namespace XIAOWOBrewingBarrel;

// Kept independent of Unity so arithmetic can be checked against repeated aging.
internal static class AgingBatchMath
{
    internal readonly record struct Plan(long Age, long Value, int Days, bool AgeLimited)
    {
        internal int NativeAge => (int)Math.Min(Age, int.MaxValue);
    }

    internal static bool TryPlan(long age, long value, int requestedDays, int valuePerDay, out Plan plan)
    {
        plan = default;
        if (age < 0 || requestedDays < 1) return false;
        var days = (int)Math.Min((long)requestedDays, long.MaxValue - age);
        try
        {
            var nextValue = checked(value + (long)valuePerDay * days);
            plan = new Plan(age + days, nextValue, days, days < requestedDays);
            return true;
        }
        catch (OverflowException) { return false; }
    }
}
