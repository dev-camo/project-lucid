using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000347, complete twelve-method/two-field API.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterSpinDashRoll
    {
        private readonly Character m_character;
        private float m_deactivateTimer;

        // 0x0600149c/9d. Reads intentionally store the original default in real storage.
        public float ChargeTime
        {
            get => m_character.Storage.GetValue<float>(ActorFSMKeys.SpinDashChargeTime, 0f, true);
            private set => m_character.Storage.SetValue<float>(ActorFSMKeys.SpinDashChargeTime, value);
        }

        // 0x0600149e. Store the character after the original Object base constructor.
        public CharacterSpinDashRoll(Character character)
        {
            m_character = character;
        }

        // 0x0600149f. brain is unused. Re-read timer after real rolling-state lookup.
        public void Update(CharacterBrain brain, float deltaTime)
        {
            if (m_deactivateTimer == 0f)
                return;
            if (!RollingActive())
            {
                m_deactivateTimer = 0f;
                return;
            }
            float remaining = m_deactivateTimer - deltaTime;
            // Both native architectures clamp unordered/negative results to +zero.
            m_deactivateTimer = remaining > 0f ? remaining : 0f;
            if (!(remaining > 0f))
                DeactivateRolling();
        }

        // 0x060014a0.
        public void Close()
        {
            ChargeTime = 0f;
        }

        // 0x060014a1.
        public void ResetChargeTime()
        {
            ChargeTime = 0f;
        }

        // 0x060014a2. Real getter dispatch precedes real setter dispatch.
        public void AdjustChargeTime(float deltaTime)
        {
            ChargeTime = ChargeTime + deltaTime;
        }

        // 0x060014a3. End impulses, conditionally remove rolling, then reset charge.
        public void DisableCharge()
        {
            m_character.EndRollImpulses();
            DeactivateRolling();
            ChargeTime = 0f;
        }

        // 0x060014a4. Store only while rolling; unordered time also deactivates.
        public void Deactivate(float deactivateTime)
        {
            if (!RollingActive())
                return;
            m_deactivateTimer = deactivateTime;
            if (!(deactivateTime > 0f))
                DeactivateRolling();
        }

        // 0x060014a5. Clearing the timer does not alter charge or rolling storage.
        public void ClearDeactivate()
        {
            m_deactivateTimer = 0f;
        }

        // 0x060014a6. Preserve default false and storeDefault=true.
        private bool RollingActive()
        {
            return m_character.Storage.GetValue<bool>(ActorFSMKeys.RollingActive, false, true);
        }

        // 0x060014a7. Original virtual slot39 is IsFormLocked, not a ground query.
        private void DeactivateRolling()
        {
            if (m_character.IsFormLocked())
                return;
            m_character.Storage.RemoveValue<bool>(ActorFSMKeys.RollingActive);
        }
    }
}
