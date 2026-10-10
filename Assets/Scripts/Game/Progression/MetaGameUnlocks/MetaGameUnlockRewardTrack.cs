using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200079d, all seven authored APIs/three public readonly fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MetaGameUnlockRewardTrack : MetaGameUnlockBase
    {
        public readonly RewardTrackType RewardTrackType;
        public readonly int ChallengeXP;
        public readonly RewardTrackDefinition RewardTrackDefinition;

        // 0x06002acd publishes the definition before the primitive fields. x86
        // writes XP then track; ARM writes those two values together in one STP.
        public MetaGameUnlockRewardTrack(RewardTrackDefinition rewardTrackDefinition, int challengeXP, RewardTrackType rewardTrack)
        {
            RewardTrackDefinition = rewardTrackDefinition;
            ChallengeXP = challengeXP;
            RewardTrackType = rewardTrack;
        }
        public override string GetId() => RewardTrackDefinition.GetGUID(); // 0x06002ace
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.RewardTrack; // 0x06002acf, 0xf82b7531
        public override UIWidgetProgression GetWidget() => null; // 0x06002ad0, actual null result

        // 0x06002ad1: raise only a signed state below10, then request a save
        // even if no state changed. Do not set Collected or refresh a manager here.
        public override void Save(SaveManager saveManager)
        {
            SaveDataChallengeRewardTrack saveData = saveManager.CurrentSave.GetOrCreateRewardTrackData(RewardTrackDefinition.Type);
            if (saveData.UnlockState < SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked)
                saveData.UnlockState = SaveDataChallengeRewardTrack.FeatureUnlockState.Unlocked;
            saveManager.RequestSave();
        }
        public override void ReleaseAssets() => RewardTrackDefinition.IconAsset.Unload(); // 0x06002ad2

        // 0x06002ad3 loads synchronously before the required callback invocation;
        // a null callback still loads first and then faults. There is no null guard.
        public override void GetCustomSprite(Action<Sprite> onLoaded)
        {
            onLoaded(RewardTrackDefinition.IconAsset.Load());
        }
    }
}
