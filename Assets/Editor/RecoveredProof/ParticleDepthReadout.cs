using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace ProjectLucid
{
    // Temporary measurement only: the production vertex and depth helper remain literal source copies.
    internal sealed class ParticleDepthReadout
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>();
        private string folder;
        private Shader shader;
        private string sourcePath;

        public static void WithCopy(string path, bool frontFace, Action<ParticleDepthReadout> body)
        {
            var fixture = new ParticleDepthReadout();
            Exception primary = null;
            try
            {
                // Import before the caller publishes its fixture globals, never in the draw sequence.
                fixture.Create(path, frontFace);
                body(fixture);
            }
            catch (Exception error) { primary = error; throw; }
            finally { fixture.Cleanup(primary); }
        }

        private void Create(string path, bool frontFace)
        {
            sourcePath = path;
            foreach (string original in new[] { path, path + ".meta" })
                originals.Add(original, File.ReadAllBytes(original));
            string source = File.ReadAllText(path);
            string candidate = "Assets/ParticleDepthReadout_" + Guid.NewGuid().ToString("N");
            Require(!Present(candidate) && !Present(candidate + ".meta"), "owned folder is absent");
            folder = candidate;
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder))), "owned folder creation");
            string copyPath = folder + "/Readout.shader";
            string copy = DiagnosticSource(source, frontFace);
            File.WriteAllText(copyPath, copy, new UTF8Encoding(false));
            string guid = Guid.NewGuid().ToString("N");
            Require(string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)), "owned shader GUID is absent");
            string metadata = File.ReadAllText(path + ".meta");
            string originalGuid = AssetDatabase.AssetPathToGUID(path);
            Require(originalGuid.Length == 32 && Count(metadata, "guid: " + originalGuid) == 1, "source metadata GUID");
            File.WriteAllText(copyPath + ".meta", metadata.Replace("guid: " + originalGuid, "guid: " + guid), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(copyPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            shader = AssetDatabase.LoadAssetAtPath<Shader>(copyPath);
            Require(shader != null, "owned shader import");
            Require(AssetDatabase.AssetPathToGUID(copyPath) == guid, "owned shader GUID");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                Require(message.severity != ShaderCompilerMessageSeverity.Error, message.message);
            Debug.Log("Project Lucid particle depth copy: source=" + path + " sourceSHA256=" + Hash(originals[path]) +
                " sourceMetaSHA256=" + Hash(originals[path + ".meta"]) + " copySHA256=" + Hash(File.ReadAllBytes(copyPath)) +
                " copyMetaSHA256=" + Hash(File.ReadAllBytes(copyPath + ".meta")) + " copyGUID=" + guid);
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        public void Capture(Material production, Texture2D originalDepth, Texture2D rgbaDepth, Color[] uploaded,
                            float rawEye2, float rawEye4, Func<Material, int, Color> draw, string context)
        {
            Require(shader.isSupported && shader.passCount == 3, "owned shader support in the original draw pipeline");
            Texture previousDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            Vector4 previousProjection = Shader.GetGlobalVector("_ProjectionParams");
            RenderTexture previousTarget = RenderTexture.active;
            var copy = Own(new Material(shader));
            copy.CopyPropertiesFromMaterial(production);
            copy.shaderKeywords = production.shaderKeywords;
            Debug.Log("Project Lucid particle depth CPU inputs: context=" + context + " source=" + sourcePath +
                " device=" + SystemInfo.graphicsDeviceType + " topOrigin=" + SystemInfo.graphicsUVStartsAtTop +
                " reversedZ=" + SystemInfo.usesReversedZBuffer + " rawEye2=" + Number(rawEye2) + " rawEye4=" + Number(rawEye4) +
                " assignedUploadRedByCPUIndex=[" + string.Join(",", uploaded.Select(pixel => Number(pixel.r))) + "]" +
                " projection=" + Vector(previousProjection) + " ortho=" + Vector(Shader.GetGlobalVector("unity_OrthoParams")) +
                " zBuffer=" + Vector(Shader.GetGlobalVector("_ZBufferParams")) +
                " view=" + Shader.GetGlobalMatrix("unity_MatrixV").ToString("R") +
                " inverseVP=" + Shader.GetGlobalMatrix("unity_MatrixInvVP").ToString("R"));
            Exception primary = null;
            try
            {
                foreach (Texture2D depth in new[] { originalDepth, rgbaDepth })
                {
                    Shader.SetGlobalTexture("_CameraDepthTexture", depth);
                    foreach (int sign in new[] { 1, -1 })
                    {
                        Shader.SetGlobalVector("_ProjectionParams", new Vector4(sign, previousProjection.y, previousProjection.z, previousProjection.w));
                        foreach (int row in new[] { 8, 24 })
                        {
                            string arm = context + " " + depth.format + "/" + depth.graphicsFormat + " sign=" + sign + " row=" + row;
                            Debug.Log("Project Lucid particle depth arm begin: " + arm);
                            Color before = draw(production, row);
                            copy.SetFloat("_LucidDepthReadout", 0);
                            Color ordinaryCopy = draw(copy, row);
                            Debug.Log("Project Lucid particle depth ordinary admission: " + arm + " production=" + Vector(before) + " copy=" + Vector(ordinaryCopy));
                            for (int lane = 0; lane < 4; lane++)
                                Require(Mathf.Abs(before[lane] - ordinaryCopy[lane]) < .01f, "ordinary copied Frag differs: " + arm + " lane " + lane);
                            copy.SetFloat("_LucidDepthReadout", 1);
                            Color coordinates = draw(copy, row);
                            copy.SetFloat("_LucidDepthReadout", 2);
                            Color sample = draw(copy, row);
                            copy.SetFloat("_LucidDepthReadout", 3);
                            Color auxiliary = draw(copy, row);
                            Debug.Log("Project Lucid particle depth readout: " + arm +
                                " coordinates[SVy/height,actualProjectionSign,actualOrthoFlag,repeatedSourceSampleV]=" + Vector(coordinates) +
                                " depth[rawAtRepeatedSourceUV,actualSceneEyeDepth,directRawV.25,directRawV.75]=" + Vector(sample) +
                                " auxiliary[actualClipW,ZBuffer.z,ZBuffer.w,ScaledScreenHeight]=" + Vector(auxiliary));
                            // Only inspect GPU matrices if raw/branch inputs do not explain the actual helper result.
                            float explainedEye;
                            if (coordinates.b != 1) explainedEye = 1f / (auxiliary.g * sample.r + auxiliary.b);
                            else if (Mathf.Abs(sample.r - rawEye2) < .0001f) explainedEye = 2;
                            else if (Mathf.Abs(sample.r - rawEye4) < .0001f) explainedEye = 4;
                            else explainedEye = float.NaN;
                            if (float.IsNaN(explainedEye) || float.IsInfinity(explainedEye) || float.IsNaN(sample.g) ||
                                float.IsInfinity(sample.g) || Mathf.Abs(sample.g - explainedEye) >= .01f)
                            {
                                for (int matrixRow = 0; matrixRow < 8; matrixRow++)
                                {
                                    copy.SetFloat("_LucidDepthReadout", matrixRow + 4);
                                    Debug.Log("Project Lucid particle depth GPU matrix: " + arm + " " +
                                        (matrixRow < 4 ? "V" : "InvVP") + " row=" + (matrixRow % 4) + " value=" + Vector(draw(copy, row)));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                Restore(primary,
                    () => Shader.SetGlobalTexture("_CameraDepthTexture", previousDepth),
                    () => Shader.SetGlobalVector("_ProjectionParams", previousProjection),
                    () => RenderTexture.active = previousTarget);
            }
            Debug.Log("Project Lucid particle depth diagnostic restored the original binding/sign before the unchanged assertion. " +
                "Extra draws and the prior import can warm state; this measurement is not final qualification or a correction.");
        }

        private static string DiagnosticSource(string source, bool frontFace)
        {
            string shaderPrefix = "Shader \"";
            int name = source.IndexOf(shaderPrefix, StringComparison.Ordinal) + shaderPrefix.Length;
            int nameEnd = source.IndexOf('"', name);
            Require(name >= shaderPrefix.Length && nameEnd > name, "source Shader name");
            string result = source.Substring(0, name) + "Hidden/ProjectLucid/DepthReadout_" + Guid.NewGuid().ToString("N") + source.Substring(nameEnd);
            const string properties = "    Properties\n    {";
            Require(Count(result, properties) == 1, "source Properties admission");
            result = result.Replace(properties, properties + "\n        [HideInInspector]_LucidDepthReadout (\"Depth diagnostic output\", Float) = 0");
            const string pragma = "#pragma fragment Frag";
            int firstPragma = result.IndexOf(pragma, StringComparison.Ordinal);
            Require(firstPragma >= 0, "source first fragment pragma");
            result = result.Substring(0, firstPragma) + "#pragma fragment LucidDepthFrag\n            float _LucidDepthReadout;" + result.Substring(firstPragma + pragma.Length);
            int helper = source.IndexOf("float SceneEyeDepth(float4 positionCS)", StringComparison.Ordinal);
            int prefixStart = source.IndexOf("                float y =", helper, StringComparison.Ordinal);
            int prefixEnd = source.IndexOf("                float rawDepth =", prefixStart, StringComparison.Ordinal);
            Require(helper >= 0 && prefixStart > helper && prefixEnd > prefixStart, "actual helper UV prefix");
            string uvPrefix = source.Substring(prefixStart, prefixEnd - prefixStart).Replace("positionCS", "input.positionCS");
            string faceArgument = frontFace ? ", bool isFrontFace : SV_IsFrontFace" : "";
            string faceCall = frontFace ? ", isFrontFace" : "";
            string fragment = "\n            // UV/raw lanes repeat the exact source expression; the eye lane calls the unchanged helper.\n" +
                "            void LucidDepthFrag(Varyings input" + faceArgument + ", out float4 outColor : SV_Target0\n" +
                "                #if defined(_WRITE_RENDERING_LAYERS)\n                , out float4 outRenderingLayers : SV_Target1\n                #endif\n                )\n            {\n" +
                "                if (_LucidDepthReadout < 0.5)\n                {\n                    Frag(input" + faceCall + ", outColor\n" +
                "                        #if defined(_WRITE_RENDERING_LAYERS)\n                        , outRenderingLayers\n                        #endif\n                        );\n                    return;\n                }\n" +
                "                UNITY_SETUP_INSTANCE_ID(input);\n                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);\n" +
                "                #if defined(_WRITE_RENDERING_LAYERS)\n                outRenderingLayers = 0;\n                #endif\n" + uvPrefix +
                "                if (_LucidDepthReadout < 1.5) outColor = float4(input.positionCS.y / _ScaledScreenParams.y, _ProjectionParams.x, unity_OrthoParams.w, screenUV.y);\n" +
                "                else if (_LucidDepthReadout < 2.5) outColor = float4(SampleSceneDepth(screenUV), SceneEyeDepth(input.positionCS), SampleSceneDepth(float2(0.5, 0.25)), SampleSceneDepth(float2(0.5, 0.75)));\n" +
                "                else if (_LucidDepthReadout < 3.5) outColor = float4(TransformWorldToHClip(input.positionWS).w, _ZBufferParams.z, _ZBufferParams.w, _ScaledScreenParams.y);\n" +
                "                else if (_LucidDepthReadout < 4.5) outColor = UNITY_MATRIX_V[0];\n" +
                "                else if (_LucidDepthReadout < 5.5) outColor = UNITY_MATRIX_V[1];\n" +
                "                else if (_LucidDepthReadout < 6.5) outColor = UNITY_MATRIX_V[2];\n" +
                "                else if (_LucidDepthReadout < 7.5) outColor = UNITY_MATRIX_V[3];\n" +
                "                else if (_LucidDepthReadout < 8.5) outColor = UNITY_MATRIX_I_VP[0];\n" +
                "                else if (_LucidDepthReadout < 9.5) outColor = UNITY_MATRIX_I_VP[1];\n" +
                "                else if (_LucidDepthReadout < 10.5) outColor = UNITY_MATRIX_I_VP[2];\n" +
                "                else outColor = UNITY_MATRIX_I_VP[3];\n            }\n";
            int firstProgram = result.IndexOf("HLSLPROGRAM", StringComparison.Ordinal);
            int programEnd = result.IndexOf("            ENDHLSL", firstProgram, StringComparison.Ordinal);
            Require(firstProgram >= 0 && programEnd > firstPragma, "source first program end");
            return result.Insert(programEnd, fragment);
        }

        private void Cleanup(Exception primary)
        {
            var failures = new List<Exception>();
            foreach (UnityEngine.Object item in owned)
                try { if (item != null) UnityEngine.Object.DestroyImmediate(item); }
                catch (Exception error) { failures.Add(error); }
            if (folder != null)
                try
                {
                    if (Present(folder) || Present(folder + ".meta"))
                    {
                        Require(!Present(folder) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0, "owned folder is not a link");
                        Require(AssetDatabase.DeleteAsset(folder), "owned folder deletion");
                    }
                    Require(!Present(folder) && !Present(folder + ".meta"), "owned folder is gone");
                }
                catch (Exception error) { failures.Add(error); }
            foreach (KeyValuePair<string, byte[]> original in originals)
                try { Require(original.Value.SequenceEqual(File.ReadAllBytes(original.Key)), "original bytes changed: " + original.Key); }
                catch (Exception error) { failures.Add(error); }
            if (failures.Count == 0) return;
            if (primary != null) failures.Insert(0, primary);
            throw new AggregateException("Particle depth readout cleanup failed.", failures);
        }

        private static int Count(string text, string value) => (text.Length - text.Replace(value, "").Length) / value.Length;
        private static void Restore(Exception primary, params Action[] actions)
        {
            var failures = new List<Exception>();
            foreach (Action action in actions)
                try { action(); }
                catch (Exception error) { failures.Add(error); }
            if (failures.Count == 0) return;
            if (primary != null) failures.Insert(0, primary);
            throw new AggregateException("Particle depth readout state restoration failed.", failures);
        }
        private static bool Present(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Color value) => "[" + Number(value.r) + "," + Number(value.g) + "," + Number(value.b) + "," + Number(value.a) + "]";
        private static string Vector(Vector4 value) => "[" + Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + "," + Number(value.w) + "]";
        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Particle depth readout failed: " + message);
        }
    }
}
