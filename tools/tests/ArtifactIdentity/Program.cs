using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectLucid.Editor;

namespace ProjectLucid.ToolTests
{
    internal static class Program
    {
        // The original UTF8 vectors also live in the existing Python and Unity
        // identity tests. Expected digests are not calculated by this runner.
        private const string Source = "f37b79d175d61b5da2cecd9cdb5317e6cd3f766aa3d15216fe1ff22c81f5e005";
        private const string Prepared = "373ae7371342069cd66210e0a7359def1e129d54885a32ab31d2ad42160c6106";
        // These Unicode vectors are also fixed in the Python identity tests.
        private const string UnicodeSource = "450c68cbceb82d4d16bdf02dfa6956332f8d856a4cb15e5f4218c77a2f7616c1";
        private const string UnicodePrepared = "ab5b67e4566261ce27053dddd489a55bef1ec98ea4effc21ad4a69ecd1d284eb";

        private static void Write(string root, string relative, string content)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        }

        private static void Require(bool value, string message, ref int checks)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }

        private static void Vectors(string root, ref int checks)
        {
            var sourceRows = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Assets/Scripts/fixture.cs", "maintained source\n" },
                { "Packages/com.example.fixture/Runtime/fixture.cs", "maintained package\n" },
                { "ProjectSettings/fixture.asset", "project settings\n" },
                { "tools/fixture.py", "public tooling\n" }
            };
            var preparedRows = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string scope in new[] { "Assets/Recovered", "Assets/StreamingAssets" })
                foreach (string folder in new[] { "obj", "bin", "__pycache__" })
                {
                    preparedRows.Add(scope + "/" + folder + "/model.bundle", scope + ":" + folder + "\n");
                    preparedRows.Add(scope + "/" + folder + "/model.bundle.meta", "asset meta\n");
                }
            preparedRows.Add("Assets/Recovered/mesh-é-λ.bundle", "unicode content é λ\n");
            foreach (var row in sourceRows) Write(root, row.Key, row.Value);
            foreach (var row in preparedRows) Write(root, row.Key, row.Value);
            Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Original source vector", ref checks);
            Require(LucidArtifactIdentity.Fingerprint(root, true) == Prepared, "Original prepared vector", ref checks);

            foreach (string scope in new[] { "Assets", "Packages", "ProjectSettings", "tools" })
                foreach (string folder in new[] { "obj", "bin", "__pycache__" })
                    Write(root, scope + "/" + folder + "/temporary.cs", "build cache\n");
            Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Source cache exclusions", ref checks);
            Require(LucidArtifactIdentity.Fingerprint(root, true) == Prepared, "Prepared vector after source caches", ref checks);

            foreach (string scope in new[] { "Assets/Recovered", "Assets/StreamingAssets" })
                foreach (string folder in new[] { "obj", "bin", "__pycache__" })
                {
                    string relative = scope + "/" + folder + "/model.bundle";
                    Write(root, relative, "changed bundle\n");
                    Require(LucidArtifactIdentity.Fingerprint(root, true) != Prepared, "Prepared content change: " + relative, ref checks);
                    Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Source unaffected by prepared content: " + relative, ref checks);
                    Write(root, relative, preparedRows[relative]);
                    Write(root, relative + ".meta", "changed meta\n");
                    Require(LucidArtifactIdentity.Fingerprint(root, true) != Prepared, "Prepared metadata change: " + relative, ref checks);
                    Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Source unaffected by prepared metadata: " + relative, ref checks);
                    Write(root, relative + ".meta", preparedRows[relative + ".meta"]);
                }
            Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Restored source vector", ref checks);
            Require(LucidArtifactIdentity.Fingerprint(root, true) == Prepared, "Restored prepared vector", ref checks);

            Write(root, "Assets/Scripts/fixture.cs", "changed maintained source\n");
            Require(LucidArtifactIdentity.Fingerprint(root, false) != Source, "Maintained source change", ref checks);
            Require(LucidArtifactIdentity.Fingerprint(root, true) == Prepared, "Prepared unaffected by source change", ref checks);
            Write(root, "Assets/Scripts/fixture.cs", sourceRows["Assets/Scripts/fixture.cs"]);
            Require(LucidArtifactIdentity.Fingerprint(root, false) == Source, "Restored maintained source", ref checks);
        }

        private static void SourceDocumentVectors(string root, ref int checks)
        {
            string cache = Path.Combine(root, "isolated host tools");
            Directory.CreateDirectory(cache);
            string previous = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(cache);
                foreach (string relative in new[] { "Assets/Scripts/fixture.cs", "Packages/com.example.fixture/Runtime/fixture.cs" })
                {
                    // A decoy document in the tool cache must never receive credit.
                    Write(cache, relative, "wrong source checksum\n");
                    string expected = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                    string actual = ProjectLucid.CodeProgress.SourceDocuments.Resolve(root, relative);
                    Require(actual == expected, "Relative PDB resolves from project: " + relative, ref checks);
                    Require(File.ReadAllText(actual) != File.ReadAllText(Path.Combine(cache, relative)), "Tool-cache decoy rejected", ref checks);
                    Require(ProjectLucid.CodeProgress.SourceDocuments.Resolve(root, expected) == expected, "Absolute PDB identity retained", ref checks);
                }
                string outside = ProjectLucid.CodeProgress.SourceDocuments.Resolve(root, "../foreign.cs");
                Require(Path.GetRelativePath(root, outside).StartsWith("..", StringComparison.Ordinal), "Foreign source remains outside project", ref checks);
            }
            finally { Directory.SetCurrentDirectory(previous); }
            Console.WriteLine("Progress source document vectors passed from isolated tool working directory.");
        }

        private static void UnicodeVectors(string root, ref int checks)
        {
            // U+E000 follows supplementary characters in UTF16 code-unit
            // order. Python's default code-point sort reverses that relation.
            foreach (string scope in new[] { "Assets/Scripts", "Assets/Recovered" })
            {
                Write(root, scope + "/\uE000.cs", "high BMP path\n");
                Write(root, scope + "/ascii.cs", "ASCII path\n");
                Write(root, scope + "/\U0001F680.cs", "rocket path\n");
                Write(root, scope + "/\U00010000.cs", "supplementary path\n");
            }
            Require(LucidArtifactIdentity.Fingerprint(root, false) == UnicodeSource, "BMP/supplementary source vector", ref checks);
            Require(LucidArtifactIdentity.Fingerprint(root, true) == UnicodePrepared, "BMP/supplementary prepared vector", ref checks);
        }

        private static int Main(string[] args)
        {
            if (args.Length != 0)
            {
                Console.Error.WriteLine("Artifact identity vectors take no arguments.");
                return 2;
            }
            string root = Path.Combine(Path.GetTempPath(), "ProjectLucid artifact identity with spaces", Guid.NewGuid().ToString("N"));
            Exception failure = null;
            Exception cleanupFailure = null;
            int checks = 0;
            try
            {
                Directory.CreateDirectory(root);
                Vectors(root, ref checks);
                SourceDocumentVectors(root, ref checks);
                UnicodeVectors(Path.Combine(root, "unicode"), ref checks);
            }
            catch (Exception error) { failure = error; }
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (Exception error) { cleanupFailure = error; }
            if (failure != null || cleanupFailure != null)
            {
                Console.Error.WriteLine("Artifact identity vectors failed after " + checks + " assertions.");
                if (failure != null) Console.Error.WriteLine(failure);
                if (cleanupFailure != null) Console.Error.WriteLine("Cleanup failure: " + cleanupFailure);
                return 1;
            }
            Console.WriteLine("Artifact identity vectors passed: " + checks + " assertions.");
            return 0;
        }
    }
}
