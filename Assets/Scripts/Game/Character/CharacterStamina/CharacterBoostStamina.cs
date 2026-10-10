using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterBoostStamina : CharacterStamina
    {
        // 060014a8 / ARM74a4d8. Restore switch values only after real base setup.
        public CharacterBoostStamina(Character character) : base(character) => SetSwitchStamina();

        // 060014a9 / ARM74a7ac. All three consuming reads occur unconditionally
        // before the result conjunction; no short-circuit lookup is introduced.
        private void SetSwitchStamina()
        {
            bool hasStamina = TryGetValue(ActorFSMKeys.BoostSwitchStamina, out float stamina);
            bool hasRatio = TryGetValue(ActorFSMKeys.BoostSwitchRatio, out float ratio);
            bool hasAbility = TryGetValue(ActorFSMKeys.BoostSwitchAbility, out ActorAbilityType abilityType);
            if (hasStamina && hasRatio && hasAbility &&
                m_character.TryGetAbilityBoostDefinition(abilityType, out Definition definition))
                Stamina = stamina * Mathf.Min(ratio / definition.SwitchRatio, 1f);
        }

        // 060014aa / ARM74a8f0. Reload the real character/storage and definition
        // at each ordered write, retaining any intervening callback mutation.
        public void CacheFSMValues()
        {
            if (m_definition == null) return;
            m_character.Storage.SetValue(ActorFSMKeys.BoostSwitchStamina, Stamina);
            m_character.Storage.SetValue(ActorFSMKeys.BoostSwitchRatio, m_definition.SwitchRatio);
            m_character.Storage.SetValue(ActorFSMKeys.BoostSwitchAbility, m_abilityType);
        }

        // 060014ab / ARM74ab68: native hash0xaede58f3, genuine enum literal.
        public override bool IsCollectableType(CollectableType collectableType) => collectableType == CollectableType.BoostEnergy;
    }
}
