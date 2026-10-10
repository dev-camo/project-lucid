using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockBlueCoins : MetaGameUnlockBase
    {
        // Game.Runtime 06002a63, 06002a64: preserve the readonly compiler-generated
        // backing fields and the original private property's lower-case identity.
        public int Amount { get; }
        private CollectableDefinition m_blueCoinDefinition { get; }
        public readonly int ChallengeXP;
        public readonly RewardTrackType RewardTrackType;

        // 06002a65: genuine base construction completes before the definition reference
        // and raw counts are stored. There is no validation, clamping or save mutation.
        public MetaGameUnlockBlueCoins(CollectableDefinition blueCoinDefinition, int amount, int challengeXP, RewardTrackType rewardTrackType)
        {
            m_blueCoinDefinition = blueCoinDefinition;
            Amount = amount;
            ChallengeXP = challengeXP;
            RewardTrackType = rewardTrackType;
        }

        // 06002a66, 06002a68: required definition accesses retain the original null fault.
        public override string GetId() => m_blueCoinDefinition.GetGUID();
        // 06002a67: the original progression enum's serialized Int32 literal.
        public override PlayerProgressionTypes GetProgressionType() => unchecked((PlayerProgressionTypes)0x862ff4c5);
        public override UIWidgetProgression GetWidget() => m_blueCoinDefinition.ProgressionUnlockWidget;
        // 06002a69: the shipped unlock Save body returns without modifying the save.
        public override void Save(SaveManager saveManager) { }
        // 06002a6a: forward the callback unchanged to the original managed icon loader.
        public override void GetCustomSprite(Action<Sprite> onLoaded) => m_blueCoinDefinition.IconAsset.LoadAsync(onLoaded);
        // 06002a6b: release the same icon without checking definition or wrapper for null.
        public override void ReleaseAssets() => m_blueCoinDefinition.IconAsset.Unload();
    }
}
