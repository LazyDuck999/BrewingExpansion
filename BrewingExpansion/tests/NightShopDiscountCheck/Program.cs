using System.Reflection;
using Il2Cpp;
using XIAOWOBrewingBarrel;

var count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    count++;
}
var stock = new GameItem(1);
var otherStock = new GameItem(2);
var owned = new GameItem(3, true);
var friend = ItemFeatureList.DiscountBuy15();
var other = new ItemFeature("other_price_modifier");
var previous = NightShopDoctorDiscount.Begin(stock, false, false);
Check(NightShopDoctorDiscount.MayAdd(stock, friend), "Doctor friendship discount blocked");
NightShopDoctorDiscount.End(previous);
previous = NightShopDoctorDiscount.Begin(stock, true, false);
Check(!NightShopDoctorDiscount.MayAdd(stock, friend), "Brew shop inherits Doctor discount");
Check(NightShopDoctorDiscount.MayAdd(stock, other), "Other price modifiers blocked");
Check(NightShopDoctorDiscount.MayAdd(otherStock, friend), "Unrelated stock affected by current item");
Check(NightShopDoctorDiscount.MayAdd(null, friend) && NightShopDoctorDiscount.MayAdd(stock, null), "Null arguments blocked");
var nested = NightShopDoctorDiscount.Begin(stock, false, false);
Check(NightShopDoctorDiscount.MayAdd(stock, friend), "Nested Doctor visit inherits brew suppression");
NightShopDoctorDiscount.End(nested);
Check(!NightShopDoctorDiscount.MayAdd(stock, friend), "Outer brew scope not restored");
nested = NightShopDoctorDiscount.Begin(otherStock, true, false);
Check(!NightShopDoctorDiscount.MayAdd(otherStock, friend) && NightShopDoctorDiscount.MayAdd(stock, friend),
    "Nested stock did not replace current target");
NightShopDoctorDiscount.End(nested);
Check(!NightShopDoctorDiscount.MayAdd(stock, friend) && NightShopDoctorDiscount.MayAdd(otherStock, friend),
    "Nested stock target leaked");
var anotherThreadMayAdd = false;
var thread = new Thread(() => anotherThreadMayAdd = NightShopDoctorDiscount.MayAdd(stock, friend));
thread.Start(); thread.Join();
Check(anotherThreadMayAdd, "Suppression leaked across threads");
// Invoke the real Harmony finalizer, including an exception path.
try { throw new InvalidOperationException("Native callback failed"); }
catch (InvalidOperationException) { }
finally
{
    typeof(NightShopDoctorDiscountScopePatch).GetMethod("Finalizer", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, [previous]);
}
Check(NightShopDoctorDiscount.MayAdd(stock, friend), "Exception cleanup left discount suppression enabled");
previous = NightShopDoctorDiscount.Begin(owned, true, false);
Check(NightShopDoctorDiscount.MayAdd(owned, friend), "Owned items changed");
NightShopDoctorDiscount.End(previous);
previous = NightShopDoctorDiscount.Begin(stock, true, true);
Check(NightShopDoctorDiscount.MayAdd(stock, friend), "Ignored native callback starts suppression");
NightShopDoctorDiscount.End(previous);
Console.WriteLine($"PASS: {count} shop discount scope checks; in-game pricing verification pending.");
