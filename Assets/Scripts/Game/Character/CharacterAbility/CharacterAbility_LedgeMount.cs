using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002d1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_LedgeMount : CharacterAbility<CharacterAbilityDefinition_LedgeMount>, IAbilityTrigger
	{
		// 06001118 (1.10.1 RVA 0x747AB0) and 06001119 (RVA 0x747AC0)
		public bool IsTriggered { get; private set; }

		// 0600111a (1.10.1 RVA 0x747AD0) and 0600111b (RVA 0x747AF0)
		public Vector3 LedgeSurfaceNormal { get; private set; } = Vector3.zero;

		// 0600111c (1.10.1 RVA 0x747B10) and 0600111d (RVA 0x747B30)
		public Vector3 LedgeMountPoint { get; private set; } = Vector3.zero;

		// 0600111e (1.10.1 RVA 0x747B50)
		protected override void DoOnLeave()
		{
			base.DoOnLeave();
			IsTriggered = false;
		}

		// 0600111f (1.10.1 RVA 0x747B70); a mountable ledge triggers the ability, while the first climbable
		// (non mountable) ledge hit is added to the stored edge normals.
		protected sealed override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			FindLedges(out bool mountableLedgeHit, out RaycastHit mountableLedge, out bool nonMountableLedgeHit, out RaycastHit nonMountableLedge);
			if (mountableLedgeHit)
			{
				LedgeSurfaceNormal = mountableLedge.normal;
				LedgeMountPoint = mountableLedge.point;
				IsTriggered = true;
				return;
			}
			IsTriggered = false;
			if (nonMountableLedgeHit)
			{
				m_character.Storage.GetValue<List<Vector3>>(ActorFSMKeys.ClimbingNonMountableEdgeNormal, null, true).Add(nonMountableLedge.normal);
			}
		}

		// 06001120 (1.10.1 RVA 0x747DC0); raycasts from the character's down (or back) direction round to its back (or down)
		// direction. A hit whose surface is not climbable ends the search as the mountable ledge, a hit facing away from the
		// character, or a climbable one, is remembered (the first) as the non mountable ledge. Comparisons keep the native NaN behaviour.
		private void FindLedges(out bool mountableLedgeHit, out RaycastHit mountableLedge, out bool nonMountableLedgeHit, out RaycastHit nonMountableLedge)
		{
			mountableLedgeHit = false;
			nonMountableLedgeHit = false;
			mountableLedge = default(RaycastHit);
			nonMountableLedge = default(RaycastHit);
			CharacterAbilityDefinition_LedgeMount definition = Definition;
			Vector3 up = m_character.UpDirection;
			Vector3 origin = m_character.WorldPosition;
			Vector3 originOffset = m_character.WorldRotation * definition.RaycastOriginOffset;
			Vector3 forward = m_character.ForwardDirection;
			Vector3 down = -up;
			Vector3 back = -forward;
			Vector3 searchFrom = definition.ReverseSearchOrder ? down : back;
			Vector3 searchTo = definition.ReverseSearchOrder ? back : down;
			LayerMask trackMask = Definition.TrackMask;
			int steps = definition.RaycastSteps;
			float raycastDistance = definition.RaycastDistance;
			Vector3 gravityNormalised = m_character.GravityNormalised;
			Vector3 previousNormal = Vector3.zero;
			TrackManager trackManager = m_character.TrackManager;
			Vector3 forwardNormalised = m_character.ForwardDirection.normalized;
			float forwardPlane = Vector3.Dot(m_character.WorldPosition, forwardNormalised);
			origin += originOffset;
			for (int step = 0; step < steps; step++)
			{
				Vector3 direction = Vector3.Slerp(searchFrom, searchTo, step / (float)(steps - 1));
				if (!Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, trackMask, QueryTriggerInteraction.Ignore))
				{
					continue;
				}
				// Hits that are not strictly in front of the character are skipped (also when the distance is NaN).
				if (Definition.IgnoreHitsBehind && !(Vector3.Dot(hit.point, forwardNormalised) - forwardPlane > 0f))
				{
					continue;
				}
				if (MathUtilities.WithinTolerance(Vector3.Dot(hit.normal, up), 1f))
				{
					continue;
				}
				if (hit.normal == previousNormal)
				{
					continue;
				}
				previousNormal = hit.normal;
				if (Vector3.Dot(hit.normal, m_character.ForwardDirection) < -0.0001f)
				{
					if (!nonMountableLedgeHit)
					{
						nonMountableLedgeHit = true;
						nonMountableLedge = hit;
					}
					continue;
				}
				float surfaceAngle = 180f - Vector3.Angle(hit.normal, gravityNormalised);
				if (!definition.SurfaceAngleValid(surfaceAngle))
				{
					mountableLedge = hit;
					mountableLedgeHit = true;
					return;
				}
				if (!trackManager.TryGetCharacterCollisionData(hit.collider, out CharacterCollisionData collisionData))
				{
					mountableLedge = hit;
					mountableLedgeHit = true;
					return;
				}
				if (!CharacterClimbingCollisionTracker.TerrainClimbable(trackMask, collisionData))
				{
					mountableLedge = hit;
					mountableLedgeHit = true;
					return;
				}
				if (!nonMountableLedgeHit)
				{
					nonMountableLedgeHit = true;
					nonMountableLedge = hit;
				}
			}
		}

		// 06001121 (1.10.1 RVA 0x7485E0)
		public CharacterAbility_LedgeMount()
		{
		}
	}
}
