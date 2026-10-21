using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The declarations of the boss abilities and boss FSM states (Game.Runtime 020001db..020001f2 and 020001fa..0200022e, without the abstract SetValue base, with the
    // arguments classes and the projectile base's closure class). Every row is the original DummyDll ECMA-335 table of the type:
    // type flags, base type, directly declared interfaces, type attributes in metadata order, then fields and methods in
    // metadata order with their flags, and the attributes of the few members that carry any (the compiler may add its own to those). Compiler-generated names (the
    // projectile base's instance callback b__39_0 and its closure DisplayClass46_0) are part of the rows.
    public sealed class BossAbilityDeclarationTests
    {
        private sealed class Row
        {
            public readonly string Name;
            public readonly int Flags;
            public readonly string Base;
            public readonly string[] Interfaces;
            public readonly string[] Attributes;
            public readonly (string name, int flags)[] Fields;
            public readonly (string name, int flags)[] Methods;
            public readonly (string name, string[] attributes)[] MemberAttributes;

            public Row(string name, int flags, string baseName, string[] interfaces, string[] attributes,
                (string, int)[] fields, (string, int)[] methods, (string, string[])[] memberAttributes)
            {
                Name = name;
                Flags = flags;
                Base = baseName;
                Interfaces = interfaces;
                Attributes = attributes;
                Fields = fields;
                Methods = methods;
                MemberAttributes = memberAttributes;
            }
        }

        private static readonly Row[] AbilityRows =
        {
            new Row("HardlightProject.BossAbility_BodySlam", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IOffensiveAbility" },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("PointsInATriangle", 0x8051), ("Definition", 0x0006), ("m_startWindup", 0x0001), ("m_isWindingUp", 0x0001), ("m_isWoundUp", 0x0001), ("m_canTriggerAttackAnim", 0x0001), ("m_attackAnimStarted", 0x0001), ("m_waveSpawned", 0x0001), ("m_boss", 0x0001), ("m_preJumpPosition", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_abilityActivated", 0x0001), ("m_windUpTimer", 0x0001), ("<AttackIndex>k__BackingField", 0x0001), ("m_coordinateArray", 0x0021), ("m_slamTimer", 0x0001), ("m_attackAnimationTimer", 0x0001), ("m_currentFarRangeSquared", 0x0001), ("m_extraRange", 0x0001), ("m_bodyslamCountFSMVar", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_StartWindUp", 0x09E6), ("get_WoundUp", 0x09E6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x0886), ("get_AttackIndex", 0x0886), ("set_AttackIndex", 0x0881), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("TriggerAttackStrikeEffects", 0x0081), ("FireProjectileFromBoss", 0x0081), ("WindingUp", 0x0081), ("Attacking", 0x0081), ("CanAttack", 0x01E6), ("SetAttackFlow", 0x01E6), ("SetAttackWindingUp", 0x01E6), ("SetAttackWoundUp", 0x01E6), ("SetAttackAnimStarted", 0x01E6), ("SkipAttackPreparation", 0x01E6), ("ResetProjectileGroupCounter", 0x01E6), ("IncreaseValidAttackRange", 0x0081), ("CalculateTriangleCoordinates", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] { ("get_AttackIndex", new string[] { "CompilerGeneratedAttribute()" }), ("set_AttackIndex", new string[] { "CompilerGeneratedAttribute()" }), ("<AttackIndex>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_BossCrabClawAttack", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IOffensiveAbility" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_abilityActivated", 0x0001), ("m_canTriggerAttackAnim", 0x0001), ("m_attackAnimStarted", 0x0001), ("m_startWindup", 0x0001), ("m_isWindingUp", 0x0001), ("m_isWoundUp", 0x0001), ("m_boss", 0x0001), ("m_attackTimeoutTimer", 0x0001), ("m_attackAnimationTimer", 0x0001), ("m_extraWindUpTimer", 0x0001), ("m_currentFarRangeSquared", 0x0001), ("m_currentTelegraphRangeSquared", 0x0001), ("m_extraRange", 0x0001), ("m_thisAttackIndex", 0x0001), ("m_bodySlamCountFSMVar", 0x0021) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_IsTriggered", 0x09E6), ("get_StartWindUp", 0x09E6), ("get_WoundUp", 0x09E6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("IncreaseValidAttackRange", 0x0081), ("Attacking", 0x0081), ("TriggerAttackStrikeEffects", 0x0086), ("FireProjectileFromBoss", 0x0081), ("SetAttackFlow", 0x01E6), ("SetAttackWindingUp", 0x01E6), ("SetAttackWoundUp", 0x01E6), ("SetAttackAnimStarted", 0x01E6), ("SkipAttackPreparation", 0x01E6), ("ResetProjectileGroupCounter", 0x01E6), ("AbilityActivated", 0x0081), ("WindingUp", 0x0081), ("CanAttack", 0x01E6), ("ProcessAttackSettings", 0x0081), ("CharacterInAttackRange", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_BossFourmenChargeAttack", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IOffensiveAbility", "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("ArenaCentre", 0x0021), ("Definition", 0x0006), ("m_abilityActivated", 0x0001), ("m_canTriggerAttackAnim", 0x0001), ("m_startWindup", 0x0001), ("m_isWindingUp", 0x0001), ("m_isWoundUp", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_playerTargetPosition", 0x0001), ("m_normalisedDirection", 0x0001), ("m_boss", 0x0001), ("m_attackAnimationTimer", 0x0001), ("m_decelerationTimer", 0x0001), ("m_currentFarRangeSquared", 0x0001), ("m_currentTelegraphRangeSquared", 0x0001), ("m_previousSquareMagnitude", 0x0001), ("m_debugWindUpTime", 0x0001), ("m_debugWindUpTimer", 0x0001), ("m_reachedTarget", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_IsTriggered", 0x09E6), ("get_StartWindUp", 0x09E6), ("get_WoundUp", 0x09E6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("Initialise", 0x00C6), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("Attacking", 0x0081), ("TriggerAttackStrikeEffects", 0x0081), ("CanAttack", 0x01E6), ("SetAttackFlow", 0x01E6), ("SetAttackWindingUp", 0x01E6), ("SetAttackWoundUp", 0x01E6), ("SetAttackAnimStarted", 0x01E6), ("SkipAttackPreparation", 0x01E6), ("ResetProjectileGroupCounter", 0x01E6), ("AbilityActivated", 0x0081), ("WindingUp", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_DeathFlight", 0x00100001, "HardlightProject.ActorAbility",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_boss", 0x0001), ("m_bossPosition", 0x0001), ("m_characterPosition", 0x0001), ("m_relativeDirection", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_flightTime", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_CurrentTargetPosition", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_LaserFence", 0x00100001, "HardlightProject.ActorAbility",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("Definition", 0x0006) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("GetHazardDepth", 0x0086), ("GetHazardHeight", 0x0086), ("GetHazardYOffset", 0x0086), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_MoveBigCircleWithCounterSpin", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("ArenaCentre", 0x0021), ("Definition", 0x0006), ("m_boss", 0x0001), ("m_bossEntryPoint", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_bigCircleTargetPosition", 0x0001), ("m_centerOfBigCircleCircle", 0x0001), ("m_radiusOfBigCircleCircle", 0x0001), ("m_radiusOfCounterSpinCircle", 0x0001), ("m_counterSpinSpeedNormalised", 0x0001), ("m_finalArcRadians", 0x0001), ("m_progressionBigCircleArcRadians", 0x0001), ("m_progressionCounterSpinArcRadians", 0x0001), ("m_speedBigCircle", 0x0001), ("m_speedCounterSpin", 0x0001), ("m_speedBigCircleNormalised", 0x0001), ("m_speedCounterSpinNormalised", 0x0001), ("m_distanceFullAccelerationReachedSquared", 0x0001), ("m_invertDirection", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("SetDebugActive", 0x0081), ("SetDebugPositions", 0x0081), ("CalculateTargetPositionFromNewCenter", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] { ("SetDebugActive", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }), ("SetDebugPositions", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }) }),
            new Row("HardlightProject.BossAbility_MoveBigCircleWithDecayingRadius", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("ArenaCentre", 0x0021), ("Definition", 0x0006), ("m_boss", 0x0001), ("m_bossEntryPoint", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_centerOfCircle", 0x0001), ("m_bossChargingPoint", 0x0001), ("m_radiusOfCircle", 0x0001), ("m_counterSpinSpeedNormalised", 0x0001), ("m_finalArcRadians", 0x0001), ("m_progressionArcRadians", 0x0001), ("m_speed", 0x0001), ("m_speedCounterSpin", 0x0001), ("m_speedNormalised", 0x0001), ("m_totalMovementTime", 0x0001), ("m_distanceFullAccelerationReachedSquared", 0x0001), ("m_invertDirection", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_MoveRandom", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("ArenaCentre", 0x0021), ("Definition", 0x0006), ("m_boss", 0x0001), ("m_bossEntryPoint", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_finalTargetPosition", 0x0001), ("m_centerOfCircle", 0x0001), ("m_radiusOfCircle", 0x0001), ("m_speedNormalised", 0x0001), ("m_originalArcRadians", 0x0001), ("m_finalArcRadians", 0x0001), ("m_progressionArcRadians", 0x0001), ("m_speedAdjusted", 0x0001), ("m_distanceFullAccelerationReachedSquared", 0x0001), ("m_reachedTopSpeed", 0x0001), ("m_pumpTheBrakes", 0x0001), ("m_recursionLimit", 0x0001), ("m_recursionCount", 0x0001), ("m_invertDirection", 0x0001), ("m_debugInnerCoords", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("SetupAbility", 0x0081), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("SetDebugActive", 0x0081), ("SetDebugObjectActive", 0x0091), ("SetDebugPositions", 0x0081), ("CalculateNewCenter", 0x0081), ("CalculateTargetPositionFromNewCenter", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] { ("SetDebugActive", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }), ("SetDebugPositions", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }) }),
            new Row("HardlightProject.BossAbility_MoveTowardsTarget", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("minSqrMagnitude", 0x8051), ("ArenaCentre", 0x0024), ("Definition", 0x0006), ("m_boss", 0x0004), ("m_currentTargetPosition", 0x0004), ("m_targetPosition", 0x0004), ("m_movementTimer", 0x0001), ("m_decelerationTimer", 0x0001), ("m_reachedTarget", 0x0004), ("m_fourmenSystemRef", 0x0004), ("<DefinitionBase>k__BackingField", 0x0021) },
                new (string, int)[] { ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09C6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] { ("get_DefinitionBase", new string[] { "CompilerGeneratedAttribute()" }), ("<DefinitionBase>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_MoveToArenaEdge", 0x00100001, "HardlightProject.BossAbility_MoveTowardsTarget",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("DerivedDefinition", 0x0006) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_MoveToBigCircleWithCounterSpin", 0x00100001, "HardlightProject.BossAbility_MoveTowardsTarget",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("DerivedDefinition", 0x0006) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_MoveToTargetPoint", 0x00100001, "HardlightProject.BossAbility_MoveTowardsTarget",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("DerivedDefinition", 0x0006), ("m_movementTimer", 0x0001), ("m_decelerationTimer", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_TurnSpeed", 0x08C6), ("Initialise", 0x00C6), ("DoOnLeave", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_MoveToTriangleFormation", 0x00100001, "HardlightProject.BossAbility_MoveTowardsTarget",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("DerivedDefinition", 0x0006) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("SetDebugPositions", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] { ("SetDebugPositions", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }) }),
            new Row("HardlightProject.BossAbility_ProjectileSpawnAndShootBase`1", 0x00100081, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IOffensiveAbility" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0004), ("<DefinitionBase>k__BackingField", 0x0021), ("m_startWindup", 0x0001), ("m_isWindingUp", 0x0001), ("m_isWoundUp", 0x0001), ("m_canTriggerAttackAnim", 0x0001), ("m_attackAnimStarted", 0x0004), ("m_projectilesSpawned", 0x0001), ("m_boss", 0x0004), ("m_currentTargetPosition", 0x0004), ("m_abilityActivated", 0x0001), ("m_projectileCount", 0x0004), ("m_currentlyFiringProjectileIndex", 0x0004), ("m_currentlySpawningProjectileIndex", 0x0001), ("m_projectileGroupCount", 0x0004), ("m_attackAnimationTimer", 0x0004), ("m_projectileFireTimer", 0x0004), ("m_projectileSpawnTimer", 0x0001), ("m_spawnDelayTimer", 0x0004), ("m_projectileFireDivisible", 0x0004), ("m_projectSpawnDivisible", 0x0004), ("m_hasReturningProjectile", 0x0004), ("m_spawnedCurrentProjectile", 0x0001), ("m_projectiles", 0x0004), ("m_availableLanes", 0x0004), ("m_projectileSpawnOrder", 0x0004), ("m_spawnTransform", 0x0004), ("m_bossManagerRef", 0x0001), ("m_bossManager", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_StartWindUp", 0x09E6), ("get_WoundUp", 0x09E6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("TriggerAttackStrikeEffects", 0x0084), ("DecideProjectileType", 0x05C4), ("GetSpawnOrigin", 0x05C4), ("SpawnAProjectile", 0x0081), ("GetSpline", 0x05C4), ("OffsetSpawnLocation", 0x05C4), ("GetLaneChoice", 0x05C4), ("WindingUp", 0x0081), ("Spawning", 0x01C4), ("Attacking", 0x05C4), ("CanAttack", 0x01E6), ("SetAttackFlow", 0x01E6), ("SetAttackWindingUp", 0x01E6), ("SetAttackWoundUp", 0x01E6), ("SetAttackAnimStarted", 0x01E6), ("SkipAttackPreparation", 0x01E6), ("BlockSpawnLane", 0x05C4), ("ResetProjectileGroupCounter", 0x01C6), (".ctor", 0x1884), ("<Initialise>b__39_0", 0x0081) },
                new (string, string[])[] { ("get_DefinitionBase", new string[] { "CompilerGeneratedAttribute()" }), ("<Initialise>b__39_0", new string[] { "CompilerGeneratedAttribute()" }), ("<DefinitionBase>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_ProjectileSpawnAndShootBase`1+<>c__DisplayClass46_0", 0x00100103, "System.Object",
                new string[] {  },
                new string[] { "CompilerGeneratedAttribute()" },
                new (string, int)[] { ("transform", 0x0006), ("<>4__this", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886), ("<SpawnAProjectile>b__0", 0x0083) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_ProjectileSpawnAndShootInRows", 0x00100001, "HardlightProject.BossAbility_ProjectileSpawnAndShootBase`1<HardlightProject.BossAbilityDefinition_ProjectileSpawnAndShootInRows>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("m_spawnDataThisRow", 0x0001), ("m_recentlyUsedPatterns", 0x0001), ("m_splines", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("GetRandomRowPattern", 0x0081), ("DecideProjectileType", 0x00C4), ("GetSpline", 0x00C4), ("OffsetSpawnLocation", 0x00C4), ("GetLaneChoice", 0x00C4), ("GetSpawnOrigin", 0x00C4), ("Attacking", 0x00C4), ("BlockSpawnLane", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_ProjectileSpawnAndShootOneAtATime", 0x00100001, "HardlightProject.BossAbility_ProjectileSpawnAndShootBase`1<HardlightProject.BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("m_blockedLanes", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DecideProjectileType", 0x00C4), ("GetSpline", 0x00C4), ("OffsetSpawnLocation", 0x00C4), ("GetLaneChoice", 0x00C4), ("GetSpawnOrigin", 0x00C4), ("Spawning", 0x00C4), ("Attacking", 0x00C4), ("SetProjectileCount", 0x0086), ("BlockSpawnLane", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_RangedAttack", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IAbilityRange" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_canShoot", 0x0001), ("<DestinationReachedToleranceSquared>k__BackingField", 0x0021), ("<MaxFollowUpRangeSquared>k__BackingField", 0x0001), ("m_boss", 0x0001), ("m_projectileTimerInSeconds", 0x0001), ("m_waveTimerInSeconds", 0x0001), ("m_activeWave", 0x0001), ("m_shotsTakenInWave", 0x0001), ("m_waveOnCooldown", 0x0001), ("m_projectileOnCooldown", 0x0001), ("m_projectilePool", 0x0001), ("m_currentTargetPosition", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x0886), ("get_MaxRangeSquared", 0x09E6), ("get_MinRangeSquared", 0x09E6), ("get_DestinationReachedToleranceSquared", 0x09E6), ("get_MaxFollowUpRangeSquared", 0x09E6), ("set_MaxFollowUpRangeSquared", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("CacheDefinitionValues", 0x0081), ("UpdateDebug", 0x0081), ("GetDebugExtraInfo", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] { ("get_DestinationReachedToleranceSquared", new string[] { "CompilerGeneratedAttribute()" }), ("get_MaxFollowUpRangeSquared", new string[] { "CompilerGeneratedAttribute()" }), ("set_MaxFollowUpRangeSquared", new string[] { "CompilerGeneratedAttribute()" }), ("UpdateDebug", new string[] { "ConditionalAttribute(BUILD_DEVELOPMENT)" }), ("<DestinationReachedToleranceSquared>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }), ("<MaxFollowUpRangeSquared>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_RegrowClaw", 0x00100001, "HardlightProject.ActorAbility",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_boss", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("Initialise", 0x00C6), ("AttackedWithDeflatedClaw", 0x0086), ("ShouldRegrowClaw", 0x0086), ("GetKeyForClaw", 0x0081), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_RetreatingJump", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IAbilityRange", "IAbilityAnimatorTriggerable" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("<DestinationReachedToleranceSquared>k__BackingField", 0x0021), ("<MaxFollowUpRangeSquared>k__BackingField", 0x0021), ("m_triggerJump", 0x0001), ("m_animationTriggered", 0x0001), ("m_boss", 0x0001), ("m_preJumpPosition", 0x0001), ("m_rawTargetPosition", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_jumpProgress", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_MaxRangeSquared", 0x09E6), ("get_MinRangeSquared", 0x09E6), ("get_DestinationReachedToleranceSquared", 0x09E6), ("get_MaxFollowUpRangeSquared", 0x09E6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x0886), ("get_JumpProgress", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), ("SetAnimationTriggered", 0x01E6), ("SetAnimationComplete", 0x01E6), (".ctor", 0x1886) },
                new (string, string[])[] { ("get_DestinationReachedToleranceSquared", new string[] { "CompilerGeneratedAttribute()" }), ("get_MaxFollowUpRangeSquared", new string[] { "CompilerGeneratedAttribute()" }), ("<DestinationReachedToleranceSquared>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }), ("<MaxFollowUpRangeSquared>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_Scuttle", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityTrigger", "IAbilityRange" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_canScuttle", 0x0001), ("<MaxFollowUpRangeSquared>k__BackingField", 0x0001), ("m_boss", 0x0001), ("m_currentTargetPosition", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_IsTriggered", 0x09E6), ("get_CurrentTargetPosition", 0x0886), ("get_MaxRangeSquared", 0x09E6), ("get_MinRangeSquared", 0x09E6), ("get_DestinationReachedToleranceSquared", 0x09E6), ("get_MaxFollowUpRangeSquared", 0x09E6), ("set_MaxFollowUpRangeSquared", 0x0886), ("Initialise", 0x00C6), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] { ("get_MaxFollowUpRangeSquared", new string[] { "CompilerGeneratedAttribute()" }), ("set_MaxFollowUpRangeSquared", new string[] { "CompilerGeneratedAttribute()" }), ("<MaxFollowUpRangeSquared>k__BackingField", new string[] { "CompilerGeneratedAttribute()" }) }),
            new Row("HardlightProject.BossAbility_SpinOnSpot", 0x00100001, "HardlightProject.ActorAbility",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("DegreesInCircle", 0x8051), ("Definition", 0x0006), ("m_boss", 0x0001), ("m_bossEntryPoint", 0x0001), ("m_rotationInDegrees", 0x0001), ("m_spunTime", 0x0001), ("m_spunTimeNormalised", 0x0001), ("m_rotationPreviousTick", 0x0001), ("m_rotationsCompleted", 0x0001), ("m_rotationInDegreesComparison", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_RotationInDegrees", 0x0886), ("get_RotationsCompleted", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_StayAheadOfPlayer", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("ArenaCentre", 0x0021), ("Definition", 0x0006), ("m_boss", 0x0001), ("m_characterManager", 0x0001), ("m_bossEntryPoint", 0x0001), ("m_currentTargetPosition", 0x0001), ("m_centerOfCircle", 0x0001), ("m_radiusOfCircle", 0x0001), ("m_finalArcRadians", 0x0001), ("m_progressionArcRadians", 0x0001), ("m_atMaxRange", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("get_AtMaxRange", 0x0886), ("Initialise", 0x00C6), ("DoOnEnter", 0x00C4), ("SetupAbility", 0x0081), ("DoOnLeave", 0x00C4), ("GetArcAngle", 0x0081), ("DoOnUpdate", 0x00C4), ("CalculateAngles", 0x0081), ("ShouldAbilityActivate", 0x0086), (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossAbility_TurnTowardTarget", 0x00100001, "HardlightProject.ActorAbility",
                new string[] { "IAbilityBossMovement" },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Definition", 0x0006), ("m_boss", 0x0001), ("m_currentTargetPosition", 0x0001) },
                new (string, int)[] { ("get_DefinitionBase", 0x08C6), ("get_CurrentTargetPosition", 0x09E6), ("get_MovementAnimationAngleMultiplier", 0x09E6), ("get_FaceAngleTolerance", 0x09E6), ("get_TurnSpeed", 0x09E6), ("get_IsMomentum", 0x09E6), ("get_TurnToCharacter", 0x09E6), ("Initialise", 0x00C6), ("DoOnUpdate", 0x00C4), (".ctor", 0x1886) },
                new (string, string[])[] {  })
        };

        private static readonly Row[] StateRows =
        {
            new Row("HardlightProject.BossState_ActiveBossChange", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(ActiveBossChange)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ActiveBossIncrement", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "GraphNodeDefaultNameAttribute(ActiveBossIncrement)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ActiveBossReset", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(ActiveReset)", "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ActiveBossSetup", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(ActiveBossSetup)", "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_BodySlam", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})", "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(BodySlam)" },
                new (string, int)[] { ("m_abilityDefinition", 0x0021) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("HandleMovement", 0x0081), ("MoveToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_CalculateClawRegrowth", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(RegrowClaw)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] { ("m_abilityDefinition", 0x0021) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ClearAttackOverride", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(ClearAttackOverride)", "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_CoolDownProjectilesOfType", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(CoolDownProjectilesOfType)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("m_cooldownAllOnEntry", 0x0001), ("m_cooldownAllOnLeave", 0x0001), ("m_allowExceptions", 0x0001), ("m_projectileType", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_CoolDownProjectilesOfType+ProjectileJSONCtorArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)", "FiniteStateMachineJsonCtorArgsAttribute()" },
                new (string, int)[] { ("CoolDownAllOnEntry", 0x0006), ("CoolDownAllOnLeave", 0x0006), ("AllowExceptions", 0x0006), ("ProjectileType", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_DeathFlight", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(DeathFlight)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] { ("m_abilityDefinition", 0x0021) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("MoveToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_DecideMovement", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(DecideMovement)", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] { ("RaycastHeightBoost", 0x8051), ("m_movementRange", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoUpdate", 0x00C4), ("CalculateMovementTarget", 0x0081), ("GetHeightFromRaycast", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_DecideMovement+RangeJSONCtorArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("BossMovementRange", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_DecideMovementOrAttack", 0x00100001, "HardlightProject.BossState_DecideMovement",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/{0})", "GraphNodeDefaultNameAttribute(DecideMovementOrAttack)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("m_attackAbilityDefinition", 0x0001) },
                new (string, int)[] { ("ConstructInstance", 0x0096), (".ctor", 0x1884), ("DoOnEnter", 0x00C4), ("DoUpdate", 0x00C4), ("CanAttack", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_DecideMovementOrAttack+AttackAbilityJSONCtorArgs", 0x00102004, "HardlightProject.BossState_DecideMovement+RangeJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("AttackAbilityDefinitionName", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] { ("AttackAbilityDefinitionName", new string[] { "GraphUnityObjectAttribute(AbilityDefinition)" }) }),
            new Row("HardlightProject.BossState_ForcedRotation", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(ForceRotation)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("TurnToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_GuardianReadyForFinalHit", 0x00100001, "HardlightProject.ActorState",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(GuardianReadyForFinalHit)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_KillProjectilesOfType", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(KillProjectilesOfType)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("m_killAllOnEntry", 0x0001), ("m_killAllOnLeave", 0x0001), ("m_projectileType", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_KillProjectilesOfType+ProjectileJSONCtorArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "FiniteStateMachineJsonCtorArgsAttribute()" },
                new (string, int)[] { ("KillAllOnEntry", 0x0006), ("KillAllOnLeave", 0x0006), ("ProjectileType", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_MoveToTarget", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "GraphNodeDefaultNameAttribute(MoveToTarget)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("RaycastStartHeightBoost", 0x8051), ("RaycastDistanceMultiplier", 0x8051) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("HandleMovement", 0x0081), ("DoOnLeave", 0x00C4), ("SetBossMovementDirections", 0x0081), ("GetHeightFromRaycast", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ResetGuardianProjectileCount", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(ResetGuardianProjectileCount)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_RestoreAllHealth", 0x00100001, "HardlightProject.FSMState_Trigger",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(RestoreAllHealth)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("InvokeTriggerStart", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_RetreatingJump", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(RetreatingJump)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] { ("m_abilityDefinition", 0x0021) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("MoveToward", 0x0081), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_Scuttle", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Crab/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(Scuttling)" },
                new (string, int)[] { ("RaycastStartHeightBoost", 0x8051), ("RaycastDistanceMultiplier", 0x8051), ("m_abilityDefinition", 0x0021) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("MoveToward", 0x0081), ("DoOnLeave", 0x00C4), ("GetHeightFromRaycast", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackAnimationStarted", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/{0})", "GraphNodeDefaultNameAttribute(SetAttackAnimationStarted)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackFlow", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(SetAttackFlow)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackIsWindingUp", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(SetAttackIsWindingUp)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackIsWoundUp", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(SetAttackIsWoundUp)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackOverride", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(SetAttackOverride)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Crab/{0})" },
                new (string, int)[] { ("m_attackTypeToSet", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetAttackOverride+AttackTypeJSONArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("AttackTypeToSet", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetBossScale", 0x00100001, "HardlightProject.BossState_SetValue",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(SetScale)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("m_scale", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetBossScale+RotationJSONCtorArgs", 0x00102004, "HardlightProject.BossState_SetValue+SettableJSONCtorArgs",
                new string[] {  },
                new string[] { "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("Scale", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetProjectileCount", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(SetProjectileCount)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})" },
                new (string, int)[] { ("m_projectileCount", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetProjectileCount+ProjectileCountJSONCtorArgs", 0x00102004, "HardlightProject.ActorState_Ability+AbilityJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("ProjectileCount", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetProjectileSpawnPoint", 0x00100001, "HardlightProject.BossState_SetValue",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(SetProjectileSpawnPoint)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})" },
                new (string, int)[] { ("m_position", 0x0001), ("m_attachPoint", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetProjectileSpawnPoint+PositionJSONCtorArgs", 0x00102004, "HardlightProject.BossState_SetValue+SettableJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("Position", 0x0006), ("AttachPoint", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetSkipAttackPreparation", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(SetSkipAttackPreparation)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetWorldY", 0x00100001, "HardlightProject.BossState_SetValue",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(SetWorldY)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("m_yValue", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SetWorldY+SetYJSONCtorArgs", 0x00102004, "HardlightProject.BossState_SetValue+SettableJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("YValue", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_ShootLasers", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "GraphNodeDefaultNameAttribute(ShootLasers)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoUpdate", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SnapBodyToRotation", 0x00100001, "HardlightProject.BossState_SetValue",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "GraphNodeDefaultNameAttribute(SnapBodyToRotation)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("m_rotation", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SnapBodyToRotation+RotationJSONCtorArgs", 0x00102004, "HardlightProject.BossState_SetValue+SettableJSONCtorArgs",
                new string[] {  },
                new string[] { "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("Rotation", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_SwitchActiveProjectileReturnCollider", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(SwitchActiveProjectileReturnCollider)", "GraphNodeMenuFormatAttribute(Boss/Guardian/{0})", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TriggerEffectOnEntry", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeDefaultNameAttribute(TriggerBossEffect)", "GraphNodeMenuFormatAttribute(Boss/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] { ("m_triggerEffect", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TriggerEffectOnEntry+TriggerEffectJSONCtorArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "FiniteStateMachineJsonCtorArgsAttribute()" },
                new (string, int)[] { ("TriggerEffect", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TriggerEffectOnLeave", 0x00100001, "HardlightProject.ActorState_Effect",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/{0})", "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(TriggerBossEffect)" },
                new (string, int)[] { ("m_triggerEffect", 0x0001) },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnLeave", 0x00C4) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TriggerEffectOnLeave+TriggerEffectJSONCtorArgs", 0x00102004, "HardlightProject.ActorState+BaseJSONCtorArgs",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(2,False)", "FiniteStateMachineJsonCtorArgsAttribute()", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] { ("TriggerEffect", 0x0006) },
                new (string, int)[] { (".ctor", 0x1886) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TurnAwayFromTarget", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "Il2CppSetOptionAttribute(1,False)", "Il2CppSetOptionAttribute(2,False)", "GraphNodeDefaultNameAttribute(TurnAwayFromTarget)", "GraphNodeMenuFormatAttribute(Boss/{0})" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("TurnToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TurnToBossManagerTarget", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/Fourmen/{0})", "Il2CppSetOptionAttribute(1,False)", "GraphNodeDefaultNameAttribute(Turn Towards Boss Manager Target)", "Il2CppSetOptionAttribute(2,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("TurnToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TurnToCharacter", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/{0})", "GraphNodeDefaultNameAttribute(Turn Towards Character)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("TurnToward", 0x0081) },
                new (string, string[])[] {  }),
            new Row("HardlightProject.BossState_TurnToTarget", 0x00100001, "HardlightProject.ActorState_Ability`1<HardlightProject.ActorAbility>",
                new string[] {  },
                new string[] { "GraphNodeMenuFormatAttribute(Boss/{0})", "GraphNodeDefaultNameAttribute(Turn Towards Target)", "Il2CppSetOptionAttribute(2,False)", "Il2CppSetOptionAttribute(1,False)" },
                new (string, int)[] {  },
                new (string, int)[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoUpdate", 0x00C4), ("TurnToward", 0x0081) },
                new (string, string[])[] {  })
        };

        private static Type Resolve(string name)
        {
            string[] parts = name.Split('+');
            Type type = typeof(Boss).Assembly.GetType(parts[0], true);
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                type = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(nested => nested.Name == part || nested.Name.StartsWith(part + "`"));
            }
            return type;
        }

        private static string Describe(Type type) => type.IsGenericType && !type.IsGenericTypeDefinition
            ? type.GetGenericTypeDefinition().FullName + "<" + string.Join(",", type.GetGenericArguments().Select(Describe)) + ">"
            : type.FullName;

        private static string Argument(CustomAttributeTypedArgument argument)
        {
            if (argument.Value is Type type) return type.Name;
            if (argument.Value is Enum) return Convert.ToInt32(argument.Value).ToString();
            return Convert.ToString(argument.Value);
        }

        private static string[] AttributeRow(MemberInfo member) => member.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name + "(" + string.Join(",", data.ConstructorArguments.Select(Argument)) + ")").ToArray();

        private static void AssertRows(Row[] rows)
        {
            foreach (Row row in rows)
            {
                Type type = Resolve(row.Name);
                AssertMembers(type, row.Flags, row.Fields, row.Methods);
                Assert.That(Describe(type.BaseType), Is.EqualTo(row.Base), row.Name + " base");
                string[] declared = type.GetInterfaces().Except(type.BaseType.GetInterfaces()).Select(face => face.Name).ToArray();
                Assert.That(declared, Is.EquivalentTo(row.Interfaces), row.Name + " interfaces");
                Assert.That(AttributeRow(type), Is.EqualTo(row.Attributes), row.Name + " attributes");
                foreach ((string member, string[] attributes) in row.MemberAttributes)
                {
                    MemberInfo found = type.GetMethods(Declared).FirstOrDefault(method => method.Name == member)
                        ?? (MemberInfo)type.GetField(member, Declared);
                    Assert.That(found, Is.Not.Null, row.Name + "." + member);
                    Assert.That(AttributeRow(found), Is.EqualTo(attributes), row.Name + "." + member + " attributes");
                }
                // The metadata lists [Conditional] only on the members named above, so no other method may carry one.
                string[] conditional = row.MemberAttributes.Select(entry => entry.name).ToArray();
                foreach (MethodInfo method in type.GetMethods(Declared).Where(method => !conditional.Contains(method.Name)))
                    Assert.That(AttributeRow(method).Where(text => text.StartsWith("ConditionalAttribute")), Is.Empty, row.Name + "." + method.Name);
            }
        }

        [Test]
        public void AbilityDeclarationsKeepTheOriginalFlagsBasesAttributesFieldsAndMethods() => AssertRows(AbilityRows);

        [Test]
        public void StateDeclarationsKeepTheOriginalFlagsBasesAttributesFieldsAndMethods() => AssertRows(StateRows);

        // BossAbilityDefinition_ProjectileSpawnAndShoot is the sole constraint of the projectile base's type parameter
        // (generic parameter #0 T, no variance and no special constraint flags).
        [Test]
        public void ProjectileSpawnAndShootBaseConstrainsItsDefinitionType()
        {
            Type parameter = typeof(BossAbility_ProjectileSpawnAndShootBase<>).GetGenericArguments().Single();
            Assert.That(parameter.Name, Is.EqualTo("T"));
            Assert.That(parameter.GenericParameterAttributes, Is.EqualTo(GenericParameterAttributes.None));
            Assert.That(parameter.GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(BossAbilityDefinition_ProjectileSpawnAndShoot) }));
            Type closure = Resolve("HardlightProject.BossAbility_ProjectileSpawnAndShootBase`1+<>c__DisplayClass46_0");
            Assert.That(closure.GetGenericArguments().Single().GetGenericParameterConstraints(),
                Is.EqualTo(new[] { typeof(BossAbilityDefinition_ProjectileSpawnAndShoot) }));
        }
    }
}
