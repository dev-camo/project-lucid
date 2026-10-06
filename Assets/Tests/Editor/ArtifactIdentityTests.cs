using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using ProjectLucid.Editor;

namespace ProjectLucid.Tests
{
    public sealed class ArtifactIdentityTests
    {
        private static void Write(string root, string relative, string value)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(value));
        }

        [Test]
        public void SharedFingerprintIncludesAllPreparedAssetDirectories()
        {
            // This known vector is also checked by the Python verification tests.
            // UTF8 bytes have no BOM; only relative paths contribute to identity.
            string root = Path.Combine(Path.GetTempPath(), "ProjectLucid", "artifact identity", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Write(root, "Assets/Scripts/fixture.cs", "maintained source\n");
                Write(root, "Packages/com.example.fixture/Runtime/fixture.cs", "maintained package\n");
                Write(root, "ProjectSettings/fixture.asset", "project settings\n");
                Write(root, "tools/fixture.py", "public tooling\n");
                Write(root, "Assets/Recovered/obj/model.bundle", "Assets/Recovered:obj\n");
                Write(root, "Assets/Recovered/bin/model.bundle", "Assets/Recovered:bin\n");
                Write(root, "Assets/Recovered/__pycache__/model.bundle", "Assets/Recovered:__pycache__\n");
                Write(root, "Assets/StreamingAssets/obj/model.bundle", "Assets/StreamingAssets:obj\n");
                Write(root, "Assets/StreamingAssets/bin/model.bundle", "Assets/StreamingAssets:bin\n");
                Write(root, "Assets/StreamingAssets/__pycache__/model.bundle", "Assets/StreamingAssets:__pycache__\n");
                Write(root, "Assets/Recovered/obj/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/Recovered/bin/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/Recovered/__pycache__/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/StreamingAssets/obj/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/StreamingAssets/bin/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/StreamingAssets/__pycache__/model.bundle.meta", "asset meta\n");
                Write(root, "Assets/Recovered/mesh-é-λ.bundle", "unicode content é λ\n");
                const string source = "f37b79d175d61b5da2cecd9cdb5317e6cd3f766aa3d15216fe1ff22c81f5e005";
                const string prepared = "373ae7371342069cd66210e0a7359def1e129d54885a32ab31d2ad42160c6106";
                Assert.That(LucidArtifactIdentity.Fingerprint(root, false), Is.EqualTo(source));
                Assert.That(LucidArtifactIdentity.Fingerprint(root, true), Is.EqualTo(prepared));
                foreach (string scope in new[] { "Assets", "Packages", "ProjectSettings", "tools" })
                    foreach (string folder in new[] { "obj", "bin", "__pycache__" })
                        Write(root, scope + "/" + folder + "/temporary.cs", "build cache\n");
                Assert.That(LucidArtifactIdentity.Fingerprint(root, false), Is.EqualTo(source), "Maintained-source caches remain excluded.");
                Assert.That(LucidArtifactIdentity.Fingerprint(root, true), Is.EqualTo(prepared));
                foreach (string scope in new[] { "Assets/Recovered", "Assets/StreamingAssets" })
                    foreach (string folder in new[] { "obj", "bin", "__pycache__" })
                    {
                        string relative = scope + "/" + folder + "/model.bundle";
                        Write(root, relative, "changed bundle\n");
                        Assert.That(LucidArtifactIdentity.Fingerprint(root, true), Is.Not.EqualTo(prepared), relative);
                        Assert.That(LucidArtifactIdentity.Fingerprint(root, false), Is.EqualTo(source));
                        Write(root, relative, scope + ":" + folder + "\n");
                        Write(root, relative + ".meta", "changed meta\n");
                        Assert.That(LucidArtifactIdentity.Fingerprint(root, true), Is.Not.EqualTo(prepared), relative + ".meta");
                        Write(root, relative + ".meta", "asset meta\n");
                    }
                Assert.That(LucidArtifactIdentity.Fingerprint(root, true), Is.EqualTo(prepared));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
