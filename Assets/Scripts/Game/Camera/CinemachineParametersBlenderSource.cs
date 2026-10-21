using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200027c.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class CinemachineParametersBlenderSource : TimeScaledComponent_SDT
    {
        [SerializeField]
        [Tooltip("Higher value means longer to blend towards target value. Set to 0 for no dampening.")]
        [Min(0f)]
        private float m_smoothingDampTimeSeconds;
        // 0x06000dd5 / 0x06000dd6. Declared here because the compiler-generated backing field
        // sits between the smoothing time and the character manager in the original layout.
        public float BlendValue { get; private set; }
        protected CharacterManager m_characterManager;
        private float m_blendVelocity;

        // 0x06000dd7
        private void Start()
        {
            ProcessManager.GetSystemRef<CharacterManager>().InvokeOnValid(manager => m_characterManager = manager);
        }

        // 0x06000dd8: the shipped base body is empty.
        protected override void InternalUpdate(float deltaTime) { }

        // 0x06000dd9: dampening applies only for a strictly positive smoothing time.
        protected void UpdateBlendValue(float targetValue, float deltaTime)
        {
            if (m_smoothingDampTimeSeconds > 0f)
                targetValue = Mathf.SmoothDamp(BlendValue, targetValue, ref m_blendVelocity,
                    m_smoothingDampTimeSeconds, float.PositiveInfinity, deltaTime);
            BlendValue = targetValue;
        }

        // 0x06000dda
        protected CinemachineParametersBlenderSource() { }
    }
}
