using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200018b; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationTransitionMetaGameIsSameLevel : FSMTransition
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			public bool TrueWhenSameLevel;

			// 0600088b (1.10.1 RVA 0x665F40)
			public JSONCtorArgs()
			{;}
		}

		private readonly bool m_trueWhenSameLevel;

		// 06000888 (1.10.1 RVA 0x665A20)
		private ApplicationTransitionMetaGameIsSameLevel(FiniteStateMachine fsm, FSMIdentifier transitionId, bool trueWhenSameLevel) : base(fsm, transitionId)
		{
			m_trueWhenSameLevel = trueWhenSameLevel;
		}

		// 06000889 (1.10.1 RVA 0x665A40)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationTransitionMetaGameIsSameLevel(fsm, transitionId, args.TrueWhenSameLevel);
		}

		// 0600088a (1.10.1 RVA 0x665AE0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			App app = user.GetAs<App>();
			app.Storage.HasValue(AppFSMKeys.MetaGameState);
			MetaGameState metaGameState = app.Storage.GetValue<MetaGameState>(AppFSMKeys.MetaGameState, null, true);
			LevelManager levelManager = ProcessManager.GetSystem<LevelManager>();
			IMissionContext missionContext = ProcessManager.GetSystem<MissionManager>().ActiveMissionContext;
			GameplayLevelDefinition levelDefinition = missionContext?.MissionDefinition.SceneOverride != null ? missionContext.MissionDefinition.SceneOverride : metaGameState.LevelDefinition;

			if (levelManager.ShouldUnloadBeforeReload(levelDefinition))
			{
				app.Storage.SetValue(AppFSMKeys.LevelLoadParameters, new LevelManagerLoadLevelParameters(levelDefinition));
				return !m_trueWhenSameLevel;
			}

			if (!levelManager.TryGetCurrentLevel(out LevelManagerLevel currentLevel))
			{
				return !m_trueWhenSameLevel;
			}
			return m_trueWhenSameLevel ? currentLevel.SceneDefinition == levelDefinition : currentLevel.SceneDefinition != levelDefinition;
		}
	}
}
