using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000118; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStateRemoveProgressionUnlock : FSMState
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs
		{
			public int MaxDisplayedUnlocks;

			[GraphEnumPopup(typeof(PlayerProgressionTypes))]
			public List<string> ClearedProgressionTypes;

			// 06000742 (1.10.1 RVA 0x6534C0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly int m_maxDisplayedUnlocks;

		private readonly List<PlayerProgressionTypes> m_clearedProgressionTypes = new List<PlayerProgressionTypes>();

		// 0600073f (1.10.1 RVA 0x652F30)
		private ApplicationStateRemoveProgressionUnlock(FiniteStateMachine fsm, FSMIdentifier stateId, int maxDisplayedUnlocks, List<PlayerProgressionTypes> clearedProgressionTypes) : base(fsm, stateId)
		{
			m_maxDisplayedUnlocks = maxDisplayedUnlocks;
			m_clearedProgressionTypes = clearedProgressionTypes;
		}

		// 06000740 (1.10.1 RVA 0x652FF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			List<PlayerProgressionTypes> clearedTypes = new List<PlayerProgressionTypes>();
			foreach (string typeName in args.ClearedProgressionTypes)
			{
				clearedTypes.Add(EnumUtilities.Parse<PlayerProgressionTypes>(typeName));
			}
			return new ApplicationStateRemoveProgressionUnlock(fsm, stateId, args.MaxDisplayedUnlocks, clearedTypes);
		}

		// 06000741 (1.10.1 RVA 0x653240): the original does not chain to FSMState.DoOnEnter. Each entry
		// consumes the current unlock; once the shown count reaches the limit, every unlock of the
		// configured types is cleared.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			if (UIProgressionUtilities.TryGetCurrentUnlock(out IMetaGameUnlock currentUnlock))
			{
				App app = user.GetAs<App>();
				List<IMetaGameUnlock> unlocks = app.Storage.GetValue<List<IMetaGameUnlock>>(AppFSMKeys.MetaGameUnlocks, null, true);
				unlocks.Remove(currentUnlock);
				app.Storage.SetValue(AppFSMKeys.MetaGameUnlocks, unlocks);
				if (app.Storage.AdjustValue(AppFSMKeys.MetaGameUnlocksShown, 1, 0) >= m_maxDisplayedUnlocks)
				{
					UIProgressionUtilities.RemoveUnlocksOfType(m_clearedProgressionTypes);
				}
			}
		}
	}
}
