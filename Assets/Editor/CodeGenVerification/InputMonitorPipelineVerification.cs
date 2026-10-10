using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using UnityEditor.Compilation;

namespace ProjectLucid
{
    // Check the loaded module against its compiler input and exact slot restoration.
    // Reflection avoids a compile-time reference to a currently absent owner.
    public static class InputMonitorPipelineVerification
    {
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
        public static int Run()
        {
            var compiled = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Single(a => a.name == "HLInput.Runtime");
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "HLInput.Runtime");
            Type owner = loaded.GetType("Hardlight.InputMonitor", true), marker = loaded.GetType("Hardlight.IInputMonitor", true), baseMarker = loaded.GetType("Hardlight.IBaseInputMonitor", true);
            // Preserve the complete original Input86 roster and account explicitly
            // for the separate portable selection adapter in the same assembly.
            string sourceRoot = Path.GetFullPath("Packages/com.hardlight.hlinput/Runtime");
            string adapter = Path.GetFullPath("Packages/com.hardlight.hlinput/Runtime/Offline/PortableControllerSelection.cs");
            string[] actualSources = compiled.sourceFiles.Select(Path.GetFullPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string[] originalSources = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Select(Path.GetFullPath).Where(p => p != adapter).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(originalSources.Length == 86 && !originalSources.Any(p => p.StartsWith(Path.GetDirectoryName(adapter) + Path.DirectorySeparatorChar, StringComparison.Ordinal)), "Expected complete original Input86 sources outside the offline adapter directory.");
            Require(actualSources.Length == 87 && actualSources.Distinct(StringComparer.Ordinal).Count() == 87 && actualSources.Count(p => p == adapter) == 1, "Expected original Input86 and exactly one separate portable selection source.");
            Require(actualSources.Where(p => p != adapter).SequenceEqual(originalSources), "Actual compiled input sources differ from the complete maintained original roster.");
            Require(loaded.FullName == "HLInput.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", "Changed actual loaded assembly identity.");
            string installed = Path.GetFullPath(loaded.Location);
            Require(installed == Path.GetFullPath(compiled.outputPath), "Loaded input module differs from current compilation output.");
            var candidates = new List<string>();
            foreach (string path in Directory.GetFiles("Library/Bee/artifacts", "HLInput.Runtime.dll", SearchOption.AllDirectories))
            {
                string response = Path.ChangeExtension(path, ".rsp");
                if (!File.Exists(response)) continue;
                string output = "-out:\"" + path.Replace('\\', '/') + "\"";
                Require(File.ReadLines(response).Contains(output), "Bee input differs from actual compiler output argument.");
                using (var m = ModuleDefinition.ReadModule(path))
                    if (m.Mvid == loaded.ManifestModule.ModuleVersionId) candidates.Add(path);
            }
            Require(candidates.Count > 0, "No actual Bee input matches the current loaded module.");
            string raw = candidates.OrderBy(p => p, StringComparer.Ordinal).First();
            byte[] original = File.ReadAllBytes(raw), originalPdb = File.ReadAllBytes(Path.ChangeExtension(raw, ".pdb"));
            Require(candidates.All(p => Hash(File.ReadAllBytes(p)) == Hash(original) && File.ReadAllBytes(Path.ChangeExtension(p, ".pdb")).SequenceEqual(originalPdb)), "Ambiguous current compiler PE/PDB inputs.");
            var references = compiled.compiledAssemblyReferences.Concat(compiled.assemblyReferences.Select(a => a.outputPath)).Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
            string run = Path.Combine(".cache/project-lucid/verification", "input-monitor-pipeline-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(run);
            try
            {
                string patched = Path.Combine(run, "HLInput.Runtime.dll");
                int checks = InputMonitorPostProcessorVerification.Run(raw, patched, references);
                Require(File.ReadAllBytes(patched).SequenceEqual(File.ReadAllBytes(installed)), "Loaded Unity output differs from the exact twenty-three-cell restoration of actual Bee input.");
                Require(originalPdb.SequenceEqual(File.ReadAllBytes(Path.ChangeExtension(installed, ".pdb"))), "Actual input PDB changed during restoration.");
                Type provider = loaded.GetType("Hardlight.SourceProvider", true);
                var providerGetters = new[] { "get_KeySource", "get_AxisSource", "get_PointerSource", "get_GestureSource" }.Select(name => provider.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single(m => m.Name == name && m.GetParameters().Length == 0)).ToArray();
                Require(providerGetters.All(m => (ushort)m.Attributes == 2534 && m.IsFinal && m.IsVirtual && m.GetBaseDefinition() == m && (m.Attributes & System.Reflection.MethodAttributes.NewSlot) != 0), "Actual SourceProvider getters lack exact original slots.");
                Require(provider.GetMethod("get_TouchSource", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Attributes == (System.Reflection.MethodAttributes)2534, "Already-correct TouchSource flags changed.");
                var declared = owner.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                MethodInfo getter = declared.Single(m => m.Name == "get_SourceProvider" && m.GetParameters().Length == 0);
                MethodInfo setter = declared.Single(m => m.Name == "SetPaused" && m.ReturnType == typeof(void) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(bool));
                Require((ushort)getter.Attributes == 2534 && (ushort)setter.Attributes == 486, "Actual loaded targets lack original exact flags.");
                Require(new[] { getter, setter }.All(m => m.IsFinal && m.IsVirtual && m.GetBaseDefinition() == m && (m.Attributes & System.Reflection.MethodAttributes.NewSlot) != 0), "Actual loaded targets lost concrete new virtual slots.");
                Require(marker.IsInterface && baseMarker.IsInterface && marker.GetMethods().Length == 0 && baseMarker.GetMethods().Length == 0 && marker.GetFields().Length == 0 && baseMarker.GetFields().Length == 0 && marker.GetProperties().Length == 0 && baseMarker.GetProperties().Length == 0 && marker.GetEvents().Length == 0 && baseMarker.GetEvents().Length == 0, "Loaded marker interfaces acquired invented contracts.");
                Require(marker.GetInterfaces().SequenceEqual(new[] { baseMarker }) && baseMarker.GetInterfaces().Length == 0 && owner.GetInterfaces().Contains(marker) && owner.GetInterfaces().Contains(baseMarker), "Loaded marker inheritance differs.");
                foreach (string key in new string[] { "Hardlight.BaseActiveBindings`2|get_CurrentInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|get_CurrentBaseInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseInputAxis`2|add_AxisStartHandlers", "Hardlight.BaseInputAxis`2|remove_AxisStartHandlers", "Hardlight.BaseInputAxis`2|add_AxisHandlers", "Hardlight.BaseInputAxis`2|remove_AxisHandlers", "Hardlight.BaseInputAxis`2|add_AxisEndHandlers", "Hardlight.BaseInputAxis`2|remove_AxisEndHandlers", "Hardlight.BaseInputBinding`1|get_BindingIdentifiers", "Hardlight.BaseInputBinding`1|get_Priority", "Hardlight.BaseInputBinding`1|get_AllowRemapping", "Hardlight.BaseInputBinding`1|get_ForcedBinding", "Hardlight.BaseInputBinding`1|get_FallbackInputType" })
                {
                    string[] parts = key.Split('|');
                    var t = loaded.GetType(parts[0], true);
                    var m = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Single(v => v.Name == parts[1]);
                    Require((ushort)m.Attributes == 2534 && m.IsFinal && m.IsVirtual && m.GetBaseDefinition() == m && (m.Attributes & System.Reflection.MethodAttributes.NewSlot) != 0, "Actual additional target lacks its exact original flags: " + key);
                }
                Type axisMarker = loaded.GetType("Hardlight.IInputAxis", true), currentMarker = loaded.GetType("Hardlight.ICurrentBaseInputBindingsProvider", true);
                Require(axisMarker.IsInterface && axisMarker.GetMethods().Length == 0 && axisMarker.GetEvents().Length == 0 && axisMarker.GetNestedTypes().Select(t => t.Name).SequenceEqual(new[] { "OnAxisStartHandler", "OnAxisHandler", "OnAxisEndHandler" }), "Actual stripped Axis API or genuine nested delegates changed.");
                Require(currentMarker.IsInterface && currentMarker.GetMethods().Length == 0 && currentMarker.GetProperties().Length == 0 && currentMarker.GetEvents().Length == 0, "Actual stripped CurrentBase API changed.");
                Require((ushort)loaded.GetType("Hardlight.BaseBindingData", true).GetMethod("get_GameInput", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Attributes == 2534, "Untargeted GameInput getter changed.");
                return checks;
            }
            finally { Directory.Delete(run, true); }
        }
    }
}
