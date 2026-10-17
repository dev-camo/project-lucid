using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityUI.Runtime 0x020000c4 (TypeDefIndex 12020); x86-64 bodies.
	// Insets the content root's anchors away from the screen's unsafe edges and
	// lerps to the result. An active override takes over the orientation callback
	// and can force the full margins through Apply(true).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ContentFrameController : MonoBehaviour
	{
		private const float NotchUnits = 30f;
		private const float IPhoneXSizeUnits = 812f;
		[SerializeField]
		private RectTransform m_contentRoot;
		[SerializeField]
		private bool m_landscapeNotchDodge;
		[SerializeField]
		private bool m_portraitNotchDodge;
		[SerializeField]
		private float m_lerpTransitionTime = 0.3f;
		private Vector2 m_initialAnchorMin = Vector2.zero;
		private Vector2 m_initialAnchorMax = Vector2.zero;
		private Vector2 m_currentAnchorMin = Vector2.zero;
		private Vector2 m_currentAnchorMax = Vector2.zero;
		private bool m_shouldLerp;
		private float m_lerpProgress;
		private Vector2 m_targetAnchorMin = Vector2.zero;
		private Vector2 m_targetAnchorMax = Vector2.zero;
		private float m_portraitTopMarginRatio;
		private float m_portraitBottomMarginRatio;
		private float m_landscapeSideMarginRatio;
		private float m_landscapeBottomMarginRatio;
		private ContentFrameControllerOverride m_contentFrameControllerOverride;
		private bool m_isInitialised;
		private const float SideRatioShrink = 0.35f;
		private const float NoRatio = 0f;

		// 0x0600054e (0x1b650d0): an active override applies through its handler instead.
		private void OnEnable()
		{
			RegisterEvents();
			if (m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsOverrideActive)
			{
				return;
			}
			Apply(false);
		}

		// 0x0600054f (0x1b65840)
		private void OnDisable()
		{
			UnregisterEvents();
		}

		// 0x06000550 (0x1b65a90): only the first override is taken.
		public void SetOverride(ContentFrameControllerOverride contentFrameControllerOverride)
		{
			if (m_contentFrameControllerOverride != null)
			{
				return;
			}
			UnregisterEvents();
			m_contentFrameControllerOverride = contentFrameControllerOverride;
			RegisterEvents();
		}

		// 0x06000551 (0x1b65110): the safe-area callback is registered either way.
		private void RegisterEvents()
		{
			if (m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsOverrideActive)
			{
				m_contentFrameControllerOverride.RegisterToHandler(Apply);
			}
			else if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange += OnOrientationChangeCallback;
			}
			if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnSafeAreaChange += OnSafeAreaChangeCallback;
			}
		}

		// 0x06000552 (0x1b65850)
		private void UnregisterEvents()
		{
			if (m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsOverrideActive)
			{
				m_contentFrameControllerOverride.UnregisterToHandler(Apply);
			}
			else if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnOrientationChange -= OnOrientationChangeCallback;
			}
			if (MonoSingleton<ScreenManager>.NotNull())
			{
				MonoSingleton<ScreenManager>.Instance.OnSafeAreaChange -= OnSafeAreaChangeCallback;
			}
		}

		// 0x06000553 (0x1b65ae0): the margins are measured once, as fractions of the
		// current resolution's height. safeArea.y is measured from the bottom edge, yet
		// the original stores it as the top ratio and the gap above safeArea.yMax as the
		// bottom one.
		private void EnsureInitialisation()
		{
			if (m_isInitialised)
			{
				return;
			}
			m_isInitialised = true;

			float topMargin = Screen.safeArea.y;
			float bottomMargin = Screen.height - Screen.safeArea.yMax;
			float sideMargin = Mathf.Max(Screen.safeArea.x, Screen.width - Screen.safeArea.xMax);

			if (m_contentRoot != null)
			{
				m_initialAnchorMin = m_contentRoot.anchorMin;
				m_initialAnchorMax = m_contentRoot.anchorMax;
			}

			m_portraitTopMarginRatio = topMargin / Screen.currentResolution.height;
			m_portraitBottomMarginRatio = bottomMargin / Screen.currentResolution.height;
			m_landscapeSideMarginRatio = sideMargin * SideRatioShrink / Screen.currentResolution.height;
			m_landscapeBottomMarginRatio = bottomMargin * SideRatioShrink / Screen.currentResolution.height;
		}

		// 0x06000554 (0x1b65cd0)
		private void OnOrientationChangeCallback()
		{
			Apply(false);
		}

		// 0x06000555 (0x1b65ce0)
		private void OnSafeAreaChangeCallback(bool hasSafeArea)
		{
			Apply(false);
		}

		// 0x06000556 (0x1b65cf0): the shipped body is empty.
		public static void SetNotchEvaluator(Func<bool> notchEvaluator)
		{
		}

		// 0x06000557 (0x1b65d00)
		private bool IsSafeAreaEnabled()
		{
			return MonoSingleton<ScreenManager>.Instance.ScreenHasSafeArea();
		}

		// 0x06000558 (0x1b65350): an overridden margin collapses to NoRatio unless forced.
		// Anything that is not a dodged landscape or portrait orientation lerps back to
		// the initial anchors. The ScreenManager is read before its IsNull guard: natively
		// ScreenHasSafeArea (0x1b13110) ignores `this`, so the guard returns only once a safe
		// area is reported, while in managed C# a null Instance faults in IsSafeAreaEnabled.
		private void Apply(bool force)
		{
			EnsureInitialisation();
			if (m_contentRoot == null)
			{
				return;
			}

			if (IsSafeAreaEnabled())
			{
				if (MonoSingleton<ScreenManager>.IsNull())
				{
					return;
				}

				if (m_landscapeNotchDodge &&
					(MonoSingleton<ScreenManager>.Instance.DeviceOrientation == ScreenOrientation.LandscapeLeft ||
					 MonoSingleton<ScreenManager>.Instance.DeviceOrientation == ScreenOrientation.LandscapeRight))
				{
					float sideRatio = m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsLandscapeSideMarginOverridden && !force
						? NoRatio
						: m_landscapeSideMarginRatio;
					float bottomRatio = m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsLandscapeBottomMarginOverridden && !force
						? NoRatio
						: m_landscapeBottomMarginRatio;
					StartLerp(m_initialAnchorMin + Vector2.right * sideRatio + Vector2.up * bottomRatio,
						m_initialAnchorMax - Vector2.right * sideRatio);
					return;
				}

				if (m_portraitNotchDodge &&
					(MonoSingleton<ScreenManager>.Instance.DeviceOrientation == ScreenOrientation.Portrait ||
					 MonoSingleton<ScreenManager>.Instance.DeviceOrientation == ScreenOrientation.PortraitUpsideDown))
				{
					float topRatio = m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsPortraitTopMarginOverridden && !force
						? NoRatio
						: m_portraitTopMarginRatio;
					float bottomRatio = m_contentFrameControllerOverride != null && m_contentFrameControllerOverride.IsPortraitBottomMarginOverridden && !force
						? NoRatio
						: m_portraitBottomMarginRatio;
					StartLerp(m_initialAnchorMin + Vector2.up * bottomRatio,
						m_initialAnchorMax + Vector2.down * topRatio);
					return;
				}
			}

			StartLerp(m_initialAnchorMin, m_initialAnchorMax);
		}

		// 0x06000559 (0x1b65d70)
		private void StartLerp(Vector2 anchorMin, Vector2 anchorMax)
		{
			m_targetAnchorMin = anchorMin;
			m_targetAnchorMax = anchorMax;
			m_currentAnchorMin = m_contentRoot.anchorMin;
			m_currentAnchorMax = m_contentRoot.anchorMax;
			m_shouldLerp = true;
			m_lerpProgress = 0f;
		}

		// 0x0600055a (0x1b65db0): once the progress has reached the transition time,
		// the next Update only clears the flag.
		private void Update()
		{
			if (!m_shouldLerp)
			{
				return;
			}
			if (m_lerpProgress >= m_lerpTransitionTime)
			{
				m_shouldLerp = false;
				return;
			}
			m_lerpProgress += Time.deltaTime;
			float t = m_lerpProgress / m_lerpTransitionTime;
			m_contentRoot.anchorMin = Vector2.Lerp(m_currentAnchorMin, m_targetAnchorMin, t);
			m_contentRoot.anchorMax = Vector2.Lerp(m_currentAnchorMax, m_targetAnchorMax, t);
		}

		// 0x0600055b (0x1b65e60)
		public ContentFrameController()
		{
		}
	}
}
