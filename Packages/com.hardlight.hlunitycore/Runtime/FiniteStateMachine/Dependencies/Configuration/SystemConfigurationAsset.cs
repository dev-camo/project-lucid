using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hardlight
{
    // HLUnityCore.Runtime 0x02000241. Validation remains an original abstract authored contract.
    public abstract class SystemConfigurationAsset : ScriptableObject
    {
        [SerializeField] protected SystemConfigurationAsset m_baseAsset;
        [SerializeField] protected List<string> m_overriddenFields = new List<string>();
        public abstract void Validate();

        // 0x06000e6e; ARM64 0x1b1a9d8.
        protected static bool ValidateDirectory(string path, string relativeTo = "") { return Directory.Exists(GetFullPath(path, relativeTo)); }
        // 0x06000e6f/0x06000e70; ARM64 0x1b1aab0/0x1b1aac8. Both original wrappers call File.Exists.
        protected static bool FileExists(string path, string relativeTo = "") { return File.Exists(GetFullPath(path, relativeTo)); }
        protected static bool ValidateFile(string path, string relativeTo = "") { return File.Exists(GetFullPath(path, relativeTo)); }
        // 0x06000e71; ARM64 0x1b1a9f0. A nonblank relative root strips leading '/' before path normalization.
        protected static string GetFullPath(string path, string relativeTo)
        {
            if (!string.IsNullOrWhiteSpace(relativeTo)) path = path.TrimStart('/');
            return Path.GetFullPath(Path.Combine(relativeTo, path.Replace('/', Path.DirectorySeparatorChar)));
        }
        // 0x06000e72; ARM64 0x1b1aae0. Override list initializer runs before ScriptableObject's native constructor.
        protected SystemConfigurationAsset() { }
    }
}
