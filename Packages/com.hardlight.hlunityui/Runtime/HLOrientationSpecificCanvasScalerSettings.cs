using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000bd (TypeDefIndex 12014), methods 0x06000534..0x06000538.
	// Applies per-orientation CanvasScaler match settings whenever the screen
	// orientation changes. No shipped asset references this script (reference
	// export GUID c4f5a1e9166bc7d98f2d9018d28ac583), so the .meta GUID is fresh.
	public class HLOrientationSpecificCanvasScalerSettings : MonoBehaviour
	{
		[Serializable]
		private class OrientationSpecificCanvasScalerScreenMatchMode
		{
			public CanvasScaler.ScreenMatchMode ScreenMatchModeToUse = CanvasScaler.ScreenMatchMode.Expand;
			[UnityEngine.Range(0f, 1f)]
			public float MatchWidthOrHeight = 1f;
			public bool FlipReferenceResolution;
		}

		[SerializeField]
		private CanvasScaler m_canvasScaler;
		[SerializeField]
		private OrientationSpecificCanvasScalerScreenMatchMode m_landscapeSettings;
		[SerializeField]
		private OrientationSpecificCanvasScalerScreenMatchMode m_portraitSettings;

		// 0x06000534 (0x1b63e20): without a live ScreenManager nothing is evaluated.
		private void OnEnable()
		{
			if (MonoSingleton<ScreenManager>.IsNull())
			{
				return;
			}
			MonoSingleton<ScreenManager>.Instance.OnOrientationChange += Evaluate;
			Evaluate();
		}

		// 0x06000535 (0x1b640c0)
		private void OnDisable()
		{
			if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange -= Evaluate;
			}
		}

		// 0x06000536 (0x1b63f90): anything other than Landscape, Uninitialised included,
		// uses the portrait settings. Flipping swaps the scaler's current reference
		// resolution, so each flipped evaluation toggles it again.
		private void Evaluate()
		{
			if (m_canvasScaler == null)
			{
				return;
			}
			OrientationSpecificCanvasScalerScreenMatchMode settings =
				MonoSingleton<ScreenManager>.Instance.Orientation == UIOrientation.Landscape ? m_landscapeSettings : m_portraitSettings;
			m_canvasScaler.screenMatchMode = settings.ScreenMatchModeToUse;
			m_canvasScaler.matchWidthOrHeight = settings.MatchWidthOrHeight;
			if (settings.FlipReferenceResolution)
			{
				Vector2 referenceResolution = m_canvasScaler.referenceResolution;
				m_canvasScaler.referenceResolution = new Vector2(referenceResolution.y, referenceResolution.x);
			}
		}
	}
}
