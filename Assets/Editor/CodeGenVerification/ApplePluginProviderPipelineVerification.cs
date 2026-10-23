using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using ProjectLucid.CodeGen;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEditor.Compilation;

namespace ProjectLucid
{
    // Check the loaded HLAppleInput.Runtime module against its Bee compiler input: the loaded
    // module must be that input with exactly the two original provider getter flag cells restored.
    // Reflection is used because this assembly does not reference HLAppleInput.Runtime.
    public static class ApplePluginProviderPipelineVerification
    {
        private sealed class Compilation : ICompiledAssembly
        {
            public InMemoryAssembly InMemoryAssembly { get; set; }
            public string Name { get; set; }
            public string[] References { get; set; }
            public string[] Defines => new[] { "UNITY_2022_3_54", "UNITY_EDITOR" };
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        public static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (value, label) => { Require(value, label); ++checks; };
            var compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Single(a => a.name == "HLAppleInput.Runtime");
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "HLAppleInput.Runtime");
            check(Path.GetFullPath(loaded.Location) == Path.GetFullPath(compiled.outputPath), "Loaded module differs from current compilation output.");

            // Declared flags of the loaded module: only the two getters carry Final|Virtual|NewSlot.
            Type owner = loaded.GetType("Hardlight.SourceApplePluginControllerProvider", true);
            var declared = owner.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var flags = declared.ToDictionary(m => m.Name, m => (ushort)m.Attributes);
            check(declared.Length == 6 && owner.GetConstructors().Length == 1, "Changed provider method inventory.");
            check(flags["get_KeySource"] == 0x09e6 && flags["get_AxisSource"] == 0x09e6, "Getters lack the original 0x09e6 flags.");
            foreach (string name in new[] { "get_KeySource", "get_AxisSource" })
            {
                MethodInfo m = declared.Single(d => d.Name == name);
                check(m.IsFinal && m.IsVirtual && (m.Attributes & System.Reflection.MethodAttributes.NewSlot) != 0 && m.GetBaseDefinition() == m, "Getter lost its concrete virtual slot " + name);
            }
            check(flags["get_GlyphSource"] == 0x0886, "Untargeted GlyphSource flags changed.");
            check(flags["Initialise"] == 0x00c6 && flags["Shutdown"] == 0x00c6 && flags["Update"] == 0x00c6, "Untargeted overrides changed.");
            check((ushort)owner.GetConstructors().Single().Attributes == 0x1886, "Constructor flags changed.");
            // The retained marker interface stays genuinely empty: no member was invented to force the flags.
            Type marker = loaded.GetType("Hardlight.ISourceApplePluginControllerProvider", true);
            check(marker.IsInterface && marker.GetMethods().Length == 0 && marker.GetProperties().Length == 0 && marker.GetEvents().Length == 0, "Marker interface gained members.");
            Type generic = owner.GetInterfaces().Single(i => i.IsGenericType);
            check(generic.GetGenericTypeDefinition().GetMethods().Length == 0 && generic.GetGenericTypeDefinition().GetProperties().Length == 0, "Generic marker gained members.");

            // The loaded module is exactly the Bee compiler input plus the two restored cells.
            var candidates = new List<string>();
            foreach (string path in Directory.GetFiles("Library/Bee/artifacts", "HLAppleInput.Runtime.dll", SearchOption.AllDirectories))
            {
                string response = Path.ChangeExtension(path, ".rsp");
                if (!File.Exists(response)) continue; // Processed copies have no CSC response file.
                Require(File.ReadLines(response).Contains("-out:\"" + path.Replace('\\', '/') + "\""), "Bee input differs from actual compiler output argument.");
                using (var module = ModuleDefinition.ReadModule(path))
                    if (module.Mvid == loaded.ManifestModule.ModuleVersionId) candidates.Add(path);
            }
            check(candidates.Count > 0, "No actual Bee input matches the current loaded module.");
            string raw = candidates.OrderBy(p => p, StringComparer.Ordinal).First();
            byte[] original = File.ReadAllBytes(raw), unchanged = (byte[])original.Clone(), pdb = File.ReadAllBytes(Path.ChangeExtension(raw, ".pdb"));
            check(candidates.All(p => File.ReadAllBytes(p).SequenceEqual(original) && File.ReadAllBytes(Path.ChangeExtension(p, ".pdb")).SequenceEqual(pdb)), "Ambiguous compiler PE/PDB inputs.");
            var references = compiled.compiledAssemblyReferences.Concat(compiled.assemblyReferences.Select(a => a.outputPath)).Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
            var processor = new ApplePluginProviderFlagsPostProcessor();
            Func<byte[], string, ILPostProcessResult> process = (pe, name) => processor.Process(new Compilation { Name = name, References = references, InMemoryAssembly = new InMemoryAssembly(pe, pdb) });

            check(!ReferenceEquals(processor.GetInstance(), processor), "Fresh processor instance.");
            check(process(original, "HLInput.Runtime") == null && process(original, "Game.Runtime") == null, "Other assemblies ignored.");
            var result = process(original, "HLAppleInput.Runtime");
            check(result != null && result.Diagnostics.Count == 0, "Actual compiler input accepted.");
            byte[] patched = result.InMemoryAssembly.PeData;
            check(original.SequenceEqual(unchanged) && !ReferenceEquals(original, patched) && patched.Length == original.Length, "Input PE never mutated; length retained.");
            check(ReferenceEquals(pdb, result.InMemoryAssembly.PdbData), "PDB retained.");
            check(patched.SequenceEqual(File.ReadAllBytes(loaded.Location)), "Loaded Unity output differs from the exact two-cell restoration of the Bee input.");
            check(pdb.SequenceEqual(File.ReadAllBytes(Path.ChangeExtension(loaded.Location, ".pdb"))), "Loaded PDB changed during restoration.");
            int[] differences = Enumerable.Range(0, original.Length).Where(i => original[i] != patched[i]).ToArray();
            check(differences.Length == 4 && differences[1] == differences[0] + 1 && differences[3] == differences[2] + 1, "Exactly two sixteen-bit cells changed.");
            foreach (int offset in new[] { differences[0], differences[2] })
            {
                ushort before = (ushort)(original[offset] | original[offset + 1] << 8), after = (ushort)(patched[offset] | patched[offset + 1] << 8);
                check(before == 0x0886 && after == 0x09e6, "Cell changes exactly 0x0886 to 0x09e6.");
            }
            using (var before = ModuleDefinition.ReadModule(new MemoryStream(original, false)))
            using (var after = ModuleDefinition.ReadModule(new MemoryStream(patched, false)))
            {
                var a = before.GetTypes().SelectMany(t => t.Methods).ToArray();
                var b = after.GetTypes().SelectMany(t => t.Methods).ToArray();
                check(before.Mvid == after.Mvid && a.Length == b.Length, "MVID and MethodDef rows retained.");
                var changed = new List<string>();
                for (int i = 0; i < a.Length; ++i)
                {
                    check(a[i].MetadataToken == b[i].MetadataToken && a[i].RVA == b[i].RVA && a[i].FullName == b[i].FullName && a[i].ImplAttributes == b[i].ImplAttributes, "Method identity, RVA and implementation retained.");
                    if (a[i].Attributes != b[i].Attributes) changed.Add(a[i].FullName);
                }
                check(changed.Count == 2 && changed.All(n => n.Contains("Hardlight.SourceApplePluginControllerProvider::get_KeySource") || n.Contains("Hardlight.SourceApplePluginControllerProvider::get_AxisSource")), "Exactly the two provider getters changed.");
            }

            // Idempotence: an already restored image is accepted and returned unchanged.
            var again = process(patched, "HLAppleInput.Runtime");
            check(again.Diagnostics.Count == 0 && again.InMemoryAssembly.PeData.SequenceEqual(patched), "Restored image is a fixed point.");

            // Strict refusals. Each mutated input must yield an Error diagnostic and the unpatched bytes.
            Action<byte[], string> refused = (mutated, label) =>
            {
                var refusal = process(mutated, "HLAppleInput.Runtime");
                check(refusal.Diagnostics.Count == 1 && refusal.Diagnostics[0].DiagnosticType == Unity.CompilationPipeline.Common.Diagnostics.DiagnosticType.Error && refusal.Diagnostics[0].MessageData.StartsWith("Apple provider metadata restoration refused"), label);
                check(ReferenceEquals(refusal.InMemoryAssembly.PeData, mutated), label + " (no patched bytes returned)");
            };
            var half = (byte[])original.Clone(); // only the first getter restored
            half[differences[0]] = patched[differences[0]]; half[differences[0] + 1] = patched[differences[0] + 1];
            refused(half, "Partially restored targets refused.");
            var wrong = (byte[])original.Clone(); // a target cell with another flag value
            wrong[differences[0]] = 0x87;
            refused(wrong, "Unexpected target flags refused.");
            refused(new byte[16], "Non-PE input refused.");
            // A changed contract (any other method flag) must refuse as well: flip Update's flags in a copy.
            var drift = (byte[])original.Clone();
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(original, false)))
            {
                // Update is the sixth method row of the provider; its flag cell is found through the table walk
                // by diffing against a copy where Cecil reports the same row, so shift the cell by one row stride.
                var update = module.GetTypes().Single(t => t.FullName == "Hardlight.SourceApplePluginControllerProvider").Methods.Single(m => m.Name == "Update");
                int stride = differences[2] - differences[0]; // consecutive MethodDef rows are one stride apart
                int cell = differences[0] + (int)(update.MetadataToken.RID - module.GetTypes().Single(t => t.FullName == "Hardlight.SourceApplePluginControllerProvider").Methods.Single(m => m.Name == "get_KeySource").MetadataToken.RID) * stride;
                Require((ushort)(original[cell] | original[cell + 1] << 8) == 0x00c6, "Located Update's flag cell.");
                drift[cell] = 0xe6;
            }
            refused(drift, "Drifted non-target method flags refused.");
            return checks;
        }
    }
}
