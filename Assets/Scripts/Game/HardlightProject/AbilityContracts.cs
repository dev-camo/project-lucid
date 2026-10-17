namespace HardlightProject
{
    public interface IAbilityTriggerable : IAbilityTrigger // Game.Runtime02000081
    {
        void DoTrigger(); // original0600048b abstract contract
        void ClearTrigger(); // original0600048c abstract contract
    }
    public interface IAbility { } // original0200040e genuinely has zero members
}
