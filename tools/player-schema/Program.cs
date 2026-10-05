using Mono.Cecil;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectLucid.PlayerMetadata;

internal static class Program
{
    internal static string Hash(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    internal static string Hex(int n) => "0x" + n.ToString("x8", CultureInfo.InvariantCulture);
    internal static string Name(TypeReference t) => t.FullName.Replace('/', '+');
    internal static void PathCheck(string path)
    {
        if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path) throw new InvalidDataException("Noncanonical metadata path");
        for (string? p = path; p != null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Symbolic link in metadata path");
    }
    internal static byte[] Read(string path, long maximum = 256L * 1024 * 1024)
    {
        PathCheck(path); var f = new FileInfo(path);
        if (!f.Exists || f.Length <= 0 || f.Length > maximum) throw new InvalidDataException("Missing or oversized metadata input");
        return File.ReadAllBytes(path);
    }
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new InvalidDataException("Expected sealed manifest and fresh pending output");
            byte[] manifestBytes = Read(args[0], 4 * 1024 * 1024);
            var request = JsonSerializer.Deserialize<Request>(manifestBytes) ?? throw new InvalidDataException("Missing metadata request");
            request.Check(args[0], args[1]);
            using var resolver = new SealedResolver(request.modules);
            var reader = typeof(ModuleDefinition).Assembly;
            var readerSeal = request.engine.Single(r => r.name == "cecil");
            if (Hash(Read(reader.Location)) != readerSeal.sha256 || reader.ManifestModule.ModuleVersionId.ToString() != readerSeal.mvid)
                throw new InvalidDataException("Loaded Cecil differs from sealed installed reader");
            var serializerSeal = request.engine.Single(r => r.name == "serialization");
            var serializer = System.Reflection.Assembly.LoadFrom(serializerSeal.path);
            if (Hash(Read(serializer.Location)) != serializerSeal.sha256 || serializer.ManifestModule.ModuleVersionId.ToString() != serializerSeal.mvid)
                throw new InvalidDataException("Loaded serializer differs from sealed installed reader");
            var schema = new Inventory(resolver, serializer, request);
            var inventory = schema.Generate();
            resolver.CheckAll();
            if (Hash(Read(args[0], 4 * 1024 * 1024)) != Hash(manifestBytes) || Hash(Read(reader.Location)) != readerSeal.sha256 || Hash(Read(serializer.Location)) != serializerSeal.sha256)
                throw new InvalidDataException("Metadata context or installed reader changed");
            inventory["manifest_sha256"] = Hash(manifestBytes);
            inventory["reader"] = new { path = reader.Location, sha256 = Hash(Read(reader.Location)), mvid = reader.ManifestModule.ModuleVersionId.ToString() };
            inventory["serializer"] = new { path = serializer.Location, sha256 = Hash(Read(serializer.Location)), mvid = serializer.ManifestModule.ModuleVersionId.ToString() };
            PathCheck(args[1]);
            using (var stream = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(stream, inventory, new JsonSerializerOptions { WriteIndented = true, MaxDepth = 96 });
            Console.WriteLine("Player metadata inventory generated; comparison and asset approval remain separate.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }
    }
}

internal sealed class Seal
{
    public string path { get; set; } = "";
    public string sha256 { get; set; } = "";
    public long size { get; set; }
    public string assembly_name { get; set; } = "";
    public string mvid { get; set; } = "";
    public string kind { get; set; } = "";
    public string name { get; set; } = "";
    public void Check() { byte[] bytes = Program.Read(path); if (bytes.LongLength != size || Program.Hash(bytes) != sha256) throw new InvalidDataException("Sealed metadata bytes changed"); }
}
internal sealed class Request
{
    public int schema_version { get; set; }
    public string command { get; set; } = "";
    public string profile { get; set; } = "";
    public string target { get; set; } = "";
    public string nonce { get; set; } = "";
    public string source_fingerprint { get; set; } = "";
    public string project_root { get; set; } = "";
    public string work_root { get; set; } = "";
    public string run { get; set; } = "";
    public string player_receipt_sha256 { get; set; } = "";
    public List<Seal> modules { get; set; } = new();
    public List<Seal> engine { get; set; } = new();
    public void Check(string manifest, string output)
    {
        foreach (string p in new[] { project_root, work_root, run, manifest, output }) Program.PathCheck(p);
        if (schema_version != 1 || command != "player-schema" || profile != "zone-theme-v1" || !new[] { "macos", "windows", "linux" }.Contains(target) ||
            !Regex.IsMatch(nonce, "^[a-f0-9]{32}$") || !Regex.IsMatch(source_fingerprint, "^[a-f0-9]{64}$") || !Regex.IsMatch(player_receipt_sha256, "^[a-f0-9]{64}$") ||
            run != Path.Combine(work_root, "player-schema", "runs", nonce) || manifest != Path.Combine(run, "manifest.json") || output != Path.Combine(run, "pending.json"))
            throw new InvalidDataException("Incomplete or unowned metadata request");
        foreach (string forbidden in new[] { "Assets", "Packages", "ProjectSettings", "tools", "input" })
            if (run == Path.Combine(project_root, forbidden) || run.StartsWith(Path.Combine(project_root, forbidden) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("Metadata output is inside maintained source or supplied input");
        if (File.Exists(output) || Directory.Exists(output) || modules.Count is < 1 or > 4096 || engine.Count != 5)
            throw new InvalidDataException("Metadata request requires fresh output and bounded actual inputs");
        foreach (var r in modules) r.Check();
        foreach (var r in engine.Where(r => r.name is "cecil" or "serialization")) r.Check();
    }
}

// This resolver has no search directories or fallback. Its candidates are the
// sealed returned player modules and sealed source-plan precompiled references.
// A source plan is not proof of the compiler's actual response-file resolution.
internal sealed class SealedResolver : IAssemblyResolver
{
    private readonly Dictionary<string, Seal> records;
    private readonly Dictionary<string, AssemblyDefinition> opened = new(StringComparer.Ordinal);
    public SealedResolver(List<Seal> seals) { records = seals.ToDictionary(r => r.assembly_name, StringComparer.Ordinal); CheckAll(); }
    public void CheckAll() { foreach (var s in records.Values) s.Check(); }
    public AssemblyDefinition Read(string name)
    {
        if (opened.TryGetValue(name, out var old)) return old;
        if (!records.TryGetValue(name, out var seal)) throw new InvalidDataException("Reference is absent from sealed metadata candidates: " + name);
        seal.Check();
        var result = AssemblyDefinition.ReadAssembly(seal.path, new ReaderParameters { AssemblyResolver = this, ReadingMode = ReadingMode.Deferred, InMemory = true });
        if (result.Name.Name != name || result.MainModule.Mvid.ToString() != seal.mvid) { result.Dispose(); throw new InvalidDataException("Metadata name or MVID differs from sealed bytes"); }
        opened.Add(name, result); return result;
    }
    public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());
    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        var result = Read(name.Name);
        if (result.Name.FullName != name.FullName) throw new InvalidDataException("Sealed reference full assembly identity differs");
        return result;
    }
    public Seal Seal(ModuleDefinition module) => records[module.Assembly.Name.Name];
    public void Dispose() { foreach (var a in opened.Values) a.Dispose(); }
}

internal sealed class Inventory
{
    private readonly SealedResolver resolver;
    private readonly Request request;
    private readonly MethodInfo serialize, builtin;
    private readonly Dictionary<string, TypeDefinition> visited = new(StringComparer.Ordinal);
    private readonly Queue<TypeDefinition> pending = new();
    private readonly List<Dictionary<string, object?>> types = new();
    private readonly List<object> edges = new();
    // A versioned exact profile, never a namespace-wide permission.
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "netstandard:System.ComponentModel.EditorBrowsableState",
        "netstandard:System.ComponentModel.EditorBrowsableAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Scripting.UsedByNativeCodeAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.VisibleToOtherModulesAttribute",
        "netstandard:System.Diagnostics.DebuggerBrowsableState",
        "netstandard:System.Diagnostics.DebuggerBrowsableAttribute",
        "UnityEngine.CoreModule:UnityEngine.ExtensionOfNativeClassAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.NativeTypeAttribute",
        "UnityEngine.CoreModule:UnityEngine.ExcludeFromPresetAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.NativeClassAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Scripting.RequiredByNativeCodeAttribute",
        "Game.Runtime:HardlightProject.ZoneThemeOverride",
        "HLUnityCore.Runtime:Hardlight.ShowIfAttribute",
        "HLUnityCore.Runtime:Hardlight.InspectorConditionalAttribute", "HLUnityCore.Runtime:Hardlight.InspectorConditionalAttribute+ComparisonType",
        "HLUnityCore.Runtime:Hardlight.InspectorConditionalField",
        "HLUnityCore.Runtime:Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute", "HLUnityCore.Runtime:Unity.IL2CPP.CompilerServices.Option",
        "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite", "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceT`1",
        "Unity.Addressables:UnityEngine.AddressableAssets.AssetReference",
        "UnityEngine.CoreModule:UnityEngine.Color", "UnityEngine.CoreModule:UnityEngine.Sprite", "UnityEngine.CoreModule:UnityEngine.Object",
        "UnityEngine.CoreModule:UnityEngine.ScriptableObject", "UnityEngine.CoreModule:UnityEngine.PropertyAttribute",
        "UnityEngine.CoreModule:UnityEngine.SerializeField", "UnityEngine.CoreModule:UnityEngine.CreateAssetMenuAttribute",
        "UnityEngine.CoreModule:UnityEngine.Serialization.FormerlySerializedAsAttribute",
        "UnityEngine.SharedInternalsModule:UnityEngine.Bindings.NativeHeaderAttribute",
        "netstandard:System.Object", "netstandard:System.ValueType", "netstandard:System.Enum", "netstandard:System.Attribute",
        "netstandard:System.Reflection.DefaultMemberAttribute", "netstandard:System.AttributeUsageAttribute", "netstandard:System.AttributeTargets", "netstandard:System.FlagsAttribute",
        "netstandard:System.Runtime.CompilerServices.CompilerGeneratedAttribute", "netstandard:System.Runtime.CompilerServices.IsReadOnlyAttribute"
    };
    public Inventory(SealedResolver resolver, System.Reflection.Assembly logic, Request request)
    {
        this.resolver = resolver; this.request = request;
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        serialize = logic.GetType("Unity.SerializationLogic.UnitySerializationLogic", true)!.GetMethod("WillUnitySerialize", flags, null, new[] { typeof(FieldDefinition) }, null) ?? throw new InvalidDataException("Installed serializer API differs");
        builtin = logic.GetType("Unity.SerializationLogic.UnityEngineTypePredicates", true)!.GetMethod("ShouldHaveHadSerializableAttribute", flags, null, new[] { typeof(TypeReference) }, null) ?? throw new InvalidDataException("Installed builtin predicate API differs");
    }
    private static string Key(TypeDefinition t) => t.Module.Assembly.Name.Name + ":" + Program.Name(t);
    private void Add(TypeDefinition t, string reason)
    {
        string key = Key(t); if (!Allowed.Contains(key)) throw new InvalidDataException("Unapproved profile dependency: " + key);
        if (visited.TryAdd(key, t)) { if (visited.Count > 128) throw new InvalidDataException("Profile type count exceeds bound"); pending.Enqueue(t); }
        edges.Add(new { type = key, reason });
    }
    private TypeDefinition Resolve(TypeReference t) => t.Resolve() ?? throw new InvalidDataException("Unresolved metadata type: " + Program.Name(t));
    internal void AttributeDependency(TypeReference t) => Dependency(t, "attribute-value");
    internal TypeReference ContractType(string name) => resolver.Read("netstandard").MainModule.GetType(name) ?? throw new InvalidDataException("Required compiler contract type is absent");
    private void Dependency(TypeReference t, string reason, bool includePrimitive = false)
    {
        if (t is GenericParameter) return;
        if (t is GenericInstanceType g) { Add(Resolve(g.ElementType), reason); foreach (var argument in g.GenericArguments) Dependency(argument, reason); return; }
        if (t is ArrayType a) { Dependency(a.ElementType, reason); return; }
        if (t is TypeSpecification) throw new InvalidDataException("Unsupported closure reference");
        if (!includePrimitive && t.MetadataType is >= MetadataType.Boolean and <= MetadataType.String) return;
        Add(Resolve(t), reason);
    }
    public Dictionary<string, object?> Generate()
    {
        foreach (string key in new[] {
            "Game.Runtime:HardlightProject.ZoneThemeOverride", "UnityEngine.CoreModule:UnityEngine.Color", "UnityEngine.CoreModule:UnityEngine.Sprite",
            "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite", "Unity.Addressables:UnityEngine.AddressableAssets.AssetReferenceT`1", "Unity.Addressables:UnityEngine.AddressableAssets.AssetReference",
            "HLUnityCore.Runtime:Hardlight.ShowIfAttribute", "HLUnityCore.Runtime:Hardlight.InspectorConditionalField", "HLUnityCore.Runtime:Hardlight.InspectorConditionalAttribute/ComparisonType" })
        {
            string[] parts = key.Split(':', 2); var module = resolver.Read(parts[0]).MainModule;
            Add(module.GetType(parts[1]) ?? throw new InvalidDataException("Required profile type is absent"), "profile-root");
        }
        while (pending.Count != 0) types.Add(TypeSchema(pending.Dequeue()));
        return new Dictionary<string, object?> {
            ["schema_version"] = 1, ["command"] = "player-schema", ["status"] = "pending-native-verification",
            ["identity_status"] = "pending-wrapper", ["profile"] = request.profile, ["target"] = request.target, ["nonce"] = request.nonce,
            ["source_fingerprint"] = request.source_fingerprint, ["player_receipt_sha256"] = request.player_receipt_sha256,
            ["project_root"] = request.project_root, ["work_root"] = request.work_root, ["run"] = request.run,
            ["assemblies"] = types.GroupBy(t => (string)t["assembly"]!).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new {
                name = g.Key, types = g.OrderBy(t => (string)t["full_name"]!, StringComparer.Ordinal).ToArray(),
                module = resolver.Seal(visited.Values.First(t => t.Module.Assembly.Name.Name == g.Key).Module)
            }).ToArray(),
            ["assembly_count"] = types.Select(t => t["assembly"]).Distinct().Count(), ["type_count"] = types.Count,
            ["closure_edges"] = edges, ["errors"] = Array.Empty<object>(), ["references_modified"] = false,
            ["metadata_graph_complete"] = true, ["player_schema_verified"] = false, ["remap_approved"] = false, ["gameplay_verified"] = false,
            ["compiler_response_files_verified"] = false, ["resolver_provenance"] = "metadata-resolution-over-sealed-plan-candidates",
            ["scope"] = "Exact bounded profile metadata only. Source plan candidates are not proof of compiler command resolution; serialization comparison and original asset approval remain separate."
        };
    }
    internal Dictionary<string, object?> Reference(TypeReference t, int depth = 0)
    {
        if (depth > 32) throw new InvalidDataException("Type reference nesting exceeds bound");
        TypeReference definition = t is GenericInstanceType g ? g.ElementType : t is TypeSpecification s ? s.ElementType : t;
        string assembly = definition is GenericParameter ? definition.Module.Assembly.Name.Name : Resolve(definition).Module.Assembly.Name.Name;
        var result = new Dictionary<string, object?> { ["assembly"] = assembly, ["declared_scope"] = t.Scope?.ToString(), ["reflection_full_name"] = Program.Name(t) };
        if (t is GenericParameter p) {
            result["kind"] = "generic_parameter"; result["parameter_index"] = p.Position; result["parameter_owner"] = p.Type == GenericParameterType.Method ? "method" : "type";
            result["name"] = p.Name; result["canonical_name"] = (p.Type == GenericParameterType.Method ? "!!" : "!") + p.Position;
        }
        else if (t is ArrayType array || t is PointerType || t is ByReferenceType) {
            var spec = (TypeSpecification)t; var element = Reference(spec.ElementType, depth + 1); result["kind"] = t is ArrayType ? "array" : t is PointerType ? "pointer" : "by_reference";
            result["element"] = element; result["rank"] = t is ArrayType a ? a.Rank : 1; result["vector_array"] = t is ArrayType v && v.IsVector;
            result["canonical_name"] = element["canonical_name"] + (t is ArrayType a2 ? a2.IsVector ? "[]" : "[" + new string(',', a2.Rank - 1) + "]" : t is PointerType ? "*" : "&");
        }
        else if (t is GenericInstanceType generic) {
            var args = generic.GenericArguments.Select(a => Reference(a, depth + 1)).ToArray(); result["kind"] = "generic_instance"; result["definition"] = Program.Name(generic.ElementType);
            result["definition_token"] = Program.Hex(Resolve(generic.ElementType).MetadataToken.ToInt32()); result["arguments"] = args;
            result["canonical_name"] = Program.Name(generic.ElementType) + "<" + String.Join(",", args.Select(a => a["canonical_name"])) + ">";
        }
        else if (t is TypeSpecification) throw new InvalidDataException("Unsupported metadata type reference");
        else { result["kind"] = "named"; result["canonical_name"] = Program.Name(t); result["token"] = Program.Hex(Resolve(t).MetadataToken.ToInt32()); }
        return result;
    }
    private bool HasBase(TypeDefinition t, string name)
    {
        var seen = new HashSet<string>();
        while (true) { if (Program.Name(t) == name) return true; if (!seen.Add(Key(t))) throw new InvalidDataException("Cyclic metadata base"); if (t.BaseType == null) return false; t = Resolve(t.BaseType); }
    }
    private object[] Attributes(Mono.Cecil.ICustomAttributeProvider provider, ModuleDefinition module, int ownerToken)
    {
        if (provider.CustomAttributes.Count > 1024) throw new InvalidDataException("Too many custom attributes");
        return provider.CustomAttributes.Select(attribute => {
            Add(Resolve(attribute.AttributeType), "custom-attribute");
            return (object)new BlobDecoder(this, attribute, resolver.Seal(module), ownerToken).Decode();
        }).ToArray();
    }
    private Dictionary<string, object?> TypeSchema(TypeDefinition t)
    {
        if (t.BaseType != null) Dependency(t.BaseType, "base");
        foreach (var p in t.GenericParameters) foreach (var c in p.Constraints) Dependency(c.ConstraintType, "generic-constraint");
        var fields = new List<Dictionary<string, object?>>();
        foreach (var f in t.Fields.OrderBy(f => f.MetadataToken.ToInt32())) {
            int flags = (int)f.Attributes;
            bool sf = f.CustomAttributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeField"), sr = f.CustomAttributes.Any(a => a.AttributeType.FullName == "UnityEngine.SerializeReference");
            bool ns = (flags & 128) != 0 || f.CustomAttributes.Any(a => a.AttributeType.FullName == "System.NonSerializedAttribute");
            bool candidate = !f.IsStatic && !f.IsLiteral && !f.IsInitOnly && !ns && (f.IsPublic || sf || sr);
            bool eligible = (bool)serialize.Invoke(null, new object[] { f })!;
            if (eligible) Dependency(f.FieldType, "serialized-field");
            TypeReference? constantType = f.HasConstant ? Resolve(f.FieldType).IsEnum ? Resolve(f.FieldType).Fields.Single(v => v.Name == "value__").FieldType : f.FieldType : null;
            fields.Add(new Dictionary<string, object?> {
                ["name"] = f.Name, ["token"] = Program.Hex(f.MetadataToken.ToInt32()), ["attributes"] = flags, ["schema_complete"] = true,
                ["field_type"] = Reference(f.FieldType), ["custom_attributes"] = Attributes(f, t.Module, f.MetadataToken.ToInt32()), ["custom_attributes_complete"] = true,
                ["is_public"] = f.IsPublic, ["is_static"] = f.IsStatic, ["is_const"] = f.IsLiteral, ["is_readonly"] = f.IsInitOnly, ["non_serialized"] = ns,
                ["serialize_field"] = sf, ["serialize_reference"] = sr, ["unity_serialization_candidate"] = candidate,
                ["unity_type_eligibility_verified"] = true, ["will_unity_serialize"] = eligible,
                ["has_default_value"] = f.HasConstant, ["default_value_complete"] = true, ["default_value"] = f.HasConstant ? f.Constant : null,
                ["default_value_type"] = constantType == null ? null : Reference(constantType)
            });
        }
        return new Dictionary<string, object?> {
            ["assembly"] = t.Module.Assembly.Name.Name, ["token"] = Program.Hex(t.MetadataToken.ToInt32()), ["name"] = t.Name, ["namespace"] = t.Namespace, ["full_name"] = Program.Name(t),
            ["attributes"] = (int)t.Attributes, ["is_value_type"] = t.IsValueType, ["is_enum"] = t.IsEnum, ["is_abstract"] = t.IsAbstract,
            ["declaring_type"] = t.DeclaringType == null ? null : Program.Name(t.DeclaringType), ["base_type"] = t.BaseType == null ? null : Reference(t.BaseType),
            ["schema_complete"] = true, ["custom_attributes_complete"] = true, ["custom_attributes"] = Attributes(t, t.Module, t.MetadataToken.ToInt32()),
            ["generic_parameters"] = t.GenericParameters.Select(p => new { name = p.Name, index = p.Position, attributes = (int)p.Attributes, constraints = p.Constraints.Select(c => Reference(c.ConstraintType)).ToArray(), constraints_complete = true }).ToArray(),
            ["unity_component"] = HasBase(t, "UnityEngine.MonoBehaviour"), ["unity_scriptable_object"] = HasBase(t, "UnityEngine.ScriptableObject"),
            ["enum_underlying_type"] = t.IsEnum ? Reference(t.Fields.Single(f => f.Name == "value__").FieldType) : null,
            ["fields"] = fields, ["builtin_serializer_eligibility"] = (bool)builtin.Invoke(null, new object[] { t })!,
            ["module_provenance"] = resolver.Seal(t.Module)
        };
    }
    internal TypeDefinition Definition(TypeReference t) => Resolve(t);
    internal TypeReference TypeValue(string name, ModuleDefinition owner)
    {
        // Exact simple/nested named typeof values only. General generic type
        // name parsing remains unsupported rather than guessing an assembly.
        int comma = name.IndexOf(','); if (comma < 1 || name.Contains('[')) throw new InvalidDataException("Unsupported typeof encoding");
        string type = name[..comma].Trim().Replace('+', '/'); string identity = name[(comma + 1)..].Trim();
        var reference = AssemblyNameReference.Parse(identity); var assembly = resolver.Resolve(reference);
        return assembly.MainModule.GetType(type) ?? throw new InvalidDataException("Encoded typeof type is absent");
    }
}

// Parse the complete ECMA blob ourselves. Cecil's decoded object/null alone
// loses the boxed element tag. Each string retains its exact encoded bytes.
internal sealed class BlobDecoder
{
    private readonly Inventory inventory;
    private readonly CustomAttribute attribute;
    private readonly Seal seal;
    private readonly int ownerToken;
    private readonly byte[] bytes;
    private int position;
    public BlobDecoder(Inventory inventory, CustomAttribute attribute, Seal seal, int ownerToken) {
        this.inventory = inventory; this.attribute = attribute; this.seal = seal; this.ownerToken = ownerToken; bytes = attribute.GetBlob();
        if (bytes.Length is < 4 or > 1048576) throw new InvalidDataException("Unbounded ECMA attribute blob");
    }
    private byte Byte() { if (position >= bytes.Length) throw new InvalidDataException("Truncated ECMA blob"); return bytes[position++]; }
    private byte[] Bytes(int n) { if (n < 0 || position + n > bytes.Length) throw new InvalidDataException("Truncated ECMA value"); var b = bytes[position..(position+n)]; position += n; return b; }
    private ushort U16() => BitConverter.ToUInt16(Bytes(2));
    private (string?, byte[]) String() {
        int start = position; byte first = Byte(); if (first == 255) return (null, bytes[start..position]);
        int length;
        if (first < 128) length = first;
        else if ((first & 192) == 128) { length = ((first & 63) << 8) | Byte(); if (length < 128) throw new InvalidDataException("Noncanonical ECMA length"); }
        else if ((first & 224) == 192) { length = ((first & 31) << 24) | (Byte() << 16) | (Byte() << 8) | Byte(); if (length < 16384) throw new InvalidDataException("Noncanonical ECMA length"); }
        else throw new InvalidDataException("Invalid ECMA string length");
        if (length > 1048576) throw new InvalidDataException("Oversized ECMA string");
        string value = new UTF8Encoding(false, true).GetString(Bytes(length)); return (value, bytes[start..position]);
    }
    private static string Primitive(byte tag) => tag switch {
        2 => "IL2CPP_TYPE_BOOLEAN", 3 => "IL2CPP_TYPE_CHAR", 4 => "IL2CPP_TYPE_I1", 5 => "IL2CPP_TYPE_U1", 6 => "IL2CPP_TYPE_I2", 7 => "IL2CPP_TYPE_U2",
        8 => "IL2CPP_TYPE_I4", 9 => "IL2CPP_TYPE_U4", 10 => "IL2CPP_TYPE_I8", 11 => "IL2CPP_TYPE_U8", 12 => "IL2CPP_TYPE_R4", 13 => "IL2CPP_TYPE_R8", 14 => "IL2CPP_TYPE_STRING",
        _ => throw new InvalidDataException("Unsupported ECMA primitive tag") };
    private byte Tag(TypeReference t) => t.MetadataType switch {
        MetadataType.Boolean => 2, MetadataType.Char => 3, MetadataType.SByte => 4, MetadataType.Byte => 5, MetadataType.Int16 => 6, MetadataType.UInt16 => 7,
        MetadataType.Int32 => 8, MetadataType.UInt32 => 9, MetadataType.Int64 => 10, MetadataType.UInt64 => 11, MetadataType.Single => 12, MetadataType.Double => 13,
        MetadataType.String => 14, MetadataType.Object => 81, _ when t.FullName == "System.Type" => 80,
        _ when t is ArrayType => 29, _ when inventory.Definition(t).IsEnum => 85, _ => throw new InvalidDataException("Unsupported ECMA declared type") };
    private object Value(TypeReference t, int depth = 0, bool boxed = false) {
        if (depth > 16) throw new InvalidDataException("ECMA value recursion exceeds bound");
        byte tag = Tag(t); int start = position;
        if (tag == 81) {
            byte actual = Byte(); TypeReference element = Tagged(actual, t.Module);
            var result = (Dictionary<string, object?>)Value(element, depth + 1, true); result["ecma_boxed_tag"] = actual; result["ecma_declared_type"] = inventory.Reference(t); return result;
        }
        if (tag == 85) {
            inventory.AttributeDependency(t);
            var type = inventory.Definition(t); var inner = type.Fields.Single(f => f.Name == "value__").FieldType;
            return new Dictionary<string, object?> { ["kind"] = "enum", ["type"] = inventory.Reference(t), ["value"] = Value(inner, depth + 1), ["value_complete"] = true };
        }
        if (tag == 80) {
            var typeName = String(); return new Dictionary<string, object?> { ["kind"] = "type", ["value"] = typeName.Item1 == null ? null : inventory.Reference(inventory.TypeValue(typeName.Item1, t.Module)), ["value_complete"] = true };
        }
        if (tag == 29) {
            var array = (ArrayType)t; if (!array.IsVector) throw new InvalidDataException("Unsupported attribute array rank");
            int count = BitConverter.ToInt32(Bytes(4)); if (count < -1 || count > 4096) throw new InvalidDataException("Unbounded ECMA array");
            var element = array.ElementType; bool isEnum = inventory.Definition(element).IsEnum;
            return new Dictionary<string, object?> { ["kind"] = "array", ["element_type"] = isEnum ? "IL2CPP_TYPE_ENUM" : Tag(element) == 80 ? "IL2CPP_TYPE_IL2CPP_TYPE_INDEX" : Primitive(Tag(element)),
                ["enum_type"] = isEnum ? inventory.Reference(element) : null, ["value"] = count == -1 ? null : Enumerable.Range(0,count).Select(_ => Value(element,depth+1)).ToArray(), ["value_complete"] = true };
        }
        object? value;
        if (tag == 14) {
            var s = String(); return new Dictionary<string, object?> { ["kind"] = "primitive", ["type"] = Primitive(tag), ["value"] = s.Item1, ["value_complete"] = true,
                ["string_length"] = s.Item1 == null ? -1 : Encoding.UTF8.GetByteCount(s.Item1), ["raw_encoding_sha256"] = Program.Hash(s.Item2), ["ecma_tag"] = tag, ["ecma_boxed"] = boxed };
        }
        value = tag switch { 2 => Byte() switch { 0 => false, 1 => true, _ => throw new InvalidDataException("Invalid Boolean ECMA value") }, 3 => (int)U16(), 4 => (sbyte)Byte(), 5 => Byte(), 6 => BitConverter.ToInt16(Bytes(2)), 7 => U16(),
            8 => BitConverter.ToInt32(Bytes(4)), 9 => BitConverter.ToUInt32(Bytes(4)), 10 => BitConverter.ToInt64(Bytes(8)), 11 => BitConverter.ToUInt64(Bytes(8)),
            12 => BitConverter.ToSingle(Bytes(4)), 13 => BitConverter.ToDouble(Bytes(8)), _ => throw new InvalidDataException("Unknown ECMA value") };
        return new Dictionary<string, object?> { ["kind"] = "primitive", ["type"] = Primitive(tag), ["value"] = value, ["value_complete"] = true, ["ecma_tag"] = tag, ["ecma_boxed"] = boxed, ["raw_encoding_sha256"] = Program.Hash(bytes[start..position]) };
    }
    private TypeReference Tagged(byte tag, ModuleDefinition module) {
        if (tag == 85) { string? type = String().Item1; if (type == null) throw new InvalidDataException("Null encoded enum type"); return inventory.TypeValue(type,module); }
        if (tag == 80) return inventory.ContractType("System.Type");
        if (tag == 29) return new ArrayType(Tagged(Byte(),module));
        return tag switch { 2=>module.TypeSystem.Boolean,3=>module.TypeSystem.Char,4=>module.TypeSystem.SByte,5=>module.TypeSystem.Byte,6=>module.TypeSystem.Int16,7=>module.TypeSystem.UInt16,8=>module.TypeSystem.Int32,9=>module.TypeSystem.UInt32,
            10=>module.TypeSystem.Int64,11=>module.TypeSystem.UInt64,12=>module.TypeSystem.Single,13=>module.TypeSystem.Double,14=>module.TypeSystem.String,81=>module.TypeSystem.Object,_=>throw new InvalidDataException("Unsupported boxed/named ECMA tag") };
    }
    public Dictionary<string, object?> Decode() {
        if (U16() != 1) throw new InvalidDataException("Invalid ECMA attribute prolog");
        var ctor = attribute.Constructor.Resolve() ?? throw new InvalidDataException("Unresolved attribute constructor");
        object[] arguments = ctor.Parameters.Select(p => Value(p.ParameterType)).ToArray();
        int count = U16(); if (count > 4096) throw new InvalidDataException("Too many ECMA named arguments");
        var fields = new List<object>(); var properties = new List<object>(); var names = new HashSet<string>();
        for (int i=0;i<count;i++) {
            byte member = Byte(), tag = Byte(); if (member is not (83 or 84)) throw new InvalidDataException("Invalid ECMA named argument kind");
            TypeReference encoded = Tagged(tag, ctor.Module); string? name = String().Item1;
            if (System.String.IsNullOrEmpty(name) || !names.Add(member+":"+name)) throw new InvalidDataException("Duplicate or empty ECMA named member");
            TypeReference? declared = null; TypeDefinition? owner = ctor.DeclaringType;
            while(owner!=null && declared==null) { declared=member==83?owner.Fields.SingleOrDefault(f=>f.Name==name)?.FieldType:owner.Properties.SingleOrDefault(p=>p.Name==name)?.PropertyType; owner=owner.BaseType==null?null:inventory.Definition(owner.BaseType); }
            if (declared==null || Program.Name(encoded)!=Program.Name(declared)) throw new InvalidDataException("ECMA named member differs from declaration");
            var record = new { name, value = Value(declared) }; if(member==83) fields.Add(record);else properties.Add(record);
        }
        if (position != bytes.Length) throw new InvalidDataException("ECMA attribute has unconsumed trailing bytes");
        return new Dictionary<string, object?> { ["assembly"] = ctor.Module.Assembly.Name.Name, ["full_name"] = Program.Name(ctor.DeclaringType), ["constructor_token"] = Program.Hex(ctor.MetadataToken.ToInt32()),
            ["constructor_signature"] = ctor.FullName, ["constructor_parameter_types"] = ctor.Parameters.Select(p=>inventory.Reference(p.ParameterType)).ToArray(),
            ["arguments"] = arguments, ["fields"] = fields, ["properties"] = properties,
            ["ecma_blob_hex"] = Convert.ToHexString(bytes).ToLowerInvariant(), ["ecma_blob_sha256"] = Program.Hash(bytes), ["ecma_decoding_complete"] = true,
            ["ecma_provenance"] = new { module_path=seal.path,module_sha256=seal.sha256,module_mvid=seal.mvid,owner_token=Program.Hex(ownerToken) } };
    }
}
