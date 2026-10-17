using System.IO;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000268 (06000f3b), body from the 1.10.1
    // x86-64 binary.
    public static class DirectoryUtilities
    {
        // 0x06000f3b (0x1b1ce60): subdirectories first, each with the caller's
        // recursive flag, then the files; the directory itself is kept.
        public static void Clear(string path, bool recursive = false)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            foreach (DirectoryInfo directory in directoryInfo.GetDirectories())
            {
                directory.Delete(recursive);
            }
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
        }
    }
}
