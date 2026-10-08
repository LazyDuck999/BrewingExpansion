using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using XIAOWOBrewingBarrel;
internal static class MetadataContract {
    internal static void Check(string path,Action<bool,string> check) {
        using var file=File.OpenRead(path); using var pe=new PEReader(file); var md=pe.GetMetadataReader();
        var specs=((string Type,string Method,Type Return,Type[] Args,string Proxy,string Native)[])
            typeof(LiunianOptimizerCompatibility).GetField("Specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        foreach(var spec in specs) {
            var type=md.TypeDefinitions.Select(md.GetTypeDefinition).Single(t => md.GetString(t.Namespace)=="ProbablyOptimized" && md.GetString(t.Name)==spec.Type);
            var method=type.GetMethods().Select(md.GetMethodDefinition).Single(m => md.GetString(m.Name)==spec.Method);
            var sig=method.DecodeSignature(new Names(),0);
            check(sig.ReturnType==spec.Return.FullName && sig.ParameterTypes.SequenceEqual(spec.Args.Select(t=>t.FullName)),"Reviewed callback signature differs: "+spec.Type+"."+spec.Method);
        }
    }
    private class Names : ISignatureTypeProvider<string,int> {
        public string GetPrimitiveType(PrimitiveTypeCode c)=>"System."+c;
        public string GetTypeFromDefinition(MetadataReader r,TypeDefinitionHandle h,byte k){ var t=r.GetTypeDefinition(h);return r.GetString(t.Namespace)+"."+r.GetString(t.Name); }
        public string GetTypeFromReference(MetadataReader r,TypeReferenceHandle h,byte k){ var t=r.GetTypeReference(h);return r.GetString(t.Namespace)+"."+r.GetString(t.Name); }
        public string GetTypeFromSpecification(MetadataReader r,int c,TypeSpecificationHandle h,byte k)=>r.GetTypeSpecification(h).DecodeSignature(this,c);
        public string GetArrayType(string e,ArrayShape s)=>e+"[]";
        public string GetByReferenceType(string e)=>e+"&";
        public string GetFunctionPointerType(MethodSignature<string> s)=>"fn";
        public string GetGenericInstantiation(string t,ImmutableArray<string> a)=>t+"<"+string.Join(",",a)+">";
        public string GetGenericMethodParameter(int c,int i)=>"!!"+i;
        public string GetGenericTypeParameter(int c,int i)=>"!"+i;
        public string GetModifiedType(string m,string e,bool b)=>e;
        public string GetPinnedType(string e)=>e;
        public string GetPointerType(string e)=>e+"*";
        public string GetSZArrayType(string e)=>e+"[]";
    }
}
