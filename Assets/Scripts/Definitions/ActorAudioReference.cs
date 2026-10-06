using System;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorAudioReference : ISerializationCallbackReceiver
    {
        [HideInInspector] public string Name;
        [HashEnum(typeof(ActorAudioTypes))] public ActorAudioTypes AudioType;
        public SelectionModeType SelectionMode;
        [HashEnum(typeof(HLAudioClipIdentifier))] [HideInInspector]
        public HLAudioClipIdentifier[] Clips = Array.Empty<HLAudioClipIdentifier>();
        public ClipWithWeight[] WeightedClips = Array.Empty<ClipWithWeight>();
        private const int LastUsedClipIndexDefault = -1;
        private int m_lastUsedClipIndex = LastUsedClipIndexDefault;

        // 0x06001985: a present zero clip remains a nullable value. Single-clip
        // selection stores index zero and bypasses the multi-clip repeat reset.
        public HLAudioClipIdentifier? Get(LevelSetupTypes currentLevelType)
        {
            if (WeightedClips == null || WeightedClips.Length == 0) return null;
            if (WeightedClips.Length == 1)
            {
                m_lastUsedClipIndex = 0;
                return WeightedClips[0].GetClipID(currentLevelType);
            }
            int index = 0;
            switch (SelectionMode)
            {
                case SelectionModeType.InOrder:
                    index = unchecked(m_lastUsedClipIndex + 1) % WeightedClips.Length;
                    break;
                case SelectionModeType.Random:
                    index = GetClipIndex(GetSelectedWeight());
                    break;
                case SelectionModeType.RandomNoRepeat:
                    do { index = GetClipIndex(GetSelectedWeight()); }
                    while (index == m_lastUsedClipIndex);
                    break;
            }
            m_lastUsedClipIndex = index;
            HLAudioClipIdentifier clip = WeightedClips[index].GetClipID(currentLevelType);
            if ((int)clip == 0 && SelectionMode == SelectionModeType.RandomNoRepeat)
                m_lastUsedClipIndex = LastUsedClipIndexDefault;
            return clip;
        }
        // 0x06001986: sum in authored order; Random.Range is the real engine call.
        private float GetSelectedWeight()
        {
            float weight = 0f;
            foreach (ClipWithWeight clip in WeightedClips) weight += clip.ProbabilityWeighting;
            return UnityEngine.Random.Range(0f, weight);
        }
        // 0x06001987: open intervals intentionally exclude exact weight boundaries.
        private int GetClipIndex(float selectedWeight)
        {
            float weight = 0f;
            ClipWithWeight[] clips = WeightedClips;
            for (int i = 0; i < clips.Length; ++i)
            {
                float previousWeight = weight;
                weight += clips[i].ProbabilityWeighting;
                if (previousWeight < selectedWeight && weight > selectedWeight) return i;
            }
            return 0;
        }
        // 0x06001988: this outer callback uses Enum.ToString rather than the
        // standalone child callbacks' registry names. It visits every override.
        public void OnBeforeSerialize()
        {
            Name = AudioType.ToString();
            foreach (ClipWithWeight clip in WeightedClips)
            {
                clip.Name = clip.ClipID.ToString();
                foreach (LevelSpecificOverride level in clip.LevelSpecificOverrides)
                    level.Name = level.LevelType.ToString();
            }
        }
        // 0x06001989: the same ordered name refresh, with no legacy Clips migration.
        public void OnAfterDeserialize()
        {
            Name = AudioType.ToString();
            foreach (ClipWithWeight clip in WeightedClips)
            {
                clip.Name = clip.ClipID.ToString();
                foreach (LevelSpecificOverride level in clip.LevelSpecificOverrides)
                    level.Name = level.LevelType.ToString();
            }
        }
        // 0x0600198a: two shared empty arrays, then last-index -1; base-only body.
        public enum SelectionModeType { InOrder = 0, Random = 1, RandomNoRepeat = 2 }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class ClipWithWeight : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            [HashEnum(typeof(HLAudioClipIdentifier))] public HLAudioClipIdentifier ClipID;
            public float ProbabilityWeighting = 1f;
            public bool HasLevelSpecificOverride;
            [ShowIf("HasLevelSpecificOverride", (string)null)]
            public LevelSpecificOverride[] LevelSpecificOverrides;
            // 0x0600198b: the first matching override wins, including zero IDs.
            public HLAudioClipIdentifier GetClipID(LevelSetupTypes currentLevelType)
            {
                if (HasLevelSpecificOverride)
                    foreach (LevelSpecificOverride level in LevelSpecificOverrides)
                        if (level.LevelType == currentLevelType) return level.ClipID;
                return ClipID;
            }
            public void OnBeforeSerialize() { Name = ClipID.GetString(); } // 0x0600198c
            public void OnAfterDeserialize() { Name = ClipID.GetString(); } // 0x0600198d
            // 0x0600198e: weight one; override array remains null.
        }
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class LevelSpecificOverride : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            public HLAudioClipIdentifier ClipID;
            public LevelSetupTypes LevelType;
            public void OnBeforeSerialize() { Name = LevelType.GetString(); } // 0x0600198f
            public void OnAfterDeserialize() { Name = LevelType.GetString(); } // 0x06001990
            // 0x06001991: base-only, with no inferred array/default enum values.
        }
    }
}
