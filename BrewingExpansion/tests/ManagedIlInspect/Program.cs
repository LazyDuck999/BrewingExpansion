using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var md = pe.GetMetadataReader();
var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(c => unchecked((ushort)c.Value));
string Token(int token)
{
    try
    {
        var h = MetadataTokens.Handle(token);
        switch (h.Kind)
        {
            case HandleKind.UserString: return "\"" + md.GetUserString((UserStringHandle)h) + "\"";
            case HandleKind.TypeDefinition:
                var td = md.GetTypeDefinition((TypeDefinitionHandle)h);
                return md.GetString(td.Namespace) + "." + md.GetString(td.Name);
            case HandleKind.TypeReference:
                var tr = md.GetTypeReference((TypeReferenceHandle)h);
                return md.GetString(tr.Namespace) + "." + md.GetString(tr.Name);
            case HandleKind.MethodDefinition:
                var m = md.GetMethodDefinition((MethodDefinitionHandle)h);
                return Token(MetadataTokens.GetToken(m.GetDeclaringType())) + "::" + md.GetString(m.Name);
            case HandleKind.FieldDefinition:
                var f = md.GetFieldDefinition((FieldDefinitionHandle)h);
                return Token(MetadataTokens.GetToken(f.GetDeclaringType())) + "::" + md.GetString(f.Name);
            case HandleKind.MemberReference:
                var r = md.GetMemberReference((MemberReferenceHandle)h);
                return Token(MetadataTokens.GetToken(r.Parent)) + "::" + md.GetString(r.Name);
            case HandleKind.MethodSpecification:
                return Token(MetadataTokens.GetToken(md.GetMethodSpecification((MethodSpecificationHandle)h).Method)) + "<...>";
        }
    }
    catch { }
    return "token:" + token.ToString("X8");
}
foreach (var th in md.TypeDefinitions)
{
    var t = md.GetTypeDefinition(th);
    if (args.Length > 1 && Token(MetadataTokens.GetToken(th)) != args[1]) continue;
    Console.WriteLine("\nTYPE " + Token(MetadataTokens.GetToken(th)));
    if (args.Length > 1)
        Console.WriteLine("  LAYOUT " + t.Attributes + " BASE " + Token(MetadataTokens.GetToken(t.BaseType)));
    foreach (var fh in t.GetFields())
    {
        var field = md.GetFieldDefinition(fh);
        var constant = field.GetDefaultValue();
        Console.WriteLine("  FIELD " + field.Attributes + " " + md.GetString(field.Name)
            + (constant.IsNil ? "" : " = " + Convert.ToHexString(md.GetBlobBytes(md.GetConstant(constant).Value))));
        if (args.Length > 1) Console.WriteLine("    SIGNATURE " + Convert.ToHexString(md.GetBlobBytes(field.Signature)));
    }
    foreach (var mh in t.GetMethods())
    {
        var method = md.GetMethodDefinition(mh);
        Console.WriteLine("\n  METHOD " + method.Attributes + " " + Token(MetadataTokens.GetToken(mh)));
        if (method.RelativeVirtualAddress == 0) continue;
        var body = pe.GetMethodBody(method.RelativeVirtualAddress);
        var il = body.GetILBytes();
        var p = 0;
        int I32() { var n = BitConverter.ToInt32(il, p); p += 4; return n; }
        while (p < il.Length)
        {
            var start = p;
            ushort code = il[p++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[p++]);
            var op = codes[code];
            object operand = "";
            switch (op.OperandType)
            {
                case OperandType.InlineBrTarget: var b = I32(); operand = "IL_" + (p+b).ToString("X4"); break;
                case OperandType.ShortInlineBrTarget: var s = (sbyte)il[p++]; operand = "IL_"+(p+s).ToString("X4"); break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                case OperandType.InlineString:
                case OperandType.InlineSig: operand = Token(I32()); break;
                case OperandType.InlineI: operand = I32(); break;
                case OperandType.ShortInlineI: operand = (sbyte)il[p++]; break;
                case OperandType.InlineI8: operand = BitConverter.ToInt64(il,p); p+=8; break;
                case OperandType.InlineR: operand = BitConverter.ToDouble(il,p); p+=8; break;
                case OperandType.ShortInlineR: operand = BitConverter.ToSingle(il,p); p+=4; break;
                case OperandType.InlineVar: operand = BitConverter.ToUInt16(il,p); p+=2; break;
                case OperandType.ShortInlineVar: operand = il[p++]; break;
                case OperandType.InlineSwitch:
                    var count = I32(); var targets = new int[count];
                    for (var i=0;i<count;i++) targets[i]=I32();
                    operand=string.Join(", ",targets.Select(x=>"IL_"+(p+x).ToString("X4"))); break;
            }
            Console.WriteLine($"    IL_{start:X4}: {op.Name,-12} {operand}");
        }
        foreach (var e in body.ExceptionRegions)
            Console.WriteLine($"    EXCEPTION {e.Kind}: try IL_{e.TryOffset:X4} len {e.TryLength}, handler IL_{e.HandlerOffset:X4} len {e.HandlerLength}");
    }
}
