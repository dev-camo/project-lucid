using System;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000578, complete twelve-method owner.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeRewardCharacterArchetype", menuName = "HardlightProject/ChallengeReward/ChallengeRewardCharacterArchetype")]
    public class ChallengeRewardCharacterArchetype : ChallengeReward
    {
        [SerializeField] private CharacterArchetypeDefinition m_archetypeDefinition;

        public override ChallengeRewardType RewardType => ChallengeRewardType.CharacterArchetype; // 0x06001d97
        public CharacterArchetypeDefinition ArchetypeDefinition => m_archetypeDefinition; // 0x06001d98
        public override void Validate(UnityEngine.Object context) { } // 0x06001d99

        // 0x06001d9a: virtual reward type first, then the archetype's identifier string.
        public override string GetAnalyticsName()
        {
            return string.Format("{0}_{1}", RewardType, m_archetypeDefinition.CharacterArchetype.GetString());
        }

        public override string GetRewardGUID() => m_archetypeDefinition.GetGUID(); // 0x06001d9b

        // 0x06001d9c: unlocks the archetype directly in the current save.
        public override void GiveReward()
        {
            s_saveManagerRef.Get().CurrentSave.UnlockCharacterArchetype(m_archetypeDefinition.CharacterArchetype);
        }

        // 0x06001d9d: the name of the first character of this archetype, in
        // dictionary order; None when no character has it.
        public override Strings GetRewardName()
        {
            foreach (var (_, characterDefinition) in s_dataManagerRef.Get().Characters)
            {
                if (characterDefinition.Archetype == m_archetypeDefinition.CharacterArchetype)
                    return characterDefinition.Name;
            }
            return Strings.NONE;
        }

        // 0x06001d9e/1d9f use the texture wrapper only; the sprite callback is ignored.
        public override void LoadImage(Action<Texture> textureCallback, Action<Sprite> spriteCallback)
        {
            m_archetypeDefinition.Texture.LoadAsync(textureCallback);
        }

        public override void UnloadImage() => m_archetypeDefinition.Texture.Unload();

        // 0x06001da0: unlock the archetype, then mark this reward collected (found by
        // the virtual reward GUID), then request the save.
        public override void Save()
        {
            SaveManager saveManager = s_saveManagerRef.Get();
            SaveDataGame saveData = saveManager.CurrentSave;
            saveData.GetOrCreateCharacterArchetypeData(m_archetypeDefinition.CharacterArchetype).Unlocked = true;
            saveData.GetRewardDataByRewardGUIDUnsafe(GetRewardGUID()).Collected = true;
            saveManager.RequestSave();
        }

        // 0x06001da1: the track type is ignored.
        public override IMetaGameUnlock GetMetaGameUnlock(RewardTrackType trackType)
        {
            return new MetaGameUnlockCharacterArchetype(m_archetypeDefinition);
        }

        // 0x06001da2: base-only constructor.
        public ChallengeRewardCharacterArchetype() { }
    }
}
