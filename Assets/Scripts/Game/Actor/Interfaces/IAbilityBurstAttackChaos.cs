namespace HardlightProject
{
    // Original Game.Runtime0200007d; all three methods are genuinely abstract.
    public interface IAbilityBurstAttackChaos : IAbilityBurstAttack<CharacterAbilityDefinition_BurstAttackChaos>, IAbility
    {
        void OnStaminaRecharge(bool triggerAbility);
        bool TryStopTimeDilation(bool interrupt);
        bool IsTimeDilationActive();
    }
}
