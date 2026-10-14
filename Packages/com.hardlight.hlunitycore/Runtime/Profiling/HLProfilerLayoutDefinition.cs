using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000052; bodies translated from the 1.10.1 x86-64 binary.
	[Serializable]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerLayoutDefinition
	{
		// Original 02000053.
		public enum LayoutFormat
		{
			SingleValue,
			RelatedValues,
			ValueGroup
		}

		[SerializeField]
		private string m_name;
		[SerializeField]
		private LayoutFormat m_format;
		[SerializeField]
		private HLProfilerDataDefinition[] m_dataDefinitionList;

		// 0600022a
		public string Name => m_name;
		// 0600022b
		public LayoutFormat Format => m_format;
		// 0600022c
		public IReadOnlyCollection<HLProfilerDataDefinition> DataDefinitionList => Array.AsReadOnly(m_dataDefinitionList);

		// 0600022d
		public HLProfilerLayoutDefinition(string name, LayoutFormat format, HLProfilerDataDefinition[] dataDefinitionList)
		{
			m_name = name;
			m_format = format;
			m_dataDefinitionList = dataDefinitionList ?? new HLProfilerDataDefinition[0];
		}

		// 0600022e. A single-value layout named after its marker.
		public HLProfilerLayoutDefinition(HLProfilerMarker marker, uint sampleBufferSize = 0, bool isShownByDefault = true)
			: this(HLProfilerMarkerConverter.GetName(marker), LayoutFormat.SingleValue,
				new[] { new HLProfilerDataDefinition(marker, null, sampleBufferSize, isShownByDefault) })
		{
		}
	}
}
