using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using System.Text;

namespace HardlightProject
{
	// Original Game.Runtime 020002de; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_MovementJumpBase<AbilityDef> : CharacterAbility_MovementAir<AbilityDef> where AbilityDef : CharacterAbilityDefinition_Jump
	{
		public bool Jump { get; private set; }

		private bool m_queuedJump;

		private bool m_endJump;

		private float m_timeActivated;

		public float TimeElapsed
		{
			get
			{
				return m_character.GetTotalFixedTime() - m_timeActivated;
			}
		}

		protected abstract bool CanTrigger();

		// 06001176 (1.10.1 RVA 0xE64CD0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (m_endJump)
			{
				brain.EndAction(GameAction.CharacterJump);
				m_endJump = false;
				return;
			}
			if (m_queuedJump)
			{
				Jump = CanJump && brain.Jump;
				m_queuedJump = false;
				m_timeActivated = brain.GetActionTimestamp(GameAction.CharacterJump);
				return;
			}
			if (Jump)
			{
				Jump = CanJump && brain.Jump;
			}
			else
			{
				Jump = CanJump && brain.Jump && CanTrigger();
			}
		}

		// 06001177 (1.10.1 RVA 0xE64DD0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			Jump = false;
			m_queuedJump = false;
			m_endJump = false;
		}

		// 06001178 (1.10.1 RVA 0xE64E00)
		public void EndJump()
		{
			m_endJump = true;
			Jump = false;
		}

		// 06001179 (1.10.1 RVA 0xE64E20)
		public void QueueJump(bool trigger)
		{
			m_queuedJump = trigger;
		}

		// 0600117a (1.10.1 RVA 0xE64E30)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append("Jump = " + (Jump ? "On" : "Off") + " ");
		}

		// 0600117b (1.10.1 RVA 0xE64F50)
		protected CharacterAbility_MovementJumpBase()
		{
		}
	}
}
