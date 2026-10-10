using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime.dll 0x02000574. Abstract reward-specific APIs stay
    // abstract; platform event collection remains at the original boundary.
    [CreateAssetMenu(fileName = "ChallengeReward",
        menuName = "HardlightProject/DefinitionData/Definitions/ChallengeReward")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class ChallengeReward : ScriptableObjectWithGuid
    {
        protected const string AssetMenu = "HardlightProject/ChallengeReward/";
        protected static readonly SystemRef<DataManager> s_dataManagerRef =
            ProcessManager.GetSystemRef<DataManager>(null, true);
        protected static readonly SystemRef<SaveManager> s_saveManagerRef =
            ProcessManager.GetSystemRef<SaveManager>(null, true);

        [Min(1f), SerializeField] protected int m_xpThreshold;
        [SerializeField] protected UIWidgetButtonChallengeRewardSize m_rewardSize;

        // 0x06001d76 and 0x06001d81 read serialized values directly. The Min
        // attribute supplies no runtime clamp or constructor initialization.
        public int XPThreshold => m_xpThreshold;
        public abstract ChallengeRewardType RewardType { get; }
        public abstract void Validate(UnityEngine.Object context);
        public abstract string GetAnalyticsName();
        public abstract string GetRewardGUID();
        public abstract void GiveReward();
        public abstract Strings GetRewardName();
        public abstract void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback);
        public abstract void UnloadImage();
        public abstract void Save();
        public abstract IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType);
        public UIWidgetButtonChallengeRewardSize Size => m_rewardSize;

        // 0x06001d82: preserve the original static collector call. Offline
        // routing belongs outside this reconstructed method.
        public virtual void FireAnalyticsEvents(RewardTrackType rewardTrackType, int rewardNumber)
        {
            Hardlight.Analytics.AnalyticsEventCollector.RewardTrackClaimedEvent(
                this, rewardTrackType, rewardNumber);
        }

        // 0x06001d83 returns the original hashed Medium value 0xc9747dce.
        // Size does not call this virtual default method.
        protected virtual UIWidgetButtonChallengeRewardSize GetDefaultRewardSize()
        {
            return UIWidgetButtonChallengeRewardSize.Medium;
        }

        // 0x06001d84 is base-only. Original 0x06001d85 initializes the two
        // static references in the field order above, using null and true.
        protected ChallengeReward() { }

        // Original public nested enum 0x02000575, Int32 values from metadata.
        public enum ChallengeRewardType
        {
            Ornament = 0,
            BlueCoins = 1,
            MusicTrack = 2,
            DreamPowerStoreBand = 3,
            RewardTrack = 4,
            CharacterArchetype = 5,
            GameplayLevel = 6,
            Skin = 7,
            GameFeature = 8
        }
    }
}
