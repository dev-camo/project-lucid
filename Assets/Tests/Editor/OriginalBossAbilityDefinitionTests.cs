using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldMemberAccess;
using Owned = ProjectLucid.Tests.WorldTriggerTestSupport.Owned;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // The boss ability definitions of Game.Runtime (TypeDefs 02000481..02000498, the nested SpawnData
    // 02000493; x86-64 0x53E640..0x53F8F0). Declarations come from the original metadata, field defaults
    // from the constructors (the constants they load are named in each case), the cached values from the
    // CalculateCachedValues bodies, and the script classes from the get_ScriptType bodies. A definition is
    // a real ScriptableObject; nothing shared is touched. OnValidate (the base AbilityDefinition body)
    // is not repeated here.
    public sealed class OriginalBossAbilityDefinitionTests
    {
        private sealed class DefinitionRow
        {
            public readonly Type Type;
            public readonly Type Base;
            public readonly int Flags;
            public readonly string Attributes;
            public readonly string[] Fields;
            public readonly string[] Methods;

            public DefinitionRow(Type type, Type baseType, int flags, string attributes, string[] fields, string[] methods)
            {
                Type = type;
                Base = baseType;
                Flags = flags;
                Attributes = attributes;
                Fields = fields;
                Methods = methods;
            }
        }

        // Type flags, base, type attributes, then every field "name:type:flags:attribute names" and every method
        // "name:flags", in the metadata order of the original DummyDll (a pseudo attribute is the flag bit).
        private static readonly DefinitionRow[] Rows =
        {
            new DefinitionRow(typeof(BossAbilityDefinition_BodySlam), typeof(AbilityDefinition), 0x00100001,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "AttackAnimationLength:Single:0006:TooltipAttribute", "TriggerWaveTime:Single:0006:TooltipAttribute",
                    "TraversalTime:Single:0006:TooltipAttribute", "TraversalCurve:AnimationCurve:0006:TooltipAttribute",
                    "TargetValidRangeFar:Single:0006:TooltipAttribute", "TargetValidRangeNear:Single:0006:TooltipAttribute",
                    "TargetGrowthPerSecond:Single:0006:TooltipAttribute", "FaceAngleTolerance:Single:0006:TooltipAttribute",
                    "CustomInitialFaceAngleTolerance:Boolean:0006:TooltipAttribute", "InitialFaceAngleTolerance:Single:0006:ShowIfAttribute",
                    "TurnSpeed:Single:0006:TooltipAttribute", "Projectile:PrefabPoolType:0006:TooltipAttribute",
                    "ProjectileLifetime:Single:0006:TooltipAttribute", "CameraTarget:PrefabPoolType:0006:TooltipAttribute",
                    "CameraTargetLifetime:Single:0006:TooltipAttribute", "TriangleJumpDistance:Single:0006:TooltipAttribute",
                    "AngleOfJumps:Single:0006:TooltipAttribute", "MinTriangleRotationDegreesPerCycle:Single:0006:TooltipAttribute",
                    "MaxTriangleRotationDegreesPerCycle:Single:0006:TooltipAttribute", "<TargetValidRangeNearSquared>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                    "<MinTriangleRotationRadiansPerCycle>k__BackingField:Single:0001:CompilerGeneratedAttribute", "<MaxTriangleRotationRadiansPerCycle>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                    "<AngleOfJumpsAsRadians>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", "get_TargetValidRangeNearSquared:0886", "set_TargetValidRangeNearSquared:0881", "get_MinTriangleRotationRadiansPerCycle:0886",
                    "set_MinTriangleRotationRadiansPerCycle:0881", "get_MaxTriangleRotationRadiansPerCycle:0886", "set_MaxTriangleRotationRadiansPerCycle:0881", "get_AngleOfJumpsAsRadians:0886",
                    "set_AngleOfJumpsAsRadians:0881", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_BossCrabClawAttack), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "AttackAnimationLength:Single:0006:TooltipAttribute", "PostWindUpAttackTimeOut:Single:0006:TooltipAttribute",
                    "ExtraWindUpSeconds:Single:0006:TooltipAttribute", "TelegraphClawChoiceRange:Single:0006:TooltipAttribute",
                    "TargetValidRangeFar:Single:0006:TooltipAttribute", "TargetValidRangeNear:Single:0006:TooltipAttribute",
                    "TargetGrowthPerSecond:Single:0006:TooltipAttribute", "ValidAttackAngle:Int32:0006:TooltipAttribute",
                    "Projectile:PrefabPoolType:0006:TooltipAttribute", "ProjectileLifetime:Single:0006:TooltipAttribute",
                    "LongWindUpAttackComparison:Int32:0006:TooltipAttribute", "TargetValidRangeNearSquared:Single:0086:",
                },
                new[]
                {
                    "get_ScriptType:08c6", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_ChargeAttack), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "AccelerationTime:Single:0006:TooltipAttribute", "TopSpeed:Single:0006:",
                    "AccelerationCurve:AnimationCurve:0006:TooltipAttribute", "DecelerationTime:Single:0006:TooltipAttribute",
                    "DecelerationCurve:AnimationCurve:0006:TooltipAttribute", "MovementAnimationAngleMultiplier:Single:0006:TooltipAttribute",
                    "FaceAngleTolerance:Single:0006:", "TurnSpeed:Single:0006:",
                    "DistanceAllowedFromOrigin:Single:0006:", "ConeOfVisionDistance:Single:0006:",
                    "ConeOfVisionAngle:Single:0006:", "LineOfSightTimeout:Single:0006:TooltipAttribute",
                    "<DistanceAllowedFromOriginSquared>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", "get_DistanceAllowedFromOriginSquared:0886", "set_DistanceAllowedFromOriginSquared:0881", "CalculateCachedValues:00c6",
                    ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_DeathFlight), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "SpinRateDegreesPerSecond:Vector3:0006:TooltipAttribute", "MovementRateMetersPerSecond:Single:0006:TooltipAttribute",
                    "AccelerationMetersPerSecond:Single:0006:TooltipAttribute", "HeightCurve:AnimationCurve:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_LaserFence), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "HeightBoostPerLaserPlacement:Single:0006:TooltipAttribute HideIfAttribute", "InitialBoostHeight:Single:0006:TooltipAttribute HideIfAttribute",
                    "HazardDepth:Single:0006:TooltipAttribute", "HazardHeight:Single:0006:TooltipAttribute",
                    "HazardYOffset:Single:0006:TooltipAttribute", "Attachment:ActorAttachPointType:0006:TooltipAttribute",
                    "HasStaticTargets:Boolean:0006:TooltipAttribute", "LaserTarget:Vector3:0006:TooltipAttribute ShowIfAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveBigCircleWithCounterSpin), typeof(AbilityDefinition), 0x00100001,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "BigCircleSpeed:Single:0006:", "CounterRotateSpeed:Single:0006:",
                    "FaceAngleTolerance:Single:0006:", "TurnSpeed:Single:0006:",
                    "MovementAnimationAngleMultiplier:Single:0006:", "BigCircleRadius:Single:0006:",
                    "CounterSpinRadius:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveBigCircleWithDecayingRadius), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "MaxMovementSpeed:Single:0006:", "MovementSpeedCurve:AnimationCurve:0006:",
                    "EndingRadius:Single:0006:TooltipAttribute", "StartingRadius:Single:0006:",
                    "RadiusDecayCurve:AnimationCurve:0006:", "TimeToDecayFully:Single:0006:",
                    "EndDistanceTolerance:Single:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveRandom), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "MinimumSpeed:Single:0006:", "MaximumSpeed:Single:0006:",
                    "AccelerationPerSecond:Single:0006:", "MinDistanceTargetX:Single:0006:",
                    "EndDistanceTolerance:Single:0006:TooltipAttribute", "FaceAngleTolerance:Single:0006:",
                    "TurnSpeed:Single:0006:", "DistanceAllowedFromOrigin:Single:0006:",
                    "MovementAnimationAngleMultiplier:Single:0006:TooltipAttribute", "FinalTargetRadiusMultiplier:Single:0006:TooltipAttribute RangeAttribute",
                    "<DistanceAllowedFromOriginSquared>k__BackingField:Single:0001:CompilerGeneratedAttribute", "<DistanceAllowedFromOriginAdjustedForDiameter>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                    "<DistanceAllowedFromOriginAdjustedForDiameterSquared>k__BackingField:Single:0001:CompilerGeneratedAttribute", "<DistanceAllowedFromOriginAdjustedForRadius>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", "get_DistanceAllowedFromOriginSquared:0886", "set_DistanceAllowedFromOriginSquared:0881", "get_DistanceAllowedFromOriginAdjustedForDiameter:0886",
                    "set_DistanceAllowedFromOriginAdjustedForDiameter:0881", "get_DistanceAllowedFromOriginAdjustedForDiameterSquared:0886", "set_DistanceAllowedFromOriginAdjustedForDiameterSquared:0881", "get_DistanceAllowedFromOriginAdjustedForRadius:0886",
                    "set_DistanceAllowedFromOriginAdjustedForRadius:0881", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveTowardsTarget), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "AccelerationTime:Single:0006:TooltipAttribute", "TopSpeed:Single:0006:",
                    "AccelerationCurve:AnimationCurve:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveToArenaEdge), typeof(BossAbilityDefinition_MoveTowardsTarget), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "Radius:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveToBigCircleWithCounterSpin), typeof(BossAbilityDefinition_MoveTowardsTarget), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False)",
                new[]
                {
                    "BigCircleRadius:Single:0006:", "CounterSpinRadius:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveToTarget), typeof(BossAbilityDefinition_MoveTowardsTarget), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "MovementAnimationAngleMultiplier:Single:0006:TooltipAttribute", "FaceAngleTolerance:Single:0006:",
                    "TurnSpeed:Single:0006:", "DistanceAllowedFromOrigin:Single:0006:",
                    "TargetIsBossOrigin:Boolean:0006:", "Target:Vector3:0006:HideIfAttribute",
                    "<DistanceAllowedFromOriginSquared>k__BackingField:Single:0001:CompilerGeneratedAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", "get_DistanceAllowedFromOriginSquared:0886", "set_DistanceAllowedFromOriginSquared:0881", "CalculateCachedValues:00c6",
                    ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MoveToTriangleFormation), typeof(BossAbilityDefinition_MoveTowardsTarget), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "StartingRadius:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MovementRetreatingJump), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "JumpHeightCurve:AnimationCurve:0006:", "JumpSpeedCurve:AnimationCurve:0006:",
                    "JumpMaxHeight:Single:0006:", "MiniumJumpRange:Single:0006:",
                    "FaceAngleTolerance:Single:0006:", "TurnSpeed:Single:0006:",
                    "MiniumJumpRangeSquared:Single:0006:HideInInspector",
                },
                new[]
                {
                    "get_ScriptType:08c6", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MovementScuttle), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "ScuttleSpeed:Single:0006:", "ScuttleRange:Single:0006:",
                    "ScuttleStartMinimumMovementDistance:Single:0006:", "ScuttleEndDistanceTolerance:Single:0006:TooltipAttribute",
                    "ScuttleDestinationReachedSeconds:Single:0006:TooltipAttribute", "FaceAngleTolerance:Single:0006:",
                    "TurnSpeed:Single:0006:", "FullSpeedCurve:AnimationCurve:0006:",
                    "TargetDistanceForFullSpeed:Single:0006:", "ScuttleRangeSquared:Single:0006:HideInInspector",
                    "ScuttleMinimumMovementDistanceSquared:Single:0006:HideInInspector", "ScuttleEndDistanceToleranceSquared:Single:0006:HideInInspector",
                    "TargetDistanceForFullSpeedSquared:Single:0006:HideInInspector",
                },
                new[]
                {
                    "get_ScriptType:08c6", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_MovementTurnTowardTarget), typeof(AbilityDefinition), 0x00100001,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "FaceAngleTolerance:Single:0006:", "TurnSpeed:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_ProjectileSpawnAndShoot), typeof(AbilityDefinition), 0x00100081,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "AttackAnimationLengthSeconds:Single:0006:TooltipAttribute FormerlySerializedAsAttribute", "ProjectileGroupSpawnTimeSeconds:Single:0006:FormerlySerializedAsAttribute TooltipAttribute",
                    "CullTimeOnceBehindPlayerSeconds:Single:0006:FormerlySerializedAsAttribute TooltipAttribute", "KillerProjectile:ProjectileType:0006:",
                    "ReturningProjectile:ProjectileType:0006:", "ProjectileRadiansPerSecond:Single:0006:",
                    "ReturnRadiansPerSecond:Single:0006:", "ProjectileGroupToContainReturnable:Int32:0006:",
                    "InitialMovementState:ProjectileMovementState:0006:", "ReturnableProjectileSpawnAngle:Vector3:0006:TooltipAttribute",
                    "SpawnPointAttach:ActorAttachPointType:0006:",
                },
                new[]
                {
                    ".ctor:1884",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_ProjectileSpawnAndShootInRows), typeof(BossAbilityDefinition_ProjectileSpawnAndShoot), 0x00100001,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False)",
                new[]
                {
                    "ProjectileGroupLaunchTimeSeconds:Single:0006:FormerlySerializedAsAttribute TooltipAttribute", "SpawnOrders:SpawnData[]:0006:",
                    "RowPatternIgnoreCount:Int32:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", "CalculateCachedValues:00c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime), typeof(BossAbilityDefinition_ProjectileSpawnAndShoot), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "ProjectileGroupLaunchTimeSeconds:Single:0006:FormerlySerializedAsAttribute TooltipAttribute", "TimeBetweenSpawnAndLaneMoveSeconds:Single:0006:FormerlySerializedAsAttribute TooltipAttribute",
                    "ProjectileMoveToLaneVelocity:Single:0006:", "ProjectileSpawnCircleRadius:Single:0006:",
                    "LaneIgnoreCount:Int32:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_RangedAttack), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "ProjectileCount:Int32:0006:TooltipAttribute", "WaveCount:Int32:0006:TooltipAttribute",
                    "WaveCooldownSeconds:Single:0006:TooltipAttribute", "ProjectileCooldownSeconds:Single:0006:TooltipAttribute",
                    "Projectiles:Projectile[]:0006:TooltipAttribute", "ProjectileAccuracy:Vector3:0006:TooltipAttribute",
                    "MinRange:Single:0006:TooltipAttribute", "MaxRange:Single:0006:TooltipAttribute",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_RegrowClaw), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(1,False) Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute()",
                new[]
                {
                    "UsesToTriggerRegrowth:Int32:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_SpinOnSpot), typeof(AbilityDefinition), 0x00100001,
                "CreateAssetMenuAttribute() Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "SpinRotationCurve:AnimationCurve:0006:", "TimeToFullyRotateInSeconds:Single:0006:TooltipAttribute",
                    "RotationsUntilComplete:Int32:0006:", "DesiredEndRotation:Int32:0006:TooltipAttribute",
                    "DesiredEndRotationToleranceInDegrees:Int32:0006:TooltipAttribute", "Clockwise:Boolean:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_StayAheadOfPlayer), typeof(AbilityDefinition), 0x00100001,
                "Il2CppSetOptionAttribute(2,False) CreateAssetMenuAttribute() Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "MinRadiansAheadOfPlayer:Single:0006:", "MaxRadiansAheadOfPlayer:Single:0006:",
                    "MovementPerSecond:Single:0006:TooltipAttribute", "Radius:Single:0006:",
                    "Clockwise:Boolean:0006:", "TurnSpeed:Single:0006:",
                },
                new[]
                {
                    "get_ScriptType:08c6", ".ctor:1886",
                }),
            new DefinitionRow(typeof(BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData), typeof(object), 0x00102002,
                "Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)",
                new[]
                {
                    "LanesInUseCSV:String:0006:", "ReturnableLanesCSV:String:0006:",
                    "LanesInUseInt:List`1:0006:HideInInspector", "ReturnableLaneChoices:List`1:0006:HideInInspector",
                    "ReturnableLane:Int32:0086:",
                },
                new[]
                {
                    "CalculateCachedValues:0086", "ChooseReturnRow:0086", ".ctor:1886",
                }),
        };

        private static string AttributeNames(MemberInfo member) => string.Join(" ", member.GetCustomAttributesData()
            .Select(data => data.AttributeType.Name).Where(name => name != "NonSerializedAttribute" && name != "SerializableAttribute"));

        private static string TypeAttributes(Type type) => string.Join(" ", type.GetCustomAttributesData()
            .Where(data => data.AttributeType.Name != "SerializableAttribute")
            .Select(data => data.AttributeType.Name + "(" + string.Join(",", data.ConstructorArguments.Select(argument => argument.Value)) + ")"));

        private static string FieldRow(FieldInfo field) =>
            field.Name + ":" + field.FieldType.Name + ":" + ((int)field.Attributes).ToString("x4") + ":" + AttributeNames(field);

        private static float Bits(uint bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);

        private static T Make<T>(Owned owned) where T : ScriptableObject => owned.Track(ScriptableObject.CreateInstance<T>());

        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void BossAbilityDefinitionsKeepTheOriginalFlagsBasesAndTypeAttributes()
        {
            foreach (DefinitionRow row in Rows)
            {
                Assert.That((int)row.Type.Attributes, Is.EqualTo(row.Flags), row.Type.Name + " type flags");
                Assert.That(row.Type.BaseType, Is.EqualTo(row.Base), row.Type.Name + " base");
                Assert.That(TypeAttributes(row.Type), Is.EqualTo(row.Attributes), row.Type.Name + " type attributes");
            }
            Assert.That(Rows.Length, Is.EqualTo(24), "the 23 definitions and the nested SpawnData");
        }

        [Test]
        public void BossAbilityDefinitionsKeepTheOriginalFieldsInMetadataOrder()
        {
            foreach (DefinitionRow row in Rows)
                Assert.That(row.Type.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(FieldRow).ToArray(),
                    Is.EqualTo(row.Fields), row.Type.Name + " fields");
        }

        [Test]
        public void BossAbilityDefinitionsKeepTheOriginalMethodsInMetadataOrder()
        {
            foreach (DefinitionRow row in Rows)
                Assert.That(row.Type.GetMembers(Declared).OfType<MethodBase>().OrderBy(method => method.MetadataToken)
                    .Select(method => method.Name + ":" + ((int)method.Attributes).ToString("x4")).ToArray(),
                    Is.EqualTo(row.Methods), row.Type.Name + " methods");
        }

        // CreateAssetMenu keeps its three named arguments exactly, including the file name the
        // MoveToBigCircleWithCounterSpin definition copies from its sibling.
        [Test]
        public void BossAbilityDefinitionsKeepTheirAssetMenuNames()
        {
            var menus = new (Type type, string fileName, string menuName, int order)[]
            {
            (typeof(BossAbilityDefinition_BodySlam), "BossAbilityDefinition_BodySlam", "HardlightProject/DefinitionData/AbilityDef/BodySlam", 1),
            (typeof(BossAbilityDefinition_BossCrabClawAttack), "BossAbilityDefinition_BossCrabClawAttack", "HardlightProject/DefinitionData/AbilityDef/MeleeAttack", 1),
            (typeof(BossAbilityDefinition_ChargeAttack), "BossAbilityDefinition_ChargeAttack", "HardlightProject/DefinitionData/AbilityDef/ChargeAttack", 1),
            (typeof(BossAbilityDefinition_DeathFlight), "BossAbilityDefinition_DeathFlight", "HardlightProject/DefinitionData/AbilityDef/Death", 1),
            (typeof(BossAbilityDefinition_LaserFence), "BossAbilityDefinition_LaserFence", "HardlightProject/DefinitionData/AbilityDef/LaserFence", 1),
            (typeof(BossAbilityDefinition_MoveBigCircleWithCounterSpin), "BossAbilityDefinition_MoveBigCircleWithCounterSpin", "HardlightProject/DefinitionData/AbilityDef/MoveBigCircleWithCounterSpin", 1),
            (typeof(BossAbilityDefinition_MoveBigCircleWithDecayingRadius), "BossAbilityDefinition_MoveBigCircleWithDecayingRadius", "HardlightProject/DefinitionData/AbilityDef/MoveBigCircleWithDecayingRadius", 1),
            (typeof(BossAbilityDefinition_MoveRandom), "BossAbilityDefinition_MoveRandom", "HardlightProject/DefinitionData/AbilityDef/Random", 1),
            (typeof(BossAbilityDefinition_MoveTowardsTarget), "BossAbilityDefinition_MoveTowardsTarget", "HardlightProject/DefinitionData/AbilityDef/MoveTowardsTarget", 1),
            (typeof(BossAbilityDefinition_MoveToArenaEdge), "BossAbilityDefinition_MoveToArenaEdge", "HardlightProject/DefinitionData/AbilityDef/MoveToArenaEdge", 1),
            (typeof(BossAbilityDefinition_MoveToBigCircleWithCounterSpin), "BossAbilityDefinition_MoveBigCircleWithCounterSpin", "HardlightProject/DefinitionData/AbilityDef/MoveToBigCircleWithCounterSpin", 1),
            (typeof(BossAbilityDefinition_MoveToTarget), "BossAbilityDefinition_MoveToTarget", "HardlightProject/DefinitionData/AbilityDef/MoveToTarget", 1),
            (typeof(BossAbilityDefinition_MoveToTriangleFormation), "BossAbilityDefinition_MoveToTriangleFormation", "HardlightProject/DefinitionData/AbilityDef/MoveToTriangleFormation", 1),
            (typeof(BossAbilityDefinition_MovementRetreatingJump), "BossAbilityDefinition_MovementRetreatingJump", "HardlightProject/DefinitionData/AbilityDef/RetreatingJump", 1),
            (typeof(BossAbilityDefinition_MovementScuttle), "BossAbilityDefinition_MovementScuttle", "HardlightProject/DefinitionData/AbilityDef/Scuttle", 1),
            (typeof(BossAbilityDefinition_MovementTurnTowardTarget), "BossAbilityDefinition_MovementTurnTowardTarget", "HardlightProject/DefinitionData/AbilityDef/TurnTowardsTarget", 1),
            (typeof(BossAbilityDefinition_ProjectileSpawnAndShoot), "BossAbilityDefinition_ProjectileSpawnAndShoot", "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShoot", 1),
            (typeof(BossAbilityDefinition_ProjectileSpawnAndShootInRows), "BossAbilityDefinition_ProjectileSpawnAndShootInRows", "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShootInRows", 1),
            (typeof(BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime), "BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime", "HardlightProject/DefinitionData/AbilityDef/ProjectileSpawnAndShootOneAtATime", 1),
            (typeof(BossAbilityDefinition_RangedAttack), "BossAbilityDefinition_RangedAttack", "HardlightProject/DefinitionData/AbilityDef/RangedAttack", 1),
            (typeof(BossAbilityDefinition_RegrowClaw), "BossAbilityDefinition_RegrowClaw", "HardlightProject/DefinitionData/AbilityDef/RegrowClaw", 1),
            (typeof(BossAbilityDefinition_SpinOnSpot), "BossAbilityDefinition_SpinOnSpot", "HardlightProject/DefinitionData/AbilityDef/SpinOnSpot", 1),
            (typeof(BossAbilityDefinition_StayAheadOfPlayer), "BossAbilityDefinition_StayAheadOfPlayer", "HardlightProject/DefinitionData/AbilityDef/StayAheadOfPlayer", 1),
            };
            foreach ((Type type, string fileName, string menuName, int order) in menus)
            {
                var menu = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
                Assert.That(menu, Is.Not.Null, type.Name);
                Assert.That(menu.fileName, Is.EqualTo(fileName), type.Name + " file name");
                Assert.That(menu.menuName, Is.EqualTo(menuName), type.Name + " menu name");
                Assert.That(menu.order, Is.EqualTo(order), type.Name + " order");
            }
        }

        // The nested enum and the scalar fields of a definition take no value from Unity here: a freshly created
        // definition holds exactly what its constructor stored, so every value-typed field not listed is zero.
        private static void AssertDefaults(Type type, params (string field, object value)[] expected)
        {
            Owned.Run(owned =>
            {
                UnityEngine.Object definition = owned.Track(ScriptableObject.CreateInstance(type));
                Dictionary<string, object> table = expected.ToDictionary(entry => entry.field, entry => entry.value);
                var listed = new HashSet<string>();
                for (Type current = type; current != typeof(AbilityDefinition); current = current.BaseType)
                {
                    foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        if (!field.FieldType.IsValueType) continue;
                        object want;
                        if (table.TryGetValue(field.Name, out want)) listed.Add(field.Name);
                        else want = Activator.CreateInstance(field.FieldType);
                        object actual = field.GetValue(definition);
                        string label = type.Name + "." + field.Name;
                        if (field.FieldType == typeof(float))
                            Assert.That(BitConverter.SingleToInt32Bits((float)actual), Is.EqualTo(BitConverter.SingleToInt32Bits((float)want)), label);
                        else
                            Assert.That(actual, Is.EqualTo(want), label);
                    }
                }
                Assert.That(listed, Is.EquivalentTo(table.Keys), type.Name + ": every listed field exists");
            });
        }

        // Each pair is read from the constants the constructor loads (the address of each constant is given) and
        // stores at the field offset the cited store names. Arguments are float bit patterns, so 0.8f is not 0.8.
        [Test]
        public void BossAbilityDefinitionsStartWithTheirNativeFieldDefaults()
        {
            // 0x53E770: 1.0 and 0.8 from 0x29A7530, 0.7, 10, 10, then 30, 36, 30, 90 from 0x29A7540.
            AssertDefaults(typeof(BossAbilityDefinition_BodySlam),
                ("AttackAnimationLength", Bits(0x3f800000)), ("TriggerWaveTime", Bits(0x3f4ccccd)), ("TraversalTime", Bits(0x3f333333)),
                ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)), ("TriangleJumpDistance", Bits(0x41f00000)),
                ("AngleOfJumps", Bits(0x42100000)), ("MinTriangleRotationDegreesPerCycle", Bits(0x41f00000)),
                ("MaxTriangleRotationDegreesPerCycle", Bits(0x42b40000)));
            // 0x53E840: 2 and 4 from 0x29A7550, 2.0 at +0x50 and 2 at +0x74.
            AssertDefaults(typeof(BossAbilityDefinition_BossCrabClawAttack),
                ("AttackAnimationLength", Bits(0x40000000)), ("PostWindUpAttackTimeOut", Bits(0x40800000)),
                ("ExtraWindUpSeconds", Bits(0x40000000)), ("LongWindUpAttackComparison", 2));
            // 0x53E920: 0.5 at +0x48 and +0x58, 15/1/10/30 from 0x29A7560, 50 and 35 from 0x29A7570, 6.0 at +0x80.
            AssertDefaults(typeof(BossAbilityDefinition_ChargeAttack),
                ("AccelerationTime", Bits(0x3f000000)), ("DecelerationTime", Bits(0x3f000000)),
                ("MovementAnimationAngleMultiplier", Bits(0x41700000)), ("FaceAngleTolerance", Bits(0x3f800000)),
                ("TurnSpeed", Bits(0x41200000)), ("DistanceAllowedFromOrigin", Bits(0x41f00000)),
                ("ConeOfVisionDistance", Bits(0x42480000)), ("ConeOfVisionAngle", Bits(0x420c0000)), ("LineOfSightTimeout", Bits(0x40c00000)));
            // 0x53E9C0: the spin rate is a copy of Vector3.up (static +0x18/+0x20), 5 and 2 from 0x29A7580.
            AssertDefaults(typeof(BossAbilityDefinition_DeathFlight),
                ("SpinRateDegreesPerSecond", Vector3.up), ("MovementRateMetersPerSecond", Bits(0x40a00000)),
                ("AccelerationMetersPerSecond", Bits(0x40000000)));
            // 0x53EA90: 0.3, 2, 2, 8 from 0x29A7590, then the quadword 0x41C7D0C2_40A00000 at +0x58: 5.0 and an
            // attach point stored as the raw hash 0x41C7D0C2.
            AssertDefaults(typeof(BossAbilityDefinition_LaserFence),
                ("HeightBoostPerLaserPlacement", Bits(0x3e99999a)), ("InitialBoostHeight", Bits(0x40000000)),
                ("HazardDepth", Bits(0x40000000)), ("HazardHeight", Bits(0x41000000)), ("HazardYOffset", Bits(0x40a00000)),
                ("Attachment", (ActorAttachPointType)unchecked((int)0x41c7d0c2)));
            // 0x53EB20: 10, 10, 50, 20 from 0x29A75A0 at +0x50, and 7.0 at +0x60.
            AssertDefaults(typeof(BossAbilityDefinition_MoveBigCircleWithCounterSpin),
                ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)),
                ("MovementAnimationAngleMultiplier", Bits(0x42480000)), ("BigCircleRadius", Bits(0x41a00000)),
                ("CounterSpinRadius", Bits(0x40e00000)));
            // 0x53EBA0: 3 and 25 from 0x29A75B0 at +0x58, and 1.1 at +0x6C.
            AssertDefaults(typeof(BossAbilityDefinition_MoveBigCircleWithDecayingRadius),
                ("EndingRadius", Bits(0x40400000)), ("StartingRadius", Bits(0x41c80000)), ("EndDistanceTolerance", Bits(0x3f8ccccd)));
            // 0x53EF00: 3, 1.1, 10, 10 from 0x29A75E0 at +0x54, 25 and 50 from 0x29A75F0 at +0x64, 1.5 at +0x6C.
            AssertDefaults(typeof(BossAbilityDefinition_MoveRandom),
                ("MinDistanceTargetX", Bits(0x40400000)), ("EndDistanceTolerance", Bits(0x3f8ccccd)),
                ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)),
                ("DistanceAllowedFromOrigin", Bits(0x41c80000)), ("MovementAnimationAngleMultiplier", Bits(0x42480000)),
                ("FinalTargetRadiusMultiplier", Bits(0x3fc00000)));
            // 0x53EFB0 stores 0.5 at +0x48; the three definitions below inline that store into their own constructors.
            AssertDefaults(typeof(BossAbilityDefinition_MoveTowardsTarget), ("AccelerationTime", Bits(0x3f000000)));
            // 0x53EF90: 30.0 at +0x58.
            AssertDefaults(typeof(BossAbilityDefinition_MoveToArenaEdge), ("Radius", Bits(0x41f00000)), ("AccelerationTime", Bits(0x3f000000)));
            // 0x53F030: 20 and 7 from 0x29A7600 at +0x58.
            AssertDefaults(typeof(BossAbilityDefinition_MoveToBigCircleWithCounterSpin),
                ("BigCircleRadius", Bits(0x41a00000)), ("CounterSpinRadius", Bits(0x40e00000)), ("AccelerationTime", Bits(0x3f000000)));
            // 0x53F100: 15, 1, 10, 30 from 0x29A7560 at +0x58.
            AssertDefaults(typeof(BossAbilityDefinition_MoveToTarget),
                ("MovementAnimationAngleMultiplier", Bits(0x41700000)), ("FaceAngleTolerance", Bits(0x3f800000)),
                ("TurnSpeed", Bits(0x41200000)), ("DistanceAllowedFromOrigin", Bits(0x41f00000)), ("AccelerationTime", Bits(0x3f000000)));
            // 0x53F180 only stores the inherited 0.5.
            AssertDefaults(typeof(BossAbilityDefinition_MoveToTriangleFormation), ("AccelerationTime", Bits(0x3f000000)));
            // 0x53EC50: 35 and 10 from 0x29A75C0 at +0x5C, and 10.0 at +0x64.
            AssertDefaults(typeof(BossAbilityDefinition_MovementRetreatingJump),
                ("MiniumJumpRange", Bits(0x420c0000)), ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)));
            // 0x53ED10: 10, 0.5, 1.1, 0.2 from 0x29A75D0 at +0x4C, 10 and 10 from 0x29A6CE0 at +0x5C, 30.0 at +0x70.
            AssertDefaults(typeof(BossAbilityDefinition_MovementScuttle),
                ("ScuttleRange", Bits(0x41200000)), ("ScuttleStartMinimumMovementDistance", Bits(0x3f000000)),
                ("ScuttleEndDistanceTolerance", Bits(0x3f8ccccd)), ("ScuttleDestinationReachedSeconds", Bits(0x3e4ccccd)),
                ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)), ("TargetDistanceForFullSpeed", Bits(0x41f00000)));
            // 0x53EDA0: 10 and 10 from 0x29A6CE0 at +0x48.
            AssertDefaults(typeof(BossAbilityDefinition_MovementTurnTowardTarget),
                ("FaceAngleTolerance", Bits(0x41200000)), ("TurnSpeed", Bits(0x41200000)));
            // 0x53F830: the quadword 0x3_3F800000 at +0x50 (1.0 and 3) and 1 at +0x5C.
            AssertDefaults(typeof(BossAbilityDefinition_SpinOnSpot),
                ("TimeToFullyRotateInSeconds", Bits(0x3f800000)), ("RotationsUntilComplete", 3), ("DesiredEndRotationToleranceInDegrees", 1));
            // 0x53F8C0: -0.12, -0.24, 0.01, 25 from 0x29A7640 at +0x48, true at +0x58, 90.0 at +0x5C.
            AssertDefaults(typeof(BossAbilityDefinition_StayAheadOfPlayer),
                ("MinRadiansAheadOfPlayer", Bits(0xbdf5c28f)), ("MaxRadiansAheadOfPlayer", Bits(0xbe75c28f)),
                ("MovementPerSecond", Bits(0x3c23d70a)), ("Radius", Bits(0x41c80000)), ("Clockwise", true), ("TurnSpeed", Bits(0x42b40000)));
            // 0x53F7B0: 2 at +0x48.
            AssertDefaults(typeof(BossAbilityDefinition_RegrowClaw), ("UsesToTriggerRegrowth", 2));
            // 0x53F730: 0.5 and 0.05 from 0x29A7630 at +0x50.
            AssertDefaults(typeof(BossAbilityDefinition_RangedAttack),
                ("WaveCooldownSeconds", Bits(0x3f000000)), ("ProjectileCooldownSeconds", Bits(0x3d4ccccd)));
            // The three projectile definitions store the base defaults themselves (0x53F200, 0x53F4C0, 0x53F680): 1 and 1
            // from 0x29A6C30 at +0x48, 0.5 at +0x50, 0.04 and 0.06 from 0x29A7610 at +0x5C and a spawn angle of
            // (0, 0, -50) at +0x6C.
            (string, object)[] spawn =
            {
                ("AttackAnimationLengthSeconds", Bits(0x3f800000)), ("ProjectileGroupSpawnTimeSeconds", Bits(0x3f800000)),
                ("CullTimeOnceBehindPlayerSeconds", Bits(0x3f000000)), ("ProjectileRadiansPerSecond", Bits(0x3d23d70a)),
                ("ReturnRadiansPerSecond", Bits(0x3d75c28f)), ("ReturnableProjectileSpawnAngle", new Vector3(0f, 0f, Bits(0xc2480000))),
            };
            // 0x53F4C0: 1.0 at +0x7C and 3 at +0x88.
            AssertDefaults(typeof(BossAbilityDefinition_ProjectileSpawnAndShootInRows),
                spawn.Concat(new (string, object)[] { ("ProjectileGroupLaunchTimeSeconds", Bits(0x3f800000)), ("RowPatternIgnoreCount", 3) }).ToArray());
            // 0x53F680: 1, 0.25, 20, 6 from 0x29A7620 at +0x7C and 2 at +0x8C.
            AssertDefaults(typeof(BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime),
                spawn.Concat(new (string, object)[]
                {
                    ("ProjectileGroupLaunchTimeSeconds", Bits(0x3f800000)), ("TimeBetweenSpawnAndLaneMoveSeconds", Bits(0x3e800000)),
                    ("ProjectileMoveToLaneVelocity", Bits(0x41a00000)), ("ProjectileSpawnCircleRadius", Bits(0x40c00000)),
                    ("LaneIgnoreCount", 2),
                }).ToArray());
        }

        // 0x53E720: the near range is squared, and the three angles are scaled by the radian constant 0x3C8EFA35
        // (0x29A7520). Every output gets a different input so a crossed store would show.
        [Test]
        public void BodySlamCalculatesItsSquaredRangeAndRadianAngles()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_BodySlam>(owned);
                float radians = Bits(0x3c8efa35);
                Assert.That(definition.TargetValidRangeNearSquared, Is.EqualTo(0f), "nothing is cached before the call");
                definition.TargetValidRangeNear = 3f;
                definition.MinTriangleRotationDegreesPerCycle = 45f;
                definition.MaxTriangleRotationDegreesPerCycle = 135f;
                definition.AngleOfJumps = 60f;
                definition.CalculateCachedValues();
                Assert.That(definition.TargetValidRangeNearSquared, Is.EqualTo(9f));
                Assert.That(definition.MinTriangleRotationRadiansPerCycle, Is.EqualTo(45f * radians));
                Assert.That(definition.MaxTriangleRotationRadiansPerCycle, Is.EqualTo(135f * radians));
                Assert.That(definition.AngleOfJumpsAsRadians, Is.EqualTo(60f * radians));
                definition.TargetValidRangeNear = -4f;
                definition.CalculateCachedValues();
                Assert.That(definition.TargetValidRangeNearSquared, Is.EqualTo(16f), "recalculated, and a negative range squares positive");
            });
        }

        // 0x53E810 squares the near range into the public field.
        [Test]
        public void BossCrabClawAttackCalculatesTheSquaredNearRange()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_BossCrabClawAttack>(owned);
                definition.TargetValidRangeNear = 6f;
                definition.CalculateCachedValues();
                Assert.That(definition.TargetValidRangeNearSquared, Is.EqualTo(36f));
            });
        }

        // 0x53E8F0
        [Test]
        public void ChargeAttackCalculatesTheSquaredDistanceAllowedFromOrigin()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_ChargeAttack>(owned);
                definition.DistanceAllowedFromOrigin = 7f;
                definition.CalculateCachedValues();
                Assert.That(definition.DistanceAllowedFromOriginSquared, Is.EqualTo(49f));
            });
        }

        // 0x53EEA0: the squared distance, the distance less the multiplied minimum (the diameter form) and its
        // square, and the distance less the minimum (the radius form). The inputs keep all four apart.
        [Test]
        public void MoveRandomCalculatesTheDistanceAllowedFromOriginInItsFourForms()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_MoveRandom>(owned);
                definition.DistanceAllowedFromOrigin = 10f;
                definition.MinDistanceTargetX = 2f;
                definition.FinalTargetRadiusMultiplier = 1.25f;
                definition.CalculateCachedValues();
                Assert.That(definition.DistanceAllowedFromOriginSquared, Is.EqualTo(100f));
                Assert.That(definition.DistanceAllowedFromOriginAdjustedForDiameter, Is.EqualTo(7.5f));
                Assert.That(definition.DistanceAllowedFromOriginAdjustedForDiameterSquared, Is.EqualTo(56.25f));
                Assert.That(definition.DistanceAllowedFromOriginAdjustedForRadius, Is.EqualTo(8f));
            });
        }

        // The multiplied minimum is rounded to single (mulss at 0x53EECB) before it is subtracted (subss at 0x53EED2). With
        // 1.1 and 3.3 the product is not a single, and the subtraction of the unrounded product would end one step higher
        // (0x41AAF5C3). The expected bit patterns are single-precision arithmetic done outside the game.
        [Test]
        public void MoveRandomRoundsTheMultipliedMinimumToSingleBeforeSubtracting()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_MoveRandom>(owned);
                definition.DistanceAllowedFromOrigin = 25f;
                definition.MinDistanceTargetX = 3.3f;
                definition.FinalTargetRadiusMultiplier = 1.1f;
                definition.CalculateCachedValues();
                Assert.That(BitConverter.SingleToInt32Bits(definition.DistanceAllowedFromOriginSquared), Is.EqualTo(0x441c4000));
                Assert.That(BitConverter.SingleToInt32Bits(definition.DistanceAllowedFromOriginAdjustedForDiameter), Is.EqualTo(0x41aaf5c2));
                Assert.That(BitConverter.SingleToInt32Bits(definition.DistanceAllowedFromOriginAdjustedForDiameterSquared), Is.EqualTo(0x43e456a3));
                Assert.That(BitConverter.SingleToInt32Bits(definition.DistanceAllowedFromOriginAdjustedForRadius), Is.EqualTo(0x41ad999a));
            });
        }

        // 0x53F0D0
        [Test]
        public void MoveToTargetCalculatesTheSquaredDistanceAllowedFromOrigin()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_MoveToTarget>(owned);
                definition.DistanceAllowedFromOrigin = 12f;
                definition.CalculateCachedValues();
                Assert.That(definition.DistanceAllowedFromOriginSquared, Is.EqualTo(144f));
            });
        }

        // 0x53EC20
        [Test]
        public void RetreatingJumpCalculatesTheSquaredJumpRange()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_MovementRetreatingJump>(owned);
                definition.MiniumJumpRange = 9f;
                definition.CalculateCachedValues();
                Assert.That(definition.MiniumJumpRangeSquared, Is.EqualTo(81f));
            });
        }

        // 0x53ECD0 squares four inputs with one vector multiply; each lane lands in the field of its own input
        // (range at +0x74, minimum movement at +0x78, end tolerance at +0x7C, full speed at +0x80).
        [Test]
        public void ScuttleSquaresEachOfItsFourDistancesIntoItsOwnField()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_MovementScuttle>(owned);
                definition.ScuttleRange = 2f;
                definition.ScuttleStartMinimumMovementDistance = 3f;
                definition.ScuttleEndDistanceTolerance = 5f;
                definition.TargetDistanceForFullSpeed = 7f;
                definition.CalculateCachedValues();
                Assert.That(definition.ScuttleRangeSquared, Is.EqualTo(4f));
                Assert.That(definition.ScuttleMinimumMovementDistanceSquared, Is.EqualTo(9f));
                Assert.That(definition.ScuttleEndDistanceToleranceSquared, Is.EqualTo(25f));
                Assert.That(definition.TargetDistanceForFullSpeedSquared, Is.EqualTo(49f));
            });
        }

        // 0x53F2A0 walks the authored rows in order and calls 0x53F2F0 on each: the lanes come from the CSV text,
        // entries that do not parse are skipped, and an earlier result is cleared first.
        [Test]
        public void InRowsRebuildsTheLanesOfEveryAuthoredRow()
        {
            Owned.Run(owned =>
            {
                var definition = Make<BossAbilityDefinition_ProjectileSpawnAndShootInRows>(owned);
                var first = new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData { LanesInUseCSV = "1,2,x,,3", ReturnableLanesCSV = "7" };
                var second = new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData { LanesInUseCSV = "-4, 5", ReturnableLanesCSV = "8,9" };
                definition.SpawnOrders = new[] { first, second };
                first.LanesInUseInt.Add(99);
                first.ReturnableLaneChoices.Add(99);
                definition.CalculateCachedValues();
                Assert.That(first.LanesInUseInt, Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(first.ReturnableLaneChoices, Is.EqualTo(new[] { 7 }));
                Assert.That(second.LanesInUseInt, Is.EqualTo(new[] { -4, 5 }), "a signed entry and one with leading white space parse");
                Assert.That(second.ReturnableLaneChoices, Is.EqualTo(new[] { 8, 9 }));
                definition.SpawnOrders = new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData[0];
                definition.CalculateCachedValues();
                Assert.That(first.LanesInUseInt, Is.EqualTo(new[] { 1, 2, 3 }), "an empty order list touches no row");
                definition.SpawnOrders = null;
                Assert.Throws<NullReferenceException>(() => definition.CalculateCachedValues());
            });
        }

        // 0x53F2F0: the two lists are filled from their own CSV; a null CSV faults at Split (0x53F363); a row with
        // nothing parsable leaves both lists empty.
        [Test]
        public void SpawnDataFillsEachLaneListFromItsOwnCsv()
        {
            var row = new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData { LanesInUseCSV = "3,1,3", ReturnableLanesCSV = "6,2" };
            row.CalculateCachedValues();
            Assert.That(row.LanesInUseInt, Is.EqualTo(new[] { 3, 1, 3 }), "duplicates and order are kept");
            Assert.That(row.ReturnableLaneChoices, Is.EqualTo(new[] { 6, 2 }));
            row.LanesInUseCSV = "none";
            row.ReturnableLanesCSV = "";
            row.CalculateCachedValues();
            Assert.That(row.LanesInUseInt, Is.Empty);
            Assert.That(row.ReturnableLaneChoices, Is.Empty);
            Assert.Throws<NullReferenceException>(() => new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData().CalculateCachedValues());
        }

        // 0x53F510 stores List[Random.Range(0, Count)] into ReturnableLane. One choice is deterministic; with several,
        // only listed values appear and each of them does; an empty list faults in the list indexer.
        [Test]
        public void SpawnDataChoosesTheReturnRowFromTheReturnableLanes()
        {
            Random.State priorState = Random.state;
            try
            {
                var row = new BossAbilityDefinition_ProjectileSpawnAndShootInRows.SpawnData();
                row.ReturnableLaneChoices.Add(4);
                row.ChooseReturnRow();
                Assert.That(row.ReturnableLane, Is.EqualTo(4));
                row.ReturnableLaneChoices.AddRange(new[] { 5, 6 });
                Random.InitState(20261021);
                var seen = new HashSet<int>();
                for (int i = 0; i < 400; i++)
                {
                    row.ChooseReturnRow();
                    seen.Add(row.ReturnableLane);
                }
                Assert.That(seen, Is.EquivalentTo(new[] { 4, 5, 6 }));
                row.ReturnableLaneChoices.Clear();
                Assert.Throws<ArgumentOutOfRangeException>(() => row.ChooseReturnRow());
            }
            finally
            {
                Random.state = priorState;
            }
        }

        // Every get_ScriptType loads the Il2CppType of one ability class (the addresses are the first instruction of
        // each body); the pairing is not by suffix for MoveToTarget (a "Point"), the turn, the scuttle or the jump.
        [Test]
        public void EachDefinitionNamesTheAbilityScriptOfItsOwn()
        {
            var scripts = new (Type definition, string script)[]
            {
                (typeof(BossAbilityDefinition_BodySlam), "BossAbility_BodySlam"),
                (typeof(BossAbilityDefinition_BossCrabClawAttack), "BossAbility_BossCrabClawAttack"),
                (typeof(BossAbilityDefinition_ChargeAttack), "BossAbility_BossFourmenChargeAttack"),
                (typeof(BossAbilityDefinition_DeathFlight), "BossAbility_DeathFlight"),
                (typeof(BossAbilityDefinition_LaserFence), "BossAbility_LaserFence"),
                (typeof(BossAbilityDefinition_MoveBigCircleWithCounterSpin), "BossAbility_MoveBigCircleWithCounterSpin"),
                (typeof(BossAbilityDefinition_MoveBigCircleWithDecayingRadius), "BossAbility_MoveBigCircleWithDecayingRadius"),
                (typeof(BossAbilityDefinition_MoveRandom), "BossAbility_MoveRandom"),
                (typeof(BossAbilityDefinition_MoveTowardsTarget), "BossAbility_MoveTowardsTarget"),
                (typeof(BossAbilityDefinition_MoveToArenaEdge), "BossAbility_MoveToArenaEdge"),
                (typeof(BossAbilityDefinition_MoveToBigCircleWithCounterSpin), "BossAbility_MoveToBigCircleWithCounterSpin"),
                (typeof(BossAbilityDefinition_MoveToTarget), "BossAbility_MoveToTargetPoint"),
                (typeof(BossAbilityDefinition_MoveToTriangleFormation), "BossAbility_MoveToTriangleFormation"),
                (typeof(BossAbilityDefinition_MovementRetreatingJump), "BossAbility_RetreatingJump"),
                (typeof(BossAbilityDefinition_MovementScuttle), "BossAbility_Scuttle"),
                (typeof(BossAbilityDefinition_MovementTurnTowardTarget), "BossAbility_TurnTowardTarget"),
                (typeof(BossAbilityDefinition_ProjectileSpawnAndShootInRows), "BossAbility_ProjectileSpawnAndShootInRows"),
                (typeof(BossAbilityDefinition_ProjectileSpawnAndShootOneAtATime), "BossAbility_ProjectileSpawnAndShootOneAtATime"),
                (typeof(BossAbilityDefinition_RangedAttack), "BossAbility_RangedAttack"),
                (typeof(BossAbilityDefinition_RegrowClaw), "BossAbility_RegrowClaw"),
                (typeof(BossAbilityDefinition_SpinOnSpot), "BossAbility_SpinOnSpot"),
                (typeof(BossAbilityDefinition_StayAheadOfPlayer), "BossAbility_StayAheadOfPlayer"),
            };
            Owned.Run(owned =>
            {
                foreach ((Type definition, string script) in scripts)
                    Assert.That(((AbilityDefinition)owned.Track(ScriptableObject.CreateInstance(definition))).ScriptType.Name,
                        Is.EqualTo(script), definition.Name);
            });
        }
    }
}
