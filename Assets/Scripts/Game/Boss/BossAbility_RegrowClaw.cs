using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 020001ed; bodies translated from the 1.10.1 x86-64 binary (0x6FE4D0..0x6FE9B0).
    // Counts, per claw, the attacks made while the claw is deflated and tells when the claw should regrow.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossAbility_RegrowClaw : ActorAbility
    {
        public BossAbilityDefinition_RegrowClaw Definition;

        private Boss m_boss;

        public override AbilityDefinition DefinitionBase => Definition; // 06000b1a

        // 06000b1b: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_RegrowClaw;
        }

        // 06000b1c
        public void AttackedWithDeflatedClaw(BossAttackTypes clawChoice)
        {
            m_boss.Storage.AdjustValue(GetKeyForClaw(clawChoice), 1);
        }

        // 06000b1d: a claw whose stored count (one if it was never counted) reached the threshold regrows, and its count restarts.
        public bool ShouldRegrowClaw(BossAttackTypes clawChoice)
        {
            GraphStorageKey key = GetKeyForClaw(clawChoice);
            int deflatedCount = m_boss.Storage.GetValue(key, 1, true);
            int usesToTriggerRegrowth = Definition.UsesToTriggerRegrowth;
            if (deflatedCount >= usesToTriggerRegrowth)
                m_boss.Storage.SetValue(key, 0);
            return deflatedCount >= usesToTriggerRegrowth;
        }

        // 06000b1e: anything but the left claw counts as the right one.
        private GraphStorageKey GetKeyForClaw(BossAttackTypes claw)
        {
            return claw == BossAttackTypes.BossCrab_LeftClaw
                ? BossFSMKeys.BossCrabDeflatedLeftCount
                : BossFSMKeys.BossCrabDeflatedRightCount;
        }
    }
}
