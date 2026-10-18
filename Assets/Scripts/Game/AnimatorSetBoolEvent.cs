using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0200009e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class AnimatorSetBoolEvent : MonoBehaviour
	{
		// Original Game.Runtime 0200009f.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		public class AnimationBoolEvent : UnityEvent<string, bool>
		{
			// 06000533 (1.10.1 RVA 0x52F0D0)
			public AnimationBoolEvent()
			{
			}
		}

		[SerializeField]
		private AnimationBoolEvent m_boolEvent;

		[SerializeField]
		private string m_settingString;

		[SerializeField]
		private bool m_settingBool;

		// 06000531 (1.10.1 RVA 0x52F070)
		public void InvokeEvent()
		{
			m_boolEvent.Invoke(m_settingString, m_settingBool);
		}

		// 06000532 (1.10.1 RVA 0x52F0C0)
		public AnimatorSetBoolEvent()
		{
		}
	}
}
