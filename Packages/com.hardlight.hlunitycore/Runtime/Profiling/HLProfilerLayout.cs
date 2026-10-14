using System;
using System.Collections.Generic;
using System.Text;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000051; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerLayout
	{
		private readonly string m_name;
		private readonly HLProfilerLayoutDefinition.LayoutFormat m_format;
		private readonly List<HLProfilerDataReference> m_dataList;

		// 06000220
		public string Name => m_name;
		// 06000221
		public IReadOnlyList<HLProfilerDataReference> DataList => m_dataList;

		// 06000222
		public HLProfilerLayout(string name, HLProfilerLayoutDefinition.LayoutFormat format, List<HLProfilerDataReference> dataList)
		{
			m_name = name;
			m_format = format;
			m_dataList = dataList;
		}

		// 06000223
		public void Start()
		{
			for (int i = 0; i < m_dataList.Count; i++)
				m_dataList[i]?.Start();
		}

		// 06000224
		public void Stop()
		{
			for (int i = 0; i < m_dataList.Count; i++)
				m_dataList[i]?.Stop();
		}

		// 06000225
		public void AppendFormattedValueToString(StringBuilder stringBuilder, bool activeOnly = true)
		{
			switch (m_format)
			{
				case HLProfilerLayoutDefinition.LayoutFormat.SingleValue:
					AppendSingleValue(stringBuilder, activeOnly);
					break;
				case HLProfilerLayoutDefinition.LayoutFormat.RelatedValues:
					AppendRelatedValues(stringBuilder, activeOnly);
					break;
				case HLProfilerLayoutDefinition.LayoutFormat.ValueGroup:
					AppendValueGroup(stringBuilder, activeOnly);
					break;
				default:
					throw new ArgumentException("Unsupported format value.", nameof(m_format));
			}
		}

		// 06000226. Both values must be active to drop the not-active prefix.
		private void AppendRelatedValues(StringBuilder stringBuilder, bool activeOnly)
		{
			bool isActive = m_dataList[0].IsActive && m_dataList[1].IsActive;
			if (!isActive && activeOnly)
				return;
			stringBuilder.Append((isActive ? string.Empty : HLProfilerUtils.NotActivePrefix) + m_name + ": ");
			AppendValue(stringBuilder, m_dataList[0], activeOnly);
			stringBuilder.Append(" / ");
			AppendValue(stringBuilder, m_dataList[1], activeOnly);
			stringBuilder.Append("\n");
		}

		// 06000227. The not-active prefix follows activeOnly for every line, not each value's state.
		private void AppendValueGroup(StringBuilder stringBuilder, bool activeOnly)
		{
			string prefix = activeOnly ? string.Empty : HLProfilerUtils.NotActivePrefix;
			stringBuilder.AppendLine(m_name + ":");
			for (int i = 0; i < m_dataList.Count; i++)
			{
				if (activeOnly && !m_dataList[i].IsActive)
					continue;
				stringBuilder.Append("   " + prefix + m_dataList[i].DisplayName + " ");
				AppendValue(stringBuilder, m_dataList[i], activeOnly);
				stringBuilder.Append("\n");
			}
		}

		// 06000228
		private void AppendSingleValue(StringBuilder stringBuilder, bool activeOnly)
		{
			if (activeOnly && !m_dataList[0].IsActive)
				return;
			stringBuilder.Append((m_dataList[0].IsActive ? string.Empty : HLProfilerUtils.NotActivePrefix) + m_name + ": ");
			AppendValue(stringBuilder, m_dataList[0], activeOnly);
			stringBuilder.Append("\n");
		}

		// 06000229. Despite its name, a true appendHidden skips hidden values.
		private static void AppendValue(StringBuilder stringBuilder, HLProfilerDataReference data, bool appendHidden)
		{
			if (!data.IsVisible && appendHidden)
				return;
			stringBuilder.Append(data.ToFormattedString());
		}
	}
}
