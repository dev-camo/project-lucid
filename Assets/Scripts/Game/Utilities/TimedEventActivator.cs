using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
	// Original Game.Runtime 020007ef and TimedEvent (020007f0); bodies translated from the
	// 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TimedEventActivator : TimeScaledComponent_SDT
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		private class TimedEvent
		{
			[Tooltip("Time until this event is invoked.")]
			[SerializeField]
			private float m_time;
			[Tooltip("The event to invoke when time has expired.")]
			[SerializeField]
			private UnityEvent m_event;

			// 06002deb (1.10.1 RVA 0x5F7490): an elapsed event consumes its own time from the
			// shared clock. Only a clock still short of the time keeps the event waiting, so a
			// NaN clock fires it.
			public bool Update(ref float time)
			{
				if (time < m_time) return false;
				time -= m_time;
				m_event.Invoke();
				return true;
			}
		}

		[Tooltip("Initial delay before starting timed events.")]
		[SerializeField]
		private float m_delay;
		[Tooltip("Determines whether to loop at the end or to finish, disabling the component.")]
		[SerializeField]
		private bool m_loop = true;
		[SerializeField]
		[Tooltip("The timed events to be triggered.")]
		private List<TimedEvent> m_timedEvents = new List<TimedEvent>();
		[SerializeField]
		private bool m_manuallyStart;
		private float m_time;
		private int m_timedEventIndex;
		private bool m_canUpdate;

		// 06002de5 (1.10.1 RVA 0x5F7310)
		protected override void OnEnable()
		{
			base.OnEnable();
			if (!m_manuallyStart)
				StartSequence();
		}

		// 06002de6 (1.10.1 RVA 0x5F7380)
		protected override void OnDisable()
		{
			base.OnDisable();
			StopSequence();
		}

		// 06002de7 (1.10.1 RVA 0x5F7350): the clock starts at minus the delay.
		public void StartSequence()
		{
			m_time = -m_delay;
			m_timedEventIndex = 0;
			m_canUpdate = true;
		}

		// 06002de8 (1.10.1 RVA 0x5F73A0)
		public void StopSequence()
		{
			m_canUpdate = false;
		}

		// 06002de9 (1.10.1 RVA 0x5F73B0): the event count is read once per update. Every event
		// whose time has elapsed fires in one update; past the last event the sequence wraps
		// when looping and otherwise disables the component.
		protected override void InternalUpdate(float deltaTime)
		{
			if (!m_canUpdate) return;
			m_time += deltaTime;
			if (m_time < 0f) return;
			int timedEventCount = m_timedEvents.Count;
			while (m_timedEvents[m_timedEventIndex].Update(ref m_time))
			{
				m_timedEventIndex++;
				if (m_timedEventIndex < timedEventCount) continue;
				if (!m_loop)
				{
					enabled = false;
					return;
				}
				m_timedEventIndex = 0;
			}
		}
	}
}
