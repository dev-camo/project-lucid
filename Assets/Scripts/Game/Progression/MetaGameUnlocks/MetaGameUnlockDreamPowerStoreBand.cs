using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000795, all eight authored APIs and three public fields.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameUnlockDreamPowerStoreBand : MetaGameUnlockBase
    {
        public readonly DreamPowerStoreBandDefinition Definition;
        public readonly int ChallengeXP;
        public readonly RewardTrackType RewardTrackType;

        // 0x06002a8a: base first, then definition, XP, requested track; no validation.
        public MetaGameUnlockDreamPowerStoreBand(DreamPowerStoreBandDefinition definition, int challengeXP, RewardTrackType rewardTrackType)
        {
            Definition = definition;
            ChallengeXP = challengeXP;
            RewardTrackType = rewardTrackType;
        }

        public override string GetId() => Definition.GetGUID(); // 0x06002a8b

        // 0x06002a8c: Band2/Band3/Band4 map to their own keys; every other band
        // type, including None, falls back to DreamPowerStoreBand1.
        public override PlayerProgressionTypes GetProgressionType()
        {
            switch (Definition.Type)
            {
                case DreamPowerStoreBandType.Band2:
                    return PlayerProgressionTypes.DreamPowerStoreBand2;
                case DreamPowerStoreBandType.Band3:
                    return PlayerProgressionTypes.DreamPowerStoreBand3;
                case DreamPowerStoreBandType.Band4:
                    return PlayerProgressionTypes.DreamPowerStoreBand4;
                default:
                    return PlayerProgressionTypes.DreamPowerStoreBand1;
            }
        }

        public override UIWidgetProgression GetWidget() => Definition.ProgressionUnlockWidget; // 0x06002a8d

        // 0x06002a8e: authentic empty body.
        public override void Save(SaveManager saveManager) { }

        // 0x06002a8f: the callback is invoked at once with null, with no null guard.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            onLoaded(null);
        }

        // 0x06002a90/2a91 use the band's sprite wrapper; a null callback is forwarded unchanged.
        public override void GetCustomSprite(Action<Sprite> onLoaded)
        {
            Definition.ImageAsset.LoadAsync(onLoaded);
        }

        public override void ReleaseAssets() => Definition.ImageAsset.Unload();
    }
}
