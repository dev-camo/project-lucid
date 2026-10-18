using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200062e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorDrivenLifetimeObject : TimedLifetimeObject
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		private Hazard[] m_hazards;

		[SerializeField]
		private bool m_selfInitialise;

		[SerializeField]
		[ShowIf("m_selfInitialise", (string)null)]
		private float m_lifetime;

		// 06002109 (1.10.1 RVA 0x56B280): the animator is stepped manually from
		// InternalUpdate, so its own update is switched off.
		public override void Initialise(IPlayerDamageable playerDamageable, float lifetime)
		{
			m_animator.enabled = false;
			foreach (Hazard hazard in m_hazards)
				hazard.RegisterPlayerDamageableInterface(playerDamageable);
			base.Initialise(playerDamageable, lifetime);
		}

		// 0600210a (1.10.1 RVA 0x56B300)
		public override void ResetTimedLifetimeObject()
		{
			m_animator.Rebind();
			m_animator.Update(0f);
			base.ResetTimedLifetimeObject();
		}

		// 0600210b (1.10.1 RVA 0x56B340): a self-initialised object deactivates
		// itself once its lifetime has expired.
		protected override void InternalUpdate(float deltaTime)
		{
			if (IsPaused)
				return;
			m_animator.Update(deltaTime);
			base.InternalUpdate(deltaTime);
			if (m_selfInitialise && ObjectExpired)
				gameObject.SetActive(false);
		}

		// 0600210c (1.10.1 RVA 0x56B3B0)
		protected override void OnEnable()
		{
			if (m_selfInitialise && !m_initialised)
				Initialise(null, m_lifetime);
			base.OnEnable();
		}

		// 0600210d (1.10.1 RVA 0x56B3F0)
		public AnimatorDrivenLifetimeObject()
		{
		}
	}
}
