using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000306; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE67E00..0xE68B10). A homing ability that attaches the character to a rail it is close to.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterAbility_RailsProximity<T> : CharacterAbility_TargetingHoming<T>, IAbilityTrigger where T : CharacterAbilityDefinition_RailsHoming
	{
		private const float RailHomingReactivateTime = 0.1f;

		// 0600126d (1.10.1 RVA 0xE67E00)
		protected override AnimationCurve AdditionalSpeedCurve
		{
			get
			{
				return Definition.HomingAdditionalSpeed;
			}
		}

		// 0600126e (1.10.1 RVA 0xE67E70); the native body inlines the targeting timer update.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (IsTriggered)
			{
				return;
			}
			IsTriggered = false;
			if (!ValidToTrigger())
			{
				m_targetRequiresRefresh = true;
				return;
			}
			bool jumpOnRailTracker = m_character.Storage.GetValue(ActorFSMKeys.JumpOnRailTracker, false, true);
			if (jumpOnRailTracker && m_character.LocalVelocity.y > 0f)
			{
				return;
			}
			if (!m_targetRequiresRefresh)
			{
				m_targetRequiresRefresh = m_character.Storage.GetValue<ISurface>(ActorFSMKeys.RailAutoAttach, null, true) != null;
				if (!m_targetRequiresRefresh)
				{
					return;
				}
			}
			if (Definition.HomingDistance == 0f)
			{
				return;
			}
			if (!ProximityCheckToTargetedObject(Definition.HomingDistance, jumpOnRailTracker))
			{
				return;
			}
			float timeSinceRailActive = m_character.GetTotalFixedTime() - m_character.Storage.GetValue(ActorFSMKeys.RailActiveLastTime, 0f, true);
			// The native comparison is unordered-true, so a NaN time still reactivates (0xE682A5-0xE682B6).
			if (!jumpOnRailTracker && timeSinceRailActive < RailHomingReactivateTime)
			{
				return;
			}
			float targetSpeed = Mathf.Max(m_character.WorldVelocityMagnitude, Definition.HomingVelocityMin);
			IsTriggered = TrySetTargetImmediately(m_targetedObject, m_character.WorldVelocity, targetSpeed, Definition.HomingRotationalVelocity);
		}

		// 0600126f has no native body: original abstract contract.
		protected abstract bool ValidToTrigger();

		// 06001270 (1.10.1 RVA 0xE683A0); finds the rail surface in range and publishes it as the targeted object.
		private bool ProximityCheckToTargetedObject(float proximityDistance, bool jumpOnRailTracker)
		{
			Vector3 position = m_character.WorldPosition;
			if (m_character.TrackManager == null)
			{
				return false;
			}
			ISurface autoAttachSurface = m_character.Storage.GetValue<ISurface>(ActorFSMKeys.RailAutoAttach, null, true);
			if (autoAttachSurface != null)
			{
				m_targetedLocation = autoAttachSurface.FindNearestSurfaceLocationFromWorld(m_character.WorldPosition);
			}
			else if (!m_character.TrackManager.TryGetRailHit(position, proximityDistance, ref m_targetedLocation))
			{
				return false;
			}
			if (!(m_targetedLocation.m_surface is Component surfaceComponent))
			{
				return false;
			}
			if (!CanJoinSurface(position, m_targetedLocation, jumpOnRailTracker))
			{
				return false;
			}
			if (!TargetedLocationIsValid())
			{
				return false;
			}
			SetTargetedObject(surfaceComponent.gameObject, m_targetedLocation.m_worldPosition, m_targetedLocation.m_worldRotation, HomingTargetType.Rail);
			return true;
		}

		// 06001271 (1.10.1 RVA 0xE68A50)
		protected virtual bool CanJoinSurface(Vector3 position, SurfaceLocation surfaceLocation, bool railTrackerActive)
		{
			return surfaceLocation.m_surface != null;
		}

		// 06001272 (1.10.1 RVA 0xE68A60)
		protected override void GetDebugExtraInfo(StringBuilder extraInfo)
		{
			base.GetDebugExtraInfo(extraInfo);
			extraInfo.Append("Indicator = " + (m_targetedLocation.m_surface == null ? "Off" : "On") + " ");
		}

		// 06001273 (1.10.1 RVA 0xE68B10)
		protected CharacterAbility_RailsProximity()
		{
		}
	}
}
