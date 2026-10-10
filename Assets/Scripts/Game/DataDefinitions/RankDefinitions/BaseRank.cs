using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class BaseRank
    {
        [SerializeField] protected RankType m_rankType;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);

        // Original Game.Runtime 0x06001eca, ARM64 0x52c784..0x52c78c.
        public RankType RankType => m_rankType;

        // Original 0x06001ecb..0x06001ece, ARM64 0x52c78c..0x52c984.
        // Native code queries the real manager and dictionary on every call;
        // missing registration/key/null values retain their original failures.
        public ManagedAddressableAsset<Sprite> GetBaseAddressable()
        {
            return m_dataManagerRef.Get().RankDefinitions[m_rankType].BaseAddressable;
        }

        public ManagedAddressableAsset<Sprite> GetBackgroundAddressable()
        {
            return m_dataManagerRef.Get().RankDefinitions[m_rankType].BackgroundAddressable;
        }

        public ManagedAddressableAsset<Sprite> GetForegroundAddressable()
        {
            return m_dataManagerRef.Get().RankDefinitions[m_rankType].ForegroundAddressable;
        }

        public RankDefinition GetRankTypeDefinition()
        {
            return m_dataManagerRef.Get().RankDefinitions[m_rankType];
        }

        // Implicit original 0x06001ecf, ARM64 0x52c984..0x52ca1c: the genuine
        // GetSystemRef call and readonly field assignment precede Object..ctor.
    }
}
