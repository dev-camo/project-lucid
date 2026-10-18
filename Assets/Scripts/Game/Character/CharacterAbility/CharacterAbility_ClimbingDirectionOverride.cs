using Unity.IL2CPP.CompilerServices;
using Hardlight;
using System.Text;

namespace HardlightProject
{
	// Original Game.Runtime 020002c7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_ClimbingDirectionOverride : CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingDirectionOverride>
	{
		private bool m_directionOverrideActivePrevious;

		// 060010d8 (1.10.1 RVA 0x7452B0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			m_directionOverrideActivePrevious = m_character.IsDirectionOverrideActive();
		}

		// 060010d9 (1.10.1 RVA 0x745320); the stored request is consumed only when an override was active at the previous update.
		protected override bool CanEnterClimb()
		{
			bool enterWhenOverrideEnds = false;
			if (!m_directionOverrideActivePrevious)
			{
				return false;
			}
			if (m_character.Storage.TryGetValue(ActorFSMKeys.ClimbingEnterWhenDirectionOverrideEnds, out enterWhenOverrideEnds))
			{
				m_character.Storage.RemoveValue<bool>(ActorFSMKeys.ClimbingEnterWhenDirectionOverrideEnds);
			}
			return enterWhenOverrideEnds;
		}

		// 060010da (1.10.1 RVA 0x745480); "modifer" is the original spelling.
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append(", direction modifer active = " + m_character.IsDirectionOverrideActive().ToString());
		}

		// 060010db (1.10.1 RVA 0x745540)
		public CharacterAbility_ClimbingDirectionOverride()
		{
		}
	}
}
