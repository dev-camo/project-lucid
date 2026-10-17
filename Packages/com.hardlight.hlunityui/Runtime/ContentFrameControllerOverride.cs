using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

// Original HLUnityUI.Runtime 0x02000009 (TypeDefIndex 11833); x86-64 bodies.
// Each margin is overridden only while its flag is set and the handler reports
// the override as active; the flag is read before the handler is queried.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Serializable]
public sealed class ContentFrameControllerOverride
{
	[SerializeField]
	private bool m_overrideLandscapeSideMargin;
	[SerializeField]
	private bool m_overrideLandscapeBottomMargin;
	[SerializeField]
	private bool m_overridePortraitTopMargin;
	[SerializeField]
	private bool m_overridePortraitBottomMargin;
	private IContentFrameControllerOverrideHandler m_handler;

	// 0x06000022 (0x1b39f90)
	public bool IsOverrideActive => m_handler != null && m_handler.IsOverrideActive();

	// 0x06000023 (0x1b3a040)
	public bool IsLandscapeSideMarginOverridden => m_overrideLandscapeSideMargin && IsOverrideActive;

	// 0x06000024 (0x1b3a0f0)
	public bool IsLandscapeBottomMarginOverridden => m_overrideLandscapeBottomMargin && IsOverrideActive;

	// 0x06000025 (0x1b3a1a0)
	public bool IsPortraitTopMarginOverridden => m_overridePortraitTopMargin && IsOverrideActive;

	// 0x06000026 (0x1b3a250)
	public bool IsPortraitBottomMarginOverridden => m_overridePortraitBottomMargin && IsOverrideActive;

	// 0x06000027 (0x1b3a300): the first handler is kept.
	public void SetContentFrameControllerOverrideHandler(IContentFrameControllerOverrideHandler handler)
	{
		if (m_handler == null)
		{
			m_handler = handler;
		}
	}

	// 0x06000028 (0x1b3a320)
	public void RegisterToHandler(Action<bool> onApply)
	{
		if (m_handler != null)
		{
			m_handler.OnApply += onApply;
		}
	}

	// 0x06000029 (0x1b3a3d0)
	public void UnregisterToHandler(Action<bool> onApply)
	{
		if (m_handler != null)
		{
			m_handler.OnApply -= onApply;
		}
	}

	// 0x0600002a (0x1b3a480)
	public ContentFrameControllerOverride()
	{
	}
}
