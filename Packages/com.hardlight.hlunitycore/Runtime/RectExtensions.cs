using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0200007c; bodies translated from the 1.10.1 x86-64 binary.
	public static class RectExtensions
	{
		// 060002f2 (1.10.1 RVA 0x1AB3170)
		public static void Clamp(this Rect a, float minXVal, float maxXVal, float minYVal, float maxYVal)
		{
		}

		// 060002f3 (1.10.1 RVA 0x1AB3180)
		public static Vector2 TopLeft(this Rect a)
		{
			return new Vector2(a.xMin, a.yMin);
		}

		// 060002f4 (1.10.1 RVA 0x1AB3190)
		public static Vector2 TopRight(this Rect a)
		{
			return new Vector2(a.xMax, a.yMin);
		}

		// 060002f5 (1.10.1 RVA 0x1AB31A0)
		public static Vector2 BtmLeft(this Rect a)
		{
			return new Vector2(a.xMin, a.yMax);
		}

		// 060002f6 (1.10.1 RVA 0x1AB31B0)
		public static Vector2 BtmRight(this Rect a)
		{
			return new Vector2(a.xMax, a.yMax);
		}

		// 060002f7 (1.10.1 RVA 0x1AB31C0)
		public static bool Contains(this Rect a, Rect b)
		{
			return a.Contains(b.min) && a.Contains(b.max);
		}

		// 060002f8 (1.10.1 RVA 0x1AB3230)
		public static bool ContainsPartial(this Rect a, Rect b, bool allowInverse = false)
		{
			return a.Overlaps(b, allowInverse);
		}

		// 060002f9 (1.10.1 RVA 0x1AB3360)
		public static Rect Absolute(this Rect rect)
		{
			if (rect.xMin > rect.xMax)
			{
				float xMin = rect.xMin;
				rect.xMin = rect.xMax;
				rect.xMax = xMin;
			}
			if (rect.yMin > rect.yMax)
			{
				float yMin = rect.yMin;
				rect.yMin = rect.yMax;
				rect.yMax = yMin;
			}
			return rect;
		}

		// 060002fa (1.10.1 RVA 0x1AB33D0)
		public static Rect ScaleInverse(this Rect rect, float factor)
		{
			return rect.Scale(1f / factor);
		}

		// 060002fb (1.10.1 RVA 0x1AB3400)
		public static Rect Scale(this Rect rect, float factor)
		{
			Vector2 size = rect.size * factor;
			return new Rect(rect.position + (rect.size - size) * 0.5f, size);
		}

		// 060002fc (1.10.1 RVA 0x1AB3420)
		public static Rect Scale(this Rect rect, Vector2 increment)
		{
			return new Rect(rect.position - increment * 0.5f, rect.size + increment);
		}

		// 060002fd (1.10.1 RVA 0x1AB3440)
		public static Vector2 ClampToEdge(this Rect rect, Vector2 v)
		{
			return Vector2.Min(Vector2.Max(v, rect.min), rect.max);
		}

		// 060002fe (1.10.1 RVA 0x1AB3460)
		public static Vector2 ProjectToEdge(this Rect rect, Vector2 vStart, Vector2 vEnd)
		{
			vEnd = ProjectToEdge(rect.xMin, vStart, vEnd, 0);
			vEnd = ProjectToEdge(rect.xMax, vStart, vEnd, 0);
			vEnd = ProjectToEdge(rect.yMin, vStart, vEnd, 1);
			return ProjectToEdge(rect.yMax, vStart, vEnd, 1);
		}

		// 060002ff (1.10.1 RVA 0x1AB35B0)
		private static Vector2 ProjectToEdge(float edge, Vector2 vStart, Vector2 vEnd, int index)
		{
			float t = (edge - vStart[index]) / (vEnd[index] - vStart[index]);
			if (t > 0f && t < 1f)
			{
				vEnd = Vector2.Lerp(vStart, vEnd, t);
			}
			return vEnd;
		}
	}
}
