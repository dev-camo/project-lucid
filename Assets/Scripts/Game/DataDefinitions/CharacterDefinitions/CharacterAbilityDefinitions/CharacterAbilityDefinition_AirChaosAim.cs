using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004b6; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_AirChaosAim", menuName = "HardlightProject/DefinitionData/AbilityDef/AirChaosAim", order = 1)]
	public class CharacterAbilityDefinition_AirChaosAim : CharacterAbilityDefinition_AirChaos
	{
		[Tooltip("Distance to project the aim's raycast.")]
		public float RaycastDistance = 100f;

		[Tooltip("Distance to project to test for ground from the target if in air.")]
		public float GroundDistance = 50f;

		[Tooltip("Minimum angle targeted wall must be from world up to test for a ledge.")]
		public float LedgeAngleMin;

		[Tooltip("Maximum angle targeted wall must be from world up to test for a ledge.")]
		public float LedgeAngleMax = 180f;

		[Tooltip("Distance to project from wall hit to test for a ledge.")]
		public float LedgeDistance = 1f;

		[Tooltip("Distance to offset back from ledge.")]
		public float LedgeOffset = 0.5f;

		[Tooltip("Distance to propel character if no raycast hit.")]
		public float WorldDistance = 10f;

		[Tooltip("Distance to offset target along normal of raycast hit.")]
		public float OffsetDistance = 0.1f;

		[Tooltip("Layers the chaos aim interacts with and treats as the usable track.")]
		public LayerMask TrackMask = -1;

		[Tooltip("Layers the chaos aim collides with.")]
		public LayerMask CollisionMask = -1;

		[Tooltip("The homing attack prefab to detect and monitor possible targets.")]
		public HomingAttack HomingAttackPrefab;

		[Tooltip("Mappings for homing to aim target types.  If not found, defaults to the base homing target.")]
		public SerializableDictionary<HomingTargetType, AbilityAimTargetType> MappingHomingToAimTargetTypes =
			new SerializableDictionary<HomingTargetType, AbilityAimTargetType>(HardlightEnumComparers.HomingTargetTypeComparer);

		[Tooltip("Target types for this homing attack and their origin offset.")]
		public List<HomingTargetAimDefinition> HomingTargetDefinitions;

		[Tooltip("Viewport border widths to stop targeting on hte very edge of the screen.")]
		public Vector2 HomingTargetScreenBorders;

		[Tooltip("Offset of box collider used to detect potential targets.")]
		public Vector3 HomingTargetZoneOffset;

		[Tooltip("The distance to project to activate a game object homing target.")]
		[Min(1f)]
		public float HomingTargetProjectDistance = 1f;

		[Min(0f)]
		[Tooltip("Game object homing target cone ellipse angles.")]
		public Vector2 HomingTargetProjectConeAngle = new Vector2(30f, 30f);

		[Min(1f)]
		[Tooltip("The distance to project to activate a rail homing target.")]
		public float HomingRailProjectDistance = 1f;

		[Min(0f)]
		[Tooltip("Rail homing cone ellipse angles.")]
		public Vector2 HomingRailProjectConeAngle = new Vector2(30f, 30f);

		[Min(0.1f)]
		[Tooltip("The distance to step when projecting through the world to find a rail homing target.")]
		public float HomingRailProjectDistanceStep = 1f;

		[Min(0f)]
		[Tooltip("The time it takes to lerp to the aim target.")]
		public float AimLerpSmoothTime = 1f;

		[Tooltip("The maximum allowed speed to lerp to the aim target.")]
		[Min(0f)]
		public float AimLerpMaxSpeed = 10f;

		[Tooltip("Scale animation when aim lerp reaches the target.")]
		public AnimationCurve AimLerpScaleAnim = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1f));

		[Tooltip("Time to wait before camera orientates to target on entering.")]
		public float CameraTargetDelayOnEnter;

		[Tooltip("Time to wait before camera orientates when staying on the same target.")]
		public float CameraTargetDelayOnStay;

		[Tooltip("Mapped camera target horizontal angle to input value.")]
		public AnimationCurve CameraTargetHorizontalInputValue = new AnimationCurve(new Keyframe(-180f, 1f), new Keyframe(0f, 0f), new Keyframe(180f, 1f));

		[Tooltip("Mapped camera target vertical angle to input value.")]
		public AnimationCurve CameraTargetVerticalInputValue = new AnimationCurve(new Keyframe(-180f, 1f), new Keyframe(0f, 0f), new Keyframe(180f, 1f));

		public SerializableDictionary<AbilityAimTargetType, ChaosAimTarget> TargetTypes =
			new SerializableDictionary<AbilityAimTargetType, ChaosAimTarget>(HardlightEnumComparers.AbilityAimTargetTypeComparer);

		// 06001ac1 (1.10.1 RVA 0x5411C0)
		public override Type ScriptType => typeof(CharacterAbility_AirChaosAim);

		// 06001ac2..06001acf: auto-property accessors (RVA 0x541220..0x541350).
		public ConeVolume.ConeDescription HomingTargetCone { get; private set; }
		public float HomingTargetProjectDistanceSqr { get; private set; }
		public Vector2 HomingTargetProjectConeCosAngle { get; private set; }
		public Vector3 HomingTargetZoneSize { get; private set; }
		public Vector2 HomingRailProjectConeTangents { get; private set; }
		public float LedgeCosAngleMin { get; private set; }
		public float LedgeCosAngleMax { get; private set; }

		// 06001ad0 (1.10.1 RVA 0x541360). The cone angle cosine is taken of the raw
		// angle (no Deg2Rad), unlike the tangents and the ledge cosines (0x541517).
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			HomingTargetCone = new ConeVolume.ConeDescription
			{
				Distance = HomingTargetProjectDistance,
				Tangents = new Vector2(
					Mathf.Tan(HomingTargetProjectConeAngle.x * Mathf.Deg2Rad),
					Mathf.Tan(HomingTargetProjectConeAngle.y * Mathf.Deg2Rad))
			};
			HomingTargetZoneSize = ConeVolume.CalculateConeBounds(HomingTargetCone).size;
			HomingTargetProjectDistanceSqr = HomingTargetProjectDistance * HomingTargetProjectDistance;
			HomingTargetProjectConeCosAngle = new Vector2(
				Mathf.Cos(HomingTargetProjectConeAngle.x),
				Mathf.Cos(HomingTargetProjectConeAngle.y));
			HomingRailProjectConeTangents = new Vector2(
				Mathf.Tan(HomingRailProjectConeAngle.x * Mathf.Deg2Rad),
				Mathf.Tan(HomingRailProjectConeAngle.y * Mathf.Deg2Rad));
			LedgeCosAngleMin = Mathf.Cos(LedgeAngleMin * Mathf.Deg2Rad);
			LedgeCosAngleMax = Mathf.Cos(LedgeAngleMax * Mathf.Deg2Rad);
			foreach (KeyValuePair<AbilityAimTargetType, ChaosAimTarget> target in TargetTypes)
				target.Value.CalculateCachedValues();
		}

		// 06001ad1 (1.10.1 RVA 0x541880)
		public CharacterAbilityDefinition_AirChaosAim()
		{
		}

		// Original Game.Runtime 020004b7.
		[Serializable]
		public class HomingTargetAimDefinition
		{
			public RuntimeType TargetType;

			public Vector3 OffsetLocal;

			public List<EnemyType> EnemyTypes;

			public List<HomingTargetType> HomingTypes;

			// 06001ad2 (1.10.1 RVA 0x541D60)
			public HomingTargetAimDefinition()
			{
			}
		}

		// Original Game.Runtime 020004b8.
		[Serializable]
		public class ChaosAimTarget
		{
			public AbilityAimTransformType BeamTransformType;

			public AbilityAimTransformType DiscTransformType;

			public AbilityAimTransformType RingTransformType;

			public Quaternion TargetRotation = Quaternion.identity;

			public AbilityAimTransformType ArrowsTransformType;

			public Quaternion ArrowsRotation = Quaternion.identity;

			public bool Arrow0Enabled = true;

			[ShowIf("Arrow0Enabled", null)]
			public AnimationCurve Arrow0PositionAnimation;

			[ShowIf("Arrow0Enabled", null)]
			public AnimationCurve Arrow0RotationAnimation;

			public bool Arrow1Enabled = true;

			[ShowIf("Arrow1Enabled", null)]
			public AnimationCurve Arrow1PositionAnimation;

			[ShowIf("Arrow1Enabled", null)]
			public AnimationCurve Arrow1RotationAnimation;

			public bool RequiresLerpToTarget;

			public CameraAbilityTargetDefinition CameraTargetDefinition;

			[Tooltip("Camera override when on different slopes.")]
			public List<CameraAbilitySlopeDefinition> CameraSlopeOverrides;

			public MetadataKeyType CameraOverrideMetadataKey;

			[Tooltip("If grace distance past aim target changes the target then the logic for this will be used instead.")]
			public float RaycastGraceDistance;

			[Tooltip("Whether on exiting target logic, character is orientated to collision normal.")]
			public bool OrientateCharacterOnExit;

			public ActorAnimationDefinition AnimationDefinition;

			private float m_arrow0AnimTimeTotal;

			private float m_arrow1AnimTimeTotal;

			// 06001ad3 (1.10.1 RVA 0x541810)
			public void CalculateCachedValues()
			{
				m_arrow0AnimTimeTotal = Mathf.Max(Arrow0PositionAnimation.TotalTime(), Arrow0RotationAnimation.TotalTime());
				m_arrow1AnimTimeTotal = Mathf.Max(Arrow1PositionAnimation.TotalTime(), Arrow1RotationAnimation.TotalTime());
			}

			// 06001ad4 (1.10.1 RVA 0x541D70). Each arrow clock wraps with a while loop, so a
			// zero cached total never leaves the loop and a NaN time is left untouched.
			public void UpdateAnimation(ref float arrow0AnimTime, ref float arrow1AnimTime, float deltaTime)
			{
				arrow0AnimTime += deltaTime;
				while (arrow0AnimTime >= m_arrow0AnimTimeTotal)
					arrow0AnimTime -= m_arrow0AnimTimeTotal;
				arrow1AnimTime += deltaTime;
				while (arrow1AnimTime >= m_arrow1AnimTimeTotal)
					arrow1AnimTime -= m_arrow1AnimTimeTotal;
			}

			// 06001ad5 (1.10.1 RVA 0x541DE0). Both bounds are inclusive, and a NaN angle
			// fails neither comparison so it selects the first range of the first override.
			public CameraAbilityDefinition GetCameraOverride(float slopeAngle)
			{
				foreach (CameraAbilitySlopeDefinition slopeOverride in CameraSlopeOverrides)
				{
					foreach (Vector2 angleRange in slopeOverride.AngleRanges)
					{
						if (angleRange.x > slopeAngle)
							continue;
						if (slopeAngle > angleRange.y)
							continue;
						return slopeOverride;
					}
				}
				return null;
			}

			// 06001ad6 (1.10.1 RVA 0x5420B0)
			public ChaosAimTarget()
			{
			}
		}
	}
}
