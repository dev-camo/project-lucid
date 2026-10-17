using System;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// HLUnityCore.Runtime TypeDefIndex 9114 (0x0200016b), methods 0x060009db..0x060009e8;
	// bodies from the 1.10.1 x86-64 binary (0x1af8400..0x1af8760).
	// Every member is [Conditional("BUILD_DEVELOPMENT")], so callers lose their calls in
	// the retail player, but the bodies themselves were still compiled. In those bodies
	// any call to another [Conditional] member (HLOutput.Log/LogWarning, or one HLAssert
	// overload forwarding to another) is absent too. Only Error(bool) and True(bool) kept
	// code; every other shipped body is a bare return. The removed calls are not
	// recoverable and are not reconstructed.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class HLAssert
	{
		// 0x060009db: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Warn(bool test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009dc, 0x1af8410: a failed test reports through HLOutput.LogError, which
		// is not conditional. The body has the same shape as HLOutput.LogError(bool, ...)
		// at 0x1b02c70, so a call to that overload with !test cannot be told apart, and
		// whether context is passed is unrecoverable: LogError at 0x1af8470 never reads it.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Error(bool test, object message, UnityEngine.Object context = null)
		{
			if (!test)
			{
				HLOutput.LogError(message, context);
			}
		}

		// 0x060009dd: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void False(bool test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009de, 0x1af8540: a failed test appends the context's name when there is
		// one, logs an error, and pauses the editor during play outside batch mode.
		// Whether context is passed to LogError is unrecoverable: 0x1af8470 never reads it.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void True(bool test, object message, UnityEngine.Object context = null)
		{
			if (test)
			{
				return;
			}

			if (context != null)
			{
				message = string.Format("{0} - context: {1}", message, context.name);
			}
			HLOutput.LogError(message, context);
			if (Application.isEditor && Application.isPlaying && !Application.isBatchMode)
			{
				UnityEngine.Debug.Break();
			}
		}

		// 0x060009df: empty retail body; the delegate is never invoked.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Warn(Func<bool> test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e0: empty retail body; the delegate is never invoked.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Error(Func<bool> test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e1: empty retail body; the delegate is never invoked.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void False(Func<bool> test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e2: empty retail body; the delegate is never invoked.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void True(Func<bool> test, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e3: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void Fail(object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e4: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void NotInvalidFloat(float value, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e5: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void NotInvalid(Vector3 vector, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e6: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void NotInvalid(Quaternion quaternion, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e7: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void NotInvalid(LightweightTransform lightTransform, object message, UnityEngine.Object context = null)
		{
		}

		// 0x060009e8: empty retail body.
		[Conditional("BUILD_DEVELOPMENT")]
		public static void NotInvalid(Transform transform, object message, UnityEngine.Object context = null)
		{
		}
	}
}
