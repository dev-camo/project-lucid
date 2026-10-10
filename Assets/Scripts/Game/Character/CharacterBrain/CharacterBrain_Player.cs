using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterBrain_Player : CharacterBrain
    {
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly List<InputSubscription> m_inputSubscriptions = new List<InputSubscription>();
        private readonly CharacterControlsMappingDefinition m_controlsMappingDefinition;

        // Original 060012ff: SystemRef.Get precedes the authentic save-settings getter.
        private SaveDataSettings SaveDataSettings => m_saveManagerRef.Get().GetSaveDataSettings();

        // Original 06001300: the two field initializers precede the original base constructor.
        public CharacterBrain_Player(CharacterControlsMappingDefinition controlsMappingDefinition)
        {
            m_controlsMappingDefinition = controlsMappingDefinition;
        }

        // Original 06001301: movement subscriptions precede mapping enumeration.
        private void SubscribeInputs()
        {
            SubscribeCompositeMovementInput(GameInput.CharacterMovementComposite);
            SubscribeMovementInput(GameInput.Left, Vector2.left);
            SubscribeMovementInput(GameInput.Right, Vector2.right);
            SubscribeMovementInput(GameInput.Up, Vector2.up);
            SubscribeMovementInput(GameInput.Down, Vector2.down);
            foreach (var mapping in m_controlsMappingDefinition.ControlsMapping)
            {
                GameAction action = mapping.Action;
                GameControlsMappingDefinition primary = mapping.Mapping;
                GameControlsMappingDefinition secondary = mapping.MappingSecondary;
                bool hasSecondary = secondary != null;
                // The Unity object comparison is captured once; device lists are read per call.
                var secondaryGamepad = hasSecondary ? secondary.GamepadInputs : null;
                SubscribeDeviceMapping(action, primary.GamepadInputs, secondaryGamepad);
                var secondaryKeyboard = hasSecondary ? secondary.KeyboardInputs : null;
                SubscribeDeviceMapping(action, primary.KeyboardInputs, secondaryKeyboard);
                var secondaryTouch = hasSecondary ? secondary.TouchInputs : null;
                SubscribeDeviceMapping(action, primary.TouchInputs, secondaryTouch);
            }
        }

        // Original 06001302: both counts are captured before iteration; only primary count bounds it.
        private void SubscribeDeviceMapping(GameAction action, IReadOnlyList<GameControlsBinding> deviceBindings,
            IReadOnlyList<GameControlsBinding> deviceBindingSecondary)
        {
            int count = deviceBindings.Count;
            int secondaryCount = deviceBindingSecondary == null ? 0 : deviceBindingSecondary.Count;
            for (int i = 0; i < count; ++i)
            {
                GameControlsBinding binding = deviceBindings[i];
                if (i < secondaryCount)
                    SubscribeActionInputs(action, binding, deviceBindingSecondary[i],
                        new CharacterActionSettingsSubscription.ButtonMappingFunc(GameActionButtonMapping));
                else
                    SubscribeActionInput(action, binding);
            }
        }

        // Original 06001303.
        private void SubscribeActionInput(GameAction action, GameControlsBinding binding)
        {
            SubscribeInput(new CharacterActionSubscription(action, this, binding));
        }

        // Original 06001304: retain the first subscription if its callback or the second step faults.
        private void SubscribeActionInputs(GameAction action, GameControlsBinding bindingPrimary,
            GameControlsBinding bindingSecondary, CharacterActionSettingsSubscription.ButtonMappingFunc buttonMappingFunc)
        {
            var subscription = new CharacterActionSettingsSubscription(action, this, bindingPrimary, bindingSecondary, buttonMappingFunc);
            SubscribeInput(subscription.ActionPrimary);
            SubscribeInput(subscription.ActionSecondary);
        }

        // Original 06001305.
        private void SubscribeMovementInput(GameInput gameInput, Vector2 direction)
        {
            SubscribeInput(new CharacterMovementSubscription(this, new GameControlsBinding(gameInput), direction));
        }

        // Original 06001306.
        private void SubscribeCompositeMovementInput(GameInput gameInput)
        {
            SubscribeInput(new CharacterCompositeMovementSubscription(this, new GameControlsBinding(gameInput)));
        }

        // Original 06001307: append precedes virtual dispatch, including the null/fault prefix.
        private void SubscribeInput(InputSubscription inputSubscription)
        {
            m_inputSubscriptions.Add(inputSubscription);
            inputSubscription.Subscribe();
        }

        // Original 06001308: enumeration/disposal must finish successfully before Clear.
        private void UnsubscribeInputs()
        {
            foreach (InputSubscription subscription in m_inputSubscriptions)
                subscription.Unsubscribe();
            m_inputSubscriptions.Clear();
        }

        // Original 06001309.
        public override void Initialise()
        {
            base.Initialise();
            SubscribeInputs();
        }

        // Original 0600130a: the shipped implementation only unsubscribes.
        public override void Close()
        {
            UnsubscribeInputs();
        }

        // Original 0600130b: other actions return None without resolving the save system.
        private ButtonMappingType GameActionButtonMapping(GameAction action)
        {
            if (action == GameAction.CharacterHomingAttackOneShot || action == GameAction.CharacterRailTarget ||
                action == GameAction.CharacterHomingAttack)
                return SaveDataSettings.HomingAttackMapping;
            return ButtonMappingType.None;
        }
    }
}
