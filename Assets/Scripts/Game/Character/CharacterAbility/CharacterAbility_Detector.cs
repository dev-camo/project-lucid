using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002f2; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_Detector<T> : CharacterAbility<T> where T : CharacterAbilityDefinition_Detector
	{
		private float m_refreshTimer;

		// 060011f8 (1.10.1 RVA 0xE60080); a zero interval disables detection.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (Definition.RefreshInterval == 0f)
			{
				return;
			}
			m_refreshTimer += deltaTime;
			if (!(Definition.RefreshInterval > m_refreshTimer))
			{
				m_refreshTimer = 0f;
				DoOnDetect();
			}
		}

		// 060011f9
		protected abstract void DoOnDetect();

		// 060011fa (1.10.1 RVA 0xE601C0)
		protected CharacterAbility_Detector()
		{
		}
	}
}
