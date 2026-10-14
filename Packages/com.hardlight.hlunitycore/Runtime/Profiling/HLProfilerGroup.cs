using System;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 0200004f; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerGroup
	{
		// 0600020a / 0600020b
		public string Name { get; private set; }
		// 0600020c / 0600020d
		public bool IsVisible { get; private set; }
		// 0600020e / 0600020f
		public HLProfilerCategory Category { get; private set; }
		// 06000210
		public IReadOnlyList<HLProfilerLayout> LayoutList => m_layoutList;
		// 06000211 / 06000212
		private bool IsActive { get; set; }

		private readonly List<HLProfilerLayout> m_layoutList = new List<HLProfilerLayout>();

		// 06000213
		public HLProfilerGroup(string name, HLProfilerCategory category, bool visibleByDefault)
		{
			Name = name;
			Category = category;
			IsVisible = visibleByDefault;
		}

		// 06000214
		public void AddData(HLProfilerLayoutDefinition layoutDefinition, List<HLProfilerDataReference> dataList)
		{
			m_layoutList.Add(new HLProfilerLayout(layoutDefinition.Name, layoutDefinition.Format, dataList));
		}

		// 06000215
		public void AddData(HLProfilerDataReference data, string markerDisplayName)
		{
			var dataList = new List<HLProfilerDataReference> { data };
			m_layoutList.Add(new HLProfilerLayout(markerDisplayName, HLProfilerLayoutDefinition.LayoutFormat.SingleValue, dataList));
			if (IsActive)
				data.Start();
		}

		// 06000216. Only the last layout with a matching name is stopped and removed.
		public void RemoveData(string statsName)
		{
			for (int i = m_layoutList.Count - 1; i >= 0; i--)
			{
				if (m_layoutList[i].Name != statsName)
					continue;
				m_layoutList[i].Stop();
				m_layoutList.RemoveAt(i);
				return;
			}
		}

		// 06000217
		public void ToggleVisible(bool visible)
		{
			if (IsVisible == visible)
				return;
			IsVisible = visible;
			if (visible)
				StartGroupProfiling();
			else
				StopGroupProfiling();
		}

		// 06000218
		public void StartGroupProfiling()
		{
			if (!IsVisible)
				return;
			IsActive = true;
			for (int i = 0; i < m_layoutList.Count; i++)
				m_layoutList[i].Start();
		}

		// 06000219. Unlike starting, stopping skips null layouts.
		public void StopGroupProfiling()
		{
			IsActive = false;
			for (int i = 0; i < m_layoutList.Count; i++)
				m_layoutList[i]?.Stop();
		}

		// 0600021a
		public void AppendToString(StringBuilder stringBuilder, bool appendOnlyActiveData)
		{
			if (!IsVisible && appendOnlyActiveData)
				return;
			for (int i = 0; i < m_layoutList.Count; i++)
				m_layoutList[i].AppendFormattedValueToString(stringBuilder, appendOnlyActiveData);
		}
	}
}
