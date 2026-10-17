using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9366 (0x02000267), methods 0x06000f38..0x06000f3a;
	// bodies from the 1.10.1 x86-64 binary (0x1b1c5f0, 0x1b1caa0, 0x1b1c950). Draws a
	// ray with a two-stroke arrow head at its tip.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class DebugArrow
	{
		private const float DefaultArrowHeadLength = 0.25f;

		private const float DefaultArrowHeadAngle = 35f;

		// 0x06000f38, 0x1b1c5f0: the head strokes start at position + direction. Both
		// bodies compute each stroke inline, with a 180 + angle yaw for the first and
		// 180 - angle for the second (subss at 0x1b1c7a4), scaling by the length after;
		// GetArrowHeadDirection has no call sites. The binary cannot decide whether the
		// source called the helper with -arrowHeadAngle or wrote the expressions inline;
		// the helper form used here gives the same result.
		public static void DrawGizmo(Vector3 position, Vector3 direction, Color colour, float arrowHeadLength = DefaultArrowHeadLength, float arrowHeadAngle = DefaultArrowHeadAngle)
		{
			Gizmos.color = colour;
			Gizmos.DrawRay(position, direction);
			Vector3 right = GetArrowHeadDirection(direction, arrowHeadAngle, arrowHeadLength);
			Vector3 left = GetArrowHeadDirection(direction, -arrowHeadAngle, arrowHeadLength);
			Gizmos.DrawRay(position + direction, right);
			Gizmos.DrawRay(position + direction, left);
		}

		// 0x06000f39, 0x1b1caa0: the same shape through Debug.DrawRay(start, dir, color),
		// again inline with the 180 - angle yaw for the second stroke (subss at 0x1b1cca0).
		// Being [Conditional], its call sites are absent from the retail player.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void DrawDebugRay(Vector3 position, Vector3 direction, Color colour, float arrowHeadLength = DefaultArrowHeadLength, float arrowHeadAngle = DefaultArrowHeadAngle)
		{
			UnityEngine.Debug.DrawRay(position, direction, colour);
			Vector3 right = GetArrowHeadDirection(direction, arrowHeadAngle, arrowHeadLength);
			Vector3 left = GetArrowHeadDirection(direction, -arrowHeadAngle, arrowHeadLength);
			UnityEngine.Debug.DrawRay(position + direction, right, colour);
			UnityEngine.Debug.DrawRay(position + direction, left, colour);
		}

		// 0x06000f3a, 0x1b1c950: forward turned back by 180 + angle degrees of yaw in the
		// direction's look frame, then scaled.
		private static Vector3 GetArrowHeadDirection(Vector3 direction, float arrowHeadAngle, float arrowHeadLength)
		{
			return Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 180f + arrowHeadAngle, 0f) * Vector3.forward * arrowHeadLength;
		}
	}
}
