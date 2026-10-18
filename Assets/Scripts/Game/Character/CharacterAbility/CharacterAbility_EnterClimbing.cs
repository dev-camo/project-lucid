using Unity.IL2CPP.CompilerServices;
using Hardlight;
using System.Text;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002c9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_EnterClimbing<T> : CharacterAbility<T>, IAbilityTrigger where T : CharacterAbilityDefinition_EnterClimbing
	{
		public bool IsTriggered { get; private set; }

		public CharacterClimbingCollisionTracker ClimbingCollisionTracker { get; private set; }

		private TimeManager m_timeManager;

		// 060010e3 (1.10.1 RVA 0xE60220)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			ClimbingCollisionTracker = new CharacterClimbingCollisionTracker(m_character, Definition.TrackMask);
			m_timeManager = ProcessManager.GetSystem<TimeManager>();
		}

		// 060010e4 (1.10.1 RVA 0xE60370)
		private bool CollidingWithClimbableSurface()
		{
			return ClimbingCollisionTracker.IsColliding && Definition.SurfaceAngleValid(GetSlopeAngle());
		}

		// 060010e5 (1.10.1 RVA 0xE60410); 180 minus the angle between gravity and the collision normal.
		protected virtual float GetSlopeAngle()
		{
			return 180f - Vector3.Angle(m_character.Gravity, ClimbingCollisionTracker.CollisionNormal);
		}

		// 060010e6
		protected abstract bool CanEnterClimb();

		// 060010e7 (1.10.1 RVA 0xE605A0)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			ClimbingCollisionTracker.Start();
		}

		// 060010e8 (1.10.1 RVA 0xE605D0)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			IsTriggered = false;
			ClimbingCollisionTracker.Stop();
		}

		// 060010e9 (1.10.1 RVA 0xE60600); the native code inlines CollidingWithClimbableSurface.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			ClimbingCollisionTracker.Update();
			IGraphStorage storage = m_character.Storage;
			float lastClimbTime = storage.GetValue(ActorFSMKeys.ClimbingLastTimestamp, 0f, true);
			float timeSinceClimbing = m_timeManager.GetTotalTime(TimeCategory.PlayerMovement) - lastClimbTime;
			if (timeSinceClimbing >= Definition.ReAttachCooldownSeconds && CollidingWithClimbableSurface() && !m_character.IsDirectionOverrideActive())
			{
				IsTriggered = CanEnterClimb();
				if (IsTriggered)
				{
					m_character.Storage.SetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, ClimbingCollisionTracker.WorldVelocityAtLastTerrainEnter);
					m_character.Storage.SetValue(ActorFSMKeys.ClimbingEnterCollisionNormal, ClimbingCollisionTracker.CollisionNormal);
				}
			}
			else
			{
				IsTriggered = false;
			}
		}

		// 060010ea (1.10.1 RVA 0xE60A40)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append("CanClimb = " + (IsTriggered ? "On" : "Off"));
		}

		// 060010eb (1.10.1 RVA 0xE60AD0)
		protected CharacterAbility_EnterClimbing()
		{
		}
	}
}
