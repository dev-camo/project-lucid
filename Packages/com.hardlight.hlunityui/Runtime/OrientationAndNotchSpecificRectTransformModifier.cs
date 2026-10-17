using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000bf (TypeDefIndex 12015), methods 0x06000539..0x0600053e.
	// Adds notch-specific settings on top of the orientation settings when the
	// injected evaluator reports a notch. No shipped asset references this script
	// (reference export GUID 9accf7a67fdbebc08efd217e5027f0c7), so the .meta GUID is fresh.
	// Attribute order as in dump.cs TypeDefIndex 12015.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[RequireComponent(typeof(RectTransformModifier))]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class OrientationAndNotchSpecificRectTransformModifier : OrientationSpecificRectTransformModifier
	{
		private static Func<bool> IsNotchEnabled;

		[SerializeField]
		private OrientationSpecificRectSettings[] m_portraitNotchSettings;
		[SerializeField]
		private OrientationSpecificRectSettings[] m_landscapeLeftNotchSettings;
		[SerializeField]
		private OrientationSpecificRectSettings[] m_landscapeRightNotchSettings;

		// 0x06000539 (0x1b64260): hides the base OnEnable, so no WaitForRequiredSystems
		// coroutine runs; the refresh is immediate and happens even without a live
		// ScreenManager, where the base InitialiseValues then dereferences a null Instance.
		private void OnEnable()
		{
			if (!MonoSingleton<ScreenManager>.IsNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange += InitialiseAndSetValues;
			}
			InitialiseAndSetValues();
		}

		// 0x0600053a (0x1b64390)
		private void OnDisable()
		{
			if (!MonoSingleton<ScreenManager>.IsNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange -= InitialiseAndSetValues;
			}
		}

		// 0x0600053b
		public static void SetNotchEvaluator(Func<bool> notchEvaluator)
		{
			IsNotchEnabled = notchEvaluator;
		}

		// 0x0600053c (0x1b64510): the notch array is appended after the base orientation
		// array. Only a Landscape UI on a LandscapeLeft or LandscapeRight device picks a
		// landscape notch array; every other combination uses the portrait one.
		protected override void InitialiseValues()
		{
			base.InitialiseValues();
			if (IsNotchEnabled != null && IsNotchEnabled())
			{
				UIOrientation orientation = MonoSingleton<ScreenManager>.Instance.Orientation;
				ScreenOrientation deviceOrientation = MonoSingleton<ScreenManager>.Instance.DeviceOrientation;
				if (orientation == UIOrientation.Landscape && deviceOrientation == ScreenOrientation.LandscapeLeft)
				{
					m_desiredSetting.AddRange(m_landscapeLeftNotchSettings);
				}
				else if (orientation == UIOrientation.Landscape && deviceOrientation == ScreenOrientation.LandscapeRight)
				{
					m_desiredSetting.AddRange(m_landscapeRightNotchSettings);
				}
				else
				{
					m_desiredSetting.AddRange(m_portraitNotchSettings);
				}
			}
		}

		// 0x0600053d
		protected override void InitialiseAndSetValues()
		{
			InitialiseValues();
			SetAllValues();
		}
	}
}
