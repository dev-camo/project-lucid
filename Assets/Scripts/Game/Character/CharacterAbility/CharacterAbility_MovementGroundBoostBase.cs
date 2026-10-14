using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002da; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_MovementGroundBoostBase<TAbilityDef> : CharacterAbility_MovementGround<TAbilityDef>, IAbilityTrigger where TAbilityDef : CharacterAbilityDefinition_MovementGroundBoostBase
	{
		public bool IsTriggered { get; private set; }

		private bool m_queuedBoost;

		private float m_timeActivated;

		public float TimeElapsed
		{
			get
			{
				return m_character.GetTotalFixedTime() - m_timeActivated;
			}
		}

		// 06001155 (1.10.1 RVA 0xE646B0)
		public void QueueBoost(bool trigger)
		{
			m_queuedBoost = trigger && !IsTriggered;
		}

		protected abstract bool CanTrigger();

		// 06001157 (1.10.1 RVA 0xE646E0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			CharacterStamina stamina = m_character.BoostStamina;
			if (!IsTriggered && brain.Boost && CanTrigger() && !stamina.UpdatedFromAbility)
			{
				QueueBoost(true);
			}
			if (!m_queuedBoost)
			{
				if (IsTriggered)
				{
					IsTriggered = brain.Boost && stamina.IsTriggered;
					stamina.UpdateFromAbility(Definition.AbilityType, Definition.BoostDefinition);
				}
				return;
			}
			m_queuedBoost = false;
			if (!stamina.UpdatedFromAbility)
			{
				stamina.UpdateFromAbility(Definition.AbilityType, Definition.BoostDefinition, false);
			}
			if (!CanTrigger())
			{
				IsTriggered = false;
				return;
			}
			IsTriggered = stamina.CanTrigger();
			if (IsTriggered)
			{
				m_timeActivated = brain.GetActionTimestamp(GameAction.CharacterBoost);
				stamina.ActivationQueued = true;
				m_character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, Definition.StateTimeMin);
			}
		}

		// 06001158 (1.10.1 RVA 0xE64B40)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			IsTriggered = false;
			m_queuedBoost = false;
			m_character.BoostStamina.Clear();
		}

		// 06001159 (1.10.1 RVA 0xE64B80)
		public override bool GetUIText(StringBuilder stringInfoBuilder)
		{
			base.GetUIText(stringInfoBuilder);
			return m_character.BoostStamina.GetUIText(stringInfoBuilder);
		}

		// 0600115a (1.10.1 RVA 0xE64BB0)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			m_character.BoostStamina.GetDebugExtraInfo(extraInfo);
		}

		// 0600115b (1.10.1 RVA 0xE64C60)
		protected CharacterAbility_MovementGroundBoostBase()
		{
		}
	}
}
