using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterActionSettingsSubscription
    {
        public CharacterActionSubscription ActionPrimary { get; private set; } // Original 0600130e..0f.
        public CharacterActionSubscription ActionSecondary { get; private set; } // Original 06001310..11.
        private readonly ButtonMappingFunc m_buttonMappingFunc;

        // Original nested 02000319/06001314..17.
        public delegate ButtonMappingType ButtonMappingFunc(GameAction action);

        // Original 06001312 plus display owner 0200031a/06001318..1a.
        // Callback storage occurs after both owned action subscriptions are constructed.
        public CharacterActionSettingsSubscription(GameAction action, CharacterBrain brain, GameControlsBinding bindingPrimary, GameControlsBinding bindingSecondary, ButtonMappingFunc buttonMappingFunc)
        {
            ActionPrimary = new CharacterActionSubscription(action, brain, bindingPrimary, () => GameInputValidator(action, ButtonMappingType.Primary));
            ActionSecondary = new CharacterActionSubscription(action, brain, bindingSecondary, () => GameInputValidator(action, ButtonMappingType.Secondary));
            m_buttonMappingFunc = buttonMappingFunc;
        }

        // Original 06001313 calls the mapping delegate once.
        private bool GameInputValidator(GameAction action, ButtonMappingType validMapping)
        {
            ButtonMappingType mapping = m_buttonMappingFunc(action);
            return mapping == validMapping || mapping == ButtonMappingType.All;
        }

    }
}
