using System;
using System.Text;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.SceneManagement;

namespace Hardlight.Utils
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ResourceUtils
    {
        // Original 0600123c: Scene.name, then slash split with empty entries retained.
        public static string CurrentSceneName
        {
            get
            {
                string[] parts = SceneManager.GetActiveScene().name.Split('/', StringSplitOptions.None);
                return parts[parts.Length - 1];
            }
        }

        // Original 0600123d; a trailing slash produces the original empty leaf.
        public static string GetResourceLeafName(string resourcePath)
        {
            string[] parts = resourcePath.Split('/', StringSplitOptions.None);
            return parts[parts.Length - 1];
        }

        // Original 0600123e: only a slash strictly after index zero splits the path.
        public static void SplitPathAndName(string fullPath, out string path, out string name)
        {
            int index = fullPath.LastIndexOf('/');
            if (index > 0)
            {
                path = fullPath.Substring(0, index);
                name = fullPath.Substring(index + 1, fullPath.Length - (index + 1));
            }
            else
            {
                path = string.Empty;
                name = fullPath;
            }
        }

        // Original 0600123f uses the genuine OpString operators, retaining doubled slashes.
        public static string CombinePaths(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path2))
                return path1;
            if (string.IsNullOrEmpty(path1))
                return path2;
            if (path1[path1.Length - 1] == '/')
                return OpString.i + path1 + path2;
            return OpString.i + path1 + "/" + path2;
        }

        // Original 06001240 compares invariant lowercase characters, then counts remaining slashes.
        public static string GetPathDifference(string path1, string path2)
        {
            if (path1.Length == 0)
                return path2;
            int index = 0;
            int lastSlash = -1;
            while (index < path1.Length)
            {
                if (index >= path2.Length)
                    break;
                if (char.ToLowerInvariant(path1[index]) != char.ToLowerInvariant(path2[index]))
                    break;
                if (path1[index] == '/')
                    lastSlash = index;
                index++;
            }
            if (index == 0)
                return path2;
            if (index == path1.Length && index == path2.Length)
                return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (; index < path1.Length; index++)
            {
                if (path1[index] == '/')
                    builder.Append("../");
            }
            if (builder.Length == 0 && lastSlash == path2.Length - 1)
                return "./";
            return builder.ToString() + path2.Substring(lastSlash + 1);
        }

        // Original 06001241 is the public, base-only constructor of this nonstatic class.
        public ResourceUtils() { }
    }
}
