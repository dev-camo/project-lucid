using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002e0; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_MovementRail<TAbilityDef> : CharacterAbility_MovementTracker<TAbilityDef, RailDefinition>, IAbilityTrigger where TAbilityDef : CharacterAbilityDefinition_MovementRail
	{
		public bool IsTriggered { get; protected set; }

		public bool JumpOnRailTracker { get; private set; }

		private InputDebounce m_inputDebounce;

		private bool m_queuedJump;

		private bool m_isAutoRail;

		private StackableDataHandle m_controlsEnabledHandle;

		private StackableDataHandle m_gravityEnabledHandle;

		// 06001186 (1.10.1 RVA 0xE65500)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_inputDebounce = new InputDebounce(Definition.TurnAroundInputThreshold, Definition.TurnAroundInputTime);
		}

		// 06001187 (1.10.1 RVA 0xE65660)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			ResetTurnAround();
		}

		// 06001188 (1.10.1 RVA 0xE65690)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			m_character.Storage.RemoveValue<bool>(ActorFSMKeys.RailTurnAroundInProgress);
		}

		public abstract void SetActive(bool active);

		// 0600118a (1.10.1 RVA 0xE65810)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			UpdateTurnAroundCooldown(deltaTime);
			if (!IsTriggered)
			{
				return;
			}
			UpdateForAutoRail();
			UpdateForJump(brain.Jump || m_queuedJump);
			m_queuedJump = false;
			if (!CanTurnAround)
			{
				ResetTurnAround();
				return;
			}
			m_inputDebounce.Update(Mathf.Min(m_character.CalculateControllerMovementToCamera(), 0f), deltaTime);
			m_character.Storage.SetValue(ActorFSMKeys.RailTurnAroundInProgress, m_inputDebounce.TimeExceeded);
		}

		// 0600118b (1.10.1 RVA 0xE65B00)
		private void ResetTurnAround()
		{
			m_inputDebounce.Reset(true, true);
			m_character.Storage.SetValue(ActorFSMKeys.RailTurnAroundInProgress, false);
		}

		// 0600118c (1.10.1 RVA 0xE65C20)
		protected override RailDefinition GetTrackerDefinition()
		{
			return DataManager.RailDefinitions[RailType.Default];
		}

		// 0600118d (1.10.1 RVA 0xE65CA0)
		private void UpdateForAutoRail()
		{
			if (m_character.GetModifierValue<bool>((int)GameplayModifierType.AutoRail) || m_isAutoRail)
			{
				CanTurnAround = false;
			}
		}

		// 0600118e (1.10.1 RVA 0xE65D00)
		public override void OnSurfaceChanged(ISurface surface)
		{
			base.OnSurfaceChanged(surface);
			m_isAutoRail = false;
			UpdateForAutoRail();
			if (m_controlsEnabledHandle != null)
			{
				m_character.RemoveModifierOverrides(m_controlsEnabledHandle);
				m_controlsEnabledHandle = null;
			}
			if (m_gravityEnabledHandle != null)
			{
				m_character.RemoveModifierOverrides(m_gravityEnabledHandle);
				m_gravityEnabledHandle = null;
			}
			if (surface == null)
			{
				return;
			}
			RailDefinition railDefinition = GetTrackerDefinition();
			MetadataGroup metadata = surface.GetSurfaceMetadata().GetGroup(railDefinition.MetadataGroupKey);
			metadata.TryGetValue(railDefinition.MetadataAutoRailKey, out m_isAutoRail);
			UpdateForAutoRail();
			UpdateTurnAroundCooldown();
			m_character.AddModifierEnabledOverrideFromMetadata(GameplayModifierType.ControlsEnabled, metadata,
				railDefinition.MetadataControlsKey, ref m_controlsEnabledHandle);
			m_character.AddModifierEnabledOverrideFromMetadata(GameplayModifierType.ApplyGravity, metadata,
				railDefinition.MetadataGravityKey, ref m_gravityEnabledHandle);
		}

		// 0600118f (1.10.1 RVA 0xE65F70)
		public void QueueJump()
		{
			m_queuedJump = true;
		}

		// 06001190 (1.10.1 RVA 0xE65F80)
		public void ClearJump()
		{
			JumpOnRailTracker = false;
		}

		// 06001191 (1.10.1 RVA 0xE65F90)
		private void UpdateForJump(bool activate)
		{
			JumpOnRailTracker = false;
			if (activate && CanJump && Vector3.Dot(m_character.WorldUp, m_character.UpDirection) >= Definition.JumpAngleCosineOffGravityThreshold)
			{
				JumpOnRailTracker = true;
			}
		}

		// 06001192 (1.10.1 RVA 0xE66070)
		private void UpdateTurnAroundCooldown(float deltaTime = 0f)
		{
			float cooldown = m_character.Storage.GetValueOnly<float>(ActorFSMKeys.RailTurnAroundCooldown);
			if (cooldown != 0f)
			{
				CanTurnAround = false;
				if (deltaTime != 0f)
				{
					cooldown -= deltaTime;
					if (cooldown <= 0f)
					{
						m_character.Storage.RemoveValue<float>(ActorFSMKeys.RailTurnAroundCooldown);
					}
					else
					{
						m_character.Storage.SetValue(ActorFSMKeys.RailTurnAroundCooldown, Mathf.Max(cooldown, 0f));
					}
				}
			}
		}

		// 06001193 (1.10.1 RVA 0xE662F0)
		public void UpdateTurnAround(float deltaTime)
		{
			float threshold = Definition.TurnAroundSpeedThreshold;
			float speed = m_character.WorldVelocityMagnitude - Mathf.Abs(m_inputDebounce.Input) * Definition.TurnAroundDeceleration * deltaTime;
			if (m_slopeOverride != null && !m_slopeOverride.CanTurnAround && m_slopeOverride.SpeedThreshold > speed)
			{
				speed = m_slopeOverride.SpeedThreshold;
				threshold = speed;
			}
			m_character.SetWorldVelocity(m_character.WorldVelocityNormalised * speed);
			if (m_inputDebounce.ThresholdExceeded && speed >= threshold)
			{
				return;
			}
			TurnAround = m_inputDebounce.ThresholdExceeded;
			m_character.Storage.SetValue(ActorFSMKeys.RailTurnAroundInProgress, false);
		}

		// 06001194 (1.10.1 RVA 0xE66570)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append("Brain = " + (IsTriggered ? "On" : "Off") + " ");
		}

		// 06001195 (1.10.1 RVA 0xE66690)
		protected CharacterAbility_MovementRail()
		{
		}
	}
}
