using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEditor.Compilation;
using UnityEngine;

namespace ProjectLucid.Editor
{
    /// <summary>Genuine player script compilation; output assemblies are read as metadata, never loaded.</summary>
    public static class LucidPlayerCompilation
    {
        [Serializable] private class FileRecord { public string path, sha256; public long size; }
        [Serializable] private sealed class EngineRecord : FileRecord { public string name, assembly_name, mvid; }
        [Serializable] private sealed class InputRecord : FileRecord { public string kind; }
        [Serializable] private class OutputRecord : FileRecord { public string relative_path; }
        [Serializable] private sealed class ModuleRecord : OutputRecord
        {
            public string returned_path, assembly_name, mvid;
            public FileRecord compiler_file;
        }
        [Serializable] private sealed class Diagnostic { public string type, message, file; public int line, column; }
        [Serializable] private sealed class Report
        {
            public int schema_version = 2;
            public string command = "player-code", status = "failed", identity_status = "pending-wrapper", compilation_status = "failed";
            public string unity_version, target, active_target, build_target, build_group = "Standalone", options = "None";
            public int subtarget = 0;
            public string[] extra_scripting_defines = new string[0];
            public string compilation_api = "PlayerBuildInterface.CompilePlayerScripts", identity_nonce, identity_context_sha256;
            public string project_root, work_root, run, source_fingerprint_before, source_fingerprint_after;
            public string inputs_fingerprint_before, inputs_fingerprint_after;
            public EngineRecord[] engine_before, engine_after;
            public PlayerMonoContractSelection.Receipt native_profile_queries_before, native_profile_queries_after;
            public InputRecord[] inputs;
            public ModuleRecord[] modules;
            public OutputRecord[] files;
            public string compiler_output, snapshot_policy = "separate-compiler-output-exact-snapshot-v1";
            public string compilation_cache_policy = "engine-clean-player-cache-v1";
            public LucidCleanPlayerCompilation.Receipt clean_compilation;
            public OutputRecord[] compiler_files;
            public Diagnostic[] diagnostics;
            public string[] returned_assemblies;
            public string error;
            public bool player_schema_verified = false, gameplay_verified = false;
            public string input_plan_description = "Current-target PlayerWithoutTestAssemblies source/precompiled-reference plan; not a compiler response-file or player layout proof.";
        }
        private sealed class Context
        {
            public string Root, Work, Run, Target, Nonce, Source, Path, Digest;
            public byte[] Bytes;
            public EngineRecord[] Engine;
        }
        private static readonly Regex Hex = new Regex("^[0-9a-f]{64}$");
        private static readonly Regex Nonce = new Regex("^[0-9a-f]{32}$");
        private static readonly Regex Mvid = new Regex("^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$");
        private const long MaxFile = 256L * 1024 * 1024;
        private const string Version = "2022.3.54f1";

        public static void Run()
        {
            string root = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName);
            Context context = ReadContext(root, Environment.GetCommandLineArgs());
            string output = Path.Combine(context.Run, "assemblies");
            string compilerOutput = CheckedPath(Path.Combine(context.Work, "player-code", "compiler-runs", context.Nonce));
            string pending = Path.Combine(context.Run, "pending.json");
            if (Directory.Exists(output) || File.Exists(output) || File.Exists(pending) || Directory.Exists(pending)
                || Directory.Exists(compilerOutput) || File.Exists(compilerOutput))
                throw new IOException("Player compilation requires a fresh owned destination.");
            foreach (string entry in Directory.GetFileSystemEntries(context.Run))
                if (Path.GetFileName(entry) != "context.txt" && Path.GetFileName(entry) != "editor.log")
                    throw new IOException("Unexpected files in fresh player compilation run.");
            var diagnostics = new List<Diagnostic>();
            var report = new Report { project_root = root, work_root = context.Work, run = context.Run, target = context.Target,
                unity_version = Application.unityVersion, identity_nonce = context.Nonce, identity_context_sha256 = context.Digest,
                compiler_output = compilerOutput };
            TypeDB typeDb = null;
            Action<string, CompilerMessage[]> handler = (assembly, messages) =>
            {
                foreach (CompilerMessage message in messages)
                {
                    if (diagnostics.Count >= 10000 || (message.message?.Length ?? 0) > 16384)
                        throw new InvalidDataException("Compiler diagnostics exceed supported bounds.");
                    diagnostics.Add(new Diagnostic { type = message.type.ToString(), message = message.message ?? "", file = message.file ?? "",
                        line = message.line, column = message.column });
                }
            };
            try
            {
                if (Application.unityVersion != Version || !Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Player compilation requires the matching stable batch Editor.");
                BuildTarget target = Target(context.Target);
                report.build_target = target.ToString();
                report.active_target = EditorUserBuildSettings.activeBuildTarget.ToString();
                if (EditorUserBuildSettings.activeBuildTarget != target || (int)StandaloneBuildSubtarget.Player != 0)
                    throw new InvalidOperationException("Active player target or installed Player subtarget differs from the requested profile.");
                report.source_fingerprint_before = LucidArtifactIdentity.Fingerprint(root, false);
                if (report.source_fingerprint_before != context.Source) throw new InvalidDataException("Maintained source differs from the player context.");
                report.engine_before = Engine(context);
                CheckEngine(context.Engine, report.engine_before);
                EngineRecord editorCore = context.Engine.Single(e => e.name == "editor_core");
                report.native_profile_queries_before = PlayerMonoContractSelection.Capture(target, editorCore.path, editorCore.sha256, editorCore.mvid);
                report.inputs = Inputs(root);
                report.inputs_fingerprint_before = InputFingerprint(report.inputs);
                CompilationPipeline.assemblyCompilationFinished += handler;
                Directory.CreateDirectory(compilerOutput);
                var settings = new ScriptCompilationSettings { target = target, group = BuildTargetGroup.Standalone,
                    subtarget = (int)StandaloneBuildSubtarget.Player, options = ScriptCompilationOptions.None, extraScriptingDefines = new string[0] };
                // Unity's preliminary Editor compile can consume a queued clean
                // request. Prime the exact installed player backend with its real
                // clean option, then obtain outputs through the public player API.
                report.clean_compilation = LucidCleanPlayerCompilation.Prime(target, compilerOutput,
                    editorCore.path, editorCore.sha256, editorCore.mvid);
                if (report.clean_compilation.status != "completed" ||
                    report.clean_compilation.terminal_status_value != 4 || !report.clean_compilation.output_restored)
                    throw new InvalidOperationException("Clean player compilation failed: " + report.clean_compilation.error);
                ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(settings, compilerOutput);
                report.clean_compilation.public_player_compile_called = true;
                typeDb = result.typeDB;
                if (result.assemblies == null || result.assemblies.Count == 0 || typeDb == null)
                    throw new InvalidOperationException("CompilePlayerScripts returned no complete assembly/type information.");
                report.returned_assemblies = result.assemblies.ToArray();
                if (report.returned_assemblies.Length > 4096 || diagnostics.Any(d => d.type == "Error"))
                    throw new InvalidOperationException("Player compiler failed or returned too many assemblies.");
                // Unity owns its compiler destination. Preserve a separate byte-for-byte
                // snapshot before sealing evidence so a later compile cannot remove it.
                report.compiler_files = OutputFiles(compilerOutput);
                SnapshotOutputs(compilerOutput, output, report.compiler_files);
                report.modules = ReadModules(compilerOutput, output, report.returned_assemblies, context);
                var required = new HashSet<string>(new[] { "Game.Runtime", "HLUnityCore.Runtime", "Unity.Addressables", "Unity.ResourceManager" });
                required.ExceptWith(report.modules.Select(m => m.assembly_name));
                if (required.Count != 0) throw new InvalidDataException("Required player game/package assemblies were not compiled.");
                report.files = OutputFiles(output);
                CheckSnapshot(compilerOutput, output, report.compiler_files, report.files);
                report.inputs_fingerprint_after = InputFingerprint(Inputs(root));
                if (report.inputs_fingerprint_before != report.inputs_fingerprint_after)
                    throw new InvalidDataException("Player source or precompiled references changed during compilation.");
                report.source_fingerprint_after = LucidArtifactIdentity.Fingerprint(root, false);
                if (report.source_fingerprint_after != context.Source) throw new InvalidDataException("Maintained source changed during player compilation.");
                report.native_profile_queries_after = PlayerMonoContractSelection.Capture(target, editorCore.path, editorCore.sha256, editorCore.mvid);
                if (JsonUtility.ToJson(report.native_profile_queries_before) != JsonUtility.ToJson(report.native_profile_queries_after))
                    throw new InvalidDataException("Native runtime/profile query results changed during player compilation.");
                report.engine_after = Engine(context);
                CheckEngine(context.Engine, report.engine_after);
                if (LucidArtifactIdentity.Fingerprint(root, false) != context.Source)
                    throw new InvalidDataException("Maintained source changed during the final Editor identity check.");
                if (!ReadBytes(context.Path, 16384).SequenceEqual(context.Bytes)) throw new InvalidDataException("Player context changed during compilation.");
                CheckSnapshot(compilerOutput, output, report.compiler_files, OutputFiles(output));
                report.status = "pending-identity-verification";
                report.compilation_status = "compiled";
            }
            catch (Exception error)
            {
                report.error = error.GetType().Name + ": " + error.Message;
                throw;
            }
            finally
            {
                CompilationPipeline.assemblyCompilationFinished -= handler;
                typeDb?.Dispose();
                report.diagnostics = diagnostics.ToArray();
                CheckedPath(pending);
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(JsonUtility.ToJson(report, true) + "\n");
            }
            Debug.Log("Project Lucid compiled " + report.modules.Length + " player script assemblies for " + context.Target + ". Metadata/layout/gameplay validation is separate.");
        }

        private static BuildTarget Target(string target)
        {
            if (target == "macos") return BuildTarget.StandaloneOSX;
            if (target == "windows") return BuildTarget.StandaloneWindows64;
            if (target == "linux") return BuildTarget.StandaloneLinux64;
            throw new InvalidDataException("Unsupported player compilation target.");
        }
        private static string Option(string[] args, string name)
        {
            int count = args.Count(a => a == name);
            int index = Array.IndexOf(args, name);
            if (count != 1 || index + 1 >= args.Length) throw new InvalidDataException("Missing or duplicate player option " + name);
            return args[index + 1];
        }
        private static Context ReadContext(string root, string[] args)
        {
            string path = CheckedPath(Option(args, "-lucidPlayerContext"));
            string digest = Option(args, "-lucidPlayerDigest"), nonce = Option(args, "-lucidPlayerNonce");
            if (!Hex.IsMatch(digest) || !Nonce.IsMatch(nonce)) throw new InvalidDataException("Invalid player context digest/nonce.");
            byte[] bytes = ReadBytes(path, 16384);
            if (Hash(bytes) != digest || bytes.Any(b => b > 127)) throw new InvalidDataException("Player context hash or encoding differs.");
            string text = Encoding.ASCII.GetString(bytes);
            if (!text.EndsWith("\n", StringComparison.Ordinal) || text.Contains("\r")) throw new InvalidDataException("Noncanonical player context lines.");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in text.Substring(0, text.Length - 1).Split('\n'))
            {
                int equal = line.IndexOf('=');
                if (equal <= 0 || !values.TryAdd(line.Substring(0, equal), line.Substring(equal + 1)))
                    throw new InvalidDataException("Duplicate or malformed player context field.");
            }
            var expected = new HashSet<string>(new[] { "schema", "kind", "nonce", "project_root_base64", "work_root_base64", "run_base64", "target", "unity_version", "source_fingerprint" });
            foreach (string name in new[] { "editor", "editor_core", "engine_core", "cecil", "serialization" })
                foreach (string field in new[] { "path_base64", "sha256", "size", "mvid", "assembly_name" }) expected.Add(name + "_" + field);
            if (!expected.SetEquals(values.Keys) || values["schema"] != "2" || values["kind"] != "project-lucid-player-code-v2" ||
                values["nonce"] != nonce || values["unity_version"] != Version || !Hex.IsMatch(values["source_fingerprint"]))
                throw new InvalidDataException("Unsupported or incomplete player context.");
            string Decode(string encoded)
            {
                byte[] decoded = Convert.FromBase64String(encoded);
                if (Convert.ToBase64String(decoded) != encoded) throw new InvalidDataException("Noncanonical player path encoding.");
                return new UTF8Encoding(false, true).GetString(decoded);
            }
            string requestedRoot = CheckedPath(Decode(values["project_root_base64"]));
            string work = CheckedPath(Decode(values["work_root_base64"]));
            string run = CheckedPath(Decode(values["run_base64"]));
            string cache = Path.Combine(root, ".cache", "project-lucid");
            if (requestedRoot != root || (work != cache && !work.StartsWith(cache + Path.DirectorySeparatorChar, StringComparison.Ordinal)) ||
                !Directory.Exists(work) || run != Path.Combine(work, "player-code", "runs", nonce) ||
                path != Path.Combine(run, "context.txt") || !Directory.Exists(run))
                throw new InvalidDataException("Player context does not own a fresh cache run.");
            Target(values["target"]);
            var engine = new List<EngineRecord>();
            foreach (string name in new[] { "editor", "editor_core", "engine_core", "cecil", "serialization" })
            {
                long size;
                string sizeText = values[name + "_size"];
                if (!Int64.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out size) || size <= 0 || size.ToString(CultureInfo.InvariantCulture) != sizeText ||
                    !Hex.IsMatch(values[name + "_sha256"]) || (name != "editor" && !Mvid.IsMatch(values[name + "_mvid"])))
                    throw new InvalidDataException("Invalid installed Editor identity component.");
                engine.Add(new EngineRecord { name = name, path = CheckedPath(Decode(values[name + "_path_base64"])), size = size,
                    sha256 = values[name + "_sha256"], mvid = values[name + "_mvid"], assembly_name = values[name + "_assembly_name"] });
            }
            return new Context { Root = root, Work = work, Run = run, Target = values["target"], Nonce = nonce, Source = values["source_fingerprint"], Path = path, Digest = digest, Bytes = bytes, Engine = engine.ToArray() };
        }
        private static string CheckedPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (full != path) throw new InvalidDataException("Noncanonical player evidence path.");
            for (string current = full; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Player evidence path contains a symbolic link.");
            return full;
        }
        private static byte[] ReadBytes(string path, long maximum)
        {
            CheckedPath(path);
            if (!File.Exists(path) || new FileInfo(path).Length <= 0 || new FileInfo(path).Length > maximum)
                throw new InvalidDataException("Player evidence file is absent or oversized.");
            return File.ReadAllBytes(path);
        }
        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static T Record<T>(string path, long maximum = MaxFile) where T : FileRecord, new()
        {
            CheckedPath(path);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > maximum) throw new InvalidDataException("Compilation file is absent or oversized.");
            long size = info.Length;
            string digest;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (new FileInfo(path).Length != size) throw new IOException("Compilation file changed while hashing.");
            return new T { path = path, size = size, sha256 = digest };
        }

        // Only the installed metadata reader library is loaded. Player DLLs are
        // passed to Cecil ReadModule and never to Assembly.Load/LoadFrom.
        private static Tuple<string, string> Metadata(string path, Context context)
        {
            EngineRecord pinned = context.Engine.Single(e => e.name == "cecil");
            if (Record<FileRecord>(pinned.path).sha256 != pinned.sha256) throw new InvalidDataException("Installed Cecil changed.");
            System.Reflection.Assembly reader = System.Reflection.Assembly.LoadFrom(pinned.path);
            if (reader.Location != pinned.path || reader.ManifestModule.ModuleVersionId.ToString("D") != pinned.mvid)
                throw new InvalidDataException("Loaded Cecil differs from the pinned installed module.");
            Type moduleType = reader.GetType("Mono.Cecil.ModuleDefinition", true);
            MethodInfo method = moduleType.GetMethod("ReadModule", new[] { typeof(string) });
            if (method == null) throw new InvalidDataException("Installed Cecil API differs.");
            object module = method.Invoke(null, new object[] { path });
            try
            {
                object assembly = moduleType.GetProperty("Assembly").GetValue(module);
                if (assembly == null) throw new InvalidDataException("Output is not an assembly manifest.");
                object name = assembly.GetType().GetProperty("Name").GetValue(assembly);
                string simple = (string)name.GetType().GetProperty("Name").GetValue(name);
                string mvid = ((Guid)moduleType.GetProperty("Mvid").GetValue(module)).ToString("D");
                if (!Regex.IsMatch(simple, "^[A-Za-z0-9_.-]{1,200}$") || !Mvid.IsMatch(mvid) || mvid == Guid.Empty.ToString("D"))
                    throw new InvalidDataException("Invalid assembly name/MVID.");
                return Tuple.Create(simple, mvid);
            }
            finally { ((IDisposable)module).Dispose(); }
        }
        private static EngineRecord[] Engine(Context context)
        {
            // On macOS this Unity API reports the .app bundle. Bind its actual
            // executable to the separately hashed wrapper identity.
            string editorPath = EditorApplication.applicationPath;
            if (Application.platform == RuntimePlatform.OSXEditor && editorPath.EndsWith(".app", StringComparison.Ordinal))
                editorPath = Path.Combine(editorPath, "Contents", "MacOS", "Unity");
            if (CheckedPath(editorPath) != context.Engine.Single(e => e.name == "editor").path ||
                CheckedPath(typeof(PlayerBuildInterface).Assembly.Location) != context.Engine.Single(e => e.name == "editor_core").path ||
                CheckedPath(typeof(UnityEngine.Object).Assembly.Location) != context.Engine.Single(e => e.name == "engine_core").path)
                throw new InvalidDataException("Actual running Editor/compiler/engine paths differ from the context. " +
                    "Editor=" + EditorApplication.applicationPath + "; compiler=" + typeof(PlayerBuildInterface).Assembly.Location +
                    "; engine=" + typeof(UnityEngine.Object).Assembly.Location);
            var records = new List<EngineRecord>();
            foreach (EngineRecord expected in context.Engine)
            {
                var actual = Record<EngineRecord>(expected.path, expected.name == "editor" ? 2L * 1024 * 1024 * 1024 : MaxFile);
                actual.name = expected.name;
                if (actual.name == "editor") { actual.assembly_name = ""; actual.mvid = ""; }
                else { var identity = Metadata(actual.path, context); actual.assembly_name = identity.Item1; actual.mvid = identity.Item2; }
                records.Add(actual);
            }
            if (typeof(PlayerBuildInterface).Assembly.ManifestModule.ModuleVersionId.ToString("D") != records.Single(e => e.name == "editor_core").mvid ||
                typeof(UnityEngine.Object).Assembly.ManifestModule.ModuleVersionId.ToString("D") != records.Single(e => e.name == "engine_core").mvid)
                throw new InvalidDataException("Loaded Editor/compiler modules differ from the installed metadata.");
            return records.ToArray();
        }
        private static void CheckEngine(EngineRecord[] expected, EngineRecord[] actual)
        {
            if (expected.Length != actual.Length) throw new InvalidDataException("Installed Editor identity is incomplete.");
            for (int i = 0; i < expected.Length; i++)
                if (expected[i].name != actual[i].name || expected[i].path != actual[i].path || expected[i].size != actual[i].size ||
                    expected[i].sha256 != actual[i].sha256 || expected[i].assembly_name != actual[i].assembly_name || expected[i].mvid != actual[i].mvid)
                    throw new InvalidDataException("Installed Editor identity changed.");
        }
        private static InputRecord[] Inputs(string root)
        {
            var records = new Dictionary<string, InputRecord>(StringComparer.Ordinal);
            foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (assembly.defines.Contains("UNITY_EDITOR")) throw new InvalidDataException("Player source plan unexpectedly has UNITY_EDITOR.");
                foreach (var group in new[] { Tuple.Create("source", assembly.sourceFiles), Tuple.Create("precompiled_reference", assembly.compiledAssemblyReferences) })
                    foreach (string supplied in group.Item2)
                    {
                        string path = CheckedPath(Path.GetFullPath(Path.IsPathRooted(supplied) ? supplied : Path.Combine(root, supplied)));
                        InputRecord existing;
                        if (records.TryGetValue(path, out existing))
                        {
                            if (existing.kind != group.Item1) throw new InvalidDataException("Compiler input is ambiguously source and reference.");
                            continue;
                        }
                        var record = Record<InputRecord>(path); record.kind = group.Item1; records.Add(path, record);
                    }
            }
            if (records.Count == 0 || records.Count > 30000 || !records.Values.Any(i => i.kind == "source"))
                throw new InvalidDataException("Player input plan is absent or oversized.");
            return records.Values.OrderBy(r => r.kind + "\0" + r.path, StringComparer.Ordinal).ToArray();
        }
        private static string InputFingerprint(InputRecord[] inputs)
        {
            using (SHA256 sha = SHA256.Create())
            {
                foreach (InputRecord input in inputs.OrderBy(r => r.kind + "\0" + r.path, StringComparer.Ordinal))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(input.kind + "\0" + input.path + "\0" + input.sha256 + "\n");
                    sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        private static string OutputPath(string output, string supplied)
        {
            if (String.IsNullOrEmpty(supplied) || supplied.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
                throw new InvalidDataException("Invalid returned player assembly path.");
            string full = CheckedPath(Path.GetFullPath(Path.IsPathRooted(supplied) ? supplied : Path.Combine(output, supplied)));
            if (!full.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("Returned player assembly is outside the fresh output.");
            return full;
        }
        private static ModuleRecord[] ReadModules(string compilerOutput, string output, string[] returned, Context context)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var records = new List<ModuleRecord>();
            foreach (string supplied in returned)
            {
                string origin = OutputPath(compilerOutput, supplied);
                string relative = origin.Substring(compilerOutput.Length + 1);
                string path = CheckedPath(Path.Combine(output, relative));
                var compilerFile = Record<FileRecord>(origin);
                var record = Record<ModuleRecord>(path);
                var identity = Metadata(path, context);
                var originalIdentity = Metadata(origin, context);
                if (compilerFile.size != record.size || compilerFile.sha256 != record.sha256
                    || identity.Item1 != originalIdentity.Item1 || identity.Item2 != originalIdentity.Item2)
                    throw new InvalidDataException("Player snapshot differs from the genuine returned compiler module.");
                record.compiler_file = compilerFile;
                record.returned_path = supplied; record.relative_path = relative.Replace('\\', '/');
                record.assembly_name = identity.Item1; record.mvid = identity.Item2;
                if (Path.GetFileName(path) != record.assembly_name + ".dll" || !names.Add(record.assembly_name) || !paths.Add(path))
                    throw new InvalidDataException("Duplicate or incorrectly named player assembly output.");
                records.Add(record);
            }
            return records.ToArray();
        }
        private static void SnapshotOutputs(string compilerOutput, string output, OutputRecord[] files)
        {
            CheckedPath(output);
            if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Player snapshot destination is not fresh.");
            Directory.CreateDirectory(output);
            foreach (OutputRecord file in files)
            {
                string origin = OutputPath(compilerOutput, file.relative_path);
                if (origin != file.path) throw new InvalidDataException("Compiler manifest path differs.");
                string destination = OutputPath(output, file.relative_path);
                Directory.CreateDirectory(CheckedPath(Path.GetDirectoryName(destination)));
                using (var source = new FileStream(origin, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[65536];
                    long remaining = file.size;
                    while (remaining != 0)
                    {
                        int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (read == 0) throw new IOException("Compiler output was truncated during snapshot copy.");
                        target.Write(buffer, 0, read);
                        remaining -= read;
                    }
                    if (source.ReadByte() != -1) throw new IOException("Compiler output grew during snapshot copy.");
                    target.Flush(true);
                }
                FileRecord copied = Record<FileRecord>(destination);
                if (copied.sha256 != file.sha256 || copied.size != file.size)
                    throw new IOException("Compiler output changed while creating the player snapshot.");
            }
        }
        private static void CheckSnapshot(string compilerOutput, string output, OutputRecord[] expected, OutputRecord[] snapshot)
        {
            OutputRecord[] current = OutputFiles(compilerOutput);
            if (expected.Length != current.Length || expected.Length != snapshot.Length)
                throw new InvalidDataException("Player snapshot file population changed.");
            for (int i = 0; i < expected.Length; i++)
            {
                OutputRecord e = expected[i], c = current[i], s = snapshot[i];
                if (e.relative_path != c.relative_path || e.relative_path != s.relative_path
                    || e.path != c.path || c.path != OutputPath(compilerOutput, e.relative_path)
                    || s.path != OutputPath(output, e.relative_path)
                    || e.size != c.size || e.size != s.size || e.sha256 != c.sha256 || e.sha256 != s.sha256)
                    throw new InvalidDataException("Player compiler output and immutable snapshot differ.");
            }
        }
        private static OutputRecord[] OutputFiles(string output)
        {
            var records = new List<OutputRecord>();
            var directories = new Stack<string>();
            directories.Push(CheckedPath(output));
            long total = 0;
            int directoryCount = 0;
            while (directories.Count != 0)
            {
                string directory = directories.Pop();
                if (++directoryCount > 8192) throw new InvalidDataException("Player output directory count exceeds supported bounds.");
                foreach (string entry in Directory.GetFileSystemEntries(directory).OrderBy(p => p, StringComparer.Ordinal))
                {
                    string path = CheckedPath(entry);
                    if (Directory.Exists(path)) { directories.Push(path); continue; }
                    var record = Record<OutputRecord>(path); record.relative_path = path.Substring(output.Length + 1).Replace('\\', '/');
                    records.Add(record); total += record.size;
                    if (records.Count > 8192 || total > 8L * 1024 * 1024 * 1024) throw new InvalidDataException("Player output manifest exceeds supported bounds.");
                }
            }
            return records.OrderBy(r => r.relative_path, StringComparer.Ordinal).ToArray();
        }
    }
}
