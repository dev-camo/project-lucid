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
        public void PreparedCharStandardAmyMaterialResolvesItsOriginalShader()
        {
            const string materialPath = "Assets/Recovered/Data/Art/Characters/Amy/amy_mat.mat";
            Assert.That(Hash(materialPath + ".meta"), Is.EqualTo("80b2afcc33e31f41682878f3d36571f9d1a4478376bc05a0da95ef59d5401eac"));
            Run(materialPath, "2c2852d28ebcb6a448343928f6d50f9d", "fb495c01c5fd7534590903b8e77f3281",
                "HardLight/Character/HL_Char_Standard", "Assets/Shaders/Recovered/HL_Char_Standard.shader", "74e0619038884cad9e9240ec1f1aca3ae77bede3ef94f6125647f67598380f14");
            const string erosion = "_SampleTexture2D_d532aa29fc9a40a7bb50548e1579ecae_Texture_1_Texture2D";
            const string erosionGuid = "e8129d5371ff1e04d8070064644689a1";
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Recovered/HL_Char_Standard.shader");
            int index = shader.FindPropertyIndex(erosion);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That((shader.GetPropertyFlags(index) & UnityEngine.Rendering.ShaderPropertyFlags.NonModifiableTextureData) != 0, Is.True);
            string erosionPath = AssetDatabase.GUIDToAssetPath(erosionGuid);
            Assert.That(erosionPath, Is.Not.Empty);
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(erosionPath);
            Assert.That(texture, Is.Not.Null);
            Assert.That(ObjectIdentifier.TryGetObjectIdentifier(texture, out ObjectIdentifier textureId), Is.True);
            Assert.That(textureId.guid.ToString(), Is.EqualTo(erosionGuid));
            Assert.That(textureId.localIdentifierInFile, Is.EqualTo(2800000));
            Assert.That(textureId.fileType, Is.EqualTo(FileType.MetaAssetType));
            Material fresh = null;
            try
            {
                fresh = new Material(shader);
                Assert.That(fresh.GetTexture(erosion), Is.SameAs(texture));
            }
            finally { if (fresh != null) UnityEngine.Object.DestroyImmediate(fresh); }
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
