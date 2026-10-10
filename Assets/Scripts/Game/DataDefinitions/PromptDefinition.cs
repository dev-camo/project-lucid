using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000598; method IDs below refer to the supplied release.
    // Source inferred from complete ARM64 and x86_64 bodies and readable metadata.
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [UnityEngine.CreateAssetMenuAttribute(fileName = "PromptDefinition", menuName = "HardlightProject/DefinitionData/Definitions/PromptDefinition")]
    public class PromptDefinition : ScriptableObjectWithGuid
    {
        [UnityEngine.TooltipAttribute("Prompt slot to spawn the prompt in. If the slot doesn't exist when the prompt is triggered, the prompt will not spawn.")]
        [UnityEngine.SerializeField]
        [Hardlight.Utils.HashEnumAttribute(typeof(HardlightProject.PromptSlotType))]
        private HardlightProject.PromptSlotType m_promptSlot = PromptSlotType.GameHUDFixedPosition;

        [Hardlight.Utils.HashEnumAttribute(typeof(Hardlight.Enums.Strings))]
        [UnityEngine.SerializeField]
        private Hardlight.Enums.Strings m_text;

        [UnityEngine.SerializeField]
        private UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite m_atlasedImage;

        [UnityEngine.SerializeField]
        private HardlightProject.PromptDefinition.PromptOccurenceType m_pauseOccurence;

        [UnityEngine.SerializeField]
        private HardlightProject.PromptDefinition.PromptOccurenceType m_displayOccurence = PromptOccurenceType.Always;

        [UnityEngine.SerializeField]
        private System.Boolean m_tapToContinue = true;

        [UnityEngine.MinAttribute(0f)]
        [UnityEngine.SerializeField]
        private System.Single m_minimumContinueSeconds;

        [UnityEngine.SerializeField]
        [UnityEngine.MinAttribute(0f)]
        private System.Single m_automaticContinueSeconds;

        [UnityEngine.SerializeField]
        private HardlightProject.UIContainerPrompt m_promptPrefab;

        [UnityEngine.TooltipAttribute("A higher number here will cause a lower-priority prompt in the same slot to be cancelled when this prompt is requested.")]
        [UnityEngine.SerializeField]
        [UnityEngine.RangeAttribute(0f, 100f)]
        private System.Int32 m_priority = 50;

        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("If true, this prompt will not display if any other prompt is displaying in any slot, and will also prevent displaying of other prompts when it is displayed. Ignores priority.")]
        private System.Boolean m_isExclusive;

        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<HardlightProject.PromptDefinition.PromptDefinitionChoice> m_choices;

        [UnityEngine.SerializeField]
        private System.Collections.Generic.List<HardlightProject.PromptDefinition.PromptDefinitionDisplayInput> m_displayGameInputs;

        [UnityEngine.SerializeField]
        private UnityEngine.GameObject m_displayGameInputsSeparator;

        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("Should this prompt send a FTUE Analytics Event? If so, assign a sequential Step ID to help order the \"FTUE Steps\" in the generated data.")]
        private System.Boolean m_sendFTUEAnalytics;

        [UnityEngine.SerializeField]
        private System.String m_ftueStepID;

        // Original 0x06001eaa; returns the stored value without copying or normalizing it.
        public HardlightProject.PromptSlotType PromptSlot => m_promptSlot;

        // Original 0x06001eab; returns the stored value without copying or normalizing it.
        public Hardlight.Enums.Strings Text => m_text;

        // Original 0x06001eac; returns the stored value without copying or normalizing it.
        public UnityEngine.AddressableAssets.AssetReferenceAtlasedSprite Image => m_atlasedImage;

        // Original 0x06001ead; returns the stored value without copying or normalizing it.
        public HardlightProject.PromptDefinition.PromptOccurenceType PauseOccurence => m_pauseOccurence;

        // Original 0x06001eae; returns the stored value without copying or normalizing it.
        public HardlightProject.PromptDefinition.PromptOccurenceType DisplayOccurence => m_displayOccurence;

        // Original 0x06001eaf; returns the stored value without copying or normalizing it.
        public System.Boolean TapToContinue => m_tapToContinue;

        // Original 0x06001eb0; returns the stored value without copying or normalizing it.
        public System.Single MinimumContinueSeconds => m_minimumContinueSeconds;

        // Original 0x06001eb1; returns the stored value without copying or normalizing it.
        public System.Single AutomaticContinueSeconds => m_automaticContinueSeconds;

        // Original 0x06001eb2; returns the stored value without copying or normalizing it.
        public HardlightProject.UIContainerPrompt PromptPrefab => m_promptPrefab;

        // Original 0x06001eb3; returns the stored value without copying or normalizing it.
        public System.Int32 Priority => m_priority;

        // Original 0x06001eb4; returns the stored value without copying or normalizing it.
        public System.Boolean IsExclusive => m_isExclusive;

        // Original 0x06001eb5; returns the stored value without copying or normalizing it.
        public System.Collections.Generic.IReadOnlyList<HardlightProject.PromptDefinition.PromptDefinitionChoice> Choices => m_choices;

        // Original 0x06001eb6; returns the stored value without copying or normalizing it.
        public System.Collections.Generic.IReadOnlyList<HardlightProject.PromptDefinition.PromptDefinitionDisplayInput> DisplayGameInputs => m_displayGameInputs;

        // Original 0x06001eb7; returns the stored value without copying or normalizing it.
        public UnityEngine.GameObject DisplayGameInputsSeparator => m_displayGameInputsSeparator;

        // Original 0x06001eb8; returns the stored value without copying or normalizing it.
        public System.Boolean SendFTUEAnalytics => m_sendFTUEAnalytics;

        // Original 0x06001eb9; returns the stored value without copying or normalizing it.
        public System.String FTUEStepID => m_ftueStepID;

        // Original 0x06001eba. The first default remains; later defaults are cleared.
        // Retain live List enumeration and its disposal/fault ordering. Empty lists stay empty.
        protected override void OnValidate()
        {
            base.OnValidate();
            bool foundDefault = false;
            foreach (PromptDefinitionChoice choice in m_choices)
            {
                if (foundDefault)
                {
                    if (choice.IsDefaultChoice)
                        choice.IsDefaultChoice = false;
                }
                else
                {
                    foundDefault |= choice.IsDefaultChoice;
                }
            }

            // The shipped body evaluates Unity equality and discards its result.
            // The supplied native body does not establish what used the result in source.
            foreach (PromptDefinitionDisplayInput input in m_displayGameInputs)
                _ = input.PromptDisplayInputPrefab == null;
        }

        // Original 0x06001ebb. Initializers above run before the original base constructor;
        // choices, display inputs and all reference fields retain their null defaults.
        public PromptDefinition() { }

        // Original 0x02000599; retain the shipped Occurence spelling and Int32 literals.
        public enum PromptOccurenceType
        {
            Never = 0,
            FirstTimeLevel = 1,
            FirstTimeSave = 2,
            Always = 3,
            UntilAcknowledged = 4,
        }

        // Original 0x0200059a; whole nested serialized owner.
        [System.Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
        public class PromptDefinitionChoice
        {
            [Hardlight.Utils.HashEnumAttribute(typeof(Hardlight.Enums.Strings))]
            [UnityEngine.SerializeField]
            private Hardlight.Enums.Strings m_text;

            [UnityEngine.SerializeField]
            private HardlightProject.UIContainerPromptChoice m_promptChoicePrefab;

            [UnityEngine.SerializeField]
            [UnityEngine.TooltipAttribute("If true, if the player exits the prompt without making a choice (walking away or tap to dismiss), then this choice will be chosen automatically.")]
            private System.Boolean m_isDefaultChoice;

            [UnityEngine.SerializeField]
            private Hardlight.GameInput m_gameInput;

            [UnityEngine.SerializeField]
            private HardlightProject.GameAction m_gameAction;

            [UnityEngine.SerializeField]
            private HardlightProject.GameAction m_gameActionCombo;

            // Original 0x06001ebc.
            public Hardlight.Enums.Strings Text => m_text;

            // Original 0x06001ebd.
            public HardlightProject.UIContainerPromptChoice PromptChoicePrefab => m_promptChoicePrefab;

            // Original 0x06001ebe / 0x06001ebf.
            public System.Boolean IsDefaultChoice
            {
                get => m_isDefaultChoice;
                set => m_isDefaultChoice = value;
            }

            // Original 0x06001ec0.
            public Hardlight.GameInput GameInput => m_gameInput;

            // Original 0x06001ec1.
            public HardlightProject.GameAction GameAction => m_gameAction;

            // Original 0x06001ec2.
            public HardlightProject.GameAction GameActionCombo => m_gameActionCombo;

            // Original 0x06001ec3; only the base Object constructor runs.
            public PromptDefinitionChoice() { }
        }

        // Original 0x0200059b; whole nested serialized owner.
        [System.Serializable]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
        public class PromptDefinitionDisplayInput
        {
            [UnityEngine.SerializeField]
            [Hardlight.Utils.HashEnumAttribute(typeof(Hardlight.GameInput))]
            private Hardlight.GameInput m_gameInput;

            [UnityEngine.SerializeField]
            private Hardlight.SerializableDictionary<Hardlight.UIInputType,Hardlight.GameInput> m_inputTypeOverride = new Hardlight.SerializableDictionary<Hardlight.UIInputType,Hardlight.GameInput>(HardlightUIEnumComparers.UIInputTypeComparer);

            [UnityEngine.SerializeField]
            [Hardlight.Utils.HashEnumAttribute(typeof(HardlightProject.GameAction))]
            private HardlightProject.GameAction m_gameAction;

            [UnityEngine.SerializeField]
            [Hardlight.Utils.HashEnumAttribute(typeof(HardlightProject.GameAction))]
            private HardlightProject.GameAction m_gameActionCombo;

            [UnityEngine.SerializeField]
            private Hardlight.SerializableDictionary<Hardlight.UIInputType,HardlightProject.GameAction> m_inputTypeActionOverride = new Hardlight.SerializableDictionary<Hardlight.UIInputType,HardlightProject.GameAction>(HardlightUIEnumComparers.UIInputTypeComparer);

            [UnityEngine.SerializeField]
            private HardlightProject.UIContainerPromptDisplayInput m_promptDisplayInputPrefab;

            [UnityEngine.SerializeField]
            private HardlightProject.UIContainerPromptDisplayInput.DecoratorType m_decorator;

            // Original 0x06001ec4.
            public HardlightProject.UIContainerPromptDisplayInput PromptDisplayInputPrefab => m_promptDisplayInputPrefab;

            // Original 0x06001ec5.
            public HardlightProject.UIContainerPromptDisplayInput.DecoratorType Decorator => m_decorator;

            // Original 0x06001ec6. Probe the actual override dictionary first, then read
            // the fallback freshly on a miss. Null dictionaries retain their original fault.
            public GameInput GetGameInput(UIInputType inputType)
            {
                GameInput value;
                return m_inputTypeOverride.TryGetValue(inputType, out value) ? value : m_gameInput;
            }

            // Original 0x06001ec7; action overrides are independent of input overrides.
            public GameAction GetGameAction(UIInputType inputType)
            {
                GameAction value;
                return m_inputTypeActionOverride.TryGetValue(inputType, out value) ? value : m_gameAction;
            }

            // Original 0x06001ec8; the combo has no device-specific override.
            public GameAction GetGameActionCombo() => m_gameActionCombo;

            // Original 0x06001ec9. The two initializers read the genuine UI comparer
            // separately in field order, allocate distinct dictionaries, then call Object.
            public PromptDefinitionDisplayInput() { }
        }

    }
}
