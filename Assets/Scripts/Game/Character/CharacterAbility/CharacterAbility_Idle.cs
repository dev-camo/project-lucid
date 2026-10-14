using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002ce; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_Idle : CharacterAbility<CharacterAbilityDefinition_Idle>, IAbilityTrigger
	{
		public bool IsTriggered { get; private set; }

		private float m_enterIdleElapsedTime;

		// 0600110a (1.10.1 RVA 0x747390)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			IsTriggered = false;
			m_enterIdleElapsedTime = 0f;
		}

		// 0600110b (1.10.1 RVA 0x7473E0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			IsTriggered = false;
		}

		// 0600110c (1.10.1 RVA 0x747400)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			// Idle once we've been still (no stick, no slope, no constant push) for long enough.
			Vector3 worldUp = m_character.WorldUp;
			Vector3 velocity = m_character.WorldVelocity;
			Vector3 planarVelocity = velocity - worldUp * Vector3.Dot(worldUp, velocity);
			if (planarVelocity.sqrMagnitude < Definition.MinimumVelocityCheckSqr
				&& Mathf.Approximately(0f, m_character.ControllerMovementMagnitude)
				&& !m_character.HasConstantDirectionModifier
				&& Vector3.Dot(m_character.UpDirection, worldUp) >= m_character.Constants.MovementStationarySlopeCosineAngleMax)
			{
				CharacterAbilityDefinition.Motion gripMotion = m_character.Storage.GetValue<CharacterAbilityDefinition.Motion>(ActorFSMKeys.GroundGripMotion, null, true);
				if (gripMotion != null && !gripMotion.ClampToSpeedMin)
				{
					m_enterIdleElapsedTime += deltaTime;
					IsTriggered = m_enterIdleElapsedTime >= Definition.SecondsInMinimumVelocityCheck;
					return;
				}
			}
			m_enterIdleElapsedTime = 0f;
			IsTriggered = false;
		}

		// 0600110d (1.10.1 RVA 0x747750)
		public CharacterAbility_Idle()
		{
		}
	}
}
