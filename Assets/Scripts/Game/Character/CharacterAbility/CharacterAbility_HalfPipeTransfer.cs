using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002cd; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_HalfPipeTransfer : CharacterAbility_HalfPipe<CharacterAbilityDefinition_HalfPipeTransfer>
	{
		private ColliderTrackingUtilities.RaycastTransform m_raycastTransform;

		private Vector3 m_direction;

		private float m_speed;

		private Vector3 m_gravity;

		private float m_hitTime;

		private Vector3 m_hitPosition;

		private Vector3 m_hitNormal;

		// 06001100 (1.10.1 RVA 0x7469A0)
		public float HitTime
		{
			get
			{
				return m_hitTime;
			}
		}

		// 06001101 (1.10.1 RVA 0x7469B0)
		public Vector3 HitNormal
		{
			get
			{
				return m_hitNormal;
			}
		}

		// 06001102 (1.10.1 RVA 0x7469D0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_speed = 0f;
			m_hitTime = 0f;
		}

		// 06001103 (1.10.1 RVA 0x746A00); the stored gravity is the supplied one with the air motion's multiplier divided out.
		public void CacheTransform(CharacterAbilityDefinition_HalfPipe.HalfPipeMotion airMotion, ColliderTrackingUtilities.RaycastTransform raycastTransform, Vector3 direction, float speed, Vector3 gravity)
		{
			m_raycastTransform = raycastTransform;
			m_direction = direction;
			m_speed = speed;
			m_gravity = gravity;
			AirMotion = airMotion;
			float gravityMultiplier = GetGravityMultiplier(m_character.WorldVelocity, out Vector3 _);
			m_gravity = gravity / gravityMultiplier;
		}

		// 06001104 (1.10.1 RVA 0x746B40)
		public bool ProjectToHalfPipe(float deltaTime)
		{
			Vector3 worldUp = m_character.WorldUp;
			ColliderTrackingUtilities.DebugBeginLocationInstantiation(m_character);
			bool projected = ColliderTrackingUtilities.ProjectToNeighbourSlope(m_raycastTransform, m_direction, worldUp, m_speed, GetGravity, false, deltaTime, out m_hitTime, out m_hitPosition, out m_hitNormal);
			ColliderTrackingUtilities.DebugEndLocationInstantiation();
			if (projected)
			{
				Vector3 toHit = m_hitPosition - m_character.WorldPosition;
				Vector3 planar = toHit - worldUp * Vector3.Dot(worldUp, toHit);
				Vector3 lipDirection = Vector3.Cross(planar, worldUp).normalized;
				m_direction = Vector3.ProjectOnPlane(m_direction, lipDirection);
				float speed = m_character.WorldVelocityMagnitude;
				Vector3 velocity = Vector3.ProjectOnPlane(m_character.WorldVelocity, lipDirection).normalized;
				m_character.SetWorldVelocity(velocity * speed);
				// The shipped method evaluates this rotation and discards it (0x7470B9).
				_ = Quaternion.LookRotation(m_hitNormal, worldUp);
			}
			return projected;
		}

		// 06001105 (1.10.1 RVA 0x7470E0)
		public override bool OnDrawGizmosSelected()
		{
			float hitTime = 0f;
			Vector3 hitPosition = Vector3.zero;
			Vector3 hitNormal = Vector3.zero;
			bool selected = base.OnDrawGizmosSelected();
			if (selected)
			{
				ColliderTrackingUtilities.ProjectToNeighbourSlope(m_raycastTransform, m_direction, Vector3.up, m_speed, GetGravity, true, Time.fixedDeltaTime, out hitTime, out hitPosition, out hitNormal);
			}
			return selected;
		}

		// 06001106 (1.10.1 RVA 0x7472B0)
		private Vector3 GetGravity(Vector3 velocity)
		{
			float gravityMultiplier = GetGravityMultiplier(velocity, out Vector3 _);
			return gravityMultiplier * m_gravity;
		}

		// 06001107 (1.10.1 RVA 0x747330)
		public CharacterAbility_HalfPipeTransfer()
		{
		}
	}
}
