using System.Numerics;
using XIAOWOBrewingBarrel;

var checks = 0;
void Require(bool result, string message)
{
    if (!result) throw new Exception(message);
    checks++;
}
var random = new Random(70123);
for (var i = 0; i < 1000; i++)
{
    var age = random.Next(0, 1_000_000);
    var value = random.NextInt64(0, 10_000_000);
    var days = random.Next(1, 1001);
    var rate = random.Next(-10, 101);
    var expectedAge = age;
    var expectedValue = value;
    for (var day = 0; day < days; day++) { expectedAge++; expectedValue += rate; }
    Require(AgingBatchMath.TryPlan(age, value, days, rate, out var plan), "Normal aging rejected");
    Require(plan.Age == expectedAge && plan.Value == expectedValue && !plan.AgeLimited, "Repeated aging mismatch");
}
foreach (var days in new[] { 1, 2, 201, 241, 6_400_000, 17_179_870 })
{
    Require(AgingBatchMath.TryPlan(14_400_019, 999_999, days, int.MaxValue, out var plan), "Large batch rejected");
    var exact = new BigInteger(999_999) + new BigInteger(int.MaxValue) * days;
    Require(plan.Age == 14_400_019 + days && new BigInteger(plan.Value) == exact, "Large batch mismatch");
}
Require(AgingBatchMath.TryPlan(int.MaxValue - 3, 100, 100, 7, out var limited)
    && limited.Age == (long)int.MaxValue + 97 && limited.NativeAge == int.MaxValue
    && limited.Value == 800 && limited.Days == 100 && !limited.AgeLimited,
    "Int32 crossing lost age or value");
Require(AgingBatchMath.TryPlan(int.MaxValue, 100, 100, 7, out var full)
    && full.Age == (long)int.MaxValue + 100 && full.Days == 100 && full.Value == 800,
    "Int32 limit prevented continued aging");
for (var i = 0; i < 2000; i++)
{
    var age = random.NextInt64(int.MaxValue - 1000L, long.MaxValue - 20_000_000L);
    var value = random.NextInt64(0, 10_000_000_000_000_000L);
    var days = random.Next(1, 17_179_871);
    var rate = random.Next(-10, 101);
    Require(AgingBatchMath.TryPlan(age, value, days, rate, out var plan), "64-bit batch rejected");
    Require(new BigInteger(plan.Age) == new BigInteger(age) + days
        && new BigInteger(plan.Value) == new BigInteger(value) + new BigInteger(rate) * days
        && plan.NativeAge == (int)Math.Min(plan.Age, int.MaxValue) && !plan.AgeLimited,
        "64-bit batch differs from exact arithmetic");
}
Require(AgingBatchMath.TryPlan(3_000_000_000L, 100, 1, 7, out var nativeDay)
    && nativeDay.Age == 3_000_000_001L && nativeDay.Value == 107 && nativeDay.NativeAge == int.MaxValue,
    "Single native day beyond Int32 failed");
Require(AgingBatchMath.TryPlan(long.MaxValue - 3, 100, 100, 7, out var longLimit)
    && longLimit.Age == long.MaxValue && longLimit.Value == 121 && longLimit.Days == 3 && longLimit.AgeLimited,
    "Int64 limit did not keep age/value consistent");
Require(AgingBatchMath.TryPlan(long.MaxValue, 100, 100, 7, out var maxed)
    && maxed.Days == 0 && maxed.Value == 100 && maxed.NativeAge == int.MaxValue,
    "Int64 limit kept increasing value");
Require(!AgingBatchMath.TryPlan(0, long.MaxValue, 2, 3, out _), "Value overflow was not rejected");
Require(!AgingBatchMath.TryPlan(0, long.MinValue, 2, -3, out _), "Value underflow was not rejected");
Require(!AgingBatchMath.TryPlan(-1, 100, 1, 3, out _), "Corrupt age accepted");
Require(!AgingBatchMath.TryPlan(0, 100, 0, 3, out _), "Zero days accepted");
Console.WriteLine($"PASS: {checks} checks; repeated aging, large batches and numeric limits.");
