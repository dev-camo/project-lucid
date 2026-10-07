using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class LucidCleanPlayerCompilation
    {
        [Serializable]
        public sealed class Receipt
        {
            public string status = "failed", error;
            public string unity_version, module_path, module_sha256_before, module_sha256_after, module_mvid;
            public string interface_type, options_type, status_type;
            public string compile_method, tick_method, get_output_method, set_output_method;
            public string options_names, output_path, previous_output_path, restored_output_path;
            public int options_value, target_value, group_value, subtarget, tick_count;
            public string target_name, group_name, start_status, terminal_status;
            public int start_status_value = -1, terminal_status_value = -1;
            public string[] extra_scripting_defines, observed_statuses;
            public bool building_for_editor = false, output_restored = false;
            public bool public_player_compile_called = false;
            public long elapsed_milliseconds;
        }

        // Finish this genuine player compilation first. The caller then invokes
        // PlayerBuildInterface.CompilePlayerScripts and seals its returned files.
        public static Receipt Prime(BuildTarget target, string outputDirectory,
            string expectedModulePath, string expectedModuleSha256, string expectedModuleMvid)
        {
            var receipt = new Receipt { unity_version = Application.unityVersion,
                target_name = target.ToString(), target_value = (int)target,
                group_name = BuildTargetGroup.Standalone.ToString(), group_value = (int)BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Player, extra_scripting_defines = new string[0],
                output_path = outputDirectory };
            var observed = new System.Collections.Generic.List<string>();
            var clock = Stopwatch.StartNew();
            MethodInfo getOutput = null, setOutput = null;
            bool previousCaptured = false;
            try
            {
                if (Application.unityVersion != "2022.3.54f1" || !Application.isBatchMode ||
                    EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorUserBuildSettings.activeBuildTarget != target || receipt.subtarget != 0 ||
                    (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64))
                    throw new InvalidOperationException("Clean player compilation requires the matching stable batch Editor and player target.");
                if (Path.GetFullPath(outputDirectory) != outputDirectory || !Directory.Exists(outputDirectory) ||
                    Directory.GetFileSystemEntries(outputDirectory).Length != 0)
                    throw new IOException("Clean player compilation requires a fresh empty output directory.");
                for (string p = outputDirectory; !String.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                    if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Clean player output contains a symbolic link.");

                Assembly module = typeof(PlayerBuildInterface).Assembly;
                receipt.module_path = module.Location;
                receipt.module_mvid = module.ManifestModule.ModuleVersionId.ToString("D");
                receipt.module_sha256_before = Hash(module.Location);
                if (module.Location != expectedModulePath || receipt.module_mvid != expectedModuleMvid ||
                    receipt.module_sha256_before != expectedModuleSha256)
                    throw new InvalidDataException("Installed player compilation module differs from the context.");
                Type api = module.GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface", true);
                Type optionsType = module.GetType("UnityEditor.Scripting.ScriptCompilation.EditorScriptCompilationOptions", true);
                Type statusType = module.GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilation+CompileStatus", true);
                receipt.interface_type = api.FullName; receipt.options_type = optionsType.FullName; receipt.status_type = statusType.FullName;
                if (!optionsType.IsEnum || !statusType.IsEnum || EnumValue(optionsType, "BuildingCleanCompilation") != 16384 ||
                    EnumValue(optionsType, "BuildingExtractTypeDB") != 4096 || EnumValue(optionsType, "BuildingUseDeterministicCompilation") != 512 ||
                    EnumValue(optionsType, "BuildingForEditor") != 2 || EnumValue(statusType, "Idle") != 0 ||
                    EnumValue(statusType, "Compiling") != 1 || EnumValue(statusType, "CompilationStarted") != 2 ||
                    EnumValue(statusType, "CompilationFailed") != 3 || EnumValue(statusType, "CompilationComplete") != 4)
                    throw new InvalidDataException("Installed player compilation enum values differ.");
                receipt.options_names = "BuildingCleanCompilation, BuildingExtractTypeDB, BuildingUseDeterministicCompilation";
                object options = Enum.Parse(optionsType, receipt.options_names, false);
                receipt.options_value = Convert.ToInt32(options);
                receipt.building_for_editor = (receipt.options_value & EnumValue(optionsType, "BuildingForEditor")) != 0;
                if (receipt.options_value != 20992 || receipt.building_for_editor)
                    throw new InvalidDataException("Clean compilation options do not select the player.");
                getOutput = Method(api, "GetCompileScriptsOutputDirectory", typeof(string), Type.EmptyTypes);
                setOutput = Method(api, "SetCompileScriptsOutputDirectory", typeof(void), new[] { typeof(string) });
                Type[] compileParameters = { optionsType, typeof(BuildTargetGroup), typeof(BuildTarget), typeof(int), typeof(string[]) };
                MethodInfo compile = Method(api, "CompileScripts", statusType, compileParameters);
                MethodInfo tick = Method(api, "TickCompilationPipeline", statusType, compileParameters.Concat(new[] { typeof(bool) }).ToArray());
                receipt.get_output_method = Signature(getOutput); receipt.set_output_method = Signature(setOutput);
                receipt.compile_method = Signature(compile); receipt.tick_method = Signature(tick);
                receipt.previous_output_path = (string)getOutput.Invoke(null, null);
                previousCaptured = true;
                setOutput.Invoke(null, new object[] { outputDirectory });
                if ((string)getOutput.Invoke(null, null) != outputDirectory)
                    throw new InvalidDataException("Player compiler output directory was not selected.");
                object[] arguments = { options, BuildTargetGroup.Standalone, target, receipt.subtarget, receipt.extra_scripting_defines };
                object state = compile.Invoke(null, arguments);
                receipt.start_status = state.ToString(); receipt.start_status_value = Convert.ToInt32(state);
                observed.Add(receipt.start_status);
                if (receipt.start_status_value != 1 && receipt.start_status_value != 2 && receipt.start_status_value != 4)
                    throw new InvalidOperationException("Clean player compilation did not start: " + receipt.start_status);
                int value = receipt.start_status_value;
                while (value != 4)
                {
                    if (receipt.tick_count >= 64 || clock.ElapsedMilliseconds > 120000)
                        throw new TimeoutException("Clean player compilation exceeded its bounded ticks.");
                    state = tick.Invoke(null, arguments.Concat(new object[] { true }).ToArray());
                    ++receipt.tick_count; value = Convert.ToInt32(state);
                    receipt.terminal_status = state.ToString(); receipt.terminal_status_value = value;
                    observed.Add(receipt.terminal_status);
                    if (value != 1 && value != 2 && value != 4)
                        throw new InvalidOperationException("Clean player compilation failed: " + receipt.terminal_status);
                }
                receipt.terminal_status = state.ToString(); receipt.terminal_status_value = value;
                if (clock.ElapsedMilliseconds > 120000)
                    throw new TimeoutException("Clean player compilation exceeded its elapsed limit.");
                receipt.status = "completed";
            }
            catch (Exception error)
            {
                Exception actual = error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error;
                receipt.status = "failed"; receipt.error = actual.GetType().Name + ": " + actual.Message;
            }
            finally
            {
                if (previousCaptured)
                {
                    try
                    {
                        setOutput.Invoke(null, new object[] { receipt.previous_output_path });
                        receipt.restored_output_path = (string)getOutput.Invoke(null, null);
                        receipt.output_restored = receipt.restored_output_path == receipt.previous_output_path;
                        if (!receipt.output_restored) throw new InvalidDataException("Player compilation output was not restored.");
                    }
                    catch (Exception error)
                    {
                        receipt.status = "failed";
                        receipt.error = (receipt.error == null ? "" : receipt.error + " | ") + "Output restoration: " + error.GetType().Name + ": " + error.Message;
                    }
                }
                try
                {
                    if (!String.IsNullOrEmpty(receipt.module_path))
                    {
                        receipt.module_sha256_after = Hash(receipt.module_path);
                        if (receipt.module_sha256_after != expectedModuleSha256)
                            throw new InvalidDataException("Player compilation module changed during the clean compilation.");
                    }
                }
                catch (Exception error)
                {
                    receipt.status = "failed";
                    receipt.error = (receipt.error == null ? "" : receipt.error + " | ") + error.GetType().Name + ": " + error.Message;
                }
                receipt.observed_statuses = observed.ToArray();
                receipt.elapsed_milliseconds = clock.ElapsedMilliseconds;
            }
            return receipt;
        }

        private static int EnumValue(Type type, string name)
        {
            if (!Enum.IsDefined(type, name)) throw new InvalidDataException("Missing player compilation enum " + name);
            return Convert.ToInt32(Enum.Parse(type, name, false));
        }
        private static MethodInfo Method(Type type, string name, Type result, Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            if (method == null || method.DeclaringType != type || method.ReturnType != result || method.IsGenericMethod)
                throw new InvalidDataException("Installed player compilation method differs: " + name);
            return method;
        }
        private static string Signature(MethodInfo method) => method.DeclaringType.FullName + "." + method.Name +
            "(" + String.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName)) + ")->" + method.ReturnType.FullName;
        private static string Hash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
