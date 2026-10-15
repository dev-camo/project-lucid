using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using System.Text;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002cb; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterAbility_HalfPipe<T> : CharacterAbility<T> where T : CharacterAbilityDefinition_HalfPipe
	{
		private float m_leanStart;

		private float m_leanTimer;

		private float m_activeTimer;

		private CharacterAbility_HalfPipeSide m_entrySide;

		public CharacterAbilityDefinition_HalfPipe.HalfPipeMotion AirMotion
 { get
; protected set
; }

		public bool AirActive { get; private set; }

		// 060010f0 (1.10.1 RVA 0xE60B30)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			Vector3 worldUp = m_character.WorldUp;
			float side = Vector3Extensions.SignOfAngle(m_character.ForwardDirection, worldUp, m_character.Collider.GetCollisionContactNormal());
			m_entrySide = side >= 0f ? CharacterAbility_HalfPipeSide.Right : CharacterAbility_HalfPipeSide.Left;
			m_character.Storage.SetValue(ActorFSMKeys.HalfPipeColliderEntrySide, m_entrySide);
			AirActive = false;
			m_leanStart = m_character.ControllerMovement.x;
			m_leanTimer = 0f;
			m_activeTimer = 0f;
		}

		// 060010f1 (1.10.1 RVA 0xE60CF0)
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			AirActive = brain.AirActive || (brain.AirStompAttack && m_character.HasAbilityType(ActorAbilityType.Character_AirStompAttack));
			float lean = LeanValue();
			m_leanTimer = lean != 0f ? m_leanTimer + deltaTime : 0f;
			m_activeTimer += deltaTime;
			if (brain.AirActive && m_activeTimer <= Definition.ExitAirTimeThreshold)
			{
				AirActive = false;
				// Only the air-active impulse ends here; a simultaneous stomp input stays available (0xE60ECA-0xE60EE1).
				m_character.EndAirActiveImpulse();
			}
		}

		// 060010f2 (1.10.1 RVA 0xE60EF0)
		public float GetGravityMultiplier(Vector3 velocity, out Vector3 clampedVelocity)
		{
			if (AirMotion == null)
			{
				clampedVelocity = velocity;
				return 1f;
			}
			Vector3 worldUp = m_character.WorldUp;
			Vector3 verticalVelocity = Vector3.Project(velocity, worldUp);
			float verticalSpeed = Vector3.Dot(worldUp, verticalVelocity);
			CharacterAbilityDefinition_HalfPipe.HalfPipeGravity gravity = AirMotion.Gravity;
			float closestThreshold = float.MaxValue;
			foreach (CharacterAbilityDefinition_HalfPipe.HalfPipeGravityOverride gravityOverride in AirMotion.GravityOverrides)
			{
				if (Mathf.Abs(verticalSpeed) <= gravityOverride.VerticalSpeedThreshold)
				{
					float difference = gravityOverride.VerticalSpeedThreshold - Mathf.Abs(verticalSpeed);
					if (difference <= closestThreshold)
					{
						gravity = gravityOverride.Gravity;
						closestThreshold = difference;
					}
				}
			}
			clampedVelocity = velocity;
			if (verticalSpeed < -gravity.TerminalSpeed)
			{
				clampedVelocity = velocity - verticalVelocity - worldUp * gravity.TerminalSpeed;
				verticalSpeed = -gravity.TerminalSpeed;
			}
			return verticalSpeed >= 0f ? gravity.GravityMultiplierAscending : gravity.GravityMultiplierDescending;
		}

		// 060010f3 (1.10.1 RVA 0xE61170)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append(string.Format("EntrySide = {0} ", m_entrySide));
		}

		// 060010f4 (1.10.1 RVA 0xE61200)
		public bool ExitFromLean()
		{
			return m_leanTimer >= Definition.ExitLeanTime;
		}

		// 060010f5 (1.10.1 RVA 0xE61290)
		private float LeanValue()
		{
			float input = m_character.ControllerMovement.x;
			if (m_leanStart == 0f)
			{
				float deadzone = Definition.ExitLeanDeadzone;
				if (Mathf.Abs(input) > deadzone)
				{
					return Mathf.Sign(input) * (Mathf.Abs(input) - deadzone) - deadzone;
				}
				return 0f;
			}
			m_leanStart = input;
			return 0f;
		}

		// 060010f6 (1.10.1 RVA 0xE61370)
		protected CharacterAbility_HalfPipe()
		{
		}
	}
}
