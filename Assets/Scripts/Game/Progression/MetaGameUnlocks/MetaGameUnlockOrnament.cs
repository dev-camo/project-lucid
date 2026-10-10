using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class MetaGameUnlockOrnament : MetaGameUnlockBase
    {
        // Original Game.Runtime06002abe..2ac2: five readonly auto-property fields in shipped order.
        public OrnamentDefinition OrnamentDefinition { get; }
        public MissionDefinition MissionDefinition { get; }
        public AchievementIdentifier AchievementIdentifier { get; }
        public int ChallengeXP { get; }
        public RewardTrackType RewardTrackType { get; } = HardlightProject.RewardTrackType.Adventure;

        // Original06002ac3: default Adventure field precedes the base-only constructor.
        private MetaGameUnlockOrnament(OrnamentDefinition ornamentDefinition)
        {
            OrnamentDefinition = ornamentDefinition;
        }

        // Original06002ac4/5/6 retain constructor chaining and post-ornament assignments.
        public MetaGameUnlockOrnament(OrnamentDefinition ornamentDefinition, MissionDefinition missionDefinition)
            : this(ornamentDefinition)
        {
            MissionDefinition = missionDefinition;
        }

        public MetaGameUnlockOrnament(OrnamentDefinition ornamentDefinition, AchievementIdentifier achievementId)
            : this(ornamentDefinition)
        {
            AchievementIdentifier = achievementId;
        }

        public MetaGameUnlockOrnament(OrnamentDefinition ornamentDefinition, int challengeXP, RewardTrackType rewardTrackType)
            : this(ornamentDefinition)
        {
            ChallengeXP = challengeXP;
            RewardTrackType = rewardTrackType;
        }

        // Original06002ac7: inherited actual GUID getter; no own Guid alias.
        public override string GetId() => OrnamentDefinition.GetGUID();

        // Original06002ac8: original hash 0x93382a01 selects Ornament.
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.Ornament;

        // Original06002ac9 forwards the original widget getter, without a null guard.
        public override UIWidgetProgression GetWidget() => OrnamentDefinition.ProgressionUnlockWidget;

        // Original06002aca: authentic empty save body.
        public override void Save(SaveManager saveManager)
        {
        }

        // Original06002acb: uncropped validity first; original definition is reread for selection.
        // Load executes even when the callback is null and precedes callback invocation.
        public override void GetCustomTexture(Action<Texture> onLoaded)
        {
            var texture = (OrnamentDefinition.ThumbnailUncroppedAsset.IsValid()
                ? OrnamentDefinition.ThumbnailUncroppedAsset : OrnamentDefinition.ThumbnailAsset).Load();
            onLoaded?.Invoke(texture);
        }

        // Original06002acc reevaluates validity and unloads that selected original asset.
        public override void ReleaseAssets()
        {
            (OrnamentDefinition.ThumbnailUncroppedAsset.IsValid()
                ? OrnamentDefinition.ThumbnailUncroppedAsset : OrnamentDefinition.ThumbnailAsset).Unload();
        }
    }
}
