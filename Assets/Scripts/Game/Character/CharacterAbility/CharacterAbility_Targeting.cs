using Unity.IL2CPP.CompilerServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200030b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_Targeting<T> : CharacterAbility<T> where T : CharacterAbilityDefinition
	{
		protected SurfaceLocation m_targetedLocation;

		protected bool m_targetRequiresRefresh;

		protected CharacterTargetObjectData m_targetedObject;

		private float m_lastUpdateTime;

		private float m_currentTime;

		private float m_nextTargetIntervalTime;

		protected abstract float TargetingIntervalSeconds
		{
			get;
		}

		protected abstract float CooldownTimeSeconds
		{
			get;
		}

		protected abstract bool UseTimeScaling
		{
			get;
		}

		// 0600129b (1.10.1 RVA 0xE69160)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			if (!m_character.HomingPool.IsTargetActive(this))
			{
				m_targetedObject.Target = null;
			}
			m_nextTargetIntervalTime = 0f;
			m_targetRequiresRefresh = true;
		}

		// 0600129c (1.10.1 RVA 0xE691C0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			m_currentTime += UseTimeScaling ? deltaTime : Time.fixedDeltaTime;
			if (m_lastUpdateTime + m_nextTargetIntervalTime <= m_currentTime)
			{
				m_lastUpdateTime = m_currentTime;
				m_targetRequiresRefresh = true;
				m_nextTargetIntervalTime = TargetingIntervalSeconds;
			}
		}

		// 0600129d (1.10.1 RVA 0xE69260)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_lastUpdateTime = m_currentTime + CooldownTimeSeconds;
		}

		// 0600129e (1.10.1 RVA 0xE692B0)
		protected bool TargetedLocationIsValid()
		{
			Vector3 worldPosition = m_character.WorldPosition;
			Vector3 direction = m_targetedLocation.m_worldPosition - worldPosition;
			return !Physics.Raycast(worldPosition, direction.normalized, direction.magnitude, m_character.CollisionMask, QueryTriggerInteraction.Ignore);
		}

		// 0600129f (1.10.1 RVA 0xE693F0)
		protected void SetTargetedObject(GameObject gameObject, Vector3 position, Quaternion rotation, HomingTargetType type, TargetObjectMetaData metaData = null)
		{
			m_targetedObject.Target = gameObject;
			m_targetedObject.WorldPosition = position;
			m_targetedObject.WorldRotation = rotation;
			if (CharacterMovementUtilities.AreRotationsReversed(m_character.WorldRotation, rotation))
			{
				m_targetedObject.WorldRotation *= Actor.RotationReverseLocal;
			}
			m_targetedObject.Type = type;
			m_targetedObject.MetaData = metaData;
		}

		// 060012a0 (1.10.1 RVA 0xE695D0)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append("Target = " + (m_targetedObject.Target != null ? m_targetedObject.Target.name : "None") + " ");
		}

		// 060012a1 (1.10.1 RVA 0xE696B0)
		protected CharacterAbility_Targeting()
		{
		}
	}
}
