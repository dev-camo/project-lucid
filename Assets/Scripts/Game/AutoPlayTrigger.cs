using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.AutoPlay
{
	// Original Game.Runtime 0x02000019; bodies translated from the 1.10.1 x86-64 binary.
	[RequireComponent(typeof(BoxCollider))]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AutoPlayTrigger : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("Will only trigger if entering collider has this tag. Set to empty to ignore tags.")]
		private string m_triggerTag = "Player";

		// 0x06000047/0x06000048
		public bool IsTriggered { get; protected set; }

		// 0x06000049 (1.10.1 RVA 0x508540): an empty tag accepts any collider; nothing here clears the flag.
		protected virtual void OnTriggerEnter(Collider other)
		{
			if (string.IsNullOrEmpty(m_triggerTag) || other.CompareTag(m_triggerTag))
				IsTriggered = true;
		}
	}
}
