using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x020009f5; bodies translated from the 1.10.1 x86-64 binary.
	// Re-evaluates when a persistent object changes or a level activates instead of on a time update.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ConditionalTriggerEventPersistentObjectBooleans : ConditionalTriggerEvent
	{
		private readonly List<TriggerCondition_PersistentLevelObjectBoolean> m_persistentConditions = new List<TriggerCondition_PersistentLevelObjectBoolean>();
		private LevelManager m_levelManager;

		// 0600393d (1.10.1 RVA 0x6AECB0)
		protected override void OnValidate()
		{
			base.OnValidate();
			foreach (TriggerCondition condition in m_conditions)
			{
				if (!(condition is TriggerCondition_PersistentLevelObjectBoolean))
					HLOutput.LogError("ConditionalTriggerEventPersistentObjectBooleans expects all conditions to be of the type TriggerCondition_PersistentLevelObjectBoolean");
			}
		}

		// 0600393e (1.10.1 RVA 0x6AEDB0): the update subscription is switched off before the
		// base subscribes, and every condition must really be a persistent boolean.
		protected override void OnEnable()
		{
			m_updateOn = UpdateOn.None;
			base.OnEnable();
			foreach (TriggerCondition condition in m_conditions)
			{
				var persistentCondition = (TriggerCondition_PersistentLevelObjectBoolean)condition;
				persistentCondition.PersistentLevelObject.OnStateChangedEvent += Evaluate;
				m_persistentConditions.Add(persistentCondition);
			}
			Evaluate();
			ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(LevelManagerValid);
		}

		// 0600393f (1.10.1 RVA 0x6AF050)
		private void LevelManagerValid(LevelManager manager)
		{
			m_levelManager = manager;
			m_levelManager.InvokeOnLevelActivated(Evaluate, true);
		}

		// 06003940 (1.10.1 RVA 0x6AF0F0)
		private void Evaluate(LevelManagerLevel _)
		{
			Evaluate();
		}

		// 06003941 (1.10.1 RVA 0x6AF100)
		protected override void OnDisable()
		{
			m_levelManager.RemoveLevelActivatedAction(Evaluate);
			foreach (TriggerCondition_PersistentLevelObjectBoolean persistentCondition in m_persistentConditions)
				persistentCondition.PersistentLevelObject.OnStateChangedEvent -= Evaluate;
			m_persistentConditions.Clear();
			base.OnDisable();
		}
	}
}
