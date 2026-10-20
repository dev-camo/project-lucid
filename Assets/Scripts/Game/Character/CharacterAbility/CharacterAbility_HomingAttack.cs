using Unity.IL2CPP.CompilerServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002fb; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE62660..0xE63AC0). A homing attack that locks the pending target and chains combo hits.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_HomingAttack<T> : CharacterAbility_HomingAttackBase<T> where T : CharacterAbilityDefinition_HomingAttack
	{
		private int m_comboCount;

		private float m_comboTimer;

		private StackableDataHandle m_comboTimeOverride;

		// 0600121e (1.10.1 RVA 0xE62660)
		protected override AnimationCurve AdditionalSpeedCurve
		{
			get
			{
				return Definition.HomingAdditionalSpeed;
			}
		}

		// 0600121f (1.10.1 RVA 0xE626D0)
		public virtual void SetActive(bool active, bool enableForceTrigger = false)
		{
			if (active)
			{
				ActiveHomingTarget activeTarget = m_character.HomingPool.ActiveTarget;
				if (activeTarget != null && activeTarget.Object.TryGetMetaDataAsType(out HomingObjectMetaData metaData))
				{
					metaData.HomingTarget.DoOnActive(enableForceTrigger, m_character);
					m_character.Storage.SetValue(ActorFSMKeys.HomingTargetType, metaData.HomingTarget.TargetType);
				}
				m_character.Storage.SetValue(ActorFSMKeys.HomingTargetTime, 0f);
			}
			else if (!m_character.Storage.TryGetValue(ActorFSMKeys.HomingAttackOneShotActive, out bool oneShotActive) || !oneShotActive)
			{
				m_character.HomingPool.ClearTarget();
			}
		}

		// 06001220 (1.10.1 RVA 0xE629B0); keeps the active target while it stays valid and unobstructed. A deactivated
		// target (squared distance under DeactivationDistance, or a lightspeed dash target stored) is released, and is
		// cancelled only if it has also timed out or left the hierarchy.
		public virtual bool UpdateTarget(float deltaTime, out ActiveHomingTarget activeTarget, out float time, out Vector3 characterToTarget, out float characterToTargetDistanceSqr)
		{
			time = 0f;
			activeTarget = m_character.HomingPool.ActiveTarget;
			characterToTarget = Vector3.zero;
			characterToTargetDistanceSqr = 0f;
			if (activeTarget == null || !activeTarget.Object.TryGetMetaDataAsType(out HomingObjectMetaData metaData))
			{
				return false;
			}
			HomingTarget homingTarget = metaData.HomingTarget;
			Vector3 center = m_character.Bounds.center;
			characterToTarget = homingTarget.WorldPosition - center;
			characterToTargetDistanceSqr = characterToTarget.sqrMagnitude;
			bool deactivated = Definition.DeactivationDistance > characterToTargetDistanceSqr || m_character.Storage.HasValue<ISpline>(ActorFSMKeys.LightspeedDashTarget);
			time = m_character.Storage.AdjustValue(ActorFSMKeys.HomingTargetTime, deltaTime);
			// The native comparison is unordered-true, so a NaN time still counts as inside the timeout.
			bool withinTimeout = !(time >= Definition.TimeoutSeconds);
			bool targetActive = homingTarget.gameObject.activeInHierarchy;
			bool valid = withinTimeout && targetActive;
			bool blocked = false;
			if (!deactivated && valid)
			{
				blocked = Physics.Raycast(center, characterToTarget, characterToTarget.magnitude, m_character.CollisionMask, QueryTriggerInteraction.Ignore);
				if (!blocked)
				{
					return true;
				}
			}
			ClearTarget(homingTarget, !valid || blocked);
			return false;
		}

		// 06001221 (1.10.1 RVA 0xE62DF0)
		private void ClearTarget(HomingTarget homingTarget, bool wasCancelled)
		{
			if (wasCancelled)
			{
				homingTarget.DoOnCancelled();
			}
			m_character.HomingPool.ClearTarget();
		}

		// 06001222 (1.10.1 RVA 0xE62E30)
		public override void OnHomingTargetHit(HomingTarget target)
		{
			AddComboHit();
			m_targetRequiresRefresh = true;
			base.OnHomingTargetHit(target);
		}

		// 06001223 (1.10.1 RVA 0xE62E90)
		public override void RemoveTarget(HomingTarget target)
		{
			if (ValidateTargetType(target))
			{
				base.RemoveTarget(target);
			}
		}

		// 06001224 (1.10.1 RVA 0xE62F40)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			ClearComboHits();
			IsTriggered = false;
			if (m_character.HomingPool.IsTargetActive(this))
			{
				m_character.HomingPool.ClearTarget();
			}
		}

		// 06001225 (1.10.1 RVA 0xE63000); a direction override only allows homing that overrides targeting.
		protected virtual bool HomingAttackActivated(CharacterBrain brain)
		{
			if (m_character.IsDirectionOverrideActive())
			{
				return m_character.HomingPool.ActiveTargetOverridesTargeting();
			}
			return true;
		}

		// 06001226 (1.10.1 RVA 0xE63040); locks the pending target at the speed that reaches it, then ages the combo.
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool locked = false;
			if (m_character.HomingPool.PendingTargetIsValidForAbility(this) && HomingAttackActivated(brain))
			{
				CharacterTargetObjectData pendingTarget = m_character.HomingPool.PendingTarget.Object;
				if (pendingTarget.MetaData.IsTargetable)
				{
					Vector3 toTarget = pendingTarget.Target.transform.position - m_character.WorldPosition;
					float distance = toTarget.magnitude;
					float speed;
					if (Definition.HomingVelocityFromTime && Definition.HomingVelocityTime > 0f)
					{
						speed = distance / Definition.HomingVelocityTime;
					}
					else
					{
						Vector3 direction = toTarget / distance;
						speed = Mathf.Min(Mathf.Max(Vector3.Dot(m_character.WorldVelocity, direction), Definition.HomingVelocityMin), Definition.HomingVelocityMax);
					}
					locked = m_character.HomingPool.LockPendingTarget(speed, m_character.WorldVelocity);
				}
			}
			UpdateComboHits(deltaTime);
			return locked;
		}

		// 06001227 (1.10.1 RVA 0xE63480); each hit within the configured levels applies or updates that level's time setting; past the last level the current setting is left in place.
		private void AddComboHit()
		{
			if (m_comboCount < Definition.ComboTimeSettings.Length)
			{
				TimeManager timeManager = ProcessManager.GetSystem<TimeManager>();
				TimeSettings_SDT setting = Definition.ComboTimeSettings[m_comboCount];
				if (m_comboTimeOverride == null)
				{
					m_comboTimeOverride = timeManager.ApplyTimeSetting_SDT(setting);
				}
				else
				{
					timeManager.UpdateTimeSetting(m_comboTimeOverride, setting);
				}
			}
			m_comboCount++;
			m_comboTimer = Definition.ComboBufferTime;
		}

		// 06001228 (1.10.1 RVA 0xE636A0); the combo ends once its buffer has run out.
		private void UpdateComboHits(float deltaTime)
		{
			if (m_comboTimer > 0f)
			{
				m_comboTimer = Mathf.Max(m_comboTimer - deltaTime, 0f);
				if (m_comboTimer > 0f)
				{
					return;
				}
			}
			ClearComboHits();
		}

		// 06001229 (1.10.1 RVA 0xE636E0)
		private void ClearComboHits()
		{
			if (m_comboTimeOverride != null)
			{
				ProcessManager.GetSystem<TimeManager>().RemoveTimeSetting(m_comboTimeOverride);
				m_comboTimeOverride = null;
			}
			m_comboCount = 0;
		}

		// 0600122a (1.10.1 RVA 0xE63780)
		protected override bool ValidateTargetType(HomingTarget target)
		{
			return Definition.HomingMask.IncludesLayer(target.gameObject) && Definition.HasTargetType(target.TargetType);
		}

		// 0600122b (1.10.1 RVA 0xE63890)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append(string.Format("ProjectDistance = {0} ", Definition.TargetDistance));
			extraInfo.Append(string.Format("InputDiscardTargetAngle = {0:F1} ", Definition.InputDiscardTargetAngle));
			extraInfo.Append(string.Format("TargetMaxPitchAngle = {0:F1} ", Definition.TargetMaxPitchAngle));
		}

		// 0600122c (1.10.1 RVA 0xE63AC0)
		protected CharacterAbility_HomingAttack()
		{
		}
	}
}
