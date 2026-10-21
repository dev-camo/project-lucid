using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000950; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class UIContainerWhatsNew : UIContainer
	{
		[SerializeField]
		private Animation m_animation;

		[SerializeField]
		private AnimationClip m_closeClip;

		[SerializeField]
		private AnimationClip m_openClip;

		[SerializeField]
		private UIModernEvent m_closeEvent;

		private App m_app;

		// 0600358b (1.10.1 RVA 0x683AF0)
		private void Start()
		{
			m_app = ProcessManager.GetSystem<App>();
			AnimateIn();
		}

		// 0600358c (1.10.1 RVA 0x683C20): the guard tests the open clip, not the close clip, as in the original.
		public void Action_Close()
		{
			if (m_animation == null || m_openClip == null)
			{
				m_app.AddUniqueUIModernEvent(m_closeEvent);
				return;
			}
			m_animation.Play(m_closeClip.name);
		}

		// 0600358d (1.10.1 RVA 0x683D00)
		public void Action_AnimationCloseFinish()
		{
			m_app.AddUniqueUIModernEvent(m_closeEvent);
		}

		// 0600358e (1.10.1 RVA 0x683B70)
		private void AnimateIn()
		{
			if (m_animation == null || m_openClip == null)
			{
				return;
			}
			m_animation.Play(m_openClip.name);
		}

		// 0600358f (1.10.1 RVA 0x683CE0)
		private void Close()
		{
			m_app.AddUniqueUIModernEvent(m_closeEvent);
		}

		// 06003590 (1.10.1 RVA 0x683D20)
		public UIContainerWhatsNew()
		{
		}
	}
}
