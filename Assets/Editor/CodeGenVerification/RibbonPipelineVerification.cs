using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using ProjectLucid.CodeGen;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using UnityEditor.Compilation;

namespace ProjectLucid
{
    public static class RibbonPipelineVerification
    {
        private sealed class Compilation : ICompiledAssembly
        {
            public string Name { get; set; }
            public string[] References { get; set; }
            public string[] Defines { get; set; }
            public InMemoryAssembly InMemoryAssembly { get; set; }
        }

        private static string Body(MethodDefinition method) => string.Join("\n", method.Body.Instructions.Select(i => i.OpCode.Name + ":" + (i.Operand == null ? "" : i.Operand.ToString())));

        public static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (value, description) => {
                if (!value) throw new InvalidOperationException(description);
                ++checks;
            };
            var compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Single(a => a.name == "HLSplines.Runtime");
            var loaded = typeof(Hardlight.KnotT).Assembly;
            string installed = Path.GetFullPath(loaded.Location);
            check(installed == Path.GetFullPath(compiled.outputPath), "Loaded ribbon module differs from current Unity compilation output.");
            // Bee's actual CSC response file distinguishes its original input
            // from processed copies. A matching MVID excludes earlier imports.
            var candidates = Directory.GetFiles("Library/Bee/artifacts", "HLSplines.Runtime.dll", SearchOption.AllDirectories)
                .Where(p => File.Exists(Path.ChangeExtension(p, ".rsp"))).Where(p => {
                    string output = "-out:\"" + p.Replace('\\', '/') + "\"";
                    if (!File.ReadLines(Path.ChangeExtension(p, ".rsp")).Contains(output))
                        throw new InvalidOperationException("Bee ribbon input differs from its actual CSC output argument.");
                    using (var module = ModuleDefinition.ReadModule(p)) return module.Mvid == loaded.ManifestModule.ModuleVersionId;
                }).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            check(candidates.Length > 0, "No actual Bee input matches the current loaded ribbon module.");
            string raw = candidates[0];
            byte[] input = File.ReadAllBytes(raw), before = (byte[])input.Clone();
            byte[] pdb = File.ReadAllBytes(Path.ChangeExtension(raw, ".pdb"));
            check(candidates.All(p => File.ReadAllBytes(p).SequenceEqual(input)), "Ambiguous ribbon compiler inputs.");
            var assembly = new Compilation {
                Name = compiled.name, Defines = compiled.defines,
                References = compiled.compiledAssemblyReferences.Concat(compiled.assemblyReferences.Select(a => a.outputPath)).ToArray(),
                InMemoryAssembly = new InMemoryAssembly(input, pdb)
            };
            var processor = new RibbonAccessorAttributeOrderPostProcessor();
            check(processor.WillProcess(assembly), "Actual ribbon module was ignored.");
            var result = processor.Process(assembly);
            check(result != null && result.Diagnostics.Count == 0, "Genuine ribbon declarations failed restoration.");
            byte[] outputBytes = result.InMemoryAssembly.PeData;
            check(input.SequenceEqual(before), "Ribbon processor mutated compiler input.");
            check(ReferenceEquals(result.InMemoryAssembly.PdbData, pdb), "Ribbon processor replaced the PDB buffer.");
            check(!outputBytes.SequenceEqual(input), "The raw compiler getter order was not restored.");
            check(outputBytes.SequenceEqual(File.ReadAllBytes(installed)), "Actual Unity ribbon output differs from the exact accessor-order restoration.");
            check(pdb.SequenceEqual(File.ReadAllBytes(Path.ChangeExtension(installed, ".pdb"))), "Actual Unity ribbon PDB changed during restoration.");
            assembly.InMemoryAssembly = result.InMemoryAssembly;
            var repeated = processor.Process(assembly);
            check(repeated.Diagnostics.Count == 0 && repeated.InMemoryAssembly.PeData.SequenceEqual(outputBytes), "Ribbon restoration is not idempotent.");
            check(ReferenceEquals(repeated.InMemoryAssembly.PdbData, pdb), "Repeated restoration changed PDB identity.");
            assembly.Name = "Game.Runtime";
            check(!processor.WillProcess(assembly) && processor.Process(assembly) == null, "Ribbon processing escaped its original assembly boundary.");
            assembly.Name = compiled.name;
            var empty = new InMemoryAssembly(Array.Empty<byte>(), pdb);
            assembly.InMemoryAssembly = empty;
            var rejected = processor.Process(assembly);
            check(rejected.Diagnostics.Count == 1 && rejected.Diagnostics[0].DiagnosticType == DiagnosticType.Error, "Malformed ribbon compilation was not rejected.");
            check(ReferenceEquals(rejected.InMemoryAssembly, empty) && ReferenceEquals(rejected.InMemoryAssembly.PdbData, pdb), "Rejected ribbon compilation changed its input buffers.");
            using (var rawStream = new MemoryStream(input, false))
            using (var outputStream = new MemoryStream(outputBytes, false))
            using (var original = ModuleDefinition.ReadModule(rawStream))
            using (var restored = ModuleDefinition.ReadModule(outputStream))
            {
                check(original.Mvid == restored.Mvid && original.Mvid == loaded.ManifestModule.ModuleVersionId, "Restoration changed actual ribbon module identity.");
                var tokens = new System.Collections.Generic.HashSet<uint>();
                foreach (string name in new[] { "KnotT", "LinearRatio", "PositionAndTangent" })
                {
                    var a = original.Types.Single(t => t.FullName == "Hardlight." + name);
                    var b = restored.Types.Single(t => t.FullName == a.FullName);
                    foreach (var property in a.Properties)
                    {
                        var getter = property.GetMethod;
                        var after = b.Properties.Single(p => p.Name == property.Name).GetMethod;
                        check(getter.CustomAttributes.Select(c => c.AttributeType.FullName).SequenceEqual(new[] { "System.Runtime.CompilerServices.IsReadOnlyAttribute", "System.Runtime.CompilerServices.CompilerGeneratedAttribute" }), "Natural ribbon getter marker order differs.");
                        check(after.CustomAttributes.Select(c => c.AttributeType.FullName).SequenceEqual(getter.CustomAttributes.Reverse().Select(c => c.AttributeType.FullName)), "Original ribbon getter marker order was lost.");
                        check(getter.MetadataToken == after.MetadataToken && tokens.Add(getter.MetadataToken.ToUInt32()), "Ribbon getter token changed or was duplicated.");
                        check(after.CustomAttributes.Select((c, i) => c.Constructor.FullName == getter.CustomAttributes[1 - i].Constructor.FullName && c.AttributeType.Scope.ToString() == getter.CustomAttributes[1 - i].AttributeType.Scope.ToString() && c.GetBlob().SequenceEqual(getter.CustomAttributes[1 - i].GetBlob())).All(v => v), "Restoration changed marker scope, constructor or raw blob.");
                        check(Body(getter) == Body(after), "Restoration changed ribbon getter instructions.");
                    }
                }
                check(tokens.Count == 6, "Expected exactly six original ribbon accessors.");
            }
            return checks;
        }
    }
}
