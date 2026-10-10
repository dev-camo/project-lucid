namespace HardlightProject
{
    // Original Game.Runtime0200007c; one genuinely abstract method, zero native credit.
    public interface IAbilityBurstAttack<out T> : IAbility
        where T : CharacterAbilityDefinition_BurstAttack
    {
        T BurstAttackDefinition { get; }
    }
}
