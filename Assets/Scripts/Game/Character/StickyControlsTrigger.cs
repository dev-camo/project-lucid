using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0200073f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class StickyControlsTrigger : CharacterTrigger
	{
		[SerializeField]
		[Tooltip("Sticky control parameters.")]
		private CharacterStickyControls m_stickyControls = new CharacterStickyControls();
		[SerializeField]
		[Tooltip("Events to fire when sticky controls become active.")]
		private UnityEvent m_onActivated;
		[SerializeField]
		[Tooltip("Events to fire when sticky controls are deactivated.")]
		private UnityEvent m_onDeactivated;

		// 060028ce (1.10.1 RVA 0x5CB390)
		public void SetTransformOverride(Transform transformOverride)
		{
			m_stickyControls.SetTransformOverride(transformOverride);
		}

		// 060028cf (1.10.1 RVA 0x5CB3B0): each hook runs the base hook first.
		protected override void DoTriggerEnter(Character character)
		{
			base.DoTriggerEnter(character);
			m_stickyControls.DoTriggerActivate(character, m_onActivated, m_onDeactivated);
		}

		// 060028d0 (1.10.1 RVA 0x5CB3E0)
		protected override void DoTriggerStay(Character character)
		{
			base.DoTriggerStay(character);
			m_stickyControls.DoTriggerStay(character);
		}

		// 060028d1 (1.10.1 RVA 0x5CB410)
		protected override void DoTriggerExit(Character character)
		{
			base.DoTriggerExit(character);
			m_stickyControls.DoTriggerDeactivate(character);
		}
	}
}
