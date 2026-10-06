// Complete original actor animation settings; no replacement animation parameters.
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ActorAnimationSettingsDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ActorAnimationSettingsDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorAnimationSettingsDefinition : ScriptableObject // original02000450
    {
        [SerializeField]
        private AnimationParameterWrapper m_rawXLocalVelocity;
        [SerializeField]
        private AnimationParameterWrapper m_rawYLocalVelocity;
        [SerializeField]
        private AnimationParameterWrapper m_rawZLocalVelocity;
        [SerializeField]
        private AnimationParameterWrapper m_rawXZLocalVelocity;
        [SerializeField]
        private AnimationParameterWrapper m_rawXControlInput;
        [SerializeField]
        private AnimationParameterWrapper m_rawYControlInput;
        [SerializeField]
        private AnimationParameterWrapper m_dampenedXZLocalVelocity;
        [Min(1f)] [SerializeField]
        private int m_dampenedXZLocalVelocitySampleSize;
        public AnimationParameterWrapper RawXLocalVelocity => m_rawXLocalVelocity; // original06001963
        public AnimationParameterWrapper RawYLocalVelocity => m_rawYLocalVelocity; // original06001964
        public AnimationParameterWrapper RawZLocalVelocity => m_rawZLocalVelocity; // original06001965
        public AnimationParameterWrapper RawXZLocalVelocity => m_rawXZLocalVelocity; // original06001966
        public AnimationParameterWrapper RawXControlInput => m_rawXControlInput; // original06001967
        public AnimationParameterWrapper RawYControlInput => m_rawYControlInput; // original06001968
        public AnimationParameterWrapper DampenedXZLocalVelocity => m_dampenedXZLocalVelocity; // original06001969
        public int DampenedXZLocalVelocitySampleSize => m_dampenedXZLocalVelocitySampleSize; // original0600196a
        public ActorAnimationSettingsDefinition() { } // 196b: all fields originally default.
    }
}
