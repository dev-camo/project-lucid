namespace HardlightProject
{
    // Original Game.Runtime0200007c; one abstract method.
    public interface IAbilityBurstAttack<out T> : IAbility
        where T : CharacterAbilityDefinition_BurstAttack
    {
        T BurstAttackDefinition { get; }
    }
}
