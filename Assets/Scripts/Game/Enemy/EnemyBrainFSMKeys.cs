using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class EnemyBrainFSMKeys
    {
        public static readonly GraphStorageKey MovementImpulse = new GraphStorageKey("MovementImpulse", 0, 0);
        public static readonly GraphStorageKey FocusTarget = new GraphStorageKey("FocusTarget", 0, 0);
        public static readonly GraphStorageKey TargetLocked = new GraphStorageKey("TargetLocked", 0, 0);
        public static readonly GraphStorageKey TargetCachedPosition = new GraphStorageKey("TargetCachedPosition", 0, 0);
        public static readonly GraphStorageKey TargetCachedTime = new GraphStorageKey("TargetCachedTime", 0, 0);
        public static readonly GraphStorageKey IdleElapsedTime = new GraphStorageKey("IdleElapsedTime", 0, 0);
        public static readonly GraphStorageKey TargetSpottedAnimationTrigger = new GraphStorageKey("TargetSpottedAnimationTrigger", 0, 0);
        public static readonly GraphStorageKey WatchElapsedTime = new GraphStorageKey("WatchElapsedTime", 0, 0);
        public static readonly GraphStorageKey StartAttackWindUp = new GraphStorageKey("StartAttackWindUp", 0, 0);
        public static readonly GraphStorageKey AnimatorReadyWindUp = new GraphStorageKey("AnimatorReadyWindUp", 0, 0);
        public static readonly GraphStorageKey AttackWindUpTimer = new GraphStorageKey("AttackWindUpTimer", 0, 0);
        public static readonly GraphStorageKey AttackWindUpComplete = new GraphStorageKey("AttackWindUpComplete", 0, 0);
        public static readonly GraphStorageKey AttackTarget = new GraphStorageKey("AttackTarget", 0, 0);
        public static readonly GraphStorageKey AttackAnimationCompleted = new GraphStorageKey("AttackAnimationCompleted", 0, 0);
        public static readonly GraphStorageKey MoveToDestinationReached = new GraphStorageKey("MoveToDestinationReached", 0, 0);
        public static readonly GraphStorageKey TargetReached = new GraphStorageKey("TargetReached", 0, 0);
        public static readonly GraphStorageKey FireProjectileTargetPosition = new GraphStorageKey("FireProjectileTargetPosition", 0, 0);
    }
}
