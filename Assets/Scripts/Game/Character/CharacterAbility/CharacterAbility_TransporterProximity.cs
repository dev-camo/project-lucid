using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200030d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_TransporterProximity : CharacterAbility_TargetingHoming<CharacterAbilityDefinition_TransporterProximity>, IAbilityTrigger
	{
		// 060012b3 (1.10.1 RVA 0x754570); m_targetRequiresRefresh is reused as the "an auto-attach surface is stored" flag.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			if (IsTriggered)
			{
				return;
			}
			// The original clears the already false flag here (0x754617).
			IsTriggered = false;
			if (!m_targetRequiresRefresh)
			{
				m_targetRequiresRefresh = m_character.Storage.GetValue<ISurface>(ActorFSMKeys.TransporterAutoAttach, null, true) != null;
				if (!m_targetRequiresRefresh)
				{
					return;
				}
			}
			if (ProximityCheckToTargetedObject(Definition.HomingDistance))
			{
				float targetSpeed = Mathf.Max(m_character.WorldVelocityMagnitude, Definition.HomingVelocityMin);
				IsTriggered = TrySetTargetImmediately(m_targetedObject, m_character.WorldVelocity, targetSpeed, Definition.HomingRotationalVelocity);
			}
		}

		// 060012b4 (1.10.1 RVA 0x754800); an auto-attached surface is trusted at any distance, otherwise the nearest transporter must be within HomingDistanceSqr.
		private bool ProximityCheckToTargetedObject(float proximityDistance)
		{
			if (proximityDistance == 0f)
			{
				return false;
			}
			// The original reads the TrackManager getter again for the search below (0x7548A5, 0x754A25).
			if (m_character.TrackManager == null)
			{
				return false;
			}
			Vector3 position = m_character.WorldPosition;
			ISurface attachedSurface = m_character.Storage.GetValue<ISurface>(ActorFSMKeys.TransporterAutoAttach, null, true);
			if (attachedSurface != null)
			{
				m_targetedLocation = attachedSurface.FindNearestSurfaceLocationFromWorld(m_character.WorldPosition);
			}
			else if (!m_character.TrackManager.TryGetTransporterHit(position, proximityDistance, ref m_targetedLocation))
			{
				return false;
			}
			if (!(m_targetedLocation.m_surface is Component component))
			{
				return false;
			}
			if (attachedSurface == null && (position - m_targetedLocation.m_worldPosition).sqrMagnitude > Definition.HomingDistanceSqr)
			{
				return false;
			}
			if (!TargetedLocationIsValid())
			{
				return false;
			}
			SetTargetedObject(component.gameObject, m_targetedLocation.m_worldPosition, m_targetedLocation.m_worldRotation, HomingTargetType.Transporter);
			return true;
		}

		// 060012b5 (1.10.1 RVA 0x754C30)
		public CharacterAbility_TransporterProximity()
		{
		}
	}
}
