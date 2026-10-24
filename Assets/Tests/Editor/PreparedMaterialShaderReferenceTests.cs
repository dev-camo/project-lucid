using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace ProjectLucid.Tests
{
    public sealed class PreparedMaterialShaderReferenceTests
    {
        private Fixture active;

        // The release asset map and original standalone Material/Shader documents
        // supply these identities. Prepared hashes change only m_Shader type2→3;
        // Unity2022.3's native two-order import control establishes that kind.
        [Test]
        public void PreparedSkyMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Shaders/Shaders/Sky/HL_Sky_Cubemap.mat",
                "83c385824ffc8434080e17de38866c76", "c24466ea4a824924e820666d8ffb14a4",
                "HardLight/Sky/HL_Sky_Cubemap", "Assets/Shaders/Recovered/HL_Sky_Cubemap.shader",
                "56cb61dbf9fb9ee4b50d1a322d3721dbae4bb23ac9423c2802469be6115594af");
        }

        [Test]
        public void PreparedEggmanMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Shaders/Shaders/Gameplay/HL_Gameplay_Eggman_Logo.mat",
                "f097d8d0808faa547be6ab67961b7020", "109d879a0e474224f9576baad15f0273",
                "HardLight/Gameplay/HL_Gameplay_Eggman_Logo", "Assets/Shaders/Recovered/HL_Gameplay_Eggman_Logo.shader",
                "f21fe4aef0791351d6a53f6c6c33b436400fe3a42ec7dfd984ac7a6d7b1cd43e");
        }

        [Test]
        public void PreparedPfxAlphaBlendAddMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Common And Gameplay/Materials/common_fx_dream_orb.mat",
                "42e0cb1eb208c7a46bb5d865b9a48ce9", "eb77dff1bca503f43903d55ab1540df0",
                "HardLight/Particle/HL_PFX_AlphaBlendAdd", "Assets/Shaders/Recovered/HL_PFX_AlphaBlendAdd.shader",
                "93098558119c06c2a9ffd7253c87c9e265afc673e7af526277696bbd3cd8bf58");
        }

        [Test]
        public void PreparedGhostedMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Characters/Ghosted/Body/character_ghosted_cheese_v.mat",
                "172defa961562e249a98d1932858da68", "c18e3bc73f2c52c458bbc4569a3d42a1",
                "HardLight/Character/HL_Char_Ghosted", "Assets/Shaders/Recovered/HL_Char_Ghosted.shader",
                "b23231e9007a0d51f85954ea3d9fb9b335c2dd58eeced6a565a999273f0645f1");
        }

        [Test]
        public void PreparedCollectableMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Shaders/Shaders/Gameplay/HL_Gameplay_Collectable.mat",
                "b4121c5f8822c604eb14d55c61e5da6a", "79454e735e6b2824d92c0ebe4fc51d38",
                "HardLight/Gameplay/HL_Gameplay_Collectable", "Assets/Shaders/Recovered/HL_Gameplay_Collectable.shader",
                "590d4d29e5a45e6ee67be0b291c81aee6d977ae3edfb55e779f6c733b8e36f23");
        }

        [Test]
        public void PreparedMatcapMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Shaders/Shaders/Environment/HL_Matcap.mat",
                "9475067459eabbb47ad146cf8c26c7c6", "fc62d258c9bcd2648b4158e86c2d4178",
                "HardLight/Environment/HL_Matcap", "Assets/Shaders/Recovered/HL_Matcap.shader",
                "8a3bc0d1f665014051da140d769265a67d60c80ecf818e06b466b0b18de5bd66");
        }

        [Test]
        public void PreparedTrailMaterialResolvesItsOriginalShader()
        {
            Run("Assets/Recovered/Data/Art/Shaders/Shaders/Particle/HL_Particle_Trail.mat",
                "574043b807ede064dae0e243260e96ac", "3d28166e35482e444b56af851ff24084",
                "HardLight/Particle/HL_Particle_Trail", "Assets/Shaders/Recovered/HL_Particle_Trail.shader",
                "fcdc2c62c3865874e44a64d89112a6ef903e9db5b0bc0dd13cb012b8ab8dad6d");
        }

        private void Run(string materialPath, string materialGuid, string shaderGuid,
                         string shaderName, string shaderPath, string preparedSha256)
        {
            Assert.That(active, Is.Null, "A prior material fixture still owns its assets.");
            active = new Fixture();
            active.Run(materialPath, materialGuid, shaderGuid, shaderName, shaderPath, preparedSha256);
        }

        [TearDown]
        public void DeleteOwnedMaterialAndCheckOriginalInputs()
        {
            Fixture owned = active;
            if (owned == null) return;
            try { owned.Cleanup(); }
            finally { active = null; }
        }

        private sealed class Fixture
        {
            private readonly Dictionary<string, string> originals = new Dictionary<string, string>();
            private string folder;

            public void Run(string materialPath, string materialGuid, string shaderGuid,
                            string shaderName, string shaderPath, string preparedSha256)
            {
                foreach (string path in new[] { materialPath, materialPath + ".meta", shaderPath, shaderPath + ".meta" })
                    originals.Add(path, Hash(path));
                byte[] yaml = File.ReadAllBytes(materialPath);
                Assert.That(Hash(materialPath), Is.EqualTo(preparedSha256), "Expected bytes come from the original Material with only the import kind adapted.");
                Assert.That(AssetDatabase.AssetPathToGUID(materialPath), Is.EqualTo(materialGuid));
                string pointer = "  m_Shader: {fileID: 4800000, guid: " + shaderGuid + ", type: 3}";
                string text = new UTF8Encoding(false, true).GetString(yaml);
                Assert.That(text.Split(new[] { pointer }, StringSplitOptions.None), Has.Length.EqualTo(2));

                string candidate = "Assets/PreparedMaterialShaderReference_" + Guid.NewGuid().ToString("N");
                Assert.That(Directory.Exists(candidate) || File.Exists(candidate) || File.Exists(candidate + ".meta"), Is.False);
                folder = candidate;
                Assert.That(AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder)), Is.Not.Empty);
                string pathCopy = folder + "/" + Path.GetFileName(materialPath);
                string guidCopy = Guid.NewGuid().ToString("N");
                Assert.That(AssetDatabase.GUIDToAssetPath(guidCopy), Is.Empty);
                File.WriteAllBytes(pathCopy, yaml);
                File.WriteAllText(pathCopy + ".meta", "fileFormatVersion: 2\nguid: " + guidCopy
                    + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", new UTF8Encoding(false));
                string metaHash = Hash(pathCopy + ".meta");
                AssetDatabase.ImportAsset(pathCopy, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(pathCopy);
                Assert.That(material, Is.Not.Null);
                // Resolve the genuine serialized reference before any direct Shader load.
                Shader shader = material.shader;
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.name, Is.EqualTo(shaderName));
                Assert.That(AssetDatabase.GetAssetPath(shader), Is.EqualTo(shaderPath));
                Assert.That(ObjectIdentifier.TryGetObjectIdentifier(shader, out ObjectIdentifier shaderId), Is.True);
                Assert.That(shaderId.guid.ToString(), Is.EqualTo(shaderGuid));
                Assert.That(shaderId.localIdentifierInFile, Is.EqualTo(4800000));
                Assert.That(shaderId.fileType, Is.EqualTo(FileType.MetaAssetType));
                Assert.That(AssetImporter.GetAtPath(shaderPath), Is.TypeOf<ShaderImporter>());
                Assert.That(ObjectIdentifier.TryGetObjectIdentifier(material, out ObjectIdentifier materialId), Is.True);
                Assert.That(materialId.guid.ToString(), Is.EqualTo(guidCopy));
                Assert.That(materialId.localIdentifierInFile, Is.EqualTo(2100000));
                Assert.That(materialId.fileType, Is.EqualTo(FileType.SerializedAssetType));
                Assert.That(Hash(pathCopy), Is.EqualTo(preparedSha256));
                Assert.That(Hash(pathCopy + ".meta"), Is.EqualTo(metaHash));
            }

            public void Cleanup()
            {
                var failures = new List<Exception>();
                try
                {
                    if (folder != null && (Directory.Exists(folder) || File.Exists(folder + ".meta")))
                        Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
                    Assert.That(folder == null || (!Directory.Exists(folder) && !File.Exists(folder + ".meta")), Is.True);
                }
                catch (Exception failure) { failures.Add(failure); }
                foreach (var original in originals)
                {
                    try { Assert.That(Hash(original.Key), Is.EqualTo(original.Value), original.Key); }
                    catch (Exception failure) { failures.Add(failure); }
                }
                if (failures.Count != 0) throw new AggregateException("Owned material fixture cleanup failed.", failures);
            }
        }

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
