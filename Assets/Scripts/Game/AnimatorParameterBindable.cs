using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020009e3; bodies translated from the 1.10.1 x86-64
	// AnimatorParameterBindable<float> instantiations.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class AnimatorParameterBindable<T> : MonoBehaviour
	{
		[SerializeField]
		private Animator m_animator;

		[SerializeField]
		protected string m_propertyName;

		public readonly Bindable<T> Bindable = new Bindable<T>();

		private int m_propertyHash;

		// 060038df (1.10.1 RVA 0x1057750): an assigned animator is kept.
		private void Reset()
		{
			if (m_animator != null)
				return;
			m_animator = GetComponent<Animator>();
		}

		// 060038e0 (1.10.1 RVA 0x10577E0): without a name or an animator the hash
		// stays unset, but Update still reads through it.
		private void Awake()
		{
			if (string.IsNullOrEmpty(m_propertyName) || m_animator == null)
				return;
			m_propertyHash = Animator.StringToHash(m_propertyName);
		}

		// 060038e1 (1.10.1 RVA 0x1057860)
		private void Update()
		{
			Bindable.Value = GetAnimatorValue(m_animator, m_propertyHash);
		}

		// 060038e2
		protected abstract T GetAnimatorValue(Animator animator, int propertyHash);

		// 060038e3 (1.10.1 RVA 0x10578A0)
		protected AnimatorParameterBindable()
		{
		}
	}
}
