using System;

namespace Hardlight.Utils
{
    // Original URI helpers only; other FileUtilities operations remain unresolved.
    public static partial class FileUtilities
    {
        // HLUnityCore.Runtime.dll:Hardlight.Utils.FileUtilities:0x0600120a;
        // arm64 0x1b3b120. These are the observed Mac retail prefixes.
        public static string GetLocalFileUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            return path.StartsWith("/") ? "file://" : "file:///";
        }

        // Original token 0x0600120b; arm64 0x1b3b1ec. Preserve the native
        // concatenation for whitespace, which returns path concatenated twice.
        public static string GetPathWithLocalFileUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Concat(path, path);
            return string.Concat(path.StartsWith("/") ? "file://" : "file:///", path);
        }
    }
}
