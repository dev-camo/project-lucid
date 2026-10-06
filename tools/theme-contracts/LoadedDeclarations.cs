using Mono.Cecil;
using System.Globalization;
using System.Text.Json;

namespace ProjectLucid.ThemeContractMetadata;

// Disk metadata only. This never loads a game/player assembly into the CLR.
// Resolution is restricted to the exact sealed loaded modules plus the installed
// facade recorded separately; every forwarder edge retains its declared scope.
internal sealed class LoadedDeclarations : IAssemblyResolver
{
    private readonly Request request;
    private readonly Dictionary<string, Seal> seals;
    private readonly Dictionary<string, AssemblyDefinition> opened = new(StringComparer.Ordinal);
    private readonly List<object> edges = new();
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal) {
        "Game.Runtime:HardlightProject.ZoneThemeOverride",
        "HLUnityCore.Runtime:Hardlight.InspectorConditionalAttribute",
        "HLUnityCore.Runtime:Hardlight.InspectorConditionalAttribute+ComparisonType",
        "HLUnityCore.Runtime:Hardlight.InspectorConditionalField",
        "HLUnityCore.Runtime:Hardlight.ShowIfAttribute",
        "HLUnityCore.Runtime:Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute",
        "HLUnityCore.Runtime:Unity.IL2CPP.CompilerServices.Option",
        "System:System.ComponentModel.EditorBrowsableAttribute",
        "System:System.ComponentModel.EditorBrowsableState",
        "Unity.Addressables:UnityEngine.AddressableAssets.AssetReference",
        "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite",
        "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceT`1",
        "UnityEngine.CoreModule:UnityEngine.Color",
        "UnityEngine.CoreModule:UnityEngine.CreateAssetMenuAttribute",
        "UnityEngine.CoreModule:UnityEngine.ExcludeFromPresetAttribute",
        "UnityEngine.CoreModule:UnityEngine.ExtensionOfNativeClassAttribute",
        "UnityEngine.CoreModule:UnityEngine.Object",
        "UnityEngine.CoreModule:UnityEngine.PropertyAttribute",
        "UnityEngine.CoreModule:UnityEngine.ScriptableObject",
        "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute",
        "UnityEngine.CoreModule:UnityEngine.SerializeField",
        "UnityEngine.CoreModule:UnityEngine.Sprite",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.NativeHeaderAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.NativeTypeAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.VisibleToOtherModulesAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.NativeClassAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Scripting.RequiredByNativeCodeAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Scripting.UsedByNativeCodeAttribute",
        "mscorlib:System.Attribute",
        "mscorlib:System.AttributeTargets",
        "mscorlib:System.AttributeUsageAttribute",
        "mscorlib:System.Diagnostics.DebuggerBrowsableAttribute",
        "mscorlib:System.Diagnostics.DebuggerBrowsableState",
        "mscorlib:System.Enum",
        "mscorlib:System.FlagsAttribute",
        "mscorlib:System.Object",
        "mscorlib:System.Reflection.DefaultMemberAttribute",
        "mscorlib:System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        "mscorlib:System.Runtime.CompilerServices.IsReadOnlyAttribute",
        "mscorlib:System.ValueType",
    };
    public LoadedDeclarations(Request request) {
        this.request=request;
        if(request.loaded_modules.Count!=10||request.loaded_modules.Select(s=>s.assembly_name).Distinct(StringComparer.Ordinal).Count()!=10)
            throw new InvalidDataException("Exact selected loaded modules, handle dependency and separate facade required");
        seals=request.loaded_modules.ToDictionary(s=>s.assembly_name,StringComparer.Ordinal);
        if(!seals.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"Game.Runtime","HLUnityCore.Runtime","System","Unity.Addressables","UnityEngine.CoreModule","UnityEngine.SharedInternalsModule","mscorlib","netstandard","Unity.ResourceManager","System.Core"}))
            throw new InvalidDataException("Loaded declaration modules differ from exact profile");
        foreach(var s in seals.Values)s.Check();
        var core=request.engine.Single(s=>s.name=="editor_core");
        string ending=Path.Combine("Managed","UnityEngine","UnityEditor.CoreModule.dll");
        string contents=core.path[..^(ending.Length+1)];
        string runtime=Path.Combine(contents,"MonoBleedingEdge","lib","mono","unityjit-macos");
        var paths=new Dictionary<string,string>(StringComparer.Ordinal) {
          ["mscorlib"]=Path.Combine(runtime,"mscorlib.dll"),["System"]=Path.Combine(runtime,"System.dll"),["System.Core"]=Path.Combine(runtime,"System.Core.dll"),
          ["netstandard"]=Path.Combine(runtime,"Facades","netstandard.dll"),
          ["UnityEngine.CoreModule"]=Path.Combine(contents,"Managed","UnityEngine","UnityEngine.CoreModule.dll"),
          ["UnityEngine.SharedInternalsModule"]=Path.Combine(contents,"Managed","UnityEngine","UnityEngine.SharedInternalsModule.dll") };
        foreach(string n in new[]{"Game.Runtime","HLUnityCore.Runtime","Unity.Addressables","Unity.ResourceManager"})paths[n]=Path.Combine(request.project_root,"Library","ScriptAssemblies",n+".dll");
        foreach(var p in paths)if(seals[p.Key].path!=p.Value)throw new InvalidDataException("Loaded declaration path is not the exact installed/project module");
    }
    private AssemblyDefinition Read(string name) {
        if(opened.TryGetValue(name,out var old))return old;
        if(!seals.TryGetValue(name,out var s))throw new InvalidDataException("Declaration dependency absent from exact sealed candidates: "+name);
        s.Check();var a=AssemblyDefinition.ReadAssembly(new MemoryStream(Program.Read(s.path)),new ReaderParameters{ReadingMode=ReadingMode.Deferred,InMemory=true,ReadSymbols=false,AssemblyResolver=this});
        if(a.Name.FullName!=s.assembly_identity||a.MainModule.Mvid.ToString()!=s.mvid){a.Dispose();throw new InvalidDataException("Loaded declaration full assembly/MVID differs from disk");}
        opened.Add(name,a);return a;
    }
    public AssemblyDefinition Resolve(AssemblyNameReference name)=>Resolve(name,new ReaderParameters());
    public AssemblyDefinition Resolve(AssemblyNameReference name,ReaderParameters parameters) {
        var a=Read(name.Name);if(a.Name.FullName!=name.FullName)throw new InvalidDataException("Declared reference full assembly identity differs");return a;
    }
    private static string Name(TypeReference t)=>t.FullName.Replace('/','+');
    private TypeDefinition Definition(TypeReference t,int depth=0) {
        if(depth>16)throw new InvalidDataException("Declaration forwarder recursion exceeds bound");
        if(t is TypeSpecification s)return Definition(s.ElementType,depth+1);
        if(t is GenericParameter)throw new InvalidDataException("Generic parameter has no TypeDef");
        string assembly=t.Scope switch {AssemblyNameReference a=>a.Name,ModuleDefinition m=>m.Assembly.Name.Name,_=>throw new InvalidDataException("Unsupported declared scope")};
        if(!seals.ContainsKey(assembly))throw new InvalidDataException("Declaration dependency absent: "+assembly+" / "+Name(t));
        var module=Read(assembly).MainModule;
        if(t.Scope is AssemblyNameReference ar&&module.Assembly.Name.FullName!=ar.FullName)throw new InvalidDataException("Declared type scope differs from sealed assembly");
        var defined=module.GetType(t.FullName);if(defined!=null)return defined;
        var forwards=module.ExportedTypes.Where(f=>f.FullName==t.FullName).ToArray();
        if(forwards.Length!=1||!forwards[0].IsForwarder||forwards[0].Scope is not AssemblyNameReference target)
            throw new InvalidDataException("Type absent or ambiguous in exact declared module: "+Name(t));
        edges.Add(new{full_name=Name(t),declared_module=seals[assembly],forwarder_token=Program.Hex(forwards[0].MetadataToken.ToInt32()),target_identity=target.FullName});
        return Definition(new TypeReference(t.Namespace,t.Name,module,target),depth+1);
    }
    private Dictionary<string,object?> Ref(TypeReference? t,int depth=0) {
        if(depth>32)throw new InvalidDataException("Declaration type graph depth exceeds bound");
        if(t==null)return new();
        var r=new Dictionary<string,object?>();
        if(t is GenericParameter p) {
            r["kind"]="generic_parameter";r["assembly"]=p.Module.Assembly.Name.Name;r["name"]=p.Name;r["parameter_index"]=p.Position;
            r["parameter_owner"]=p.Type==GenericParameterType.Method?"method":"type";r["canonical_name"]=(p.Type==GenericParameterType.Method?"!!":"!")+p.Position;return r;
        }
        TypeReference root=t is GenericInstanceType g?g.ElementType:t is TypeSpecification s?s.ElementType:t;
        var d=Definition(root);r["assembly"]=d.Module.Assembly.Name.Name;r["assembly_identity"]=d.Module.Assembly.Name.FullName;
        if(t is GenericInstanceType instance) {
            var arguments=instance.GenericArguments.Select(a=>Ref(a,depth+1)).ToArray();r["kind"]="generic_instance";
            r["definition"]=Name(instance.ElementType);r["definition_token"]=Program.Hex(d.MetadataToken.ToInt32());r["arguments"]=arguments;
            r["canonical_name"]=Name(instance.ElementType)+"<"+String.Join(",",arguments.Select(a=>a["canonical_name"]))+">";
        } else if(t is ArrayType||t is PointerType||t is ByReferenceType) {
            var spec=(TypeSpecification)t;var element=Ref(spec.ElementType,depth+1);r["kind"]=t is ArrayType?"array":t is PointerType?"pointer":"by_reference";
            r["element"]=element;r["rank"]=t is ArrayType a?a.Rank:1;r["vector_array"]=t is ArrayType v&&v.IsVector;
            r["canonical_name"]=element["canonical_name"]+(t is ArrayType a2?a2.IsVector?"[]":"["+new string(',',a2.Rank-1)+"]":t is PointerType?"*":"&");
        } else if(t is TypeSpecification)throw new InvalidDataException("Unsupported declaration type graph");
        else {r["kind"]="named";r["canonical_name"]=Name(t);r["token"]=Program.Hex(d.MetadataToken.ToInt32());}
        return r;
    }
    private static object? ExpectedRef(JsonElement e) {
        if(e.ValueKind==JsonValueKind.Null)return null;
        string k=e.GetProperty("kind").GetString()!;var r=new Dictionary<string,object?> { ["kind"]=k,["assembly"]=e.GetProperty("assembly").GetString(),["canonical_name"]=e.GetProperty("canonical_name").GetString() };
        if(k=="generic_parameter")foreach(string n in new[]{"name","parameter_index","parameter_owner"})r[n]=e.GetProperty(n);
        else {
            r["assembly_identity"]=e.GetProperty("assembly_display_name").GetString();
            if(k=="named")r["token"]=e.GetProperty("token").GetString();
            else if(k=="generic_instance") {r["definition"]=e.GetProperty("definition");r["definition_token"]=e.GetProperty("definition_token");r["arguments"]=e.GetProperty("arguments").EnumerateArray().Select(ExpectedRef).ToArray();}
            else if(k is "array" or "pointer" or "by_reference") {r["element"]=ExpectedRef(e.GetProperty("element"));r["rank"]=e.GetProperty("rank");r["vector_array"]=e.GetProperty("vector_array");}
            else throw new InvalidDataException("Unsupported loaded reference kind");
        }
        return r;
    }
    private static string Primitive(MetadataType t)=>t switch {
      MetadataType.Boolean=>"IL2CPP_TYPE_BOOLEAN",MetadataType.Char=>"IL2CPP_TYPE_CHAR",MetadataType.SByte=>"IL2CPP_TYPE_I1",MetadataType.Byte=>"IL2CPP_TYPE_U1",MetadataType.Int16=>"IL2CPP_TYPE_I2",MetadataType.UInt16=>"IL2CPP_TYPE_U2",MetadataType.Int32=>"IL2CPP_TYPE_I4",MetadataType.UInt32=>"IL2CPP_TYPE_U4",MetadataType.Int64=>"IL2CPP_TYPE_I8",MetadataType.UInt64=>"IL2CPP_TYPE_U8",MetadataType.Single=>"IL2CPP_TYPE_R4",MetadataType.Double=>"IL2CPP_TYPE_R8",MetadataType.String=>"IL2CPP_TYPE_STRING",_=>throw new InvalidDataException("Unsupported attribute primitive")};
    private object Value(CustomAttributeArgument a,int depth=0) {
        if(depth>16)throw new InvalidDataException("Attribute nesting exceeds bound");
        if(a.Value is CustomAttributeArgument inner)return Value(inner,depth+1);
        if(a.Type.FullName=="System.Type")return new{kind="type",value=a.Value is TypeReference tr?Ref(tr):null};
        if(a.Type is ArrayType arr) {
            var d=Definition(arr.ElementType);bool en=d.IsEnum;
            return new{kind="array",element_type=en?"IL2CPP_TYPE_ENUM":arr.ElementType.FullName=="System.Type"?"IL2CPP_TYPE_IL2CPP_TYPE_INDEX":Primitive(arr.ElementType.MetadataType),enum_type=en?Ref(arr.ElementType):null,value=a.Value==null?null:((CustomAttributeArgument[])a.Value).Select(v=>Value(v,depth+1)).ToArray()};
        }
        if(a.Type.MetadataType is >=MetadataType.Boolean and <=MetadataType.String)return new{kind="primitive",type=Primitive(a.Type.MetadataType),value=a.Value is char c?(object)(int)c:a.Value};
        var type=Definition(a.Type);if(!type.IsEnum)throw new InvalidDataException("Unsupported decoded attribute argument");
        return new{kind="enum",type=Ref(a.Type),value=Value(new CustomAttributeArgument(type.Fields.Single(f=>f.Name=="value__").FieldType,a.Value),depth+1)};
    }
    private static object ExpectedValue(JsonElement e) {
        string k=e.GetProperty("kind").GetString()!;
        if(k=="primitive")return new{kind=k,type=e.GetProperty("type").GetString(),value=e.GetProperty("value")};
        if(k=="type")return new{kind=k,value=ExpectedRef(e.GetProperty("value"))};
        if(k=="enum")return new{kind=k,type=ExpectedRef(e.GetProperty("type")),value=ExpectedValue(e.GetProperty("value"))};
        if(k=="array")return new{kind=k,element_type=e.GetProperty("element_type"),enum_type=ExpectedRef(e.GetProperty("enum_type")),value=e.GetProperty("value").ValueKind==JsonValueKind.Null?null:e.GetProperty("value").EnumerateArray().Select(ExpectedValue).ToArray()};
        throw new InvalidDataException("Unsupported loaded attribute value");
    }
    private object Attribute(CustomAttribute a) {
        var owner=Definition(a.AttributeType);var ctor=owner.Methods.SingleOrDefault(m=>m.Name==".ctor"&&m.Parameters.Select(p=>Name(p.ParameterType)).SequenceEqual(a.Constructor.Parameters.Select(p=>Name(p.ParameterType))))??throw new InvalidDataException("Attribute constructor is absent/ambiguous");
        return new{assembly=owner.Module.Assembly.Name.Name,full_name=Name(owner),constructor_token=Program.Hex(ctor.MetadataToken.ToInt32()),arguments=a.ConstructorArguments.Select(v=>Value(v)).ToArray(),fields=a.Fields.Select(v=>new{name=v.Name,value=Value(v.Argument)}).ToArray(),properties=a.Properties.Select(v=>new{name=v.Name,value=Value(v.Argument)}).ToArray()};
    }
    private object Pseudo(string name) {
        var owner=Read("mscorlib").MainModule.GetType(name)??throw new InvalidDataException("Pseudo attribute type absent");
        var ctor=owner.Methods.Single(m=>m.Name==".ctor"&&m.Parameters.Count==0);
        return new{assembly="mscorlib",full_name=name,constructor_token=Program.Hex(ctor.MetadataToken.ToInt32()),arguments=Array.Empty<object>(),fields=Array.Empty<object>(),properties=Array.Empty<object>()};
    }
    private object[] Attributes(Mono.Collections.Generic.Collection<CustomAttribute> attributes,bool serializable=false,bool nonserialized=false) {
        var result=attributes.Select(Attribute).ToList();if(serializable)result.Add(Pseudo("System.SerializableAttribute"));if(nonserialized)result.Add(Pseudo("System.NonSerializedAttribute"));return result.ToArray();
    }
    private static object[] ExpectedAttributes(JsonElement e) => e.EnumerateArray().Select(a=>(object)new{assembly=a.GetProperty("assembly").GetString(),full_name=a.GetProperty("full_name").GetString(),constructor_token=a.GetProperty("constructor_token").GetString(),arguments=a.GetProperty("arguments").EnumerateArray().Select(ExpectedValue).ToArray(),fields=a.GetProperty("fields").EnumerateArray().Select(v=>new{name=v.GetProperty("name").GetString(),value=ExpectedValue(v.GetProperty("value"))}).ToArray(),properties=a.GetProperty("properties").EnumerateArray().Select(v=>new{name=v.GetProperty("name").GetString(),value=ExpectedValue(v.GetProperty("value"))}).ToArray()}).ToArray();
    private static void Same(object? actual,object? expected,string where) {
        var a=JsonSerializer.SerializeToElement(actual);var e=JsonSerializer.SerializeToElement(expected);
        bool Equal(JsonElement x,JsonElement y) {
            if(x.ValueKind!=y.ValueKind)return false;
            if(x.ValueKind==JsonValueKind.Object){var d=x.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value);return d.Count==y.EnumerateObject().Count()&&y.EnumerateObject().All(p=>d.TryGetValue(p.Name,out var v)&&Equal(v,p.Value));}
            if(x.ValueKind==JsonValueKind.Array)return x.GetArrayLength()==y.GetArrayLength()&&x.EnumerateArray().Zip(y.EnumerateArray()).All(p=>Equal(p.First,p.Second));
            return x.GetRawText()==y.GetRawText();
        }
        if(!Equal(a,e))throw new InvalidDataException("Loaded declaration mismatch at "+where+" (disk "+a.GetRawText()+"; loaded "+e.GetRawText()+")");
    }
    private void ReferenceCheck(TypeReference? actual,JsonElement expected,string where) {Same(actual==null?null:Ref(actual),ExpectedRef(expected),where);}
    public object Generate() {
        if(request.loaded_types.ValueKind!=JsonValueKind.Array||request.loaded_types.GetArrayLength()!=Allowed.Count)throw new InvalidDataException("Exactly forty loaded declarations required");
        var seen=new HashSet<string>(StringComparer.Ordinal);var rows=new List<object>();
        foreach(var e in request.loaded_types.EnumerateArray()) {
            string assembly=e.GetProperty("assembly").GetString()!,name=e.GetProperty("full_name").GetString()!,key=assembly+":"+name;
            if(!Allowed.Contains(key)||!seen.Add(key))throw new InvalidDataException("Loaded declarations are outside/duplicated in exact profile");
            var seal=seals[assembly];
        var module=Read(assembly).MainModule;
            var token=e.GetProperty("token").GetString()!;
            if(!System.Text.RegularExpressions.Regex.IsMatch(token,"\\A0x02[a-f0-9]{6}\\z"))throw new InvalidDataException("Invalid loaded TypeDef token");
            var t=module.LookupToken(Convert.ToInt32(token[2..],16)) as TypeDefinition??throw new InvalidDataException("Loaded token is not an actual TypeDef");
            if(Name(t)!=name)throw new InvalidDataException("Loaded TypeDef token/name differs from disk: "+key);
            var lm=e.GetProperty("loaded_module");Program.ExactKeys(lm,new[]{"path","sha256","mvid","assembly_identity"});
            var readerSeal=request.engine.Single(r=>r.name=="cecil");var actualReader=typeof(ModuleDefinition).Assembly;
            var readerProof=e.GetProperty("loaded_module_reader");Program.ExactKeys(readerProof,new[]{"path","sha256","mvid","assembly_identity"});
            foreach(var pair in new[]{("path",readerSeal.path),("sha256",Program.Hash(Program.Read(actualReader.Location))),("mvid",actualReader.ManifestModule.ModuleVersionId.ToString()),("assembly_identity",actualReader.GetName().FullName!)})Same(pair.Item2,readerProof.GetProperty(pair.Item1),key+" actual reader "+pair.Item1);
            foreach(var pair in new[]{("path",seal.path),("sha256",seal.sha256),("mvid",seal.mvid),("assembly_identity",module.Assembly.Name.FullName)})Same(pair.Item2,lm.GetProperty(pair.Item1),key+" module "+pair.Item1);
            foreach(string complete in new[]{"schema_complete","custom_attributes_complete","loaded_module_identity_verified"})Same(true,e.GetProperty(complete),key+" "+complete);
            Same((int)t.Attributes,e.GetProperty("attributes"),key+" flags");Same(t.Name,e.GetProperty("name"),key+" name");var namespaceOwner=t;while(namespaceOwner.DeclaringType!=null)namespaceOwner=namespaceOwner.DeclaringType;Same(namespaceOwner.Namespace,e.GetProperty("namespace"),key+" namespace");
            Same(t.IsEnum,e.GetProperty("is_enum"),key+" enum");Same(t.IsValueType,e.GetProperty("is_value_type"),key+" value type");Same(t.IsAbstract,e.GetProperty("is_abstract"),key+" abstract");
            Same(t.DeclaringType==null?null:Name(t.DeclaringType),e.GetProperty("declaring_type"),key+" declaring type");ReferenceCheck(t.BaseType,e.GetProperty("base_type"),key+" base");
            ReferenceCheck(t.IsEnum?t.Fields.Single(f=>f.Name=="value__").FieldType:null,e.GetProperty("enum_underlying_type"),key+" enum underlying");
            var gp=e.GetProperty("generic_parameters");Same(t.GenericParameters.Count,gp.GetArrayLength(),key+" generic count");
            for(int i=0;i<t.GenericParameters.Count;i++) {var p=t.GenericParameters[i];var expected=gp[i];Same(p.Name,expected.GetProperty("name"),key+" generic name");Same(p.Position,expected.GetProperty("index"),key+" generic position");Same((int)p.Attributes,expected.GetProperty("attributes"),key+" generic flags");Same(true,expected.GetProperty("constraints_complete"),key+" constraints complete");var c=expected.GetProperty("constraints");Same(p.Constraints.Count,c.GetArrayLength(),key+" constraint count");for(int n=0;n<p.Constraints.Count;n++)ReferenceCheck(p.Constraints[n].ConstraintType,c[n],key+" constraint");}
            Same(Attributes(t.CustomAttributes,t.IsSerializable),ExpectedAttributes(e.GetProperty("custom_attributes")),key+" custom attributes");
            var fields=e.GetProperty("fields");Same(t.Fields.Count,fields.GetArrayLength(),key+" field count");
            for(int i=0;i<t.Fields.Count;i++) {
                var f=t.Fields[i];var x=fields[i];string at=key+" field "+i;
                Same(f.Name,x.GetProperty("name"),at+" name");Same(Program.Hex(f.MetadataToken.ToInt32()),x.GetProperty("token"),at+" token");Same((int)f.Attributes,x.GetProperty("attributes"),at+" flags");
                ReferenceCheck(f.FieldType,x.GetProperty("field_type"),at+" type");Same(Attributes(f.CustomAttributes,nonserialized:f.IsNotSerialized),ExpectedAttributes(x.GetProperty("custom_attributes")),at+" custom attributes");
                foreach(var p in new[]{("is_public",f.IsPublic),("is_static",f.IsStatic),("is_readonly",f.IsInitOnly),("is_const",f.IsLiteral),("non_serialized",f.IsNotSerialized),("has_default_value",f.HasConstant),("schema_complete",true),("custom_attributes_complete",true),("default_value_complete",true)})Same(p.Item2,x.GetProperty(p.Item1),at+" "+p.Item1);
                Same(f.HasConstant?f.Constant:null,x.GetProperty("default_value"),at+" constant");
                var constantType=f.HasConstant?(Definition(f.FieldType).IsEnum?Definition(f.FieldType).Fields.Single(z=>z.Name=="value__").FieldType:f.FieldType):null;
                ReferenceCheck(constantType,x.GetProperty("default_value_type"),at+" constant type");
                bool serialize=f.CustomAttributes.Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeField"),reference=f.CustomAttributes.Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeReference");
                Same(serialize,x.GetProperty("serialize_field"),at+" SerializeField");Same(reference,x.GetProperty("serialize_reference"),at+" SerializeReference");
                Same(!f.IsStatic&&!f.IsLiteral&&!f.IsInitOnly&&!f.IsNotSerialized&&(f.IsPublic||serialize||reference),x.GetProperty("unity_serialization_candidate"),at+" declared serialization candidate");
                Same(false,x.GetProperty("unity_type_eligibility_verified"),at+" installed serializer approval stays separate");
            }
            rows.Add(new{assembly,full_name=name,token,attributes=(int)t.Attributes,module=seal,field_count=t.Fields.Count,custom_attribute_count=t.CustomAttributes.Count,generic_parameter_count=t.GenericParameters.Count,
              declared_base_scope=t.BaseType?.Scope?.ToString(),fields=t.Fields.Select(f=>new{name=f.Name,token=Program.Hex(f.MetadataToken.ToInt32()),attributes=(int)f.Attributes,declared_type=f.FieldType.FullName,declared_scope=f.FieldType.Scope?.ToString(),custom_attribute_blobs=f.CustomAttributes.Select(a=>new{name=a.AttributeType.FullName,declared_scope=a.AttributeType.Scope?.ToString(),blob_hex=Convert.ToHexString(a.GetBlob()).ToLowerInvariant()}).ToArray()}).ToArray(),custom_attribute_blobs=t.CustomAttributes.Select(a=>new{name=a.AttributeType.FullName,declared_scope=a.AttributeType.Scope?.ToString(),blob_hex=Convert.ToHexString(a.GetBlob()).ToLowerInvariant()}).ToArray()});
        }
        foreach(var s in seals.Values)s.Check();
        return new{schema_version=1,profile="zone-theme-v1",loaded_inventory_sha256=request.loaded_inventory_sha256,type_count=rows.Count,modules=seals.Values.OrderBy(s=>s.path,StringComparer.Ordinal).ToArray(),types=rows,metadata_forwarder_edges=edges,implicit_dependency_resolution=false,player_clr_loads=false,editor_invoked=false,layout_approved=false,owners_verified=false};
    }
    public void Dispose(){foreach(var a in opened.Values)a.Dispose();}
}
