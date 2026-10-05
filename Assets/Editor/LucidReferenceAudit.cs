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
            public string[] invalid_script_bindings;
            public string[] shader_errors;
            public string limitation = "Reference resolution does not verify implemented behavior, authored flow, or presentation parity.";
        }

        private static readonly Regex GuidReference = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly Regex ScriptReference = new Regex(@"^\s*m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*(\d+)\}\s*$", RegexOptions.Compiled);
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
            var resolved = new Dictionary<string, string>();
            var missing = new Dictionary<string, HashSet<string>>();
            var badScripts = new HashSet<string>();
            var scriptResults = new Dictionary<string, string>();
            var shaderErrors = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.EnumerateFiles(recovered, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!SerializedExtensions.Contains(Path.GetExtension(file))) continue;
                using (var reader = new StreamReader(file))
                {
                    string line = reader.ReadLine();
                    if (line == null || !line.StartsWith("%YAML", StringComparison.Ordinal)) continue;
                    scanned++;
                    string relative = "Assets/" + file.Substring(Application.dataPath.Length + 1).Replace('\\', '/');
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.TrimStart().StartsWith("m_Script:", StringComparison.Ordinal))
                        {
                            string error;
                            if (!scriptResults.TryGetValue(line, out error))
                            {
                                error = GetScriptBindingError(line);
                                scriptResults.Add(line, error);
                            }
                            if (error != null) badScripts.Add(relative + ": " + error);
                        }
                        if (line.IndexOf("guid:", StringComparison.Ordinal) < 0) continue;
                        foreach (Match match in GuidReference.Matches(line))
                        {
                            string guid = match.Groups[1].Value.ToLowerInvariant();
                            if (IsBuiltinGuid(guid)) continue;
                            string destination;
                            if (!resolved.TryGetValue(guid, out destination))
                            {
                                destination = AssetDatabase.GUIDToAssetPath(guid);
                                resolved.Add(guid, destination);
                            }
                            if (String.IsNullOrEmpty(destination))
                            {
                                HashSet<string> examples;
                                if (!missing.TryGetValue(guid, out examples)) missing.Add(guid, examples = new HashSet<string>());
                                if (examples.Count < 8) examples.Add(relative);
                            }
                        }
                    }
                }
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
            string referenceStatus = missing.Count == 0 && badScripts.Count == 0 && shaderErrors.Count == 0 ? "complete" : "incomplete";
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
                scanned_assets = scanned,
                unresolved_references = missing.Count + badScripts.Count + shaderErrors.Count,
                missing_guids = missing.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MissingReference { guid = pair.Key, examples = pair.Value.OrderBy(s => s, StringComparer.Ordinal).ToArray() }).ToArray(),
                invalid_script_bindings = badScripts.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
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
            Debug.Log("Project Lucid reference audit: " + scanned + " assets, " + audit.unresolved_references + " unresolved references. " + output);
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
        public static string GetScriptBindingError(string line)
        {
            if (NullScriptReference.IsMatch(line)) return null;
            Match pointer = ScriptReference.Match(line);
            if (!pointer.Success || !long.TryParse(pointer.Groups[1].Value, out long fileID))
                return "Unsupported or invalid MonoScript pointer: " + line.Trim();
            if (pointer.Groups[3].Value != "3") return "Unsupported MonoScript pointer type: " + line.Trim();
            string guid = pointer.Groups[2].Value.ToLowerInvariant();
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return "MonoScript GUID is unresolved: " + guid;
            UnityEngine.Object selected = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                string actualGuid;
                long actualID;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out actualGuid, out actualID) ||
                    !string.Equals(actualGuid, guid, StringComparison.OrdinalIgnoreCase) || actualID != fileID) continue;
                if (selected != null) return "Duplicate MonoScript local identifier at " + path;
                selected = asset;
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
