using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200075c; bodies translated from the 1.10.1 x86-64 binary.
	// Accumulates the fixed delta time of timeCategory into the float value from OnEnable until the
	// TimeManager row is gone; OnDisable does not stop the running loop.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_ValueTime : TriggerCondition_ValueFloat
	{
		[SerializeField]
		private TimeCategory timeCategory = TimeCategory.PlayerPhysics;

		private static readonly SystemRef<TimeManager> s_timeManagerRef = ProcessManager.GetSystemRef<TimeManager>(null, true);

		private readonly WaitForFixedUpdate m_waitForFixedUpdate = new WaitForFixedUpdate();

		// 06002937 (1.10.1 RVA 0x5CDE90)
		protected void OnEnable()
		{
			StartCoroutine(UpdateCoroutine());
		}

		// 06002938 (1.10.1 RVA 0x5CDF70): stops a fresh iterator, which is not the one OnEnable
		// started; disabling the component alone leaves the running loop going.
		protected void OnDisable()
		{
			StopCoroutine(UpdateCoroutine());
		}

		// 06002939 (1.10.1 RVA 0x5CDF00) with the iterator 0600293c..41: the loop ends once
		// the TimeManager row is gone.
		private IEnumerator UpdateCoroutine()
		{
			while (!s_timeManagerRef.IsNull())
			{
				IncrementValue(s_timeManagerRef.Get().GetFixedDeltaTime(timeCategory));
				yield return m_waitForFixedUpdate;
			}
		}
	}
}
