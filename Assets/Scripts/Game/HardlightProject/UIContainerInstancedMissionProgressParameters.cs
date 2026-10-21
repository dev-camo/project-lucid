using System.Collections.Generic;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 020008dd; bodies translated from the 1.10.1 x86-64 binary.
	public struct UIContainerInstancedMissionProgressParameters : IUIContainerParameters
	{
		public MissionDefinition MissionDefinition { get; }

		public int Progress { get; }

		public int PreviousProgress { get; }

		public bool AnimateProgressDelta { get; }

		public IReadOnlyList<int> PreviouslyCompletedObjectiveIndices { get; }

		public IReadOnlyList<int> CompletedObjectiveIndices { get; }

		// 06003320 (1.10.1 RVA 0x63D620)
		public UIContainerInstancedMissionProgressParameters(MissionDefinition missionDefinition, int progress, int previousProgress,
			bool animateProgressDelta, IReadOnlyList<int> previouslyCompletedObjectiveIndices, IReadOnlyList<int> completedObjectiveIndices)
		{
			MissionDefinition = missionDefinition;
			Progress = progress;
			PreviousProgress = previousProgress;
			AnimateProgressDelta = animateProgressDelta;
			PreviouslyCompletedObjectiveIndices = previouslyCompletedObjectiveIndices;
			CompletedObjectiveIndices = completedObjectiveIndices;
		}
	}
}
