using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000657; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Enemy/Movement/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("TriggerGravityForce")]
	public class EnemyState_TriggerGravityForce : FSMState_Trigger
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			public bool GravityForceEnabled = true;

			// 06002226 (1.10.1 RVA 0x577EB0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_gravityForceEnabled;

		// 06002223 (1.10.1 RVA 0x577D20)
		protected EnemyState_TriggerGravityForce(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_gravityForceEnabled = ctorArgs.GravityForceEnabled;
		}

		// 06002224 (1.10.1 RVA 0x577D80)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new EnemyState_TriggerGravityForce(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06002225 (1.10.1 RVA 0x577E20)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			Enemy enemy = user.GetAs<Enemy>();
			if (enemy == null)
			{
				return;
			}
			enemy.SetGravityForceEnabled(m_gravityForceEnabled);
		}
	}
}
