using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class BossFSMKeys
    {
        public static readonly GraphStorageKey MoveToRange = new GraphStorageKey("MoveToRange", 0, 0);
        public static readonly GraphStorageKey AttackWoundUp = new GraphStorageKey("AttackWoundUp", 0, 0);
        public static readonly GraphStorageKey CurrentlyVulnerable = new GraphStorageKey("CurrentlyVulnerable", 0, 0);
        public static readonly GraphStorageKey VulnerableTargetActive = new GraphStorageKey("VulnerableTargetActive", 0, 0);
        public static readonly GraphStorageKey ForceNotVulnerable = new GraphStorageKey("ForceNotVulnerable", 0, 0);
        public static readonly GraphStorageKey CurrentlyVulnerableTime = new GraphStorageKey("CurrentlyVulnerableTime", 0, 0);
        public static readonly GraphStorageKey WasHit = new GraphStorageKey("WasHit", 0, 0);
        public static readonly GraphStorageKey ZeroHealth = new GraphStorageKey("ZeroHealth", 0, 0);
        public static readonly GraphStorageKey OriginPoint = new GraphStorageKey("OriginPoint", 0, 0);
        public static readonly GraphStorageKey CurrentPosition = new GraphStorageKey("CurrentPosition", 0, 0);
        public static readonly GraphStorageKey DesiredMovementPosition = new GraphStorageKey("DesiredMovementPosition", 0, 0);
        public static readonly GraphStorageKey DestinationReachedTimer = new GraphStorageKey("DestinationReachedTimer", 0, 0);
        public static readonly GraphStorageKey BossAttackType = new GraphStorageKey("BossAttackType", 0, 0);
        public static readonly GraphStorageKey BossAttackTypeOverride = new GraphStorageKey("BossAttackTypeOverride", 0, 0);
        public static readonly GraphStorageKey BossEffectType = new GraphStorageKey("BossEffectType", 0, 0);
        public static readonly GraphStorageKey BossCrabDeflatedLeftCount = new GraphStorageKey("BossCrabDeflatedLeftCount", 0, 0);
        public static readonly GraphStorageKey BossCrabDeflatedRightCount = new GraphStorageKey("BossCrabDeflatedRightCount", 0, 0);
        public static readonly GraphStorageKey BossHitPlayer = new GraphStorageKey("BossHitPlayer", 0, 0);
        public static readonly GraphStorageKey PhaseTransitionWait = new GraphStorageKey("PhaseTransitionWait", 0, 0);
        public static readonly GraphStorageKey AttackAndVulnerableAnimationExited = new GraphStorageKey("AttackAndVulnerableAnimationExited", 0, 0);
        public static readonly GraphStorageKey BossAttackAngleChange = new GraphStorageKey("BossAttackAngleChange", 0, 0);
        public static readonly GraphStorageKey ForceNextPhaseTrigger = new GraphStorageKey("ForceNextPhaseTrigger", 0, 0);
        public static readonly GraphStorageKey ActiveBossTimer = new GraphStorageKey("ActiveBossTimer", 0, 0);
        public static readonly GraphStorageKey GuardianIsClockwise = new GraphStorageKey("GuardianIsClockwise", 0, 0);
        public static readonly GraphStorageKey GuardianProjectileMovement = new GraphStorageKey("GuardianProjectileMovement", 0, 0);
        public static readonly GraphStorageKey AllBossesMotionTimer = new GraphStorageKey("AllBossesMotionTimer", 0, 0);
        public static readonly GraphStorageKey RadiansCurrentPosition = new GraphStorageKey("RadiansCurrentPosition", 0, 0);
    }
}
