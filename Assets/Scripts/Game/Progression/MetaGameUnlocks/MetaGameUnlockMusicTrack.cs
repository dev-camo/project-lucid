using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200079b, all ten authored APIs and three fields.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockMusicTrack : MetaGameUnlockBase
    {
        // 0x06002ab4..2ab6: readonly auto-property fields in original order.
        public MusicTrackDefinition MusicTrackDefinition { get; }
        public int ChallengeXP { get; }
        public RewardTrackType RewardTrackType { get; }

        // 0x06002ab7: base first, then definition, XP, requested track. There is
        // no Adventure initializer, validation, clamp, or extra default argument.
        public MetaGameUnlockMusicTrack(MusicTrackDefinition musicTrackDefinition, int challengeXP, RewardTrackType rewardTrackType)
        {
            MusicTrackDefinition = musicTrackDefinition;
            ChallengeXP = challengeXP;
            RewardTrackType = rewardTrackType;
        }
        public override string GetId() => MusicTrackDefinition.GetGUID(); // 0x06002ab8
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.MusicTrack; // 0x06002ab9, 0x2b976ee6
        public override UIWidgetProgression GetWidget() => MusicTrackDefinition.ProgressionUnlockWidget; // 0x06002aba
        public override void Save(SaveManager saveManager) { } // 0x06002abb, authentic empty body

        // 0x06002abc/2abd use the exact texture wrapper at definition offset0x60.
        // Loading remains asynchronous and forwards a null callback unchanged.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            MusicTrackDefinition.ImageAssetTexture.LoadAsync(onLoaded);
        }
        public override void ReleaseAssets() => MusicTrackDefinition.ImageAssetTexture.Unload();
    }
}
