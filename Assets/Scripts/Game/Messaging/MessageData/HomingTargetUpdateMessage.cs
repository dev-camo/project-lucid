namespace HardlightProject
{
    public struct HomingTargetUpdateMessage // 020006d5: all three fields / one method.
    {
        public bool IsValid;
        public CharacterHomingPool.PooledTarget TargetValue;
        public bool IsActive;

        public bool CanActivate() // 060024f3: short circuit and live ability callbacks.
        {
            if (!IsValid) return false;
            foreach (IHomingAbility ability in TargetValue.Abilities)
                if (ability.CanActivate) return true;
            return false;
        }
    }
}
