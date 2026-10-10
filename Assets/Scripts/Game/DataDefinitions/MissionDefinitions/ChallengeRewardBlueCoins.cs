using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ChallengeRewardBlueCoins", menuName = "HardlightProject/ChallengeReward/ChallengeRewardBlueCoins")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeRewardBlueCoins : ChallengeReward
    {
        [SerializeField] private int m_reward;
        private static CollectableDefinition s_definition_internal;

        // Game.Runtime 06001d86, 06001d87: the original reward category and authored amount.
        public override ChallengeRewardType RewardType => ChallengeRewardType.BlueCoins;
        public int BlueCoinAmount => m_reward;

        // 06001d88: reuse a GUID-aware non-null definition. An unavailable DataManager
        // returns the existing value; an available manager performs a required dictionary lookup.
        public static CollectableDefinition Definition
        {
            get
            {
                if (s_definition_internal != null) return s_definition_internal;
                if (s_dataManagerRef.TryGet(out DataManager dataManager))
                    s_definition_internal = dataManager.CollectableDefinitions[CollectableType.BlueCoin];
                return s_definition_internal;
            }
        }

        // 06001d89: the shipping validation method is an empty return.
        public override void Validate(UnityEngine.Object context) { }

        // 06001d8a: the supplied literal is "{0}_{1}". Preserve the virtual RewardType
        // lookup and String.Format's enum/int boxing and current-culture formatting.
        public override string GetAnalyticsName() => string.Format("{0}_{1}", RewardType, m_reward);
        // 06001d8b: use the collectable definition's GUID, rather than this reward's GUID.
        public override string GetRewardGUID() => Definition.GetGUID();
        // 06001d8c: the original reward does no immediate currency mutation here.
        public override void GiveReward() { }
        // 06001d8d: preserve the original localization enum's Int32 literal.
        public override Strings GetRewardName() => (Strings)0x207546e7;

        // 06001d8e: only the sprite callback triggers loading; the texture callback is unused.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            if (spriteCallback != null) spriteCallback(Definition.IconAsset.Load());
        }
        // 06001d8f: unload the same managed icon without a null-definition guard.
        public override void UnloadImage() => Definition.IconAsset.Unload();

        // 06001d90: mark this reward's existing saved record collected, request a save,
        // then recalculate progression. Keep the required record lookup and callback order.
        public override void Save()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            saveManager.CurrentSave.GetRewardDataByTrackGUIDUnsafe(GetGUID()).Collected = true;
            saveManager.RequestSave();
            ProcessManager.GetSystem<ProgressionManager>(null, true).CacheChallengeProgress();
        }

        // 06001d91: the unlock retains the collectable definition, amount, XP and track.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType) =>
            new MetaGameUnlockBlueCoins(Definition, m_reward, m_xpThreshold, trackType);

        // 06001d92: preserve both original telemetry calls in their authored order.
        // Offline routing is implemented separately from this reconstructed source.
        public override void FireAnalyticsEvents(RewardTrackType rewardTrackType, int rewardNumber)
        {
            base.FireAnalyticsEvents(rewardTrackType, rewardNumber);
            Hardlight.Analytics.AnalyticsEventCollector.RewardTrackBlueCoinsAwardedEvent(this);
        }
        // 06001d93: the original default widget size is Small.
        protected override UIWidgetButtonChallengeRewardSize GetDefaultRewardSize() => UIWidgetButtonChallengeRewardSize.Small;
        // 06001d94: no instance field initializer precedes the genuine ChallengeReward base.
        public ChallengeRewardBlueCoins() { }
    }
}
