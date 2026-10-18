using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 0x020009f3; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ConditionalTriggerEvent : TimeScaledComponent_SDT
	{
		[SerializeField]
		protected UnityEvent m_whileConditionsNotMet;
		[SerializeField]
		protected UnityEvent m_onConditionsMet;
		[SerializeField]
		protected UnityEvent m_whileConditionsRemainMet;
		[SerializeField]
		protected UnityEvent m_onConditionsNoLongerMet;
		[SerializeField]
		private bool m_triggerConditionsNoLongerMetOnDestroy;
		[SerializeField]
		private CheckType m_checkType;
		[SerializeField]
		protected TriggerCondition[] m_conditions;
		private bool m_conditionsMet;

		// 06003933 (1.10.1 RVA 0x6AE9B0)
		protected override void Awake()
		{
			base.Awake();
			if (!CheckIsValid())
				enabled = false;
		}

		// 06003934 (1.10.1 RVA 0x6AE9F0)
		protected override void InternalUpdate(float deltaTime)
		{
			Evaluate();
		}

		// 06003935 (1.10.1 RVA 0x6AEA00): the met event runs before the flag is set, and the
		// remain-met event also runs on the update that first meets the conditions.
		protected void Evaluate()
		{
			if (EvaluateConditions())
			{
				if (!m_conditionsMet)
				{
					m_onConditionsMet?.Invoke();
					m_conditionsMet = true;
				}
				m_whileConditionsRemainMet?.Invoke();
			}
			else if (m_conditionsMet)
			{
				m_conditionsMet = false;
				m_onConditionsNoLongerMet?.Invoke();
			}
			else
			{
				m_whileConditionsNotMet?.Invoke();
			}
		}

		// 06003936 (1.10.1 RVA 0x6AEAD0): an empty list is met for either check type, and a
		// check type outside All and Any is met on the first condition.
		private bool EvaluateConditions()
		{
			bool allMet = true;
			foreach (TriggerCondition condition in m_conditions)
			{
				bool met = condition.Evaluate();
				switch (m_checkType)
				{
					case CheckType.All:
						if (!met)
							return false;
						break;
					case CheckType.Any:
						if (met)
							return true;
						break;
					default:
						return true;
				}
				allMet &= met;
			}
			return allMet;
		}

		// 06003937 (1.10.1 RVA 0x6AEB40)
		public int GetConditionsCount()
		{
			return m_conditions.Length;
		}

		// 06003938 (1.10.1 RVA 0x6AEB50): every condition is evaluated.
		public int GetConditionsPassedCount()
		{
			int passed = 0;
			foreach (TriggerCondition condition in m_conditions)
			{
				if (condition.Evaluate())
					passed++;
			}
			return passed;
		}

		// 06003939 (1.10.1 RVA 0x6AEBA0)
		protected override void OnEnable()
		{
			base.OnEnable();
			if (!CheckIsValid())
				enabled = false;
		}

		// 0600393a (1.10.1 RVA 0x6AEBE0)
		public override void OnDestroy()
		{
			base.OnDestroy();
			if (m_triggerConditionsNoLongerMetOnDestroy && m_conditionsMet)
			{
				m_conditionsMet = false;
				m_onConditionsNoLongerMet?.Invoke();
			}
		}

		// 0600393b (1.10.1 RVA 0x6AEC30): valid only while an event has a persistent listener.
		public override bool CheckIsValid()
		{
			return base.CheckIsValid()
				&& (m_whileConditionsNotMet.GetPersistentEventCount() > 0
					|| m_onConditionsMet.GetPersistentEventCount() > 0
					|| m_whileConditionsRemainMet.GetPersistentEventCount() > 0
					|| m_onConditionsNoLongerMet.GetPersistentEventCount() > 0);
		}

		private enum CheckType
		{
			All = 0,
			Any = 1
		}
	}
}
