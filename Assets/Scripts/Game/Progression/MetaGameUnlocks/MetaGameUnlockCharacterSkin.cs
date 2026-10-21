using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000793, all ten authored APIs and three fields.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MetaGameUnlockCharacterSkin : MetaGameUnlockBase
    {
        // 0x06002a7a..2a7c: readonly auto-property fields in original order.
        public CharacterSkinDefinition CharacterSkinDefinition { get; }
        public int ChallengeXP { get; }
        public RewardTrackType RewardTrackType { get; }

        // 0x06002a7d: base first, then skin, XP, requested track; no validation.
        public MetaGameUnlockCharacterSkin(CharacterSkinDefinition characterSkinDefinition, int challengeXP, RewardTrackType trackType)
        {
            CharacterSkinDefinition = characterSkinDefinition;
            ChallengeXP = challengeXP;
            RewardTrackType = trackType;
        }

        public override string GetId() => CharacterSkinDefinition.GetGUID(); // 0x06002a7e

        // 0x06002a7f: original key 0xdd0b5760.
        public override PlayerProgressionTypes GetProgressionType() => PlayerProgressionTypes.CharacterSkin;

        // 0x06002a80: a skin has no unlock widget.
        public override UIWidgetProgression GetWidget() => null;

        // 0x06002a81: authentic empty body.
        public override void Save(SaveManager saveManager) { }

        // 0x06002a82/2a83 use the skin's thumbnail wrapper; a null callback is forwarded unchanged.
        public override void GetCustomSprite(Action<Sprite> onLoaded)
        {
            CharacterSkinDefinition.Thumbnail.LoadAsync(onLoaded);
        }

        public override void ReleaseAssets() => CharacterSkinDefinition.Thumbnail.Unload();
    }
}
