using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000050; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "ProfilerGroup_", menuName = "Hardlight/HLUnityCore/Profiler/Group Definition")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerGroupDefinition : ScriptableObject
	{
		[SerializeField]
		private string m_groupName;
		[SerializeField]
		[TextArea]
		private string m_description;
		[SerializeField]
		private HLProfilerCategory m_category;
		[SerializeField]
		[Tooltip("Should this group be displayed in the profiler console by default.")]
		private bool m_shownByDefault = true;
		[SerializeField]
		private HLProfilerLayoutDefinition[] m_dataDefinition;

		// 0600021b
		public string GroupName => m_groupName;
		// 0600021c
		public HLProfilerCategory Category => m_category;
		// 0600021d
		public bool ShownByDefault => m_shownByDefault;
		// 0600021e
		public HLProfilerLayoutDefinition[] DataLayout => m_dataDefinition;
	}
}
