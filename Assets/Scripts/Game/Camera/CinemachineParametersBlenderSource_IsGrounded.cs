using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime type 0x0200027e.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CinemachineParametersBlenderSource_IsGrounded : CinemachineParametersBlenderSource
    {
        // 0x06000dde: the blend value is 1 while the active tracking reports ground contact.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_characterManager.TryGetCurrentCharacter(out Character character))
                UpdateBlendValue(character.ActiveTracking.IsOnGround() ? 1f : 0f, deltaTime);
        }

        // 0x06000ddf
        public CinemachineParametersBlenderSource_IsGrounded() { }
    }
}
