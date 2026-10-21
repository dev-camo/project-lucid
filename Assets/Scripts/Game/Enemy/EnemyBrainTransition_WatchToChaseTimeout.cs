using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000651; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Brain/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyBrainTransition_WatchToChaseTimeout : EnemyBrainTransition
	{
		// 060021fc (1.10.1 RVA 0x575E50)
		protected EnemyBrainTransition_WatchToChaseTimeout(FiniteStateMachine fsm, FSMIdentifier transitionId, BaseJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 060021fd (1.10.1 RVA 0x575E60)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyBrainTransition_WatchToChaseTimeout(fsm, transitionId, JsonUtility.FromJson<BaseJSONCtorArgs>(jsonCtorArgs));
		}

		// 060021fe (1.10.1 RVA 0x575EF0): its only caller passes 0, and the retail body stores a constant 0 (xorps at
		// 0x57603E, with no argument set up in DoOnEnter at 0x576160). Whether the argument was ever used is undetermined.
		private void SetElapsedSeconds(IGraphUser user, float value)
		{
			user.Storage.SetValue(EnemyBrainFSMKeys.WatchElapsedTime, value);
		}

		// 060021ff (1.10.1 RVA 0x576060)
		public float GetElapsedSeconds(IGraphUser user, float adjustValue = 0f)
		{
			return user.Storage.AdjustValue(EnemyBrainFSMKeys.WatchElapsedTime, adjustValue, 0f);
		}

		// 06002200 (1.10.1 RVA 0x576160)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			SetElapsedSeconds(user, 0f);
		}

		// 06002201 (1.10.1 RVA 0x576170)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.RemoveValue<float>(EnemyBrainFSMKeys.WatchElapsedTime);
		}

		// 06002202 (1.10.1 RVA 0x5762E0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			float elapsedSeconds = GetElapsedSeconds(user, context.DeltaTime);
			return elapsedSeconds >= user.GetAs<EnemyBrain>().Enemy.Definition.Traits.WatchToChaseTimeout;
		}
	}
}
