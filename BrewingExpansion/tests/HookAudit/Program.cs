using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

var records = new List<object>();
foreach (var file in args.Where(a => a != "--closures"))
{
    using var stream = File.OpenRead(file);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    var provider = new AttributeTypes();
    foreach (var th in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(th);
        var patchClass = md.GetString(type.Name);
        if (args.Contains("--closures"))
        {
            var parent = type.GetDeclaringType();
            if (parent.IsNil) continue;
            var parentName = md.GetString(md.GetTypeDefinition(parent).Name);
            if (parentName != "MachineFurnace" && parentName != "MachineBottlePrinter") continue;
            records.Add(new { parentName, patchClass,
                methods = type.GetMethods().Select(h => md.GetString(md.GetMethodDefinition(h).Name)).ToArray(),
                properties = type.GetProperties().Select(h => md.GetString(md.GetPropertyDefinition(h).Name)).ToArray(),
                fields = type.GetFields().Select(h => md.GetString(md.GetFieldDefinition(h).Name)).ToArray() });
            continue;
        }
        foreach (var ah in type.GetCustomAttributes())
        {
            var a = md.GetCustomAttribute(ah);
            if (a.Constructor.Kind != HandleKind.MemberReference) continue;
            var ctor = md.GetMemberReference((MemberReferenceHandle)a.Constructor);
            if (ctor.Parent.Kind != HandleKind.TypeReference) continue;
            var attr = provider.GetTypeFromReference(md, (TypeReferenceHandle)ctor.Parent, 0);
            if (attr != "HarmonyLib.HarmonyPatch") continue;
            var value = a.DecodeValue(provider);
            object? ConvertArg(CustomAttributeTypedArgument<string> arg) => arg.Value is ImmutableArray<CustomAttributeTypedArgument<string>> array
                ? array.Select(ConvertArg).ToArray() : arg.Value;
            records.Add(new { assembly = Path.GetFileName(file), patchClass,
                arguments = value.FixedArguments.Select(ConvertArg).ToArray() });
        }
    }
}
Console.WriteLine(JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));

sealed class AttributeTypes : ICustomAttributeTypeProvider<string>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSystemType() => "System.Type";
    public bool IsSystemType(string type) => type == "System.Type";
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var t = reader.GetTypeDefinition(handle);
        return reader.GetString(t.Namespace) + "." + reader.GetString(t.Name);
    }
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var t = reader.GetTypeReference(handle);
        return reader.GetString(t.Namespace) + "." + reader.GetString(t.Name);
    }
    public string GetTypeFromSerializedName(string name) => name.Split(',')[0];
    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;
}
