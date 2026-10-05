using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using Cpp2IL.Core;
using Cpp2IL.Core.Model.Contexts;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ProjectLucid.CodeProgress;

// This reader measures source-body presence, never native-byte equivalence or
// behavioral parity. No original/player CLR assembly is loaded for inspection.
internal static class Program
{
    private sealed class NoResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name) => throw new InvalidDataException("Implicit assembly resolution is forbidden.");
        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);
        public void Dispose() { }
    }
    private const string Pin = "b5ad444b82267cb1e4b88b8b373c008105bdea52";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> Primitive = new(StringComparer.Ordinal) {
        "System.Void", "System.Boolean", "System.Char", "System.SByte", "System.Byte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single", "System.Double",
        "System.String", "System.Object", "System.IntPtr", "System.UIntPtr", "System.TypedReference" };

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string Name(string value) => value.Replace('/', '+');
    private static string Token(uint value) => "0x" + value.ToString("x8", CultureInfo.InvariantCulture);
    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString() ?? throw new InvalidDataException("Null context string.");
    private static bool Selected(JsonElement context, string assembly, string type)
    {
        if (!context.GetProperty("namespace_filters").TryGetProperty(assembly, out var filters)) return true;
        return filters.EnumerateArray().Any(prefix => Name(type).StartsWith(prefix.GetString()!, StringComparison.Ordinal));
    }
    private static string Regular(string path, long maximum)
    {
        if (Path.GetFullPath(path) != path) throw new InvalidDataException("Evidence paths must be canonical absolute paths.");
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Evidence symlink rejected.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > maximum) throw new InvalidDataException("Missing or oversized evidence file.");
        return path;
    }
    private static string DirectoryPath(string path)
    {
        if (Path.GetFullPath(path) != path || !Directory.Exists(path)) throw new InvalidDataException("Evidence directory must exist at a canonical path.");
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Evidence directory symlink rejected.");
        return path;
    }
    private static string[] ValidateContext(JsonElement context, string contextPath)
    {
        string[] fields = ["schema_version", "kind", "identity_nonce", "source_fingerprint", "source_root", "work_root", "run", "binary", "metadata", "scope", "namespace_filters", "player_modules"];
        if (context.ValueKind != JsonValueKind.Object || context.EnumerateObject().Select(p => p.Name).OrderBy(p => p).SequenceEqual(fields.OrderBy(p => p)) != true ||
            context.GetProperty("schema_version").GetInt32() != 1 || Text(context, "kind") != "project-lucid-code-progress-v1")
            throw new InvalidDataException("Invalid reader context.");
        string nonce = Text(context, "identity_nonce"), source = Text(context, "source_fingerprint");
        if (!Regex.IsMatch(nonce, "\\A[0-9a-f]{32}\\z") || !Regex.IsMatch(source, "\\A[0-9a-f]{64}\\z")) throw new InvalidDataException("Invalid reader identity.");
        string root = DirectoryPath(Text(context, "source_root")), work = DirectoryPath(Text(context, "work_root")), run = DirectoryPath(Text(context, "run"));
        if (work == root || new[] { "input", "Assets", "Packages", "ProjectSettings", "Library", ".git" }.Any(name =>
            work == Path.Combine(root, name) || work.StartsWith(Path.Combine(root, name) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) ||
            run != Path.Combine(work, "code-progress", "runs", nonce) || contextPath != Path.Combine(run, "context.json"))
            throw new InvalidDataException("Reader requires a nonce-owned work run.");
        string[] scope = context.GetProperty("scope").EnumerateArray().Select(e => e.GetString()!).ToArray();
        if (scope.Length is 0 or > 256 || scope.Distinct().Count() != scope.Length || scope.Any(name => name == null ||
            !Regex.IsMatch(name, "\\A(?:Game\\.Runtime|HL[A-Za-z0-9]+(?:\\.Runtime)?|Assembly-CSharp(?:-firstpass)?)\\z")))
            throw new InvalidDataException("Invalid fixed assembly scope.");
        var filters = context.GetProperty("namespace_filters");
        if (filters.ValueKind != JsonValueKind.Object || filters.EnumerateObject().Count() != 2 ||
            !filters.TryGetProperty("Assembly-CSharp", out var game) || !filters.TryGetProperty("Assembly-CSharp-firstpass", out var first) ||
            game.ValueKind != JsonValueKind.Array || first.ValueKind != JsonValueKind.Array ||
            !game.EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "HardlightProject." }) ||
            !first.EnumerateArray().Select(e => e.GetString()).SequenceEqual(new[] { "Hardlight." }))
            throw new InvalidDataException("Mixed assemblies require exact proprietary namespace scope.");
        var modules = context.GetProperty("player_modules");
        if (modules.ValueKind != JsonValueKind.Array || modules.GetArrayLength() > 256 || modules.EnumerateArray().Select(e => e.GetString()).Distinct().Count() != modules.GetArrayLength())
            throw new InvalidDataException("Invalid returned player module selection.");
        return scope;
    }
    private static object Seal(string path)
    {
        Regular(path, 2L * 1024 * 1024 * 1024);
        return new { path, size = new FileInfo(path).Length, sha256 = Hash(path) };
    }
    private static string NativeType(TypeAnalysisContext type, int depth = 0)
    {
        if (depth > 64) throw new InvalidDataException("Type nesting limit.");
        if (type is GenericParameterTypeAnalysisContext parameter)
            return (parameter.Owner is MethodAnalysisContext ? "!!" : "!") + parameter.Index;
        if (type is GenericInstanceTypeAnalysisContext generic)
            return NativeType(generic.GenericType, depth + 1) + "<" + string.Join(",", generic.GenericArguments.Select(a => NativeType(a, depth + 1))) + ">";
        if (type is WrappedTypeAnalysisContext wrapped)
        {
            string suffix = type switch { SzArrayTypeAnalysisContext => "[]", ArrayTypeAnalysisContext array => "[" + array.Rank + ":nonvector]",
                ByRefTypeAnalysisContext => "&", PointerTypeAnalysisContext => "*", _ => throw new InvalidDataException("Unknown type wrapper.") };
            return NativeType(wrapped.ElementType, depth + 1) + suffix;
        }
        string name = Name(type.DefaultFullName), assembly = type.DeclaringAssembly.DefaultName;
        if (Primitive.Contains(name) && assembly == "mscorlib") return "primitive:" + name;
        return "[" + assembly + "]" + name;
    }
    private static string PlayerType(TypeReference type, int depth = 0)
    {
        if (depth > 64) throw new InvalidDataException("Type nesting limit.");
        if (type is GenericParameter parameter) return (parameter.Type == GenericParameterType.Method ? "!!" : "!") + parameter.Position;
        if (type is GenericInstanceType generic)
            return PlayerType(generic.ElementType, depth + 1) + "<" + string.Join(",", generic.GenericArguments.Select(a => PlayerType(a, depth + 1))) + ">";
        if (type is ByReferenceType byref) return PlayerType(byref.ElementType, depth + 1) + "&";
        if (type is PointerType pointer) return PlayerType(pointer.ElementType, depth + 1) + "*";
        if (type is ArrayType array) return PlayerType(array.ElementType, depth + 1) + (array.IsVector ? "[]" : "[" + array.Rank + ":nonvector]");
        if (type is TypeSpecification) throw new InvalidDataException("Unsupported modifier/function pointer signature.");
        if (Primitive.Contains(type.FullName) && type.MetadataType != MetadataType.Class && type.MetadataType != MetadataType.ValueType)
            return "primitive:" + type.FullName;
        string assembly = type.Scope switch { AssemblyNameReference reference => reference.Name, ModuleDefinition module => module.Assembly.Name.Name,
            _ => throw new InvalidDataException("Unresolved signature module scope.") };
        return "[" + assembly + "]" + Name(type.FullName);
    }
    private static object NativeSignature(MethodAnalysisContext method) => new {
        owner = Name(method.DeclaringType!.DefaultFullName), name = method.DefaultName, is_static = method.IsStatic,
        return_type = NativeType(method.ReturnType), parameters = method.Parameters.Select(p => new { type = NativeType(p.ParameterType),
            direction = (int)p.Attributes & 3 }).ToArray(),
        declaring_generic_parameters = method.DeclaringType.GenericParameters.Select(p => new { flags = (int)p.Attributes,
            constraints = p.ConstraintTypes.Select(t => NativeType(t)).OrderBy(t => t, StringComparer.Ordinal).ToArray() }).ToArray(),
        generic_parameters = method.GenericParameters.Select(p => new { flags = (int)p.Attributes,
            constraints = p.ConstraintTypes.Select(t => NativeType(t)).OrderBy(t => t, StringComparer.Ordinal).ToArray() }).ToArray() };
    private static object PlayerSignature(MethodDefinition method) => new {
        owner = Name(method.DeclaringType.FullName), name = method.Name, is_static = method.IsStatic,
        return_type = PlayerType(method.ReturnType), parameters = method.Parameters.Select(p => new { type = PlayerType(p.ParameterType), direction = (int)p.Attributes & 3 }).ToArray(),
        declaring_generic_parameters = method.DeclaringType.GenericParameters.Select(p => new { flags = (int)p.Attributes,
            constraints = p.Constraints.Select(c => PlayerType(c.ConstraintType)).OrderBy(t => t, StringComparer.Ordinal).ToArray() }).ToArray(),
        generic_parameters = method.GenericParameters.Select(p => new { flags = (int)p.Attributes,
            constraints = p.Constraints.Select(c => PlayerType(c.ConstraintType)).OrderBy(t => t, StringComparer.Ordinal).ToArray() }).ToArray() };

    private static object Native(JsonElement context, string[] scope)
    {
        string binary = Regular(Text(context, "binary"), 2L * 1024 * 1024 * 1024), metadata = Regular(Text(context, "metadata"), 512L * 1024 * 1024);
        var seals = new[] { Seal(binary), Seal(metadata) };
        new Cpp2IlCorePlugin().OnLoad();
        Cpp2IlApi.InitializeLibCpp2Il(binary, metadata, UnityVersion.Parse("2022.3.54f1"), false);
        var app = Cpp2IlApi.CurrentAppContext!;
        var expectedScope = app.Assemblies.Where(a => a.Definition != null && (a.DefaultName == "Game.Runtime" || a.DefaultName.StartsWith("HL", StringComparison.Ordinal) ||
            a.DefaultName is "Assembly-CSharp" or "Assembly-CSharp-firstpass")).Select(a => a.DefaultName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (!expectedScope.SequenceEqual(scope.OrderBy(n => n, StringComparer.Ordinal))) throw new InvalidDataException("Scope omits original proprietary assemblies.");
        var assemblies = app.Assemblies.Where(a => a.Definition != null && scope.Contains(a.DefaultName, StringComparer.Ordinal)).ToArray();
        if (assemblies.Length != scope.Length) throw new InvalidDataException("Original assembly scope is incomplete.");
        var rows = new List<Dictionary<string, object?>>();
        var genericMap = app.ConcreteGenericMethodsByRef.Values.Where(g => g.BaseMethodContext.Definition != null && g.UnderlyingPointer != 0)
            .GroupBy(g => g.BaseMethodContext.Definition!).ToDictionary(g => g.Key, g => g.ToArray());
        using var bytes = File.OpenRead(binary);
        foreach (var assembly in assemblies.OrderBy(a => a.DefaultName, StringComparer.Ordinal))
        foreach (var type in assembly.Types.Where(t => t.Definition != null && Selected(context, assembly.DefaultName, t.DefaultFullName)))
        foreach (var method in type.Methods.Where(m => m.Definition != null))
        {
            string id = assembly.DefaultName + ":" + Name(type.DefaultFullName) + ":" + Token(method.Token);
            var row = new Dictionary<string, object?> { ["id"] = id, ["assembly"] = assembly.DefaultName, ["token"] = Token(method.Token),
                ["declaring_type"] = Name(type.DefaultFullName), ["name"] = method.DefaultName, ["signature"] = null,
                ["signature_error"] = null, ["native_body"] = false, ["native_pointer"] = null, ["file_offset"] = null,
                ["native_prefix_sha256"] = null, ["concrete_generic_variants"] = 0 };
            try { row["signature"] = NativeSignature(method); }
            catch (Exception error) { row["signature_error"] = error.GetType().Name; }
            // Abstract/runtime/extern contracts do not contribute source bodies.
            bool contract = method.IsAbstract || ((int)method.DefaultImplAttributes & 0x1003) != 0 || ((int)method.DefaultAttributes & 0x2000) != 0;
            var generics = genericMap.TryGetValue(method.Definition!, out var variants) ? variants : [];
            row["concrete_generic_variants"] = generics.Length;
            ulong pointer = method.UnderlyingPointer;
            if (pointer == 0 && generics.Length > 0) pointer = generics[0].UnderlyingPointer;
            if (!contract && pointer != 0)
            {
                if (!app.Binary.TryMapVirtualAddressToRaw(pointer, out long offset) || offset < 0 || offset > bytes.Length - 16)
                    throw new InvalidDataException("Original method pointer does not map into input bytes.");
                byte[] prefix = new byte[16]; bytes.Position = offset; bytes.ReadExactly(prefix);
                row["native_body"] = true; row["native_pointer"] = "0x" + pointer.ToString("x"); row["file_offset"] = offset;
                row["native_prefix_sha256"] = Convert.ToHexString(SHA256.HashData(prefix)).ToLowerInvariant();
            }
            rows.Add(row);
        }
        if (rows.Select(r => r["id"]).Distinct().Count() != rows.Count) throw new InvalidDataException("Duplicate original methods.");
        if (Hash(binary) != ((JsonElement)JsonSerializer.SerializeToElement(seals[0])).GetProperty("sha256").GetString())
            throw new InvalidDataException("Input changed during native inventory.");
        return new { kind = "original-native-method-inventory-v1", cpp2il_pin = Pin, modules = seals, methods = rows.OrderBy(r => r["id"], Comparer<object?>.Create((a,b) => StringComparer.Ordinal.Compare((string?)a,(string?)b))).ToArray(),
            native_byte_matching_verified = false, gameplay_verified = false };
    }

    private static IEnumerable<TypeDefinition> Types(TypeDefinition type)
    { yield return type; foreach (var nested in type.NestedTypes) foreach (var child in Types(nested)) yield return child; }
    private static bool Generated(MethodDefinition method)
    {
        const string marker = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
        if (method.Name.Contains('<') || method.DeclaringType.FullName.Contains('<') || method.CustomAttributes.Any(a => a.AttributeType.FullName == marker)) return true;
        for (var type = method.DeclaringType; type != null; type = type.DeclaringType)
            if (type.CustomAttributes.Any(a => a.AttributeType.FullName == marker)) return true;
        return false;
    }
    private static bool Trivial(Instruction instruction)
    {
        var code = instruction.OpCode.Code; string name = code.ToString();
        // No field access, calculation, conditional decision or call is allowed.
        // Conservative exclusions include genuine constant/out/identity methods.
        return instruction.OpCode.FlowControl is FlowControl.Return or FlowControl.Branch ||
            name.StartsWith("Ldc_", StringComparison.Ordinal) || name.StartsWith("Conv_", StringComparison.Ordinal) ||
            name.StartsWith("Ldloc", StringComparison.Ordinal) || name.StartsWith("Stloc", StringComparison.Ordinal) ||
            name.StartsWith("Ldarg", StringComparison.Ordinal) || name.StartsWith("Starg", StringComparison.Ordinal) ||
            name.StartsWith("Stind", StringComparison.Ordinal) ||
            code is Code.Ldnull or Code.Ldstr or Code.Initobj or Code.Stobj or Code.Box or Code.Unbox or Code.Unbox_Any or Code.Castclass or Code.Dup or Code.Pop;
    }
    private static string BodyStatus(MethodDefinition method)
    {
        if (!method.HasBody || method.IsAbstract || method.IsPInvokeImpl || method.IsRuntime) return "no_body";
        if (Generated(method)) return "compiler_generated_unresolved";
        var codes = method.Body.Instructions.Where(i => i.OpCode.Code != Code.Nop).ToArray();
        if (codes.Length == 0 || codes.All(i => i.OpCode.Code == Code.Ret)) return "empty_body_withheld";
        // A constructor forwarding only to its base has no maintained own body.
        // Withhold these even when genuine; a declaration shell emits the same IL.
        if (method.IsConstructor && !method.IsStatic && method.DeclaringType.BaseType != null &&
            codes.Count(i => i.OpCode.Code == Code.Call) == 1 &&
            codes.All(i => i.OpCode.Code == Code.Call || Trivial(i)) &&
            codes.Single(i => i.OpCode.Code == Code.Call).Operand is MethodReference baseCall &&
            baseCall.Name == ".ctor" && baseCall.DeclaringType.FullName == method.DeclaringType.BaseType.FullName)
            return "empty_body_withheld";
        if (codes.Any(i => i.Operand is MethodReference called && called.DeclaringType.FullName is "System.NotImplementedException" or "System.NotSupportedException"))
            return "exception_placeholder_withheld";
        if (codes.Any(i => i.OpCode.Code is Code.Throw or Code.Rethrow) && !codes.Any(i => i.OpCode.Code == Code.Ret))
            return "throw_only_withheld";
        // Debug IL may store/reload locals or branch to its final ret. No-effect
        // constant/default/argument-forwarding bodies remain withheld even when genuinely authored.
        bool constants = codes.All(Trivial);
        return constants ? "constant_or_default_body_withheld" : "implementation_candidate";
    }
    private static object Player(JsonElement context, string[] scope)
    {
        string root = Text(context, "source_root");
        var rows = new List<Dictionary<string, object?>>(); var modules = new List<object>();
        foreach (var file in context.GetProperty("player_modules").EnumerateArray())
        {
            string dll = Regular(file.GetString()!, 256L * 1024 * 1024), pdb = Regular(Path.ChangeExtension(dll, ".pdb"), 256L * 1024 * 1024);
            var dllSeal = Seal(dll); var pdbSeal = Seal(pdb);
            using var module = ModuleDefinition.ReadModule(dll, new ReaderParameters { ReadSymbols = true, SymbolReaderProvider = new PortablePdbReaderProvider(), ReadingMode = ReadingMode.Deferred, AssemblyResolver = new NoResolver() });
            string assembly = module.Assembly.Name.Name;
            if (!scope.Contains(assembly, StringComparer.Ordinal)) throw new InvalidDataException("Unexpected player assembly.");
            modules.Add(new { assembly, mvid = module.Mvid.ToString(), dll = dllSeal, pdb = pdbSeal });
            foreach (var type in module.Types.SelectMany(Types).Where(t => Selected(context, assembly, t.FullName))) foreach (var method in type.Methods)
            {
                var row = new Dictionary<string, object?> { ["assembly"] = assembly, ["token"] = Token(method.MetadataToken.ToUInt32()),
                    ["declaring_type"] = Name(type.FullName), ["name"] = method.Name, ["signature"] = null, ["signature_error"] = null,
                    ["body_status"] = BodyStatus(method), ["source_status"] = "no_sequence_points", ["source_documents"] = new List<object>(),
                    ["il_sha256"] = method.HasBody ? HashIL(method) : null };
                try { row["signature"] = PlayerSignature(method); }
                catch (Exception error) { row["signature_error"] = error.GetType().Name; }
                var documents = method.DebugInformation.SequencePoints.Where(p => !p.IsHidden).Select(p => p.Document).DistinctBy(d => d.Url).ToArray();
                var sources = (List<object>)row["source_documents"]!;
                if (documents.Length > 0)
                {
                    row["source_status"] = "verified";
                    foreach (var document in documents)
                    {
                        string path = Path.GetFullPath(document.Url);
                        string relative = Path.GetRelativePath(root, path).Replace('\\','/');
                        if (!(relative.StartsWith("Assets/", StringComparison.Ordinal) || relative.StartsWith("Packages/", StringComparison.Ordinal)) ||
                            relative.Split('/').Any(p => p is ".." or "Editor" or "Tests" or "bin" or "obj" or "__pycache__" || p.StartsWith('.')) ||
                            relative.StartsWith("Assets/Recovered/", StringComparison.Ordinal) || relative.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal) ||
                            !relative.EndsWith(".cs", StringComparison.Ordinal))
                        { row["source_status"] = "outside_maintained_runtime_source"; continue; }
                        Regular(path, 4L * 1024 * 1024);
                        byte[] data = File.ReadAllBytes(path);
                        byte[] checksum = document.HashAlgorithm switch { DocumentHashAlgorithm.SHA256 => SHA256.HashData(data), DocumentHashAlgorithm.SHA1 => SHA1.HashData(data),
                            _ => throw new InvalidDataException("Unsupported PDB source checksum algorithm.") };
                        if (!checksum.SequenceEqual(document.Hash)) row["source_status"] = "source_checksum_mismatch";
                        sources.Add(new { relative_path = relative, size = data.Length, sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                            pdb_checksum_algorithm = document.HashAlgorithm.ToString(), pdb_checksum = Convert.ToHexString(document.Hash).ToLowerInvariant() });
                    }
                }
                rows.Add(row);
            }
            if (JsonSerializer.Serialize(Seal(dll)) != JsonSerializer.Serialize(dllSeal) || JsonSerializer.Serialize(Seal(pdb)) != JsonSerializer.Serialize(pdbSeal))
                throw new InvalidDataException("Player module/PDB changed during metadata inspection.");
        }
        return new { kind = "maintained-player-source-body-inventory-v1", modules, methods = rows.OrderBy(r => r["assembly"]).ThenBy(r => r["token"]).ToArray(),
            native_byte_matching_verified = false, gameplay_verified = false };
    }
    private static string HashIL(MethodDefinition method)
    {
        // Cecil's instruction text is sealed together with DLL bytes and MVID;
        // this digest distinguishes bodies, not code equivalence across targets.
        var text = string.Join("\n", method.Body.Instructions.Select(i => i.ToString()));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 || args[0] is not ("native" or "player")) throw new ArgumentException("native|player CONTEXT_JSON FRESH_OUTPUT_JSON");
            string contextPath = Regular(args[1], 4 * 1024 * 1024); byte[] raw = File.ReadAllBytes(contextPath);
            using var document = JsonDocument.Parse(raw); var context = document.RootElement;
            string[] scope = ValidateContext(context, contextPath);
            string output = Path.GetFullPath(args[2]), run = Text(context, "run");
            if (output != args[2]) throw new InvalidDataException("Output must be canonical.");
            if (Path.GetDirectoryName(output) != run || File.Exists(output) || Directory.Exists(output)) throw new InvalidDataException("Reader requires a fresh run-owned output.");
            object inventory = args[0] == "native" ? Native(context, scope) : Player(context, scope);
            if (!raw.SequenceEqual(File.ReadAllBytes(contextPath))) throw new InvalidDataException("Reader context changed.");
            var result = new { schema_version = 1, status = "pending-wrapper-verification", identity_nonce = Text(context, "identity_nonce"),
                source_fingerprint = Text(context, "source_fingerprint"), context_sha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(), inventory,
                reader = new { assembly = typeof(Program).Assembly.FullName, sha256 = Hash(typeof(Program).Assembly.Location),
                    cpp2il_core = new { sha256 = Hash(typeof(Cpp2IlApi).Assembly.Location), mvid = typeof(Cpp2IlApi).Module.ModuleVersionId.ToString() },
                    cecil = new { sha256 = Hash(typeof(ModuleDefinition).Assembly.Location), mvid = typeof(ModuleDefinition).Module.ModuleVersionId.ToString() } },
                recovered_behavior_verified = false, native_byte_matching_verified = false, gameplay_verified = false };
            using var stream = new FileStream(output, FileMode.CreateNew); JsonSerializer.Serialize(stream, result, JsonOptions);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1; }
    }
}
