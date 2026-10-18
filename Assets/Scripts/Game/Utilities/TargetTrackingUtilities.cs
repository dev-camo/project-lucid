using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000a33 and its delegate TargetValidDelegate (02000a34); bodies
	// translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public static class TargetTrackingUtilities
	{
		public delegate bool TargetValidDelegate(HomingTarget target);

		// 06003a6c (1.10.1 RVA 0x6B7B10): the cone is grown from the origin in Step increments,
		// capped at its Distance, until a step finds a target. Every step adds to the one valid
		// list, which is cleared only here.
		public static bool ProjectToTargetedObject(ConeVolume.ConeDescription cone, List<HomingTarget> targets, Vector3 forward, Quaternion worldToLocal, ref List<HomingTarget> validTargets, out HomingTarget bestTarget, TargetValidDelegate targetValidDelegate = null)
		{
			validTargets.Clear();
			float maxDistance = cone.Distance;
			float distance = 0f;
			while (distance < maxDistance)
			{
				distance = Mathf.Min(distance + cone.Step, maxDistance);
				cone.Distance = distance;
				if (ProjectStepToTargetedObject(cone, targets, forward, worldToLocal, ref validTargets, out bestTarget, targetValidDelegate))
					return true;
			}
			bestTarget = null;
			return false;
		}

		// 06003a6d (1.10.1 RVA 0x6B7C40): a target is projected onto the forward line through
		// the cone origin. Its depth along forward must lie in [0, Distance], and the cone
		// test is centred on the projected point with that depth as its length. The best
		// target has the smallest squared offset from the line plus squared depth; a tie
		// goes to the later target.
		private static bool ProjectStepToTargetedObject(ConeVolume.ConeDescription cone, List<HomingTarget> targets, Vector3 forward, Quaternion worldToLocal, ref List<HomingTarget> validTargets, out HomingTarget bestTarget, TargetValidDelegate targetValidDelegate = null)
		{
			bestTarget = null;
			float bestDistanceSquared = float.MaxValue;
			foreach (HomingTarget target in targets)
			{
				if (target == null) continue;
				Vector3 targetPosition = target.WorldPosition;
				Vector3 closestPoint = MathUtilities.GetClosestPointOnLine(cone.Origin, forward, targetPosition);
				Vector3 alongForward = closestPoint - cone.Origin;
				float depth = Vector3.Dot(alongForward, forward);
				if (depth < 0f || depth > cone.Distance) continue;
				if (!ConeVolume.IsPositionInsideCone(targetPosition, closestPoint, cone.Tangents, depth, worldToLocal)) continue;
				if (targetValidDelegate != null && !targetValidDelegate(target)) continue;
				validTargets.AddUnique(target);
				float distanceSquared = (targetPosition - closestPoint).sqrMagnitude + alongForward.sqrMagnitude;
				if (distanceSquared > bestDistanceSquared) continue;
				bestDistanceSquared = distanceSquared;
				bestTarget = target;
			}
			return bestTarget != null;
		}
	}
}
