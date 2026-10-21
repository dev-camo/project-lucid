using System;
using System.Collections.Generic;
using Hardlight.Enums;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.UIContainerShared
{
    // Test-only ChallengeReward that records every call a reward track status makes on it, with the arguments, so the status's
    // delegation can be checked. The authored name and images are what a case sets; no service is touched by any member.
    // Editor-only assemblies cannot supply a ScriptableObject that CreateInstance can build.
    public sealed class UIChallengeTrackRewardRecordingReward : ChallengeReward
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<(RewardTrackType trackType, int rewardNumber)> AnalyticsEvents = new List<(RewardTrackType, int)>();
        public readonly List<RewardTrackType> UnlockRequests = new List<RewardTrackType>();
        public Strings Name;
        public Texture Texture;
        public Sprite Sprite;
        public IMetaGameUnlock Unlock;

        // Runs inside Save, so a case can read the state of the object that called it at that moment.
        public Action OnSave;

        public override ChallengeRewardType RewardType => ChallengeRewardType.GameFeature;
        public override void Validate(UnityEngine.Object context) { }
        public override string GetAnalyticsName() => "lucid-recording-reward";
        public override string GetRewardGUID() => "lucid-recording-reward-guid";
        public override void GiveReward() { }

        public override Strings GetRewardName()
        {
            Calls.Add("GetRewardName");
            return Name;
        }

        // Both callbacks are invoked, so a case sees which one the caller wired to which target.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            Calls.Add("LoadImage");
            textureCallback(Texture);
            spriteCallback(Sprite);
        }

        public override void UnloadImage() => Calls.Add("UnloadImage");

        public override void Save()
        {
            Calls.Add("Save");
            OnSave?.Invoke();
        }

        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            UnlockRequests.Add(trackType);
            return Unlock;
        }

        public override void FireAnalyticsEvents(RewardTrackType rewardTrackType, int rewardNumber)
        {
            AnalyticsEvents.Add((rewardTrackType, rewardNumber));
        }
    }
}
