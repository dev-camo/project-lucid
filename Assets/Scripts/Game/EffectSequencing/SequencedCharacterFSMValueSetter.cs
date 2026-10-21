using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace HardlightProject
{
    // Original Game.Runtime 02000617 and its display class <>c__DisplayClass6_0 (02000618);
    // bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class SequencedCharacterFSMValueSetter<T> : SequencedEffect
    {
        [SerializeField] private string m_fsmKeyValue;
        [FormerlySerializedAs("m_boolValueToSet"), SerializeField] private T m_valueToSet;
        [SerializeField, Min(0f)] private float m_delayToRemove;
        private static readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private GraphStorageKey m_fsmVarName;

        // 0x060020ab: the storage key is resolved from the serialized name once.
        private void Start()
        {
            m_fsmVarName = new GraphStorageKey(m_fsmKeyValue);
        }

        // 0x060020ac. Without a current character nothing is stored, but completion
        // is still reported. The value is removed again after a positive delay.
        protected override void OnEffectTriggered(IEffectData data)
        {
            if (m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
            {
                character.Storage.SetValue(m_fsmVarName, m_valueToSet);
                if (m_delayToRemove > 0f)
                    TimeScaledUtilities_SDT.DelayFixedSeconds(m_delayToRemove, m_timeCategory, () => RemoveValue(character));
            }
            m_onComplete();
        }

        // 0x060020ad
        private void RemoveValue(Character character)
        {
            character.Storage.RemoveValue<T>(m_fsmVarName);
        }

        protected SequencedCharacterFSMValueSetter() { }
    }
}
