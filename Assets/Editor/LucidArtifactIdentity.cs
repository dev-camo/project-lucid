using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ProjectLucid.Editor
{
    // Content identity shared with tools/lucidlib/verification.py. Generated
    // test/audit receipts become stale when maintained code or assets change.
    public static class LucidArtifactIdentity
    {
        public static string Fingerprint(string root, bool prepared)
        {
            root = Path.GetFullPath(root);
            var files = new List<string>();
            string[] bases = prepared ? new[] { "Assets/Recovered", "Assets/StreamingAssets" }
                : new[] { "Assets", "Packages", "ProjectSettings", "tools" };
            foreach (string name in bases)
            {
                string path = Path.Combine(root, name);
                if (Directory.Exists(path)) Collect(root, path, prepared, files);
            }
            using (var digest = SHA256.Create())
            {
                foreach (string file in files.OrderBy(p => Relative(root, p), StringComparer.Ordinal))
                {
                    string hash;
                    using (var stream = File.OpenRead(file))
                    using (var content = SHA256.Create()) hash = Hex(content.ComputeHash(stream));
                    byte[] row = Encoding.UTF8.GetBytes(Relative(root, file) + "\0" + hash + "\n");
                    digest.TransformBlock(row, 0, row.Length, row, 0);
                }
                digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return Hex(digest.Hash);
            }
        }

        private static void Collect(string root, string directory, bool prepared, List<string> files)
        {
            RejectSymlink(directory);
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                RejectSymlink(child);
                string name = Path.GetFileName(child);
                if (name.StartsWith(".", StringComparison.Ordinal) || name == "obj" || name == "bin" || name == "__pycache__") continue;
                if (!prepared && Relative(root, directory) == "Assets" && (name == "Recovered" || name == "StreamingAssets")) continue;
                Collect(root, child, prepared, files);
            }
            foreach (string file in Directory.EnumerateFiles(directory))
            {
                RejectSymlink(file);
                string name = Path.GetFileName(file);
                if (name == ".DS_Store" || name.EndsWith(".pyc") || name.EndsWith(".pyo") || name.EndsWith(".tmp")) continue;
                if (!prepared && Relative(root, directory) == "Assets" && (name == "Recovered.meta" || name == "StreamingAssets.meta")) continue;
                files.Add(file);
            }
        }

        private static string Relative(string root, string path) => path.Substring(root.Length + 1).Replace('\\', '/');
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        private static void RejectSymlink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Refusing symlink in project identity: " + path);
        }
    }
}
