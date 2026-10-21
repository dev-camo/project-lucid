using System;
using System.Collections;
using System.IO;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020000ad; bodies 0x534F50..0x5352C0 (1.10.1 x86-64).
    // Build stamp written beside the streaming assets and read back by ApplicationInfoView.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class ApplicationInfo
    {
        private static readonly string FileName = "ApplicationInfo.json";
        private static readonly string Location = Application.streamingAssetsPath + Path.DirectorySeparatorChar.ToString() + FileName;

        public string BuildCode;
        public long BuildTime;

        // 06000594
        public static void WriteToFile(string buildCode)
        {
            ApplicationInfo info = new ApplicationInfo { BuildCode = buildCode, BuildTime = DateTime.Now.ToFileTimeUtc() };
            string json = JsonUtility.ToJson(info);
            using (StreamWriter writer = new StreamWriter(Location))
            {
                writer.Write(json);
            }
        }

        // 06000595
        public static ApplicationInfo CreateDefault()
        {
            return new ApplicationInfo { BuildCode = "n/a", BuildTime = DateTime.Now.ToFileTimeUtc() };
        }

        // 06000596 / MoveNext 0600059d: the file is read through the streaming-assets importer
        // when present; otherwise the callback receives a default stamp without yielding.
        public static IEnumerator LoadOrCreateDefault(Action<ApplicationInfo> callback)
        {
            if (File.Exists(Location))
            {
                yield return JSONExtensions.ImportJsonFile(FileName, jsonData => callback?.Invoke(JsonUtility.FromJson<ApplicationInfo>(jsonData)));
            }
            else
            {
                callback?.Invoke(CreateDefault());
            }
        }
    }
}
