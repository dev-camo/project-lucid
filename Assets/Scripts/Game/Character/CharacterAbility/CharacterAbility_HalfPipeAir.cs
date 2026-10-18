using Unity.IL2CPP.CompilerServices;
using Hardlight;
using Hardlight.Utils;
using System.Text;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002cc; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_HalfPipeAir : CharacterAbility_HalfPipe<CharacterAbilityDefinition_HalfPipeAir>
	{
		private float m_airTime;

		private Vector3 m_projectedVelocity;

		private Vector3 m_projectedGravity;

		private ColliderTrackingUtilities.RaycastTransform m_projectedRaycastTransform;

		// 060010f7 (1.10.1 RVA 0x745630); the stored motion index selects an override while its activation time lasts, otherwise the activation speeds do.
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			Vector3 upDirection = m_character.UpDirection;
			float leaveTime = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 0f, true);
			int motionIndex = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -1, true);
			Vector3 motionNormal = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionNormal, upDirection, true);
			AirMotion = Definition.AirMotion;
			m_airTime = 0f;
			if (Vector3.Dot(motionNormal, upDirection) >= Definition.AirMotionExclude.ExcludeAngleCosine)
			{
				motionIndex = -1;
			}
			else
			{
				if (motionIndex >= 0)
				{
					int overrideIndex = Mathf.Min(motionIndex, Definition.AirMotionOverrides.Length - 1);
					CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride motionOverride = Definition.AirMotionOverrides[overrideIndex];
					motionIndex = -1;
					if (motionOverride.ActivationTime > m_character.GetTotalFixedTime() - leaveTime)
					{
						AirMotion = motionOverride;
						motionIndex = overrideIndex;
					}
				}
				if (motionIndex == -1)
				{
					float speed = m_character.WorldVelocityMagnitude;
					int count = Definition.AirMotionOverrides.Length;
					for (int i = 0; i < count; i++)
					{
						CharacterAbilityDefinition_HalfPipeAir.HalfPipeMotionOverride motionOverride = Definition.AirMotionOverrides[i];
						if (motionOverride.ActivationSpeed > speed)
						{
							break;
						}
						AirMotion = motionOverride;
						motionIndex = i;
					}
				}
			}
			m_character.Storage.SetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 0f);
			m_character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionIndex, motionIndex);
			CoroutineUtils.OnFixedUpdate(() =>
			{
				m_projectedVelocity = m_character.WorldVelocity;
				m_projectedGravity = m_character.Gravity;
				m_projectedRaycastTransform = new ColliderTrackingUtilities.RaycastTransform
				{
					LayerMask = m_character.TrackMask,
					Position = m_character.WorldPosition,
					Direction = -m_character.Collider.GetCollisionContactNormal(),
					Up = m_character.WorldUp
				};
			});
		}

		// 060010f8 (1.10.1 RVA 0x745CE0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			m_airTime += deltaTime;
		}

		// 060010f9 (1.10.1 RVA 0x745D50); advances the stored motion index while overrides remain after it.
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_airTime = 0f;
			m_character.Storage.SetValue(ActorFSMKeys.HalfPipeAirLeaveTime, m_character.GetTotalFixedTime());
			int motionIndex = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -1, true);
			if (motionIndex < Definition.AirMotionOverrides.Length - 1)
			{
				m_character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionIndex, motionIndex + 1);
			}
			m_character.Storage.SetValue(ActorFSMKeys.HalfPipeAirMotionNormal, m_character.UpDirection);
		}

		// 060010fa (1.10.1 RVA 0x746140); the base result is discarded, the projection is drawn while there is a projected velocity.
		public override bool OnDrawGizmosSelected()
		{
			base.OnDrawGizmosSelected();
			// Written as a negated "<" so that a NaN velocity still projects and reports true (0x7461E2, 0x74641D).
			if (m_projectedVelocity.sqrMagnitude < 0.0001f)
			{
				return false;
			}
			Vector3 direction = m_projectedVelocity.normalized;
			float magnitude = m_projectedVelocity.magnitude;
			Vector3 up = m_projectedRaycastTransform.Up;
			float deltaTime = Time.fixedDeltaTime;
			ColliderTrackingUtilities.ProjectToNeighbourSlope(m_projectedRaycastTransform, direction, up, magnitude, _ => m_projectedGravity, true, deltaTime, out float _, out Vector3 _, out Vector3 _);
			return true;
		}

		// 060010fb (1.10.1 RVA 0x746490)
		public override bool GetUIText(StringBuilder stringInfoBuilder)
		{
			base.GetUIText(stringInfoBuilder);
			float airTime = m_airTime;
			if (airTime != 0f)
			{
				stringInfoBuilder.AppendLine(string.Format("Air Time = {0:F2}", airTime));
			}
			return airTime != 0f;
		}

		// 060010fc (1.10.1 RVA 0x746550)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			float leaveTime = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirLeaveTime, 0f, true);
			int motionIndex = m_character.Storage.GetValue(ActorFSMKeys.HalfPipeAirMotionIndex, -1, true);
			if (leaveTime > 0f)
			{
				extraInfo.Append(string.Format("LeaveTime = {0:F2} MotionIndex = {1} ", m_character.GetTotalFixedTime() - leaveTime, motionIndex));
			}
			else
			{
				extraInfo.Append(string.Format("AirTime = {0:F2} MotionIndex = {1} ", m_airTime, motionIndex));
			}
		}

		// 060010fd (1.10.1 RVA 0x746830)
		public CharacterAbility_HalfPipeAir()
		{
		}
	}
}
