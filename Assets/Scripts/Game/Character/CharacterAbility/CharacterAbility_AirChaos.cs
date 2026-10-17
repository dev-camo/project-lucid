using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002b4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_AirChaos<T> : CharacterAbility_Air where T : CharacterAbilityDefinition_AirChaos
	{
		private float m_time;

		private StackableDataHandle m_timeHandle;

		private TimeSetting m_timeSetting;

		private GameObject m_debugTargetObject;

		private readonly SystemRef<TimeManager> m_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>();

		public T DefinitionAirChaos { get; private set; }

		protected bool IsActivated
		{
			get
			{
				return m_timeSetting != null;
			}
		}

		// 06001033 (1.10.1 RVA 0xE5D370)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			DefinitionAirChaos = definition as T;
		}

		// 06001034 (1.10.1 RVA 0xE5D430)
		public virtual void DoOnActivation()
		{
			m_time = 0f;
			m_timeSetting = TimeSetting.GetDefault(1f);
			m_timeHandle = m_timeManagerRef.Get().ApplyTimeSetting(m_timeSetting);
			m_character.Storage.SetValue(ActorFSMKeys.ChaosControlActive, true);
			m_character.ActivateAudioFilter(true, true);
		}

		// 06001036 (1.10.1 RVA 0xE5D5F0)
		public bool UpdateState(float deltaTime)
		{
			m_time += deltaTime;
			float elapsed = m_time - DefinitionAirChaos.m_timeScaleDelay;
			float scale = DefinitionAirChaos.m_timeScaleAnimation.Evaluate(Mathf.Max(elapsed, 0f));
			foreach (TimeCategory category in DefinitionAirChaos.m_timeCategories)
				m_timeSetting.SetCategory(category, scale);
			m_timeManagerRef.Get().UpdateTimeSetting(m_timeHandle, m_timeSetting);
			return elapsed > 0f;
		}

		// 06001037 (1.10.1 RVA 0xE5D7D0)
		public virtual void DoOnDeactivation()
		{
			m_timeManagerRef.Get().RemoveTimeSetting(m_timeHandle);
			m_time = 0f;
			m_timeHandle = null;
			m_timeSetting = null;
			m_character.Storage.RemoveValue<bool>(ActorFSMKeys.ChaosControlActive);
			m_character.ActivateAudioFilter(false, true);
		}

		// 06001038 (1.10.1 RVA 0xE5D960): tests SaveDataSettings+0x40
		// (m_airAbilityHold) only; unlike the base it ignores Definition.Input.
		protected override bool IsControlContinuous()
		{
			return !m_character.SaveDataSettings.AirAbilityHold;
		}

		// 06001039 (1.10.1 RVA 0xE5D980)
		public CharacterAbility_AirChaos()
		{
		}
	}
}
