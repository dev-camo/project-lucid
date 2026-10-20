using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002fe; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_HomingAttackAirOneShot : CharacterAbility_HomingAttackAir<CharacterAbilityDefinition_HomingAttackAirOneShot>, IAbilityOneShot
	{
		// 06001232 (1.10.1 RVA 0x74EA30); the ability is told when the target has been hit.
		public override void AddTarget(HomingTarget target)
		{
			base.AddTarget(target);
			target.ActionOnCharacterHit += ActionOnCharacterHit;
		}

		// 06001233 (1.10.1 RVA 0x74EB40)
		public override void RemoveTarget(HomingTarget target)
		{
			base.RemoveTarget(target);
			target.ActionOnCharacterHit -= ActionOnCharacterHit;
		}

		// 06001234 (1.10.1 RVA 0x74EC50); a hit removes the target from the pool.
		private void ActionOnCharacterHit(HomingTarget target)
		{
			RemoveTarget(target);
		}

		// 06001235 (1.10.1 RVA 0x74EC70); always in front of the character, never from the camera or the input direction.
		protected override void UpdateHomingTargetLocation()
		{
			SetHomingTargetTransform(m_character.WorldPosition, m_character.WorldRotation, TargetZoneSize, TargetZoneOffset, 0f);
		}

		// 06001236 (1.10.1 RVA 0x74ED50); a queued press triggers at once; otherwise the last press triggers if it falls
		// within TriggerTimeBefore/After of the current fixed time and is not the press the previous one-shot recorded.
		protected override bool HomingAttackActivated(CharacterBrain brain)
		{
			if (!base.HomingAttackActivated(brain) || m_character.HomingPool.ActiveTarget != null || !m_character.HomingPool.PendingTargetIsValidForAbility(this))
			{
				return false;
			}
			if (!m_character.Storage.TryGetValue(ActorFSMKeys.HomingAttackActionTimestamp, out float lastAttackTimestamp))
			{
				return false;
			}
			if (brain.ActionStateQueued.GetValue(GameAction.CharacterHomingAttackOneShot))
			{
				IsTriggered = true;
				return true;
			}
			float actionTimestamp = brain.GetActionTimestamp(GameAction.CharacterHomingAttackOneShot);
			if (MathUtilities.WithinTolerance(actionTimestamp, lastAttackTimestamp))
			{
				return false;
			}
			float pressOffset = actionTimestamp - m_character.GetTotalFixedTime();
			IsTriggered = pressOffset < 0f ? CanTriggerBefore(-pressOffset) : CanTriggerAfter(pressOffset);
			return IsTriggered;
		}

		// 06001237 (1.10.1 RVA 0x74F1F0)
		public override bool OverridesTargeting
		{
			get
			{
				return true;
			}
		}

		// 06001238 (1.10.1 RVA 0x74F200); a successful lock records when the one-shot was pressed.
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool locked = base.HandleAttack(brain, deltaTime);
			if (locked)
			{
				float actionTimestamp = m_character.GetBrainActionTimestamp(GameAction.CharacterHomingAttackOneShot);
				m_character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, actionTimestamp);
			}
			return locked;
		}

		// 06001239 (1.10.1 RVA 0x74F190)
		public bool CanTriggerBefore(float timeElapsed)
		{
			return timeElapsed < Definition.TriggerTimeBefore;
		}

		// 0600123a (1.10.1 RVA 0x74F130)
		public bool CanTriggerAfter(float timeElapsed)
		{
			return timeElapsed < Definition.TriggerTimeAfter;
		}

		// 0600123b (1.10.1 RVA 0x74F350)
		public override void SetActive(bool active, bool enableForceTrigger = false)
		{
			if (active)
			{
				m_character.Storage.SetValue(ActorFSMKeys.HomingAttackOneShotActive, true);
			}
			else
			{
				m_character.Storage.RemoveValue<bool>(ActorFSMKeys.HomingAttackOneShotActive);
			}
			base.SetActive(active, enableForceTrigger);
		}

		// 0600123c (1.10.1 RVA 0x74F550)
		public CharacterAbility_HomingAttackAirOneShot()
		{
		}
	}
}
