using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002f5; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_DetectorLightspeedDash : CharacterAbility_Detector<CharacterAbilityDefinition_DetectorLightspeedDash>
	{
		private SurfaceLocation m_surfaceLocation;

		private SurfaceDetectorUpdateMessage m_updateMessage = new SurfaceDetectorUpdateMessage
		{
			Type = HomingTargetType.LightspeedDash
		};

		private readonly SystemRef<MessageManager> m_messageManagerRef = ProcessManager.GetSystemRef<MessageManager>();

		// 06001205 (1.10.1 RVA 0x74CD80); a ray from the camera to the found location that hits collision clears the surface.
		protected override void DoOnDetect()
		{
			TrackManager trackManager = m_character.TrackManager;
			if (trackManager.TryGetClosestLightspeedDashSpline(m_character.WorldPosition, Definition.ProximityDistance, ref m_surfaceLocation, false))
			{
				Vector3 cameraPosition = m_character.CameraPosition;
				Vector3 toSurface = m_surfaceLocation.m_worldPosition - cameraPosition;
				float distance = toSurface.magnitude;
				Vector3 direction = toSurface / distance;
				if (Physics.Raycast(cameraPosition, direction, distance, m_character.CollisionMask, QueryTriggerInteraction.Ignore))
				{
					m_surfaceLocation.m_surface = null;
				}
				else
				{
					SetSurfaceLocation();
				}
			}
			PublishDetectorMessage();
		}

		// 06001206 (1.10.1 RVA 0x74CF70); keeps the start or end location of the surface when within the proximity, the nearer end winning.
		private void SetSurfaceLocation()
		{
			SurfaceLocation start = m_surfaceLocation.m_surface.StartLocation();
			SurfaceLocation end = m_surfaceLocation.m_surface.EndLocation();
			Vector3 position = m_character.WorldPosition;
			float startSqrDistance = (start.m_worldPosition - position).sqrMagnitude;
			float endSqrDistance = (end.m_worldPosition - position).sqrMagnitude;
			m_surfaceLocation.m_surface = null;
			if (Definition.ProximityDistanceSqr > startSqrDistance)
			{
				m_surfaceLocation = start;
			}
			if (startSqrDistance > endSqrDistance && Definition.ProximityDistanceSqr > endSqrDistance)
			{
				m_surfaceLocation = end;
			}
		}

		// 06001207 (1.10.1 RVA 0x74D300)
		private void PublishDetectorMessage()
		{
			m_updateMessage.SurfaceLocation = m_surfaceLocation;
			MessageManager manager = m_messageManagerRef.GetSafe();
			if (manager != null)
			{
				Component sender = m_character;
				manager.ComponentMessagesWithCompletion.PublishMessage(in sender, in m_updateMessage, () => { }, -1);
			}
		}

		// 06001208 (1.10.1 RVA 0x74D4C0)
		public CharacterAbility_DetectorLightspeedDash()
		{
		}
	}
}
