using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public int scanned_assets;
            public int unresolved_references;
            public MissingReference[] missing_guids;
            public string[] invalid_script_bindings;
            public string[] shader_errors;
            public string limitation = "Reference resolution does not verify implemented behavior, authored flow, or presentation parity.";
        }

        private static readonly Regex GuidReference = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);
        private static readonly HashSet<string> SerializedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unity", ".prefab", ".asset", ".mat", ".controller", ".overridecontroller", ".playable",
            ".mixer", ".anim", ".spriteatlas", ".spriteatlasv2", ".rendertexture", ".lighting"
        };

        public static void Run()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string recovered = Path.Combine(Application.dataPath, "Recovered");
            if (!Directory.Exists(recovered)) throw new InvalidOperationException("Prepare supplied assets before auditing references.");
            var resolved = new Dictionary<string, string>();
            var missing = new Dictionary<string, HashSet<string>>();
            var badScripts = new HashSet<string>();
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
                            else if (line.IndexOf("m_Script:", StringComparison.Ordinal) >= 0)
                            {
                                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(destination);
                                if (script == null || script.GetClass() == null) badScripts.Add(relative + " -> " + destination);
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
            var audit = new Audit
            {
                status = missing.Count == 0 && badScripts.Count == 0 && shaderErrors.Count == 0 ? "complete" : "incomplete",
                unity_version = Application.unityVersion,
                source_fingerprint = LucidArtifactIdentity.Fingerprint(root, false),
                prepared_asset_fingerprint = LucidArtifactIdentity.Fingerprint(root, true),
                scanned_assets = scanned,
                unresolved_references = missing.Count + badScripts.Count + shaderErrors.Count,
                missing_guids = missing.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MissingReference { guid = pair.Key, examples = pair.Value.OrderBy(s => s, StringComparer.Ordinal).ToArray() }).ToArray(),
                invalid_script_bindings = badScripts.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                shader_errors = shaderErrors.ToArray()
            };
            string output = Path.Combine(root, ".cache", "project-lucid", "reports", "unity-reference-audit.json");
            string[] arguments = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(arguments, "-lucidAuditOutput");
            if (outputIndex >= 0)
            {
                if (outputIndex + 1 >= arguments.Length) throw new ArgumentException("Missing audit output path");
                output = Path.GetFullPath(arguments[outputIndex + 1]);
            }
            string cache = Path.Combine(root, ".cache", "project-lucid") + Path.DirectorySeparatorChar;
            if (!output.StartsWith(cache, StringComparison.Ordinal)) throw new IOException("Audit output must be inside the generated cache");
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
