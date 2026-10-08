using BrewingExpansion;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
var pars = new[] { typeof(string), typeof(int) };
System.Reflection.MethodBase Find(Type t, Type result = null) => ClosureHookTarget.FindUnique(t, result ?? typeof(void), pars, "machine", typeof(Item));
Check(Find(typeof(Unique))?.Name == "Print", "Property-backed native wrapper closure");
Check(Find(typeof(FieldBacked))?.Name == "Print", "Field-backed closure");
Check(Find(typeof(Ambiguous)) == null, "Same closure ambiguity must not select first");
Check(Find(typeof(TwoClosures)) == null, "Cross-closure ambiguity must not select first");
Check(Find(typeof(WrongMember)) == null, "Wrong captured type must not match");
Check(Find(typeof(Unique), typeof(bool)) == null, "Wrong return contract must not match");
Check(Find(typeof(Generic)) == null, "Generic callback must not match");
Check(Find(typeof(Missing)) == null, "Missing target is safe to skip");
Console.WriteLine("Closure hook checks passed: " + checks);

class Item { }
class Unique
{
    class DisplayClass { public Item machine { get; set; } public void Print(string s, int n) { } public void Wrong(string s) { } }
    class DecoyDisplayClass { public void Print(string s, int n) { } }
}
class FieldBacked { class DisplayClass { public Item machine = new(); public void Print(string s, int n) { } } }
class Ambiguous { class DisplayClass { public Item machine { get; set; } public void A(string s, int n) { } public void B(string s, int n) { } } }
class TwoClosures
{
    class DisplayClass1 { public Item machine { get; set; } public void A(string s, int n) { } }
    class DisplayClass2 { public Item machine { get; set; } public void B(string s, int n) { } }
}
class WrongMember { class DisplayClass { public string machine { get; set; } public void Print(string s, int n) { } } }
class Generic { class DisplayClass { public Item machine { get; set; } public void Print<T>(string s, int n) { } } }
class Missing { }
