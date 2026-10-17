using System;
using System.Collections.Generic;
using System.IO;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x02000214, methods 0x06000d89..0x06000d93.
    // The implicit constructor 0x06000d91 is the PerformanceProfile list
    // initializer followed by the ScriptableObjectWithGuid constructor.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PerformanceProfileCustom : PerformanceProfile
    {
        public IReadOnlyList<PerformanceAttribute> Attributes => m_attributes;

        // 0x06000d8a: appends one Medium attribute per feature; existing rows stay.
        public void SetAttributes(IReadOnlyList<ScalableFeature> scalableFeatures)
        {
            for (int i = 0; i < scalableFeatures.Count; i++)
                m_attributes.Add(new PerformanceAttribute { Feature = scalableFeatures[i], Level = QualityLevel.Medium });
        }

        // 0x06000d8b: the level is boxed, so it prints its enum name.
        public string GetAttributeSetting(PerformanceAttribute attribute)
        {
            return string.Format("{0} ({1})", attribute.Feature.ID, attribute.Level);
        }

        // 0x06000d8c and closure 0x06000d93: step to the next declared level,
        // wrapping to the first. An undeclared level finds -1 and also lands on
        // the first value.
        public void UpdateAttribute(PerformanceAttribute attribute)
        {
            QualityLevel[] qualityLevels = Enum.GetValues(typeof(QualityLevel)) as QualityLevel[];
            int index = Array.FindIndex(qualityLevels, qualityLevel => qualityLevel == attribute.Level) + 1;
            if (index == qualityLevels.Length)
                index = 0;
            attribute.Level = qualityLevels[index];
        }

        // 0x06000d8d: activate this profile by name before writing it (Save inlined).
        public void UpdateAndSaveProfile(string path)
        {
            ProcessManager.GetSystem<ScalablePerformance>().SetPerformanceProfile(name);
            Save(path);
        }

        // 0x06000d8e: a missing file leaves the fresh, attribute-free instance.
        public static PerformanceProfileCustom LoadOrCreate(string path, string name)
        {
            PerformanceProfileCustom profile = CreateInstance<PerformanceProfileCustom>();
            profile.name = name;
            if (File.Exists(path))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), profile);
            return profile;
        }

        // 0x06000d8f.
        private void Save(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(this));
        }

        // 0x06000d90.
        public static void DeleteFrom(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            else
                HLOutput.LogError("No saved profile found at " + path);
        }
    }
}
