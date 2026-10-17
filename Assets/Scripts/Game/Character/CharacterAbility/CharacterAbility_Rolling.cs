using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002e7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_Rolling : CharacterAbility_MovementGround<CharacterAbilityDefinition_Rolling>, IAbilityTrigger
	{
		public bool IsRollingTriggered { get; private set; }

		public bool IsRollingExiting { get; private set; }

		public bool IsTriggered
		{
			get
			{
				return IsRollingTriggered;
			}
		}

		// 060011c2 (1.10.1 RVA 0x74B120)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			bool rollApplied = m_character.GetBrainAppliedState(GameAction.CharacterRoll);
			bool charging = m_character.SpinDashRoll.ChargeTime > 0f;
			UpdateTrigger(rollApplied || charging);
		}

		// 060011c3 (1.10.1 RVA 0x74B2E0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			bool charging = m_character.SpinDashRoll.ChargeTime > 0f;
			UpdateTrigger(brain.Roll || charging);
		}

		// 060011c4 (1.10.1 RVA 0x74B1A0)
		private void UpdateTrigger(bool active)
		{
			bool rollingActive = m_character.Storage.GetValue(ActorFSMKeys.RollingActive, false, true);
			IsRollingExiting = !active && (rollingActive || IsRollingTriggered);
			IsRollingTriggered = active;
		}

		// 060011c5 (1.10.1 RVA 0x74B370)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			IsRollingTriggered = false;
			IsRollingExiting = false;
		}

		// 060011c6 (1.10.1 RVA 0x74B390)
		public CharacterAbility_Rolling()
		{
		}
	}
}
