using System;
using System.Reflection;

namespace BrewingExpansion;

// Generated closure numbering can change between game builds. Select by the
// native contract, and refuse ambiguous matches rather than patching the first.
internal static class ClosureHookTarget
{
    internal static MethodBase FindUnique(Type owner, Type returnType, Type[] parameters,
        string itemMember, Type itemType)
    {
        MethodBase found = null;
        foreach (var nested in owner.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!nested.Name.Contains("DisplayClass")) continue;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var property = nested.GetProperty(itemMember, flags);
            var field = nested.GetField(itemMember, flags);
            if (property?.PropertyType != itemType && field?.FieldType != itemType) continue;
            foreach (var method in nested.GetMethods(flags))
            {
                if (method.IsGenericMethod || method.ReturnType != returnType) continue;
                var actual = method.GetParameters();
                if (actual.Length != parameters.Length) continue;
                var matches = true;
                for (var i = 0; i < actual.Length; i++)
                    if (actual[i].ParameterType != parameters[i]) { matches = false; break; }
                if (!matches) continue;
                if (found != null) return null;
                found = method;
            }
        }
        return found;
    }
}
