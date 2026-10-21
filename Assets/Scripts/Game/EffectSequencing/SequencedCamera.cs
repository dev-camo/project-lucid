using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0x02000611 (+ nested enum 0x02000612). Applies or removes a camera
    // type override through a CameraTypeManipulator when the sequenced effect triggers.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SequencedCamera : SequencedEffect
    {
        private enum TriggerParameter
        {
            Start = 0,
            End = 1
        }

        [SerializeField]
        private TriggerParameter m_triggerParameter;
        [SerializeField]
        private CameraTypeManipulator m_cameraTypeManipulator;

        // 0x060020a5; x86_64 0x568590. The effect completes through the manipulator's callback.
        protected override void OnEffectTriggered(IEffectData data)
        {
            switch (m_triggerParameter)
            {
                case TriggerParameter.Start:
                    m_cameraTypeManipulator.ApplyCameraType(m_onComplete);
                    break;
                case TriggerParameter.End:
                    m_cameraTypeManipulator.RemoveCameraType(m_onComplete);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // 0x060020a6; the base initializer for the Effects time category is inlined in the original.
        public SequencedCamera() { }
    }
}
