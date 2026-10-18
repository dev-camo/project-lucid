using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ProjectLucid.Editor
{
    /// <summary>Read-only reference inventory; successful import does not prove gameplay.</summary>
    public static class LucidReferenceAudit
    {
        [Serializable] private sealed class MissingReference
        {
            public string guid;
            public string[] examples;
        }
        [Serializable] private sealed class UnresolvedObjectReference
        {
            public string target;
            public string reason;
            public string[] examples;
        }
        [Serializable] private sealed class Audit
        {
            public string status;
            public string unity_version;
            public string source_fingerprint;
            public string prepared_asset_fingerprint;
            public string reference_status;
            public string identity_mode;
            public string identity_status;
            public string identity_nonce;
            public string identity_context_sha256;
            public int scanned_assets;
            public int unresolved_references;
            public MissingReference[] missing_guids;
            public UnresolvedObjectReference[] unresolved_object_references;
            public string[] invalid_script_bindings;
            public string[] shader_errors;
            public string limitation = "Reference resolution does not verify implemented behavior, authored flow, or presentation parity. " +
                "Non-script object pointers are checked by GUID and local identifier only: their type field and the local identifiers of builtin resources are not checked.";
        }

        private static readonly Regex GuidReference = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex ScriptReference = new Regex(@"^\s*m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*(\d+)\}\s*$", RegexOptions.Compiled);
        // Unity's YAML PPtr: {fileID: N} names a document in the same file;
        // {fileID: N, guid: G, type: T} names a loaded object of another asset.
        // Every flow mapping with a fileID key is a candidate, whatever its key
        // order, spacing or key quoting, so a malformed pointer cannot pass unseen.
        private static readonly Regex PointerKey = new Regex(@"(?:\A\{|,)\s*(?:fileID|'fileID'|""fileID"")\s*:", RegexOptions.Compiled);
        private static readonly Regex PointerEntry = new Regex(@"\A\s*(fileID|guid|type)\s*:\s*([^\s:]+)\s*\z", RegexOptions.Compiled);
        private static readonly Regex SignedInteger = new Regex(@"\A-?\d+\z", RegexOptions.Compiled);
        private static readonly Regex HexGuid = new Regex(@"\A[0-9a-fA-F]{32}\z", RegexOptions.Compiled);
        // Unity's emitter writes one field per line: indentation, any "- " sequence
        // indicators, an optional "key: ", then the value. The match ends where the
        // value starts.
        private static readonly Regex FieldPrefix = new Regex(@"\A\s*(?:-(?:\s+|\z))*(?:[^\s'""{\[#][^:]*?:(?:\s+|\z))?", RegexOptions.Compiled);
        private static readonly Regex NullScriptReference = new Regex(@"^\s*m_Script:\s*\{fileID:\s*0\}\s*$", RegexOptions.Compiled);
        private static readonly HashSet<string> SerializedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".overridecontroller", ".playable",
            ".mixer", ".anim", ".spriteatlas", ".spriteatlasv2", ".rendertexture", ".lighting"
        };

        public static void Run()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string[] arguments = Environment.GetCommandLineArgs();
            string output = ReadOption(arguments, "-lucidAuditOutput") ??
                Path.Combine(root, ".cache", "project-lucid", "reports", "unity-reference-audit.json");
            output = CheckedCachePath(root, output);
            IdentityContext context = ReadIdentityContext(root, arguments, output);
            string sourceBefore = LucidArtifactIdentity.Fingerprint(root, false);
            if (context != null)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Reference audit requires a stable imported project.");
                context.CheckSourceFingerprint(sourceBefore);
            }
            string recovered = Path.Combine(Application.dataPath, "Recovered");
            if (!Directory.Exists(recovered)) throw new InvalidOperationException("Prepare supplied assets before auditing references.");
            var scan = new ReferenceScan();
            var shaderErrors = new List<string>();
            foreach (string file in Directory.EnumerateFiles(recovered, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!SerializedExtensions.Contains(Path.GetExtension(file))) continue;
                scan.ScanFile(file, "Assets/" + file.Substring(Application.dataPath.Length + 1).Replace('\\', '/'));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) { shaderErrors.Add(path + ": shader did not import"); continue; }
                foreach (var message in ShaderUtil.GetShaderMessages(shader))
                    if (message.severity.ToString() == "Error") shaderErrors.Add(path + ": " + message.message);
            }
            string sourceFingerprint = LucidArtifactIdentity.Fingerprint(root, false);
            if (sourceFingerprint != sourceBefore) throw new InvalidOperationException("Maintained source changed during reference audit.");
            if (context != null)
            {
                context.CheckSourceFingerprint(sourceFingerprint);
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Project import changed during reference audit.");
                context.CheckUnchanged(root);
            }
            string referenceStatus = scan.Status(shaderErrors.Count);
            var audit = new Audit
            {
                // Supplied hashes remain non-authoritative until the CLI checks
                // all current bytes again after this Editor process exits.
                status = context == null ? referenceStatus : "pending-identity-verification",
                unity_version = Application.unityVersion,
                source_fingerprint = sourceFingerprint,
                prepared_asset_fingerprint = context == null ? LucidArtifactIdentity.Fingerprint(root, true) : context.PreparedFingerprint,
                reference_status = referenceStatus,
                identity_mode = context == null ? "editor-content-sha256-v1" : "wrapper-prepost-v1",
                identity_status = context == null ? "complete" : "pending-wrapper",
                identity_nonce = context == null ? null : context.Nonce,
                identity_context_sha256 = context == null ? null : context.Digest,
                scanned_assets = scan.Scanned,
                unresolved_references = scan.Unresolved(shaderErrors.Count),
                missing_guids = scan.Missing.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MissingReference { guid = pair.Key, examples = pair.Value.OrderBy(s => s, StringComparer.Ordinal).ToArray() }).ToArray(),
                unresolved_object_references = scan.ObjectReferences().Select(entry => new UnresolvedObjectReference
                    { target = entry.Target, reason = entry.Reason, examples = entry.Examples }).ToArray(),
                invalid_script_bindings = scan.InvalidScriptBindings(),
                shader_errors = shaderErrors.ToArray()
            };
            string directory = Path.GetDirectoryName(output);
            EnsureNoSymlinks(root, directory);
            Directory.CreateDirectory(directory);
            EnsureNoSymlinks(root, output);
            string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonUtility.ToJson(audit, true) + "\n");
                if (File.Exists(output)) File.Replace(temporary, output, null);
                else File.Move(temporary, output);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Debug.Log("Project Lucid reference audit: " + scan.Scanned + " assets, " + audit.unresolved_references + " unresolved references. " + output);
        }

        // Reference state for the serialized YAML files of one audit.
        private sealed class ReferenceScan
        {
            private const int ExampleLimit = 8, ExampleLength = 256, LoadsPerUnload = 256, ContinuationLimit = 4;
            private static readonly Regex ObjectHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
            private readonly Dictionary<string, string> resolved = new Dictionary<string, string>();
            private readonly Dictionary<string, string> scriptResults = new Dictionary<string, string>();
            private readonly HashSet<string> badScripts = new HashSet<string>();
            // Local identifiers loaded per external GUID, with their multiplicity;
            // null means the target path enumerated no loadable objects at all.
            private readonly Dictionary<string, Dictionary<long, int>> loadedIdentifiers = new Dictionary<string, Dictionary<long, int>>();
            private readonly SortedDictionary<string, ObjectReferenceEntry> badObjects = new SortedDictionary<string, ObjectReferenceEntry>(StringComparer.Ordinal);
            private int loadsSinceUnload;
            public readonly Dictionary<string, HashSet<string>> Missing = new Dictionary<string, HashSet<string>>();
            public int Scanned;

            public void ScanFile(string file, string relative)
            {
                using (var reader = new StreamReader(file))
                {
                    string line = reader.ReadLine();
                    if (line == null || !line.StartsWith("%YAML", StringComparison.Ordinal)) return;
                    Scanned++;
                    string currentObject = null;
                    // Stripped documents are anchors too. A local pointer may name a
                    // later document, so local pointers are checked after the file.
                    // Anchors keep their multiplicity: a repeated one names no unique document.
                    var anchors = new Dictionary<long, int>();
                    var locals = new List<KeyValuePair<long, string>>();
                    // Quote of a quoted scalar that continues onto the next line, or '\0'.
                    char quote = '\0';
                    string pending = reader.ReadLine();
                    while ((line = pending) != null)
                    {
                        pending = reader.ReadLine();
                        Match header = line.StartsWith("--- ", StringComparison.Ordinal) ? ObjectHeader.Match(line) : Match.Empty;
                        if (header.Success)
                        {
                            quote = '\0';
                            currentObject = "!u!" + header.Groups[1].Value + " &" + header.Groups[2].Value;
                            if (long.TryParse(header.Groups[2].Value, out long anchor))
                                anchors[anchor] = anchors.TryGetValue(anchor, out int seen) ? seen + 1 : 1;
                            continue;
                        }
                        int value = FlowValueStart(line, ref quote);
                        // Unity's YAML emitter wraps a long flow mapping onto indented
                        // continuation lines when it reserializes an asset, for example
                        // "{fileID: N, guid: G," then "  type: 3}". Join an open mapping
                        // with them so it is checked whole; one still open after the
                        // bound is reported as malformed.
                        for (int joined = 0; joined < ContinuationLimit && value >= 0 && IsOpen(line, value) && pending != null &&
                            pending.Length > 0 && (pending[0] == ' ' || pending[0] == '\t'); joined++)
                        {
                            line = line.TrimEnd() + " " + pending.TrimStart();
                            pending = reader.ReadLine();
                        }
                        bool scriptLine = line.TrimStart().StartsWith("m_Script:", StringComparison.Ordinal);
                        if (scriptLine)
                        {
                            string error;
                            if (!scriptResults.TryGetValue(line, out error))
                            {
                                error = GetScriptBindingError(line);
                                scriptResults.Add(line, error);
                            }
                            // Null pointers are identical text, so each one names its
                            // object to give one diagnostic per affected document.
                            if (error != null)
                                badScripts.Add(relative + (NullScriptReference.IsMatch(line) && currentObject != null ? " " + currentObject : "") + ": " + error);
                        }
                        // m_Script keeps its stricter MonoScript class check above.
                        else if (value >= 0 && line.IndexOf("fileID", value, StringComparison.Ordinal) >= 0)
                        {
                            string example = Example(relative, currentObject, line);
                            foreach (string candidate in PointerCandidates(line.Substring(value)))
                            {
                                if (!TryParsePointer(candidate, out string localID, out string guid, out string type))
                                    AddObject(relative + " " + candidate, "Unsupported or malformed object pointer", example);
                                else if (!long.TryParse(localID, out long fileID))
                                    AddObject(relative + " " + candidate, "Local identifier is not a signed 64-bit value", example);
                                else if (guid == null)
                                {
                                    if (fileID != 0) locals.Add(new KeyValuePair<long, string>(fileID, example));
                                }
                                else CheckExternal(guid.ToLowerInvariant(), fileID, type, example);
                            }
                        }
                        if (line.IndexOf("guid:", StringComparison.Ordinal) < 0) continue;
                        foreach (Match match in GuidReference.Matches(line))
                        {
                            string guid = match.Groups[1].Value.ToLowerInvariant();
                            if (IsBuiltinGuid(guid)) continue;
                            if (String.IsNullOrEmpty(Resolve(guid)))
                            {
                                HashSet<string> examples;
                                if (!Missing.TryGetValue(guid, out examples)) Missing.Add(guid, examples = new HashSet<string>());
                                if (examples.Count < ExampleLimit) examples.Add(relative);
                            }
                        }
                    }
                    foreach (var local in locals)
                    {
                        if (!anchors.TryGetValue(local.Key, out int count))
                            AddObject(relative + " {fileID: " + local.Key + "}", "Local identifier names no document in this file", local.Value);
                        else if (count != 1)
                            AddObject(relative + " {fileID: " + local.Key + "}", "Local identifier names more than one document in this file", local.Value);
                    }
                }
            }

            // Start of the line's value when it is a flow collection, or -1. A quoted
            // value is string text, not a PPtr, and may continue onto later lines;
            // quote carries that state between lines.
            private static int FlowValueStart(string line, ref char quote)
            {
                int start = 0;
                if (quote == '\0')
                {
                    start = FieldPrefix.Match(line).Length;
                    if (start >= line.Length) return -1;
                    if (line[start] == '{' || line[start] == '[') return start;
                    if (line[start] != '\'' && line[start] != '"') return -1;
                    quote = line[start++];
                }
                for (int i = start; i < line.Length; i++)
                {
                    // Single-quoted text escapes ' as ''; double-quoted text uses backslashes.
                    if (quote == '"' && line[i] == '\\') { i++; continue; }
                    if (line[i] != quote) continue;
                    if (quote == '\'' && i + 1 < line.Length && line[i + 1] == '\'') { i++; continue; }
                    quote = '\0';
                    break;
                }
                return -1;
            }

            private static bool IsOpen(string line, int value)
            {
                bool[] quoted = QuotedText(line, value);
                int depth = 0;
                for (int i = value; i < line.Length; i++)
                {
                    if (quoted[i]) continue;
                    if (line[i] == '{') depth++;
                    else if (line[i] == '}' && depth > 0) depth--;
                }
                return depth > 0;
            }

            // Marks the quoted scalars of a flow collection that starts at start,
            // delimiters included, so their braces and commas are string text. A quote
            // opens a scalar only where one may begin: after '{', '[', ',' or ':' and
            // any spaces, so an apostrophe inside a plain scalar opens nothing.
            // Single-quoted text escapes ' as ''; double-quoted text uses backslashes.
            // An unclosed scalar runs to the end of the text.
            private static bool[] QuotedText(string text, int start)
            {
                var quoted = new bool[text.Length];
                char previous = '{';
                for (int i = start; i < text.Length; i++)
                {
                    char c = text[i];
                    if ((c == '\'' || c == '"') && (previous == '{' || previous == '[' || previous == ',' || previous == ':'))
                    {
                        int end = i + 1;
                        for (; end < text.Length; end++)
                        {
                            if (c == '"' && text[end] == '\\') { end++; continue; }
                            if (text[end] != c) continue;
                            if (c == '\'' && end + 1 < text.Length && text[end + 1] == '\'') { end++; continue; }
                            break;
                        }
                        end = Math.Min(end, text.Length - 1);
                        for (int j = i; j <= end; j++) quoted[j] = true;
                        i = end;
                        previous = c;
                        continue;
                    }
                    if (c != ' ' && c != '\t') previous = c;
                }
                return quoted;
            }

            // Each flow mapping in value that has its own fileID key, in text order.
            // An unclosed one runs to the end of the value.
            private static IEnumerable<string> PointerCandidates(string value)
            {
                bool[] quoted = QuotedText(value, 0);
                var open = new Stack<int>();
                var spans = new List<KeyValuePair<int, int>>();
                for (int i = 0; i < value.Length; i++)
                {
                    if (quoted[i]) continue;
                    if (value[i] == '{') open.Push(i);
                    else if (value[i] == '}' && open.Count > 0) spans.Add(new KeyValuePair<int, int>(open.Pop(), i + 1));
                }
                while (open.Count > 0) spans.Add(new KeyValuePair<int, int>(open.Pop(), value.Length));
                spans.Sort((left, right) => left.Key.CompareTo(right.Key));
                foreach (var span in spans)
                {
                    // Keys of nested mappings belong to those mappings. Quoted text
                    // stays in, so pointer-like text there fails closed.
                    var own = new StringBuilder();
                    for (int i = span.Key; i < span.Value; i++)
                    {
                        var nested = spans.FirstOrDefault(other => other.Key == i && other.Key != span.Key);
                        if (nested.Value > 0) { own.Append("{}"); i = nested.Value - 1; }
                        else own.Append(value[i]);
                    }
                    if (PointerKey.IsMatch(own.ToString())) yield return value.Substring(span.Key, span.Value - span.Key);
                }
            }

            // {fileID: N} or {fileID: N, guid: G, type: T}, each key once in any
            // order; a nested, unclosed or extended mapping is not a pointer.
            private static bool TryParsePointer(string candidate, out string fileID, out string guid, out string type)
            {
                fileID = guid = type = null;
                if (candidate.Length < 2 || candidate[candidate.Length - 1] != '}' || candidate.IndexOf('{', 1) >= 0) return false;
                foreach (string entry in candidate.Substring(1, candidate.Length - 2).Split(','))
                {
                    Match pair = PointerEntry.Match(entry);
                    if (!pair.Success) return false;
                    string text = pair.Groups[2].Value;
                    switch (pair.Groups[1].Value)
                    {
                        case "fileID": if (fileID != null || !SignedInteger.IsMatch(text)) return false; fileID = text; break;
                        case "guid": if (guid != null || !HexGuid.IsMatch(text)) return false; guid = text; break;
                        default: if (type != null || !SignedInteger.IsMatch(text)) return false; type = text; break;
                    }
                }
                return fileID != null && (guid == null) == (type == null);
            }

            private string Resolve(string guid)
            {
                string destination;
                if (!resolved.TryGetValue(guid, out destination))
                {
                    destination = AssetDatabase.GUIDToAssetPath(guid);
                    resolved.Add(guid, destination);
                }
                return destination;
            }

            // A missing GUID is reported once, in missing_guids. Builtin
            // resources and {fileID: 0} nulls are outside this project check.
            private void CheckExternal(string guid, long fileID, string type, string example)
            {
                if (IsBuiltinGuid(guid) || fileID == 0) return;
                string path = Resolve(guid);
                if (String.IsNullOrEmpty(path)) return;
                Dictionary<long, int> identifiers;
                if (!loadedIdentifiers.TryGetValue(guid, out identifiers))
                {
                    identifiers = LoadedIdentifierCounts(path, guid);
                    loadedIdentifiers.Add(guid, identifiers);
                    // Only identifiers are cached, so loaded targets can be released.
                    if (++loadsSinceUnload >= LoadsPerUnload) { loadsSinceUnload = 0; EditorUtility.UnloadUnusedAssetsImmediate(); }
                }
                string target = "{fileID: " + fileID + ", guid: " + guid + ", type: " + type + "}";
                int count;
                if (identifiers == null) AddObject(target, "Unsupported target: no loadable objects at " + path, example);
                else if (!identifiers.TryGetValue(fileID, out count)) AddObject(target, "Local identifier does not resolve at " + path, example);
                else if (count != 1) AddObject(target, "Duplicate local identifier at " + path, example);
            }

            private void AddObject(string target, string reason, string example)
            {
                ObjectReferenceEntry entry;
                if (!badObjects.TryGetValue(target, out entry)) badObjects.Add(target, entry = new ObjectReferenceEntry(target, reason));
                if (entry.ExampleSet.Count < ExampleLimit) entry.ExampleSet.Add(example);
            }

            // Source asset, object and field line, bounded like missing_guids.
            private static string Example(string relative, string currentObject, string line)
            {
                string text = relative + (currentObject != null ? " " + currentObject : "") + ": " + line.Trim();
                return text.Length <= ExampleLength ? text : text.Substring(0, ExampleLength);
            }

            public IEnumerable<ObjectReferenceEntry> ObjectReferences() { return badObjects.Values; }
            public string[] InvalidScriptBindings() { return badScripts.OrderBy(s => s, StringComparer.Ordinal).ToArray(); }
            public int Unresolved(int shaderErrors) { return Missing.Count + badScripts.Count + badObjects.Count + shaderErrors; }
            public string Status(int shaderErrors) { return Unresolved(shaderErrors) == 0 ? "complete" : "incomplete"; }
        }

        private sealed class ObjectReferenceEntry
        {
            public readonly string Target, Reason;
            public readonly HashSet<string> ExampleSet = new HashSet<string>();
            public ObjectReferenceEntry(string target, string reason) { Target = target; Reason = reason; }
            public string[] Examples { get { return ExampleSet.OrderBy(s => s, StringComparer.Ordinal).ToArray(); } }
        }

        // Applies Run's YAML reference rules to one file outside Assets/Recovered,
        // so isolated fixtures can check the status the audit would report.
        // Each unresolved object pointer is reported as "target: reason".
        public static string ScanSerializedFile(string file, string relative, out string[] invalidScriptBindings,
            out string[] unresolvedObjectReferences, out int unresolvedReferences)
        {
            var scan = new ReferenceScan();
            scan.ScanFile(file, relative);
            if (scan.Scanned != 1) throw new InvalidDataException("Not a serialized YAML file: " + file);
            invalidScriptBindings = scan.InvalidScriptBindings();
            unresolvedObjectReferences = scan.ObjectReferences().Select(entry => entry.Target + ": " + entry.Reason).ToArray();
            unresolvedReferences = scan.Unresolved(0);
            return scan.Status(0);
        }

        private static string ReadOption(string[] arguments, string name)
        {
            string value = null;
            for (int i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] != name) continue;
                if (value != null || i + 1 >= arguments.Length || String.IsNullOrEmpty(arguments[i + 1]) || arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException("Missing or duplicate reference audit option: " + name);
                value = arguments[++i];
            }
            return value;
        }

        private static string CheckedCachePath(string root, string path)
        {
            string full = Path.GetFullPath(path);
            string cache = Path.Combine(root, ".cache", "project-lucid") + Path.DirectorySeparatorChar;
            if (!full.StartsWith(cache, StringComparison.Ordinal)) throw new IOException("Audit evidence must be inside the generated cache.");
            EnsureNoSymlinks(root, full);
            return full;
        }

        private sealed class IdentityContext
        {
            public readonly string Path, Digest, Nonce, SourceFingerprint, PreparedFingerprint;
            public IdentityContext(string path, string digest, string nonce, string source, string prepared)
            { Path = path; Digest = digest; Nonce = nonce; SourceFingerprint = source; PreparedFingerprint = prepared; }
            public void CheckSourceFingerprint(string current)
            { if (current != SourceFingerprint) throw new InvalidOperationException("Maintained source differs from the audit identity context."); }
            public void CheckUnchanged(string root)
            { if (HashContext(CheckedCachePath(root, Path)) != Digest) throw new IOException("Audit identity context changed during scanning."); }
        }

        private static byte[] ReadContextBytes(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16384)
                throw new IOException("Audit identity context is missing or exceeds its supported size.");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > 16384) throw new IOException("Invalid audit identity context size.");
            return bytes;
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static string HashContext(string path) { return HashBytes(ReadContextBytes(path)); }

        private static IdentityContext ReadIdentityContext(string root, string[] arguments, string output)
        {
            string path = ReadOption(arguments, "-lucidAuditIdentityContext");
            string digest = ReadOption(arguments, "-lucidAuditIdentityDigest");
            string nonce = ReadOption(arguments, "-lucidAuditIdentityNonce");
            if (path == null && digest == null && nonce == null) return null;
            if (path == null || digest == null || nonce == null ||
                !Regex.IsMatch(digest, @"\A[0-9a-f]{64}\z") || !Regex.IsMatch(nonce, @"\A[0-9a-f]{32}\z"))
                throw new ArgumentException("Incomplete or invalid audit identity options.");
            path = CheckedCachePath(root, path);
            if (System.IO.Path.GetFileName(path) != "identity-context.txt" ||
                System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) != nonce ||
                output != System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "pending-audit.json"))
                throw new IOException("Audit identity context must use its own pending run output.");
            byte[] bytes = ReadContextBytes(path);
            if (HashBytes(bytes) != digest) throw new IOException("Audit identity context digest differs from this invocation.");
            string[] lines = new UTF8Encoding(false, true).GetString(bytes).Split('\n');
            if (lines.Length != 8 || lines[0] != "ProjectLucid.audit-identity-context-v1" ||
                lines[1] != "algorithm=sha256-path-content-v1" || lines[2] != "nonce=" + nonce ||
                !lines[3].StartsWith("project_root_base64=", StringComparison.Ordinal) ||
                lines[4] != "unity_version=2022.3.54f1" || lines[7] != "" ||
                !Regex.IsMatch(lines[5], @"\Asource_fingerprint=[0-9a-f]{64}\z") ||
                !Regex.IsMatch(lines[6], @"\Aprepared_asset_fingerprint=[0-9a-f]{64}\z"))
                throw new ArgumentException("Unsupported audit identity context format.");
            if (Application.unityVersion != "2022.3.54f1") throw new InvalidOperationException("Audit identity requires the matching Unity Editor.");
            string encoded = lines[3].Substring("project_root_base64=".Length);
            byte[] rootBytes = Convert.FromBase64String(encoded);
            string suppliedRoot = new UTF8Encoding(false, true).GetString(rootBytes);
            if (Convert.ToBase64String(rootBytes) != encoded || suppliedRoot != root || System.IO.Path.GetFullPath(suppliedRoot) != root)
                throw new IOException("Audit identity context belongs to another project.");
            return new IdentityContext(path, digest, nonce,
                lines[5].Substring("source_fingerprint=".Length), lines[6].Substring("prepared_asset_fingerprint=".Length));
        }

        // Resolve the complete PPtr, including a signed 64-bit local identifier.
        // A matching GUID alone can select a different subasset in a DLL or an
        // unrelated script. This checks loaded class eligibility; original type
        // equivalence and runtime behavior remain separate reconstruction gates.
        // A null m_Script leaves a MonoBehaviour or ScriptableObject document
        // without its class. Rejecting it is fail-closed audit policy, not a
        // claim about the original data; optional null PPtrs are not checked here.
        public static string GetScriptBindingError(string line)
        {
            if (NullScriptReference.IsMatch(line)) return "MonoScript pointer is null: " + line.Trim();
            Match pointer = ScriptReference.Match(line);
            if (!pointer.Success || !long.TryParse(pointer.Groups[1].Value, out long fileID))
                return "Unsupported or invalid MonoScript pointer: " + line.Trim();
            if (pointer.Groups[3].Value != "3") return "Unsupported MonoScript pointer type: " + line.Trim();
            string guid = pointer.Groups[2].Value.ToLowerInvariant();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return "MonoScript GUID is unresolved: " + guid;
            UnityEngine.Object selected = null;
            foreach (KeyValuePair<long, UnityEngine.Object> loaded in LoadedObjects(path, guid))
            {
                if (loaded.Key != fileID) continue;
                if (selected != null) return "Duplicate MonoScript local identifier at " + path;
                selected = loaded.Value;
            }
            MonoScript script = selected as MonoScript;
            if (script == null) return "MonoScript local identifier does not resolve at " + path + ": " + fileID;
            Type type = script.GetClass();
            if (type == null) return "MonoScript class is unresolved at " + path;
            if (type.IsAbstract || type.ContainsGenericParameters ||
                (!typeof(MonoBehaviour).IsAssignableFrom(type) && !typeof(ScriptableObject).IsAssignableFrom(type)))
                return "MonoScript class is not a concrete component or data asset at " + path;
            return null;
        }

        // Every object the AssetDatabase loads at path under this GUID, keyed by its
        // local identifier. LoadAllAssetsAtPath does not enumerate scenes or
        // folders, so the main object is included and deduplicated by reference.
        private static IEnumerable<KeyValuePair<long, UnityEngine.Object>> LoadedObjects(string path, string guid)
        {
            var seen = new HashSet<UnityEngine.Object>();
            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(path);
            IEnumerable<UnityEngine.Object> candidates = AssetDatabase.LoadAllAssetsAtPath(path);
            if (main != null) candidates = candidates.Concat(new[] { main });
            foreach (UnityEngine.Object asset in candidates)
            {
                string actualGuid;
                long actualID;
                if (asset == null || !seen.Add(asset) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out actualGuid, out actualID) ||
                    !string.Equals(actualGuid, guid, StringComparison.OrdinalIgnoreCase)) continue;
                yield return new KeyValuePair<long, UnityEngine.Object>(actualID, asset);
            }
        }

        // Multiplicity per local identifier, or null when nothing loads.
        private static Dictionary<long, int> LoadedIdentifierCounts(string path, string guid)
        {
            var counts = new Dictionary<long, int>();
            foreach (KeyValuePair<long, UnityEngine.Object> loaded in LoadedObjects(path, guid))
                counts[loaded.Key] = counts.TryGetValue(loaded.Key, out int count) ? count + 1 : 1;
            return counts.Count == 0 ? null : counts;
        }

        private static bool IsBuiltinGuid(string guid)
        {
            return guid == "00000000000000000000000000000000" ||
                   guid == "0000000000000000e000000000000000" ||
                   guid == "0000000000000000f000000000000000";
        }

        private static void EnsureNoSymlinks(string root, string path)
        {
            string current = root;
            foreach (string component in path.Substring(root.Length + 1).Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, component);
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Refusing a symlinked report destination: " + current);
            }
        }
    }
}
