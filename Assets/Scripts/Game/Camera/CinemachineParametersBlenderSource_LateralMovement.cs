using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200027f.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CinemachineParametersBlenderSource_LateralMovement : CinemachineParametersBlenderSource
    {
        [Tooltip("Uses direct input values between -1 and 1 rather than calculated lateral movement.")]
        [SerializeField]
        private bool m_useRawControllerInput;

        // 0x06000de0: the horizontal axis of the raw or the processed controller movement.
        protected override void InternalUpdate(float deltaTime)
        {
            if (!m_characterManager.TryGetCurrentCharacter(out Character character)) return;
            UpdateBlendValue(m_useRawControllerInput ? character.RawControllerMovement.x : character.ControllerMovement.x, deltaTime);
        }

        // 0x06000de1
        public CinemachineParametersBlenderSource_LateralMovement() { }
    }
}
