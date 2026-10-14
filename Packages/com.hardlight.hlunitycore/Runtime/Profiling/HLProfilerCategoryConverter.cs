using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using Unity.Profiling;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000041; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public static class HLProfilerCategoryConverter
	{
		// 040000b5; initialised by the original static constructor 06000195 (1.10.1 RVA 0x1AA27E0),
		// which uses the shared category comparer and adds each category in enum order.
		private static readonly Dictionary<HLProfilerCategory, ProfilerCategory> LookupTable =
			new Dictionary<HLProfilerCategory, ProfilerCategory>(HLProfilerEnumComparers.CategoryComparer)
		{
			{ HLProfilerCategory.Memory, ProfilerCategory.Memory },
			{ HLProfilerCategory.Input, ProfilerCategory.Input },
			{ HLProfilerCategory.Vr, ProfilerCategory.Vr },
			{ HLProfilerCategory.Loading, ProfilerCategory.Loading },
			{ HLProfilerCategory.Network, ProfilerCategory.Network },
			{ HLProfilerCategory.Lighting, ProfilerCategory.Lighting },
			{ HLProfilerCategory.Particles, ProfilerCategory.Particles },
			{ HLProfilerCategory.Video, ProfilerCategory.Video },
			{ HLProfilerCategory.Audio, ProfilerCategory.Audio },
			{ HLProfilerCategory.Ai, ProfilerCategory.Ai },
			{ HLProfilerCategory.Animation, ProfilerCategory.Animation },
			{ HLProfilerCategory.Physics, ProfilerCategory.Physics },
			{ HLProfilerCategory.Gui, ProfilerCategory.Gui },
			{ HLProfilerCategory.Scripts, ProfilerCategory.Scripts },
			{ HLProfilerCategory.Render, ProfilerCategory.Render },
			{ HLProfilerCategory.VirtualTexturing, ProfilerCategory.VirtualTexturing },
			{ HLProfilerCategory.Internal, ProfilerCategory.Internal },
		};

		// 06000194 (1.10.1 RVA 0x1AA2770). A missing category throws, as the original indexer does.
		public static ProfilerCategory ToProfilerCategory(HLProfilerCategory hlProfilerCategory)
		{
			return LookupTable[hlProfilerCategory];
		}
	}
}
