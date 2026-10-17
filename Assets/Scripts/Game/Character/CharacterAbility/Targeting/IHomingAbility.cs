namespace HardlightProject
{
    public interface IHomingAbility : IAbility // 02000310, all eight abstract accessor/method rows.
    {
        void Reset();
        bool CanActivate { get; }
        bool IsTriggered { get; set; }
        bool OverridesTargeting { get; }
        bool ShouldProjectFromCamera();
        float GetAdditionalSpeed(float time);
        CharacterAbilityDefinition_Targeting TargetingDefinition { get; }
    }
}
