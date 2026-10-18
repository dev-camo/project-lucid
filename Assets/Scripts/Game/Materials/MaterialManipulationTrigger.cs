using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020006c7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class MaterialManipulationTrigger : MonoBehaviour
	{
		[SerializeField]
		private SceneMaterialManipulator m_manipulator;
		[SerializeField]
		private MaterialManipulationTriggerType[] m_manipulationTriggers;

		// 060024c5 (1.10.1 RVA 0x59A2D0): each trigger type is sent in array order.
		public void Action_Trigger()
		{
			foreach (MaterialManipulationTriggerType triggerType in m_manipulationTriggers)
				m_manipulator.Trigger(triggerType);
		}
	}
}
