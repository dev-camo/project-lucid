using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000746; bodies translated from the 1.10.1 x86-64 binary.
	// Evaluate xors the answer with m_invert; the character overloads feed DoEvaluate(Character),
	// and the parameterless one resolves the current character first.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class TriggerCondition : MonoBehaviour
	{
		[SerializeField]
		private bool m_invert;

		private static SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);

		// 060028ee
		protected abstract bool DoEvaluate(Character character);

		// 060028ef (1.10.1 RVA 0x5CC780)
		public bool Evaluate()
		{
			return DoEvaluate() != m_invert;
		}

		// 060028f0 (1.10.1 RVA 0x5CC7B0): false while no character is current.
		protected virtual bool DoEvaluate()
		{
			return TryGetCharacter(out Character character) && DoEvaluate(character);
		}

		// 060028f1 (1.10.1 RVA 0x5CC930)
		public bool Evaluate(Character character)
		{
			return DoEvaluate(character) != m_invert;
		}

		// 060028f2 (1.10.1 RVA 0x5CC8B0)
		private static bool TryGetCharacter(out Character character)
		{
			return m_characterManagerRef.Get().TryGetCurrentCharacter(out character);
		}
	}
}
