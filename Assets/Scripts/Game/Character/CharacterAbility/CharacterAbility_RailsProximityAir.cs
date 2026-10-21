using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000307; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_RailsProximityAir : CharacterAbility_RailsProximity<CharacterAbilityDefinition_RailsProximityAir>
	{
		// 06001274 (1.10.1 RVA 0x750490); only an airborne character attaches.
		protected override bool ValidToTrigger()
		{
			return !m_character.Collider.GroundHit.Hit;
		}

		// 06001275 (1.10.1 RVA 0x7504C0); the surface must face the direction of travel within the angle tolerance. While
		// the rail tracker is active the character must instead be moving towards the surface and within the attach distance.
		protected override bool CanJoinSurface(Vector3 position, SurfaceLocation surfaceLocation, bool railTrackerActive)
		{
			if (!base.CanJoinSurface(position, surfaceLocation, railTrackerActive))
			{
				return false;
			}
			Vector3 surfaceNormal = surfaceLocation.m_worldRotation * Vector3.up;
			float velocityDot = Vector3.Dot(m_character.WorldVelocityNormalised, surfaceNormal);
			if (!railTrackerActive)
			{
				return Definition.SurfaceNormalAngleCosineTolerance >= velocityDot;
			}
			float distanceAlongNormal = Vector3.Dot(surfaceNormal, position - surfaceLocation.m_worldPosition);
			if (velocityDot > 0f && distanceAlongNormal > -Definition.RailTrackerAttachDistance)
			{
				return true;
			}
			return velocityDot < 0f && Definition.RailTrackerAttachDistance > distanceAlongNormal;
		}

		// 06001276 (1.10.1 RVA 0x7506E0)
		public CharacterAbility_RailsProximityAir()
		{
		}
	}
}
