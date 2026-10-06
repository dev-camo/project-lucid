using Mono.Cecil;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ProjectLucid.ThemeContractMetadata;

internal static class Program
{
    sealed class NoResolver : IAssemblyResolver
    {
        public AssemblyDefinition Resolve(AssemblyNameReference name) => throw new InvalidDataException("Implicit contract dependency resolution is prohibited");
        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);
        public void Dispose() { }
    }
    internal static readonly Regex Digest = new("\\A[a-f0-9]{64}\\z", RegexOptions.CultureInvariant);
    internal static readonly Regex Nonce = new("\\A[a-f0-9]{32}\\z", RegexOptions.CultureInvariant);
    internal static void UniqueJson(JsonElement node)
    {
        if(node.ValueKind==JsonValueKind.Object) {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in node.EnumerateObject()) {
                if(!names.Add(item.Name))throw new InvalidDataException("Duplicate contract context property");
                UniqueJson(item.Value);
            }
        } else if(node.ValueKind==JsonValueKind.Array)foreach(var item in node.EnumerateArray())UniqueJson(item);
    }
    internal static void ExactKeys(JsonElement node, IEnumerable<string> expected)
    {
        if(node.ValueKind!=JsonValueKind.Object||!node.EnumerateObject().Select(p=>p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidDataException("Contract context object shape differs");
    }
    internal static readonly string[] Names = {
      "System.Attribute","System.AttributeTargets","System.AttributeUsageAttribute","System.Boolean",
      "System.ComponentModel.EditorBrowsableAttribute","System.ComponentModel.EditorBrowsableState",
      "System.Diagnostics.DebuggerBrowsableAttribute","System.Diagnostics.DebuggerBrowsableState",
      "System.Enum","System.FlagsAttribute","System.Int32","System.IntPtr","System.Object",
      "System.Reflection.DefaultMemberAttribute","System.Runtime.CompilerServices.CompilerGeneratedAttribute",
      "System.Runtime.CompilerServices.IsReadOnlyAttribute","System.Single","System.String","System.ValueType","System.Void" };
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string Hex(int value) => "0x"+value.ToString("x8",CultureInfo.InvariantCulture);
    internal static void PathCheck(string path)
    {
        if(string.IsNullOrEmpty(path)||path.Length>4096||!Path.IsPathFullyQualified(path)||Path.GetFullPath(path)!=path)throw new InvalidDataException("Noncanonical contract path");
        for(string? p=path;p!=null;p=Path.GetDirectoryName(p))
            if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Symbolic link in contract path");
    }
    internal static byte[] Read(string path,long limit=256L*1024*1024)
    {
        PathCheck(path);var file=new FileInfo(path);
        if(!file.Exists||file.Length<=0||file.Length>limit)throw new InvalidDataException("Missing or unbounded contract input");
        return File.ReadAllBytes(path);
    }
    static object Reference(TypeReference? value) => value==null?new{}:(object)new {
        full_name=value.FullName,scope=value.Scope?.ToString(),token=Hex(value.MetadataToken.ToInt32())};
    static object? Constant(object? value) => value switch {
        float f=>new{primitive="Single",ieee754_little_endian_hex=Convert.ToHexString(BitConverter.GetBytes(f)).ToLowerInvariant()},
        double d=>new{primitive="Double",ieee754_little_endian_hex=Convert.ToHexString(BitConverter.GetBytes(d)).ToLowerInvariant()},_=>value};
    static object Definition(TypeDefinition t) => new {
        name=t.FullName,token=Hex(t.MetadataToken.ToInt32()),attributes=(int)t.Attributes,base_type=Reference(t.BaseType),is_enum=t.IsEnum,
        fields=t.Fields.Select(f=>new{name=f.Name,token=Hex(f.MetadataToken.ToInt32()),attributes=(int)f.Attributes,
            type=Reference(f.FieldType),has_constant=f.HasConstant,constant=f.HasConstant?Constant(f.Constant):null,
            custom_attributes=f.CustomAttributes.Select(a=>new{name=a.AttributeType.FullName,scope=a.AttributeType.Scope?.ToString(),
                blob_hex=Convert.ToHexString(a.GetBlob()).ToLowerInvariant()}).ToArray()}).ToArray(),
        custom_attributes=t.CustomAttributes.Select(a=>new{name=a.AttributeType.FullName,scope=a.AttributeType.Scope?.ToString(),
            blob_hex=Convert.ToHexString(a.GetBlob()).ToLowerInvariant()}).ToArray()};
    private static int Main(string[] args)
    {
        try {
            bool attestOnly=args.Length==2&&args[0]=="--attest-loaded";
            if(args.Length!=2||!BitConverter.IsLittleEndian)throw new InvalidDataException("Expected manifest and fresh pending output");
            string manifestPath=attestOnly?args[1]:args[0];
            byte[] raw=Read(manifestPath,4*1024*1024);
            using(var context=JsonDocument.Parse(raw,new JsonDocumentOptions{MaxDepth=96}))UniqueJson(context.RootElement);
            var request=JsonSerializer.Deserialize<Request>(raw,new JsonSerializerOptions{
                UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=96})??throw new InvalidDataException("Contract context absent");
            request.Check(manifestPath,attestOnly?Path.Combine(request.run,"pending.json"):args[1],fresh:!attestOnly);
            var reader=typeof(ModuleDefinition).Assembly;
            Seal readerSeal=request.engine.Single(r=>r.name=="cecil");
            if(Hash(Read(reader.Location))!=readerSeal.sha256||reader.ManifestModule.ModuleVersionId.ToString()!=readerSeal.mvid)
                throw new InvalidDataException("Loaded reader differs from installed engine");
            using var declarations=new LoadedDeclarations(request);
            object loadedDeclarations=declarations.Generate();
            if(attestOnly){
                if(Hash(Read(manifestPath,4*1024*1024))!=Hash(raw)||Hash(Read(reader.Location))!=readerSeal.sha256)
                    throw new InvalidDataException("Loaded declaration context or reader changed");
                Console.WriteLine(JsonSerializer.Serialize(loadedDeclarations));return 0;
            }
            var rows=new List<object>();
            // Read each assembly separately. Same-name reference/facade modules
            // are intentionally distinct; no resolver or player CLR load is used.
            foreach(var seal in request.modules) {
                seal.Check();byte[] bytes=Read(seal.path);
                using var assembly=AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),new ReaderParameters{ReadingMode=ReadingMode.Deferred,InMemory=true,AssemblyResolver=new NoResolver(),ReadSymbols=false});
                if(assembly.Name.FullName!=seal.assembly_identity||assembly.MainModule.Mvid.ToString()!=seal.mvid)
                    throw new InvalidDataException("Contract assembly identity differs from context");
                var defined=Names.Select(n=>assembly.MainModule.GetType(n)).Where(t=>t!=null).Select(t=>Definition(t!)).ToArray();
                var forwarded=assembly.MainModule.ExportedTypes.Where(t=>Names.Contains(t.FullName,StringComparer.Ordinal)).Select(t=>new {
                    name=t.FullName,token=Hex(t.MetadataToken.ToInt32()),attributes=(int)t.Attributes,is_forwarder=t.IsForwarder,
                    scope=t.Scope.ToString(),declaring_type=t.DeclaringType?.FullName}).ToArray();
                rows.Add(new{path=seal.path,sha256=seal.sha256,size=bytes.Length,mvid=assembly.MainModule.Mvid.ToString(),
                    assembly=assembly.Name.FullName,role=seal.kind,reference_scopes=assembly.MainModule.AssemblyReferences.Select(r=>r.FullName).ToArray(),
                    defined_types=defined,forwarders=forwarded});
                seal.Check();
            }
            request.Check(args[0],args[1]);
            if(Hash(Read(args[0],4*1024*1024))!=Hash(raw)||Hash(Read(reader.Location))!=readerSeal.sha256)
                throw new InvalidDataException("Contract context or reader changed");
            var result=new {schema_version=1,command="runtime-contracts",profile="zone-theme-v1",status="pending-native-verification",
              identity_status="pending-wrapper",target=request.target,nonce=request.nonce,source_fingerprint=request.source_fingerprint,
              project_root=request.project_root,work_root=request.work_root,run=request.run,manifest_sha256=Hash(raw),
              player_receipt_sha256=request.player_receipt_sha256,player_schema_sha256=request.player_schema_sha256,
              original_schema_sha256=request.original_schema_sha256,loaded_inventory_sha256=request.loaded_inventory_sha256,
              reader_path=reader.Location,reader_sha256=Hash(Read(reader.Location)),reader_mvid=reader.ManifestModule.ModuleVersionId.ToString(),
              modules=rows,loaded_declarations=loadedDeclarations,player_clr_loads=false,editor_invoked=false,runtime_verified=false,layout_approved=false,
              runtime_selection_verified=false,player_schema_verified=false,remap_approved=false,gameplay_verified=false,
              references_modified=false,compiler_response_files_verified=false,errors=Array.Empty<string>(),
              resolver_provenance="metadata-resolution-over-sealed-plan-candidates",implicit_dependency_resolution=false};
            using(var stream=new FileStream(args[1],FileMode.CreateNew,FileAccess.Write,FileShare.None))
                JsonSerializer.Serialize(stream,result,new JsonSerializerOptions{WriteIndented=true,MaxDepth=96});
            Console.WriteLine("Exact target contract metadata generated; runtime and binding approval remain separate.");return 0;
        } catch(Exception e) {Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
}

internal sealed class Seal
{
    public string path{get;set;}="";public string sha256{get;set;}="";public long size{get;set;}
    public string assembly_name{get;set;}="";public string assembly_identity{get;set;}="";
    public string mvid{get;set;}="";public string name{get;set;}="";public string kind{get;set;}="";
    public void Check() {
        if(!Program.Digest.IsMatch(sha256)||size<=0||size>(name=="editor"?2L*1024*1024*1024:256L*1024*1024))
            throw new InvalidDataException("Invalid bounded contract seal");
        if(name!="editor"&&(!Guid.TryParseExact(mvid,"D",out var id)||id==Guid.Empty||id.ToString()!=mvid||
            !Regex.IsMatch(assembly_name,"\\A[A-Za-z0-9_.-]{1,200}\\z",RegexOptions.CultureInvariant)))
            throw new InvalidDataException("Invalid contract module MVID/name");
        if(name=="editor"&&(mvid!=""||assembly_name!=""))throw new InvalidDataException("Native Editor cannot claim a managed identity");
        byte[] bytes=Program.Read(path,name=="editor"?2L*1024*1024*1024:256L*1024*1024);
        if(bytes.LongLength!=size||Program.Hash(bytes)!=sha256)throw new InvalidDataException("Sealed contract bytes changed");
    }
}
internal sealed class Request
{
    public int schema_version{get;set;}public string command{get;set;}="";public string profile{get;set;}="";
    public string target{get;set;}="";public string nonce{get;set;}="";public string source_fingerprint{get;set;}="";
    public string project_root{get;set;}="";public string work_root{get;set;}="";public string run{get;set;}="";
    public string player_receipt_sha256{get;set;}="";public string player_schema_sha256{get;set;}="";
    public string original_schema_sha256{get;set;}="";public string loaded_inventory_sha256{get;set;}="";
    public string compiler_nonce{get;set;}="";public JsonElement native_query{get;set;}
    public List<Seal> modules{get;set;}=new();public List<Seal> engine{get;set;}=new();
    public List<Seal> loaded_modules{get;set;}=new();public JsonElement loaded_types{get;set;}
    public void Check(string manifest,string output,bool fresh=true)
    {
        foreach(string path in new[]{project_root,work_root,run,manifest,output})Program.PathCheck(path);
        if(schema_version!=1||command!="runtime-contracts"||profile!="zone-theme-v1"||!new[]{"macos","windows","linux"}.Contains(target)||
           !Program.Nonce.IsMatch(nonce)||!Program.Nonce.IsMatch(compiler_nonce)||new[]{source_fingerprint,player_receipt_sha256,player_schema_sha256,original_schema_sha256,loaded_inventory_sha256}.Any(s=>!Program.Digest.IsMatch(s))||
           run!=Path.Combine(work_root,"runtime-contracts","runs",nonce)||manifest!=Path.Combine(run,"manifest.json")||output!=Path.Combine(run,"pending.json"))
            throw new InvalidDataException("Incomplete or unowned contract context");
        foreach(string forbidden in new[]{"Assets","Packages","ProjectSettings","tools","input"}) {
            string path=Path.Combine(project_root,forbidden);
            if(run==path||run.StartsWith(path+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidDataException("Contract output in maintained source or input");
        }
        if((fresh&&(File.Exists(output)||Directory.Exists(output)))||modules.Count!=7||engine.Count!=5||modules.Select(s=>s.path).Distinct(StringComparer.Ordinal).Count()!=7||
           !modules.Select(s=>s.kind).OrderBy(s=>s,StringComparer.Ordinal).SequenceEqual(new[]{"compiler_reference","runtime_facade","runtime_mscorlib","runtime_system","returned_player_assembly","returned_player_assembly","returned_player_assembly"}.OrderBy(s=>s,StringComparer.Ordinal)))
            throw new InvalidDataException("Fresh destination and exactly seven owned module roles required");
        if(!engine.Select(s=>s.name).ToHashSet(StringComparer.Ordinal).SetEquals(new[]{"editor","editor_core","engine_core","cecil","serialization"})||engine.Select(s=>s.path).Distinct(StringComparer.Ordinal).Count()!=5)
            throw new InvalidDataException("Exact installed engine seals required");
        foreach(var seal in modules)seal.Check();foreach(var seal in engine)seal.Check();
        string suffix=target=="macos"?"macos":target=="windows"?"win32":"linux";
        Seal core=engine.Single(s=>s.name=="editor_core");
        if(core.sha256!="fb89c4764167359e3796b9a7e893f507d508f86eb8ad7daa55c78bf07abcd0b9"||core.mvid!="70a991b8-3ee9-4c31-8ecd-89fa65e3f64c"||core.assembly_name!="UnityEditor.CoreModule")
            throw new InvalidDataException("Unreviewed native query engine module");
        string ending=Path.Combine("Managed","UnityEngine","UnityEditor.CoreModule.dll");
        if(!core.path.EndsWith(Path.DirectorySeparatorChar+ending,StringComparison.Ordinal))throw new InvalidDataException("Native engine location unsupported");
        string contents=core.path[..^(ending.Length+1)];
        string[] enginePaths={Path.Combine(contents,"MacOS","Unity"),core.path,Path.Combine(contents,"Managed","UnityEngine","UnityEngine.CoreModule.dll"),Path.Combine(contents,"Managed","Unity.Cecil.dll"),Path.Combine(contents,"Managed","Unity.SerializationLogic.dll")};
        string[] engineNames={"editor","editor_core","engine_core","cecil","serialization"};
        for(int i=0;i<engineNames.Length;i++)if(engine.Single(s=>s.name==engineNames[i]).path!=enginePaths[i])
            throw new InvalidDataException("Installed engine module path differs");
        string runtime=Path.Combine(contents,"MonoBleedingEdge","lib","mono","unityjit-"+suffix);
        Program.ExactKeys(native_query,new[]{"unity_version","target","module_path","module_sha256","module_mvid","module_assembly","api_compatibility_value","api_compatibility_name","scripting_backend_value","scripting_backend_name","platform_profile_suffix","compatibility_profile_folder","mono_runtime_lib_directory","runtime_selection_verified","runtime_assemblies_loaded","build_player_called","layout_approved","gameplay_verified","methods"});
        string nativeTarget=target=="macos"?"StandaloneOSX":target=="windows"?"StandaloneWindows64":"StandaloneLinux64";
        if(native_query.ValueKind!=JsonValueKind.Object||native_query.GetProperty("module_path").GetString()!=core.path||
           native_query.GetProperty("unity_version").GetString()!="2022.3.54f1"||native_query.GetProperty("target").GetString()!=nativeTarget||
           native_query.GetProperty("module_assembly").GetString()!="UnityEditor.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"||
           native_query.GetProperty("module_sha256").GetString()!=core.sha256||native_query.GetProperty("module_mvid").GetString()!=core.mvid||
           native_query.GetProperty("scripting_backend_value").GetInt32()!=0||native_query.GetProperty("scripting_backend_name").GetString()!="Mono2x"||
           native_query.GetProperty("api_compatibility_value").GetInt32()!=6||native_query.GetProperty("api_compatibility_name").GetString()!="NET_Standard_2_0"||
           native_query.GetProperty("platform_profile_suffix").GetString()!=suffix||native_query.GetProperty("compatibility_profile_folder").GetString()!="unityaot-"+suffix||
           native_query.GetProperty("mono_runtime_lib_directory").GetString()!=runtime)
            throw new InvalidDataException("Contract context differs from genuine native target query");
        foreach(string flag in new[]{"runtime_selection_verified","runtime_assemblies_loaded","build_player_called","layout_approved","gameplay_verified"})
            if(native_query.GetProperty(flag).ValueKind!=JsonValueKind.False)throw new InvalidDataException("Native query approval claims prohibited");
        var methods=native_query.GetProperty("methods");
        if(methods.ValueKind!=JsonValueKind.Array||methods.GetArrayLength()!=3)throw new InvalidDataException("Exact three native methods required");
        string[] owners={"UnityEditor.BuildPipeline","UnityEditor.BuildPipeline","UnityEditor.BuildTargetDiscovery"};
        string[] methodNames={"GetMonoRuntimeLibDirectory","CompatibilityProfileToClassLibFolder","GetPlatformProfileSuffix"};
        string[] tokens={"0x06002264","0x06002265","0x0600237b"};
        for(int i=0;i<3;i++) {
            var method=methods[i];Program.ExactKeys(method,new[]{"declaring_type","method","token","attributes","implementation_attributes","parameter_type","return_type"});
            if(method.GetProperty("declaring_type").GetString()!=owners[i]||method.GetProperty("method").GetString()!=methodNames[i]||method.GetProperty("token").GetString()!=tokens[i]||method.GetProperty("attributes").GetInt32()!=(i==2?150:147)||method.GetProperty("implementation_attributes").GetInt32()!=4096||method.GetProperty("return_type").GetString()!="System.String"||method.GetProperty("parameter_type").GetString()!=(i==1?"UnityEditor.ApiCompatibilityLevel":"UnityEditor.BuildTarget"))
                throw new InvalidDataException("Native method declaration differs");
        }
        foreach(var role in new[]{("compiler_reference",Path.Combine(contents,"NetStandard","ref","2.1.0","netstandard.dll")),
          ("runtime_facade",Path.Combine(runtime,"Facades","netstandard.dll")),("runtime_mscorlib",Path.Combine(runtime,"mscorlib.dll")),("runtime_system",Path.Combine(runtime,"System.dll"))})
            if(modules.Single(s=>s.kind==role.Item1).path!=role.Item2)throw new InvalidDataException("Native-selected contract path differs");
        var returned=modules.Where(s=>s.kind=="returned_player_assembly").ToArray();
        if(!returned.Select(s=>s.assembly_name).OrderBy(s=>s,StringComparer.Ordinal).SequenceEqual(new[]{"Game.Runtime","HLUnityCore.Runtime","Unity.Addressables"}.OrderBy(s=>s,StringComparer.Ordinal)))
            throw new InvalidDataException("Returned player modules differ");
        foreach(var s in returned)
            if(s.path!=Path.Combine(work_root,"player-code","runs",compiler_nonce,"assemblies",s.assembly_name+".dll"))
                throw new InvalidDataException("Returned module is outside genuine compiler output");
    }
}
