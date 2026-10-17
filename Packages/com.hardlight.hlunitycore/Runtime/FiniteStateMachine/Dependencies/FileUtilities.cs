using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 020002c8 (0600120a..06001214), bodies from the
    // 1.10.1 x86-64 binary. Null checks are off for the whole type: null paths
    // fail inside the framework calls, and a null callback is invoked unchecked.
    [PublicAPI]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class FileUtilities
    {
        // 0x0600120a (0x1b34410): the scheme prefix only; blank input is returned.
        public static string GetLocalFileUri(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
            return path.StartsWith("/") ? "file://" : "file:///";
        }

        // 0x0600120b (0x1b34490): GetLocalFileUri inlined, so blank input comes
        // back concatenated with itself.
        public static string GetPathWithLocalFileUri(string path)
        {
            return GetLocalFileUri(path) + path;
        }

        // 0x0600120c (0x1b34520): every line but the last uses WriteLine (TextWriter
        // slot 18), the last uses Write (slot 16); an empty list throws on lines[-1].
        public static void WriteAllLinesWithoutFinalNewLine(string path, IReadOnlyList<string> lines)
        {
            if (string.IsNullOrEmpty(path))
            {
                HLOutput.LogError("No path to write all lines to.");
                return;
            }
            if (lines == null)
            {
                HLOutput.LogError("No lines to write at path '" + path + "'.");
                return;
            }

            using (FileStream fileStream = File.Create(path))
            using (StreamWriter streamWriter = new StreamWriter(fileStream))
            {
                int lastIndex = lines.Count - 1;
                for (int i = 0; i < lastIndex; i++)
                {
                    streamWriter.WriteLine(lines[i]);
                }
                streamWriter.Write(lines[lastIndex]);
            }
        }

        // 0x0600120d (0x1b349e0): read, let the callback edit the lists, write all.
        public static void ReadWriteLines(string path, ReadWriteLinesCallback1 overrideCallback)
        {
            Dictionary<string, List<string>> pathLinePairs = MultiReadLines(path);
            overrideCallback(pathLinePairs[path]);
            MultiWriteLines(pathLinePairs);
        }

        // 0x0600120e (0x1b34ce0).
        public static void ReadWriteLines(string path1, string path2, ReadWriteLinesCallback2 overrideCallback)
        {
            Dictionary<string, List<string>> pathLinePairs = MultiReadLines(path1, path2);
            overrideCallback(pathLinePairs[path1], pathLinePairs[path2]);
            MultiWriteLines(pathLinePairs);
        }

        // 0x0600120f (0x1b34dc0).
        public static void ReadWriteLines(string path1, string path2, string path3, ReadWriteLinesCallback3 overrideCallback)
        {
            Dictionary<string, List<string>> pathLinePairs = MultiReadLines(path1, path2, path3);
            overrideCallback(pathLinePairs[path1], pathLinePairs[path2], pathLinePairs[path3]);
            MultiWriteLines(pathLinePairs);
        }

        // 0x06001210 (0x1b34ec0).
        public static void ReadWriteLines(string path1, string path2, string path3, string path4, ReadWriteLinesCallback4 overrideCallback)
        {
            Dictionary<string, List<string>> pathLinePairs = MultiReadLines(path1, path2, path3, path4);
            overrideCallback(pathLinePairs[path1], pathLinePairs[path2], pathLinePairs[path3], pathLinePairs[path4]);
            MultiWriteLines(pathLinePairs);
        }

        // 0x06001211 (0x1b34ff0).
        public static void ReadWriteLines(string path1, string path2, string path3, string path4, string path5, ReadWriteLinesCallback5 overrideCallback)
        {
            Dictionary<string, List<string>> pathLinePairs = MultiReadLines(path1, path2, path3, path4, path5);
            overrideCallback(pathLinePairs[path1], pathLinePairs[path2], pathLinePairs[path3], pathLinePairs[path4], pathLinePairs[path5]);
            MultiWriteLines(pathLinePairs);
        }

        // 0x06001212 (0x1b34a80): Dictionary.Add, so a repeated path throws.
        public static Dictionary<string, List<string>> MultiReadLines(params string[] paths)
        {
            Dictionary<string, List<string>> pathLinePairs = new Dictionary<string, List<string>>();
            foreach (string path in paths)
            {
                pathLinePairs.Add(path, new List<string>(File.ReadAllLines(path)));
            }
            return pathLinePairs;
        }

        // 0x06001213 (0x1b34b90): File.WriteAllLines(string, IEnumerable<string>).
        public static void MultiWriteLines(Dictionary<string, List<string>> pathLinePairs)
        {
            foreach (KeyValuePair<string, List<string>> pathLinePair in pathLinePairs)
            {
                File.WriteAllLines(pathLinePair.Key, pathLinePair.Value);
            }
        }

        // 0x06001214 (0x1b35160). Trims, keeps any "scheme://" prefix, folds runs of
        // '/', drops "./" and resolves "../" against the slash stack (falling back
        // to the prefix). A trailing "/.." or "/." is then resolved once more; the
        // trailing "/.." pops twice and keeps the second slash.
        public static string SanitiseUnixPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            path = path.Trim();
            if (path.Equals("../", StringComparison.Ordinal) || path.Equals("/..", StringComparison.Ordinal)
                || path.Equals("./", StringComparison.Ordinal) || path.Equals("/.", StringComparison.Ordinal))
            {
                return "/";
            }

            int startIndex = 0;
            StringBuilder stringBuilder = new StringBuilder();
            int schemeIndex = path.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0)
            {
                startIndex = schemeIndex + "://".Length;
            }
            if (startIndex > 0)
            {
                stringBuilder.Append(path.Substring(0, startIndex));
            }

            Stack<int> slashIndices = new Stack<int>();
            bool appendSlash = false;
            int index = startIndex;
            while (index < path.Length)
            {
                if (path[index] == '/')
                {
                    if (path.ContainsAt(index + 1, "../"))
                    {
                        stringBuilder.Length = slashIndices.TryPop(out int slashIndex) ? slashIndex : startIndex;
                        index += 3;
                    }
                    else if (path.ContainsAt(index + 1, "./"))
                    {
                        index += 2;
                    }
                    else if (appendSlash)
                    {
                        index++;
                    }
                    else
                    {
                        appendSlash = true;
                    }
                }
                else
                {
                    if (appendSlash)
                    {
                        appendSlash = false;
                        slashIndices.Push(stringBuilder.Length);
                        stringBuilder.Append('/');
                    }
                    stringBuilder.Append(path[index]);
                    index++;
                }
            }

            if (appendSlash)
            {
                stringBuilder.Append('/');
            }

            int length = stringBuilder.Length;
            if (stringBuilder.ContainsAt(length - 3, "/.."))
            {
                slashIndices.TryPop(out _);
                stringBuilder.Length = slashIndices.TryPop(out int lastSlashIndex) ? lastSlashIndex + 1 : startIndex;
            }
            else if (stringBuilder.ContainsAt(length - 2, "/."))
            {
                stringBuilder.Length -= 1;
            }
            return stringBuilder.ToString();
        }
    }
}
